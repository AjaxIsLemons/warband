using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit runtime tuning cockpit. Reflects over <see cref="TuningData"/> and generates a
/// control per public field (sliders with editable ranges, color pickers, foldouts, list editor).
/// Every edit writes back into <c>tuningConfig.data</c> and calls <c>ReplayPlayer.ReapplyTuning()</c>
/// so changes show live. Save persists tuning.json + the slider-range map; Reload re-reads both.
/// F1 toggles; the window is draggable (header) and resizable (bottom-right handle). See render-polish.md.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DebugMenu : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindFirstObjectByType<DebugMenu>() == null)
            new GameObject("~DebugMenu").AddComponent<DebugMenu>();
    }

    // ---- palette / metrics ---------------------------------------------------
    private static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.97f);
    private static readonly Color HeaderBg = new Color(0.14f, 0.16f, 0.21f, 1f);
    private static readonly Color Border = new Color(0.30f, 0.55f, 0.90f, 0.55f);
    private static readonly Color TextCol = new Color(0.88f, 0.91f, 0.96f);
    private static readonly Color Muted = new Color(0.58f, 0.63f, 0.71f);
    private const float LabelW = 118f;
    private const float ValW = 62f;
    private const float MiniW = 42f;

    // ---- state ---------------------------------------------------------------
    private UIDocument _doc;
    private VisualElement _panel;   // the whole window (absolute-positioned)
    private ScrollView _body;       // scrolling content region
    private TextField _search;
    private bool _open = true;

    private TuningConfig _config;
    private ReplayPlayer _player;

    // Editable slider ranges, keyed by stable field path ("post.bloomIntensity", "tells[0].flashSeconds").
    private readonly Dictionary<string, (float min, float max)> _ranges = new Dictionary<string, (float, float)>();

    // Filter bookkeeping — rebuilt every BuildUI().
    private sealed class RowRef { public VisualElement El; public string Label; }
    private sealed class FoldRef { public Foldout Fo; public readonly List<RowRef> Desc = new List<RowRef>(); }
    private List<RowRef> _rows = new List<RowRef>();
    private List<FoldRef> _folds = new List<FoldRef>();

    // Window geometry (persisted across rebuilds; edited by drag/resize).
    private float _px = 14f, _py = 14f, _pw = 468f, _ph = 640f;

    private static string RangesPath => Path.Combine(Application.streamingAssetsPath, "tuning.ranges.json");

    // ---- lifecycle -----------------------------------------------------------

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        var theme = Resources.Load<ThemeStyleSheet>("DebugTheme");
        if (theme != null) ps.themeStyleSheet = theme;
        ps.scaleMode = PanelScaleMode.ConstantPixelSize;
        _doc.panelSettings = ps;
        _doc.sortingOrder = 1000;

        _config = FindFirstObjectByType<TuningConfig>();
        _player = FindFirstObjectByType<ReplayPlayer>();
        LoadRanges();
        BuildUI();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            _open = !_open;
            if (_panel != null) _panel.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Config/player may spawn around the same time as us — re-find and rebuild once available.
        if (_config == null)
        {
            _config = FindFirstObjectByType<TuningConfig>();
            if (_config != null) BuildUI();
        }
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
    }

    /// <summary>Push edited data into the live scene.</summary>
    private void Apply()
    {
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
        if (_player != null) _player.ReapplyTuning();
    }

    // ---- top-level UI build --------------------------------------------------

    private void BuildUI()
    {
        var root = _doc.rootVisualElement;
        root.Clear();
        _rows = new List<RowRef>();
        _folds = new List<FoldRef>();

        _panel = new VisualElement();
        var s = _panel.style;
        s.position = Position.Absolute;
        s.left = _px; s.top = _py; s.width = _pw; s.height = _ph;
        s.flexDirection = FlexDirection.Column;
        s.backgroundColor = PanelBg;
        s.color = TextCol; // inherits to every label/field below
        s.overflow = Overflow.Hidden;
        Round(s, 8);
        s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = s.borderBottomWidth = 1;
        s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = Border;

        BuildHeader();
        BuildToolbar();

        _body = new ScrollView(ScrollViewMode.Vertical);
        _body.style.flexGrow = 1;
        _body.style.paddingLeft = 10; _body.style.paddingRight = 10;
        _body.style.paddingTop = 6; _body.style.paddingBottom = 10;
        _panel.Add(_body);

        BuildBody();
        BuildResizeHandle();

        root.Add(_panel);
        _panel.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.flexShrink = 0;
        header.style.paddingLeft = 10; header.style.paddingRight = 10;
        header.style.paddingTop = 7; header.style.paddingBottom = 7;
        header.style.backgroundColor = HeaderBg;

        var title = new Label("WARBAND — TUNING");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 13;
        title.style.color = new Color(0.85f, 0.92f, 1f);
        var hint = new Label("F1");
        hint.style.color = Muted; hint.style.fontSize = 11;
        header.Add(title);
        header.Add(hint);
        _panel.Add(header);

        // Drag the whole header to move the window.
        Vector2 origin = default, startPos = default;
        bool dragging = false;
        header.RegisterCallback<PointerDownEvent>(e =>
        {
            dragging = true;
            origin = (Vector2)e.position;
            startPos = new Vector2(_px, _py);
            header.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        header.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!dragging) return;
            Vector2 d = (Vector2)e.position - origin;
            _px = startPos.x + d.x; _py = startPos.y + d.y;
            _panel.style.left = _px; _panel.style.top = _py;
        });
        header.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!dragging) return;
            dragging = false;
            header.ReleasePointer(e.pointerId);
        });
    }

    private void BuildToolbar()
    {
        var bar = new VisualElement();
        bar.style.flexShrink = 0;
        bar.style.paddingLeft = 10; bar.style.paddingRight = 10;
        bar.style.paddingTop = 6; bar.style.paddingBottom = 6;
        bar.style.borderBottomWidth = 1;
        bar.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);

        // Row 1: search + Save + Reload
        var row1 = HRow();
        _search = new TextField("search");
        _search.style.flexGrow = 1;
        _search.tooltip = "filter fields (case-insensitive)";
        _search.RegisterValueChangedCallback(e => ApplyFilter(e.newValue));
        var save = new Button(OnSave) { text = "Save" };
        var reload = new Button(OnReload) { text = "Reload" };
        StyleButton(save); StyleButton(reload);
        row1.Add(_search); row1.Add(save); row1.Add(reload);
        bar.Add(row1);

        // Row 2: battle speed
        var row2 = HRow();
        var spdLabel = FieldLabel("battle speed");
        var spd = new Slider(1f, 40f) { value = _player != null ? _player.ticksPerSecond : 10f };
        spd.style.flexGrow = 1;
        var spdVal = new FloatField { value = _player != null ? _player.ticksPerSecond : 10f };
        spdVal.style.width = ValW; spdVal.style.flexShrink = 0;
        spd.RegisterValueChangedCallback(e =>
        {
            if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
            if (_player != null) _player.ticksPerSecond = e.newValue;
            spdVal.SetValueWithoutNotify(e.newValue);
        });
        spdVal.RegisterValueChangedCallback(e =>
        {
            if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
            if (_player != null) _player.ticksPerSecond = e.newValue;
            spd.SetValueWithoutNotify(e.newValue);
        });
        row2.Add(spdLabel); row2.Add(spd); row2.Add(spdVal);
        bar.Add(row2);

        _panel.Add(bar);
    }

    private void BuildBody()
    {
        var data = _config != null ? _config.data : null;
        if (data == null)
        {
            var wait = new Label("Waiting for TuningConfig in scene…");
            wait.style.color = Muted; wait.style.marginTop = 8;
            _body.Add(wait);
            return;
        }
        BuildObject(data, _body, "", new List<FoldRef>());
    }

    private void BuildResizeHandle()
    {
        var handle = new VisualElement();
        var s = handle.style;
        s.position = Position.Absolute;
        s.right = 3; s.bottom = 3; s.width = 15; s.height = 15;
        s.backgroundColor = new Color(0.4f, 0.6f, 0.95f, 0.5f);
        Round(s, 3);
        _panel.Add(handle);

        Vector2 origin = default, startSize = default;
        bool resizing = false;
        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            resizing = true;
            origin = (Vector2)e.position;
            startSize = new Vector2(_pw, _ph);
            handle.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        handle.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!resizing) return;
            Vector2 d = (Vector2)e.position - origin;
            _pw = Mathf.Max(320f, startSize.x + d.x);
            _ph = Mathf.Max(220f, startSize.y + d.y);
            _panel.style.width = _pw; _panel.style.height = _ph;
        });
        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!resizing) return;
            resizing = false;
            handle.ReleasePointer(e.pointerId);
        });
    }

    // ---- reflection-driven field generation ----------------------------------

    private void BuildObject(object obj, VisualElement parent, string path, List<FoldRef> ancestors)
    {
        if (obj == null) return;
        foreach (var f in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var ft = f.FieldType;
            string p = string.IsNullOrEmpty(path) ? f.Name : path + "." + f.Name;

            if (ft == typeof(float)) AddFloatRow(obj, f, parent, p, ancestors);
            else if (ft == typeof(int)) AddIntRow(obj, f, parent, p, ancestors);
            else if (ft == typeof(bool)) AddBoolRow(obj, f, parent, ancestors);
            else if (ft == typeof(string)) AddStringRow(obj, f, parent, ancestors);
            else if (ft == typeof(Color)) AddColorRow(obj, f, parent, ancestors);
            else if (ft.IsEnum) AddEnumRow(obj, f, parent, ancestors);
            else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                AddListFoldout(obj, f, parent, p, ancestors);
            else if (ft.IsClass)
                AddNestedFoldout(f.GetValue(obj), parent, f.Name, p, ancestors, expanded: true);
        }
    }

    private void AddNestedFoldout(object child, VisualElement parent, string title, string path, List<FoldRef> ancestors, bool expanded)
    {
        var fo = MakeFoldout(title, expanded);
        parent.Add(fo);
        var fr = new FoldRef { Fo = fo };
        _folds.Add(fr);
        var next = new List<FoldRef>(ancestors) { fr };
        BuildObject(child, fo, path, next);
    }

    private void AddListFoldout(object obj, FieldInfo f, VisualElement parent, string path, List<FoldRef> ancestors)
    {
        var list = f.GetValue(obj) as System.Collections.IList;
        var fo = MakeFoldout($"{f.Name} ({(list?.Count ?? 0)})", expanded: false);
        parent.Add(fo);
        var fr = new FoldRef { Fo = fo };
        _folds.Add(fr);
        var next = new List<FoldRef>(ancestors) { fr };

        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            string title = $"[{i}]";
            var ek = item?.GetType().GetField("eventKind");
            var ekVal = ek?.GetValue(item);
            if (ekVal != null) title = $"[{i}] {ekVal}";

            var itemFo = MakeFoldout(title, expanded: false);
            fo.Add(itemFo);
            var ifr = new FoldRef { Fo = itemFo };
            _folds.Add(ifr);
            var inext = new List<FoldRef>(next) { ifr };
            BuildObject(item, itemFo, $"{path}[{i}]", inext);
        }
    }

    private void AddFloatRow(object obj, FieldInfo f, VisualElement parent, string path, List<FoldRef> ancestors)
    {
        float v = (float)f.GetValue(obj);
        var (lo, hi) = RangeFor(path, f, v);

        var row = HRow();
        row.Add(FieldLabel(f.Name));

        var slider = new Slider(lo, hi) { value = v };
        slider.style.flexGrow = 1; slider.style.marginRight = 4;
        var val = new FloatField { value = v };
        val.style.width = ValW; val.style.flexShrink = 0;
        var minF = new FloatField { value = lo }; minF.tooltip = "slider min";
        var maxF = new FloatField { value = hi }; maxF.tooltip = "slider max";
        minF.style.width = MiniW; minF.style.flexShrink = 0;
        maxF.style.width = MiniW; maxF.style.flexShrink = 0;

        slider.RegisterValueChangedCallback(e =>
        {
            f.SetValue(obj, e.newValue);
            val.SetValueWithoutNotify(e.newValue);
            Apply();
        });
        // Exact field accepts ANY value; slider just clamps its knob to the visible range.
        val.RegisterValueChangedCallback(e =>
        {
            f.SetValue(obj, e.newValue);
            slider.SetValueWithoutNotify(e.newValue);
            Apply();
        });
        minF.RegisterValueChangedCallback(e =>
        {
            var r = _ranges[path]; _ranges[path] = (e.newValue, r.max);
            slider.lowValue = e.newValue;
        });
        maxF.RegisterValueChangedCallback(e =>
        {
            var r = _ranges[path]; _ranges[path] = (r.min, e.newValue);
            slider.highValue = e.newValue;
        });

        row.Add(slider); row.Add(val); row.Add(minF); row.Add(maxF);
        parent.Add(row);
        RegisterRow(row, f.Name, ancestors);
    }

    private void AddIntRow(object obj, FieldInfo f, VisualElement parent, string path, List<FoldRef> ancestors)
    {
        int v = (int)f.GetValue(obj);
        var (lo, hi) = RangeFor(path, f, v);
        int loI = Mathf.RoundToInt(lo), hiI = Mathf.RoundToInt(hi);

        var row = HRow();
        row.Add(FieldLabel(f.Name));

        var slider = new SliderInt(loI, hiI) { value = v };
        slider.style.flexGrow = 1; slider.style.marginRight = 4;
        var val = new IntegerField { value = v };
        val.style.width = ValW; val.style.flexShrink = 0;
        var minF = new IntegerField { value = loI }; minF.tooltip = "slider min";
        var maxF = new IntegerField { value = hiI }; maxF.tooltip = "slider max";
        minF.style.width = MiniW; minF.style.flexShrink = 0;
        maxF.style.width = MiniW; maxF.style.flexShrink = 0;

        slider.RegisterValueChangedCallback(e =>
        {
            f.SetValue(obj, e.newValue);
            val.SetValueWithoutNotify(e.newValue);
            Apply();
        });
        val.RegisterValueChangedCallback(e =>
        {
            f.SetValue(obj, e.newValue);
            slider.SetValueWithoutNotify(e.newValue);
            Apply();
        });
        minF.RegisterValueChangedCallback(e =>
        {
            var r = _ranges[path]; _ranges[path] = (e.newValue, r.max);
            slider.lowValue = e.newValue;
        });
        maxF.RegisterValueChangedCallback(e =>
        {
            var r = _ranges[path]; _ranges[path] = (r.min, e.newValue);
            slider.highValue = e.newValue;
        });

        row.Add(slider); row.Add(val); row.Add(minF); row.Add(maxF);
        parent.Add(row);
        RegisterRow(row, f.Name, ancestors);
    }

    private void AddBoolRow(object obj, FieldInfo f, VisualElement parent, List<FoldRef> ancestors)
    {
        var row = HRow();
        row.Add(FieldLabel(f.Name));
        var toggle = new Toggle { value = (bool)f.GetValue(obj) };
        toggle.RegisterValueChangedCallback(e => { f.SetValue(obj, e.newValue); Apply(); });
        row.Add(toggle);
        parent.Add(row);
        RegisterRow(row, f.Name, ancestors);
    }

    private void AddStringRow(object obj, FieldInfo f, VisualElement parent, List<FoldRef> ancestors)
    {
        var row = HRow();
        row.Add(FieldLabel(f.Name));
        var tf = new TextField { value = (string)f.GetValue(obj) ?? "" };
        tf.style.flexGrow = 1;
        tf.RegisterValueChangedCallback(e => { f.SetValue(obj, e.newValue); Apply(); });
        row.Add(tf);
        parent.Add(row);
        RegisterRow(row, f.Name, ancestors);
    }

    private void AddEnumRow(object obj, FieldInfo f, VisualElement parent, List<FoldRef> ancestors)
    {
        var row = HRow();
        row.Add(FieldLabel(f.Name));
        var ef = new EnumField((Enum)f.GetValue(obj));
        ef.style.flexGrow = 1;
        ef.RegisterValueChangedCallback(e => { f.SetValue(obj, e.newValue); Apply(); });
        row.Add(ef);
        parent.Add(row);
        RegisterRow(row, f.Name, ancestors);
    }

    private void AddColorRow(object obj, FieldInfo f, VisualElement parent, List<FoldRef> ancestors)
    {
        var block = new VisualElement();
        block.style.flexDirection = FlexDirection.Column;
        block.style.marginBottom = 5; block.style.marginTop = 1;

        var top = HRow();
        top.Add(FieldLabel(f.Name));
        var swatch = new VisualElement();
        swatch.style.width = 18; swatch.style.height = 18; swatch.style.flexShrink = 0;
        swatch.style.marginRight = 6;
        Round(swatch.style, 3);
        swatch.style.borderLeftWidth = swatch.style.borderRightWidth = swatch.style.borderTopWidth = swatch.style.borderBottomWidth = 1;
        var sw = new Color(1f, 1f, 1f, 0.25f);
        swatch.style.borderLeftColor = swatch.style.borderRightColor = swatch.style.borderTopColor = swatch.style.borderBottomColor = sw;
        var hex = new TextField { value = "" };
        hex.style.flexGrow = 1; hex.tooltip = "#RRGGBBAA";
        top.Add(swatch); top.Add(hex);
        block.Add(top);

        var r = new Slider("R", 0f, 1f); var g = new Slider("G", 0f, 1f);
        var b = new Slider("B", 0f, 1f); var a = new Slider("A", 0f, 1f);
        foreach (var sl in new[] { r, g, b, a }) { sl.style.marginTop = 1; sl.style.marginBottom = 1; block.Add(sl); }

        Color Cur() => (Color)f.GetValue(obj);
        void Sync(Color c)
        {
            swatch.style.backgroundColor = c;
            hex.SetValueWithoutNotify("#" + ColorUtility.ToHtmlStringRGBA(c));
            r.SetValueWithoutNotify(c.r); g.SetValueWithoutNotify(c.g);
            b.SetValueWithoutNotify(c.b); a.SetValueWithoutNotify(c.a);
        }
        void SetC(Color c) { f.SetValue(obj, c); Sync(c); Apply(); }

        Sync(Cur());
        r.RegisterValueChangedCallback(e => { var c = Cur(); c.r = e.newValue; SetC(c); });
        g.RegisterValueChangedCallback(e => { var c = Cur(); c.g = e.newValue; SetC(c); });
        b.RegisterValueChangedCallback(e => { var c = Cur(); c.b = e.newValue; SetC(c); });
        a.RegisterValueChangedCallback(e => { var c = Cur(); c.a = e.newValue; SetC(c); });
        hex.RegisterValueChangedCallback(e => { if (ColorUtility.TryParseHtmlString(e.newValue, out var c)) SetC(c); });

        parent.Add(block);
        RegisterRow(block, f.Name, ancestors);
    }

    // ---- ranges: defaults + persistence --------------------------------------

    /// <summary>Range for a field path: persisted value wins, else its [Range]/[Min] attribute,
    /// else [0, max(1, |value|*4)]. Computed defaults get stored so Save persists them too.</summary>
    private (float min, float max) RangeFor(string path, FieldInfo f, float value)
    {
        if (_ranges.TryGetValue(path, out var r)) return r;

        float lo, hi;
        var ra = f.GetCustomAttribute<RangeAttribute>();
        if (ra != null) { lo = ra.min; hi = ra.max; }
        else
        {
            var mi = f.GetCustomAttribute<MinAttribute>();
            lo = mi != null ? mi.min : 0f;
            hi = Mathf.Max(lo + 1f, Mathf.Abs(value) * 4f);
        }
        var tuple = (lo, hi);
        _ranges[path] = tuple;
        return tuple;
    }

    private sealed class RangePair { public float min; public float max; }

    private void LoadRanges()
    {
        _ranges.Clear();
        try
        {
            if (!File.Exists(RangesPath)) return;
            var dto = JsonConvert.DeserializeObject<Dictionary<string, RangePair>>(File.ReadAllText(RangesPath));
            if (dto == null) return;
            foreach (var kv in dto)
                if (kv.Value != null) _ranges[kv.Key] = (kv.Value.min, kv.Value.max);
        }
        catch (Exception e) { Debug.LogWarning($"[DebugMenu] ranges load failed ({e.Message}); using defaults"); }
    }

    private void SaveRanges()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RangesPath));
            var dto = _ranges.ToDictionary(kv => kv.Key, kv => new RangePair { min = kv.Value.min, max = kv.Value.max });
            File.WriteAllText(RangesPath, JsonConvert.SerializeObject(dto, Formatting.Indented));
        }
        catch (Exception e) { Debug.LogError($"[DebugMenu] ranges save failed ({e.Message})"); }
    }

    // ---- toolbar actions -----------------------------------------------------

    private void OnSave()
    {
        if (_config != null) _config.WriteToJson();
        SaveRanges();
    }

    private void OnReload()
    {
        if (_config != null) _config.LoadFromJson();
        LoadRanges();     // reset ranges from disk; BuildBody re-adds any missing defaults
        BuildUI();
        Apply();
    }

    // ---- search filter -------------------------------------------------------

    private void ApplyFilter(string query)
    {
        string q = (query ?? "").Trim().ToLowerInvariant();
        bool empty = q.Length == 0;

        foreach (var row in _rows)
        {
            bool match = empty || row.Label.ToLowerInvariant().Contains(q);
            row.El.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
        }
        foreach (var fold in _folds)
        {
            bool any = empty || fold.Desc.Any(rw => rw.Label.ToLowerInvariant().Contains(q));
            fold.Fo.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (!empty && any) fold.Fo.value = true; // auto-expand foldouts containing a match
        }
    }

    private void RegisterRow(VisualElement el, string label, List<FoldRef> ancestors)
    {
        var r = new RowRef { El = el, Label = label };
        _rows.Add(r);
        foreach (var a in ancestors) a.Desc.Add(r);
    }

    // ---- small UI helpers ----------------------------------------------------

    private static VisualElement HRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 3; row.style.marginTop = 1;
        return row;
    }

    private static Label FieldLabel(string text)
    {
        var l = new Label(text);
        l.style.width = LabelW; l.style.flexShrink = 0;
        l.style.fontSize = 12; l.style.marginRight = 4;
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.overflow = Overflow.Hidden;
        return l;
    }

    private static Foldout MakeFoldout(string title, bool expanded)
    {
        var fo = new Foldout { text = title, value = expanded };
        fo.style.marginTop = 3; fo.style.marginBottom = 2;
        fo.style.color = TextCol;
        return fo;
    }

    private static void StyleButton(Button b)
    {
        b.style.marginLeft = 4;
        b.style.paddingLeft = 8; b.style.paddingRight = 8;
        b.style.color = TextCol;
        b.style.backgroundColor = new Color(0.20f, 0.24f, 0.32f);
        Round(b.style, 4);
    }

    private static void Round(IStyle s, float r)
    {
        s.borderTopLeftRadius = r; s.borderTopRightRadius = r;
        s.borderBottomLeftRadius = r; s.borderBottomRightRadius = r;
    }
}
