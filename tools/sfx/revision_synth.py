#!/usr/bin/env python3
"""Deterministic placeholder masters for the Revision ceremony.

These are intentionally synthetic, not final mix assets: glass harmonics, dry sand, and a single
low bell establish an ownable temporal vocabulary while the normal sfx.py bake still owns format,
length, high-pass, release, and level. No third-party samples or runtime procedural audio.
"""

import math
import os
import random
import struct
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "docs", "audio", "src", "board")
SR = 44100
TAU = math.tau


def sine(t, hz, phase=0.0):
    return math.sin(TAU * hz * t + phase)


def exp_decay(t, speed):
    return math.exp(-speed * max(0.0, t))


def smooth(a):
    return a * a * (3.0 - 2.0 * a)


def clamp(a):
    return max(-0.98, min(0.98, a))


def finish(samples, fade_ms=8):
    n = max(1, int(SR * fade_ms / 1000))
    for i in range(min(n, len(samples))):
        samples[i] *= i / n
        samples[-1 - i] *= i / n
    peak = max(abs(x) for x in samples) or 1.0
    return [clamp(x * 0.88 / peak) for x in samples]


def write(name, seconds, fn, fade_ms=8):
    rng = random.Random("warband.revision." + name)
    samples = [fn(i / SR, i / max(1, int(seconds * SR) - 1), rng)
               for i in range(int(seconds * SR))]
    samples = finish(samples, fade_ms)
    path = os.path.join(OUT, name + ".wav")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = [int(round(x * 32767)) for x in samples]
    with wave.open(path, "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(SR)
        wav.writeframes(struct.pack("<%dh" % len(pcm), *pcm))
    print(os.path.relpath(path, ROOT))


def glass_bell(t, base, decay=5.0):
    return exp_decay(t, decay) * (
        0.62 * sine(t, base) +
        0.25 * sine(t, base * 2.71, 0.4) +
        0.13 * sine(t, base * 4.19, 1.1))


def grit(t, rng, density=1.0):
    # Sample-and-hold dust is dryer than full-band white noise and survives the pipeline HPF.
    cell = int(t * 7200 * density)
    local = random.Random(cell * 7919 + 17)
    return local.uniform(-1.0, 1.0)


write("revision_split", 1.05, lambda t, p, r:
      0.55 * glass_bell(t, 61.0, 2.8) +
      (0.34 * grit(t, r, 1.8) + 0.18 * sine(t, 1550 + 2300 * p))
      * exp_decay(t, 9.0) +
      0.22 * glass_bell(max(0.0, t - 0.14), 392.0, 5.5)
      * (1.0 if t >= 0.14 else 0.0))


def hold(t, p, r):
    # Frequencies are integer multiples of the loop period so both ends meet in phase.
    return (0.11 * sine(t, 8 / 0.95) +
            0.08 * sine(t, 19 / 0.95, 0.7) +
            0.035 * grit(t, r, 0.35) *
            (0.55 + 0.45 * sine(t, 5 / 0.95)))


write("revision_hold", 0.95, hold, fade_ms=0)
write("revision_final_hold", 0.95, lambda t, p, r:
      0.15 * sine(t, 5 / 0.95) +
      0.09 * sine(t, 11 / 0.95, 0.8) +
      0.04 * grit(t, r, 0.28) * (0.6 + 0.4 * sine(t, 3 / 0.95)),
      fade_ms=0)

write("revision_reopen", 0.21, lambda t, p, r:
      0.52 * sine(t, 840 - 390 * smooth(p)) * exp_decay(t, 11.0) +
      0.35 * grit(t, r, 1.4) * exp_decay(t, 17.0))

write("revision_scrub", 0.11, lambda t, p, r:
      0.72 * glass_bell(t, 980.0, 24.0) +
      0.25 * grit(t, r, 1.6) * exp_decay(t, 30.0), fade_ms=3)

write("revision_tear", 0.48, lambda t, p, r:
      0.45 * grit(t, r, 2.2) * exp_decay(t, 7.0) +
      0.32 * sine(t, 2800 - 2200 * smooth(p)) * exp_decay(t, 5.0) +
      0.34 * glass_bell(t, 73.0, 4.0))


def rewind_bed(t, p, r):
    return (0.10 * sine(t, 9 / 0.8) +
            0.07 * sine(t, 23 / 0.8, 1.2) +
            0.06 * grit(t, r, 0.45) * (0.65 + 0.35 * sine(t, 7 / 0.8)))


write("revision_rewind_bed", 0.8, rewind_bed, fade_ms=0)

write("revision_rewind_riser", 1.9, lambda t, p, r:
      smooth(p) * (
          0.28 * sine(t, 110 + 900 * p * p) +
          0.18 * sine(t, 700 + 3500 * p * p, 0.4) +
          0.22 * grit(t, r, 1.5 + 2.5 * p)) +
      0.22 * glass_bell(t, 49.0, 1.4), fade_ms=3)


def borrowed_land(t, p, r):
    onset = exp_decay(t, 5.0)
    shimmer = (1.0 - exp_decay(t, 12.0)) * exp_decay(t, 2.8)
    return (0.52 * glass_bell(t, 65.4, 3.4) +
            0.34 * sine(t, 523.25, 0.2) * onset +
            shimmer * (0.20 * sine(t, 1046.5) + 0.13 * sine(t, 1568.0, 0.8)) +
            0.11 * grit(t, r, 1.2) * exp_decay(t, 8.0))


write("revision_land_borrowed", 1.02, borrowed_land, fade_ms=16)


def recall_land(t, p, r):
    snap = exp_decay(t, 7.5)
    return (0.55 * glass_bell(t, 55.0, 3.1) +
            0.27 * sine(t, 310 - 180 * smooth(p), 1.1) * snap +
            0.18 * sine(t, 233.1, 0.3) * exp_decay(t, 2.6) +
            0.14 * grit(t, r, 1.0) * exp_decay(t, 9.0))


write("revision_land_recall", 1.02, recall_land, fade_ms=16)

write("revision_return", 0.58, lambda t, p, r:
      0.56 * glass_bell(t, 293.7, 7.0) +
      0.30 * sine(t, 160 + 420 * smooth(p)) * exp_decay(t, 5.5) +
      0.11 * grit(t, r, 0.9) * exp_decay(t, 10.0))
