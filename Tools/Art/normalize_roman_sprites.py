"""Normalize Roman unit artwork into deterministic Unity sprite assets.

Normal enemies are grouped by shared pose/weapon family and fitted to the exact
same foreground box.  This removes the small scale and baseline differences that
make palette variants look like unrelated illustrations.  Named allies keep their
original aspect ratio while sharing the same canvas and ground line.
"""

from __future__ import annotations

import argparse
import hashlib
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops


CANVAS_SIZE = (1024, 2048)
PORTRAIT_SIZE = (1024, 1024)
WHITE = (255, 255, 255)
PORTRAIT_BOXES = {
    "spear": (260, 100, 960, 800),
    "crossbow": (240, 190, 960, 910),
    "command": (170, 50, 870, 750),
    "caster": (120, 60, 920, 860),
}


@dataclass(frozen=True)
class SpriteSpec:
    key: str
    source_name: str
    group: str
    exact_box: tuple[int, int, int, int] | None


SPECS = (
    # Palette families: every member is fitted to the exact same foreground box.
    SpriteSpec("HASTATI", "하스타티.png", "spear", (152, 60, 872, 1960)),
    SpriteSpec("PRINCIPES", "프린키페스.png", "spear", (152, 60, 872, 1960)),
    SpriteSpec("TRIARII", "트리아리.png", "spear", (152, 60, 872, 1960)),
    SpriteSpec("CENTURION", "센츄리온.png", "spear", (152, 60, 872, 1960)),
    SpriteSpec("VELITES", "벨리테스.png", "crossbow", (62, 210, 962, 1950)),
    SpriteSpec("SCORPIO", "스콜피오.png", "crossbow", (62, 210, 962, 1950)),
    SpriteSpec("OPTIO", "옵티오.png", "command", (152, 70, 872, 1970)),
    SpriteSpec("TRIBUNE", "트리뷴.png", "command", (152, 70, 872, 1970)),
    SpriteSpec("EQUITES", "에퀴테스.png", "caster", (62, 90, 962, 1970)),
    # Named allies/bosses: standard canvas and baseline, no aspect distortion.
    SpriteSpec("AGRIPPA", "아그리파.png", "unique", None),
    SpriteSpec("OCTAVIA", "옥타비아.png", "unique", None),
    SpriteSpec("CAESAR", "카이사르.png", "unique", None),
)


def foreground_bbox(image: Image.Image, threshold: int = 8) -> tuple[int, int, int, int]:
    rgb = image.convert("RGB")
    background = Image.new("RGB", rgb.size, WHITE)
    difference = ImageChops.difference(rgb, background).convert("L")
    mask = difference.point(lambda value: 255 if value > threshold else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("No foreground pixels were detected")
    return bbox


def resize_exact(image: Image.Image, target_box: tuple[int, int, int, int]) -> Image.Image:
    source_box = foreground_bbox(image)
    foreground = image.convert("RGB").crop(source_box)
    width = target_box[2] - target_box[0]
    height = target_box[3] - target_box[1]
    resized = foreground.resize((width, height), Image.Resampling.LANCZOS)
    result = Image.new("RGB", CANVAS_SIZE, WHITE)
    result.paste(resized, (target_box[0], target_box[1]))
    return result


def resize_unique(image: Image.Image) -> Image.Image:
    source_box = foreground_bbox(image)
    foreground = image.convert("RGB").crop(source_box)
    max_width, max_height = 900, 1900
    scale = min(max_width / foreground.width, max_height / foreground.height)
    size = (round(foreground.width * scale), round(foreground.height * scale))
    resized = foreground.resize(size, Image.Resampling.LANCZOS)
    x = (CANVAS_SIZE[0] - size[0]) // 2
    y = 1980 - size[1]
    result = Image.new("RGB", CANVAS_SIZE, WHITE)
    result.paste(resized, (x, y))
    return result


def save_portrait(standing: Image.Image, destination: Path, group: str) -> None:
    # Identical crop geometry for all generic enemies reinforces palette-family
    # continuity and avoids generative drift in helmets, weapons, and proportions.
    portrait = standing.crop(PORTRAIT_BOXES[group]).resize(PORTRAIT_SIZE, Image.Resampling.LANCZOS)
    portrait.save(destination, optimize=True)


def write_unity_meta(asset: Path, template: Path, project_root: Path) -> None:
    relative = asset.relative_to(project_root).as_posix()
    guid = hashlib.md5(f"NeverTheLast/{relative}".encode("utf-8")).hexdigest()
    lines = template.read_text(encoding="utf-8").splitlines()
    lines[1] = f"guid: {guid}"
    asset.with_suffix(asset.suffix + ".meta").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()

    standing_dir = args.project_root / "Assets/Resources/Sprite/Standings"
    portrait_dir = args.project_root / "Assets/Resources/Sprite/Portraits"
    standing_dir.mkdir(parents=True, exist_ok=True)
    portrait_dir.mkdir(parents=True, exist_ok=True)
    standing_meta_template = standing_dir / "AGNI_STANDING.png.meta"
    portrait_meta_template = portrait_dir / "AGNI_PORTRAIT.png.meta"

    for spec in SPECS:
        source = args.source_dir / spec.source_name
        if not source.is_file():
            raise FileNotFoundError(source)

        with Image.open(source) as image:
            standing = resize_exact(image, spec.exact_box) if spec.exact_box else resize_unique(image)

        standing_path = standing_dir / f"{spec.key}_STANDING.png"
        standing.save(standing_path, optimize=True)
        write_unity_meta(standing_path, standing_meta_template, args.project_root)

        if spec.group != "unique":
            portrait_path = portrait_dir / f"{spec.key}_PORTRAIT.png"
            save_portrait(standing, portrait_path, spec.group)
            write_unity_meta(portrait_path, portrait_meta_template, args.project_root)
        else:
            portrait_path = portrait_dir / f"{spec.key}_PORTRAIT.png"
            if portrait_path.is_file():
                write_unity_meta(portrait_path, portrait_meta_template, args.project_root)

        bbox = foreground_bbox(standing)
        print(f"{spec.key:10} {spec.group:8} canvas={standing.size} bbox={bbox}")


if __name__ == "__main__":
    main()
