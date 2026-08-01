using System;
using System.Collections.Generic;
using UnityEngine;
using Warband.Sim;

/// <summary>
/// One live field, drawn as the three-layer ground language of combat-spectacle §4: a footprint
/// RIM (the authoritative extent), a scrolling floor FILL under it, and — for walls — the raised
/// slab. It replaces the flat repainted hex the board used to draw, which read as a solid orange
/// slab at any zoom and told you nothing about what the ground was doing.
///
/// Order of the sentence is the whole point: <b>boundary before body</b>. The rim traces itself
/// over <see cref="EdgeTraceSeconds"/> (Ring `_ArcFill` 0→1), and only then does the floor fade up
/// over FxTune.fieldSpawnSeconds. Idle is deliberately QUIET (T0, below the 1.1 bloom threshold,
/// ~fieldIdleAlpha opacity) — a field is terrain you read, not an effect that shouts; the pulse is
/// what shouts, and only when an event actually landed on those hexes.
///
/// Same laws as the rest of the FX runtime (fx-runtime.md): every animated shader input is an MPB
/// float written from <see cref="Step"/>, there is no Update/coroutine/Time.deltaTime below this
/// line, and nothing here uses Random — the hazard flicker hashes off the hex, so a frozen preview
/// reproduces a live frame by construction.
/// </summary>
public sealed class FieldView
{
    /// <summary>How long the rim takes to draw itself before the floor starts (combat-spectacle §4
    /// says 0.2 s; FxTune.fieldSpawnSeconds owns the floor half, which is the tunable one).</summary>
    private const float EdgeTraceSeconds = 0.2f;
    private const float PulseSeconds = 0.2f;
    /// <summary>The Ring sits on the hex's INRADIUS (cos 30°) inside a quad whose half-width is the
    /// circumradius, so neighbouring boundary hexes' rings very nearly touch and the perimeter reads
    /// as one chain rather than six loose circles.</summary>
    private const float RimRadius = 0.866f;
    /// <summary>Small: the floor must still read as a HEX (telegraph = hitbox), so the fill only
    /// softens its corners instead of rounding into a blob.</summary>
    private const float FillEdgeFade = 0.15f;
    private const float SlabHeight = 0.4f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int ArcFillId = Shader.PropertyToID("_ArcFill");
    private static readonly int RotationId = Shader.PropertyToID("_Rotation");

    /// <summary>Per-flavor character (§4). Color still comes from FieldTune so the F1 palette keeps
    /// owning meaning; this is only how that color BEHAVES.</summary>
    private struct Look
    {
        public float Scroll;     // floor phase per second — ember veins crawl, wax barely moves
        public float Spin;       // rim turns per second (Dread's walking chain-dash)
        public float Fill;       // GroundFill _Intensity
        public float Thickness;  // rim band width
        public float Arc;        // rim arc fill — <1 leaves a gap, which spinning reads as dashed
        public float Flicker;    // rim brightness wobble amplitude (hazard only)
    }

    private sealed class Piece
    {
        public Transform T;
        public Renderer R;
        public MaterialPropertyBlock Mpb;
        public Hex H;
    }

    private Transform _group;
    private bool _wall;
    private FieldFlavor _flavor;
    private Func<Hex, Vector3> _hexToWorld;
    private Func<TuningData> _tuning;
    private float _hexSize;
    private Mesh _slabMesh;
    private Action<Renderer, Color> _paintSlab;

    private readonly List<Hex> _hexes = new List<Hex>();
    private readonly List<Hex> _boundary = new List<Hex>();
    private readonly List<Piece> _fills = new List<Piece>();
    private readonly List<Piece> _rims = new List<Piece>();
    private readonly List<Piece> _slabs = new List<Piece>();

    private float _age, _pulse, _expireT;
    private bool _expiring;

    /// <summary>Paint this footprint in a color the FieldTune palette does not assign to any
    /// flavor. Deployment's muster rings borrow this view — they are the same three-layer ground
    /// sentence, but they are not sim fields and must not read as one.</summary>
    public Color? ColorOverride;

    /// <summary>Alpha scale on the RIM, 1 = the authored look.</summary>
    public float Emphasis = 1f;

    /// <summary>
    /// Alpha scale on the FLOOR FILL, applied on top of <see cref="Emphasis"/>.
    ///
    /// Split from the rim because deployment needs quiet and loud to differ in KIND, not in
    /// opacity. Several musters can overlap most of a deploy half at once; dimming their floors
    /// equally just makes one large dim wash where the boundaries used to be. Dropping the floor to
    /// zero leaves outlines, which stay legible however many of them cross.
    /// </summary>
    public float FillEmphasis = 1f;

    /// <summary>
    /// Optional identity channels for planning overlays. Combat fields leave these at their
    /// defaults; overlapping muster footprints use concentric radii plus different arc gaps so
    /// ownership does not depend on hue alone.
    /// </summary>
    public float RimRadiusScale = 1f;
    public float? RimArcOverride;
    public float RimRotationOffset;

    /// <summary><paramref name="startAge"/> is how long this field has ALREADY existed in FX
    /// seconds. Live playback passes ~0 (the field just appeared), but a frozen scrub builds every
    /// field the fold currently holds at once — without this, a capture at tick 60 would show a
    /// field created at tick 22 still tracing its rim.</summary>
    public static FieldView Create(Transform parent, int id, bool wall, FieldFlavor flavor,
                                   Func<Hex, Vector3> hexToWorld, Func<TuningData> tuning,
                                   float hexSize, Mesh slabMesh, Action<Renderer, Color> paintSlab,
                                   float startAge)
    {
        var v = new FieldView
        {
            _wall = wall,
            _flavor = flavor,
            _hexToWorld = hexToWorld,
            _tuning = tuning,
            _hexSize = hexSize,
            _slabMesh = slabMesh,
            _paintSlab = paintSlab,
            _age = Mathf.Max(0f, startAge),
        };
        v._group = new GameObject($"field_{id}").transform;
        v._group.SetParent(parent, false);
        return v;
    }

    /// <summary>Does this field cover a hex? The pulse router asks per impact, so it is a linear
    /// scan over at most a radius-2 footprint.</summary>
    public bool Covers(Hex h) => !_expiring && _hexes.Contains(h);

    /// <summary>An event landed on these hexes — flare the floor for <see cref="PulseSeconds"/>.</summary>
    public void Pulse() => _pulse = 1f;

    /// <summary>The fold dropped this field: fade out instead of vanishing. Idempotent, because
    /// SyncFields runs every frame and would otherwise restart the fade.</summary>
    public void BeginExpire()
    {
        if (_expiring) return;
        _expiring = true;
        _expireT = 0f;
    }

    public void Destroy()
    {
        if (_group != null) UnityEngine.Object.DestroyImmediate(_group.gameObject);
        _group = null;
    }

    /// <summary>Re-point the view at the hexes the fold says it covers. Cheap and idempotent on an
    /// unchanged footprint, which matters because an ATTACHED aura recomputes its hexes from a
    /// walking anchor every single SyncFields pass. A footprint change rebuilds the geometry but
    /// never touches <see cref="_age"/> — the aura must not replay its spawn-in every step.</summary>
    public void SetFootprint(List<Hex> hexes)
    {
        if (Same(hexes)) return;
        _hexes.Clear();
        _hexes.AddRange(hexes);
        Rebuild();
    }

    private bool Same(List<Hex> hexes)
    {
        if (hexes.Count != _hexes.Count) return false;
        for (int i = 0; i < hexes.Count; i++) if (!hexes[i].Equals(_hexes[i])) return false;
        return true;
    }

    /// <summary>Advance the FX clock; false once the expiry fade is done and the owner should
    /// destroy this view.</summary>
    public bool Step(float dt)
    {
        var data = _tuning();
        var fx = data != null ? data.fx : new FxTune();
        _age += dt;
        if (_pulse > 0f) _pulse = Mathf.Max(0f, _pulse - dt / PulseSeconds);
        if (_expiring)
        {
            _expireT += dt;
            if (_expireT >= Mathf.Max(0.01f, fx.fieldExpireSeconds)) return false;
        }
        Apply(data, fx);
        return true;
    }

    // ---- geometry ------------------------------------------------------------

    private void Rebuild()
    {
        var data = _tuning();
        float tileScale = data != null && data.board != null ? data.board.tileScale : 0.9f;
        float span = 2f * _hexSize * tileScale;   // the VfxLibrary meshes are unit-diameter

        var set = new HashSet<Hex>(_hexes);
        _boundary.Clear();
        foreach (var h in _hexes) if (IsBoundary(set, h)) _boundary.Add(h);

        // The VFX meshes live in the XY plane (they are authored for billboards) — this is the same
        // 90° flip onto the board that VfxInstance's Ground orientation applies.
        var flat = Quaternion.Euler(90f, 0f, 0f);

        // A wall is a prop, not a decal: it keeps the raised Lit slab it always had (the board's own
        // hex mesh, which already bakes hexSize), and only gains a rim strip at its base so its
        // footprint reads on the floor like every other field.
        if (_wall)
        {
            Place(_slabs, _hexes, "slab", _slabMesh, null,
                  new Vector3(tileScale, 1f, tileScale), Quaternion.identity, SlabHeight);
            PaintSlabs(data);
        }
        else
        {
            Place(_fills, _hexes, "fill", VfxLibrary.HexMesh,
                  VfxLibrary.MaterialFor(VfxLibrary.ShaderGroundFill, null),
                  new Vector3(span, span, span), flat, 0.03f);
        }

        Place(_rims, _boundary, "rim", VfxLibrary.QuadMesh,
              VfxLibrary.MaterialFor(VfxLibrary.ShaderRing, null),
              new Vector3(span, span, span), flat, 0.05f);

        // Pose the new pieces at the CURRENT age immediately (the VfxInstance.Begin law): a field
        // is built inside ApplyFold, and without this the frame between the build and the first
        // Step would render fresh quads at their raw material defaults — white rings on the board.
        Apply(data, data != null ? data.fx : new FxTune());
    }

    /// <summary>Grow/reuse/hide one layer's pieces over a hex list — the same pooling the old
    /// RebuildFieldTiles used, so an aura that shuffles one hex per step allocates nothing.</summary>
    private void Place(List<Piece> pool, List<Hex> hexes, string name, Mesh mesh, Material mat,
                       Vector3 scale, Quaternion rot, float height)
    {
        while (pool.Count < hexes.Count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_group, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (mat != null) mr.sharedMaterial = mat;
            pool.Add(new Piece { T = go.transform, R = mr, Mpb = new MaterialPropertyBlock() });
        }
        for (int i = 0; i < pool.Count; i++)
        {
            var p = pool[i];
            bool on = i < hexes.Count;
            if (p.T.gameObject.activeSelf != on) p.T.gameObject.SetActive(on);
            if (!on) continue;
            p.H = hexes[i];
            p.T.position = _hexToWorld(p.H) + new Vector3(0f, height, 0f);
            p.T.rotation = rot;
            p.T.localScale = scale;
        }
    }

    /// <summary>Repaint the wall slabs every pass — that is what makes a tuning.json hot-reload
    /// recolor a wall already standing on the board, exactly as the old flat tiles did.</summary>
    private void PaintSlabs(TuningData data)
    {
        if (_slabs.Count == 0 || _paintSlab == null) return;
        var wallColor = data != null ? data.fields.wall : new FieldTune().wall;
        for (int i = 0; i < _slabs.Count; i++)
            if (_slabs[i].T.gameObject.activeSelf) _paintSlab(_slabs[i].R, wallColor);
    }

    private static bool IsBoundary(HashSet<Hex> set, Hex h)
    {
        foreach (var d in Hex.Directions) if (!set.Contains(h + d)) return true;
        return false;
    }

    // ---- per-step look -------------------------------------------------------

    private void Apply(TuningData data, FxTune fx)
    {
        var look = LookFor(_flavor, _wall);
        Color color = ColorFor(data);

        float edge = Mathf.Clamp01(_age / EdgeTraceSeconds);
        float floor = Mathf.Clamp01((_age - EdgeTraceSeconds) / Mathf.Max(0.01f, fx.fieldSpawnSeconds));
        float fade = _expiring
            ? 1f - Mathf.Clamp01(_expireT / Mathf.Max(0.01f, fx.fieldExpireSeconds))
            : 1f;
        // The pulse is the field's one loud moment: the floor takes the full boost, the rim half of
        // it, so the flash reads as the GROUND lighting up rather than the outline thickening.
        float boost = 1f + (fx.fieldPulseBoost - 1f) * _pulse;
        float phase = _age * look.Scroll;

        float idle = fx.fieldIdleAlpha;
        for (int i = 0; i < _fills.Count; i++)
        {
            var p = _fills[i];
            if (!p.T.gameObject.activeSelf) continue;
            var c = color;
            c.a = idle * floor * fade * boost * Emphasis * FillEmphasis;
            p.Mpb.SetColor(ColorId, c);
            // Per-hex phase offset: every overlay carries the same unit UVs, so without this the
            // noise would run in lockstep across the footprint and the field would read as one
            // texture stamped N times instead of continuous ground.
            p.Mpb.SetFloat(PhaseId, phase + Hash01(p.H));
            p.Mpb.SetFloat(IntensityId, look.Fill);
            p.Mpb.SetFloat(EdgeFadeId, FillEdgeFade);
            p.R.SetPropertyBlock(p.Mpb);
        }

        float rimBoost = 1f + (boost - 1f) * 0.5f;
        float spin = look.Spin != 0f ? Mathf.Repeat(_age * look.Spin, 1f) : 0f;
        for (int i = 0; i < _rims.Count; i++)
        {
            var p = _rims[i];
            if (!p.T.gameObject.activeSelf) continue;
            var c = color;
            c.a = fx.fieldEdgeAlpha * fade * rimBoost * Flicker(look, p.H) * Emphasis;
            p.Mpb.SetColor(ColorId, c);
            p.Mpb.SetFloat(RadiusId, RimRadius * Mathf.Clamp(RimRadiusScale, 0.4f, 1f));
            p.Mpb.SetFloat(ThicknessId, look.Thickness);
            p.Mpb.SetFloat(SoftnessId, 0.12f);
            // The trace IS the arc filling in. Past the spawn it holds at the flavor's own arc, so
            // Debuff keeps a gap that its rotation turns into a walking dash.
            float arc = RimArcOverride.HasValue
                ? Mathf.Clamp01(RimArcOverride.Value)
                : look.Arc;
            p.Mpb.SetFloat(ArcFillId, Mathf.Min(edge, arc));
            p.Mpb.SetFloat(RotationId, Mathf.Repeat(spin + RimRotationOffset, 1f));
            p.R.SetPropertyBlock(p.Mpb);
        }

        // Lit slabs are opaque, so they can't fade with the rim — a wall pops out when its rim
        // finishes draining. Walls are structure, and the sim's walls outlive their fights anyway.
        PaintSlabs(data);
    }

    /// <summary>Hazard's edge is alive — ember veins never burn steady. Deterministic on purpose:
    /// the wobble hashes off the hex, so two captures of the same tick are the same pixels.</summary>
    private float Flicker(Look look, Hex h)
    {
        if (look.Flicker <= 0f) return 1f;
        return 1f + look.Flicker * Mathf.Sin(_age * 7f + Hash01(h) * (Mathf.PI * 2f));
    }

    /// <summary>A stable 0..1 per hex — the per-field variation the "no Random" law leaves us
    /// (the same trick the combat numbers use on unit ids).</summary>
    private static float Hash01(Hex h) =>
        (unchecked((uint)(h.Q * 73856093) ^ (uint)(h.R * 19349663)) % 1000u) / 1000f;

    private Color ColorFor(TuningData data)
    {
        if (ColorOverride.HasValue) return ColorOverride.Value;
        var f = data != null ? data.fields : new FieldTune();
        switch (_flavor)
        {
            case FieldFlavor.Hazard: return f.hazard;
            case FieldFlavor.Boon: return f.boon;
            case FieldFlavor.Buff: return f.buff;
            case FieldFlavor.Debuff: return f.debuff;
            default: return _wall ? f.wall : f.neutral;
        }
    }

    private static Look LookFor(FieldFlavor flavor, bool wall)
    {
        if (wall) return new Look { Fill = 0f, Thickness = 0.07f, Arc = 1f };
        switch (flavor)
        {
            // Burning ground: crawling ember veins under a flickery edge.
            case FieldFlavor.Hazard:
                return new Look { Scroll = 0.45f, Fill = 1.4f, Thickness = 0.11f, Arc = 1f, Flicker = 0.3f };
            // Grace: a candle-wax pool — thin unbroken warm ring, floor barely moving.
            case FieldFlavor.Boon:
                return new Look { Scroll = 0.10f, Fill = 0.9f, Thickness = 0.06f, Arc = 1f };
            case FieldFlavor.Buff:
                return new Look { Scroll = 0.12f, Fill = 0.9f, Thickness = 0.06f, Arc = 1f };
            // Dread: ash-violet swirl + a chain-dash ring that walks with its anchor. The "dash" is
            // one rotating gap rather than real dashes — the shader has an arc, not a pattern, and
            // at board zoom a turning break reads the same.
            case FieldFlavor.Debuff:
                return new Look { Scroll = 0.18f, Spin = 0.08f, Fill = 1.0f, Thickness = 0.08f, Arc = 0.88f };
            default:
                return new Look { Scroll = 0.10f, Fill = 0.8f, Thickness = 0.06f, Arc = 1f };
        }
    }
}
