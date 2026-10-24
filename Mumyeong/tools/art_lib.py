"""Small drawing helpers for gen_character.py (procedural art, no external services).

Everything is drawn on a supersampled RGBA canvas (SS x the design size) and downsampled with LANCZOS at the end.
Shapes are given in design units; smooth shapes go through a closed Catmull-Rom spline.
"""
import math

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

SS = 3


def catmull(pts, closed=True, steps=10):
    """Closed (or open) Catmull-Rom spline through pts."""
    n = len(pts)
    out = []
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = pts[(i - 1) % n] if closed or i > 0 else pts[0]
        p1 = pts[i]
        p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if closed or i + 2 < n else pts[-1]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 +
                       (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 +
                       (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            out.append((x, y))
    if not closed:
        out.append(pts[-1])
    return out


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))

    # ---------------------------------------------------------------- masks
    def blank(self):
        return Image.new("L", (self.w * SS, self.h * SS), 0)

    def _pts(self, pts):
        return [(x * SS, y * SS) for x, y in pts]

    def shape(self, pts, smooth=True, mask=None, value=255):
        """Mask of a closed shape (smooth = spline through the points, else a polygon)."""
        m = mask if mask is not None else self.blank()
        p = catmull(pts) if smooth else pts
        ImageDraw.Draw(m).polygon(self._pts(p), fill=value)
        return m

    def ellipse(self, cx, cy, rx, ry, mask=None, value=255):
        m = mask if mask is not None else self.blank()
        ImageDraw.Draw(m).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=value)
        return m

    def line(self, pts, width, smooth=True, mask=None, value=255):
        m = mask if mask is not None else self.blank()
        p = catmull(pts, closed=False) if smooth and len(pts) > 2 else pts
        d = ImageDraw.Draw(m)
        d.line(self._pts(p), fill=value, width=max(1, int(width * SS)), joint="curve")
        r = width * SS / 2
        for x, y in (p[0], p[-1]):
            d.ellipse([x * SS - r, y * SS - r, x * SS + r, y * SS + r], fill=value)
        return m

    def tapered(self, pts, w0, w1, mask=None, value=255):
        """A stroke whose width goes from w0 to w1 along the (smoothed) path — hair, wisps, plumes."""
        m = mask if mask is not None else self.blank()
        p = catmull(pts, closed=False) if len(pts) > 2 else list(pts)
        # resample so the stamped circles overlap (no beads on long segments)
        step = max(0.5, min(w0, w1) / 4)
        q = [p[0]]
        for (x0, y0), (x1, y1) in zip(p, p[1:]):
            k = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
            q += [(x0 + (x1 - x0) * i / k, y0 + (y1 - y0) * i / k) for i in range(1, k + 1)]
        p = q
        d = ImageDraw.Draw(m)
        n = len(p)
        for i, (x, y) in enumerate(p):
            r = (w0 + (w1 - w0) * i / max(1, n - 1)) * SS / 2
            d.ellipse([x * SS - r, y * SS - r, x * SS + r, y * SS + r], fill=value)
        return m

    # ---------------------------------------------------------------- painting
    def fill(self, mask, color, alpha=1.0, blur=0.0, clip=None):
        """Paints colour through mask (optionally blurred, optionally clipped by another mask)."""
        m = mask
        if blur:
            m = m.filter(ImageFilter.GaussianBlur(blur * SS))
        if clip is not None:
            m = ImageChops.multiply(m, clip)
        if alpha != 1.0:
            m = m.point(lambda v: int(v * alpha))
        layer = Image.new("RGBA", self.img.size, tuple(color[:3]) + (0,))
        layer.putalpha(m)
        self.img.alpha_composite(layer)

    def gradient_fill(self, mask, top, bottom, y0, y1, alpha=1.0, clip=None):
        """Vertical gradient (design y0 -> y1) through mask."""
        h, w = self.img.size[1], self.img.size[0]
        t = np.clip((np.arange(h) / SS - y0) / max(1e-6, (y1 - y0)), 0, 1)[:, None]
        rgb = np.zeros((h, w, 3), np.float32)
        for i in range(3):
            rgb[..., i] = top[i] + (bottom[i] - top[i]) * t
        m = mask if clip is None else ImageChops.multiply(mask, clip)
        a = np.asarray(m, np.float32) * alpha
        layer = Image.fromarray(np.dstack([rgb, a]).clip(0, 255).astype(np.uint8), "RGBA")
        self.img.alpha_composite(layer)

    def texture_fill(self, mask, field, dark, light, alpha=1.0):
        """Colour from a [0,1] field (same SS size) between dark and light, through mask."""
        rgb = np.zeros(field.shape + (3,), np.float32)
        for i in range(3):
            rgb[..., i] = dark[i] + (light[i] - dark[i]) * field
        a = np.asarray(mask, np.float32) * alpha
        layer = Image.fromarray(np.dstack([rgb, a]).clip(0, 255).astype(np.uint8), "RGBA")
        self.img.alpha_composite(layer)

    def rim(self, mask, color, dx, dy, width, alpha=0.8, blur=1.0):
        """Rim light: the part of mask that is not covered by mask shifted by (dx, dy)."""
        shifted = ImageChops.offset(mask, int(dx * SS), int(dy * SS))
        edge = ImageChops.subtract(mask, shifted)
        edge = edge.filter(ImageFilter.MaxFilter(max(1, int(width * SS) | 1))) if width > 1 else edge
        edge = ImageChops.multiply(edge, mask)
        self.fill(edge, color, alpha=alpha, blur=blur)

    def result(self, scale=1.0):
        w, h = int(self.w * scale), int(self.h * scale)
        return self.img.resize((w, h), Image.LANCZOS)


def noise(w, h, seed, scales=(96, 48, 24, 12, 6)):
    """Value noise in [0,1], shape (h, w)."""
    rng = np.random.default_rng(seed)
    acc = np.zeros((h, w), np.float32)
    total = 0.0
    for i, s in enumerate(scales):
        gw, gh = w // s + 3, h // s + 3
        small = Image.fromarray((rng.random((gh, gw)) * 255).astype(np.uint8))
        up = np.asarray(small.resize((gw * s, gh * s), Image.BICUBIC), np.float32)[:h, :w] / 255
        wgt = 0.55 ** i
        acc += up * wgt
        total += wgt
    acc /= total
    acc = (acc - acc.min()) / max(1e-6, acc.max() - acc.min())
    return acc


def mask_from_array(a):
    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), "L")


def vertical_fade(canvas, y_full, y_zero):
    """Multiplies the canvas alpha so it fades from 1 at y_full to 0 at y_zero (design units)."""
    img = canvas.img
    h = img.size[1]
    y = np.arange(h) / SS
    f = np.clip((y_zero - y) / max(1e-6, (y_zero - y_full)), 0, 1)
    f = f * f * (3 - 2 * f)
    a = np.asarray(img.getchannel("A"), np.float32) * f[:, None]
    img.putalpha(Image.fromarray(a.astype(np.uint8)))


def rot(pts, cx, cy, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def shift(pts, dx, dy):
    return [(x + dx, y + dy) for x, y in pts]
