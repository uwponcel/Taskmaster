from PIL import Image, ImageDraw
import math

S = 8

GOLD_HI = (240, 210, 140)
GOLD_LO = (140, 100, 45)
DISC_HI = (54, 42, 34)
DISC_LO = (20, 15, 12)
CREAM = (245, 235, 215)
SHADOW = (25, 18, 12)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def radial_fill(draw, cx, cy, r, hi, lo, steps, light_offset=0.28):
    # True outer boundary FIRST, always centered at (cx, cy) - this is what has
    # to line up with the groove outline and glyph, which are drawn at the true
    # center too. Filling it with the off-center gradient's own concentric
    # circles (as before) let the highlight's biggest ring define the outer
    # edge, so the whole disc ended up shifted toward the light position
    # instead of just the shading within it - that's the off-center bug.
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=lo + (255,))

    # Off-center highlight blob layered on top for a directional-light feel,
    # inset just enough that it can never poke past the true outer boundary.
    offset_dist = math.hypot(r * light_offset, r * light_offset)
    max_hi_r = max(0.0, r - offset_dist) * 0.92
    lx = cx - r * light_offset
    ly = cy - r * light_offset
    for i in range(steps, 0, -1):
        t = i / steps
        rad = max_hi_r * t
        # t=1 -> full-size circle -> must be the darker base color (lo), so the
        # smallest, last-drawn, topmost circle at the center is the brightest
        # (hi). Getting this backwards (lerp(lo, hi, t)) draws the BIGGEST
        # circle brightest first and the SMALLEST circle darkest last right on
        # top of it - a small dark dot sitting inside a lighter halo, which is
        # exactly the stray-circle artifact this fixes.
        color = lerp(hi, lo, t)
        draw.ellipse([lx - rad, ly - rad, lx + rad, ly + rad], fill=color + (255,))


def draw_medallion(size, ring_frac=0.09, margin_frac=0.03):
    c = size * S
    img = Image.new("RGBA", (c, c), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = c / 2
    r_outer = (c / 2) * (1 - margin_frac)
    ring = r_outer * ring_frac

    # Outer gold ring (metal bevel via off-center radial fill)
    radial_fill(d, cx, cy, r_outer, GOLD_HI, GOLD_LO, steps=40, light_offset=0.30)

    # Inner dark disc (recessed face of the medallion), slightly smaller than the ring
    r_inner = r_outer - ring
    radial_fill(d, cx, cy, r_inner, DISC_HI, DISC_LO, steps=40, light_offset=0.35)

    # Thin darker groove between ring and disc for definition
    d.ellipse([cx - r_inner, cy - r_inner, cx + r_inner, cy + r_inner],
              outline=(15, 10, 8, 255), width=int(1.2 * S))

    return img, cx, cy, r_inner


def draw_check(draw, cx, cy, r, color, width):
    # A single bold checkmark, proportioned to the inner disc.
    pts = [
        (cx - r * 0.42, cy + r * 0.02),
        (cx - r * 0.12, cy + r * 0.34),
        (cx + r * 0.46, cy - r * 0.32),
    ]
    draw.line(pts, fill=color, width=width, joint="curve")
    # Round the joints/ends so the emboss reads cleanly at small sizes.
    for p in pts:
        draw.ellipse([p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2], fill=color)


def make_corner_icon():
    img, cx, cy, r_inner = draw_medallion(64)
    d = ImageDraw.Draw(img)
    w = int(5.0 * S)
    # Shadow pass (offset down-right) then the lit stroke on top - a cheap
    # emboss/engrave trick using only flat 2D drawing.
    draw_check(d, cx + 1.2 * S, cy + 1.4 * S, r_inner, SHADOW + (255,), w)
    draw_check(d, cx, cy, r_inner, CREAM + (255,), w)
    img = img.resize((64, 64), Image.LANCZOS)
    img.save(r"C:\git\perso\Taskmaster\ref\corner-icon.png")
    print("corner-icon.png")


def make_emblem():
    # Same medallion+checkmark motif as the corner icon, at Blish's window
    # title-bar emblem slot. Unlike Maestro's ornate treble-clef glyph (thin
    # linework with lots of negative space), ours is a solid filled disc that
    # reads as much "heavier" at the same pixel size - so it needs both a
    # smaller canvas AND real transparent margin around the medallion instead
    # of filling the frame edge-to-edge, to actually look smaller in place.
    size = 68
    img, cx, cy, r_inner = draw_medallion(size, margin_frac=0.14)
    d = ImageDraw.Draw(img)
    w = int(7.0 * S)
    draw_check(d, cx + 1.6 * S, cy + 1.8 * S, r_inner, SHADOW + (255,), w)
    draw_check(d, cx, cy, r_inner, CREAM + (255,), w)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(r"C:\git\perso\Taskmaster\ref\taskmaster-emblem.png")
    print("taskmaster-emblem.png")


def make_readme_logo():
    # Same medallion+checkmark motif, at README-header size (matches Maestro's
    # 256x256 maestro-logo.png). Not embedded in the module - a doc/marketing
    # asset, saved outside ref/ so it doesn't end up in the compiled .bhm.
    size = 256
    img, cx, cy, r_inner = draw_medallion(size, margin_frac=0.05)
    d = ImageDraw.Draw(img)
    w = int(7.0 * S)
    draw_check(d, cx + 1.6 * S, cy + 1.8 * S, r_inner, SHADOW + (255,), w)
    draw_check(d, cx, cy, r_inner, CREAM + (255,), w)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(r"C:\git\perso\Taskmaster\.github\images\taskmaster-logo.png")
    print("taskmaster-logo.png")


make_corner_icon()
make_emblem()
make_readme_logo()
