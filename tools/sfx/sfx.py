#!/usr/bin/env python3
"""warband SFX tooling — measure, lint, bake, audition, and price combat audio density.

The gap this closes is the one root-caused in `docs/vault/Design/audio.md` §2: the batch that
shipped muted "passed structural validation" — i.e. the files existed and imported. Nothing
measured onset latency, duration, level or crest, which are the four numbers that decide whether a
cue feels good. So a 1.04 s click reached the game and nobody knew until it was played.

    sfx.py measure <dir|file>...   raw numbers, no contract
    sfx.py lint                    measure + enforce families.json + cross-check tuning.json
    sfx.py bake                    src/ -> baked/: mono, trim to transient, cap, HPF, normalise
    sfx.py sheet                   docs/audio/index.html — waveforms + A/B players + pass/fail
    sfx.py density                 sound onsets/sec per fixture (the voice budget input)

Python 3 stdlib only, no Unity, headless. Everything writes under docs/audio/ and tools/ —
Resources/ is never touched, so the game stays bit-identical until the direction is approved.
"""

import argparse
import json
import math
import os
import re
import struct
import subprocess
import sys
import wave

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FAMILIES = os.path.join(ROOT, "tools", "sfx", "families.json")
WORK = os.path.join(ROOT, "docs", "audio")
TUNING = os.path.join(ROOT, "client", "Assets", "StreamingAssets", "tuning.json")

# First sample above this fraction of the clip's own peak counts as the onset. -34 dB rel. peak:
# low enough to catch a soft attack, high enough to ignore generator noise floor.
ONSET_REL = 0.02


# --------------------------------------------------------------------------- WAV I/O


def read_wav(path):
    """-> (samples as floats in [-1,1] mono-mixed, samplerate, source channels)."""
    with wave.open(path, "rb") as w:
        n, sr, ch, sw = w.getnframes(), w.getframerate(), w.getnchannels(), w.getsampwidth()
        raw = w.readframes(n)
    if sw == 2:
        a = struct.unpack("<%dh" % (len(raw) // 2), raw)
        scale = 32768.0
    elif sw == 1:
        a = [b - 128 for b in raw]          # 8-bit WAV is unsigned
        scale = 128.0
    elif sw == 3:
        a = [int.from_bytes(raw[i:i + 3], "little", signed=True) for i in range(0, len(raw), 3)]
        scale = 8388608.0
    elif sw == 4:
        a = struct.unpack("<%di" % (len(raw) // 4), raw)
        scale = 2147483648.0
    else:
        raise ValueError(f"{path}: unsupported sample width {sw}")
    a = [x / scale for x in a]
    if ch > 1:                               # downmix
        a = [sum(a[i:i + ch]) / ch for i in range(0, len(a) - ch + 1, ch)]
    return a, sr, ch


def write_wav(path, samples, sr):
    """16-bit mono PCM. Clamped, not dithered — these are short cues, not masters."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ints = [max(-32768, min(32767, int(round(x * 32767)))) for x in samples]
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(struct.pack("<%dh" % len(ints), *ints))


# --------------------------------------------------------------------------- measurement


def measure(path):
    a, sr, ch = read_wav(path)
    n = len(a)
    peak = max((abs(x) for x in a), default=0.0)
    if peak <= 0.0:
        return dict(path=path, sr=sr, channels=ch, samples=n, silent=True, onset_ms=0.0,
                    audible_ms=0.0, total_ms=n / sr * 1000, peak_db=-99.0, crest_db=0.0)
    thr = peak * ONSET_REL
    first = next((i for i, x in enumerate(a) if abs(x) > thr), 0)
    last = next((i for i in range(n - 1, -1, -1) if abs(a[i]) > thr), n - 1)
    span = a[first:last + 1]
    rms = math.sqrt(sum(x * x for x in span) / max(1, len(span)))
    return dict(
        path=path, sr=sr, channels=ch, samples=n, silent=False,
        onset_ms=first / sr * 1000,
        audible_ms=len(span) / sr * 1000,
        total_ms=n / sr * 1000,
        peak_db=20 * math.log10(peak),
        crest_db=20 * math.log10(peak / max(1e-9, rms)),
    )


# --------------------------------------------------------------------------- DSP (bake)


def highpass(a, sr, hz, poles=2):
    """Cascaded one-pole high-pass. 12 dB/oct at poles=2 — enough to pull the low mud out of a
    small cue without needing a biquad. Also kills DC, so no separate mean-subtract."""
    if hz <= 0:
        mean = sum(a) / max(1, len(a))
        return [x - mean for x in a]
    rc = 1.0 / (2 * math.pi * hz)
    alpha = rc / (rc + 1.0 / sr)
    for _ in range(poles):
        out = [0.0] * len(a)
        prev_x = prev_y = 0.0
        for i, x in enumerate(a):
            y = alpha * (prev_y + x - prev_x)
            out[i] = y
            prev_x, prev_y = x, y
        a = out
    return a


def bake_one(path, spec):
    """mono -> trim front -> cap -> HPF -> re-trim tail -> fade -> normalise.

    Order matters, and two of the steps are ordered by a non-obvious dependency:

    * The HPF must run BEFORE the tail trim. Filtering removes low-frequency rumble, so a clip's
      audible span genuinely shortens as the mud comes out — trimming first leaves up to 100 ms of
      sub-threshold tail behind (found by `lint` on the first bake of `commit_*`/`riser_pyromancer`).
      Dead tail is not just wasted bytes: it holds a pooled voice open for its whole length.
    * Normalisation must run LAST, because both the filter and the fade change the peak.
    """
    a, sr, _ = read_wav(path)
    peak = max((abs(x) for x in a), default=0.0)
    if peak <= 0.0:
        return None, "silent source"

    thr = peak * ONSET_REL
    first = next((i for i, x in enumerate(a) if abs(x) > thr), 0)

    # Back off 1 ms before the detected onset so the attack's leading edge survives the cut.
    start = max(0, first - int(sr * 0.001))
    a = a[start:start + int(sr * spec["maxAudibleMs"] / 1000.0)]
    if not a:
        return None, "empty after trim"

    a = highpass(a, sr, spec["highPassHz"])

    peak = max((abs(x) for x in a), default=0.0)
    if peak <= 0.0:
        return None, "silent after filtering"
    thr = peak * ONSET_REL
    last = next((i for i in range(len(a) - 1, -1, -1) if abs(a[i]) > thr), len(a) - 1)

    # Two endings, and using one fade for both is what made clips "end really abruptly"
    # (Jake, 2026-07-27). A clip that DECAYED to the threshold on its own needs only a short
    # anti-click fade — nothing is being removed. A clip the cap TRUNCATED is still loud at the cut
    # (measured: riser_phalanx at -3.0 dB, i.e. full volume) and needs a real release.
    # Tolerance matters here. `last + 1 >= len(a)` looks equivalent but requires the very LAST
    # sample to be above threshold, and a waveform crosses zero constantly — so whether a clip got
    # its release came down to where a zero crossing happened to fall. cast_generic missed by one
    # sample (last=17638 of 17640) and shipped with a 12 ms gate on a -11 dB tail. Ask the real
    # question instead: is it still loud NEAR the end?
    truncated = (len(a) - 1 - last) < int(sr * 0.005)
    fo = int(sr * (spec["releaseMs"] if truncated else spec["fadeOutMs"]) / 1000.0)
    a = a[:min(len(a), last + 1 + fo)]
    fo = min(fo, len(a))

    # 1 ms fade-in: the trim can land on a non-zero sample, which clicks.
    fi = min(int(sr * 0.001), len(a))
    for i in range(fi):
        a[i] *= i / fi
    # Linear on a still-loud sound reads as a fader pull. A real decay is exponential — fast drop,
    # long tail — so use that when releasing a truncation, and keep linear for the tiny anti-click.
    if truncated and fo > 1:
        k = 4.0
        base = math.exp(-k)
        for i in range(fo):
            t = 1.0 - i / fo                       # i counts back from the final sample
            a[len(a) - 1 - i] *= (math.exp(-k * t) - base) / (1.0 - base)
    else:
        for i in range(fo):
            a[len(a) - 1 - i] *= i / max(1, fo)

    peak = max((abs(x) for x in a), default=0.0)
    if peak <= 0.0:
        return None, "silent after fade"
    a = [x * (10 ** (spec["targetPeakDb"] / 20.0) / peak) for x in a]
    return (a, sr), None


# --------------------------------------------------------------------------- contract


def load_families():
    with open(FAMILIES) as f:
        cfg = json.load(f)
    out = {}
    for surface, sdef in cfg["surfaces"].items():
        fams = {}
        for name, fdef in sdef["families"].items():
            spec = dict(cfg["defaults"])
            spec.update({k: v for k, v in fdef.items() if not k.startswith("_")})
            spec["family"] = name
            spec["surface"] = surface
            fams[name] = spec
        out[surface] = dict(resources=sdef["resources"], families=fams)
    return out


def expected_files(spec):
    """A family with variants=n expects `name_1..name_n`; variants=0 expects a bare `name`."""
    n = spec.get("variants", 0)
    return [f"{spec['family']}_{i}" for i in range(1, n + 1)] if n else [spec["family"]]


def check(m, spec):
    """-> list of human-readable violations."""
    bad = []
    if m["silent"]:
        return ["clip is silent"]
    if m["channels"] != spec["channels"]:
        bad.append(f"{m['channels']}ch (want {spec['channels']})")
    if m["sr"] != spec["sampleRate"]:
        bad.append(f"{m['sr']}Hz (want {spec['sampleRate']})")
    if m["onset_ms"] > spec["maxOnsetMs"]:
        bad.append(f"onset {m['onset_ms']:.0f}ms > {spec['maxOnsetMs']}ms")
    if m["audible_ms"] > spec["maxAudibleMs"]:
        bad.append(f"audible {m['audible_ms']:.0f}ms > {spec['maxAudibleMs']}ms")
    # Trailing silence past the fade is wasted voice-time, not just wasted bytes — it holds a pooled
    # voice open for its whole length.
    #
    # The threshold stays PEAK-RELATIVE (-34 dB) rather than absolute, and that is load-bearing:
    # the shipped padded clips are not digital silence. Measured, `select_1` carries 820 ms of tail
    # at -34 dB rel. peak but only 20 ms at -60 dBFS — the padding is low-level noise around -40 dB.
    # An absolute floor would have scored the worst clip in the set as clean.
    #
    # The budget then has to accommodate a deliberate exponential release, which spends its last
    # ~18% below that threshold, plus the sound's own natural decay. 1.5x covers both and still
    # leaves a 3-10x margin against real padding (820 ms vs a 240 ms worst-case budget).
    tail_budget = max(40.0, spec["releaseMs"] * 1.5)
    tail = m["total_ms"] - m["audible_ms"] - m["onset_ms"]
    if tail > tail_budget:
        bad.append(f"{tail:.0f}ms dead tail (budget {tail_budget:.0f}ms)")
    if abs(m["peak_db"] - spec["targetPeakDb"]) > spec["peakTolDb"]:
        bad.append(f"peak {m['peak_db']:.1f}dB (want {spec['targetPeakDb']:.1f}±{spec['peakTolDb']})")
    if m["crest_db"] < spec["minCrestDb"]:
        bad.append(f"crest {m['crest_db']:.1f}dB < {spec['minCrestDb']}dB (no transient)")
    return bad


# --------------------------------------------------------------------------- tuning.json x-ref


def tuning_sound_ids():
    """Every clip id any tell row names, with the field it came from."""
    with open(TUNING) as f:
        d = json.load(f)
    ids = {}
    for row in d.get("tells", []):
        for field in ("sound", "critSound", "castSound"):
            v = row.get(field, "")
            if v:
                ids.setdefault(v, set()).add(field)
    return ids


# --------------------------------------------------------------------------- commands


def cmd_measure(args):
    paths = []
    for t in args.targets:
        t = t if os.path.isabs(t) else os.path.join(ROOT, t)
        if os.path.isdir(t):
            paths += [os.path.join(t, f) for f in sorted(os.listdir(t)) if f.endswith(".wav")]
        else:
            paths.append(t)
    if not paths:
        print("no .wav found", file=sys.stderr)
        return 1
    print(f"{'clip':28s} {'onset':>8s} {'audible':>9s} {'total':>8s} {'peak':>9s} {'crest':>8s}  fmt")
    print("-" * 82)
    for p in paths:
        m = measure(p)
        print(f"{os.path.basename(p):28s} {m['onset_ms']:7.1f}ms {m['audible_ms']:8.1f}ms "
              f"{m['total_ms']:7.1f}ms {m['peak_db']:8.1f}dB {m['crest_db']:7.1f}dB  "
              f"{m['channels']}ch/{m['sr']}")
    return 0


def cmd_lint(args):
    cfg = load_families()
    referenced = tuning_sound_ids()
    fails = 0
    board_ids = set()

    for surface, sdef in cfg.items():
        root = os.path.join(ROOT, args.dir, surface) if args.dir else os.path.join(ROOT, sdef["resources"])
        print(f"\n=== {surface}  ({os.path.relpath(root, ROOT)})")
        if not os.path.isdir(root):
            print(f"  !! directory does not exist")
            fails += 1
            continue
        present = {f[:-4] for f in os.listdir(root) if f.endswith(".wav")}
        for name, spec in sdef["families"].items():
            for stem in expected_files(spec):
                path = os.path.join(root, stem + ".wav")
                if not os.path.exists(path):
                    print(f"  MISSING  {stem}")
                    fails += 1
                    continue
                if surface == "board":
                    board_ids.add(stem)
                present.discard(stem)
                bad = check(measure(path), spec)
                if bad:
                    print(f"  FAIL     {stem:22s} {'; '.join(bad)}")
                    fails += 1
                else:
                    print(f"  ok       {stem}")
        for extra in sorted(present):
            if surface == "board":
                board_ids.add(extra)
            print(f"  extra    {extra:22s} (no family in families.json)")

    # Both directions against tuning.json — the reverse check is what surfaced the 10 mute hit_*.
    # Board only: UI cues are dispatched by `Family()` in C#, not named in tell rows.
    print("\n=== tuning.json cross-reference (board only)")
    if args.dir:
        print(f"  note: linting {args.dir}, which uses post-D3 family names — tell rows still")
        print(f"        name the pre-collapse ids, so every mismatch below is expected until the")
        print(f"        rows are repointed (Design/audio.md build order step 6).")
    dangling = sorted(i for i in referenced if i not in board_ids)
    if dangling:
        print(f"  {len(dangling)} id(s) referenced by tell rows with NO clip (silent no-op):")
        for i in dangling:
            print(f"    {i:20s} via {'/'.join(sorted(referenced[i]))}")
    unused = sorted(i for i in board_ids if i not in referenced)
    if unused:
        print(f"  {len(unused)} clip(s) no tell row names: {', '.join(unused)}")
    if not dangling and not unused:
        print("  clean")

    print(f"\n{'FAILED' if fails else 'PASS'} — {fails} violation(s)")
    return 1 if fails and not args.report_only else 0


def cmd_bake(args):
    cfg = load_families()
    src_root = os.path.join(WORK, "src")
    out_root = os.path.join(WORK, "baked")
    total = ok = 0
    for surface, sdef in cfg.items():
        src = os.path.join(src_root, surface)
        if not os.path.isdir(src):
            print(f"  (no sources for {surface} at {os.path.relpath(src, ROOT)})")
            continue
        print(f"\n=== {surface}")
        for stem in sorted(f[:-4] for f in os.listdir(src) if f.endswith(".wav")):
            total += 1
            family = re.sub(r"_\d+$", "", stem)
            spec = sdef["families"].get(family)
            if spec is None:
                print(f"  SKIP  {stem:22s} no family '{family}' in families.json")
                continue
            baked, err = bake_one(os.path.join(src, stem + ".wav"), spec)
            if err:
                print(f"  FAIL  {stem:22s} {err}")
                continue
            samples, sr = baked
            out = os.path.join(out_root, surface, stem + ".wav")
            write_wav(out, samples, sr)
            before = measure(os.path.join(src, stem + ".wav"))
            after = measure(out)
            ok += 1
            print(f"  ok    {stem:22s} {before['audible_ms']:6.0f}ms -> {after['audible_ms']:5.0f}ms   "
                  f"{before['peak_db']:6.1f}dB -> {after['peak_db']:5.1f}dB   "
                  f"{before['total_ms'] * before['sr'] / 1000 * before['channels'] * 2 / 1024:6.0f}KB -> "
                  f"{os.path.getsize(out) / 1024:5.0f}KB")
    print(f"\nbaked {ok}/{total} into {os.path.relpath(out_root, ROOT)}")
    return 0 if ok == total else 1


def cmd_density(args):
    """Sound onsets per second of real playback, per fixture.

    Shells the existing `--coverage` reporter (the source of truth for event counts) and asks, per
    signature, whether ANY tell row naming that signature carries a `sound`. That is an ONSET
    count, not a clip-identity resolution: the per-weapon/per-ability rows only decide WHICH clip
    plays, never WHETHER one does, so they cannot change the number. `minAmount` gating can only
    reduce it, so this is an honest upper bound.
    """
    with open(TUNING) as f:
        tells = json.load(f)["tells"]

    # Mirror of ReplayPlayer.BusFor + the cast path. Kept deliberately tiny because it IS a second
    # copy of a rule that lives in C#; it exists only to price the per-bus caps offline, never at
    # runtime. If BusFor changes, change this line and say so.
    BUS_CAPS = {"Decisive": 4, "Cast": 4, "Impact": 6, "State": 3}

    def bus_for(sig):
        """Coverage signature -> the bus its impact-time sound would ride.
        Mirrors ReplayPlayer.BusFor. Note `Attack` -> Impact: the per-weapon hit sounds are authored
        on the SWING event, not on DamageDealt (which carries no sound row at all), so treating only
        `Damage` as Impact silently files every weapon hit under State."""
        head = sig.split("/")[0]
        if head in ("Death", "CheatDeath"):
            return "Decisive"          # crits also land here, but no signature marks them
        if head in ("Attack", "Damage"):
            return "Impact"
        if head == "Cast":
            return "Cast"
        return "State"

    def sounds_for(sig):
        """Does this coverage signature reach a tell row with a sound? -> (impact, cast)"""
        parts = sig.split("/")
        head, qual = parts[0], (parts[1] if len(parts) > 1 else None)
        kind = {"Damage": "DamageDealt", "Status+": "StatusApplied", "Status-": "StatusExpired",
                "Field": "FieldCreated"}.get(head, head)
        impact = cast = False
        for r in tells:
            if r.get("eventKind") != kind:
                continue
            if r.get("byCause") and qual and r.get("cause") != qual:
                continue
            if r.get("byStatus") and qual and r.get("status") != qual:
                continue
            if r.get("byFlavor") and qual and r.get("flavor") != qual:
                continue
            impact = impact or bool(r.get("sound"))
            cast = cast or bool(r.get("castSound"))
        return impact, cast

    fixtures = args.fixtures or sorted(
        os.path.join("client/Assets/StreamingAssets/replays", f)
        for f in os.listdir(os.path.join(ROOT, "client/Assets/StreamingAssets/replays"))
        if f.endswith(".bytes"))

    def mean_audible(d):
        d = os.path.join(ROOT, d)
        if not os.path.isdir(d):
            return None
        ms = [measure(os.path.join(d, f))["audible_ms"]
              for f in os.listdir(d) if f.endswith(".wav")]
        return sum(ms) / len(ms) / 1000.0 if ms else None

    shipped = mean_audible("client/Assets/Resources/Board/SFX")
    baked = mean_audible("docs/audio/baked/board")

    bus_rows = []
    print(f"{'fixture':22s} {'events':>7s} {'ticks':>6s} {'play s':>7s} "
          f"{'onsets':>7s} {'/sec':>6s} {'voices now':>11s} {'baked':>8s}")
    print("-" * 84)
    for fx in fixtures:
        try:
            out = subprocess.run(
                ["dotnet", "run", "--project", "sim/Warband.Viewer", "-c", "Release",
                 "--", "--coverage", fx],
                cwd=ROOT, capture_output=True, text=True, timeout=300).stdout
        except (subprocess.TimeoutExpired, FileNotFoundError) as e:
            print(f"{os.path.basename(fx):22s} !! {e}")
            continue
        head = re.search(r"###\s+(\S+).*?(\d+) events, (\d+) ticks", out)
        if not head:
            print(f"{os.path.basename(fx):22s} !! could not parse coverage header")
            continue
        name, events, ticks = head.group(1), int(head.group(2)), int(head.group(3))
        onsets = 0
        per_bus = {b: 0 for b in BUS_CAPS}
        for row in re.finditer(r"^\|\s*([A-Za-z+\-/]+)\s*\|\s*(\d+)\s*\|", out, re.M):
            sig, count = row.group(1), int(row.group(2))
            impact, cast = sounds_for(sig)
            onsets += count * (int(impact) + int(cast))   # Cast fires castSound AND sound
            if impact:
                per_bus[bus_for(sig)] += count
            if cast:
                per_bus["Cast"] += count
        bus_rows.append((name, ticks / 5.0, per_bus))
        play_s = ticks / 5.0                              # 5 tps playback (half the 10 tps contract)
        rate = onsets / play_s if play_s else 0
        now = f"{rate * shipped:10.1f}" if shipped else "         ?"
        aft = f"{rate * baked:7.1f}" if baked else "      ?"
        print(f"{name:22s} {events:7d} {ticks:6d} {play_s:6.1f}s {onsets:7d} {rate:5.1f}/s "
              f"{now} {aft}")
    # Per-bus pressure: onsets/s on a bus x that bus's mean clip length = voices it wants at once.
    # Over its cap means SfxPlayer steals from within the bus, i.e. sounds silently disappear.
    if baked and bus_rows:
        lens = {}
        bd = os.path.join(ROOT, "docs/audio/baked/board")
        if os.path.isdir(bd):
            for f in os.listdir(bd):
                if f.endswith(".wav"):
                    lens[f[:-4]] = measure(os.path.join(bd, f))["audible_ms"] / 1000.0
        fam = {
            "Decisive": ["death", "cheatdeath", "crit"],
            "Cast":     [k for k in lens if k.startswith("riser_")] + ["cast_generic", "cast_fire"],
            "Impact":   [k for k in lens if k.startswith("hit_")],
            "State":    ["heal", "shield"],
        }
        print(f"\n{'fixture':22s} " + " ".join(f"{b:>12s}" for b in BUS_CAPS))
        print(f"{'':22s} " + " ".join(f"{'(cap ' + str(c) + ')':>12s}" for c in BUS_CAPS.values()))
        print("-" * 84)
        worst = {}
        for name, play_s, per in bus_rows:
            cells = []
            for b, cap in BUS_CAPS.items():
                ln = [lens[k] for k in fam[b] if k in lens]
                mean = sum(ln) / len(ln) if ln else 0.0
                v = (per[b] / play_s) * mean if play_s else 0.0
                worst[b] = max(worst.get(b, 0.0), v)
                cells.append(f"{v:11.1f}" + ("!" if v > cap else " "))
            print(f"{name:22s} " + " ".join(cells))
        print()
        for b, cap in BUS_CAPS.items():
            v = worst.get(b, 0.0)
            print(f"  {b:9s} peak {v:5.1f} vs cap {cap}  "
                  + ("OVER — voices would be stolen within the bus" if v > cap else "ok"))

    print("\nonsets = events whose signature reaches a tell row carrying sound/castSound.")
    print(f"voices = onsets/s x mean audible board-clip length "
          f"(shipped {shipped * 1000:.0f} ms -> baked {baked * 1000:.0f} ms)."
          if shipped and baked else "")
    print("That length, not the voice pool, is the §3.1 lever — the pool only catches the peaks.")
    return 0


def cmd_sheet(args):
    """Self-referencing HTML audition page: waveform, A/B players, pass/fail per clip."""
    cfg = load_families()
    src_root, baked_root = os.path.join(WORK, "src"), os.path.join(WORK, "baked")
    out = os.path.join(WORK, "index.html")

    # Time and amplitude are both ABSOLUTE across every waveform on the page: full height = 0 dBFS,
    # full width = `span_ms`. Per-clip normalisation (the obvious implementation) would make a
    # -11.9 dB source and its -5.0 dB bake draw identically and a 945 ms clip draw the same width as
    # a 49 ms one — hiding the exact two things this page exists to compare.
    def spark(path, span_ms, w=320, h=44):
        a, sr, _ = read_wav(path)
        total = max(1, int(sr * span_ms / 1000.0))
        step = max(1, total // w)
        pts = []
        for i in range(0, total, step):
            seg = a[i:i + step]
            pts.append(max((abs(x) for x in seg), default=0.0))
        d = " ".join(f"M{x},{h/2 - v*h/2:.1f}V{h/2 + v*h/2:.1f}"
                     for x, v in enumerate(pts) if v > 0.001)
        return (f'<svg viewBox="0 0 {max(1, len(pts))} {h}" preserveAspectRatio="none" class="wf">'
                f'<path d="{d}"/></svg>')

    rows = []
    for surface, sdef in cfg.items():
        rows.append(f"<h2>{surface}</h2>")
        sdir, bdir = os.path.join(src_root, surface), os.path.join(baked_root, surface)
        if not os.path.isdir(bdir):
            rows.append("<p class='muted'>nothing baked yet — run <code>make sfx-bake</code></p>")
            continue
        for stem in sorted(f[:-4] for f in os.listdir(bdir) if f.endswith(".wav")):
            family = re.sub(r"_\d+$", "", stem)
            spec = sdef["families"].get(family)
            bpath = os.path.join(bdir, stem + ".wav")
            spath = os.path.join(sdir, stem + ".wav")
            m = measure(bpath)
            bad = check(m, spec) if spec else ["no family"]
            verdict = ("<span class='bad'>" + "; ".join(bad) + "</span>") if bad \
                else "<span class='ok'>meets contract</span>"
            sm = measure(spath) if os.path.exists(spath) else None
            # One time-axis for both waveforms in a pair, so the length cut is visible as width.
            # Capped at 6x the baked length, or a 50 ms tick would be a 5% sliver of its own 1040 ms
            # source and you could see THAT it was cut but not WHAT is left.
            full = max(m["total_ms"], sm["total_ms"] if sm else 0)
            span = min(full, m["total_ms"] * 6)
            axis = (f"{span:.0f} ms axis · source runs to {full:.0f} ms"
                    if full > span + 1 else f"{span:.0f} ms axis")
            before = ""
            if sm:
                before = (f"<div class='ab'><span class='lbl'>before</span>"
                          f"<audio controls preload='none' src='src/{surface}/{stem}.wav'></audio>"
                          f"<span class='num'>{sm['audible_ms']:.0f} ms · {sm['peak_db']:.1f} dBFS "
                          f"· crest {sm['crest_db']:.1f} dB</span>"
                          f"{spark(spath, span)}</div>")
            rows.append(f"""
<div class="clip">
  <div class="name">{stem} <span class="fam">{family} · {axis}</span></div>
  <div class="ab"><span class="lbl">after</span>
    <audio controls preload="none" src="baked/{surface}/{stem}.wav"></audio>
    <span class="num">{m['audible_ms']:.0f} ms · {m['peak_db']:.1f} dBFS · crest {m['crest_db']:.1f} dB</span>
    {spark(bpath, span)}</div>
  {before}
  <div class="verdict">{verdict}</div>
</div>""")

    html = f"""<!doctype html><meta charset="utf-8"><title>warband SFX audition</title>
<style>
 :root {{ color-scheme: light dark; --bg:#faf9f7; --fg:#1a1a1a; --mut:#6b6b6b; --line:#e2e0dc;
          --ok:#1f7a3d; --bad:#a8321f; --wave:#3b6ea5; }}
 @media (prefers-color-scheme:dark) {{ :root {{ --bg:#16161a; --fg:#e8e6e3; --mut:#9a9a9a;
          --line:#2c2c33; --ok:#5fbe7f; --bad:#e0765f; --wave:#6fa8dc; }} }}
 body {{ background:var(--bg); color:var(--fg); font:14px/1.5 ui-sans-serif,system-ui,sans-serif;
         max-width:900px; margin:0 auto; padding:2rem 1.25rem 5rem; }}
 h1 {{ font-size:1.5rem; margin:0 0 .25rem; }} h2 {{ margin:2.5rem 0 .5rem; font-size:1.1rem;
        text-transform:uppercase; letter-spacing:.08em; color:var(--mut); }}
 .lead {{ color:var(--mut); margin:0 0 1rem; }}
 .clip {{ border-top:1px solid var(--line); padding:.85rem 0; }}
 .name {{ font-weight:600; font-family:ui-monospace,monospace; }}
 .fam {{ color:var(--mut); font-weight:400; margin-left:.6rem; font-size:.85em; }}
 .ab {{ display:flex; align-items:center; gap:.6rem; margin-top:.35rem; flex-wrap:wrap; }}
 .lbl {{ width:3.2rem; color:var(--mut); font-size:.8rem; text-transform:uppercase;
         letter-spacing:.05em; }}
 audio {{ height:32px; }}
 .num {{ font-family:ui-monospace,monospace; font-size:.8rem; color:var(--mut);
         min-width:15rem; }}
 .wf {{ width:320px; height:44px; flex:0 0 auto; }}
 .wf path {{ stroke:var(--wave); stroke-width:1; vector-effect:non-scaling-stroke; }}
 .verdict {{ margin-top:.3rem; font-size:.85rem; }}
 .ok {{ color:var(--ok); }} .bad {{ color:var(--bad); }} .muted {{ color:var(--mut); }}
 code {{ font-family:ui-monospace,monospace; background:var(--line); padding:.1em .35em;
         border-radius:3px; }}
</style>
<h1>warband SFX audition</h1>
<p class="lead">Every baked clip against the <code>families.json</code> contract, A/B'd against its
source. Judge <em>before</em> vs <em>after</em>: if the bake makes a clip usable the source is fine
and only the processing was missing; if it still sounds wrong the family needs regenerating or
synthesising. See <code>docs/vault/Design/audio.md</code> §6.</p>
<p class="lead"><strong>Waveforms are absolute, not normalised</strong> — full height is 0&nbsp;dBFS,
and both clips in a pair share one time axis. The level and length changes are the visible ones.</p>
{''.join(rows)}
"""
    os.makedirs(WORK, exist_ok=True)
    with open(out, "w") as f:
        f.write(html)
    print(f"wrote {os.path.relpath(out, ROOT)}  ({os.path.getsize(out) / 1024:.0f} KB)")
    print(f"open: file://{out}")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("measure", help="raw numbers for any wav dir/file")
    p.add_argument("targets", nargs="+")
    p.set_defaults(fn=cmd_measure)

    p = sub.add_parser("lint", help="enforce families.json + cross-check tuning.json")
    p.add_argument("--dir", help="lint this dir instead of Resources (e.g. docs/audio/baked)")
    p.add_argument("--report-only", action="store_true", help="always exit 0")
    p.set_defaults(fn=cmd_lint)

    p = sub.add_parser("bake", help="docs/audio/src -> docs/audio/baked")
    p.set_defaults(fn=cmd_bake)

    p = sub.add_parser("sheet", help="write docs/audio/index.html")
    p.set_defaults(fn=cmd_sheet)

    p = sub.add_parser("density", help="sound onsets/sec per replay fixture")
    p.add_argument("fixtures", nargs="*")
    p.set_defaults(fn=cmd_density)

    args = ap.parse_args()
    sys.exit(args.fn(args))


if __name__ == "__main__":
    main()
