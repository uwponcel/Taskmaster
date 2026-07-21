from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / ".github" / "images" / "taskmaster-logo.png"
TARGETS = {
    ROOT / "ref" / "corner-icon.png": 64,
    ROOT / "ref" / "taskmaster-emblem.png": 68,
}


with Image.open(SOURCE) as source:
    logo = source.convert("RGBA")
    for target, size in TARGETS.items():
        logo.resize((size, size), Image.Resampling.LANCZOS).save(target)
        print(target.relative_to(ROOT))
