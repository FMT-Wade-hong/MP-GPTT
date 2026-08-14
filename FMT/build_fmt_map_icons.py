from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[1]
TMP = ROOT / "tmp" / "imagegen"
RESOURCES = ROOT / "Resources"
PREVIEW = ROOT / "FMT" / "MapIconPreview.png"
GLOW_PREVIEW = ROOT / "FMT" / "MapIconPreviewGlow.png"

ICON_SOURCES = {
    "fixedwing": TMP / "fmt-fixedwing-alpha.png",
    "multirotor": TMP / "fmt-multirotor-alpha.png",
    "vtol": TMP / "fmt-vtol-alpha.png",
}

ICON_OUTPUTS = {
    "fixedwing": RESOURCES / "FMTMapFixedWing.png",
    "multirotor": RESOURCES / "FMTMapMultirotor.png",
    "vtol": RESOURCES / "FMTMapVtol.png",
}

GLOW_OUTPUTS = {
    "fixedwing": RESOURCES / "FMTMapFixedWingGlow.png",
    "multirotor": RESOURCES / "FMTMapMultirotorGlow.png",
    "vtol": RESOURCES / "FMTMapVtolGlow.png",
}


def fit_icon(source: Path, output: Path, size: int = 72, padding: int = 4) -> Image.Image:
    image = Image.open(source).convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    if not bbox:
        raise RuntimeError(f"No visible subject in {source}")
    image = image.crop(bbox)
    available = size - padding * 2
    scale = min(available / image.width, available / image.height)
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    x = (size - resized.width) // 2
    y = (size - resized.height) // 2
    canvas.alpha_composite(resized, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)
    return canvas


def load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    font_name = "msjhbd.ttc" if bold else "msjh.ttc"
    font_path = Path("C:/Windows/Fonts") / font_name
    if font_path.exists():
        return ImageFont.truetype(str(font_path), size)
    return ImageFont.load_default()


def add_backlight(icon: Image.Image, size: int = 100) -> Image.Image:
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    x = (size - icon.width) // 2
    y = (size - icon.height) // 2
    base.alpha_composite(icon, (x, y))
    alpha = base.getchannel("A")

    expanded = alpha.filter(ImageFilter.MaxFilter(9))
    outline = ImageChops.subtract(expanded, alpha)
    outer_glow = expanded.filter(ImageFilter.GaussianBlur(8))
    middle_glow = expanded.filter(ImageFilter.GaussianBlur(4))
    inner_edge = ImageChops.subtract(alpha, alpha.filter(ImageFilter.MinFilter(5)))

    result = Image.new("RGBA", base.size, (0, 0, 0, 0))
    for mask, color, strength in (
        (outer_glow, (0, 142, 255), 0.48),
        (middle_glow, (0, 204, 255), 0.62),
        (outline, (22, 218, 255), 1.0),
    ):
        layer = Image.new("RGBA", base.size, color + (0,))
        layer.putalpha(mask.point(lambda value, factor=strength: round(value * factor)))
        result = Image.alpha_composite(result, layer)

    result = Image.alpha_composite(result, base)
    white_edge = Image.new("RGBA", base.size, (255, 255, 255, 0))
    white_edge.putalpha(inner_edge.point(lambda value: round(value * 0.9)))
    return Image.alpha_composite(result, white_edge)


def marker(icon: Image.Image, heading: float) -> Image.Image:
    base = Image.new("RGBA", (124, 124), (0, 0, 0, 0))
    x = (base.width - icon.width) // 2
    y = (base.height - icon.height) // 2 + 8
    base.alpha_composite(icon, (x, y))
    draw = ImageDraw.Draw(base)
    draw.polygon([(62, 2), (53, 18), (71, 18)], fill=(20, 25, 30, 220), outline=(255, 255, 255, 255))
    draw.line([(62, 2), (53, 18), (71, 18), (62, 2)], fill=(255, 255, 255, 255), width=2)
    return base.rotate(-heading, resample=Image.Resampling.BICUBIC, expand=True)


def create_preview(icons: dict[str, Image.Image], output: Path, glow_style: bool = False) -> None:
    screenshot = Path(
        "C:/Users/hongw/AppData/Local/Temp/"
        "codex-clipboard-c51af230-c5fb-4da4-8994-c035e08a15a0.png"
    )
    if screenshot.exists():
        source = Image.open(screenshot).convert("RGB")
        left = min(max(0, 520), source.width - 1)
        top = min(max(0, 68), source.height - 1)
        right = min(source.width, 1900)
        bottom = min(source.height, 800)
        background = source.crop((left, top, right, bottom)).resize((1380, 720), Image.Resampling.LANCZOS)
    else:
        background = Image.new("RGB", (1380, 720), (67, 86, 68))

    preview = background.convert("RGBA")
    overlay = Image.new("RGBA", preview.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    title_font = load_font(28, bold=True)
    label_font = load_font(21, bold=True)
    draw.rounded_rectangle((20, 18, 626, 68), radius=10, fill=(10, 20, 27, 225), outline=(45, 169, 220, 255), width=2)
    title = "FMTPlanner V1.0.5 背光飛行器圖標預覽" if glow_style else "FMTPlanner V1.0.5 地圖飛行器圖標預覽"
    draw.text((38, 27), title, font=title_font, fill=(255, 255, 255, 255))

    items = [
        ("fixedwing", "固定翼", (275, 250), 20),
        ("multirotor", "多旋翼", (690, 420), 330),
        ("vtol", "VTOL／QuadPlane", (1070, 230), 55),
    ]
    for key, label, center, heading in items:
        rendered = marker(icons[key], heading)
        x = center[0] - rendered.width // 2
        y = center[1] - rendered.height // 2
        overlay.alpha_composite(rendered, (x, y))
        text_bbox = draw.textbbox((0, 0), label, font=label_font)
        text_width = text_bbox[2] - text_bbox[0]
        label_rect = (center[0] - text_width // 2 - 12, center[1] + 72,
                      center[0] + text_width // 2 + 12, center[1] + 106)
        draw.rounded_rectangle(label_rect, radius=7, fill=(10, 20, 27, 220), outline=(52, 188, 83, 255), width=2)
        draw.text((center[0] - text_width // 2, center[1] + 76), label, font=label_font, fill=(255, 255, 255, 255))

    preview = Image.alpha_composite(preview, overlay)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview.convert("RGB").save(output, quality=94)


def main() -> None:
    icons = {
        key: fit_icon(source, ICON_OUTPUTS[key])
        for key, source in ICON_SOURCES.items()
    }
    glow_icons = {key: add_backlight(icon) for key, icon in icons.items()}
    for key, icon in glow_icons.items():
        icon.save(GLOW_OUTPUTS[key], optimize=True)
    create_preview(icons, PREVIEW)
    create_preview(glow_icons, GLOW_PREVIEW, glow_style=True)
    for path in ICON_OUTPUTS.values():
        print(path)
    for path in GLOW_OUTPUTS.values():
        print(path)
    print(PREVIEW)
    print(GLOW_PREVIEW)


if __name__ == "__main__":
    main()
