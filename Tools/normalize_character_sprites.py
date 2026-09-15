"""캐릭터 PNG의 배경을 투명화하고 Unity용 캔버스로 정규화한다.

스탠딩: 1024x1536, 초상화: 1024x1024.
외곽과 닫힌 밝은 배경을 분리해 제거하되 캐릭터의 흰색 의상은 보존한다.
"""

from __future__ import annotations

import argparse
import hashlib
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
STANDINGS = ROOT / "Assets/Resources/Sprite/Standings"
PORTRAITS = ROOT / "Assets/Resources/Sprite/Portraits"

# 인간형 초상화는 머리부터 허리선까지의 상반신만 담는다. 이전 0.50 비율은
# 니콜처럼 세로로 긴 전신 원화에서 허벅지까지 노출되어 카드 초상화가 작아 보였다.
TORSO_PORTRAIT_HEIGHT_RATIO = 0.40

SPRITE_CATEGORY_DIRECTORIES = {
    "ally": Path("Allies"),
    "normal": Path("Enemies/Normal"),
    "elite": Path("Enemies/Elite"),
    "boss": Path("Enemies/Boss"),
}

FULL_FIGURE_PORTRAIT_ASSETS = {
    "VOID_MONSTROUS_BIRD_STANDING.png",
    "VOID_SEED_STANDING.png",
    "APOCALYPSE_SEED_STANDING.png",
    "FROST_SEED_STANDING.png",
}

# 생성 원본이 피사체 알파와 함께 매우 낮은 불투명도의 조명 배경까지 포함한 경우다.
# 피사체는 대부분 alpha 250 이상이므로 낮은 알파만 제거하고 가장자리는 다시 매핑한다.
LOW_OPACITY_BACKDROP_ASSETS = {
    "RAGNAR_STANDING.png",
    "THOR_STANDING.png",
    "NORN_STANDING.png",
    "VALKYRIE_STANDING.png",
    "VOID_MARKSMAN_STANDING.png",
    "APOCALYPSE_MARKSMAN_STANDING.png",
    "VOID_MARKSMAN_NATURE_STANDING.png",
    "APOCALYPSE_MARKSMAN_NATURE_STANDING.png",
    "VOID_VANGUARD_STANDING.png",
    "APOCALYPSE_VANGUARD_STANDING.png",
    "VOID_VANGUARD_TORRENT_STANDING.png",
    "APOCALYPSE_VANGUARD_TORRENT_STANDING.png",
    "VOID_KNIGHT_STANDING.png",
    "VOID_WOLF_STANDING.png",
    "VOID_CRUSHER_STANDING.png",
    "APOCALYPSE_BEAST_STANDING.png",
    "VOID_KNIGHT_ELECTRO_STANDING.png",
    "APOCALYPSE_BEAST_CONDUCTION_STANDING.png",
}

# 원화는 우향으로 제공되었지만 적 스프라이트는 화면 왼쪽을 바라보는 것이 규칙이다.
LEFT_FACING_FLIP_ASSETS = {
    "VOID_MONSTROUS_BIRD_STANDING.png",
    "VOID_MARKSMAN_STANDING.png",
    "VOID_MARKSMAN_NATURE_STANDING.png",
}

NEW_ASSETS = {
    "AZTEC_JAGUAR": Path.home() / "Downloads/재규어 전사.png",
    "AZTEC_ELITE_JAGUAR": Path.home() / "Downloads/정예 재규어 전사.png",
    "AZTEC_EAGLE": Path.home() / "Downloads/독수리 전사.png",
    "AZTEC_ELITE_EAGLE": Path.home() / "Downloads/정예 독수리 전사.png",
    "AZTEC_SERPENT_PRIEST": Path.home() / "Downloads/뱀 사제.png",
    "AZTEC_ELITE_SERPENT_PRIEST": Path.home() / "Downloads/정예 뱀 사제.png",
    "AZTEC_TEZCATLIPOCA": Path.home() / "Downloads/테스카틀리포카.png",
}

NEW_ASSET_CATEGORIES = {
    "AZTEC_JAGUAR": "normal",
    "AZTEC_ELITE_JAGUAR": "elite",
    "AZTEC_EAGLE": "normal",
    "AZTEC_ELITE_EAGLE": "elite",
    "AZTEC_SERPENT_PRIEST": "normal",
    "AZTEC_ELITE_SERPENT_PRIEST": "elite",
    "AZTEC_TEZCATLIPOCA": "boss",
}

# 인물·장비가 흰 배경을 둘러싸 플러드필이 닿지 못했던 스탠딩이다.
# 흰 의상 캐릭터는 의상 손실을 막기 위해 이 목록에 넣지 않는다.
ENCLOSED_WHITE_BACKGROUND_ASSETS = {
    "AZTEC_EAGLE_STANDING.png",
    "AZTEC_ELITE_EAGLE_STANDING.png",
    "AZTEC_ELITE_SERPENT_PRIEST_STANDING.png",
    "AZTEC_JAGUAR_STANDING.png",
    "AZTEC_SERPENT_PRIEST_STANDING.png",
    "AZTEC_TEZCATLIPOCA_STANDING.png",
    "CENTURION_STANDING.png",
    "HASTATI_STANDING.png",
    "HOPLOMACHUS_STANDING.png",
    "MARCELLUS_STANDING.png",
    "MURMILLO_STANDING.png",
    "PRINCIPES_STANDING.png",
    "RETIARIUS_STANDING.png",
    "SABINA_STANDING.png",
    "SCORPIO_STANDING.png",
    "SECUTOR_STANDING.png",
    "SPARTACUS_STANDING.png",
    "THRAEX_STANDING.png",
    "TRIARII_STANDING.png",
    "VELITES_STANDING.png",
    "YAMA_STANDING.png",
    "INDRA_STANDING.png",
}

NEIGHBORS = (
    (-1, -1), (0, -1), (1, -1),
    (-1, 0),            (1, 0),
    (-1, 1),  (0, 1),  (1, 1),
)


def connected_background_alpha(image: Image.Image, threshold: int = 72) -> Image.Image:
    rgba = image.convert("RGBA")
    existing = np.asarray(rgba.getchannel("A"), dtype=np.uint8)
    if existing.min() < existing.max():
        return rgba

    rgb = image.convert("RGB")
    work = rgb.copy()
    marker = (255, 0, 255)
    ImageDraw.floodfill(work, (0, 0), marker, thresh=threshold)
    work_array = np.asarray(work)
    background = np.all(work_array == marker, axis=2)

    # 모서리가 피사체인 예외를 대비해 나머지 모서리에서도 밝은 배경만 확장한다.
    if background.mean() < 0.08:
        for point in ((rgb.width - 1, 0), (0, rgb.height - 1), (rgb.width - 1, rgb.height - 1)):
            ImageDraw.floodfill(work, point, marker, thresh=threshold)
        work_array = np.asarray(work)
        background = np.all(work_array == marker, axis=2)

    source = np.asarray(rgb, dtype=np.float32)
    corner_samples = np.concatenate(
        (source[:8, :8].reshape(-1, 3), source[:8, -8:].reshape(-1, 3),
         source[-8:, :8].reshape(-1, 3), source[-8:, -8:].reshape(-1, 3)), axis=0)
    background_color = np.median(corner_samples, axis=0)

    alpha = np.where(background, 0, 255).astype(np.uint8)
    # 배경에 닿는 2px 경계만 소프트 알파로 만들어 흰 테두리를 줄인다.
    ring = background.copy()
    for _ in range(2):
        expanded = ring.copy()
        expanded[1:] |= ring[:-1]
        expanded[:-1] |= ring[1:]
        expanded[:, 1:] |= ring[:, :-1]
        expanded[:, :-1] |= ring[:, 1:]
        ring = expanded
    edge = ring & ~background
    distance = np.linalg.norm(source - background_color, axis=2)
    edge_alpha = np.clip(distance / max(1, threshold) * 255.0, 0, 255).astype(np.uint8)
    alpha[edge] = np.minimum(alpha[edge], edge_alpha[edge])

    output = np.dstack((source.astype(np.uint8), alpha))
    return Image.fromarray(output, "RGBA")


def remove_low_opacity_backdrop(image: Image.Image, asset_name: str) -> Image.Image:
    """원본에 남은 저불투명도 그라데이션은 제거하고 피사체 가장자리만 보존한다."""
    rgba = image.convert("RGBA")
    if (
        asset_name not in LOW_OPACITY_BACKDROP_ASSETS
        and not asset_name.startswith((
            "VOID_MONSTROUS_BIRD", "VOID_PRISM", "VOID_BOAR",
            "VOID_KNIGHT", "VOID_WOLF", "VOID_CRUSHER", "VOID_DEER",
            "VOID_DRAGON", "VOID_MARKSMAN", "VOID_VANGUARD",
            "LIGHT",
        ))
    ):
        return rgba

    array = np.array(rgba, dtype=np.uint8)
    alpha = array[:, :, 3].astype(np.float32)
    # alpha 128 이하는 배경으로 제거하고, 128~251은 안티앨리어싱으로 재매핑한다.
    remapped = np.clip((alpha - 128.0) / 123.0 * 255.0, 0.0, 255.0).astype(np.uint8)
    array[:, :, 3] = remapped
    return Image.fromarray(array, "RGBA")


def remove_small_detached_fragments(image: Image.Image, asset_name: str) -> Image.Image:
    """용 생성본의 본체와 분리된 미세한 밝은 잔여 픽셀만 제거한다."""
    rgba = image.convert("RGBA")
    if not asset_name.startswith("VOID_DRAGON"):
        return rgba

    array = np.array(rgba, dtype=np.uint8)
    alpha = array[:, :, 3]
    components = _components(alpha > 12)
    if not components:
        return rgba

    largest_size = max(len(component) for component in components)
    threshold = max(32, int(largest_size * 0.00025))
    for component in components:
        if len(component) >= threshold:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        array[ys, xs, 3] = 0
    return Image.fromarray(array, "RGBA")


def _components(mask: np.ndarray) -> list[list[tuple[int, int]]]:
    """8방향 bool 마스크의 연결 성분을 반환한다. 외부 영상 의존성 없이 누끼 도구만으로 재실행한다."""
    height, width = mask.shape
    visited = np.zeros(mask.shape, dtype=bool)
    components: list[list[tuple[int, int]]] = []

    for start_y, start_x in np.argwhere(mask):
        if visited[start_y, start_x]:
            continue

        visited[start_y, start_x] = True
        queue = deque([(int(start_x), int(start_y))])
        component: list[tuple[int, int]] = []
        while queue:
            x, y = queue.popleft()
            component.append((x, y))
            for dx, dy in NEIGHBORS:
                nx, ny = x + dx, y + dy
                if 0 <= nx < width and 0 <= ny < height and mask[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((nx, ny))
        components.append(component)

    return components


def remove_enclosed_white_background(image: Image.Image, asset_name: str) -> Image.Image:
    """인물에 둘러싸인 균일 흰 배경과 그 안티앨리어싱 테두리만 투명화한다."""
    rgba = image.convert("RGBA")
    if asset_name not in ENCLOSED_WHITE_BACKGROUND_ASSETS:
        return rgba

    array = np.array(rgba, dtype=np.uint8)
    rgb = array[:, :, :3].astype(np.float32)
    alpha = array[:, :, 3]
    low = rgb.min(axis=2)
    high = rgb.max(axis=2)
    chroma = high - low

    # 균일한 흰 배경의 중심부. 질감·음영이 있는 흰 의상은 통과하지 않는다.
    core = (alpha > 12) & (low >= 225) & (chroma <= 35)
    selected = np.zeros(core.shape, dtype=bool)
    for component in _components(core):
        if len(component) < 300:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        pixels = rgb[ys, xs]
        mean = float(pixels.mean())
        nearly_white_ratio = float((pixels.min(axis=1) >= 245).mean())
        if mean >= 247 and nearly_white_ratio >= 0.75:
            selected[ys, xs] = True

    if not selected.any():
        return rgba

    # 선택된 흰 중심부에서만 더 어두운 안티앨리어싱 영역으로 확장한다.
    broad = (alpha > 0) & (low >= 175) & (chroma <= 55)
    grown = selected.copy()
    queue = deque((int(x), int(y)) for y, x in np.argwhere(selected))
    while queue:
        x, y = queue.popleft()
        for dx, dy in NEIGHBORS:
            nx, ny = x + dx, y + dy
            if 0 <= nx < rgba.width and 0 <= ny < rgba.height and broad[ny, nx] and not grown[ny, nx]:
                grown[ny, nx] = True
                queue.append((nx, ny))

    white_distance = np.linalg.norm(255.0 - rgb, axis=2)
    matte = np.clip((white_distance - 14.0) / 42.0 * 255.0, 0, 255).astype(np.uint8)
    array[:, :, 3][grown] = np.minimum(alpha[grown], matte[grown])
    return Image.fromarray(array, "RGBA")


def remove_large_checkerboard_residue(image: Image.Image, asset_name: str) -> Image.Image:
    """혹한의 거인 원화 내부에만 남은 대형 무채색 체크무늬 성분을 제거한다."""
    rgba = image.convert("RGBA")
    if asset_name != "FROST_GIANT_STANDING.png":
        return rgba

    array = np.array(rgba, dtype=np.uint8)
    rgb = array[:, :, :3]
    alpha = array[:, :, 3]
    neutral_bright = (
        (alpha > 250)
        & (rgb.max(axis=2).astype(np.int16) - rgb.min(axis=2).astype(np.int16) <= 2)
        & (rgb.min(axis=2) >= 238)
    )

    # 체크무늬는 망토 아래에서 수만 픽셀짜리 성분으로 이어진다. 흰 장갑판의
    # 개별 면은 검은 선으로 끊겨 이 크기에 미치지 못하므로 그대로 보존된다.
    for component in _components(neutral_bright):
        if len(component) < 15000:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        array[ys, xs, 3] = 0

    return Image.fromarray(array, "RGBA")


def remove_generated_checkerboard_residue(
    image: Image.Image, asset_name: str, aggressive: bool = False,
) -> Image.Image:
    """생성된 공허 계열 원화의 닫힌 장식 안에 남은 투명 격자를 제거한다."""
    rgba = image.convert("RGBA")
    if not asset_name.startswith((
        "VOID_BOAR", "VOID_KNIGHT", "VOID_WOLF", "VOID_CRUSHER", "VOID_DEER",
        "VOID_DRAGON", "VOID_MARKSMAN", "VOID_VANGUARD",
    )):
        return rgba

    array = np.array(rgba, dtype=np.uint8)
    rgb = array[:, :, :3].astype(np.int16)
    alpha = array[:, :, 3]
    height, width = alpha.shape
    yy, xx = np.indices((height, width))
    gray = rgb.mean(axis=2)
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    expected = np.where(((xx // 24 + yy // 24) % 2) == 0, 253, 246)
    patterned = (
        (alpha > 12)
        & (chroma <= 5)
        & (np.abs(gray - expected) <= 7)
    )

    # 갑각의 밝은 면도 일부 조건에 걸릴 수 있으므로, 여러 격자 칸이 이어진
    # 큰 영역만 시드로 삼는다. 현재 원화에서는 환형 장식 내부만 이 크기에 닿는다.
    selected = np.zeros(patterned.shape, dtype=bool)
    for component in _components(patterned):
        if len(component) < 5000:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        selected[ys, xs] = True

    if aggressive:
        # ImageGen 출력마다 어두운 격자색(약 234~246)이 달라진다. 외곽에서 실제
        # 배경의 두 명도 봉우리를 구한 뒤, 같은 반복색이 과반인 큰 내부 면만 고른다.
        exterior = (alpha == 0) & (chroma <= 8) & (rgb.min(axis=2) >= 220)
        exterior_gray = np.rint(gray[exterior]).astype(np.int16)
        if exterior_gray.size:
            histogram = np.bincount(exterior_gray, minlength=256)
            first_peak = int(histogram.argmax())
            suppressed = histogram.copy()
            suppressed[max(0, first_peak - 4):min(256, first_peak + 5)] = 0
            second_peak = int(suppressed.argmax())
            broad_seed = (alpha > 12) & (chroma <= 8) & (rgb.min(axis=2) >= 220)
            repeated = broad_seed & (
                (np.abs(gray - first_peak) <= 3) | (np.abs(gray - second_peak) <= 3)
            )
            for component in _components(broad_seed):
                if len(component) < 900:
                    continue
                xs = np.fromiter((point[0] for point in component), dtype=np.int32)
                ys = np.fromiter((point[1] for point in component), dtype=np.int32)
                if repeated[ys, xs].mean() < 0.55:
                    continue
                selected[ys, xs] = True

    if not selected.any():
        return rgba

    # 격자 셀의 미세한 노이즈와 경계만 확장 제거한다. 금색·백색 갑각은
    # 채도와 명도 조건에서 끊기므로 보존된다.
    broad = (alpha > 0) & (rgb.min(axis=2) >= 220) & (chroma <= 18)
    grown = selected.copy()
    queue = deque((int(x), int(y)) for y, x in np.argwhere(selected))
    while queue:
        x, y = queue.popleft()
        for dx, dy in NEIGHBORS:
            nx, ny = x + dx, y + dy
            if 0 <= nx < width and 0 <= ny < height and broad[ny, nx] and not grown[ny, nx]:
                grown[ny, nx] = True
                queue.append((nx, ny))

    array[:, :, 3][grown] = 0
    return Image.fromarray(array, "RGBA")


def apply_palette_swap_reference_alpha(image: Image.Image, asset_name: str) -> Image.Image:
    """팔레트 스왑은 원본 실루엣 알파를 재사용해 닫힌 체크 배경까지 제거한다."""
    if asset_name != "RUIN_INQUISITOR_STANDING.png":
        return image.convert("RGBA")

    reference_path = find_sprite_path(STANDINGS, "APOCALYPSE_INQUISITOR_STANDING.png")
    with Image.open(reference_path) as loaded:
        reference = loaded.convert("RGBA")

    target = image.convert("RGBA")
    target_box = alpha_bbox(target)
    reference_box = alpha_bbox(reference)
    reference_alpha = reference.getchannel("A").crop(reference_box).resize(
        (target_box[2] - target_box[0], target_box[3] - target_box[1]),
        Image.Resampling.LANCZOS,
    )
    alpha = Image.new("L", target.size, 0)
    alpha.paste(reference_alpha, (target_box[0], target_box[1]))
    target.putalpha(alpha)
    return target


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = np.asarray(image.getchannel("A"))
    ys, xs = np.where(alpha > 12)
    if len(xs) == 0:
        return (0, 0, image.width, image.height)
    return (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)


def fit_to_canvas(image: Image.Image, size: tuple[int, int], margin: tuple[int, int], bottom_align: bool) -> Image.Image:
    crop = image.crop(alpha_bbox(image))
    max_width = size[0] - margin[0] * 2
    max_height = size[1] - margin[1] * 2
    scale = min(max_width / crop.width, max_height / crop.height)
    resized = crop.resize((max(1, round(crop.width * scale)), max(1, round(crop.height * scale))), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - resized.width) // 2
    y = size[1] - margin[1] - resized.height if bottom_align else (size[1] - resized.height) // 2
    canvas.alpha_composite(resized, (x, y))
    return canvas


def remove_crop_boundary_fragments(image: Image.Image) -> Image.Image:
    """초상화 크롭 경계에서 잘려 고립된 무기·장식 조각만 제거한다."""
    rgba = image.convert("RGBA")
    alpha = np.asarray(rgba.getchannel("A"))
    components = _components(alpha > 12)
    if len(components) <= 1:
        return rgba

    largest_size = max(len(component) for component in components)
    output = np.array(rgba, dtype=np.uint8)
    for component in components:
        if len(component) >= largest_size * 0.05:
            continue
        touches_crop_edge = any(
            x <= 1 or y <= 1 or x >= rgba.width - 2 or y >= rgba.height - 2
            for x, y in component
        )
        if not touches_crop_edge:
            continue
        xs = np.fromiter((point[0] for point in component), dtype=np.int32)
        ys = np.fromiter((point[1] for point in component), dtype=np.int32)
        output[ys, xs, 3] = 0
    return Image.fromarray(output, "RGBA")


def portrait_from_standing(standing: Image.Image, full_figure: bool = False) -> Image.Image:
    if full_figure:
        return fit_to_canvas(standing.crop(alpha_bbox(standing)), (1024, 1024), (42, 42), bottom_align=False)

    left, top, right, bottom = alpha_bbox(standing)
    height = bottom - top
    upper_bottom = min(bottom, top + round(height * TORSO_PORTRAIT_HEIGHT_RATIO))
    alpha = np.asarray(standing.getchannel("A"))
    ys, xs = np.where(alpha[top:upper_bottom] > 12)
    # Y만 잘라 조사했으므로 xs는 이미 캔버스의 절대 X 좌표다. left를 다시 더하면
    # 니콜·가우디처럼 폭이 넓은 상반신이 우측으로 밀려 왼팔과 후드가 잘린다.
    center_x = int(np.median(xs)) if len(xs) else (left + right) // 2
    crop_size = max(1, round(height * TORSO_PORTRAIT_HEIGHT_RATIO))
    crop_left = max(0, min(standing.width - crop_size, center_x - crop_size // 2))
    crop_top = max(0, top)
    crop = standing.crop((crop_left, crop_top, min(standing.width, crop_left + crop_size), min(standing.height, crop_top + crop_size)))
    crop = remove_crop_boundary_fragments(crop)
    return fit_to_canvas(crop, (1024, 1024), (42, 42), bottom_align=False)


def is_full_figure_portrait(asset_name: str) -> bool:
    return (
        asset_name in FULL_FIGURE_PORTRAIT_ASSETS
        or asset_name.startswith((
            "VOID_MONSTROUS_BIRD", "VOID_PRISM", "VOID_BOAR",
            "VOID_KNIGHT", "VOID_WOLF", "VOID_CRUSHER", "VOID_DEER",
            "VOID_DRAGON",
        ))
    )


def unity_meta(path: Path) -> None:
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    guid = hashlib.md5(path.as_posix().encode("utf-8")).hexdigest()
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextureImporter:\n"
        "  internalIDToNameTable: []\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 13\n"
        "  mipmaps:\n"
        "    mipMapMode: 0\n"
        "    enableMipMap: 0\n"
        "  isReadable: 0\n"
        "  streamingMipmaps: 0\n"
        "  sRGBTexture: 1\n"
        "  alphaSource: 1\n"
        "  alphaIsTransparency: 1\n"
        "  spriteMode: 1\n"
        "  spritePixelsToUnits: 100\n"
        "  spritePivot: {x: 0.5, y: 0.5}\n"
        "  textureType: 8\n"
        "  textureShape: 1\n"
        "  maxTextureSize: 2048\n"
        "  textureCompression: 1\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n",
        encoding="utf-8",
    )


def save_png(image: Image.Image, path: Path) -> None:
    """Windows에서도 열려 있던 원본을 안전하게 교체한다."""
    rgba = np.array(image.convert("RGBA"), dtype=np.uint8)
    rgba[:, :, :3][rgba[:, :, 3] == 0] = 0
    temporary = path.with_name(f".{path.stem}.tmp.png")
    Image.fromarray(rgba, "RGBA").save(temporary, format="PNG")
    temporary.replace(path)


def find_sprite_path(root: Path, filename: str) -> Path:
    """분류 폴더 전체에서 파일명 하나를 찾고 중복 키를 즉시 드러낸다."""
    matches = sorted(root.rglob(filename))
    if not matches:
        raise FileNotFoundError(f"스프라이트 누락: {root / filename}")
    if len(matches) > 1:
        joined = ", ".join(str(path) for path in matches)
        raise RuntimeError(f"중복 스프라이트 키 {filename}: {joined}")
    return matches[0]


def category_root(root: Path, category: str) -> Path:
    try:
        directory = SPRITE_CATEGORY_DIRECTORIES[category]
    except KeyError as error:
        raise ValueError(f"알 수 없는 스프라이트 분류: {category}") from error
    destination = root / directory
    destination.mkdir(parents=True, exist_ok=True)
    return destination


def normalize_existing() -> None:
    for path in sorted(STANDINGS.rglob("*.png")):
        with Image.open(path) as loaded:
            current = loaded.copy()
        if current.mode != "RGBA" or current.size != (1024, 1536) or current.getchannel("A").getextrema() != (0, 255):
            current = fit_to_canvas(connected_background_alpha(current), (1024, 1536), (54, 38), bottom_align=True)
        current = remove_enclosed_white_background(current, path.name)
        current = remove_large_checkerboard_residue(current, path.name)
        save_png(remove_small_detached_fragments(current, path.name), path)

    # 개별 초상화에 남은 사각형·원형 흰 배경을 재사용하지 않는다.
    # 검수된 스탠딩 알파에서 같은 규격으로 다시 잘라 모든 초상화의 누끼 품질을 맞춘다.
    for standing_path in sorted(STANDINGS.rglob("*_STANDING.png")):
        relative = standing_path.relative_to(STANDINGS)
        portrait_path = PORTRAITS / relative.with_name(
            standing_path.name.replace("_STANDING.png", "_PORTRAIT.png"))
        portrait_path.parent.mkdir(parents=True, exist_ok=True)
        with Image.open(standing_path) as loaded:
            portrait = portrait_from_standing(
                loaded.convert("RGBA"), is_full_figure_portrait(standing_path.name))
        save_png(portrait, portrait_path)
        unity_meta(portrait_path)


def import_assets(assets: dict[str, Path], category: str = "ally") -> None:
    missing = [str(path) for path in assets.values() if not path.exists()]
    if missing:
        raise FileNotFoundError("신규 원본 누락: " + ", ".join(missing))

    standing_root = category_root(STANDINGS, category)
    portrait_root = category_root(PORTRAITS, category)
    for asset_name, source in assets.items():
        with Image.open(source) as loaded:
            source_was_opaque = loaded.convert("RGBA").getchannel("A").getextrema() == (255, 255)
            transparent = connected_background_alpha(loaded)
        standing_path = standing_root / f"{asset_name}_STANDING.png"
        portrait_path = portrait_root / f"{asset_name}_PORTRAIT.png"
        transparent = remove_generated_checkerboard_residue(
            transparent, standing_path.name, aggressive=source_was_opaque)
        transparent = remove_low_opacity_backdrop(transparent, standing_path.name)
        transparent = remove_small_detached_fragments(transparent, standing_path.name)
        if standing_path.name in LEFT_FACING_FLIP_ASSETS:
            transparent = transparent.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        standing = fit_to_canvas(transparent, (1024, 1536), (54, 38), bottom_align=True)
        standing = remove_enclosed_white_background(standing, standing_path.name)
        standing = remove_large_checkerboard_residue(standing, standing_path.name)
        standing = apply_palette_swap_reference_alpha(standing, standing_path.name)
        save_png(standing, standing_path)
        save_png(portrait_from_standing(
            standing, is_full_figure_portrait(standing_path.name)), portrait_path)
        unity_meta(standing_path)
        unity_meta(portrait_path)


def import_directional_assets(assets: dict[str, Path], category: str = "ally") -> None:
    """우향 원본에서 기본 좌향과 UI/연출용 우향 한 쌍을 같은 규격으로 등록한다."""
    missing = [str(path) for path in assets.values() if not path.exists()]
    if missing:
        raise FileNotFoundError("방향별 원본 누락: " + ", ".join(missing))

    standing_root = category_root(STANDINGS, category)
    portrait_root = category_root(PORTRAITS, category)
    for asset_name, source in assets.items():
        default_standing_path = standing_root / f"{asset_name}_STANDING.png"
        with Image.open(source) as loaded:
            right_source = connected_background_alpha(loaded)
        right_source = remove_low_opacity_backdrop(right_source, default_standing_path.name)
        right_standing = fit_to_canvas(right_source, (1024, 1536), (54, 38), bottom_align=True)
        left_standing = right_standing.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        right_portrait = portrait_from_standing(
            right_standing, is_full_figure_portrait(f"{asset_name}_RIGHT_STANDING.png"))
        left_portrait = right_portrait.transpose(Image.Transpose.FLIP_LEFT_RIGHT)

        for direction, standing, portrait in (
            ("", left_standing, left_portrait),
            ("_RIGHT", right_standing, right_portrait),
        ):
            standing_path = standing_root / f"{asset_name}{direction}_STANDING.png"
            portrait_path = portrait_root / f"{asset_name}{direction}_PORTRAIT.png"
            save_png(standing, standing_path)
            save_png(portrait, portrait_path)
            unity_meta(standing_path)
            unity_meta(portrait_path)


def import_new() -> None:
    for category in SPRITE_CATEGORY_DIRECTORIES:
        assets = {
            key: path for key, path in NEW_ASSETS.items()
            if NEW_ASSET_CATEGORIES[key] == category
        }
        if assets:
            import_assets(assets, category)


def parse_asset_arguments(arguments: list[str]) -> dict[str, Path]:
    assets: dict[str, Path] = {}
    for argument in arguments:
        if "=" not in argument:
            raise ValueError(f"--asset 형식은 KEY=PNG_PATH 이어야 합니다: {argument}")
        asset_name, source = argument.split("=", 1)
        asset_name = asset_name.strip().upper()
        if not asset_name or not source.strip():
            raise ValueError(f"--asset 형식은 KEY=PNG_PATH 이어야 합니다: {argument}")
        assets[asset_name] = Path(source.strip()).expanduser()
    return assets


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--existing", action="store_true", help="현재 등록된 스프라이트 전체의 알파 및 규격 정규화")
    parser.add_argument("--new", action="store_true", help="Downloads의 아즈텍 원본 7장을 스탠딩/초상화로 등록")
    parser.add_argument(
        "--asset",
        action="append",
        default=[],
        metavar="KEY=PNG_PATH",
        help="지정한 원본을 전용 스탠딩/초상화로 등록(여러 번 사용 가능)",
    )
    parser.add_argument(
        "--directional-asset",
        action="append",
        default=[],
        metavar="KEY=PNG_PATH",
        help="우향 원본에서 기본 좌향과 _RIGHT 우향 스탠딩/초상화를 함께 등록",
    )
    parser.add_argument(
        "--category",
        choices=tuple(SPRITE_CATEGORY_DIRECTORIES),
        default="ally",
        help="--asset/--directional-asset 저장 분류(기본값: ally)",
    )
    args = parser.parse_args()
    if not args.existing and not args.new and not args.asset and not args.directional_asset:
        args.existing = args.new = True
    if args.existing:
        normalize_existing()
    if args.new:
        import_new()
    if args.asset:
        import_assets(parse_asset_arguments(args.asset), args.category)
    if args.directional_asset:
        import_directional_assets(parse_asset_arguments(args.directional_asset), args.category)


if __name__ == "__main__":
    main()
