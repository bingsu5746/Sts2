"""Procedural placeholder art for the beta (no painted art yet).

Writes, under Mumyeong/images/:
  card_portraits/figure_<key>.png (250x190) and big/ (1000x760)  — one emblem per figure + "shared"
  relics/<relic>.png (94), <relic>_outline.png, big/<relic>.png (256)
  powers/<power>.png (64) and big/ (256)
  ui/rest_option_film_reel.png (320)
Usage (from Mumyeong/): python3 tools/gen_art.py
"""
import math
import os
import re

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Mumyeong", "images")

COLORS = {
    "guanyu": (47, 143, 91),
    "joan": (217, 196, 106),
    "leonidas": (181, 70, 47),
    "genghis": (138, 106, 60),
    "yisunsin": (58, 95, 158),
    "napoleon": (91, 74, 158),
    "shared": (143, 163, 184),
}


def snake(name):
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def background(w, h, color):
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    dark = tuple(int(c * 0.18) for c in color)
    mid = tuple(int(c * 0.55) for c in color)
    cx, cy = w / 2, h * 0.45
    maxr = math.hypot(w, h) / 2
    for r in range(int(maxr), 0, -4):
        t = r / maxr
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=lerp(mid, dark, t))
    # film grain lines
    for x in range(0, w, max(6, w // 60)):
        d.line([(x, 0), (x, h)], fill=lerp(dark, mid, 0.15), width=1)
    return im


def emblem(d, key, cx, cy, s, fg):
    """Draw a simple emblem of size s centred at (cx, cy)."""
    lw = max(2, int(s * 0.06))
    if key == "guanyu":  # crescent blade on a polearm
        d.line([(cx - s * 0.45, cy + s * 0.5), (cx + s * 0.35, cy - s * 0.4)], fill=fg, width=lw)
        d.pieslice([cx, cy - s * 0.75, cx + s * 0.6, cy - s * 0.15], 200, 380, fill=fg)
        d.pieslice([cx + s * 0.08, cy - s * 0.67, cx + s * 0.52, cy - s * 0.23], 200, 380, fill=(0, 0, 0, 0))
    elif key == "joan":  # banner
        d.line([(cx - s * 0.35, cy + s * 0.55), (cx - s * 0.35, cy - s * 0.6)], fill=fg, width=lw)
        d.polygon([(cx - s * 0.35, cy - s * 0.58), (cx + s * 0.5, cy - s * 0.4), (cx + s * 0.25, cy - s * 0.2),
                   (cx + s * 0.5, cy), (cx - s * 0.35, cy - s * 0.05)], fill=fg)
    elif key == "leonidas":  # round shield with lambda
        r = s * 0.5
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=fg, width=lw * 2)
        d.line([(cx - r * 0.45, cy + r * 0.45), (cx, cy - r * 0.5), (cx + r * 0.45, cy + r * 0.45)], fill=fg, width=lw * 2)
    elif key == "genghis":  # bow and arrow
        d.arc([cx - s * 0.55, cy - s * 0.55, cx + s * 0.15, cy + s * 0.55], 270, 90, fill=fg, width=lw * 2)
        d.line([(cx - s * 0.2, cy - s * 0.5), (cx - s * 0.2, cy + s * 0.5)], fill=fg, width=max(1, lw // 2))
        d.line([(cx - s * 0.3, cy), (cx + s * 0.55, cy)], fill=fg, width=lw)
        d.polygon([(cx + s * 0.55, cy - s * 0.08), (cx + s * 0.7, cy), (cx + s * 0.55, cy + s * 0.08)], fill=fg)
    elif key == "yisunsin":  # turtle ship hull with cannon ports
        d.polygon([(cx - s * 0.6, cy), (cx + s * 0.6, cy), (cx + s * 0.45, cy + s * 0.3), (cx - s * 0.45, cy + s * 0.3)], fill=fg)
        d.pieslice([cx - s * 0.5, cy - s * 0.45, cx + s * 0.5, cy + s * 0.25], 180, 360, fill=fg)
        for i in range(-2, 3):
            d.ellipse([cx + i * s * 0.18 - s * 0.04, cy + s * 0.08, cx + i * s * 0.18 + s * 0.04, cy + s * 0.16], fill=(20, 20, 30))
        for i in range(-3, 4):
            x = cx + i * s * 0.12
            d.line([(x, cy - s * 0.18), (x, cy - s * 0.3)], fill=fg, width=max(1, lw // 2))
    elif key == "napoleon":  # bicorne hat
        d.chord([cx - s * 0.65, cy - s * 0.35, cx + s * 0.65, cy + s * 0.35], 180, 360, fill=fg)
        d.rectangle([cx - s * 0.65, cy - s * 0.02, cx + s * 0.65, cy + s * 0.06], fill=fg)
        d.ellipse([cx - s * 0.08, cy - s * 0.25, cx + s * 0.08, cy - s * 0.09], fill=(200, 40, 40))
    else:  # film reel
        r = s * 0.5
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=fg, width=lw * 2)
        d.ellipse([cx - r * 0.18, cy - r * 0.18, cx + r * 0.18, cy + r * 0.18], fill=fg)
        for k in range(6):
            a = k * math.pi / 3
            px, py = cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55
            d.ellipse([px - r * 0.17, py - r * 0.17, px + r * 0.17, py + r * 0.17], outline=fg, width=lw)


def card_portraits():
    for key, color in COLORS.items():
        for size, folder in (((1000, 760), "card_portraits/big"), ((250, 190), "card_portraits")):
            w, h = size
            im = background(w, h, color).convert("RGBA")
            layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            d = ImageDraw.Draw(layer)
            fg = lerp(color, (255, 245, 225), 0.55) + (255,)
            emblem(d, key, w / 2, h * 0.47, min(w, h) * 0.62, fg)
            glow = layer.filter(ImageFilter.GaussianBlur(max(2, w // 120)))
            im = Image.alpha_composite(im, glow)
            im = Image.alpha_composite(im, layer)
            os.makedirs(os.path.join(ROOT, folder), exist_ok=True)
            im.convert("RGB").save(os.path.join(ROOT, folder, f"figure_{key}.png"))


def icon(size, color, key, ring=True):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pad = size * 0.06
    if ring:
        d.ellipse([pad, pad, size - pad, size - pad], fill=tuple(int(c * 0.35) for c in color) + (235,),
                  outline=lerp(color, (255, 255, 255), 0.3) + (255,), width=max(2, size // 24))
    emblem(d, key, size / 2, size * 0.52, size * 0.62, lerp(color, (255, 245, 225), 0.6) + (255,))
    return im


RELICS = {
    "SoulCore": "shared", "GuanyuGreenDragonRelic": "guanyu", "JoanBannerRelic": "joan",
    "LeonidasShieldRelic": "leonidas", "GenghisSealRelic": "genghis", "YiTurtleShipRelic": "yisunsin",
    "NapoleonCrownRelic": "napoleon",
}

POWERS = {
    "ApplausePower": "shared", "GrudgePower": "leonidas",
    "GuanyuAwePower": "guanyu", "GuanyuWarGodPower": "guanyu", "GuanyuRighteousPower": "guanyu",
    "JoanFaithPower": "joan", "JoanBannerPower": "joan", "JoanPyrePower": "joan", "JoanJudgmentPower": "joan",
    "LeonidasPhalanxPower": "leonidas", "LeonidasCounterPower": "leonidas", "LeonidasMolonLabePower": "leonidas",
    "LeonidasThermopylaePower": "leonidas",
    "GenghisHordePower": "genghis", "GenghisConquestPower": "genghis", "GenghisEmpirePower": "genghis",
    "YiCannonPower": "yisunsin", "YiTurtleShipPower": "yisunsin", "YiCraneWingPower": "yisunsin",
    "YiCommanderPower": "yisunsin",
    "NapoleonArtilleryPower": "napoleon", "NapoleonBlitzPower": "napoleon", "NapoleonCoronationPower": "napoleon",
}


def relics():
    for cls, key in RELICS.items():
        name = snake(cls)
        color = COLORS[key]
        for size, folder in ((94, "relics"), (256, "relics/big")):
            os.makedirs(os.path.join(ROOT, folder), exist_ok=True)
            icon(size, color, key).save(os.path.join(ROOT, folder, f"{name}.png"))
        # outline: white silhouette of the small icon
        small = icon(94, color, key)
        alpha = small.split()[3].filter(ImageFilter.MaxFilter(5))
        outline = Image.new("RGBA", small.size, (255, 255, 255, 0))
        outline.putalpha(alpha)
        outline.save(os.path.join(ROOT, "relics", f"{name}_outline.png"))


def powers():
    for cls, key in POWERS.items():
        name = snake(cls)
        for size, folder in ((64, "powers"), (256, "powers/big")):
            os.makedirs(os.path.join(ROOT, folder), exist_ok=True)
            icon(size, COLORS[key], key, ring=False).save(os.path.join(ROOT, folder, f"{name}.png"))


def rest_icon():
    os.makedirs(os.path.join(ROOT, "ui"), exist_ok=True)
    icon(320, COLORS["shared"], "shared").save(os.path.join(ROOT, "ui", "rest_option_film_reel.png"))


if __name__ == "__main__":
    card_portraits()
    relics()
    powers()
    rest_icon()
    print("done")
