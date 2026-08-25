"""캐릭터 PNG의 배경을 투명화하고 Unity용 캔버스로 정규화한다.

스탠딩: 1024x1536, 초상화: 1024x1024.
밝은 단색 배경은 모서리에서 연결된 픽셀만 제거하므로 캐릭터 내부의 흰색 의상은 보존한다.
"""

from __future__ import annotations

import argparse
import hashlib
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


def normalize_existing() -> None:
    for path in sorted(STANDINGS.glob("*.png")):
        with Image.open(path) as loaded:
            current = loaded.copy()
        if current.mode == "RGBA" and current.size == (1024, 1536) and current.getchannel("A").getextrema() == (0, 255):
            continue
        transparent = connected_background_alpha(current)
        fit_to_canvas(transparent, (1024, 1536), (54, 38), bottom_align=True).save(path)
    for path in sorted(PORTRAITS.glob("*.png")):
        with Image.open(path) as loaded:
            current = loaded.copy()
        if current.mode == "RGBA" and current.size == (1024, 1024) and current.getchannel("A").getextrema() == (0, 255):
            continue
        transparent = connected_background_alpha(current)
        fit_to_canvas(transparent, (1024, 1024), (42, 42), bottom_align=False).save(path)


def import_new() -> None:
    missing = [str(path) for path in NEW_ASSETS.values() if not path.exists()]
    if missing:
        raise FileNotFoundError("신규 원본 누락: " + ", ".join(missing))

    for asset_name, source in NEW_ASSETS.items():
        transparent = connected_background_alpha(Image.open(source))
        standing = fit_to_canvas(transparent, (1024, 1536), (54, 38), bottom_align=True)
        standing_path = STANDINGS / f"{asset_name}_STANDING.png"
        portrait_path = PORTRAITS / f"{asset_name}_PORTRAIT.png"
        standing.save(standing_path)
        portrait_from_standing(standing).save(portrait_path)
        unity_meta(standing_path)
        unity_meta(portrait_path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--existing", action="store_true", help="현재 등록된 스프라이트 84장 정규화")
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
