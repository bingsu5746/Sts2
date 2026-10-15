"""images/swords/broken/: the broken Gram (level 0, "부서진 그람") cut cleanly out of images/swords/gram.png.

 - gram_top.png / gram_bottom.png: the two pieces on the SAME canvas as gram.png (so they line up exactly when drawn
   at the same spot), split along one jagged break line across the blade, with two small chips missing so the edges
   never match perfectly, and a thin molten-gold rim along each broken edge.
 - aura_gram_top.png / aura_gram_bottom.png: the soft glow of each piece (same algorithm as tools/gen_sword_auras.py,
   canvas + PAD on every side), so the aura follows the broken silhouette instead of the whole sword.
 - gram_seam.png: a soft white glow along the break line (tinted gold in game, drawn in the gap between the pieces).
SwordVisuals.AddBrokenGram reads BREAK_PIVOT below (keep the two in step).
Re-run after changing gram.png:  python3 tools/gen_gram_shards.py"""
import os
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SW = os.path.join(HERE, '..', 'MagicSwordsman', 'images', 'swords')
OUT = os.path.join(SW, 'broken')
PAD = 60
BREAK_PIVOT = (127, 352)  # texture px: middle of the break line (SwordVisuals.BrokenPivot)
os.makedirs(OUT, exist_ok=True)

src = Image.open(os.path.join(SW, 'gram.png')).convert('RGBA')
W, H = src.size
rgba = np.array(src).astype(np.float32)

# the break: a slanted line across the blade (blade spans x 70..185 here) with sharp, irregular teeth
rng = np.random.default_rng(1013)
xs = [40.0]
while xs[-1] < 216: xs.append(xs[-1] + rng.uniform(5, 17))  # irregular spacing
xs = np.array(xs)
# mostly shallow, uneven steps with a few deep splinters (never a regular saw)
teeth = rng.normal(0, 6, len(xs))
inside = np.where((xs > 78) & (xs < 178))[0]
for k, i in enumerate(rng.choice(inside, 4, replace=False)): teeth[i] += (1 if k % 2 else -1) * rng.uniform(12, 20)
line_pts = [(float(x), BREAK_PIVOT[1] + 0.32 * (x - BREAK_PIVOT[0]) + t) for x, t in zip(xs, teeth)]
line_y = np.interp(np.arange(W), [p[0] for p in line_pts], [p[1] for p in line_pts])

yy, xx = np.mgrid[0:H, 0:W]
soft = np.clip(line_y[None, :] - yy + 0.5, 0, 1)  # anti-aliased: 1 above the line, 0 below
above = soft
dist = np.abs(yy - line_y[None, :])  # vertical distance to the break (good enough for a rim)


def chip(mask, cx, cy, r):
    """Removes a small irregular triangle at (cx, cy): a fragment lost when the blade broke."""
    img = Image.new('L', (W, H), 0)
    ImageDraw.Draw(img).polygon([(cx - r, cy), (cx + r * 0.7, cy - r * 0.35), (cx + r * 0.2, cy + r * 0.9)], fill=255)
    return mask * (1 - np.array(img.filter(ImageFilter.GaussianBlur(0.7))) / 255.0)


top_mask = above.copy()
bottom_mask = 1 - above
top_mask = chip(top_mask, 80, line_y[80] - 2, 13)
bottom_mask = chip(bottom_mask, 168, line_y[168] + 3, 15)


def piece(mask):
    out = rgba.copy()
    out[..., 3] *= mask
    # molten-gold rim along the broken edge only (fades over ~7 px), inside the blade
    rim = np.clip(1 - dist / 7.0, 0, 1) * (out[..., 3] > 0)
    gold = np.array([255, 190, 90], np.float32)
    out[..., :3] = out[..., :3] * (1 - 0.65 * rim[..., None]) + gold * 0.65 * rim[..., None]
    return out


def aura(alpha):
    big = Image.new('L', (W + 2 * PAD, H + 2 * PAD))
    big.paste(Image.fromarray(alpha.astype(np.uint8)), (PAD, PAD))
    g = np.array(big.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(26))).astype(np.float32) / 255
    g = np.clip(g * 1.4, 0, 1) ** 1.3
    out = np.zeros(g.shape + (4,), np.uint8)
    out[..., :3] = (g * 255)[..., None]
    out[..., 3] = g * 255
    return Image.fromarray(out)


for name, mask in (('top', top_mask), ('bottom', bottom_mask)):
    p = piece(mask)
    Image.fromarray(np.clip(p, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, f'gram_{name}.png'))
    aura(p[..., 3]).save(os.path.join(OUT, f'aura_gram_{name}.png'))
    print('gram', name)

# seam glow: the break line drawn thick and blurred, on a canvas centred on the pivot
SW_W, SW_H = 300, 160
seam = Image.new('L', (SW_W, SW_H), 0)
ox, oy = BREAK_PIVOT[0] - SW_W / 2, BREAK_PIVOT[1] - SW_H / 2
pts = [(x - ox, line_y[int(x)] - oy) for x in range(76, 181, 2)]  # inside the blade only (x 70..185)
ImageDraw.Draw(seam).line(pts, fill=255, width=8)
s = np.array(seam.filter(ImageFilter.GaussianBlur(12))).astype(np.float32) / 255
xx2 = np.arange(SW_W)[None, :] + ox
taper = np.clip(1 - np.abs(xx2 - BREAK_PIVOT[0]) / 62.0, 0, 1) ** 0.6  # fades out toward the blade's edges
a = np.clip(s * 1.5 * taper, 0, 1)
img = np.zeros(a.shape + (4,), np.uint8)
img[..., :3] = 255
img[..., 3] = a * 255
Image.fromarray(img, 'RGBA').save(os.path.join(OUT, 'gram_seam.png'))
print('seam', SW_W, SW_H)
