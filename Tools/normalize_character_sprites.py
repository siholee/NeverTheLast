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

NEW_ASSETS = {
    "AZTEC_JAGUAR": Path.home() / "Downloads/재규어 전사.png",
    "AZTEC_ELITE_JAGUAR": Path.home() / "Downloads/정예 재규어 전사.png",
    "AZTEC_EAGLE": Path.home() / "Downloads/독수리 전사.png",
    "AZTEC_ELITE_EAGLE": Path.home() / "Downloads/정예 독수리 전사.png",
    "AZTEC_SERPENT_PRIEST": Path.home() / "Downloads/뱀 사제.png",
    "AZTEC_ELITE_SERPENT_PRIEST": Path.home() / "Downloads/정예 뱀 사제.png",
    "AZTEC_TEZCATLIPOCA": Path.home() / "Downloads/테스카틀리포카.png",
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


def portrait_from_standing(standing: Image.Image) -> Image.Image:
    left, top, right, bottom = alpha_bbox(standing)
    height = bottom - top
    upper_bottom = min(bottom, top + round(height * 0.52))
    alpha = np.asarray(standing.getchannel("A"))
    ys, xs = np.where(alpha[top:upper_bottom] > 12)
    center_x = int(np.median(xs + left)) if len(xs) else (left + right) // 2
    crop_size = max(1, round(height * 0.5))
    crop_left = max(0, min(standing.width - crop_size, center_x - crop_size // 2))
    crop_top = max(0, top)
    crop = standing.crop((crop_left, crop_top, min(standing.width, crop_left + crop_size), min(standing.height, crop_top + crop_size)))
    crop = remove_crop_boundary_fragments(crop)
    return fit_to_canvas(crop, (1024, 1024), (42, 42), bottom_align=False)


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
    temporary = path.with_name(f".{path.stem}.tmp.png")
    image.save(temporary, format="PNG")
    temporary.replace(path)


def normalize_existing() -> None:
    for path in sorted(STANDINGS.glob("*.png")):
        with Image.open(path) as loaded:
            current = loaded.copy()
        if current.mode != "RGBA" or current.size != (1024, 1536) or current.getchannel("A").getextrema() != (0, 255):
            current = fit_to_canvas(connected_background_alpha(current), (1024, 1536), (54, 38), bottom_align=True)
        save_png(remove_enclosed_white_background(current, path.name), path)

    # 개별 초상화에 남은 사각형·원형 흰 배경을 재사용하지 않는다.
    # 검수된 스탠딩 알파에서 같은 규격으로 다시 잘라 모든 초상화의 누끼 품질을 맞춘다.
    for standing_path in sorted(STANDINGS.glob("*_STANDING.png")):
        portrait_path = PORTRAITS / standing_path.name.replace("_STANDING.png", "_PORTRAIT.png")
        with Image.open(standing_path) as loaded:
            portrait = portrait_from_standing(loaded.convert("RGBA"))
        save_png(portrait, portrait_path)
        unity_meta(portrait_path)


def import_new() -> None:
    missing = [str(path) for path in NEW_ASSETS.values() if not path.exists()]
    if missing:
        raise FileNotFoundError("신규 원본 누락: " + ", ".join(missing))

    for asset_name, source in NEW_ASSETS.items():
        with Image.open(source) as loaded:
            transparent = connected_background_alpha(loaded)
        standing = fit_to_canvas(transparent, (1024, 1536), (54, 38), bottom_align=True)
        standing_path = STANDINGS / f"{asset_name}_STANDING.png"
        portrait_path = PORTRAITS / f"{asset_name}_PORTRAIT.png"
        standing = remove_enclosed_white_background(standing, standing_path.name)
        save_png(standing, standing_path)
        save_png(portrait_from_standing(standing), portrait_path)
        unity_meta(standing_path)
        unity_meta(portrait_path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--existing", action="store_true", help="현재 등록된 스프라이트 전체의 알파 및 규격 정규화")
    parser.add_argument("--new", action="store_true", help="Downloads의 아즈텍 원본 7장을 스탠딩/초상화로 등록")
    args = parser.parse_args()
    if not args.existing and not args.new:
        args.existing = args.new = True
    if args.existing:
        normalize_existing()
    if args.new:
        import_new()


if __name__ == "__main__":
    main()
