from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((8, 8, 248, 248), radius=54, fill="#141820", outline="#39465E", width=4)
for x, y, fill in [(57, 57, "#8AB4FF"), (137, 57, "#8AB4FF"), (57, 137, "#8AB4FF"), (137, 137, "#EDF1FA")]:
    draw.rounded_rectangle((x, y, x + 62, y + 62), radius=13, fill=fill)
image.save(root / "src" / "Harbor.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
