using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Warband.Sim;

/// <summary>
/// Replay renderer. Consumes ONLY (initial snapshot, event log) via the Warband.Sim
/// PlaybackState fold — zero combat logic client-side (render-contract.md). Play to animate
/// at 10 ticks/sec; or call BuildPreview(tick) from the editor to scrub to a frozen tick.
/// All presentation feel comes from a TuningConfig (JSON source of truth) → hot-reloadable.
/// </summary>
[ExecuteAlways]
public class ReplayPlayer : MonoBehaviour
{
    [Header("Playback")]
    public string replayFile = "replay.bytes";
    public float ticksPerSecond = 10f;
    public bool loop = true;

    [Header("Layout")]
    public float hexSize = 1f;

    private List<PlaybackUnit> _initial = new List<PlaybackUnit>();
    private List<BattleEvent> _events = new List<BattleEvent>();
    private PlaybackState _fold;
    private int _endTick, _fxCursor, _lastPreviewTick = 60;
    private float _clock;
    private bool _playing;

    private readonly Dictionary<int, UnitView> _views = new Dictionary<int, UnitView>();
    private readonly Dictionary<int, GameObject> _fieldTiles = new Dictionary<int, GameObject>();
    private readonly Stack<FloatingNumber> _numberPool = new Stack<FloatingNumber>();
    private Transform _generated;
    private FeedbackDirector _director;
    private Mesh _hexMesh;
    private Font _font;
    private Quaternion _numberFace = Quaternion.identity;
    private TuningConfig _tuning;
    private TuningData _data = new TuningData();

    private static readonly Color Team0 = new Color(0.30f, 0.55f, 0.95f);
    private static readonly Color Team1 = new Color(0.90f, 0.35f, 0.30f);
    private static readonly Color TileNeutral = new Color(0.22f, 0.23f, 0.27f);
    private static readonly Color TileTeam0 = new Color(0.20f, 0.26f, 0.36f);
    private static readonly Color TileTeam1 = new Color(0.34f, 0.24f, 0.26f);
    private static readonly Color BaseDark = new Color(0.05f, 0.055f, 0.07f);
    private static readonly Color AuraTile = new Color(0.95f, 0.80f, 0.35f);
    private static readonly Color WallTile = new Color(0.55f, 0.55f, 0.60f);

    private sealed class UnitView
    {
        public Transform Root, Body;
        public Renderer BodyRenderer;
        public Transform HpFill, ShieldFill, ManaFill, Pips;
        public int MaxHp, ManaMax;
        public Color TeamColor;
        public Vector3 BodyBaseScale, Target;
        public float FlashT, FlashDur = 0.2f, PunchT, PunchDur = 0.18f, PunchAmt;
        public Color FlashColor = Color.white;
        private MaterialPropertyBlock _mpb;

        public void ApplyVisual()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            BodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", Color.Lerp(TeamColor, FlashColor, Mathf.Clamp01(FlashT)));
            BodyRenderer.SetPropertyBlock(_mpb);
            Body.localScale = BodyBaseScale * (1f + Mathf.Clamp01(PunchT) * PunchAmt);
        }
    }

    /// <summary>Data-driven event → tell, built from TuningData.tells (JSON). No code per tell.
    /// Each event fires the MOST SPECIFIC matching tell (see Warband.Sim.TellMatch): a filterless
    /// tell is the fallback, a "cause: Burn" / "status: Taunt" tell overrides it for that signature.</summary>
    private sealed class FeedbackDirector
    {
        private readonly Dictionary<int, UnitView> _views;
        private readonly List<TellDef> _tells = new List<TellDef>();
        private readonly Action<Vector3, string, Color, float> _spawnNumber;

        public FeedbackDirector(Dictionary<int, UnitView> views, TuningData data, Action<Vector3, string, Color, float> spawnNumber)
        {
            _views = views; _spawnNumber = spawnNumber;
            if (data?.tells != null) _tells.AddRange(data.tells);
        }

        public void Handle(BattleEvent e)
        {
            TellDef best = null;
            int bestSpec = -1;
            foreach (var def in _tells)
            {
                if (!TellMatch.Matches(e, def.eventKind, def.CauseFilter, def.StatusFilter)) continue;
                if (def.Specificity > bestSpec) { best = def; bestSpec = def.Specificity; }
            }
            if (best == null) return;

            int uid = best.side == FeedbackSide.Source ? e.Source : e.Target;
            if (!_views.TryGetValue(uid, out var v)) return;
            if (best.flash) { v.FlashColor = e.Crit ? best.critFlashColor : best.flashColor; v.FlashT = 1f; v.FlashDur = best.flashSeconds; }
            if (best.punch) { v.PunchT = 1f; v.PunchDur = best.punchSeconds; v.PunchAmt = best.punchAmount; }
            if (best.number && Mathf.Abs(e.Amount) >= best.minAmount)
            {
                var col = e.Crit ? best.critNumberColor : best.numberColor;
                _spawnNumber(v.Target + Vector3.up * 1.9f, Mathf.Abs(e.Amount).ToString(), col, best.numberScale * (e.Crit ? 1.4f : 1f));
            }
        }
    }

    // ---- lifecycle -----------------------------------------------------------

    private void Start()
    {
        if (!Application.isPlaying) return;
        if (!Load()) return;
        Build();
        _clock = 0f; _fxCursor = 0; _playing = true;
        ApplyFold(0);
    }

    private void Update()
    {
        if (!_playing || _fold == null) return;
        _clock += Time.deltaTime * ticksPerSecond;
        if (_clock >= _endTick)
        {
            if (loop) { _clock = 0f; _fold = PlaybackState.From(_initial); _fxCursor = 0; ResetAnim(); }
            else _clock = _endTick;
        }
        int tick = Mathf.FloorToInt(_clock);
        ApplyFold(tick);
        DispatchUpTo(tick);

        float dt = Time.deltaTime;
        foreach (var v in _views.Values)
        {
            if (v.Root.gameObject.activeSelf)
                v.Root.position = Vector3.Lerp(v.Root.position, v.Target, 12f * dt);
            if (v.FlashT > 0f) v.FlashT -= dt / v.FlashDur;
            if (v.PunchT > 0f) v.PunchT -= dt / v.PunchDur;
            v.ApplyVisual();
        }
    }

    /// <summary>Editor scrub: freeze the fold at <paramref name="tick"/>, snap the view, and replay
    /// the last couple ticks' tells so a static capture reveals flashes/punches/numbers.</summary>
    public void BuildPreview(int tick)
    {
        _playing = false;
        _lastPreviewTick = tick;
        if (!Load()) return;
        Build();
        ApplyFold(tick);
        ResetAnim();
        foreach (var e in _events)
            if (e.Tick > tick - 2 && e.Tick <= tick) _director.Handle(e);
        foreach (var v in _views.Values) { v.Root.position = v.Target; v.ApplyVisual(); }
    }

    /// <summary>Hot-reload entry: re-read the (already-reloaded) tuning and rebuild so it shows.</summary>
    public void ReapplyTuning()
    {
        if (_tuning != null) _data = _tuning.data;
        if (Application.isPlaying)
        {
            _director = new FeedbackDirector(_views, _data, SpawnNumber);
            FrameCamera(); ApplyPost();
        }
        else BuildPreview(_lastPreviewTick);
    }

    public void ClearGenerated()
    {
        _views.Clear(); _fieldTiles.Clear(); _numberPool.Clear();
        if (_generated != null) DestroyImmediate(_generated.gameObject);
        _generated = null;
    }

    // ---- build ---------------------------------------------------------------

    private bool Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, replayFile);
        if (!File.Exists(path)) { Debug.LogError($"[ReplayPlayer] replay not found: {path}"); return false; }
        using (var fs = File.OpenRead(path))
            (_initial, _events) = Replay.Read(fs);
        _endTick = _events.Count > 0 ? _events[_events.Count - 1].Tick : 0;
        _fold = PlaybackState.From(_initial);

        _tuning = FindFirstObjectByType<TuningConfig>();
        if (_tuning != null) { _tuning.LoadFromJson(); _data = _tuning.data; }
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _director = new FeedbackDirector(_views, _data, SpawnNumber);
        return true;
    }

    private void Build()
    {
        ClearGenerated();
        _generated = new GameObject("~generated").transform;
        _generated.SetParent(transform, false);
        _generated.gameObject.hideFlags = HideFlags.DontSave;

        BuildBoard();
        foreach (var u in _initial) SpawnView(u);
        FrameCamera();
        ApplyPost();
    }

    private void ResetAnim() { foreach (var v in _views.Values) { v.FlashT = 0f; v.PunchT = 0f; } }

    private void DispatchUpTo(int tick)
    {
        while (_fxCursor < _events.Count && _events[_fxCursor].Tick <= tick)
            _director.Handle(_events[_fxCursor++]);
    }

    private void SpawnNumber(Vector3 pos, string s, Color c, float scale)
    {
        var fn = _numberPool.Count > 0 ? _numberPool.Pop()
            : FloatingNumber.Create(_generated, _font, _data.numbers.characterSize, _data.numbers.fontSize);
        fn.Play(pos, s, c, scale, _numberFace, _data.numbers.riseSpeed, _data.numbers.lifeSeconds, n => _numberPool.Push(n));
    }

    private void ApplyPost()
    {
        var vol = FindFirstObjectByType<Volume>();
        if (vol == null || vol.sharedProfile == null) return;
        var p = vol.sharedProfile;
        if (p.TryGet<Bloom>(out var b)) { b.intensity.value = _data.post.bloomIntensity; b.threshold.value = _data.post.bloomThreshold; }
        if (p.TryGet<Vignette>(out var v)) v.intensity.value = _data.post.vignette;
        if (p.TryGet<ColorAdjustments>(out var ca)) ca.saturation.value = _data.post.saturation;
        if (p.TryGet<DepthOfField>(out var d)) { d.gaussianStart.value = _data.post.dofStart; d.gaussianEnd.value = _data.post.dofEnd; }
    }

    // ---- board (hex grid) ----------------------------------------------------

    private void BuildBoard()
    {
        var baseSlab = GameObject.CreatePrimitive(PrimitiveType.Plane);
        baseSlab.name = "BoardBase";
        baseSlab.transform.SetParent(_generated, false);
        Vector3 min = HexToWorld(new Hex(0, 0));
        Vector3 max = HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1));
        Vector3 center = (min + max) * 0.5f;
        float spanX = Mathf.Abs(max.x - min.x) + 4f * hexSize;
        float spanZ = Mathf.Abs(max.z - min.z) + 4f * hexSize;
        baseSlab.transform.position = new Vector3(center.x, -0.04f, center.z);
        baseSlab.transform.localScale = new Vector3(spanX / 10f, 1f, spanZ / 10f);
        Paint(baseSlab.GetComponent<Renderer>(), BaseDark);
        DestroyImmediate(baseSlab.GetComponent<Collider>());

        var tiles = new GameObject("Tiles").transform;
        tiles.SetParent(_generated, false);
        for (int row = 0; row < Battle.BoardRows; row++)
            for (int col = 0; col < Battle.BoardCols; col++)
            {
                var tile = new GameObject($"tile_{row}_{col}");
                tile.transform.SetParent(tiles, false);
                tile.transform.position = HexToWorld(Hex.FromRowCol(row, col));
                tile.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                tile.AddComponent<MeshFilter>().sharedMesh = HexMesh();
                Color c = row <= 2 ? TileTeam0 : row >= Battle.BoardRows - 3 ? TileTeam1 : TileNeutral;
                PaintTile(tile.AddComponent<MeshRenderer>(), c);
            }
    }

    private Mesh HexMesh()
    {
        if (_hexMesh != null) return _hexMesh;
        var m = new Mesh { name = "hex" };
        var verts = new Vector3[7]; var norms = new Vector3[7];
        verts[0] = Vector3.zero; norms[0] = Vector3.up;
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Deg2Rad * (60f * i + 30f);
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hexSize;
            norms[i + 1] = Vector3.up;
        }
        var tris = new int[18];
        for (int i = 0; i < 6; i++) { tris[i * 3] = 0; tris[i * 3 + 1] = 1 + (i + 1) % 6; tris[i * 3 + 2] = 1 + i; }
        m.vertices = verts; m.normals = norms; m.triangles = tris; m.RecalculateBounds();
        _hexMesh = m;
        return m;
    }

    // ---- units ---------------------------------------------------------------

    private void SpawnView(PlaybackUnit u)
    {
        var root = new GameObject($"unit_{u.Id}_{u.Name}").transform;
        root.SetParent(_generated, false);

        var bodyScale = new Vector3(0.6f, 0.5f, 0.6f);
        var body = MakePrimitive(PrimitiveType.Capsule, root, new Vector3(0f, 0.5f, 0f), bodyScale, u.Team == 0 ? Team0 : Team1);

        MakeBarBack(root, 1.55f);
        var hp = MakeFill(root, 1.55f, new Color(0.35f, 0.85f, 0.35f));
        var shield = MakeFill(root, 1.55f, new Color(0.55f, 0.80f, 1.00f));
        shield.localPosition += new Vector3(0f, 0f, -0.04f);
        MakeBarBack(root, 1.40f, 0.09f);
        var mana = MakeFill(root, 1.40f, new Color(0.35f, 0.55f, 0.95f), 0.06f);

        var pips = new GameObject("pips").transform;
        pips.SetParent(root, false);
        pips.localPosition = new Vector3(-0.45f, 1.72f, 0f);

        _views[u.Id] = new UnitView
        {
            Root = root, Body = body, BodyRenderer = body.GetComponent<Renderer>(), BodyBaseScale = bodyScale,
            HpFill = hp, ShieldFill = shield, ManaFill = mana, Pips = pips,
            MaxHp = u.MaxHp, ManaMax = u.ManaMax, TeamColor = u.Team == 0 ? Team0 : Team1, Target = HexToWorld(u.Pos),
        };
    }

    public void ApplyFold(int tick)
    {
        _fold.AdvanceToTick(_events, tick);
        foreach (var u in _fold.Units)
        {
            if (!_views.TryGetValue(u.Id, out var v)) continue;
            v.Root.gameObject.SetActive(!u.Dead);
            if (u.Dead) continue;
            v.Target = HexToWorld(u.Pos);
            SetFill(v.HpFill, v.MaxHp > 0 ? (float)u.Hp / v.MaxHp : 0f);
            SetFill(v.ShieldFill, v.MaxHp > 0 ? Mathf.Clamp01((float)u.Shield / v.MaxHp) : 0f);
            SetFill(v.ManaFill, v.ManaMax > 0 ? (float)u.Mana / v.ManaMax : 0f);
            UpdatePips(v, u);
        }
        SyncFields();
    }

    private void UpdatePips(UnitView v, PlaybackUnit u)
    {
        var kinds = new List<StatusKind>();
        foreach (var s in u.Statuses) if (!kinds.Contains(s.Kind)) kinds.Add(s.Kind);
        for (int i = v.Pips.childCount; i < kinds.Count; i++)
            MakePrimitive(PrimitiveType.Cube, v.Pips, new Vector3(0.14f * i, 0f, 0f), Vector3.one * 0.1f, Color.white);
        for (int i = 0; i < v.Pips.childCount; i++)
        {
            var pip = v.Pips.GetChild(i);
            bool on = i < kinds.Count;
            pip.gameObject.SetActive(on);
            if (on) Paint(pip.GetComponent<Renderer>(), StatusColor(kinds[i]));
        }
    }

    private void SyncFields()
    {
        var liveIds = new HashSet<int>();
        foreach (var f in _fold.Fields)
        {
            liveIds.Add(f.Id);
            IEnumerable<Hex> hexes = f.AttachedTo >= 0
                ? (_fold.ById(f.AttachedTo) is PlaybackUnit a && !a.Dead ? Hex.Range(a.Pos, f.Radius) : new List<Hex>())
                : f.Hexes;
            if (!_fieldTiles.TryGetValue(f.Id, out var group))
            {
                group = new GameObject($"field_{f.Id}");
                group.transform.SetParent(_generated, false);
                _fieldTiles[f.Id] = group;
            }
            RebuildFieldTiles(group.transform, hexes, f.IsWall);
        }
        var gone = new List<int>();
        foreach (var kv in _fieldTiles) if (!liveIds.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone) { DestroyImmediate(_fieldTiles[id]); _fieldTiles.Remove(id); }
    }

    private void RebuildFieldTiles(Transform group, IEnumerable<Hex> hexes, bool wall)
    {
        var list = new List<Hex>(hexes);
        while (group.childCount < list.Count)
        {
            var t = new GameObject("ftile");
            t.transform.SetParent(group, false);
            t.AddComponent<MeshFilter>().sharedMesh = HexMesh();
            PaintTile(t.AddComponent<MeshRenderer>(), wall ? WallTile : AuraTile);
            t.transform.localScale = new Vector3(0.85f, 1f, 0.85f);
        }
        for (int i = 0; i < group.childCount; i++)
        {
            var t = group.GetChild(i);
            bool on = i < list.Count;
            t.gameObject.SetActive(on);
            if (on) t.position = HexToWorld(list[i]) + new Vector3(0f, wall ? 0.4f : 0.03f, 0f);
        }
    }

    // ---- camera / helpers ----------------------------------------------------

    private void FrameCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 min = HexToWorld(new Hex(0, 0));
        Vector3 max = HexToWorld(Hex.FromRowCol(Battle.BoardRows - 1, Battle.BoardCols - 1));
        Vector3 center = (min + max) * 0.5f;
        float span = Mathf.Max(Mathf.Abs(max.x - min.x), Mathf.Abs(max.z - min.z));
        var offset = Quaternion.Euler(_data.camera.pitch, _data.camera.yaw, 0f) * Vector3.back * (span * _data.camera.distance);
        cam.transform.position = center + offset;
        cam.transform.LookAt(center);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = _data.camera.background;
        _numberFace = cam.transform.rotation;
    }

    private Vector3 HexToWorld(Hex h) =>
        new Vector3(hexSize * Mathf.Sqrt(3f) * (h.Q + h.R / 2f), 0f, hexSize * 1.5f * h.R);

    private const float BarWidth = 0.9f;
    private void MakeBarBack(Transform parent, float y, float h = 0.13f) =>
        MakePrimitive(PrimitiveType.Cube, parent, new Vector3(0f, y, 0.02f), new Vector3(BarWidth, h, 0.05f), new Color(0.05f, 0.05f, 0.06f));
    private Transform MakeFill(Transform parent, float y, Color c, float h = 0.1f) =>
        MakePrimitive(PrimitiveType.Cube, parent, new Vector3(0f, y, 0f), new Vector3(BarWidth, h, 0.06f), c);
    private void SetFill(Transform fill, float frac)
    {
        frac = Mathf.Clamp01(frac);
        var s = fill.localScale; s.x = BarWidth * frac; fill.localScale = s;
        var p = fill.localPosition; p.x = -BarWidth * 0.5f * (1f - frac); fill.localPosition = p;
    }

    private Transform MakePrimitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color c)
    {
        var t = GameObject.CreatePrimitive(type).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos; t.localScale = localScale;
        var col = t.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        Paint(t.GetComponent<Renderer>(), c);
        return t;
    }

    private static Color StatusColor(StatusKind k)
    {
        switch (k)
        {
            case StatusKind.Dot: case StatusKind.Burn: return new Color(0.95f, 0.45f, 0.15f);
            case StatusKind.Haste: return new Color(0.95f, 0.90f, 0.35f);
            case StatusKind.AttackUp: return new Color(0.95f, 0.30f, 0.30f);
            case StatusKind.Root: case StatusKind.Silence: case StatusKind.Disarm: return new Color(0.55f, 0.35f, 0.75f);
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    private static readonly Dictionary<Color, Material> _matCache = new Dictionary<Color, Material>();
    private static Material CachedMat(Renderer r, Color c, bool doubleSided)
    {
        var key = doubleSided ? c + new Color(0.001f, 0f, 0f) : c;
        if (!_matCache.TryGetValue(key, out var mat) || mat == null)
        {
            var shader = r.sharedMaterial != null ? r.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            if (doubleSided && mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            _matCache[key] = mat;
        }
        return mat;
    }
    private static void Paint(Renderer r, Color c) => r.sharedMaterial = CachedMat(r, c, false);
    private static void PaintTile(Renderer r, Color c) => r.sharedMaterial = CachedMat(r, c, true);
}
