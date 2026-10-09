"""images/vfx/fx/*.png: small white-on-transparent textures for the sword effects (Visuals/SwordFx.cs) and the boss-kill
cut-in (Visuals/BossKillCutIn.cs). All are white (tinted in game with SwordVisuals.ColorOf); the alpha carries the shape.
Painterly: soft edges, dry-brush streaks, noisy smoke. Deterministic (fixed seed).
Re-run:  python3 tools/gen_fx_textures.py"""
import os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'MagicSwordsman', 'images', 'vfx', 'fx')
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(20261009)


def save(name, alpha):
    a = np.clip(alpha, 0, 1)
    img = np.zeros(a.shape + (4,), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, name + '.png'))
    print(name, a.shape)


def grid(w, h):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    return (x + 0.5) / w * 2 - 1, (y + 0.5) / h * 2 - 1  # -1..1


def noise(w, h, scale, octaves=4):
    """Smooth value noise in 0..1 (bilinear upsampled random grids)."""
    acc = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        gw, gh = max(2, int(w / scale * 2 ** o)), max(2, int(h / scale * 2 ** o))
        g = Image.fromarray((rng.random((gh, gw)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        acc += amp * np.asarray(g, np.float32) / 255; tot += amp; amp *= 0.5
    return acc / tot


# soft round glow
x, y = grid(128, 128); r2 = x * x + y * y
save('fx_glow', np.exp(-r2 * 5.5) * (r2 < 1))

# spark / ember streak, vertical (CpuParticles2D ParticleFlagAlignY points the texture's y axis along the velocity)
x, y = grid(16, 64)
save('fx_spark', np.exp(-(y * y) * 3.0 - (x * x) * 9.0) * (1 - np.abs(y)) ** 0.5)

# smoke / mist puff
x, y = grid(128, 128); r = np.sqrt(x * x + y * y)
n = noise(128, 128, 48)
save('fx_smoke', np.clip((1 - r) * 1.4, 0, 1) ** 1.6 * (0.35 + 0.9 * n))

# thin soft ring (shockwaves)
x, y = grid(256, 256); r = np.sqrt(x * x + y * y)
save('fx_ring', np.exp(-((r - 0.86) / 0.045) ** 2) + 0.35 * np.exp(-((r - 0.80) / 0.09) ** 2))

# dry-brush stroke band, horizontal, heavy at the left end and tapering ragged to the right
W, H = 1024, 192
x, y = grid(W, H); u = (x + 1) / 2  # 0..1 along the stroke
rows = noise(1, H, 3, 1)[:, 0]  # per-row bristle density
bristle = np.repeat(rows[:, None], W, 1)
streak = noise(W, H, 6, 3)
streak = 0.55 + 0.45 * np.asarray(Image.fromarray((streak * 255).astype(np.uint8)).resize((W // 8, H)).resize((W, H), Image.BICUBIC), np.float32) / 255
wob = noise(W, 1, 30, 3)[0] - 0.5
top = 0.80 - 0.50 * u ** 1.5 + 0.14 * wob            # ragged upper / lower edges, narrowing to the right
bot = 0.80 - 0.42 * u ** 1.5 + 0.14 * (noise(W, 1, 30, 3)[0] - 0.5)
edge = np.clip((np.where(y < 0, top, bot) - np.abs(y)) / 0.025, 0, 1)
start = np.clip(u / 0.03, 0, 1) ** 0.5 * (0.85 + 0.15 * np.clip(1 - u / 0.08, 0, 1))
end = np.clip((1 - u) / 0.2, 0, 1)
gapstart = 0.25 + 0.7 * rows  # each bristle row runs dry at its own point along the stroke
dry = np.clip(1 - (u - gapstart[:, None] * (0.8 + 0.4 * streak)) / 0.05, 0.12, 1)
save('fx_brush', edge * start * end ** 0.7 * dry * streak)

# ink splat blot
x, y = grid(96, 96); r = np.sqrt(x * x + y * y); ang = np.arctan2(y, x)
lobes = 0.55 + 0.18 * np.sin(ang * 5 + 1.3) + 0.1 * np.sin(ang * 9) + 0.12 * (noise(96, 96, 20) - 0.5)
blot = np.clip((lobes - r) / 0.05, 0, 1)
for k in range(5):  # satellite droplets
    a = rng.uniform(0, 6.28); d = rng.uniform(0.7, 0.9); s = rng.uniform(0.05, 0.09)
    blot = np.maximum(blot, np.clip((s - np.sqrt((x - d * np.cos(a)) ** 2 + (y - d * np.sin(a)) ** 2)) / 0.03, 0, 1))
save('fx_ink', blot)

# shard (irregular sliver of blade), pointing right
x, y = grid(64, 64)
shard = ((y > -0.35 * (1 - (x + 1) / 2) - 0.05) & (y < 0.25 * (1 - (x + 1) / 2) + 0.02) & (x > -0.85)).astype(np.float32)
img = Image.fromarray((shard * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
save('fx_shard', np.asarray(img, np.float32) / 255 * (0.7 + 0.3 * (y < 0)))

# leaf / grass blade, pointing right
x, y = grid(48, 24)
w_ = 0.75 * np.sin(np.clip((x + 1) / 2, 0, 1) * np.pi) ** 0.8
save('fx_leaf', np.clip((w_ - np.abs(y + 0.15 * x * x)) / 0.15, 0, 1) * (0.75 + 0.25 * (np.abs(y) < 0.08)))

# drop (blood), round end down
x, y = grid(32, 48)
yy = (y + 1) / 2
wid = np.where(yy > 0.62, np.sqrt(np.clip(1 - ((yy - 0.62) / 0.38) ** 2, 0, 1)), (yy / 0.62) ** 1.4) * 0.9
save('fx_drop', np.clip((wid - np.abs(x)) / 0.12, 0, 1))

# vertical light pillar
x, y = grid(128, 512)
save('fx_pillar', np.exp(-(x * x) * 7) * np.clip((1 - np.abs(y)) / 0.35, 0, 1) * (0.85 + 0.15 * noise(128, 512, 64, 2)))

# white crescent cut, from the alpha of the existing purple slash (images/vfx/slash.png) so it takes any sword colour
src = Image.open(os.path.join(HERE, '..', 'MagicSwordsman', 'images', 'vfx', 'slash.png')).convert('RGBA')
save('fx_crescent', np.asarray(src.getchannel('A'), np.float32) / 255)
