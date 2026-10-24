"""Character art for 무명 (procedural, no external services; user request 2026-10-24):
a reaper in a long black coat and a black fedora, face hidden in smoke; after the 코어 is bound, the figure's spirit
stands behind him like a guardian, and at the rest site it sits beside him.

Writes, under Mumyeong/images/character/:
  reaper.png               combat / shop body (feet at the bottom centre)
  reaper_rest.png          sitting by the fire
  spirit_<key>.png         standing guardian spirit (pale, fading into mist at the bottom)
  spirit_<key>_rest.png    the same figure sitting
  char_select_bg.png       character select background (1920x1080)
Usage (from Mumyeong/): python3 tools/gen_character.py [--preview DIR]
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_lib import SS, Canvas, mask_from_array, noise, rot, shift, vertical_fade  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Mumyeong", "images", "character")

COAT = (20, 21, 27)
COAT_HI = (52, 57, 70)
COAT_DARK = (9, 9, 12)
HAT = (16, 16, 20)
BAND = (46, 42, 44)
RIM = (150, 205, 230)
SMOKE_D = (18, 19, 24)
SMOKE_L = (150, 156, 172)


# =====================================================================================================================
# the reaper
# =====================================================================================================================

def smoke_face(cv, cx, cy, rx, ry, seed, upward=60):
    """A swirl of smoke where the face would be, with wisps escaping upward and to the right."""
    W, H = cv.w * SS, cv.h * SS
    n = noise(W, H, seed, scales=(60, 30, 15, 8))
    n2 = noise(W, H, seed + 7, scales=(40, 20, 10))
    yy, xx = np.mgrid[0:H, 0:W] / SS
    # elliptical body of the smoke, slightly distorted by noise
    d = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    d = d + (n - 0.5) * 0.9
    core = np.clip((1.25 - d) * 2.2, 0, 1)
    # wisps: above the head band, thin noisy tongues going up-right
    up = np.clip((cy - yy) / upward, 0, 1)
    tongue = np.clip(1 - np.abs((xx - cx - (cy - yy) * 0.45) / (rx * (1.1 - up * 0.6))), 0, 1)
    wisps = np.clip((n2 - 0.55) * 4, 0, 1) * tongue * (1 - up) * (yy < cy)
    a = np.clip(core * 1.3, 0, 1) * 0.97 + wisps * 0.55
    swirl = np.clip(np.sin((xx - cx) * 0.09 + (yy - cy) * 0.05 + n * 9) * 0.5 + 0.5, 0, 1)
    shade = np.clip(0.1 + n * 0.55 + swirl * 0.25 - d * 0.25 + (yy - cy) / ry * 0.15, 0, 1) ** 1.6
    cv.texture_fill(mask_from_array(np.clip(a, 0, 1)), shade, SMOKE_D, SMOKE_L)
    # two faint cold glints deep inside (not eyes you can read — just something looking back)
    for ex in (cx - rx * 0.30, cx + rx * 0.22):
        cv.fill(cv.ellipse(ex, cy - ry * 0.05, 3.2, 1.6), (200, 235, 255), alpha=0.45, blur=2.5)


def wisps(cv, paths, seed):
    """Thin smoke tongues curling up from under the brim (soft, low alpha, broken up by noise)."""
    W, H = cv.w * SS, cv.h * SS
    n = mask_from_array(np.clip((noise(W, H, seed + 5, scales=(24, 12, 6)) - 0.3) * 1.8, 0, 1))
    for path in paths:
        m = cv.tapered(list(path), 16, 3)
        cv.fill(m, (120, 126, 142), alpha=0.5, blur=4, clip=n)
        cv.fill(cv.tapered(list(path), 6, 1), (170, 176, 190), alpha=0.35, blur=1.5, clip=n)


def fedora(cv, cx, top, tilt=-4):
    """Black fedora: crown with a centre pinch, band, wide brim (cx = centre, top = top of the crown)."""
    b = top + 92  # brim line
    crown = [(cx - 72, b - 2), (cx - 76, b - 40), (cx - 66, top + 18), (cx - 34, top + 4), (cx - 6, top + 16),
             (cx + 14, top + 4), (cx + 46, top + 8), (cx + 68, top + 26), (cx + 74, b - 40), (cx + 72, b - 2)]
    crown = rot(crown, cx, b, tilt)
    m = cv.shape(crown)
    cv.gradient_fill(m, (34, 34, 42), HAT, top, b)
    # pinch shading + crease
    cv.fill(cv.line(rot([(cx - 4, top + 18), (cx - 2, top + 44), (cx - 6, top + 66)], cx, b, tilt), 5), COAT_DARK,
            alpha=0.8, blur=1.5)
    cv.fill(cv.line(rot([(cx - 50, top + 22), (cx - 30, top + 12), (cx - 12, top + 20)], cx, b, tilt), 3), COAT_HI,
            alpha=0.5, blur=1.2)
    # band
    band = rot([(cx - 74, b - 30), (cx + 74, b - 30), (cx + 73, b - 6), (cx - 73, b - 6)], cx, b, tilt)
    cv.fill(cv.shape(band, smooth=False), BAND)
    cv.fill(cv.shape(rot([(cx - 74, b - 30), (cx + 74, b - 30), (cx + 74, b - 27), (cx - 74, b - 27)], cx, b, tilt),
                     smooth=False), (90, 84, 86), alpha=0.6)
    # brim (an ellipse seen slightly from below: front edge curls down)
    brim = rot([(cx - 114, b + 4), (cx - 84, b - 14), (cx, b - 20), (cx + 88, b - 14), (cx + 118, b + 2),
                (cx + 98, b + 14), (cx, b + 20), (cx - 94, b + 16)], cx, b, tilt)
    bm = cv.shape(brim)
    cv.gradient_fill(bm, (38, 38, 46), HAT, b - 20, b + 20)
    cv.fill(cv.line(rot([(cx - 108, b + 6), (cx - 40, b + 18), (cx + 50, b + 17), (cx + 112, b + 5)], cx, b, tilt),
                    2.2), (80, 86, 100), alpha=0.55, blur=0.8)
    return cv.shape(crown, mask=bm.copy())


def reaper_stand(seed=11):
    cv = Canvas(560, 860)
    cx = 280
    sil = cv.blank()

    # ---- legs and shoes (behind the coat)
    for lx in (cx - 36, cx + 34):
        leg = [(lx - 22, 640), (lx + 22, 640), (lx + 20, 808), (lx - 20, 808)]
        m = cv.shape(leg, smooth=False)
        cv.gradient_fill(m, (24, 24, 30), (12, 12, 15), 640, 808)
        sil = cv.shape(leg, smooth=False, mask=sil)
        shoe = [(lx - 24, 800), (lx + 30, 798), (lx + 44, 812), (lx + 40, 826), (lx - 26, 826)]
        cv.fill(cv.shape(shoe), (8, 8, 10))
        cv.fill(cv.line([(lx - 18, 803), (lx + 30, 801)], 2), (95, 100, 115), alpha=0.6)
        sil = cv.shape(shoe, mask=sil)

    # ---- coat body (long, flaring, the hem tearing into smoke)
    coat = [(cx - 100, 268), (cx - 60, 252), (cx, 262), (cx + 62, 252), (cx + 104, 270), (cx + 108, 360),
            (cx + 100, 432), (cx + 112, 480), (cx + 132, 600), (cx + 146, 712), (cx + 110, 722), (cx + 60, 714), (cx + 10, 728),
            (cx - 40, 720), (cx - 96, 732), (cx - 172, 722), (cx - 196, 700), (cx - 150, 640), (cx - 128, 580), (cx - 108, 480), (cx - 98, 432), (cx - 104, 360)]
    cm = cv.shape(coat)
    sil = cv.shape(coat, mask=sil)
    cv.gradient_fill(cm, (30, 32, 40), COAT_DARK, 260, 730)
    # folds
    for pts, a in [([(cx - 70, 470), (cx - 84, 590), (cx - 108, 712)], 0.9),
                   ([(cx + 64, 470), (cx + 84, 600), (cx + 120, 716)], 0.9),
                   ([(cx - 20, 470), (cx - 26, 600), (cx - 32, 716)], 0.7),
                   ([(cx + 22, 470), (cx + 30, 600), (cx + 44, 718)], 0.6)]:
        cv.fill(cv.line(pts, 7), COAT_DARK, alpha=a, blur=3, clip=cm)
    for pts in ([(cx - 52, 480), (cx - 62, 600), (cx - 80, 712)], [(cx + 86, 480), (cx + 104, 600), (cx + 140, 708)]):
        cv.fill(cv.line(pts, 5), COAT_HI, alpha=0.45, blur=3, clip=cm)
    # front opening line + buttons
    cv.fill(cv.line([(cx + 6, 420), (cx + 8, 560), (cx + 10, 720)], 2.5), COAT_DARK, alpha=0.9, clip=cm)
    for by in (470, 520, 570):
        cv.fill(cv.ellipse(cx - 6, by, 4, 4), (60, 64, 76))
        cv.fill(cv.ellipse(cx - 7, by - 1, 1.5, 1.5), (160, 170, 190), alpha=0.7)
    # belt
    belt = [(cx - 118, 418), (cx + 118, 418), (cx + 120, 440), (cx - 120, 440)]
    cv.fill(cv.shape(belt, smooth=False), (14, 14, 17), clip=cm)
    cv.fill(cv.shape([(cx - 14, 414), (cx + 18, 414), (cx + 18, 444), (cx - 14, 444)], smooth=False), (70, 72, 80))
    cv.fill(cv.shape([(cx - 8, 420), (cx + 12, 420), (cx + 12, 438), (cx - 8, 438)], smooth=False), (14, 14, 17))

    # ---- lapels
    lap_l = [(cx - 64, 262), (cx - 6, 300), (cx + 4, 420), (cx - 40, 360), (cx - 80, 290)]
    lap_r = [(cx + 64, 262), (cx + 14, 300), (cx + 6, 420), (cx + 44, 356), (cx + 84, 290)]
    for lp in (lap_l, lap_r):
        m = cv.shape(lp)
        cv.gradient_fill(m, (40, 43, 54), (18, 19, 24), 262, 420)
    cv.fill(cv.line([(cx - 64, 264), (cx - 30, 330), (cx + 4, 418)], 2), (90, 96, 112), alpha=0.5)
    # shirt / tie under the smoke (barely there)
    cv.fill(cv.shape([(cx - 10, 290), (cx + 12, 290), (cx + 6, 400), (cx - 2, 400)]), (34, 22, 26))

    # ---- high collar: two upturned flaps, open in the middle where the smoke is
    for flap in ([(cx - 92, 282), (cx - 88, 234), (cx - 58, 244), (cx - 26, 264), (cx - 8, 284)],
                 [(cx + 8, 284), (cx + 28, 262), (cx + 60, 240), (cx + 94, 228), (cx + 98, 280)]):
        clm = cv.shape(flap, smooth=False)
        sil = cv.shape(flap, smooth=False, mask=sil)
        cv.gradient_fill(clm, (46, 49, 60), (16, 17, 22), 204, 282)
        cv.fill(cv.line(flap[1:4], 2), (110, 118, 136), alpha=0.45)
    cv.fill(cv.shape([(cx - 30, 250), (cx + 30, 250), (cx + 20, 290), (cx - 20, 290)]), (8, 8, 10))  # throat shadow

    # ---- arms (sleeves) and gloves
    arm_l = [(cx - 100, 270), (cx - 128, 300), (cx - 140, 420), (cx - 138, 540), (cx - 108, 548), (cx - 104, 430),
             (cx - 96, 330)]
    arm_r = [(cx + 104, 272), (cx + 132, 304), (cx + 146, 420), (cx + 146, 540), (cx + 116, 548), (cx + 110, 430),
             (cx + 100, 330)]
    for am, hx in ((arm_l, cx - 124), (arm_r, cx + 132)):
        m = cv.shape(am)
        sil = cv.shape(am, mask=sil)
        cv.gradient_fill(m, (34, 36, 46), (14, 14, 18), 270, 550)
        inner = cx - 100 if hx < cx else cx + 104
        cv.fill(cv.line([(inner, 300), (inner + (2 if hx > cx else -2), 420), (inner + (8 if hx > cx else -8), 540)],
                        3), (4, 4, 6), alpha=0.9, clip=m)
        cv.fill(cv.line([(hx + 8, 330), (hx + 4, 440), (hx + 6, 530)], 4), COAT_DARK, alpha=0.8, blur=2.5, clip=m)
        # cuff
        cv.fill(cv.shape([(hx - 18, 528), (hx + 18, 530), (hx + 16, 550), (hx - 18, 548)], smooth=False), (12, 12, 15))
        # gloved hand
        hand = [(hx - 14, 548), (hx + 14, 548), (hx + 16, 580), (hx + 8, 600), (hx - 8, 602), (hx - 16, 584)]
        cv.fill(cv.shape(hand), (22, 22, 26))
        sil = cv.shape(hand, mask=sil)
        cv.fill(cv.line([(hx - 6, 556), (hx - 8, 590)], 1.5), (90, 96, 110), alpha=0.5)

    # ---- smoke where the face is, then the hat over it
    smoke_face(cv, cx + 4, 236, 58, 50, seed)
    hat = fedora(cv, cx + 2, 112)
    wisps(cv, [((cx - 96, 214), (cx - 120, 170), (cx - 108, 120), (cx - 128, 80)),
               ((cx + 100, 210), (cx + 128, 168), (cx + 116, 126), (cx + 140, 96)),
               ((cx + 40, 262), (cx + 70, 290), (cx + 96, 278))], seed)
    sil = Image.fromarray(np.maximum(np.asarray(sil), np.asarray(hat)))

    # ---- hem dissolving into smoke
    W, H = cv.w * SS, cv.h * SS
    n = noise(W, H, seed + 3, scales=(40, 20, 10, 5))
    yy, xx = np.mgrid[0:H, 0:W] / SS
    band = np.clip(1 - np.abs(yy - 722) / 26, 0, 1) * np.clip(1 - np.abs(xx - cx) / 175, 0, 1)
    a = np.clip((n - 0.38) * 2.2, 0, 1) * band
    cv.texture_fill(mask_from_array(a), n, (14, 14, 18), (70, 74, 88), alpha=0.9)

    # ---- cold rim light from the upper right (where the spirit glows)
    cv.rim(sil, RIM, -2.4, 1.6, 1.4, alpha=0.75, blur=0.8)
    return cv


def reaper_sit(seed=12):
    """Sitting on a low rock by the fire, elbows on knees, hat still on; faces right (toward the fire)."""
    cv = Canvas(420, 480)
    cx = 190
    sil = cv.blank()
    # rock
    rock = [(cx - 110, 400), (cx - 90, 352), (cx - 20, 338), (cx + 60, 346), (cx + 100, 380), (cx + 96, 420),
            (cx - 100, 424)]
    rm = cv.shape(rock)
    cv.gradient_fill(rm, (70, 66, 64), (28, 26, 28), 338, 424)
    cv.fill(cv.line([(cx - 60, 360), (cx - 20, 372), (cx + 40, 366)], 2), (100, 96, 92), alpha=0.5)
    # coat tail draped over the rock
    tail = [(cx - 70, 330), (cx + 20, 330), (cx + 40, 380), (cx + 20, 440), (cx - 40, 446), (cx - 96, 430),
            (cx - 92, 380)]
    tm = cv.shape(tail)
    sil = cv.shape(tail, mask=sil)
    cv.gradient_fill(tm, (26, 27, 34), COAT_DARK, 330, 446)
    # thighs going right, shins down
    thigh = [(cx - 30, 300), (cx + 90, 316), (cx + 110, 344), (cx + 96, 360), (cx - 30, 352)]
    shin = [(cx + 80, 330), (cx + 112, 330), (cx + 118, 440), (cx + 88, 444)]
    for p in (thigh, shin):
        m = cv.shape(p)
        sil = cv.shape(p, mask=sil)
        cv.gradient_fill(m, (24, 24, 30), (12, 12, 15), 300, 444)
    shoe = [(cx + 84, 436), (cx + 130, 436), (cx + 146, 448), (cx + 140, 458), (cx + 82, 458)]
    cv.fill(cv.shape(shoe), (8, 8, 10))
    sil = cv.shape(shoe, mask=sil)
    # torso leaning forward
    torso = [(cx - 64, 160), (cx + 20, 150), (cx + 62, 176), (cx + 70, 250), (cx + 50, 320), (cx - 40, 330),
             (cx - 76, 250)]
    tom = cv.shape(torso)
    sil = cv.shape(torso, mask=sil)
    cv.gradient_fill(tom, (34, 36, 46), COAT_DARK, 150, 330)
    cv.fill(cv.line([(cx - 10, 180), (cx + 4, 250), (cx + 6, 320)], 2.5), COAT_DARK, alpha=0.8, clip=tom)
    cv.fill(cv.shape([(cx - 70, 268), (cx + 66, 268), (cx + 62, 284), (cx - 72, 284)], smooth=False), (14, 14, 17),
            clip=tom)
    # collar
    collar = [(cx - 50, 170), (cx - 40, 124), (cx - 8, 148), (cx + 24, 140), (cx + 54, 120), (cx + 58, 172),
              (cx, 184)]
    clm = cv.shape(collar)
    sil = cv.shape(collar, mask=sil)
    cv.gradient_fill(clm, (44, 47, 58), (16, 17, 22), 120, 184)
    # arm resting on the knee, hand hanging
    arm = [(cx + 30, 170), (cx + 66, 190), (cx + 112, 290), (cx + 98, 312), (cx + 40, 230)]
    am = cv.shape(arm)
    sil = cv.shape(arm, mask=sil)
    cv.gradient_fill(am, (36, 38, 48), (14, 14, 18), 170, 312)
    hand = [(cx + 96, 300), (cx + 116, 296), (cx + 124, 326), (cx + 112, 340), (cx + 98, 330)]
    cv.fill(cv.shape(hand), (22, 22, 26))
    sil = cv.shape(hand, mask=sil)
    # face smoke + hat (tilted down a little)
    smoke_face(cv, cx + 12, 124, 36, 34, seed, upward=40)
    hat = fedora_small(cv, cx + 14, 22)
    wisps(cv, [((cx - 70, 104), (cx - 88, 70), (cx - 78, 30)), ((cx + 96, 100), (cx + 120, 66), (cx + 112, 24))],
          seed)
    sil = Image.fromarray(np.maximum(np.asarray(sil), np.asarray(hat)))
    # firelight from the right (warm) + cold rim from the spirit side (left)
    cv.rim(sil, (255, 170, 90), -2.6, 0.6, 1.6, alpha=0.7, blur=1.0)
    cv.rim(sil, RIM, 2.0, 1.4, 1.0, alpha=0.4, blur=0.8)
    return cv


def fedora_small(cv, cx, top):
    """The fedora scaled for the rest-site figure (drawn at 0.78 with the same proportions)."""
    sub = Canvas(cv.w, cv.h)
    m = fedora(sub, cx / 0.78, top / 0.78 + 4, tilt=6)
    # scale the sub canvas into the main one
    s = 0.78
    big = sub.img
    small = big.resize((int(big.size[0] * s), int(big.size[1] * s)), Image.LANCZOS)
    cv.img.alpha_composite(small, (0, 0))
    ms = m.resize(small.size, Image.LANCZOS)
    full = cv.blank()
    full.paste(ms, (0, 0))
    return full


# =====================================================================================================================
# the spirits (one function per figure; pose = "stand" | "sit")
# =====================================================================================================================

GHOST = (205, 232, 255)
GHOST_D = (60, 96, 150)
GHOST_L = (245, 252, 255)


class Ghost:
    """Draws a figure in pale ghost colours: base fills in GHOST, detail lines darker/lighter, then glow + mist."""

    def __init__(self, w, h):
        self.cv = Canvas(w, h)
        self.sil = self.cv.blank()

    def body(self, pts, tone=0.0, smooth=True):
        """A filled part; tone -1 (dark) .. +1 (light)."""
        m = self.cv.shape(pts, smooth=smooth)
        self.sil = self.cv.shape(pts, smooth=smooth, mask=self.sil)
        self.cv.fill(m, mix(tone))
        return m

    def ellipse(self, cx, cy, rx, ry, tone=0.0):
        m = self.cv.ellipse(cx, cy, rx, ry)
        self.sil = self.cv.ellipse(cx, cy, rx, ry, mask=self.sil)
        self.cv.fill(m, mix(tone))
        return m

    def stroke(self, pts, width, tone=-0.6, alpha=0.9, smooth=True, clip=None, solid=False):
        m = self.cv.line(pts, width, smooth=smooth)
        if solid:
            self.sil = self.cv.line(pts, width, smooth=smooth, mask=self.sil)
        self.cv.fill(m, mix(tone), alpha=alpha, clip=clip)
        return m

    def taper(self, pts, w0, w1, tone=0.0, alpha=1.0, solid=True):
        m = self.cv.tapered(pts, w0, w1)
        if solid:
            self.sil = self.cv.tapered(pts, w0, w1, mask=self.sil)
        self.cv.fill(m, mix(tone), alpha=alpha)
        return m

    def shade(self, pts, tone, alpha=0.5, blur=4, clip=None):
        self.cv.fill(self.cv.shape(pts), mix(tone), alpha=alpha, blur=blur, clip=clip)

    def finish(self, fade_from, fade_to, seed):
        """Glow, inner light and the mist the figure dissolves into below fade_from."""
        cv = self.cv
        # inner texture: drifting light inside the body
        W, H = cv.w * SS, cv.h * SS
        n = noise(W, H, seed, scales=(50, 25, 12))
        inner = mask_from_array(np.clip((n - 0.5) * 2.5, 0, 1))
        cv.fill(inner, GHOST_L, alpha=0.25, clip=self.sil)
        # edge light
        cv.rim(self.sil, GHOST_L, 2.0, 2.0, 2.0, alpha=0.6, blur=1.2)
        cv.rim(self.sil, GHOST_L, -2.0, 2.0, 2.0, alpha=0.4, blur=1.2)
        # a ghost is see-through in the middle and bright at the edges
        inner_m = np.asarray(self.sil.filter(ImageFilter.GaussianBlur(9 * SS)), np.float32) / 255
        edge = np.clip((1 - inner_m) * 2.5, 0, 1)
        a = np.asarray(cv.img.getchannel("A"), np.float32) * (0.62 + 0.38 * edge)
        cv.img.putalpha(Image.fromarray(a.astype(np.uint8)))
        vertical_fade(cv, fade_from, fade_to)
        # mist trailing below
        yy, xx = np.mgrid[0:H, 0:W] / SS
        n2 = noise(W, H, seed + 1, scales=(60, 30, 15, 8))
        cols = np.asarray(self.sil, np.float32).max(axis=0) / 255  # columns where the body is
        colf = np.asarray(Image.fromarray((cols * 255).astype(np.uint8)[None, :]).filter(
            ImageFilter.GaussianBlur(30 * SS)), np.float32)[0] / 255
        band = np.clip((yy - (fade_from - 40)) / 80, 0, 1) * np.clip((fade_to + 60 - yy) / 100, 0, 1)
        mist = np.clip((n2 - 0.4) * 2.2, 0, 1) * band * np.clip(colf * 1.6, 0, 1)[None, :]
        cv.fill(mask_from_array(mist), GHOST, alpha=0.55)
        # outer glow (soft halo around everything)
        a = cv.img.getchannel("A").filter(ImageFilter.GaussianBlur(14 * SS))
        glow = Image.new("RGBA", cv.img.size, GHOST + (0,))
        glow.putalpha(a.point(lambda v: int(v * 0.55)))
        glow.alpha_composite(cv.img)
        cv.img = glow
        return cv


def mix(tone):
    if tone >= 0:
        return tuple(int(GHOST[i] + (GHOST_L[i] - GHOST[i]) * tone) for i in range(3))
    t = -tone
    return tuple(int(GHOST[i] + (GHOST_D[i] - GHOST[i]) * t) for i in range(3))


def torso_base(g, cx, top, pose, shoulder=118, waist=96, robe=True):
    """Shoulders/chest/waist of a figure; returns the y of the waist."""
    wy = top + 250
    pts = [(cx - shoulder, top + 40), (cx - shoulder + 20, top + 10), (cx, top), (cx + shoulder - 20, top + 10),
           (cx + shoulder, top + 40), (cx + waist + 10, top + 160), (cx + waist, wy), (cx - waist, wy),
           (cx - waist - 10, top + 160)]
    g.body(pts, tone=-0.05)
    if pose == "stand":
        skirt = [(cx - waist, wy - 10), (cx + waist, wy - 10), (cx + waist + 40, wy + 260), (cx - waist - 40, wy + 260)]
    else:
        # seated: lap going right, knees, and the robe falling to the ground
        skirt = [(cx - waist, wy - 10), (cx + waist, wy - 10), (cx + waist + 120, wy + 30), (cx + waist + 130, wy + 80),
                 (cx + waist + 110, wy + 180), (cx - waist - 10, wy + 180)]
    if robe:
        g.body(skirt, tone=-0.12)
    return wy


def head(g, cx, cy, r=46, face_right=True, tone=0.15):
    """A head seen 3/4 toward the right; the face is a light, the features only shadows."""
    g.ellipse(cx, cy, r * 0.86, r, tone=tone)
    g.ellipse(cx + (r * 0.5 if face_right else -r * 0.5), cy + r * 0.25, r * 0.36, r * 0.42, tone=tone)  # jaw/cheek
    ex = cx + (r * 0.38 if face_right else -r * 0.38)
    g.stroke([(ex - 8, cy - 4), (ex + 8, cy - 6)], 3, tone=-0.7, alpha=0.8)
    g.stroke([(ex + 14, cy - 2), (ex + 20, cy + 14), (ex + 12, cy + 18)], 2.5, tone=-0.5, alpha=0.6)  # nose
    g.ellipse(cx, cy + r * 0.95, r * 0.42, r * 0.3, tone=-0.1)  # neck


# ---------------------------------------------------------------------------------------------------------- 이순신
def spirit_yisunsin(pose):
    """Joseon general: helmet with a tall spike and plume and neck flaps, studded armour coat, a long sword held
    point-down in front with both hands on the hilt."""
    g = Ghost(680, 1010)
    cx = 330
    top = 400 if pose == "stand" else 540
    wy = torso_base(g, cx, top, pose, shoulder=126, waist=100)
    # armour coat: studs in rows, and the long skirt split in front
    for row in range(9 if pose == "stand" else 6):
        y = top + 40 + row * 34
        for col in range(-3, 4):
            x = cx + col * 30 + (15 if row % 2 else 0)
            if abs(x - cx) < 110 + row * 3:
                g.cv.fill(g.cv.ellipse(x, y, 3.2, 3.2), mix(0.9), clip=g.sil)
                g.cv.fill(g.cv.ellipse(x + 1, y + 1, 3.2, 3.2), mix(-0.6), alpha=0.5, clip=g.sil)
    for x0 in (cx - 104, cx + 104):
        g.stroke([(x0, top + 30), (x0 + (6 if x0 > cx else -6), wy)], 3, tone=-0.6)
    g.stroke([(cx - 100, wy), (cx + 100, wy)], 8, tone=-0.4)  # belt
    if pose == "stand":
        g.stroke([(cx, wy), (cx + 2, wy + 250)], 3, tone=-0.6)
    # shoulder guards
    for sx in (-1, 1):
        g.body([(cx + sx * 80, top + 4), (cx + sx * 140, top + 20), (cx + sx * 150, top + 80), (cx + sx * 96, top + 70)],
               tone=0.05)
        for k in range(3):
            g.stroke([(cx + sx * (90 + k * 4), top + 30 + k * 18), (cx + sx * (144 - k * 2), top + 44 + k * 16)], 2,
                     tone=-0.5, smooth=False)
    # arms coming to the hilt at the centre
    hilt_y = wy - 30
    for sx in (-1, 1):
        g.body([(cx + sx * 140, top + 60), (cx + sx * 150, top + 130), (cx + sx * 50, hilt_y + 10),
                (cx + sx * 30, hilt_y - 22), (cx + sx * 104, top + 110)], tone=-0.02)
    # the long sword, point down to the ground, blade in front of the body
    blade_top = hilt_y + 30
    blade_bot = blade_top + (300 if pose == "stand" else 150)
    g.body([(cx - 9, blade_top), (cx + 9, blade_top), (cx + 7, blade_bot - 18), (cx, blade_bot),
            (cx - 7, blade_bot - 18)], tone=0.85, smooth=False)
    g.stroke([(cx, blade_top + 6), (cx, blade_bot - 22)], 1.6, tone=-0.3, smooth=False)
    g.body([(cx - 30, blade_top - 4), (cx + 30, blade_top - 4), (cx + 26, blade_top + 8), (cx - 26, blade_top + 8)],
           tone=0.3)  # guard
    g.body([(cx - 8, hilt_y - 70), (cx + 8, hilt_y - 70), (cx + 8, blade_top - 4), (cx - 8, blade_top - 4)], tone=-0.3,
           smooth=False)  # grip
    for k in range(5):
        g.stroke([(cx - 8, hilt_y - 62 + k * 14), (cx + 8, hilt_y - 56 + k * 14)], 2, tone=0.5, smooth=False)
    g.ellipse(cx, hilt_y - 74, 10, 8, tone=0.4)  # pommel
    for sx in (-1, 1):  # hands on the grip
        g.ellipse(cx + sx * 4, hilt_y - 30 + sx * 22, 20, 16, tone=0.1)
    # head and helmet
    hy = top - 66
    head(g, cx + 6, hy, 44)
    g.stroke([(cx + 18, hy + 28), (cx + 40, hy + 36)], 2, tone=-0.6)  # moustache line
    g.taper([(cx + 30, hy + 38), (cx + 34, hy + 62)], 7, 2, tone=-0.3)  # small beard
    # neck flaps (드림) falling to the shoulders
    g.body([(cx - 58, hy - 10), (cx - 62, hy + 60), (cx - 30, hy + 74), (cx - 30, hy + 10)], tone=-0.15)
    g.body([(cx + 58, hy - 10), (cx + 70, hy + 56), (cx + 52, hy + 70), (cx + 40, hy + 10)], tone=-0.15)
    for k in range(3):
        g.stroke([(cx - 60, hy + 14 + k * 18), (cx - 32, hy + 18 + k * 18)], 1.6, tone=-0.6, smooth=False)
    # bowl
    g.body([(cx - 56, hy - 6), (cx - 50, hy - 46), (cx - 20, hy - 66), (cx + 26, hy - 66), (cx + 54, hy - 44),
            (cx + 60, hy - 6)], tone=0.25)
    g.stroke([(cx - 58, hy - 8), (cx + 62, hy - 8)], 7, tone=-0.3)  # brow band
    for k in range(-2, 3):
        g.stroke([(cx + k * 20, hy - 12), (cx + k * 14, hy - 60)], 1.6, tone=-0.4)
    # spike + plume (상모)
    g.body([(cx - 6, hy - 66), (cx + 6, hy - 66), (cx + 2, hy - 120), (cx, hy - 126), (cx - 2, hy - 120)], tone=0.6,
           smooth=False)
    g.ellipse(cx, hy - 70, 12, 6, tone=0.4)
    g.taper([(cx, hy - 118), (cx - 18, hy - 140), (cx - 44, hy - 148), (cx - 70, hy - 140)], 18, 4, tone=0.3)
    g.taper([(cx, hy - 116), (cx - 24, hy - 128), (cx - 54, hy - 126)], 12, 3, tone=0.0)
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=41)


# ---------------------------------------------------------------------------------------------------------- 관우
def spirit_guanyu(pose):
    """Long flowing beard, a soft cap, a robe over armour, and the crescent-bladed polearm held upright at his side."""
    g = Ghost(680, 1010)
    cx = 310
    top = 400 if pose == "stand" else 540
    wy = torso_base(g, cx, top, pose, shoulder=130, waist=104)
    # robe lapels crossing (left over right), armour scales showing at the chest
    g.stroke([(cx - 70, top + 4), (cx + 20, top + 120), (cx + 60, wy)], 10, tone=-0.35)
    g.stroke([(cx + 70, top + 4), (cx + 10, top + 90)], 6, tone=-0.35)
    for row in range(4):
        for col in range(-2, 3):
            x = cx - 20 + col * 22 + (11 if row % 2 else 0)
            y = top + 70 + row * 18
            g.stroke([(x - 10, y), (x, y + 8), (x + 10, y)], 1.6, tone=-0.45, smooth=False, clip=g.sil)
    g.stroke([(cx - 104, wy), (cx + 104, wy)], 10, tone=-0.4)
    if pose == "stand":
        for k in (-60, -20, 30, 70):
            g.stroke([(cx + k, wy + 20), (cx + k * 1.25, wy + 250)], 3, tone=-0.45)
    # polearm on his right (screen right), held by the right hand at chest height
    px = cx + 150
    pole_top = top - 210
    pole_bot = top + (560 if pose == "stand" else 380)
    g.body([(px - 5, pole_top + 40), (px + 5, pole_top + 40), (px + 5, pole_bot), (px - 5, pole_bot)], tone=-0.1,
           smooth=False)
    # crescent blade: a broad curved blade facing outward, with a notch and a hooked back
    blade = [(px - 4, pole_top + 60), (px - 8, pole_top - 30), (px - 10, pole_top - 110), (px - 2, pole_top - 150),
             (px + 30, pole_top - 120), (px + 66, pole_top - 60), (px + 78, pole_top - 4), (px + 60, pole_top + 40),
             (px + 26, pole_top + 62), (px + 8, pole_top + 66)]
    g.body(blade, tone=0.75)
    g.taper([(px + 2, pole_top - 20), (px + 8, pole_top - 30), (px - 18, pole_top - 46)], 10, 3, tone=0.6)  # back spike
    g.stroke([(px + 2, pole_top - 20), (px + 40, pole_top - 40), (px + 30, pole_top + 30)], 2, tone=-0.3)
    # dragon-head ring and tassel below the blade
    g.ellipse(px, pole_top + 66, 12, 10, tone=0.2)
    g.taper([(px - 6, pole_top + 72), (px - 22, pole_top + 110), (px - 16, pole_top + 150)], 10, 3, tone=0.1)
    # arms
    g.body([(cx + 110, top + 30), (cx + 150, top + 70), (px + 10, top + 130), (px - 6, top + 160), (cx + 100, top + 140)],
           tone=-0.05)
    g.ellipse(px, top + 130, 18, 22, tone=0.1)  # right hand on the pole
    g.body([(cx - 120, top + 30), (cx - 150, top + 90), (cx - 120, wy - 20), (cx - 90, wy - 30), (cx - 100, top + 100)],
           tone=-0.08)
    g.ellipse(cx - 104, wy - 24, 16, 16, tone=0.1)
    # head: cap with a knot and long ribbons, a very long beard
    hy = top - 70
    head(g, cx + 6, hy, 46)
    g.stroke([(cx + 18, hy - 12), (cx + 40, hy - 18)], 3.5, tone=-0.7)  # strong brow
    beard = [(cx - 14, hy + 20), (cx + 40, hy + 22), (cx + 48, hy + 90), (cx + 40, hy + 170), (cx + 24, hy + 230),
             (cx + 12, hy + 190), (cx - 6, hy + 120)]
    g.body(beard, tone=0.4)
    for k in range(5):
        g.stroke([(cx + k * 9 - 4, hy + 34), (cx + k * 8, hy + 120), (cx + 14 + k * 3, hy + 200)], 1.4, tone=-0.3,
                 alpha=0.6)
    g.taper([(cx + 30, hy + 26), (cx + 58, hy + 40), (cx + 66, hy + 70)], 6, 2, tone=0.2)  # moustache
    cap = [(cx - 50, hy - 10), (cx - 46, hy - 46), (cx - 10, hy - 66), (cx + 30, hy - 62), (cx + 54, hy - 36),
           (cx + 54, hy - 10)]
    g.body(cap, tone=-0.1)
    g.ellipse(cx - 4, hy - 70, 18, 12, tone=0.0)
    for k, a in ((0, 0), (1, 14)):
        g.taper([(cx - 44, hy - 14), (cx - 80 - a, hy + 20), (cx - 96 - a, hy + 80)], 12, 6, tone=-0.05)
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=42)


# ---------------------------------------------------------------------------------------------------------- 잔 다르크
def spirit_joan(pose):
    """Short-haired girl in plate armour, a banner on a long staff in her left hand, sword at the hip."""
    g = Ghost(680, 1010)
    cx = 350
    top = 420 if pose == "stand" else 550
    wy = torso_base(g, cx, top, pose, shoulder=108, waist=82, robe=False)
    # breastplate ridge and plates
    g.stroke([(cx + 6, top + 10), (cx + 4, wy)], 2.5, tone=0.7)
    for k in range(4):
        y = top + 140 + k * 26
        g.stroke([(cx - 84, y), (cx, y + 10), (cx + 84, y)], 2, tone=-0.5)
    # tassets / skirt of plates (standing) or thighs (sitting)
    if pose == "stand":
        g.body([(cx - 84, wy - 6), (cx + 84, wy - 6), (cx + 100, wy + 90), (cx - 100, wy + 90)], tone=-0.05)
        for k in range(3):
            g.stroke([(cx - 92 - k * 4, wy + 20 + k * 26), (cx + 92 + k * 4, wy + 20 + k * 26)], 2, tone=-0.5)
        for lx in (-44, 44):
            g.body([(cx + lx - 30, wy + 80), (cx + lx + 30, wy + 80), (cx + lx + 26, wy + 300),
                    (cx + lx - 26, wy + 300)], tone=0.05)
            g.ellipse(cx + lx, wy + 160, 26, 18, tone=0.4)  # knee cops
    else:
        g.body([(cx - 84, wy - 6), (cx + 84, wy - 6), (cx + 190, wy + 20), (cx + 200, wy + 80), (cx + 170, wy + 180),
                (cx - 90, wy + 180)], tone=-0.05)
        g.ellipse(cx + 180, wy + 40, 30, 22, tone=0.4)
    # pauldrons
    for sx in (-1, 1):
        g.body([(cx + sx * 70, top), (cx + sx * 130, top + 14), (cx + sx * 140, top + 70), (cx + sx * 90, top + 64)],
               tone=0.25)
        for k in range(3):
            g.stroke([(cx + sx * (82 + k * 4), top + 24 + k * 16), (cx + sx * (136 - k * 2), top + 32 + k * 16)], 1.8,
                     tone=-0.5, smooth=False)
    # banner staff in her left hand (screen left), raised high
    sx0 = cx - 150
    staff_top = top - 300
    staff_bot = top + (520 if pose == "stand" else 360)
    g.body([(sx0 - 5, staff_top), (sx0 + 5, staff_top), (sx0 + 5, staff_bot), (sx0 - 5, staff_bot)], tone=-0.1,
           smooth=False)
    g.body([(sx0 - 8, staff_top - 26), (sx0 + 8, staff_top - 26), (sx0, staff_top - 50)], tone=0.7, smooth=False)
    # the banner, swallow-tailed, blowing to the left
    banner = [(sx0, staff_top + 6), (sx0 - 60, staff_top + 2), (sx0 - 120, staff_top + 14), (sx0 - 176, staff_top + 4),
              (sx0 - 140, staff_top + 60), (sx0 - 180, staff_top + 120), (sx0 - 116, staff_top + 112),
              (sx0 - 60, staff_top + 130), (sx0, staff_top + 120)]
    bm = g.body(banner, tone=0.2)
    g.shade([(sx0 - 70, staff_top + 10), (sx0 - 100, staff_top + 120), (sx0 - 120, staff_top + 120),
             (sx0 - 96, staff_top + 10)], -0.5, alpha=0.4, blur=6, clip=bm)
    # a fleur on the banner
    fx, fy = sx0 - 80, staff_top + 64
    g.taper([(fx, fy + 22), (fx, fy - 18)], 8, 3, tone=0.8, solid=False)
    g.taper([(fx, fy + 6), (fx - 16, fy - 6), (fx - 20, fy + 12)], 6, 2, tone=0.8, solid=False)
    g.taper([(fx, fy + 6), (fx + 16, fy - 6), (fx + 20, fy + 12)], 6, 2, tone=0.8, solid=False)
    g.stroke([(fx - 14, fy + 14), (fx + 14, fy + 14)], 3, tone=0.8)
    # arms: left raised to the staff, right resting on the sword hilt at the hip
    g.body([(cx - 100, top + 20), (cx - 136, top + 50), (sx0 + 10, top - 40), (sx0 - 6, top - 60), (cx - 126, top + 4)],
           tone=0.0)
    g.ellipse(sx0, top - 52, 16, 18, tone=0.2)
    g.body([(cx + 100, top + 30), (cx + 140, top + 70), (cx + 122, wy - 10), (cx + 98, wy - 16), (cx + 104, top + 90)],
           tone=0.0)
    g.ellipse(cx + 110, wy - 8, 16, 15, tone=0.2)
    g.body([(cx + 104, wy + 4), (cx + 118, wy + 4), (cx + 150, wy + 200 if pose == "stand" else wy + 120),
            (cx + 138, wy + 204 if pose == "stand" else wy + 124)], tone=0.5, smooth=False)  # scabbard
    g.stroke([(cx + 92, wy - 2), (cx + 132, wy + 8)], 5, tone=0.3)  # crossguard
    # head: short bob, calm face looking up-right
    hy = top - 60
    head(g, cx + 4, hy, 40, tone=0.25)
    hair = [(cx - 44, hy + 30), (cx - 50, hy - 20), (cx - 30, hy - 48), (cx + 10, hy - 54), (cx + 40, hy - 40),
            (cx + 48, hy - 14), (cx + 30, hy - 22), (cx + 8, hy - 10), (cx - 10, hy + 20), (cx - 20, hy + 40)]
    g.body(hair, tone=-0.1)
    for k in range(4):
        g.stroke([(cx - 30 + k * 16, hy - 46), (cx - 36 + k * 12, hy + 10 - k * 6)], 1.4, tone=-0.5, alpha=0.6)
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=43)


# ---------------------------------------------------------------------------------------------------------- 레오니다스
def spirit_leonidas(pose):
    """Corinthian helmet with a transverse-free (front-to-back) crest, bare muscled arms, a round shield with a
    lambda on his left arm, a spear in his right hand, a cloak behind."""
    g = Ghost(720, 1010)
    cx = 360
    top = 400 if pose == "stand" else 540
    # cloak behind (darker)
    cloak = [(cx - 140, top + 20), (cx + 140, top + 20), (cx + 190, top + (500 if pose == "stand" else 320)),
             (cx - 190, top + (500 if pose == "stand" else 320))]
    g.body(cloak, tone=-0.45)
    wy = torso_base(g, cx, top, pose, shoulder=132, waist=96, robe=False)
    # muscled cuirass lines
    g.stroke([(cx - 70, top + 70), (cx - 10, top + 90), (cx, top + 80)], 2.5, tone=-0.5)
    g.stroke([(cx + 70, top + 70), (cx + 10, top + 90), (cx, top + 80)], 2.5, tone=-0.5)
    g.stroke([(cx, top + 90), (cx, wy - 20)], 2, tone=-0.5)
    for k in range(3):
        g.stroke([(cx - 40, top + 130 + k * 30), (cx + 40, top + 130 + k * 30)], 1.6, tone=-0.4, alpha=0.6)
    # pteruges (leather strips)
    if pose == "stand":
        for k in range(-4, 5):
            g.body([(cx + k * 22 - 10, wy - 4), (cx + k * 22 + 10, wy - 4), (cx + k * 23 + 9, wy + 80),
                    (cx + k * 23 - 9, wy + 80)], tone=-0.15 if k % 2 else 0.0, smooth=False)
        for lx in (-40, 40):
            g.body([(cx + lx - 28, wy + 70), (cx + lx + 28, wy + 70), (cx + lx + 22, wy + 300), (cx + lx - 22, wy + 300)],
                   tone=0.05)
            g.body([(cx + lx - 26, wy + 170), (cx + lx + 26, wy + 170), (cx + lx + 22, wy + 290),
                    (cx + lx - 22, wy + 290)], tone=0.3)  # greaves
    else:
        g.body([(cx - 96, wy - 6), (cx + 96, wy - 6), (cx + 200, wy + 20), (cx + 210, wy + 80), (cx + 180, wy + 180),
                (cx - 100, wy + 180)], tone=-0.05)
    # spear in the right hand (screen right), point up
    px = cx + 160
    g.body([(px - 4, top - 260), (px + 4, top - 260), (px + 4, top + (560 if pose == "stand" else 380)),
            (px - 4, top + (560 if pose == "stand" else 380))], tone=-0.1, smooth=False)
    g.body([(px - 12, top - 260), (px + 12, top - 260), (px, top - 330)], tone=0.85, smooth=False)
    g.body([(cx + 110, top + 20), (cx + 150, top + 60), (px + 12, top + 80), (px - 4, top + 110), (cx + 100, top + 120)],
           tone=0.1)
    g.ellipse(px, top + 90, 18, 20, tone=0.2)
    # round shield on the left arm, seen slightly from the side, with a lambda
    sxc, syc = cx - 120, top + 150
    g.ellipse(sxc, syc, 118, 132, tone=0.05)
    g.cv.fill(g.cv.ellipse(sxc, syc, 100, 114), mix(0.25))
    g.stroke([(sxc - 44, syc + 60), (sxc, syc - 56), (sxc + 44, syc + 60)], 14, tone=-0.55, smooth=False)
    g.stroke([(sxc - 116, syc), (sxc - 100, syc - 60)], 2, tone=0.9, alpha=0.6)
    # head: Corinthian helmet (cheek guards, eye slits, nasal), crest front to back
    hy = top - 66
    g.ellipse(cx + 4, hy + 50, 22, 18, tone=-0.1)  # neck
    helm = [(cx - 50, hy + 40), (cx - 54, hy - 20), (cx - 30, hy - 58), (cx + 20, hy - 62), (cx + 52, hy - 30),
            (cx + 56, hy + 10), (cx + 44, hy + 50), (cx + 20, hy + 54), (cx + 14, hy + 20), (cx - 10, hy + 54)]
    g.body(helm, tone=0.3)
    g.stroke([(cx - 20, hy - 6), (cx + 6, hy - 2)], 6, tone=-0.85, smooth=False)  # eye slits
    g.stroke([(cx + 22, hy - 4), (cx + 46, hy - 8)], 6, tone=-0.85, smooth=False)
    g.stroke([(cx + 14, hy - 2), (cx + 14, hy + 30)], 6, tone=0.45, smooth=False)  # nasal
    g.stroke([(cx + 2, hy + 24), (cx + 14, hy + 54)], 3, tone=-0.8)  # gap between cheek guards
    # crest: a long brush from the forehead over the top to the back
    for k in range(10):
        a = k / 9
        x0 = cx + 40 - a * 110
        y0 = hy - 58 - math.sin(a * math.pi) * 18
        g.taper([(x0, y0), (x0 - 6, y0 - 40 - math.sin(a * math.pi) * 30), (x0 - 18, y0 - 54)], 18, 6,
                tone=0.15 - (k % 2) * 0.25)
    g.taper([(cx - 70, hy - 70), (cx - 110, hy - 40), (cx - 130, hy + 20), (cx - 126, hy + 80)], 24, 6, tone=-0.1)
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=44)


# ---------------------------------------------------------------------------------------------------------- 칭기즈 칸
def spirit_genghis(pose):
    """Fur-rimmed hat with a pointed top, a braided beard, a wrap-over robe (deel) with a sash, a composite bow."""
    g = Ghost(680, 1010)
    cx = 320
    top = 400 if pose == "stand" else 540
    wy = torso_base(g, cx, top, pose, shoulder=132, waist=110)
    g.stroke([(cx - 60, top + 4), (cx + 50, top + 130), (cx + 70, wy)], 12, tone=-0.35)  # robe overlap
    g.stroke([(cx - 60, top + 4), (cx + 50, top + 130), (cx + 70, wy)], 3, tone=0.6)
    g.stroke([(cx - 110, wy - 4), (cx + 110, wy - 4)], 18, tone=-0.3)  # sash
    g.taper([(cx - 40, wy + 6), (cx - 50, wy + 70), (cx - 44, wy + 120)], 14, 8, tone=-0.3)
    # fur trim at cuffs and hem
    if pose == "stand":
        g.stroke([(cx - 150, wy + 250), (cx, wy + 262), (cx + 150, wy + 250)], 16, tone=0.4)
    # bow in the left hand (screen left), held low and diagonal, string toward him
    bx, by = cx - 150, top + 160
    bow = [(bx - 40, by - 200), (bx - 70, by - 120), (bx - 54, by - 30), (bx - 30, by), (bx - 54, by + 40),
           (bx - 70, by + 120), (bx - 34, by + 210)]
    g.taper(bow, 10, 10, tone=0.3)
    g.taper([(bx - 34, by + 210), (bx - 16, by + 230)], 8, 4, tone=0.3)
    g.taper([(bx - 40, by - 200), (bx - 20, by - 222)], 8, 4, tone=0.3)
    g.stroke([(bx - 36, by - 198), (bx - 30, by + 208)], 1.4, tone=0.9, smooth=False)
    g.body([(cx - 110, top + 30), (cx - 150, top + 70), (bx - 20, by - 10), (bx - 30, by + 16), (cx - 110, top + 130)],
           tone=0.0)
    g.ellipse(bx - 30, by + 2, 18, 20, tone=0.15)
    # right hand on the sash, a quiver's arrows over the right shoulder
    g.body([(cx + 110, top + 30), (cx + 150, top + 80), (cx + 100, wy - 8), (cx + 80, wy - 22), (cx + 100, top + 110)],
           tone=-0.02)
    g.ellipse(cx + 86, wy - 12, 18, 16, tone=0.15)
    for k in range(4):
        g.taper([(cx + 70 + k * 10, top + 40), (cx + 100 + k * 12, top - 50)], 4, 3, tone=0.2)
        g.taper([(cx + 98 + k * 12, top - 46), (cx + 104 + k * 12, top - 70)], 10, 2, tone=0.5)
    # head
    hy = top - 66
    head(g, cx + 6, hy, 46)
    g.stroke([(cx + 16, hy - 10), (cx + 42, hy - 6)], 3, tone=-0.7)
    g.taper([(cx + 30, hy + 24), (cx + 56, hy + 34), (cx + 62, hy + 60)], 7, 2, tone=-0.1)  # moustache
    g.taper([(cx + 22, hy + 36), (cx + 28, hy + 80), (cx + 22, hy + 120)], 18, 6, tone=-0.1)  # beard
    # hat: fur brim + a tall pointed crown + a red-tasselled top (in ghost tones)
    g.body([(cx - 52, hy - 30), (cx - 30, hy - 120), (cx + 4, hy - 150), (cx + 40, hy - 116), (cx + 56, hy - 30)],
           tone=0.05)
    for k in range(-2, 3):
        g.stroke([(cx + k * 14, hy - 40), (cx + 2 + k * 8, hy - 130)], 1.6, tone=-0.4, alpha=0.6)
    g.ellipse(cx + 2, hy - 30, 66, 22, tone=0.35)  # fur brim
    W, H = g.cv.w * SS, g.cv.h * SS
    fur = noise(W, H, 7, scales=(8, 4))
    brim = g.cv.ellipse(cx + 2, hy - 30, 66, 22)
    g.cv.texture_fill(brim, fur, mix(-0.2), mix(0.8), alpha=0.6)
    g.taper([(cx + 4, hy - 150), (cx + 10, hy - 176)], 10, 4, tone=0.5)
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=45)


# ---------------------------------------------------------------------------------------------------------- 나폴레옹
def spirit_napoleon(pose):
    """Bicorne worn side-on, a long coat with epaulettes and a high collar, waistcoat, the right hand tucked into the
    waistcoat, the left hand behind."""
    g = Ghost(680, 1010)
    cx = 330
    top = 410 if pose == "stand" else 545
    wy = torso_base(g, cx, top, pose, shoulder=112, waist=92)
    # waistcoat and coat edges
    g.body([(cx - 40, top + 10), (cx + 40, top + 10), (cx + 50, wy + 10), (cx - 50, wy + 10)], tone=0.35)
    for by in range(top + 40, wy, 26):
        g.cv.fill(g.cv.ellipse(cx, by, 3.2, 3.2), mix(-0.6))
    g.stroke([(cx - 40, top + 10), (cx - 60, wy + 6), (cx - 110, wy + 250 if pose == "stand" else wy + 180)], 3,
             tone=-0.6)
    g.stroke([(cx + 40, top + 10), (cx + 60, wy + 6), (cx + 110, wy + 250 if pose == "stand" else wy + 180)], 3,
             tone=-0.6)
    g.stroke([(cx - 50, wy + 4), (cx + 50, wy + 4)], 6, tone=-0.3)
    if pose == "stand":
        for lx in (-36, 36):
            g.body([(cx + lx - 24, wy + 100), (cx + lx + 24, wy + 100), (cx + lx + 20, wy + 300),
                    (cx + lx - 20, wy + 300)], tone=0.4)
            g.body([(cx + lx - 24, wy + 200), (cx + lx + 24, wy + 200), (cx + lx + 22, wy + 300),
                    (cx + lx - 22, wy + 300)], tone=-0.4)  # boots
    # epaulettes with fringe
    for sx in (-1, 1):
        g.ellipse(cx + sx * 104, top + 18, 40, 14, tone=0.55)
        for k in range(7):
            x = cx + sx * (74 + k * 9)
            g.stroke([(x, top + 28), (x + sx * 2, top + 54)], 2.4, tone=0.55, solid=True)
    # right arm folded, hand into the waistcoat (screen right shoulder, hand at the centre)
    g.body([(cx + 100, top + 30), (cx + 132, top + 70), (cx + 120, top + 150), (cx + 30, top + 140), (cx + 30, top + 110),
            (cx + 96, top + 110)], tone=-0.02)
    g.stroke([(cx + 30, top + 108), (cx + 30, top + 144)], 6, tone=-0.6)  # where the hand disappears
    # left arm behind the back
    g.body([(cx - 100, top + 30), (cx - 128, top + 80), (cx - 112, wy - 10), (cx - 90, wy - 20), (cx - 96, top + 110)],
           tone=-0.15)
    # head: short hair, high collar, the bicorne side-on
    hy = top - 58
    g.body([(cx - 40, top + 10), (cx - 36, hy + 40), (cx + 40, hy + 40), (cx + 44, top + 10)], tone=0.0)  # collar
    head(g, cx + 6, hy, 42, tone=0.25)
    g.taper([(cx - 30, hy - 30), (cx - 40, hy + 6), (cx - 30, hy + 26)], 14, 6, tone=-0.2)  # hair at the temple
    hat = [(cx - 120, hy - 22), (cx - 70, hy - 46), (cx - 20, hy - 96), (cx + 20, hy - 100), (cx + 70, hy - 50),
           (cx + 124, hy - 24), (cx + 60, hy - 30), (cx, hy - 38), (cx - 60, hy - 30)]
    g.body(hat, tone=-0.25)
    g.stroke([(cx - 116, hy - 24), (cx, hy - 40), (cx + 120, hy - 26)], 3, tone=0.6)
    g.ellipse(cx + 30, hy - 66, 14, 14, tone=0.6)  # cockade
    g.cv.fill(g.cv.ellipse(cx + 30, hy - 66, 7, 7), mix(-0.3))
    fade = (top + 300, top + 580) if pose == "stand" else (top + 250, top + 330)
    return g.finish(*fade, seed=46)


SPIRITS = {
    "guanyu": spirit_guanyu,
    "joan": spirit_joan,
    "leonidas": spirit_leonidas,
    "genghis": spirit_genghis,
    "yisunsin": spirit_yisunsin,
    "napoleon": spirit_napoleon,
}


# =====================================================================================================================
# character select background
# =====================================================================================================================

def char_select_bg():
    """A dark screening hall: a pale screen high on the left, light falling through dust and smoke, rows of empty
    seats. The right side is left empty for the reaper sprite (MumyeongCode/Visuals/RuntimeScenes.cs)."""
    W, H = 1920, 1080
    cv = Canvas(W, H)
    full = cv.blank()
    full.paste(255, [0, 0, W * SS, H * SS])
    cv.gradient_fill(full, (16, 18, 26), (6, 6, 9), 0, H)
    # screen
    scr = [(260, 140), (1000, 120), (1000, 560), (260, 580)]
    sm = cv.shape(scr, smooth=False)
    cv.fill(sm, (120, 150, 175), blur=30, alpha=0.6)
    cv.gradient_fill(sm, (200, 220, 235), (120, 140, 160), 120, 580)
    n = noise(W * SS, H * SS, 3, scales=(240, 120, 60))
    cv.texture_fill(sm, n, (150, 170, 190), (230, 240, 248), alpha=0.5)
    # beam from the projector behind the viewer, hitting the screen, with dust
    beam = [(1500, 60), (1560, 40), (1000, 560), (260, 580)]
    bm = cv.shape(beam, smooth=False)
    cv.fill(bm, (170, 200, 225), alpha=0.10, blur=24)
    yy, xx = np.mgrid[0:H * SS, 0:W * SS]
    rng = np.random.default_rng(5)
    dust = np.zeros((H * SS, W * SS), np.float32)
    for _ in range(900):
        x, y = rng.integers(0, W * SS), rng.integers(0, H * SS)
        dust[y, x] = rng.random()
    dm = mask_from_array(dust).filter(ImageFilter.MaxFilter(3))
    cv.fill(dm, (220, 235, 245), alpha=0.8, clip=bm)
    # seats: rows of dark backs
    for row, (y, s) in enumerate([(780, 1.0), (880, 1.15), (990, 1.3)]):
        x = -40 + row * 30
        while x < W - 500:
            seat = [(x, y), (x + 60 * s, y - 10), (x + 120 * s, y), (x + 116 * s, y + 120), (x + 4, y + 120)]
            m = cv.shape(seat)
            cv.gradient_fill(m, (28, 30, 40), (8, 8, 11), y - 10, y + 120)
            cv.fill(cv.line([(x + 6, y), (x + 60 * s, y - 9), (x + 114 * s, y)], 2), (80, 92, 110), alpha=0.5)
            x += 140 * s
    # smoke drifting low
    n2 = noise(W * SS, H * SS, 9, scales=(300, 150, 75, 36))
    fog = np.clip((n2 - 0.45) * 2.0, 0, 1) * np.clip((yy / SS - 600) / 400, 0, 1)
    cv.fill(mask_from_array(fog), (110, 125, 150), alpha=0.35)
    img = cv.result().convert("RGB")
    # the reaper and the spirits are separate sprites in mumyeong_char_select_bg.tscn (spirits fade in behind him)
    # vignette
    v = Image.new("L", (W, H), 0)
    vn = np.clip(1 - (((np.mgrid[0:H, 0:W][1] - W / 2) / (W * 0.62)) ** 2 +
                      ((np.mgrid[0:H, 0:W][0] - H / 2) / (H * 0.62)) ** 2), 0, 1)
    v = Image.fromarray((vn * 255).astype(np.uint8))
    black = Image.new("RGB", (W, H), (0, 0, 0))
    img = Image.composite(img, black, v.point(lambda p: int(110 + p * 145 / 255)))
    return img


# =====================================================================================================================

def main():
    preview = None
    if "--preview" in sys.argv:
        preview = sys.argv[sys.argv.index("--preview") + 1]
        os.makedirs(preview, exist_ok=True)
    only = [a for a in sys.argv[1:] if not a.startswith("--") and a != preview]
    os.makedirs(OUT, exist_ok=True)

    def save(img, name):
        path = os.path.join(OUT, name)
        if name.endswith(".jpg"):
            img.save(path, quality=90)
        else:
            img.save(path, optimize=True)
        print("wrote", path, img.size)
        return img

    want = lambda k: not only or k in only  # noqa: E731
    outs = {}
    if want("reaper"):
        outs["reaper"] = save(reaper_stand().result(), "reaper.png")
        outs["reaper_rest"] = save(reaper_sit().result(), "reaper_rest.png")
    for key, fn in SPIRITS.items():
        if want(key):
            outs[key] = save(fn("stand").result(), f"spirit_{key}.png")
            outs[key + "_rest"] = save(fn("sit").result(), f"spirit_{key}_rest.png")
    if want("bg"):
        save(char_select_bg(), "char_select_bg.png")
    if preview:
        bg = (40, 34, 38)
        for key in SPIRITS:
            if key not in outs:
                continue
            sp = outs[key]
            re = outs.get("reaper") or Image.open(os.path.join(OUT, "reaper.png"))
            w = max(sp.size[0], re.size[0]) + 100
            h = max(sp.size[1], re.size[1]) + 40
            board = Image.new("RGBA", (w * 2, h), bg + (255,))
            board.alpha_composite(sp, (0, 0))
            board.alpha_composite(re, (sp.size[0] // 2 + 20, h - re.size[1] - 10))
            rs = outs.get(key + "_rest")
            rr = outs.get("reaper_rest") or Image.open(os.path.join(OUT, "reaper_rest.png"))
            board.alpha_composite(rs.resize((rs.size[0] * 2 // 3, rs.size[1] * 2 // 3)), (w, h - rs.size[1] * 2 // 3))
            board.alpha_composite(rr, (w + 300, h - rr.size[1] - 20))
            board.convert("RGB").save(os.path.join(preview, f"preview_{key}.png"))


if __name__ == "__main__":
    main()
