using System;
using System.Collections.Generic;
using UnityEngine;
using Warband.Sim;

/// <summary>
/// The status roster over one unit's head (combat-spectacle §5), replacing the row of white pip
/// cubes. A pip told you a status EXISTED; this says which one, how many, and how long is left.
///
/// Ship-first, zero assets: six procedural family glyphs (hourglass control / flame DoT / chevrons
/// up+down / cross life / disc misc) drawn with the Warband/Sigil shader, whose white default mask
/// makes the MESH the silhouette. Color still comes from ReplayPlayer's StatusColor map — that map
/// stays the single color authority, passed in as a delegate.
///
/// Reading order is the point (§5): control (Stun/Taunt/Phase/Root) sits leftmost at 1.2× and is
/// never the icon that gets cut, then transformative windows, then counted engines, then the rest.
/// Ties break on the StatusKind ordinal, so the row is a pure function of the fold — two captures
/// of the same tick lay out identically.
///
/// Same laws as the rest of the FX runtime (fx-runtime.md): fold-driven truth (the row re-syncs
/// only when the status multiset actually changes), one clock via <see cref="Step"/>, every
/// animated shader input written from that clock through an MPB, no Update/coroutine/Time.*, and
/// no Random anywhere.
/// </summary>
public sealed class StatusIconRow
{
    /// <summary>A control status is authored bigger — §5's "leftmost, 1.2×, never hidden".</summary>
    private const float ControlScale = 1.2f;
    /// <summary>Control reads a touch hotter than the rest of the roster, still under bloom (1.1).</summary>
    private const float ControlGlow = 1.15f;
    /// <summary>Clock quad size in icon widths. The Ring shader's _Radius is in half-quad units, so
    /// the band lands at 0.5 × RingScale × _Radius = 0.56 of an icon width from the centre — just
    /// outside the widest glyph (the flame, at 0.50) and still inside the row gap.</summary>
    private const float RingScale = 1.55f;
    private const float RingRadius = 0.72f;
    private const float RingThickness = 0.16f;
    private const float PopSeconds = 0.15f;
    private const float PopAmount = 0.45f;
    private const float ChipWidth = 0.95f;   // "+N" chip width, in icon sizes
    private const int MaxDots = 3;
    /// <summary>Crisp-text law: a high fontSize glyph texture scaled down by a small characterSize.
    /// Both are expressed per icon size so the labels track fx.statusIconSize.</summary>
    private const int LabelFontSize = 180;
    private const float CountCharSize = 0.026f;
    private const float ChipCharSize = 0.036f;
    /// <summary>The active-rule badge is a literal state read, not another transient effect.
    /// Text + diamond means it survives color-vision differences and a still capture.</summary>
    private const float LiveCharSize = 0.022f;
    private const float LivePopSeconds = 0.18f;
    private const float LivePopAmount = 0.24f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int ArcFillId = Shader.PropertyToID("_ArcFill");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");
    /// <summary>The remaining arc starts at 12 o'clock (the shader's angle 0 is at -X).</summary>
    private const float RingPhase = 0.75f;

    private static readonly FxTune FallbackFx = new FxTune();

    private enum Glyph { Hourglass, Flame, ChevronUp, ChevronDown, Cross, Disc }

    /// <summary>One kind's worth of row: the glyph, its countdown clock, its stack read.</summary>
    private sealed class Slot
    {
        public Transform T;                  // pop container — scaling this scales glyph+ring+labels
        public MeshFilter Filter;
        public MeshRenderer Glyph;
        public MeshRenderer Ring;
        public MaterialPropertyBlock GlyphMpb, RingMpb;
        public MeshRenderer[] Dots;
        public TextMesh Count;
        public StatusKind Kind;
        public float PopT;
        public int Expiry;                   // -1 = permanent/swing-scoped → no ring
        public float Total;                  // ticks the ring spans, from first sight
    }

    /// <summary>One kind, folded: how many entries carry it, what it is worth, when it runs out.</summary>
    private struct Entry
    {
        public StatusKind Kind;
        public int Count;
        public int Value;    // Burn shows its decay pool instead of an entry count
        public int Expiry;
    }

    private Transform _group;
    private Font _font;
    private Func<StatusKind, Color> _color;
    private float _baseY;

    private readonly List<Slot> _slots = new List<Slot>();
    private TextMesh _chip;
    private MeshRenderer _plate;
    private Transform _live;
    private TextMesh _liveLabel;
    private MeshRenderer _livePlate;
    private float _livePopT;
    private int _liveCountSig = -1;

    // Retained scratch — the sync path allocates nothing after the first rebuild.
    private readonly List<(StatusKind Kind, int Mag, int ExpiryTick)> _cache =
        new List<(StatusKind, int, int)>();
    private readonly List<Entry> _agg = new List<Entry>();
    private static readonly Comparison<Entry> ByPriority =
        (a, b) => Tier(a.Kind) != Tier(b.Kind) ? Tier(a.Kind) - Tier(b.Kind) : (int)a.Kind - (int)b.Kind;

    /// <summary>First-sight span per kind: (expiry, total ticks). A scrub that lands mid-status has
    /// no apply tick on the wire, so the ring calls what it finds a FULL ring and drains from there
    /// — decoration, deliberately simple, and reset on a loop wrap. Append-only within a fight:
    /// expiry ticks only move forward, so a re-application always writes a new span.</summary>
    private readonly Dictionary<StatusKind, (int Expiry, float Total)> _spans =
        new Dictionary<StatusKind, (int, float)>();

    private float _clock, _tps = 10f;
    private float _sizeSig, _gapSig;
    private int _capSig;

    public static StatusIconRow Create(Transform parent, float baseY, Font font,
                                       Func<StatusKind, Color> statusColor)
    {
        var row = new StatusIconRow { _font = font, _color = statusColor, _baseY = baseY };
        row._group = new GameObject("statusicons").transform;
        row._group.SetParent(parent, false);
        row._group.localPosition = new Vector3(0f, baseY, 0f);
        return row;
    }

    /// <summary>Billboard toward the camera. Called from the same per-frame pass that styles the
    /// nameplate, so play mode and a frozen capture face the row identically.</summary>
    public void FaceCamera(Quaternion face) => _group.rotation = face;

    /// <summary>A status just landed — pop its icon. Fired by the Director at a StatusApplied tell's
    /// impact (never from the fold), which is what makes the pop read as the moment it happened. A
    /// kind that is not on the row right now (capped away, or already gone) is simply ignored.</summary>
    public void Pop(StatusKind kind)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].T.gameObject.activeSelf && _slots[i].Kind == kind) { _slots[i].PopT = 1f; return; }
    }

    /// <summary>Loop wrap / scenario switch: clear the row outright — pops, countdown spans, and the
    /// icons themselves. Hiding here rather than leaving it to the next Sync matters because a unit
    /// that wraps back to NO statuses has an unchanged (empty) multiset, so nothing would rebuild
    /// and the second loop would open wearing the first loop's roster.</summary>
    public void Reset()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].PopT = 0f;
            if (_slots[i].T.gameObject.activeSelf) _slots[i].T.gameObject.SetActive(false);
        }
        if (_chip != null) _chip.gameObject.SetActive(false);
        if (_plate != null) _plate.gameObject.SetActive(false);
        if (_live != null) _live.gameObject.SetActive(false);
        _livePopT = 0f;
        _liveCountSig = -1;
        _spans.Clear();
        _cache.Clear();
        _agg.Clear();
    }

    /// <summary>Fold-driven truth. Runs on EVERY ApplyFold, so the unchanged path — which is almost
    /// every frame — has to be an element-wise compare and nothing else.</summary>
    public void Sync(PlaybackUnit u, float clock, float ticksPerSecond, FxTune fx)
    {
        if (fx == null) fx = FallbackFx;
        _clock = clock;
        _tps = Mathf.Max(0.01f, ticksPerSecond);
        int liveCount = u.ActiveRules != null ? u.ActiveRules.Count : 0;
        if (Same(u.Statuses) && _sizeSig == fx.statusIconSize && _gapSig == fx.statusIconGap
            && _capSig == fx.statusIconCap && _liveCountSig == liveCount) return;

        _cache.Clear();
        _cache.AddRange(u.Statuses);
        _sizeSig = fx.statusIconSize; _gapSig = fx.statusIconGap; _capSig = fx.statusIconCap;
        if (_liveCountSig != liveCount && liveCount > 0) _livePopT = 1f;
        _liveCountSig = liveCount;

        Aggregate();
        Layout(fx);
        LayoutLive(fx, liveCount);
        Apply();   // pose at the current clock immediately — a row built inside ApplyFold must not
                   // render one frame of raw material defaults before the first Step (FieldView law)
    }

    private bool Same(List<(StatusKind Kind, int Mag, int ExpiryTick)> statuses)
    {
        if (statuses.Count != _cache.Count) return false;
        for (int i = 0; i < statuses.Count; i++)
        {
            var a = statuses[i]; var b = _cache[i];
            if (a.Kind != b.Kind || a.Mag != b.Mag || a.ExpiryTick != b.ExpiryTick) return false;
        }
        return true;
    }

    /// <summary>Advance the one FX clock: drain the countdown rings and run out any pops. Called
    /// from ReplayPlayer.StepFx, so BuildPreview's fixed 0.01 s loop drains a ring exactly as live
    /// play does — two captures of the same tick at different advances differ by construction.</summary>
    public void Step(float dt)
    {
        if (_slots.Count == 0 && (_live == null || !_live.gameObject.activeSelf)) return;
        _clock += dt * _tps;
        if (_livePopT > 0f)
            _livePopT = Mathf.Max(0f, _livePopT - dt / LivePopSeconds);
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.T.gameObject.activeSelf && s.PopT > 0f)
                s.PopT = Mathf.Max(0f, s.PopT - dt / PopSeconds);
        }
        Apply();
    }

    // ---- fold → row ----------------------------------------------------------

    /// <summary>Fold entries → one row entry per kind. Stacks are the entry COUNT, except Burn,
    /// which the sim merges into a single decaying pool — its magnitude IS the stack read (§5,
    /// and the reason P0 had to make that pool honest on the wire).</summary>
    private void Aggregate()
    {
        _agg.Clear();
        for (int i = 0; i < _cache.Count; i++)
        {
            var s = _cache[i];
            // Mustered is a roster tag, not a combat state: every unit carrying it also carries the
            // muster's own permanent Haste from the same trigger, so a second fight-long pip on the
            // same bodies costs a row slot and says nothing the first one didn't.
            if (s.Kind == StatusKind.Mustered) continue;
            int at = -1;
            for (int j = 0; j < _agg.Count; j++) if (_agg[j].Kind == s.Kind) { at = j; break; }
            if (at < 0)
            {
                _agg.Add(new Entry
                {
                    Kind = s.Kind,
                    Count = 1,
                    Value = s.Kind == StatusKind.Burn ? s.Mag : 1,
                    Expiry = s.ExpiryTick,
                });
                continue;
            }
            var e = _agg[at];
            e.Count++;
            // Longest remaining wins the clock: the icon leaves when the LAST entry of its kind does.
            if (s.ExpiryTick > e.Expiry) e.Expiry = s.ExpiryTick;
            e.Value = s.Kind == StatusKind.Burn ? s.Mag : e.Count;
            _agg[at] = e;
        }
        _agg.Sort(ByPriority);
    }

    /// <summary>Reading priority, combat-spectacle §5. Control first because it is the only tier
    /// that changes what the unit can DO; counted engines next-to-last because their number, not
    /// their presence, is the news.</summary>
    private static int Tier(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Stun: case StatusKind.Taunt: case StatusKind.Phase: case StatusKind.Root:
                return 0;
            case StatusKind.Frenzied: case StatusKind.MultiShotWindow:
            case StatusKind.NextSwingCrit: case StatusKind.CheatDeath:
                return 1;
            case StatusKind.Burn: case StatusKind.MultiShotRamp:
            case StatusKind.AttackUp: case StatusKind.CritUp:
                return 2;
            default:
                return 3;
        }
    }

    /// <summary>
    /// The inspector already names every passive; this is the board-level binary answer it lacked:
    /// at least one conditional rule is online RIGHT NOW. The fold's ActiveRules list is authority,
    /// so a scrub, live replay, and capture all show the same state without re-running a predicate.
    /// It owns a lane just below mana, where it stays attached to the body without consuming a
    /// status slot, covering a health read, or pretending to be a timed status.
    /// </summary>
    private void LayoutLive(FxTune fx, int count)
    {
        if (count <= 0)
        {
            if (_live != null) _live.gameObject.SetActive(false);
            return;
        }

        EnsureLive();
        if (!_live.gameObject.activeSelf) _live.gameObject.SetActive(true);
        float size = Mathf.Max(0.02f, fx.statusIconSize);
        // The row anchor is 0.17 world-units above HP at the default tune — not enough room for
        // another 0.12-high plate. Give LIVE its own lane below mana instead of painting it over
        // the health read. Because the anchor inherits each chassis' bar offset, the lane follows
        // tall silhouettes without a second model-specific table.
        _live.localPosition = new Vector3(0f, -size * 1.95f, 0f);

        string text = count > 1 ? $"◆ LIVE ×{count}" : "◆ LIVE";
        _liveLabel.text = text;
        _liveLabel.color = new Color(0.95f, 0.76f, 0.31f);
        _liveLabel.characterSize = size * LiveCharSize;
        _liveLabel.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        if (!_liveLabel.gameObject.activeSelf) _liveLabel.gameObject.SetActive(true);

        float width = size * (count > 1 ? 2.25f : 1.70f);
        _livePlate.transform.localScale = new Vector3(width, size * 0.54f, 1f);
        ApplyLive();
    }

    private void EnsureLive()
    {
        if (_live != null) return;
        _live = new GameObject("rule-live").transform;
        _live.SetParent(_group, false);

        _livePlate = Quad("plate", _live, VfxLibrary.QuadMesh,
                          VfxLibrary.MaterialFor(VfxLibrary.ShaderGroundFill, null), 0.03f);
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor(ColorId, new Color(0.03f, 0.035f, 0.05f, 0.72f));
        _livePlate.SetPropertyBlock(mpb);

        _liveLabel = Label("label", _live, TextAnchor.MiddleCenter);
        _live.gameObject.SetActive(false);
    }

    private void ApplyLive()
    {
        if (_live == null || !_live.gameObject.activeSelf) return;
        float pop = 1f + LivePopAmount * Mathf.Clamp01(_livePopT);
        _live.localScale = new Vector3(pop, pop, 1f);
    }

    /// <summary>Family glyph per kind. The hourglass carries every control status by law (§5: "the
    /// Last Hour owns control") — Phase is the exception, priced as control for READING order but
    /// drawn as the ghost disc, because it is a state the unit is in, not a leash on it.</summary>
    private static Glyph GlyphFor(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Stun: case StatusKind.Root: case StatusKind.Taunt:
            case StatusKind.Silence: case StatusKind.Disarm: case StatusKind.Slow:
                return Glyph.Hourglass;
            case StatusKind.Burn: case StatusKind.Dot: case StatusKind.BurnAmp:
                return Glyph.Flame;
            case StatusKind.Regen: case StatusKind.CheatDeath: case StatusKind.OverhealToShield:
                return Glyph.Cross;
            case StatusKind.AttackDown: case StatusKind.DamageTakenUp:
                return Glyph.ChevronDown;
            case StatusKind.Phase: case StatusKind.Mark:
                return Glyph.Disc;
            default:
                return Glyph.ChevronUp;   // every remaining kind is something going UP for its owner
        }
    }

    // ---- layout --------------------------------------------------------------

    private void Layout(FxTune fx)
    {
        float size = Mathf.Max(0.02f, fx.statusIconSize);
        float gap = Mathf.Max(0f, fx.statusIconGap);
        int cap = Mathf.Max(1, fx.statusIconCap);
        int shown = Mathf.Min(_agg.Count, cap);
        int hidden = _agg.Count - shown;

        // Lift the row clear of the HP bar by half an icon: the count dots hang below each glyph,
        // and the pips anchor was authored for a 0.1-tall cube.
        _group.localPosition = new Vector3(0f, _baseY + size * 0.45f, 0f);

        float width = gap * Mathf.Max(0, shown - 1 + (hidden > 0 ? 1 : 0));
        for (int i = 0; i < shown; i++) width += size * SlotScale(_agg[i].Kind);
        if (hidden > 0) width += size * ChipWidth;

        // Figure-ground plate (ui-review unit-hud-readability P6): one dark translucent quad
        // behind the whole row — the colored glyphs were blending into VFX behind them on busy
        // boards. Sized to the shown slots (+N chip included via width) plus the count dots'
        // descent below the glyph line; hidden outright when the row is empty.
        if (shown > 0)
        {
            var plate = Plate();
            if (!plate.gameObject.activeSelf) plate.gameObject.SetActive(true);
            plate.transform.localScale = new Vector3(width + size * 0.35f, size * 1.75f, 1f);
            plate.transform.localPosition = new Vector3(0f, -size * 0.12f, 0.03f);
        }
        else if (_plate != null && _plate.gameObject.activeSelf) _plate.gameObject.SetActive(false);

        float x = -width * 0.5f;
        for (int i = 0; i < shown; i++)
        {
            var e = _agg[i];
            var slot = SlotAt(i);
            float w = size * SlotScale(e.Kind);
            bool control = Tier(e.Kind) == 0;

            if (slot.Kind != e.Kind) slot.PopT = 0f;   // this slot now shows a different status
            slot.Kind = e.Kind;
            slot.Expiry = e.Expiry;
            slot.Total = SpanFor(e.Kind, e.Expiry);

            slot.T.gameObject.SetActive(true);
            slot.T.localPosition = new Vector3(x + w * 0.5f, 0f, 0f);
            slot.Filter.sharedMesh = GlyphMesh(GlyphFor(e.Kind));
            slot.Glyph.transform.localScale = new Vector3(w, w, 1f);

            Color c = _color(e.Kind);
            if (control) c *= ControlGlow;
            c.a = 1f;
            slot.GlyphMpb.SetColor(ColorId, c);
            slot.Glyph.SetPropertyBlock(slot.GlyphMpb);

            bool ring = e.Expiry > 0;
            var rt = slot.Ring.transform;
            if (rt.gameObject.activeSelf != ring) rt.gameObject.SetActive(ring);
            if (ring)
            {
                rt.localScale = new Vector3(w * RingScale, w * RingScale, 1f);
                var rc = c; rc.a = 0.85f;
                slot.RingMpb.SetColor(ColorId, rc);
                slot.RingMpb.SetFloat(RadiusId, RingRadius);
                slot.RingMpb.SetFloat(ThicknessId, RingThickness);
                slot.RingMpb.SetFloat(SoftnessId, 0.10f);
                slot.RingMpb.SetFloat(RotationId, RingPhase);
            }

            // Stack read: a couple of stacks are dots you count at a glance, four or more (and any
            // Burn pool) is a number, because past three the dots stop being countable.
            int stacks = e.Value;
            bool label = stacks >= 4 || (e.Kind == StatusKind.Burn && stacks >= 2);
            int dots = label || stacks < 2 ? 0 : Mathf.Min(MaxDots, stacks);
            for (int d = 0; d < MaxDots; d++)
            {
                var dot = slot.Dots[d];
                bool on = d < dots;
                if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
                if (!on) continue;
                dot.transform.localPosition = new Vector3((d - (dots - 1) * 0.5f) * w * 0.28f, -w * 0.62f, 0f);
                dot.transform.localScale = new Vector3(w * 0.16f, w * 0.16f, 1f);
                dot.SetPropertyBlock(slot.GlyphMpb);
            }
            // Bottom-right of the glyph (§5), tucked so its descent still clears the HP bar below
            // and its width stays inside the row gap rather than crowding the next icon.
            SetLabel(slot.Count, label ? "x" + stacks : null, c,
                     new Vector3(w * 0.36f, -w * 0.36f, -0.01f), size * CountCharSize);

            x += w + gap;
        }
        for (int i = shown; i < _slots.Count; i++)
            if (_slots[i].T.gameObject.activeSelf) _slots[i].T.gameObject.SetActive(false);

        if (hidden > 0)
            SetLabel(Chip(), "+" + hidden, new Color(0.80f, 0.82f, 0.86f),
                     new Vector3(x + size * ChipWidth * 0.5f, 0f, -0.01f), size * ChipCharSize);
        else if (_chip != null && _chip.gameObject.activeSelf) _chip.gameObject.SetActive(false);
    }

    private static float SlotScale(StatusKind k) => Tier(k) == 0 ? ControlScale : 1f;

    /// <summary>Ticks this kind's clock spans, remembered from the first frame the row saw it.</summary>
    private float SpanFor(StatusKind kind, int expiry)
    {
        if (expiry <= 0) return 0f;
        if (_spans.TryGetValue(kind, out var sp) && sp.Expiry == expiry) return sp.Total;
        float total = Mathf.Max(1f, expiry - _clock);
        _spans[kind] = (expiry, total);
        return total;
    }

    // ---- per-step look -------------------------------------------------------

    /// <summary>Write the two things that move: the ring's remaining arc and the pop scale.</summary>
    private void Apply()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (!s.T.gameObject.activeSelf) continue;
            float pop = 1f + PopAmount * Mathf.Clamp01(s.PopT);
            s.T.localScale = new Vector3(pop, pop, 1f);
            if (!s.Ring.gameObject.activeSelf) continue;
            float left = s.Total > 0f ? (s.Expiry - _clock) / s.Total : 0f;
            s.RingMpb.SetFloat(ArcFillId, Mathf.Clamp01(left));
            s.Ring.SetPropertyBlock(s.RingMpb);
        }
        ApplyLive();
    }

    // ---- construction --------------------------------------------------------

    private Slot SlotAt(int i)
    {
        while (_slots.Count <= i)
        {
            var slot = new Slot
            {
                GlyphMpb = new MaterialPropertyBlock(),
                RingMpb = new MaterialPropertyBlock(),
                Dots = new MeshRenderer[MaxDots],
            };
            var go = new GameObject("icon");
            go.transform.SetParent(_group, false);
            slot.T = go.transform;

            // TODO(icon atlas): the sprite-atlas upgrade swaps ONLY this pair — a StatusAtlas.cs
            // kind→cell lookup feeding VfxLibrary.QuadMesh plus the Resources/Board/Icons/
            // StatusAtlas.png mask (and its cell UVs) into this same renderer. Layout, color,
            // countdown and stack logic are already atlas-ready; the glyph meshes are the
            // zero-asset fallback that ships first.
            slot.Glyph = Quad("glyph", slot.T, GlyphMesh(Glyph.Disc),
                              VfxLibrary.MaterialFor(VfxLibrary.ShaderSigil, null), 0f);
            slot.Filter = slot.Glyph.GetComponent<MeshFilter>();
            slot.Ring = Quad("clock", slot.T, VfxLibrary.QuadMesh,
                             VfxLibrary.MaterialFor(VfxLibrary.ShaderRing, null), 0.01f);
            for (int d = 0; d < MaxDots; d++)
            {
                var dot = Quad("dot", slot.T, GlyphMesh(Glyph.Disc),
                               VfxLibrary.MaterialFor(VfxLibrary.ShaderSigil, null), 0f);
                dot.gameObject.SetActive(false);
                slot.Dots[d] = dot;
            }
            slot.Count = Label("count", slot.T, TextAnchor.MiddleCenter);
            _slots.Add(slot);
        }
        return _slots[i];
    }

    private TextMesh Chip()
    {
        if (_chip == null) _chip = Label("chip", _group, TextAnchor.MiddleCenter);
        return _chip;
    }

    /// <summary>Row backing, built once per row like the chip. GroundFill, NOT Sigil/Ring: those
    /// are additive (Blend One One) and a dark additive quad adds nothing — the plate must
    /// alpha-blend to darken. GroundFill's unset noise defaults to white (uniform fill), _Phase
    /// stays 0 (no crawl), and its radial edge fade rounds the quad into a soft pill for free.</summary>
    private MeshRenderer Plate()
    {
        if (_plate == null)
        {
            _plate = Quad("plate", _group, VfxLibrary.QuadMesh,
                          VfxLibrary.MaterialFor(VfxLibrary.ShaderGroundFill, null), 0.03f);
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ColorId, new Color(0.03f, 0.035f, 0.05f, 0.55f));
            _plate.SetPropertyBlock(mpb);
        }
        return _plate;
    }

    private static MeshRenderer Quad(string name, Transform parent, Mesh mesh, Material mat, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sharedMaterial = mat;
        return mr;
    }

    private TextMesh Label(string name, Transform parent, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tm = go.AddComponent<TextMesh>();
        tm.font = _font;
        go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        tm.anchor = anchor;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.fontSize = LabelFontSize;   // crisp text: big glyph texture, small characterSize
        go.SetActive(false);
        return tm;
    }

    private static void SetLabel(TextMesh tm, string text, Color color, Vector3 pos, float charSize)
    {
        bool on = !string.IsNullOrEmpty(text);
        if (tm.gameObject.activeSelf != on) tm.gameObject.SetActive(on);
        if (!on) return;
        tm.text = text;
        tm.color = color;
        tm.characterSize = charSize;
        tm.transform.localPosition = pos;
    }

    // ---- glyph meshes --------------------------------------------------------

    private static Mesh[] _glyphs;

    /// <summary>Built on first use and shared by every unit on the board. Each is a triangle fan
    /// from the centre over an outline — every family shape is star-shaped about its own centre, so
    /// the fan is a valid triangulation and the whole set costs one mesh each. Rebuilt per-glyph on
    /// a null, because these are DontSave objects: leaving play mode destroys them while the static
    /// array survives, and an un-guarded cache would hand out destroyed meshes in edit mode.</summary>
    private static Mesh GlyphMesh(Glyph g)
    {
        if (_glyphs == null) _glyphs = new Mesh[6];
        var mesh = _glyphs[(int)g];
        if (mesh != null) return mesh;
        switch (g)
        {
            // The Last Hour owns control (§5): every leash on a unit is an hourglass.
            case Glyph.Hourglass:
                mesh = Fan("~icon_hourglass", new[]
                {
                    new Vector2(-0.34f, 0.48f), new Vector2(0.34f, 0.48f),
                    new Vector2(0.30f, 0.36f), new Vector2(0.07f, 0f),
                    new Vector2(0.30f, -0.36f), new Vector2(0.34f, -0.48f),
                    new Vector2(-0.34f, -0.48f), new Vector2(-0.30f, -0.36f),
                    new Vector2(-0.07f, 0f), new Vector2(-0.30f, 0.36f),
                });
                break;
            case Glyph.Flame:
                mesh = Fan("~icon_flame", new[]
                {
                    new Vector2(0.00f, 0.50f), new Vector2(0.18f, 0.16f), new Vector2(0.32f, -0.08f),
                    new Vector2(0.26f, -0.36f), new Vector2(0.10f, -0.48f), new Vector2(-0.10f, -0.48f),
                    new Vector2(-0.26f, -0.36f), new Vector2(-0.32f, -0.08f), new Vector2(-0.18f, 0.16f),
                });
                break;
            case Glyph.ChevronUp: mesh = Fan("~icon_chev_up", Chevron(1f)); break;
            case Glyph.ChevronDown: mesh = Fan("~icon_chev_dn", Chevron(-1f)); break;
            case Glyph.Cross:
                mesh = Fan("~icon_cross", new[]
                {
                    new Vector2(0.48f, 0.16f), new Vector2(0.16f, 0.16f), new Vector2(0.16f, 0.48f),
                    new Vector2(-0.16f, 0.48f), new Vector2(-0.16f, 0.16f), new Vector2(-0.48f, 0.16f),
                    new Vector2(-0.48f, -0.16f), new Vector2(-0.16f, -0.16f), new Vector2(-0.16f, -0.48f),
                    new Vector2(0.16f, -0.48f), new Vector2(0.16f, -0.16f), new Vector2(0.48f, -0.16f),
                });
                break;
            default: mesh = Fan("~icon_disc", Circle(16, 0.44f)); break;
        }
        _glyphs[(int)g] = mesh;
        return mesh;
    }

    private static Vector2[] Chevron(float dir)
    {
        var p = new[]
        {
            new Vector2(-0.46f, -0.04f), new Vector2(0f, 0.42f), new Vector2(0.46f, -0.04f),
            new Vector2(0.46f, -0.30f), new Vector2(0f, -0.16f), new Vector2(-0.46f, -0.30f),
        };
        if (dir < 0f) for (int i = 0; i < p.Length; i++) p[i].y = -p[i].y;
        return p;
    }

    private static Vector2[] Circle(int n, float r)
    {
        var p = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.PI * 2f * i / n;
            p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
        return p;
    }

    /// <summary>Outline → fan mesh in the XY plane, UVs mapped into the unit square so the atlas
    /// upgrade can drop a masked quad in with no layout change.</summary>
    private static Mesh Fan(string name, Vector2[] outline)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        int n = outline.Length;
        var verts = new Vector3[n + 1];
        var uvs = new Vector2[n + 1];
        var norms = new Vector3[n + 1];
        verts[0] = Vector3.zero; uvs[0] = new Vector2(0.5f, 0.5f); norms[0] = -Vector3.forward;
        for (int i = 0; i < n; i++)
        {
            verts[i + 1] = new Vector3(outline[i].x, outline[i].y, 0f);
            uvs[i + 1] = new Vector2(outline[i].x + 0.5f, outline[i].y + 0.5f);
            norms[i + 1] = -Vector3.forward;
        }
        var tris = new int[n * 3];
        for (int i = 0; i < n; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + (i + 1) % n;
        }
        mesh.vertices = verts; mesh.uv = uvs; mesh.normals = norms; mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
