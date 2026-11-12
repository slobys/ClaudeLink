from pathlib import Path
from PIL import Image, ImageDraw, PngImagePlugin

root = Path(__file__).resolve().parents[1]
target = root / "src" / "Launcher.App" / "Assets"
target.mkdir(parents=True, exist_ok=True)
image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((7, 7, 249, 249), radius=55, fill="#2563EB")
draw.arc((48, 49, 199, 205), 55, 303, fill="#FFFFFF", width=24)
draw.line((124, 139, 200, 63), fill="#FFFFFF", width=20)
draw.line((155, 62, 201, 62, 201, 108), fill="#FFFFFF", width=18, joint="curve")
image.save(target / "App.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
metadata = PngImagePlugin.PngInfo()
metadata.add_text("origin", "Procedurally drawn by scripts/create_icon.py; no source raster or generated image model.")
image.save(target / "App.png", pnginfo=metadata)
