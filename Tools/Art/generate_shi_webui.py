"""Generate Shi SD-sprite candidates through the local AUTOMATIC1111 WebUI API."""

from __future__ import annotations

import argparse
import base64
import json
import urllib.request
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


BASE_PROMPT = r"""
748cmstyle, Cute_niji_style, <lora:iLLMythSmo0thL1nes:0.35>,
<lora:Niji_cute_style_illustrious:0.9>, masterpiece, best quality,
Arknights-inspired tactical RPG SD character sprite, super deformed adult woman,
2.5 heads tall, same character design as the reference image, full body, single subject,
calm stoic red almond eyes, pale skin, short glossy blue-black hair gathered into a small low bun,
soft center-parted bangs, two long loose side strands,
one red camellia flower and a thin gold hairpin on the viewer-right side of her hair,
two short red tassels hanging from the flower,
formal floor-length black tomesode kimono, charcoal satin highlights, long wide sleeves,
white inner collar, thin crimson piping along the collar and center opening,
wide solid crimson obi with a narrow muted-gold top edge and small black floral cord ornaments,
legs and ankles fully covered by the long kimono, no rear bow,
one black lacquer scabbard at her left hip, dark purple diamond-wrapped katana handle, brass pommel,
clean precise silhouette, restrained elegant Japanese swordswoman, orthographic game sprite,
three-quarter front view facing slightly right, centered, simple pure white background
"""

POSES = {
    "idle": r"""
disciplined upright ready stance, shoulders level and square, feet close together,
left hand lightly stabilizing the scabbard, right hand relaxed beside the hilt,
perfectly balanced vertical posture, composed stillness, sword fully sheathed
""",
    "anticipation": r"""
disciplined iaijutsu preparation, torso rotated only slightly, shoulders lowered,
left hand firmly pushing the scabbard backward, right hand gripping the hilt,
elbows kept close to the body, feet close and firmly planted, knees only slightly flexed,
blade completely inside the scabbard, compact controlled tension, no large movement
""",
    "strike": r"""
sharp disciplined iaijutsu nukitsuke, a short controlled horizontal partial draw,
right hand pulls the hilt forward while left hand drives the scabbard backward,
only the first one third of the steel blade visible outside the scabbard,
the rest of the blade remains inside the sheath, elbows close, shoulders level,
torso turns crisply, feet planted close together, knees slightly flexed,
compact precise martial form, restrained speed, no follow-through
""",
}

NEGATIVE_PROMPT = r"""
censored, worst quality, low quality, normal quality, blurry, noisy, grain, jpeg artifacts,
bad anatomy, bad hands, extra fingers, missing fingers, fused fingers, extra limbs,
two people, duplicate, multiple views, character sheet, text, logo, signature,
long loose hair, ponytail, blonde hair, blue eyes, smiling, open mouth,
short kimono, short skirt, pleated skirt, school uniform, sailor collar, labcoat,
giant decorative bow, visible thighs, bare legs, thighhighs, boots, armor,
multiple swords, two scabbards, sword on back, fully drawn sword, fully unsheathed blade,
long exposed blade, overhead slash, huge swing, attack trail, magic effect,
wide squat, spread legs, horse stance, sitting, kneeling, falling, acrobatics,
exaggerated foreshortening, extreme perspective, tilted horizon, cropped feet,
floor, scenery, detailed background, cast shadow, reference sheet, design sheet,
weapon lineup, accessory lineup, turnaround, multiple panels
"""

ACTION_PROMPT = r"""
748cmstyle, Cute_niji_style, <lora:iLLMythSmo0thL1nes:0.35>,
<lora:Niji_cute_style_illustrious:0.9>, masterpiece, best quality,
single SD chibi adult Japanese swordswoman, 2.5 heads tall, solo, full body,
side three-quarter view facing right, (disciplined standing iaijutsu draw pose:1.5),
(narrow balanced stance with both feet close together:1.35), left foot only half a step forward,
torso leaning forward slightly, shoulders level, elbows close to ribs,
(right hand gripping the katana hilt:1.35), (left hand gripping and pushing the scabbard backward:1.35),
(katana only one quarter drawn:1.55), (short silver blade segment visible between guard and scabbard mouth:1.4),
sheath remains horizontal at the left hip, compact controlled nukitsuke, precise restrained motion,
stoic red eyes, short blue-black hair in a small low bun, loose cheek strands,
red camellia and thin gold hairpin on viewer-right side, short red tassel,
plain formal floor-length black kimono, white inner collar, thin crimson collar piping,
wide plain solid-crimson obi, long wide sleeves, legs fully covered, black sandals,
one black lacquer scabbard, dark diamond-wrapped handle, no visible rear bow,
clean game sprite silhouette, centered on pure white background
"""


def encode_image(image: Image.Image) -> str:
    buffer = BytesIO()
    image.convert("RGB").save(buffer, format="PNG")
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def build_design_guide(source_path: Path) -> Image.Image:
    """Compress the standing design into chibi proportions while preserving its design regions."""
    source = Image.open(source_path).convert("RGB")
    canvas = Image.new("RGB", (768, 768), "white")

    # Head, ornament, and face retain the most identity-critical pixels.
    head = source.crop((350, 130, 735, 535)).resize((340, 358), Image.Resampling.LANCZOS)
    # The full-length garment is deliberately shortened, not cropped into a short skirt.
    body = source.crop((180, 410, 900, 2035)).resize((455, 510), Image.Resampling.LANCZOS)

    canvas.paste(body, (157, 248))
    canvas.paste(head, (214, 42))
    return canvas


def build_pose_guide(source_path: Path, pose: str) -> tuple[Image.Image, Image.Image]:
    """Keep the canonical head while giving img2img a compact iaijutsu body silhouette."""
    source = Image.open(source_path).convert("RGB").resize((768, 768), Image.Resampling.LANCZOS)
    guide = Image.new("RGB", (768, 768), "white")

    # Preserve the canonical face, hair, and camellia exactly. White pixels remain transparent.
    source_pixels = source.load()
    head_mask = Image.new("L", source.size, 0)
    mask_pixels = head_mask.load()
    for y in range(20, 335):
        for x in range(155, 615):
            red, green, blue = source_pixels[x, y]
            distance = max(red, green, blue) - min(red, green, blue)
            if min(red, green, blue) < 245 or distance > 8:
                mask_pixels[x, y] = 255
    head_mask = head_mask.filter(ImageFilter.GaussianBlur(0.6))
    guide.paste(source, mask=head_mask)

    draw = ImageDraw.Draw(guide)
    black = (25, 26, 33)
    charcoal = (45, 46, 56)
    red = (154, 24, 26)
    pale = (244, 220, 211)

    if pose == "anticipation":
        # Long kimono remains nearly vertical; tension comes from the close arms and the saya push.
        draw.polygon([(290, 285), (455, 285), (493, 455), (505, 710), (238, 710), (258, 455)], fill=black)
        draw.polygon([(290, 300), (365, 415), (455, 300), (446, 585), (300, 585)], fill=charcoal)
        draw.rectangle((274, 394, 464, 452), fill=red)
        draw.line((373, 453, 367, 700), fill=red, width=9)
        draw.line((273, 330, 342, 430), fill=black, width=55)
        draw.line((457, 330, 396, 426), fill=black, width=55)
        draw.ellipse((327, 410, 360, 443), fill=pale)
        draw.ellipse((385, 407, 418, 440), fill=pale)
        draw.line((345, 430, 132, 515), fill=(18, 18, 22), width=24)
        draw.line((345, 430, 426, 398), fill=(72, 55, 37), width=18)
    elif pose == "strike":
        # Compact nukitsuke: close feet, small torso twist, saya backward and hilt forward.
        draw.polygon([(300, 292), (462, 305), (495, 470), (526, 706), (260, 706), (260, 468)], fill=black)
        draw.polygon([(302, 306), (390, 416), (462, 316), (455, 590), (305, 590)], fill=charcoal)
        draw.polygon([(280, 393), (466, 403), (470, 461), (276, 451)], fill=red)
        draw.line((382, 457, 394, 700), fill=red, width=9)
        draw.line((302, 338, 352, 430), fill=black, width=54)
        draw.line((457, 347, 409, 424), fill=black, width=54)
        draw.ellipse((334, 414, 367, 447), fill=pale)
        draw.ellipse((396, 409, 429, 442), fill=pale)
        # The scabbard moves backward left; only a short steel section exits toward the right.
        draw.line((358, 433, 111, 520), fill=(16, 17, 21), width=25)
        draw.line((358, 433, 438, 405), fill=(73, 50, 35), width=20)
        draw.line((438, 405, 522, 376), fill=(180, 188, 198), width=12)
    else:
        raise ValueError(f"Pose guide is only available for attack poses: {pose}")

    # Regenerate everything below the jaw while leaving face/hair pixels untouched.
    inpaint_mask = Image.new("L", (768, 768), 0)
    mask_draw = ImageDraw.Draw(inpaint_mask)
    mask_draw.rectangle((95, 285, 565, 767), fill=255)
    inpaint_mask = inpaint_mask.filter(ImageFilter.GaussianBlur(10))
    return guide, inpaint_mask


def build_narrow_pose_guide(source_path: Path) -> Image.Image:
    """Turn a low/wide generated draw pose into a taller, narrow iaijutsu silhouette."""
    source = Image.open(source_path).convert("RGBA").resize((768, 768), Image.Resampling.LANCZOS)
    alpha = Image.new("L", source.size, 0)
    source_pixels = source.load()
    alpha_pixels = alpha.load()
    for y in range(768):
        for x in range(768):
            red, green, blue, _ = source_pixels[x, y]
            if min(red, green, blue) < 242 or max(red, green, blue) - min(red, green, blue) > 10:
                alpha_pixels[x, y] = 255

    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError(f"No foreground found in pose source: {source_path}")
    foreground = source.crop(bbox)
    foreground.putalpha(alpha.crop(bbox))
    new_width = int(foreground.width * 0.70)
    new_height = min(670, int(foreground.height * 1.12))
    foreground = foreground.resize((new_width, new_height), Image.Resampling.LANCZOS)

    guide = Image.new("RGB", (768, 768), "white")
    guide.paste(foreground, ((768 - new_width) // 2, 735 - new_height), foreground)
    return guide


def build_long_kimono_guide(source_path: Path) -> tuple[Image.Image, Image.Image]:
    """Replace exposed attack-pose legs with the standing design's ankle-length kimono."""
    guide = Image.open(source_path).convert("RGB").resize((768, 768), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(guide)
    garment = [(270, 505), (458, 505), (506, 734), (226, 734)]
    draw.polygon(garment, fill=(29, 29, 36))
    draw.polygon([(350, 505), (382, 505), (393, 734), (361, 734)], fill=(128, 18, 22))
    draw.line((270, 505, 226, 734), fill=(16, 16, 21), width=7)
    draw.line((458, 505, 506, 734), fill=(16, 16, 21), width=7)

    mask = Image.new("L", (768, 768), 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.polygon(garment, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(7))
    return guide, mask


def build_identity_harmonize_guide(
    source_path: Path,
    canonical_path: Path,
) -> tuple[Image.Image, Image.Image]:
    """Copy the canonical head/ornament and regenerate only the incorrect obi."""
    guide = Image.open(source_path).convert("RGB").resize((768, 768), Image.Resampling.LANCZOS)
    canonical = Image.open(canonical_path).convert("RGB").resize((768, 768), Image.Resampling.LANCZOS)

    canonical_mask = Image.new("L", (768, 768), 0)
    canonical_pixels = canonical.load()
    mask_pixels = canonical_mask.load()
    for y in range(30, 340):
        for x in range(165, 610):
            red, green, blue = canonical_pixels[x, y]
            if min(red, green, blue) < 245 or max(red, green, blue) - min(red, green, blue) > 8:
                mask_pixels[x, y] = 255
    canonical_mask = canonical_mask.filter(ImageFilter.GaussianBlur(0.5))
    guide.paste(canonical, mask=canonical_mask)

    # The standing design has a broad, plain crimson obi rather than a pale patterned sash.
    obi = [(293, 354), (464, 354), (471, 432), (288, 432)]
    draw = ImageDraw.Draw(guide)
    draw.polygon(obi, fill=(158, 25, 29))
    draw.line((294, 355, 463, 355), fill=(133, 103, 52), width=5)

    inpaint_mask = Image.new("L", (768, 768), 0)
    ImageDraw.Draw(inpaint_mask).polygon(obi, fill=255)
    inpaint_mask = inpaint_mask.filter(ImageFilter.GaussianBlur(5))
    return guide, inpaint_mask


def request_candidates(
    api_url: str,
    init_image: Image.Image,
    pose: str,
    output_dir: Path,
    denoise: float,
    count: int,
    seed: int,
    mask: Image.Image | None = None,
) -> None:
    payload = {
        "init_images": [encode_image(init_image)],
        "prompt": ACTION_PROMPT if pose == "strike" else BASE_PROMPT + "\n" + POSES[pose],
        "negative_prompt": NEGATIVE_PROMPT,
        "width": 768,
        "height": 768,
        "resize_mode": 0,
        "denoising_strength": denoise,
        "sampler_name": "DPM++ 2M SDE",
        "scheduler": "Karras",
        "steps": 35,
        "cfg_scale": 4.5,
        "batch_size": count,
        "n_iter": 1,
        "seed": seed,
        "save_images": True,
    }
    if mask is not None:
        payload.update(
            {
                "mask": encode_image(mask),
                "mask_blur": 10,
                "inpainting_fill": 0,
                "inpaint_full_res": False,
                "inpaint_full_res_padding": 32,
                "include_init_images": False,
            }
        )

    request = urllib.request.Request(
        api_url.rstrip("/") + "/sdapi/v1/img2img",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=900) as response:
        result = json.load(response)

    output_dir.mkdir(parents=True, exist_ok=True)
    for index, encoded in enumerate(result["images"]):
        image = Image.open(BytesIO(base64.b64decode(encoded.split(",")[-1]))).convert("RGB")
        image.save(output_dir / f"{pose}_{index:02d}.png")

    (output_dir / f"{pose}_info.json").write_text(result.get("info", "{}"), encoding="utf-8")


def request_txt2img(
    api_url: str,
    pose: str,
    output_dir: Path,
    count: int,
    seed: int,
) -> None:
    payload = {
        "prompt": ACTION_PROMPT if pose == "strike" else BASE_PROMPT + "\n" + POSES[pose],
        "negative_prompt": NEGATIVE_PROMPT,
        "width": 768,
        "height": 768,
        "sampler_name": "DPM++ 2M SDE",
        "scheduler": "Karras",
        "steps": 38,
        "cfg_scale": 5.0,
        "batch_size": count,
        "n_iter": 1,
        "seed": seed,
        "save_images": True,
    }
    request = urllib.request.Request(
        api_url.rstrip("/") + "/sdapi/v1/txt2img",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=900) as response:
        result = json.load(response)

    output_dir.mkdir(parents=True, exist_ok=True)
    for index, encoded in enumerate(result["images"]):
        image = Image.open(BytesIO(base64.b64decode(encoded.split(",")[-1]))).convert("RGB")
        image.save(output_dir / f"{pose}_{index:02d}.png")
    (output_dir / f"{pose}_info.json").write_text(result.get("info", "{}"), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--canonical", type=Path)
    parser.add_argument("--pose", choices=sorted(POSES), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--init",
        choices=(
            "standing", "guide", "sprite", "pose-guide", "narrow-pose",
            "long-kimono", "identity-harmonize", "txt2img",
        ),
        default="guide",
    )
    parser.add_argument("--denoise", type=float, default=0.64)
    parser.add_argument("--count", type=int, default=4)
    parser.add_argument("--seed", type=int, default=-1)
    parser.add_argument("--api", default="http://127.0.0.1:7860")
    args = parser.parse_args()

    if args.init == "txt2img":
        request_txt2img(args.api, args.pose, args.output, args.count, args.seed)
        return

    mask = None
    if args.init == "guide":
        init_image = build_design_guide(args.source)
        args.output.mkdir(parents=True, exist_ok=True)
        init_image.save(args.output / "_design_guide.png")
    elif args.init == "pose-guide":
        init_image, mask = build_pose_guide(args.source, args.pose)
        args.output.mkdir(parents=True, exist_ok=True)
        init_image.save(args.output / f"_{args.pose}_guide.png")
        mask.save(args.output / f"_{args.pose}_mask.png")
    elif args.init == "narrow-pose":
        init_image = build_narrow_pose_guide(args.source)
        args.output.mkdir(parents=True, exist_ok=True)
        init_image.save(args.output / f"_{args.pose}_guide.png")
    elif args.init == "long-kimono":
        init_image, mask = build_long_kimono_guide(args.source)
        args.output.mkdir(parents=True, exist_ok=True)
        init_image.save(args.output / f"_{args.pose}_guide.png")
        mask.save(args.output / f"_{args.pose}_mask.png")
    elif args.init == "identity-harmonize":
        if args.canonical is None:
            parser.error("--canonical is required for --init identity-harmonize")
        init_image, mask = build_identity_harmonize_guide(args.source, args.canonical)
        args.output.mkdir(parents=True, exist_ok=True)
        init_image.save(args.output / f"_{args.pose}_guide.png")
        mask.save(args.output / f"_{args.pose}_mask.png")
    else:
        init_image = Image.open(args.source).convert("RGB").resize((768, 768), Image.Resampling.LANCZOS)

    request_candidates(
        args.api,
        init_image,
        args.pose,
        args.output,
        args.denoise,
        args.count,
        args.seed,
        mask,
    )


if __name__ == "__main__":
    main()
