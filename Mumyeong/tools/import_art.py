"""Turns the AI Horde output (tools/horde_gen.py) into the mod's art files.

  backgrounds:  <raw>/bg/<name>.webp  -> images/events/theater_hall.png, scenes/<k>.png, figures/<k>.png (1920x1080)
  characters:   <raw>/ch/<name>.webp  (background already stripped by AI Horde's strip_background)
     reaper_stand_<seed>        -> images/character/reaper.png        560x860, feet at the bottom centre
     reaper_sit_<seed>          -> images/character/reaper_rest.png   420x480
     spirit_<k>_stand_<seed>    -> images/character/spirit_<k>.png    680x1010, made ghostly
     spirit_<k>_sit_<seed>      -> images/character/spirit_<k>_rest.png
Which seed to use for each character is chosen in tools/art_jobs/picks.json ({"reaper_stand": 3, ...}); without an
entry the first seed found is used.
Usage (from Mumyeong/): python3 tools/import_art.py RAW_DIR [bg] [ch]
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
IMG = os.path.join(os.path.dirname(HERE), "Mumyeong", "images")


def save(img, rel):
    path = os.path.join(IMG, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    print("wrote", rel, img.size)


# ------------------------------------------------------------------------------------------------- backgrounds
def backgrounds(raw):
    for f in sorted(glob.glob(os.path.join(raw, "bg", "*.webp"))):
        name = os.path.splitext(os.path.basename(f))[0]
        img = Image.open(f).convert("RGB")
        # cover-crop to 16:9, then 1920x1080
        w, h = img.size
        tw = int(h * 16 / 9)
        if tw <= w:
            x0 = (w - tw) // 2
            img = img.crop((x0, 0, x0 + tw, h))
        else:
            th = int(w * 9 / 16)
            y0 = (h - th) // 2
            img = img.crop((0, y0, w, y0 + th))
        img = img.resize((1920, 1080), Image.LANCZOS)
        if name == "theater_hall":
            save(img, "events/theater_hall.png")
        elif name.startswith("scene_"):
            save(img, f"events/scenes/{name[6:]}.png")
        elif name.startswith("figure_"):
            save(img, f"events/figures/{name[7:]}.png")


# ------------------------------------------------------------------------------------------------- characters
def cut(img):
    """Crop to the opaque pixels (alpha > 24), dropping faint halo left by background removal."""
    a = np.asarray(img.getchannel("A"))
    a = np.where(a > 24, a, 0).astype(np.uint8)
    img.putalpha(Image.fromarray(a))
    ys, xs = np.nonzero(a)
    if len(xs) == 0:
        return img
    return img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def fit(img, cw, ch, margin_top=10, margin_bottom=12):
    """Scale the figure to the canvas height (or width if wider) and put its feet at the bottom centre."""
    w, h = img.size
    s = min((ch - margin_top - margin_bottom) / h, (cw - 20) / w)
    img = img.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((cw - img.size[0]) // 2, ch - margin_bottom - img.size[1]))
    return canvas


def ghost(img, fade_from=0.62, fade_to=0.97):
    """Painted figure -> guardian spirit: pale blue-white, the painted detail kept as light and shadow, see-through in
    the middle, bright edges, dissolving into mist toward the bottom, a soft glow."""
    a = np.asarray(img, np.float32) / 255
    rgb, alpha = a[..., :3], a[..., 3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    lum = np.clip((lum - 0.08) / 0.8, 0, 1) ** 0.8
    dark = np.array([0.30, 0.42, 0.62], np.float32)
    light = np.array([0.93, 0.97, 1.00], np.float32)
    col = dark + (light - dark) * lum[..., None]
    # see-through middle, brighter rim
    m = Image.fromarray((alpha * 255).astype(np.uint8))
    inner = np.asarray(m.filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
    edge = np.clip((1 - inner) * 2.2, 0, 1)
    out_a = alpha * (0.66 + 0.34 * edge) * (0.75 + 0.25 * lum)
    col = col + edge[..., None] * 0.25
    # dissolve toward the bottom
    h = alpha.shape[0]
    y = np.arange(h)[:, None] / h
    f = np.clip((fade_to - y) / (fade_to - fade_from), 0, 1)
    out_a = out_a * (f * f * (3 - 2 * f))
    res = Image.fromarray((np.dstack([np.clip(col, 0, 1), np.clip(out_a, 0, 1)]) * 255).astype(np.uint8), "RGBA")
    glow_a = res.getchannel("A").filter(ImageFilter.GaussianBlur(16)).point(lambda v: int(v * 0.5))
    glow = Image.new("RGBA", res.size, (205, 232, 255, 0))
    glow.putalpha(glow_a)
    glow.alpha_composite(res)
    return glow


def pick(raw, prefix, picks):
    files = sorted(glob.glob(os.path.join(raw, "ch", prefix + "_*.webp")))
    if not files:
        return None
    want = picks.get(prefix)
    for f in files:
        if want is not None and f.endswith(f"_{want}.webp"):
            return f
    return files[0]


def characters(raw):
    picks_path = os.path.join(HERE, "art_jobs", "picks.json")
    picks = json.load(open(picks_path)) if os.path.exists(picks_path) else {}
    anchors = {}
    f = pick(raw, "reaper_stand", picks)
    if f:
        save(fit(cut(Image.open(f).convert("RGBA")), 560, 860), "character/reaper.png")
    f = pick(raw, "reaper_sit", picks)
    if f:
        save(fit(cut(Image.open(f).convert("RGBA")), 420, 480, 6, 8), "character/reaper_rest.png")
    for k in ("guanyu", "joan", "leonidas", "genghis", "yisunsin", "napoleon"):
        f = pick(raw, f"spirit_{k}_stand", picks)
        if f:
            body = fit(cut(Image.open(f).convert("RGBA")), 680, 1010, 30, 12)
            save(ghost(body), f"character/spirit_{k}.png")
        f = pick(raw, f"spirit_{k}_sit", picks)
        if f:
            body = cut(Image.open(f).convert("RGBA"))
            # seated: the figure takes the lower ~58% of the same 680x1010 canvas
            canvas = Image.new("RGBA", (680, 1010), (0, 0, 0, 0))
            seat = fit(body, 680, 600, 10, 20)
            canvas.alpha_composite(seat, (0, 1010 - 600))
            save(ghost(canvas, 0.80, 0.99), f"character/spirit_{k}_rest.png")
    return anchors


if __name__ == "__main__":
    raw = sys.argv[1]
    parts = sys.argv[2:] or ["bg", "ch"]
    if "bg" in parts:
        backgrounds(raw)
    if "ch" in parts:
        characters(raw)
