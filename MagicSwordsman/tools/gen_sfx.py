"""audio/sfx/*.wav: Ensifer's own sound effects, synthesised from scratch (no recorded or downloaded material).
User request 2026-10-09 "고유 사운드". Played by Visuals/Sfx.cs on the game's "SFX" bus.

Building blocks: filtered noise (whoosh / wind / fire roar), inharmonic metal partials (shing / clang), bell partials
(chime), pitch-dropping sine (thud), detuned low saws (hum), plus a small synthetic room reverb.
Output: 44.1 kHz, 16-bit mono WAV, peak-normalised to -3 dBFS (the game-side volume is set in Sfx.cs).
Deterministic (fixed seed). Re-run:  python3 tools/gen_sfx.py"""
import os
import wave
import numpy as np
from scipy import signal

SR = 44100
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'MagicSwordsman', 'audio', 'sfx')
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1009)


def t_(dur):
    return np.arange(int(dur * SR)) / SR


def env(dur, a, d, curve=4.0):
    """attack (linear) then exponential-ish decay over the rest."""
    t = t_(dur)
    e = np.minimum(t / max(a, 1e-4), 1.0)
    tail = np.clip((t - a) / max(d, 1e-4), 0, None)
    return e * np.exp(-curve * tail)


def pad(x, dur):
    n = int(dur * SR)
    return np.pad(x, (0, max(0, n - len(x))))[:n]


def at(x, start, dur):
    """x placed at start seconds inside a buffer of dur seconds."""
    out = np.zeros(int(dur * SR)); s = int(start * SR)
    n = min(len(x), len(out) - s)
    if n > 0:
        seg = x[:n].copy()
        if n < len(x): seg *= np.minimum(1, np.arange(n)[::-1] / (0.04 * SR))  # truncated: fade, never click
        out[s:s + n] += seg
    return out


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def lowpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'lowpass', fs=SR, output='sos'), x)


def highpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, 'highpass', fs=SR, output='sos'), x)


def sweep_noise(dur, f0, f1, q=1.6):
    """noise through a band-pass whose centre glides f0 -> f1 (shaped frame by frame in the STFT domain)."""
    n = int(dur * SR)
    f, tt, Z = signal.stft(rng.standard_normal(n), SR, nperseg=1024, noverlap=768)
    c = f0 * (f1 / f0) ** np.clip(tt / dur, 0, 1)                     # centre per frame
    bw = c / q
    G = np.exp(-0.5 * ((f[:, None] - c[None, :]) / bw[None, :]) ** 2)
    _, x = signal.istft(Z * G, SR, nperseg=1024, noverlap=768)
    return pad(x, dur)


def whoosh(dur=0.32, f0=500, f1=2600, peak=0.4):
    x = sweep_noise(dur, f0, f1, q=1.3)
    return x * env(dur, dur * peak, dur * (1 - peak), 3.0) * tailfade(t_(dur), 0.2)


def metal(dur, f0, ratios=(1, 2.76, 5.40, 8.93, 13.34), decay=(2.5, 3.5, 5, 7, 9), glide=0.0, amps=None):
    """bar / blade modes: inharmonic partials with their own decays (the 'shing')."""
    t = t_(dur); x = np.zeros_like(t)
    amps = amps or [1, 0.6, 0.45, 0.3, 0.2]
    for r, d, a in zip(ratios, decay, amps):
        f = f0 * r * (1 + glide * np.exp(-t * 18))
        ph = 2 * np.pi * np.cumsum(f) / SR
        x += a * np.sin(ph + rng.uniform(0, 6)) * np.exp(-d * t)
    return x * np.minimum(t / 0.002, 1) * tailfade(t)


def tailfade(t, part=0.3):
    """last `part` of an element fades to zero (ringing partials never stop abruptly)."""
    T = t[-1] if len(t) else 1
    return np.clip((T - t) / (T * part), 0, 1) ** 1.5


def shing(dur=0.9, f0=1650, glide=0.02):
    x = metal(dur, f0, glide=glide)
    scrape = highpass(rng.standard_normal(len(x)), 3500) * env(dur, 0.01, 0.12, 5) * 0.5
    beat = 1 + 0.15 * np.sin(2 * np.pi * 7 * t_(dur))
    return x * beat + scrape


def chime(dur, f0, decay=3.0):
    t = t_(dur); x = np.zeros_like(t)
    for r, a, d in ((1, 1, 1), (2.0, 0.5, 1.4), (3.0, 0.3, 2.0), (4.16, 0.25, 2.6), (5.43, 0.15, 3.4)):
        x += a * np.sin(2 * np.pi * f0 * r * t + rng.uniform(0, 6)) * np.exp(-decay * d * t)
    return x * np.minimum(t / 0.003, 1) * tailfade(t)


def thud(dur=0.4, f_hi=140, f_lo=42, decay=9):
    t = t_(dur)
    f = f_lo + (f_hi - f_lo) * np.exp(-t * 28)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-decay * t)
    click = lowpass(rng.standard_normal(len(t)), 1800) * env(dur, 0.001, 0.03, 6) * 0.6
    return x + click


def hum(dur, f0=82, swell=0.35):
    t = t_(dur); x = np.zeros_like(t)
    for det in (-0.6, 0, 0.7):
        ph = 2 * np.pi * f0 * (1 + det / 100) * t
        x += signal.sawtooth(ph + rng.uniform(0, 6), 0.5)  # triangle-ish
    x = lowpass(x, 420) + 0.35 * np.sin(2 * np.pi * f0 * 0.5 * t)
    return x * env(dur, dur * swell, dur * (1 - swell), 2.5) * tailfade(t)


def crackle(dur, rate=60, bright=4000):
    n = int(dur * SR); x = np.zeros(n)
    for _ in range(int(rate * dur)):
        i = rng.integers(0, n - 400); L = rng.integers(40, 300)
        x[i:i + L] += rng.standard_normal(L) * np.exp(-np.arange(L) / (L / 4)) * rng.uniform(0.3, 1)
    return highpass(x, bright * 0.4)


def drip(dur=0.25, f0=700, f1=1500):
    t = t_(dur)
    f = f0 + (f1 - f0) * (1 - np.exp(-t * 40))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * env(dur, 0.002, 0.08, 6)


def reverb(x, length=0.7, mix=0.25, damp=3000):
    ir_t = t_(length)
    ir = rng.standard_normal(len(ir_t)) * np.exp(-6 * ir_t / length)
    ir = lowpass(ir, damp); ir[:int(0.012 * SR)] = 0
    wet = signal.fftconvolve(x, ir)[:len(x) + len(ir) - 1]
    wet /= (np.max(np.abs(wet)) + 1e-9)
    dry = np.pad(x, (0, len(ir) - 1)) / (np.max(np.abs(x)) + 1e-9)
    return dry * (1 - mix) + wet * mix


def write(name, x, tail=0.5, mix=0.22):
    y = reverb(x, tail, mix) if tail > 0 else x
    fade = int(0.02 * SR); y[-fade:] *= np.linspace(1, 0, fade)
    # trim trailing near-silence
    thr = np.max(np.abs(y)) * 0.003; idx = np.nonzero(np.abs(y) > thr)[0]
    y = y[:idx[-1] + int(0.02 * SR)] if len(idx) else y
    y = y / (np.max(np.abs(y)) + 1e-9) * 10 ** (-3 / 20)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((y * 32767).astype('<i2').tobytes())
    print(f'{name}.wav  {len(y) / SR:.2f}s')


# ------------------------------------------------------------------ shared sounds
write('whoosh', whoosh(0.3, 380, 2400, 0.55), tail=0.25, mix=0.12)
write('impact', thud(0.35, 150, 45, 10) * 1.0 + at(shing(0.4, 2300) * 0.25, 0.0, 0.35)
      + lowpass(rng.standard_normal(int(0.35 * SR)), 2500) * env(0.35, 0.002, 0.06, 6) * 0.5, tail=0.3, mix=0.15)
write('switch', at(whoosh(0.18, 900, 3500, 0.7) * 0.6, 0, 0.7) + at(shing(0.6, 1900, 0.04) * 0.6, 0.11, 0.7),
      tail=0.45, mix=0.2)
write('circle', hum(1.0, 98, 0.3) * 0.8 + at(chime(0.8, 1318, 2.6) * 0.18, 0.18, 1.0)
      + at(chime(0.7, 1975, 3.0) * 0.12, 0.3, 1.0)
      + bandpass(rng.standard_normal(int(SR)), 5000, 9000) * env(1.0, 0.3, 0.7, 3) * 0.06, tail=0.6, mix=0.3)

# boss kill: a big brush-slash - rush in, ringing blade, deep boom, a long bright tail
D = 1.6
boss = (at(whoosh(0.35, 250, 3000, 0.85) * 0.9, 0.0, D)
        + at(shing(1.2, 1450, 0.05) * 0.7, 0.3, D)
        + at(shing(1.0, 2180, 0.03) * 0.35, 0.32, D)
        + at(thud(0.9, 120, 32, 4.0) * 1.2, 0.3, D)
        + at(lowpass(rng.standard_normal(int(0.9 * SR)), 900) * env(0.9, 0.005, 0.85, 4) * 0.5, 0.3, D)
        + at(chime(1.2, 659, 1.6) * 0.25, 0.45, D) + at(chime(1.1, 988, 1.8) * 0.2, 0.55, D))
write('bosskill', boss, tail=0.9, mix=0.3)

# ------------------------------------------------------------------ per-sword summons (~0.8 s each)
D = 0.9
base_whoosh = whoosh(0.3, 300, 1800, 0.8) * 0.35

summons = {
    # embers bursting: rising roar, crackle, a low thump
    'tyrfing': at(base_whoosh, 0, D) + at(sweep_noise(0.7, 200, 900, 1.0) * env(0.7, 0.12, 0.58, 3) * tailfade(t_(0.7)) * 0.8, 0.05, D)
               + at(lowpass(crackle(0.7, 70), 7000) * 0.12, 0.1, D) + at(thud(0.4, 110, 50, 8) * 0.8, 0.12, D),
    # ghost mist condensing: reversed-swell airy noise + eerie detuned sines with vibrato
    'skofnung': at(highpass(rng.standard_normal(int(0.6 * SR)), 2000) * (np.linspace(0, 1, int(0.6 * SR)) ** 3) * 0.4, 0, D)
                + at(sum(np.sin(2 * np.pi * f * t_(0.8) + 4 * np.sin(2 * np.pi * 5.5 * t_(0.8))) for f in (523, 531, 784))
                     * env(0.8, 0.35, 0.45, 3) * tailfade(t_(0.8)) * 0.25, 0.15, D) + at(chime(0.5, 1568, 4) * 0.2, 0.5, D),
    # light pillar descending: soft choir-like chord swell + a bell
    'durandal': at(sum(lowpass(signal.sawtooth(2 * np.pi * f * t_(0.85) * (1 + 0.003 * np.sin(9 * t_(0.85) + k))), 1600)
                       for k, f in enumerate((261.6, 329.6, 392.0, 523.3))) * env(0.85, 0.3, 0.55, 2.5) * tailfade(t_(0.85)) * 0.25, 0, D)
                + at(chime(0.7, 1046, 2.2) * 0.35, 0.3, D) + at(thud(0.3, 90, 50, 10) * 0.4, 0.32, D),
    # wind and grass swirl: gusting band noise, a light leaf rustle and a high chime
    'kusanagi': at(sweep_noise(0.8, 500, 1400, 2.2) * env(0.8, 0.25, 0.55, 2.5)
                   * (1 + 0.5 * np.sin(2 * np.pi * 4 * t_(0.8))) * tailfade(t_(0.8)), 0, D)
                + at(lowpass(crackle(0.5, 40, 6000), 9000) * 0.04, 0.15, D) + at(chime(0.5, 1760, 3.5) * 0.2, 0.4, D),
    # ink slash: sharp brush swish, deep taiko-like hit, a short shing
    'onimaru': at(whoosh(0.16, 1500, 5500, 0.6) * 0.8, 0, D) + at(thud(0.5, 160, 60, 7) * 1.0, 0.13, D)
               + at(shing(0.5, 1250, 0.02) * 0.35, 0.15, D),
    # blood drip: two drips over a dark low drone
    'dainsleif': at(hum(0.85, 55, 0.4) * 0.6, 0, D) + at(drip(0.25, 650, 1350) * 0.5, 0.18, D)
                 + at(drip(0.25, 600, 1250) * 0.35, 0.42, D) + at(shing(0.5, 980, 0.0) * 0.15, 0.3, D),
    # light flash: bright chime cluster + shimmer
    'claiomhsolais': at(bandpass(rng.standard_normal(int(0.7 * SR)), 6000, 12000) * env(0.7, 0.03, 0.65, 3) * 0.25, 0, D)
                     + at(chime(0.8, 1568, 2.5) * 0.4, 0.02, D) + at(chime(0.8, 2093, 2.8) * 0.3, 0.06, D)
                     + at(chime(0.8, 2637, 3.0) * 0.25, 0.1, D),
    # rainbow arc: rising arpeggio of chimes
    'caladbolg': sum(at(chime(0.6, f, 3.0) * 0.28, 0.07 * i, D)
                     for i, f in enumerate((784, 988, 1175, 1397, 1568, 1976, 2349)))
                 + at(base_whoosh, 0, D),
    # reforging: two anvil clangs + the blade ringing whole
    'gram': at(metal(0.4, 720, decay=(9, 11, 14, 16, 18)) * 0.6, 0.0, D) + at(metal(0.4, 760, decay=(9, 11, 14, 16, 18)) * 0.6, 0.16, D)
            + at(shing(0.7, 1500, 0.03) * 0.5, 0.3, D) + at(thud(0.3, 100, 50, 10) * 0.4, 0.3, D),
    # twin blades: a low and a high shing answering each other
    'ganjiang': at(base_whoosh, 0, D) + at(shing(0.7, 1150, 0.03) * 0.55, 0.08, D) + at(shing(0.6, 1720, 0.03) * 0.3, 0.22, D),
    'moye': at(base_whoosh, 0, D) + at(shing(0.7, 1720, 0.03) * 0.55, 0.08, D) + at(shing(0.6, 1150, 0.03) * 0.3, 0.22, D),
}
for name, x in summons.items():
    write('summon_' + name, x, tail=0.5, mix=0.22)
