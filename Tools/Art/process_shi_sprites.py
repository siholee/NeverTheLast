"""Prepare locally generated WebUI images as aligned transparent Unity sprites."""

from collections import deque
from pathlib import Path
import argparse

from PIL import Image, ImageDraw, ImageFilter


CANVAS_SIZE = 512
CONTENT_WIDTH = 474
CONTENT_HEIGHT = 448
BASELINE_Y = 494


def is_background(pixel: tuple[int, int, int, int]) -> bool:
    red, green, blue, _ = pixel
    return min(red, green, blue) >= 232 and max(red, green, blue) - min(red, green, blue) <= 22


def edge_background_mask(image: Image.Image) -> list[bool]:
    width, height = image.size
    pixels = image.load()
    visited = [False] * (width * height)
    queue: deque[tuple[int, int]] = deque()

    def enqueue(x: int, y: int) -> None:
        index = y * width + x
        if visited[index] or not is_background(pixels[x, y]):
            return
        visited[index] = True
        queue.append((x, y))

    for x in range(width):
        enqueue(x, 0)
        enqueue(x, height - 1)
    for y in range(height):
        enqueue(0, y)
        enqueue(width - 1, y)

    while queue:
        x, y = queue.popleft()
        if x > 0:
            enqueue(x - 1, y)
        if x + 1 < width:
            enqueue(x + 1, y)
        if y > 0:
            enqueue(x, y - 1)
        if y + 1 < height:
            enqueue(x, y + 1)

    return visited


def largest_foreground_component(image: Image.Image) -> set[tuple[int, int]]:
    width, height = image.size
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    visited: set[tuple[int, int]] = set()
    largest: set[tuple[int, int]] = set()

    for y in range(height):
        for x in range(width):
            if alpha_pixels[x, y] == 0 or (x, y) in visited:
                continue

            component: set[tuple[int, int]] = set()
            queue = deque([(x, y)])
            visited.add((x, y))
            while queue:
                current_x, current_y = queue.popleft()
                component.add((current_x, current_y))
                for next_x, next_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if not (0 <= next_x < width and 0 <= next_y < height):
                        continue
                    point = (next_x, next_y)
                    if point in visited or alpha_pixels[next_x, next_y] == 0:
                        continue
                    visited.add(point)
                    queue.append(point)

            if len(component) > len(largest):
                largest = component

    return largest


def recolor_cyan_eyes(image: Image.Image) -> None:
    pixels = image.load()
    width, height = image.size
    for y in range(int(height * 0.1), int(height * 0.5)):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            if alpha == 0:
                continue
            if blue > 125 and green > 105 and blue > red * 1.18 and green > red * 1.12:
                intensity = max(green, blue)
                pixels[x, y] = (min(255, int(intensity * 1.18)), max(28, int(red * 0.45)), max(35, int(red * 0.55)), alpha)


def remove_white_fringe(image: Image.Image) -> None:
    pixels = image.load()
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    width, height = image.size
    clear: list[tuple[int, int]] = []
    for y in range(height):
        for x in range(width):
            red, green, blue, current_alpha = pixels[x, y]
            if current_alpha == 0 or min(red, green, blue) < 205:
                continue
            touches_transparency = False
            for offset_y in range(-2, 3):
                for offset_x in range(-2, 3):
                    next_x, next_y = x + offset_x, y + offset_y
                    if 0 <= next_x < width and 0 <= next_y < height and alpha_pixels[next_x, next_y] == 0:
                        touches_transparency = True
                        break
                if touches_transparency:
                    break
            if touches_transparency:
                clear.append((x, y))
    for x, y in clear:
        red, green, blue, _ = pixels[x, y]
        pixels[x, y] = (red, green, blue, 0)


def remove_ground_strokes(image: Image.Image) -> None:
    pixels = image.load()
    alpha = image.getchannel("A")
    alpha_pixels = alpha.load()
    width, height = image.size
    clear: list[tuple[int, int]] = []
    for y in range(475, height):
        for x in range(width):
            if alpha_pixels[x, y] == 0:
                continue
            has_vertical_body = False
            # Ground scribbles are several pixels thick, so do not use the rows
            # immediately above the candidate (they would support one another).
            for above_y in range(418, 456):
                for near_x in range(max(0, x - 2), min(width, x + 3)):
                    if alpha_pixels[near_x, above_y] > 0:
                        has_vertical_body = True
                        break
                if has_vertical_body:
                    break
            if not has_vertical_body:
                clear.append((x, y))
    for x, y in clear:
        red, green, blue, _ = pixels[x, y]
        pixels[x, y] = (red, green, blue, 0)


def apply_canonical_identity(image: Image.Image, canonical_path: Path) -> None:
    """Keep Shi's face, camellia, and crimson obi identical across generated poses."""
    canonical = Image.open(canonical_path).convert("RGBA").resize(image.size, Image.Resampling.LANCZOS)
    width, height = image.size

    head_mask = Image.new("L", image.size, 0)
    canonical_pixels = canonical.load()
    head_pixels = head_mask.load()
    for y in range(int(height * 0.03), int(height * 0.45)):
        for x in range(int(width * 0.18), int(width * 0.82)):
            red, green, blue, _ = canonical_pixels[x, y]
            if min(red, green, blue) < 245 or max(red, green, blue) - min(red, green, blue) > 8:
                head_pixels[x, y] = 255
    head_mask = head_mask.filter(ImageFilter.GaussianBlur(0.55))
    image.paste(canonical, mask=head_mask)

    # Central torso proportions match closely between the selected frames, so copying this
    # narrow region is more faithful than asking the model to reinvent the plain red obi.
    obi_mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(obi_mask).rounded_rectangle((278, 337, 478, 445), radius=16, fill=255)
    obi_mask = obi_mask.filter(ImageFilter.GaussianBlur(5))
    image.paste(canonical, mask=obi_mask)


def process(source: Path, destination: Path, red_eyes: bool, canonical: Path | None = None) -> None:
    image = Image.open(source).convert("RGBA")
    if canonical is not None:
        apply_canonical_identity(image, canonical)
    if red_eyes:
        recolor_cyan_eyes(image)

    width, height = image.size
    pixels = image.load()
    background = edge_background_mask(image)
    for y in range(height):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            if background[y * width + x]:
                pixels[x, y] = (red, green, blue, 0)
            else:
                # Near-white edge pixels become softly transparent instead of leaving a halo.
                whiteness = min(red, green, blue)
                if whiteness > 210:
                    alpha = min(alpha, max(0, (240 - whiteness) * 8))
                pixels[x, y] = (red, green, blue, alpha)

    main_component = largest_foreground_component(image)
    if not main_component:
        raise RuntimeError(f"No foreground found in {source}")

    keep = main_component
    alpha_pixels = image.getchannel("A").load()
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            if alpha_pixels[x, y] > 0 and (x, y) not in keep:
                red, green, blue, _ = pixels[x, y]
                pixels[x, y] = (red, green, blue, 0)
    remove_white_fringe(image)

    xs = [point[0] for point in keep]
    ys = [point[1] for point in keep]
    margin = 4
    crop_box = (
        max(0, min(xs) - margin),
        max(0, min(ys) - margin),
        min(width, max(xs) + margin + 1),
        min(height, max(ys) + margin + 1),
    )
    cropped = image.crop(crop_box)
    scale = min(CONTENT_WIDTH / cropped.width, CONTENT_HEIGHT / cropped.height)
    resized = cropped.resize(
        (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale))),
        Image.Resampling.LANCZOS,
    )

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    paste_x = (CANVAS_SIZE - resized.width) // 2
    paste_y = BASELINE_Y - resized.height
    canvas.alpha_composite(resized, (paste_x, paste_y))
    remove_ground_strokes(canvas)
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--red-eyes", action="store_true")
    parser.add_argument("--canonical", type=Path)
    args = parser.parse_args()
    process(args.source, args.destination, args.red_eyes, args.canonical)


if __name__ == "__main__":
    main()
