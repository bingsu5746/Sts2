"""Placeholder event backgrounds (1000x760) for the slots in MumyeongCode/Events/EventArt.cs.
They exist so every page already has its own mood; replace any file with designed art of the same name and size
(see docs/mumyeong-art-slots.md). Silhouette landscapes only, no text (text in pictures is not localised).

Writes Mumyeong/images/events/theater_hall.png, scenes/<scene>.png (24), figures/<figure>.png (6).
Usage (from Mumyeong/): python3 tools/gen_event_art.py [names...]
"""
import math
import zlib
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_lib import noise  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Mumyeong", "images", "events")
W, H = 1000, 760
SS = 2


class Pic:
    def __init__(self, top, bottom, seed):
        self.seed = seed
        self.rng = np.random.default_rng(seed)
        t = np.linspace(0, 1, H * SS)[:, None, None]
        rgb = np.array(top, np.float32) * (1 - t) + np.array(bottom, np.float32) * t
        self.img = Image.fromarray(np.broadcast_to(rgb, (H * SS, W * SS, 3)).astype(np.uint8), "RGB").convert("RGBA")

    def layer(self):
        return Image.new("RGBA", self.img.size, (0, 0, 0, 0))

    def put(self, layer, blur=0):
        if blur:
            layer = layer.filter(ImageFilter.GaussianBlur(blur * SS))
        self.img.alpha_composite(layer)

    def poly(self, pts, color, blur=0):
        L = self.layer()
        ImageDraw.Draw(L).polygon([(x * SS, y * SS) for x, y in pts], fill=color)
        self.put(L, blur)

    def rect(self, x0, y0, x1, y1, color, blur=0):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], color, blur)

    def ellipse(self, cx, cy, rx, ry, color, blur=0):
        L = self.layer()
        ImageDraw.Draw(L).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=color)
        self.put(L, blur)

    def line(self, pts, color, width, blur=0):
        L = self.layer()
        ImageDraw.Draw(L).line([(x * SS, y * SS) for x, y in pts], fill=color, width=int(width * SS))
        self.put(L, blur)

    def glow(self, cx, cy, r, color, alpha=0.8):
        yy, xx = np.mgrid[0:H * SS, 0:W * SS] / SS
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
        a = np.clip(1 - d, 0, 1) ** 2 * alpha
        L = Image.fromarray(np.dstack([np.full_like(a, c) for c in color] + [a * 255]).astype(np.uint8), "RGBA")
        self.img.alpha_composite(L)

    def fog(self, color, y0, y1, alpha=0.5, seed=0):
        n = noise(W * SS, H * SS, self.seed + seed, scales=(240, 120, 60, 30))
        yy = np.arange(H * SS)[:, None] / SS
        band = np.clip((yy - y0) / max(1, (y1 - y0) * 0.3), 0, 1) * np.clip((y1 - yy) / max(1, (y1 - y0) * 0.3), 0, 1)
        a = np.clip((n - 0.4) * 2, 0, 1) * band * alpha
        L = Image.fromarray(np.dstack([np.full_like(a, c) for c in color] + [a * 255]).astype(np.uint8), "RGBA")
        self.img.alpha_composite(L)

    def specks(self, color, n, y0=0, y1=H, size=1.5, alpha=200):
        L = self.layer()
        d = ImageDraw.Draw(L)
        for _ in range(n):
            x, y = self.rng.uniform(0, W), self.rng.uniform(y0, y1)
            r = self.rng.uniform(0.5, 1) * size
            d.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=tuple(color) + (alpha,))
        self.put(L)

    def ridge(self, y, amp, color, step=40, seed=0, blur=0):
        r = np.random.default_rng(self.seed + seed)
        pts = [(0, H)]
        x = 0
        while x <= W + step:
            pts.append((x, y - r.uniform(0, amp)))
            x += step
        pts.append((W, H))
        self.poly(pts, color, blur)

    def finish(self, path):
        img = self.img.resize((W, H), Image.LANCZOS).convert("RGB")
        # vignette (no film grain: it made every placeholder ~1 MB)
        a = np.asarray(img, np.float32)
        yy, xx = np.mgrid[0:H, 0:W]
        v = 1 - 0.55 * np.clip((((xx - W / 2) / (W * 0.62)) ** 2 + ((yy - H / 2) / (H * 0.62)) ** 2), 0, 1)
        a = np.clip(a * v[:, :, None], 0, 255)
        img = Image.fromarray(a.astype(np.uint8))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path, optimize=True)
        print("wrote", path)


DARK = (8, 8, 12, 255)


def fire(p, cx, cy, w):
    p.glow(cx, cy, w * 2.2, (255, 140, 50), 0.7)
    for i in range(7):
        x = cx + p.rng.uniform(-w, w)
        h = p.rng.uniform(w * 0.6, w * 1.5)
        p.poly([(x - w * 0.25, cy), (x, cy - h), (x + w * 0.25, cy)], (255, 170, 70, 180), blur=3)
    p.specks((255, 190, 120), 60, cy - w * 3, cy, 1.6)


def battlements(p, y, color, x0=0, x1=W):
    p.rect(x0, y, x1, H, color)
    x = x0
    while x < x1:
        p.rect(x, y - 26, x + 28, y + 1, color)
        x += 52


def pillars(p, n, y0, y1, color, x0=120, x1=880, width=46):
    for i in range(n):
        x = x0 + (x1 - x0) * i / max(1, n - 1)
        p.rect(x - width / 2, y0, x + width / 2, y1, color)
        p.rect(x - width / 2 - 8, y0 - 14, x + width / 2 + 8, y0, color)


def tents(p, y, color, count=5):
    for i in range(count):
        x = 80 + i * (840 / max(1, count - 1))
        s = p.rng.uniform(0.7, 1.2)
        p.poly([(x - 70 * s, y), (x, y - 90 * s), (x + 70 * s, y)], color)


def figure_shape(p, cx, base, h, color):
    p.ellipse(cx, base - h * 0.88, h * 0.07, h * 0.08, color)
    p.poly([(cx - h * 0.12, base - h * 0.78), (cx + h * 0.12, base - h * 0.78), (cx + h * 0.15, base),
            (cx - h * 0.15, base)], color)


def waves(p, y, color, amp=18, seed=0):
    for k in range(5):
        r = np.random.default_rng(p.seed + seed + k)
        yy = y + k * 40
        pts = [(0, H)] + [(x, yy + math.sin(x / 50 + k) * amp + r.uniform(-6, 6)) for x in range(0, W + 25, 25)] + [(W, H)]
        c = tuple(int(v * (1 - k * 0.12)) for v in color[:3]) + (255,)
        p.poly(pts, c)


# --------------------------------------------------------------------------------------------- scenes
def s_bridge(p):
    fire(p, 160, 560, 70)
    p.ridge(560, 30, (20, 14, 14, 255), seed=1)
    p.poly([(0, 470), (430, 450), (520, 500), (560, 480), (1000, 470), (1000, 492), (560, 505), (520, 525),
            (430, 474), (0, 494)], DARK)
    for x in range(40, 1000, 70):
        p.line([(x, 470), (x, 600)], DARK[:3] + (255,), 6)
    p.fog((120, 80, 70), 380, 620, 0.4)


def s_siege(p):
    p.ellipse(780, 140, 36, 36, (230, 230, 220, 255), blur=1)
    p.glow(780, 140, 200, (180, 190, 220), 0.25)
    battlements(p, 470, DARK)
    p.rect(420, 520, 580, 760, (4, 4, 6, 255))
    p.line([(480, 540), (500, 620), (470, 700)], (60, 50, 40, 255), 3)
    for i in range(14):
        x = 40 + i * 70
        p.glow(x, 640 + (i % 3) * 30, 30, (255, 150, 60), 0.5)


def s_storm(p):
    waves(p, 520, (20, 40, 60, 255), amp=30)
    p.poly([(350, 520), (700, 470), (680, 540), (380, 590)], DARK)
    p.line([(520, 500), (600, 230)], DARK[:3] + (255,), 10)
    p.line([(600, 230), (640, 300)], DARK[:3] + (255,), 8)
    p.line([(820, 40), (790, 150), (830, 160), (780, 300)], (220, 230, 255, 255), 3, blur=1)
    p.specks((180, 200, 220), 500, 0, 760, 1.0, 120)


def s_temple(p):
    fire(p, 500, 600, 140)
    pillars(p, 6, 260, 700, DARK)
    p.poly([(60, 260), (500, 120), (940, 260)], DARK)
    p.poly([(520, 140), (600, 240), (560, 250)], (0, 0, 0, 0))
    p.fog((90, 60, 50), 100, 400, 0.5)


def s_scaffold(p):
    p.glow(500, 160, 300, (200, 180, 160), 0.3)
    p.rect(330, 430, 670, 470, DARK)
    for x in (350, 650):
        p.rect(x - 8, 470, x + 8, 620, DARK)
    p.rect(495, 230, 507, 430, DARK)
    p.rect(495, 230, 590, 242, DARK)
    figure_shape(p, 500, 430, 120, DARK)
    for i in range(60):
        figure_shape(p, p.rng.uniform(0, W), p.rng.uniform(640, 780), p.rng.uniform(90, 130), (12, 10, 12, 255))


def s_snowmarch(p):
    p.ridge(330, 160, (150, 160, 180, 255), step=80, seed=1)
    p.ridge(450, 120, (90, 100, 120, 255), step=60, seed=2)
    p.ridge(600, 50, (220, 225, 235, 255), step=50, seed=3)
    for i in range(9):
        figure_shape(p, 180 + i * 70, 620 - i * 12, 60 - i * 3, (30, 30, 40, 255))
    p.specks((255, 255, 255), 700, 0, 760, 1.6, 200)


def s_flood(p):
    p.glow(500, 300, 400, (150, 160, 170), 0.2)
    for i, x in enumerate((120, 330, 560, 790)):
        p.poly([(x - 80, 520), (x, 440 - i % 2 * 30), (x + 80, 520)], DARK)
    waves(p, 520, (40, 55, 60, 255), amp=10)
    p.poly([(430, 600), (570, 600), (550, 625), (450, 625)], DARK)


def s_well(p):
    p.ellipse(500, 170, 70, 70, (255, 230, 180, 255), blur=2)
    p.glow(500, 170, 360, (255, 200, 120), 0.4)
    p.ridge(560, 60, (150, 110, 70, 255), step=120, seed=1)
    p.ridge(640, 40, (110, 80, 50, 255), step=150, seed=2)
    p.ellipse(500, 600, 70, 20, DARK)
    p.rect(430, 600, 570, 660, DARK)
    for x in (300, 380, 640, 720, 800):
        figure_shape(p, x, 660, 110, (40, 28, 20, 255))


def s_mine(p):
    p.poly([(250, 760), (250, 300), (500, 160), (750, 300), (750, 760)], (40, 30, 24, 255))
    p.poly([(300, 760), (300, 320), (500, 210), (700, 320), (700, 760)], (10, 8, 8, 255))
    p.glow(500, 520, 160, (255, 180, 90), 0.6)
    p.line([(470, 120), (520, 60), (560, 0)], (200, 200, 180, 255), 6, blur=2)
    p.specks((140, 120, 100), 200, 0, 760, 1.4, 160)


def s_plague(p):
    p.glow(500, 120, 400, (150, 170, 120), 0.25)
    battlements(p, 380, (24, 26, 22, 255))
    p.poly([(400, 760), (400, 470), (500, 400), (600, 470), (600, 760)], (6, 6, 6, 255))
    for i in range(30):
        figure_shape(p, p.rng.uniform(0, W), p.rng.uniform(660, 760), 90, (14, 14, 12, 255))
    p.fog((120, 140, 100), 300, 760, 0.45)


def s_border(p):
    p.specks((220, 220, 240), 200, 0, 350, 1.0)
    p.poly([(0, 500), (1000, 500), (1000, 520), (0, 520)], DARK)
    for x in range(30, 1000, 90):
        p.rect(x, 520, x + 14, 760, DARK)
    for i in range(10):
        x = 700 + p.rng.uniform(0, 300)
        y = p.rng.uniform(440, 490)
        p.glow(x, y, 26, (255, 150, 60), 0.8)
    waves(p, 640, (10, 18, 30, 255), amp=6)


def s_coronation(p):
    p.glow(500, 330, 220, (255, 210, 120), 0.6)
    pillars(p, 4, 200, 760, DARK, 130, 870, 60)
    p.poly([(380, 760), (380, 520), (620, 520), (620, 760)], (20, 14, 10, 255))
    p.ellipse(500, 500, 40, 18, (255, 210, 110, 255), blur=1)
    p.poly([(462, 500), (470, 470), (485, 490), (500, 462), (515, 490), (530, 470), (538, 500)], (255, 210, 110, 255))
    for x in (90, 910):
        p.poly([(x - 40, 760), (x, 640), (x + 40, 760)], (8, 6, 6, 255))


def s_arena(p):
    p.glow(500, 0, 600, (255, 220, 160), 0.35)
    for row in range(3):
        y = 200 + row * 90
        for i in range(12):
            x = i * 90 + (45 if row % 2 else 0)
            p.poly([(x, y + 90), (x, y + 30), (x + 35, y), (x + 70, y + 30), (x + 70, y + 90)], (30, 20, 16, 255))
    p.rect(0, 470, W, H, (90, 70, 50, 255))
    figure_shape(p, 470, 600, 150, DARK)
    p.poly([(520, 610), (620, 590), (630, 610), (520, 625)], DARK)


def s_ambush(p):
    p.poly([(0, 0), (380, 0), (330, 760), (0, 760)], (22, 18, 16, 255))
    p.poly([(1000, 0), (620, 0), (690, 760), (1000, 760)], (22, 18, 16, 255))
    for i in range(40):
        x = p.rng.uniform(380, 620)
        y = p.rng.uniform(0, 500)
        p.line([(x, y), (x + 16, y + 40)], (10, 10, 10, 255), 2)
    p.fog((120, 100, 80), 400, 760, 0.4)


def s_mutiny(p):
    waves(p, 600, (14, 22, 34, 255), amp=8)
    p.poly([(60, 600), (940, 600), (900, 680), (100, 680)], DARK)
    p.line([(500, 600), (500, 90)], DARK[:3] + (255,), 12)
    p.line([(380, 200), (620, 200)], DARK[:3] + (255,), 8)
    for x in (300, 380, 460, 560, 640, 720):
        figure_shape(p, x, 600, 130, (16, 14, 16, 255))
    p.glow(500, 520, 160, (255, 160, 80), 0.4)


def s_famine(p):
    p.glow(500, 250, 400, (200, 160, 100), 0.25)
    battlements(p, 420, (30, 24, 18, 255), 0, 600)
    p.rect(620, 520, 1000, 760, (40, 32, 22, 255))
    for x in (700, 820, 930):
        p.poly([(x - 60, 520), (x, 460), (x + 60, 520)], (24, 18, 14, 255))
    for i in range(12):
        figure_shape(p, 640 + i * 30, 700, 70, (10, 8, 8, 255))


def s_duel(p):
    p.glow(500, 470, 500, (255, 190, 140), 0.55)
    p.ridge(480, 30, (60, 50, 60, 255), step=100, seed=1)
    p.rect(0, 520, W, H, (30, 26, 30, 255))
    figure_shape(p, 380, 600, 170, DARK)
    figure_shape(p, 620, 600, 170, DARK)
    p.line([(410, 520), (560, 470)], DARK[:3] + (255,), 4)
    p.line([(590, 520), (450, 480)], DARK[:3] + (255,), 4)


def s_exile(p):
    p.specks((230, 230, 255), 300, 0, 400, 1.0)
    p.ellipse(220, 140, 30, 30, (220, 220, 230, 255))
    waves(p, 470, (14, 20, 34, 255), amp=5)
    p.poly([(330, 480), (480, 400), (620, 410), (720, 480)], DARK)
    p.glow(860, 470, 30, (255, 220, 160), 0.9)
    p.poly([(830, 470), (890, 470), (880, 485), (840, 485)], DARK)


def s_false_order(p):
    p.poly([(0, 760), (0, 220), (500, 40), (1000, 220), (1000, 760)], (40, 30, 22, 255))
    p.glow(500, 420, 200, (255, 190, 110), 0.7)
    p.rect(330, 520, 670, 560, DARK)
    p.poly([(440, 480), (560, 470), (570, 515), (450, 525)], (230, 210, 170, 255))
    figure_shape(p, 500, 760, 260, (6, 5, 5, 255))


def s_river(p):
    p.ridge(380, 60, (30, 40, 34, 255), step=60, seed=1)
    waves(p, 470, (40, 60, 70, 255), amp=14)
    p.poly([(420, 560), (580, 560), (560, 590), (440, 590)], DARK)
    for i in range(12):
        p.glow(80 + i * 22, 380 + p.rng.uniform(-10, 10), 18, (255, 150, 60), 0.8)


def s_tribunal(p):
    p.glow(500, 100, 450, (210, 200, 180), 0.35)
    pillars(p, 6, 120, 760, (36, 32, 30, 255), 80, 920, 50)
    p.rect(250, 380, 750, 520, (20, 16, 14, 255))
    for x in (380, 500, 620):
        figure_shape(p, x, 380, 120, DARK)
    figure_shape(p, 500, 760, 200, (6, 6, 6, 255))


def s_hostage(p):
    p.rect(0, 520, W, H, (20, 18, 16, 255))
    for x in (200, 800):
        p.glow(x, 440, 70, (255, 150, 60), 0.8)
        p.rect(x - 4, 450, x + 4, 560, DARK)
    figure_shape(p, 560, 640, 220, DARK)
    figure_shape(p, 500, 650, 180, (16, 14, 14, 255))
    p.fog((90, 70, 60), 400, 760, 0.4)


def s_last_camp(p):
    p.specks((210, 210, 230), 250, 0, 380, 1.0)
    p.ridge(480, 80, (20, 20, 26, 255), step=70, seed=1)
    tents(p, 600, (24, 20, 20, 255))
    fire(p, 500, 690, 40)
    for x in (400, 450, 560, 610):
        figure_shape(p, x, 700, 90, DARK)


def s_tower(p):
    p.glow(500, 760, 500, (255, 130, 50), 0.6)
    p.rect(160, 200, 380, 760, DARK)
    p.rect(640, 260, 860, 760, DARK)
    for x in range(160, 380, 44):
        p.rect(x, 176, x + 24, 202, DARK)
    for x in range(640, 860, 44):
        p.rect(x, 236, x + 24, 262, DARK)
    figure_shape(p, 330, 200, 90, DARK)
    p.specks((255, 190, 120), 150, 300, 760, 1.8)


SCENES = {
    "bridge": ((40, 20, 24), (110, 50, 30), s_bridge),
    "siege": ((10, 14, 30), (30, 26, 40), s_siege),
    "storm": ((20, 26, 34), (40, 50, 60), s_storm),
    "temple": ((50, 26, 20), (120, 60, 30), s_temple),
    "scaffold": ((70, 64, 66), (40, 36, 40), s_scaffold),
    "snowmarch": ((120, 130, 150), (200, 205, 215), s_snowmarch),
    "flood": ((60, 66, 72), (30, 40, 44), s_flood),
    "well": ((240, 180, 110), (180, 120, 70), s_well),
    "mine": ((20, 16, 14), (10, 8, 8), s_mine),
    "plague": ((60, 70, 50), (30, 34, 26), s_plague),
    "border": ((6, 10, 22), (14, 20, 34), s_border),
    "coronation": ((30, 20, 14), (14, 10, 8), s_coronation),
    "arena": ((190, 150, 100), (120, 90, 60), s_arena),
    "ambush": ((90, 80, 70), (40, 34, 30), s_ambush),
    "mutiny": ((24, 24, 36), (40, 30, 30), s_mutiny),
    "famine": ((110, 90, 60), (60, 46, 30), s_famine),
    "duel": ((60, 50, 70), (200, 130, 100), s_duel),
    "exile": ((6, 8, 20), (14, 20, 36), s_exile),
    "false_order": ((20, 16, 12), (10, 8, 6), s_false_order),
    "river": ((40, 46, 50), (30, 40, 44), s_river),
    "tribunal": ((80, 76, 70), (40, 36, 32), s_tribunal),
    "hostage": ((20, 14, 14), (40, 26, 20), s_hostage),
    "last_camp": ((8, 10, 22), (20, 18, 28), s_last_camp),
    "tower": ((20, 10, 10), (120, 50, 20), s_tower),
}


# --------------------------------------------------------------------------------------------- hall / figures
def hall():
    p = Pic((14, 16, 24), (6, 6, 9), 1)
    p.rect(160, 90, 840, 470, (190, 205, 220, 255), blur=1)
    p.glow(500, 280, 520, (150, 180, 210), 0.3)
    p.poly([(500, 760), (160, 470), (840, 470)], (170, 200, 225, 30), blur=12)
    for row, (y, s) in enumerate([(560, 1.0), (640, 1.15), (720, 1.3)]):
        x = -20 + row * 25
        while x < W:
            p.poly([(x, y), (x + 40 * s, y - 8), (x + 80 * s, y), (x + 78 * s, y + 90), (x + 2, y + 90)],
                   (20, 22, 30, 255))
            x += 95 * s
    for x in (380, 500, 620):  # three shadows in the seats
        figure_shape(p, x, 610, 120, (2, 2, 4, 255))
    p.specks((220, 235, 245), 160, 470, 760, 1.2, 140)
    return p


def figure_bg(key):
    # a place tied to the figure, in the figure's colour; the spirit itself is drawn over it by EventArt.PlayRise
    col = {"guanyu": (47, 143, 91), "joan": (217, 196, 106), "leonidas": (181, 70, 47), "genghis": (138, 106, 60),
           "yisunsin": (58, 95, 158), "napoleon": (91, 74, 158)}[key]
    dark = tuple(int(c * 0.15) for c in col)
    mid = tuple(int(c * 0.45) for c in col)
    p = Pic(mid, dark, zlib.crc32(key.encode()) % 1000)
    p.glow(500, 330, 420, tuple(min(255, int(c * 1.4)) for c in col), 0.45)
    if key == "yisunsin":  # the southern sea at dusk, a turtle ship and panokseon silhouettes
        waves(p, 520, tuple(int(c * 0.3) for c in col) + (255,), amp=8)
        p.poly([(120, 560), (330, 560), (300, 600), (150, 600)], DARK)
        p.poly([(150, 560), (240, 520), (310, 560)], DARK)
        p.poly([(700, 540), (900, 540), (880, 580), (720, 580)], DARK)
        p.line([(800, 540), (800, 420)], DARK[:3] + (255,), 5)
    elif key == "guanyu":  # a river crossing and mountains, a war banner
        p.ridge(430, 160, tuple(int(c * 0.25) for c in col) + (255,), step=90, seed=1)
        waves(p, 600, tuple(int(c * 0.2) for c in col) + (255,), amp=4)
        p.line([(820, 600), (820, 260)], DARK[:3] + (255,), 6)
        p.poly([(822, 270), (930, 290), (822, 340)], DARK)
    elif key == "joan":  # a walled city at dawn, banners
        battlements(p, 520, tuple(int(c * 0.2) for c in col) + (255,))
        p.poly([(420, 520), (440, 380), (460, 520)], DARK)
        p.poly([(560, 520), (590, 330), (620, 520)], DARK)
        p.line([(150, 520), (150, 300)], DARK[:3] + (255,), 5)
        p.poly([(152, 305), (240, 320), (152, 360)], DARK)
    elif key == "leonidas":  # the narrow pass between cliff and sea
        p.poly([(0, 0), (420, 0), (380, 760), (0, 760)], tuple(int(c * 0.2) for c in col) + (255,))
        waves(p, 560, tuple(int(c * 0.15) for c in col) + (255,), amp=6)
        for i in range(9):
            p.ellipse(430 + i * 28, 600, 14, 18, DARK)
    elif key == "genghis":  # the open steppe, gers, horses
        p.ridge(520, 20, tuple(int(c * 0.3) for c in col) + (255,), step=160, seed=1)
        for x in (160, 260, 760):
            p.ellipse(x, 540, 50, 26, DARK)
            p.rect(x - 50, 540, x + 50, 580, DARK)
        p.specks((255, 240, 220), 120, 0, 400, 1.0)
    elif key == "napoleon":  # a field at dawn, ranks and a cannon
        p.ridge(540, 30, tuple(int(c * 0.25) for c in col) + (255,), step=120, seed=1)
        p.ellipse(500, 600, 40, 40, DARK)
        p.poly([(500, 590), (640, 560), (645, 575), (505, 610)], DARK)
        for i in range(20):
            figure_shape(p, 60 + i * 46, 560, 50, (10, 8, 14, 255))
    p.fog((200, 210, 230), 380, 760, 0.3)
    return p


def main():
    only = sys.argv[1:]
    want = lambda k: not only or k in only  # noqa: E731
    if want("hall"):
        hall().finish(os.path.join(OUT, "theater_hall.png"))
    for k, (top, bottom, fn) in SCENES.items():
        if want(k):
            p = Pic(top, bottom, zlib.crc32(k.encode()) % 10000)
            fn(p)
            p.finish(os.path.join(OUT, "scenes", f"{k}.png"))
    for k in ("guanyu", "joan", "leonidas", "genghis", "yisunsin", "napoleon"):
        if want(k):
            figure_bg(k).finish(os.path.join(OUT, "figures", f"{k}.png"))


if __name__ == "__main__":
    main()
