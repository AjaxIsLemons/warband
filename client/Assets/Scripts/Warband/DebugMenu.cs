using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Warband.Sim;

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
    // Startup order is owned by GameBoot — see that class before adding one back here.

    // ---- palette / metrics ---------------------------------------------------
    private static readonly Color PanelBg = new Color(0.09f, 0.10f, 0.13f, 0.97f);
    private static readonly Color HeaderBg = new Color(0.14f, 0.16f, 0.21f, 1f);
    private static readonly Color Border = new Color(0.30f, 0.55f, 0.90f, 0.55f);
    private static readonly Color TextCol = new Color(0.88f, 0.91f, 0.96f);
    private static readonly Color Muted = new Color(0.58f, 0.63f, 0.71f);
    // Field-surface palette — the default runtime theme is LIGHT, so every input renders as a
    // white box with our inherited light text on top (unreadable). We override the input surfaces.
    private static readonly Color InputBg = new Color(0.16f, 0.18f, 0.23f, 1f);
    private static readonly Color InputBorder = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color SliderTrack = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color SliderKnob = new Color(0.55f, 0.72f, 1f, 1f);
    private static readonly Color TabActive = new Color(0.20f, 0.30f, 0.44f);
    private static readonly Color TabInactive = new Color(0.13f, 0.15f, 0.19f);
    private const float LabelW = 118f;
    private const float ValW = 62f;
    private const float MiniW = 42f;

    // Event-line color language (mirrors the tell palette in render-polish.md): damage white,
    // crit gold, burn orange · heal green · cast cyan · status± purple · death bold red ·
    // fields by flavor · blocked grey · everything else muted.
    private static readonly Color EvDamage = new Color(0.90f, 0.92f, 0.96f);
    private static readonly Color EvCrit = new Color(1.00f, 0.82f, 0.30f);
    private static readonly Color EvBurn = new Color(0.96f, 0.55f, 0.22f);
    private static readonly Color EvHeal = new Color(0.45f, 0.85f, 0.45f);
    private static readonly Color EvCast = new Color(0.42f, 0.80f, 0.95f);
    private static readonly Color EvStatus = new Color(0.72f, 0.55f, 0.92f);
    private static readonly Color EvDeath = new Color(0.96f, 0.36f, 0.32f);
    private static readonly Color EvBlocked = new Color(0.60f, 0.63f, 0.70f);
    private static readonly Color EvFieldHazard = new Color(0.95f, 0.50f, 0.30f);
    private static readonly Color EvFieldBoon = new Color(0.50f, 0.85f, 0.55f);
    private static readonly Color EvFieldDebuff = new Color(0.72f, 0.55f, 0.92f);
    private static readonly Color EvFieldElse = new Color(0.82f, 0.80f, 0.55f);

    // ---- state ---------------------------------------------------------------
    private UIDocument _doc;
    private VisualElement _panel;   // the whole window (absolute-positioned)
    private ScrollView _body;       // TUNING tab: reflected control region
    private ScrollView _uiBody;     // UI FX tab: Hall motion/feedback recipes
    private TextField _search;
    private DropdownField _scenarioDrop;                        // replays/*.bytes picker
    private List<string> _scenarioChoices = new List<string>(); // relative paths, [ / ] cycle them
    private bool _open;

    // Tab strip: combat TUNING | menu/UI FX recipes | live replay EVENTS.
    private enum Tab { Tuning, UiEffects, Events }
    private Tab _tab = Tab.Tuning;
    private Button _tabTuning, _tabUiEffects, _tabEvents;

    // EVENTS tab surface + controls.
    private VisualElement _eventsPanel;   // whole tab body (controls row + scroll), display-toggled
    private ScrollView _eventsScroll;     // the line list (newest at the BOTTOM)
    private VisualElement _eventsContent;  // _eventsScroll.contentContainer (add/remove/trim here)
    private Toggle _followToggle, _noiseToggle;
    private TextField _eventFilter;
    private int _lastSeq;                  // last ReplayPlayer.EventSeq folded into the list

    private sealed class EventRow { public Label El; public bool Noise; public string Lower; }
    private readonly List<EventRow> _eventRows = new List<EventRow>();

    private TuningConfig _config;
    private HubPresentationConfig _uiConfig;
    private ReplayPlayer _player;
    private int _builtVersion = -1;   // _config.Version the current rows were generated from
    private int _builtUiVersion = -1;

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
        // This UIDocument has its own PanelSettings. UIDocument.sortingOrder only orders
        // documents on the same panel; cross-panel rendering AND picking use this value.
        ps.sortingOrder = 1000;
        _doc.panelSettings = ps;
        _doc.sortingOrder = 1000;

        _config = FindFirstObjectByType<TuningConfig>();
        _uiConfig = HubPresentationConfig.Load();
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

        // [ / ] cycle scenarios (like the dropdown), unless the user is typing in the search field.
        if (Keyboard.current != null && _scenarioChoices.Count > 1 && !SearchFocused())
        {
            if (Keyboard.current.leftBracketKey.wasPressedThisFrame) CycleScenario(-1);
            else if (Keyboard.current.rightBracketKey.wasPressedThisFrame) CycleScenario(1);
        }

        // Config/player may spawn around the same time as us. We also build before ReplayPlayer
        // loads the JSON, so rebuild whenever the config reloads — otherwise the rows target the
        // pre-reload objects (and the tells list would still be the empty default).
        if (_config == null) _config = FindFirstObjectByType<TuningConfig>();
        if (_config != null && _config.Version != _builtVersion) BuildUI();
        if (HubPresentationConfig.Revision != _builtUiVersion)
        {
            _uiConfig = HubPresentationConfig.Load();
            BuildUI();
        }
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
        PollEvents();
    }

    /// <summary>Push edited data into the live scene.</summary>
    private void Apply()
    {
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
        if (_player != null) _player.ReapplyTuning();
        HubPresentationConfig.NotifyChanged();
    }

    // ---- top-level UI build --------------------------------------------------

    private void BuildUI()
    {
        var root = _doc.rootVisualElement;
        root.Clear();
        // The runtime panel fills the screen. Ignore its otherwise-empty root so it cannot become
        // an invisible click shield; the visible window and all of its controls remain pickable.
        root.pickingMode = PickingMode.Ignore;
        _rows = new List<RowRef>();
        _folds = new List<FoldRef>();
        _builtVersion = _config != null ? _config.Version : -1;
        _builtUiVersion = HubPresentationConfig.Revision;

        _panel = new VisualElement();
        _panel.pickingMode = PickingMode.Position;
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
        BuildTabs();

        _body = new ScrollView(ScrollViewMode.Vertical);
        _body.style.flexGrow = 1;
        _body.style.paddingLeft = 10; _body.style.paddingRight = 10;
        _body.style.paddingTop = 6; _body.style.paddingBottom = 10;
        _panel.Add(_body);

        BuildBody();
        BuildUiEffectsBody();
        BuildEventsBody();
        BuildResizeHandle();

        root.Add(_panel);
        StyleControls();
        UpdateTabVisibility();  // apply the persisted tab (show one body, disable search off-Tuning)
        _panel.style.display = _open ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ---- tab strip -----------------------------------------------------------

    private void BuildTabs()
    {
        var strip = new VisualElement();
        strip.style.flexDirection = FlexDirection.Row;
        strip.style.flexShrink = 0;
        strip.style.paddingLeft = 10; strip.style.paddingRight = 10;
        strip.style.paddingTop = 5; strip.style.paddingBottom = 5;
        strip.style.borderBottomWidth = 1;
        strip.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);

        _tabTuning = new Button(() => SetTab(Tab.Tuning)) { text = "TUNING" };
        _tabUiEffects = new Button(() => SetTab(Tab.UiEffects)) { text = "UI FX" };
        _tabEvents = new Button(() => SetTab(Tab.Events)) { text = "EVENTS" };
        foreach (var b in new[] { _tabTuning, _tabUiEffects, _tabEvents })
        {
            b.style.flexGrow = 1;
            b.style.marginLeft = 0; b.style.marginRight = 4;
            b.style.paddingTop = 3; b.style.paddingBottom = 3;
            b.style.color = TextCol;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            Round(b.style, 4);
        }
        _tabEvents.style.marginRight = 0;
        strip.Add(_tabTuning); strip.Add(_tabUiEffects); strip.Add(_tabEvents);
        _panel.Add(strip);
    }

    private void SetTab(Tab t) { _tab = t; UpdateTabVisibility(); }

    /// <summary>Show the active tab's body, hide the other, and mirror the state onto the tab
    /// buttons + the toolbar search (which only filters the Tuning cockpit).</summary>
    private void UpdateTabVisibility()
    {
        bool tuning = _tab == Tab.Tuning;
        bool ui = _tab == Tab.UiEffects;
        bool events = _tab == Tab.Events;
        if (_body != null) _body.style.display = tuning ? DisplayStyle.Flex : DisplayStyle.None;
        if (_uiBody != null) _uiBody.style.display = ui ? DisplayStyle.Flex : DisplayStyle.None;
        if (_eventsPanel != null) _eventsPanel.style.display = events ? DisplayStyle.Flex : DisplayStyle.None;
        if (_search != null) _search.SetEnabled(!events);
        if (_tabTuning != null) _tabTuning.style.backgroundColor = tuning ? TabActive : TabInactive;
        if (_tabUiEffects != null) _tabUiEffects.style.backgroundColor = ui ? TabActive : TabInactive;
        if (_tabEvents != null) _tabEvents.style.backgroundColor = events ? TabActive : TabInactive;
    }

    /// <summary>
    /// One post-build sweep that dark-themes every stock control. The default runtime theme is
    /// light: field inputs come out as white boxes, so the light text we inherit down the panel is
    /// invisible on them. Restyling the input surfaces here (rather than at each creation site)
    /// keeps the field builders free of theme noise. Everything is built eagerly in BuildUI, so a
    /// single query pass covers the whole tree, collapsed foldouts included.
    /// </summary>
    private void StyleControls()
    {
        _panel.Query(className: "unity-base-field__input").ForEach(input =>
        {
            // Sliders share this class for their drag area; a filled box there just hides the track.
            if (input.ClassListContains("unity-base-slider__input")) return;
            input.style.backgroundColor = InputBg;
            input.style.color = TextCol;
            input.style.borderLeftColor = input.style.borderRightColor =
                input.style.borderTopColor = input.style.borderBottomColor = InputBorder;
            Round(input.style, 3);
            // Inherited color loses to any explicit color the theme puts on the glyph element, so
            // set the typed text directly — otherwise a dark-theme field would read dark-on-dark.
            input.Query<TextElement>().ForEach(t => t.style.color = TextCol);
        });
        // Built-in field labels (slider R/G/B/A, "search") in case the theme colors them explicitly.
        _panel.Query(className: "unity-base-field__label").ForEach(l => l.style.color = TextCol);
        _panel.Query(className: "unity-base-popup-field__arrow").ForEach(a =>
            a.style.unityBackgroundImageTintColor = TextCol);
        _panel.Query(className: "unity-base-slider__tracker").ForEach(t =>
        {
            t.style.backgroundColor = SliderTrack;
            t.style.borderLeftColor = t.style.borderRightColor =
                t.style.borderTopColor = t.style.borderBottomColor = Color.clear;
        });
        _panel.Query(className: "unity-base-slider__dragger").ForEach(d =>
        {
            d.style.backgroundColor = SliderKnob;
            d.style.borderLeftColor = d.style.borderRightColor =
                d.style.borderTopColor = d.style.borderBottomColor = Color.clear;
        });
        // Toggle + foldout arrow: the theme's checkmark art is dark, invisible on a dark panel.
        _panel.Query(className: "unity-toggle__checkmark").ForEach(c =>
        {
            c.style.unityBackgroundImageTintColor = TextCol;
            // The foldout arrow is a bare glyph — only real checkboxes get a box drawn around them.
            if (c.ClassListContains("unity-foldout__checkmark")) return;
            c.style.backgroundColor = InputBg;
            c.style.borderLeftColor = c.style.borderRightColor =
                c.style.borderTopColor = c.style.borderBottomColor = InputBorder;
        });
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
            // Mirror into tuning so the cockpit's Save persists what you hear/see (playback owns speed).
            if (_config != null && _config.data?.playback != null) _config.data.playback.ticksPerSecond = e.newValue;
            spdVal.SetValueWithoutNotify(e.newValue);
        });
        spdVal.RegisterValueChangedCallback(e =>
        {
            if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
            if (_player != null) _player.ticksPerSecond = e.newValue;
            if (_config != null && _config.data?.playback != null) _config.data.playback.ticksPerSecond = e.newValue;
            spd.SetValueWithoutNotify(e.newValue);
        });
        row2.Add(spdLabel); row2.Add(spd); row2.Add(spdVal);
        bar.Add(row2);

        // Row 3: scenario picker (dropdown of StreamingAssets/replays/*.bytes; [ / ] cycle it too)
        var row3 = HRow();
        var scLabel = FieldLabel("scenario");
        _scenarioChoices = ScenarioChoices();
        _scenarioDrop = new DropdownField(_scenarioChoices, CurrentScenarioIndex());
        _scenarioDrop.style.flexGrow = 1;
        _scenarioDrop.RegisterValueChangedCallback(e =>
        {
            if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
            if (_player != null && !string.IsNullOrEmpty(e.newValue)) _player.LoadScenario(e.newValue);
        });
        row3.Add(scLabel); row3.Add(_scenarioDrop);
        bar.Add(row3);

        _panel.Add(bar);
    }

    // ---- scenario picker -----------------------------------------------------

    /// <summary>Relative paths ("replays/foo.bytes") for every *.bytes under StreamingAssets/replays,
    /// plus the player's current replayFile if it lives elsewhere, so the dropdown always shows it.</summary>
    private List<string> ScenarioChoices()
    {
        var list = new List<string>();
        try
        {
            var dir = Path.Combine(Application.streamingAssetsPath, "replays");
            if (Directory.Exists(dir))
                foreach (var p in Directory.GetFiles(dir, "*.bytes"))
                    list.Add("replays/" + Path.GetFileName(p));
            list.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e) { Debug.LogWarning($"[DebugMenu] scenario scan failed ({e.Message})"); }
        string cur = _player != null ? _player.replayFile : null;
        if (!string.IsNullOrEmpty(cur) && !list.Contains(cur)) list.Insert(0, cur);
        return list;
    }

    private int CurrentScenarioIndex()
    {
        string cur = _player != null ? _player.replayFile : null;
        int i = cur != null ? _scenarioChoices.IndexOf(cur) : -1;
        return i < 0 ? 0 : i;
    }

    /// <summary>Step the scenario selection by <paramref name="dir"/> and load it, keeping the
    /// dropdown label in sync. Bound to [ / ] in Update (skipped while the search field is focused).</summary>
    private void CycleScenario(int dir)
    {
        if (_player == null) _player = FindFirstObjectByType<ReplayPlayer>();
        if (_player == null || _scenarioChoices.Count == 0) return;
        int idx = _scenarioChoices.IndexOf(_player.replayFile);
        if (idx < 0) idx = 0;
        idx = (idx + dir + _scenarioChoices.Count) % _scenarioChoices.Count;
        string next = _scenarioChoices[idx];
        _player.LoadScenario(next);
        _scenarioDrop?.SetValueWithoutNotify(next);
    }

    /// <summary>True when keyboard focus is inside the search field (so [ / ] type literally instead
    /// of cycling). Walks up from the focused element — a TextField focuses its inner text element.</summary>
    private bool SearchFocused()
    {
        if (_search == null || _search.panel == null) return false;
        var f = _search.panel.focusController?.focusedElement as VisualElement;
        while (f != null) { if (f == _search) return true; f = f.parent; }
        return false;
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

    private void BuildUiEffectsBody()
    {
        _uiBody = new ScrollView(ScrollViewMode.Vertical);
        _uiBody.style.flexGrow = 1;
        _uiBody.style.paddingLeft = 10; _uiBody.style.paddingRight = 10;
        _uiBody.style.paddingTop = 8; _uiBody.style.paddingBottom = 10;
        _panel.Add(_uiBody);

        var title = new Label("HALL PRESENTATION RECIPES");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 12;
        title.style.color = new Color(0.95f, 0.79f, 0.42f);
        title.style.marginBottom = 3;
        _uiBody.Add(title);

        var note = new Label(
            "Live edits affect the next interaction. Preview on the Hourstone Table; Save writes " +
            "Resources/UI/HubPresentation.json.");
        note.style.whiteSpace = WhiteSpace.Normal;
        note.style.fontSize = 11;
        note.style.color = Muted;
        note.style.marginBottom = 7;
        _uiBody.Add(note);

        var previews = new VisualElement();
        previews.style.flexDirection = FlexDirection.Row;
        previews.style.flexWrap = Wrap.Wrap;
        previews.style.marginBottom = 8;
        AddUiPreviewButton(previews, "REVEAL", UiPolishSignals.Cue.Reveal);
        AddUiPreviewButton(previews, "HOVER", UiPolishSignals.Cue.Preview);
        AddUiPreviewButton(previews, "SELECT", UiPolishSignals.Cue.Select);
        AddUiPreviewButton(previews, "COMMIT", UiPolishSignals.Cue.Purchase);
        AddUiPreviewButton(previews, "REROLL", UiPolishSignals.Cue.Reroll);
        AddUiPreviewButton(previews, "ROUTE", UiPolishSignals.Cue.Route);
        AddUiPreviewButton(previews, "ATTENTION", UiPolishSignals.Cue.Attention);
        AddUiPreviewButton(previews, "ERROR", UiPolishSignals.Cue.Error);
        AddUiPreviewButton(previews, "BUY RANK", UiTransactionKind.BuyRank);
        AddUiPreviewButton(previews, "BUY GEAR", UiTransactionKind.BuyWeapon);
        AddUiPreviewButton(previews, "BIND", UiTransactionKind.BindInscription);
        AddUiPreviewButton(previews, "EQUIP", UiTransactionKind.Equip);
        AddUiPreviewButton(previews, "FORGE", UiTransactionKind.Reforge);
        AddUiPreviewButton(previews, "MUSTER +", UiTransactionKind.MusterSelect);
        AddUiPreviewButton(previews, "MUSTER −", UiTransactionKind.MusterDeselect);
        _uiBody.Add(previews);

        if (_uiConfig == null)
        {
            var wait = new Label("Waiting for UI presentation configuration…");
            wait.style.color = Muted;
            _uiBody.Add(wait);
            return;
        }

        BuildObject(_uiConfig, _uiBody, "ui", new List<FoldRef>());
    }

    private static void AddUiPreviewButton(VisualElement row, string text,
                                           UiPolishSignals.Cue cue)
    {
        var button = new Button(() => UiPolishSignals.Preview(cue)) { text = text };
        button.style.minWidth = 92;
        button.style.marginLeft = 0;
        button.style.marginRight = 4;
        button.style.marginBottom = 4;
        button.style.paddingLeft = 6;
        button.style.paddingRight = 6;
        button.style.color = TextCol;
        button.style.backgroundColor = new Color(0.20f, 0.24f, 0.32f);
        Round(button.style, 4);
        row.Add(button);
    }

    private static void AddUiPreviewButton(VisualElement row, string text,
                                           UiTransactionKind transaction)
    {
        var button = new Button(() => UiPolishSignals.Preview(transaction)) { text = text };
        button.style.minWidth = 92;
        button.style.marginLeft = 0;
        button.style.marginRight = 4;
        button.style.marginBottom = 4;
        button.style.paddingLeft = 6;
        button.style.paddingRight = 6;
        button.style.color = TextCol;
        button.style.backgroundColor = new Color(0.27f, 0.21f, 0.13f);
        Round(button.style, 4);
        row.Add(button);
    }

    // ---- events tab ----------------------------------------------------------

    /// <summary>The EVENTS body: a controls row (follow / noise toggles + substring filter) over a
    /// scroll of color-coded event lines, newest at the bottom. Lines are appended live in
    /// <see cref="PollEvents"/>; this only builds the empty shell and resets the poll cursor so the
    /// next poll repopulates from the player's ring buffer (survives a BuildUI rebuild for free).</summary>
    private void BuildEventsBody()
    {
        _eventsPanel = new VisualElement();
        _eventsPanel.style.flexGrow = 1;
        _eventsPanel.style.flexDirection = FlexDirection.Column;
        _eventsPanel.style.overflow = Overflow.Hidden;

        var ctrl = HRow();
        ctrl.style.flexShrink = 0;
        ctrl.style.paddingLeft = 10; ctrl.style.paddingRight = 10;
        ctrl.style.paddingTop = 6; ctrl.style.paddingBottom = 4;

        _followToggle = new Toggle("follow") { value = true };
        _followToggle.tooltip = "keep scrolled to the newest line";
        _followToggle.style.marginRight = 12;
        _noiseToggle = new Toggle("noise") { value = false };
        _noiseToggle.tooltip = "show Move/Mana/FieldHex/BattleStart spam";
        _noiseToggle.style.marginRight = 12;
        _eventFilter = new TextField();
        _eventFilter.style.flexGrow = 1;
        _eventFilter.tooltip = "filter event lines (case-insensitive)";
        _eventFilter.RegisterValueChangedCallback(_ => RefreshEventVisibility());
        _noiseToggle.RegisterValueChangedCallback(_ => RefreshEventVisibility());

        ctrl.Add(_followToggle); ctrl.Add(_noiseToggle); ctrl.Add(_eventFilter);
        _eventsPanel.Add(ctrl);

        _eventsScroll = new ScrollView(ScrollViewMode.Vertical);
        _eventsScroll.style.flexGrow = 1;
        _eventsScroll.style.paddingLeft = 10; _eventsScroll.style.paddingRight = 10;
        _eventsScroll.style.paddingBottom = 8;
        _eventsContent = _eventsScroll.contentContainer;
        _eventsPanel.Add(_eventsScroll);

        _panel.Add(_eventsPanel);

        _eventRows.Clear();
        _lastSeq = 0;   // force the next poll to re-fold the whole ring buffer into the fresh list
    }

    /// <summary>Fold new events into the list once per frame. Keyed on ReplayPlayer.EventSeq (the
    /// same cheap-poll pattern as _config.Version): if it dropped, the buffer was cleared (fight
    /// switch) so we resync from scratch; then append only the entries newer than we last saw.</summary>
    private void PollEvents()
    {
        if (_player == null || _eventsContent == null) return;
        int seq = _player.EventSeq;
        if (seq == _lastSeq) return;

        if (seq < _lastSeq) { ClearEventLines(); _lastSeq = 0; } // fight reset: drop stale lines

        var recent = _player.RecentEvents;
        int newCount = Mathf.Clamp(seq - _lastSeq, 0, recent.Count);
        for (int i = recent.Count - newCount; i < recent.Count; i++)
            AppendEventLine(recent[i]);
        _lastSeq = seq;

        TrimEventLines();
        if (_followToggle != null && _followToggle.value) ScrollEventsToBottom();
    }

    private void AppendEventLine(BattleEvent e)
    {
        string line = $"t{e.Tick,3}  {EventText.Describe(e, EventName)}";
        var lbl = new Label(line);
        lbl.style.fontSize = 11;
        lbl.style.whiteSpace = WhiteSpace.Normal;   // long lines wrap rather than clip
        lbl.style.marginBottom = 1;
        lbl.style.color = EventColor(e);
        if (e.Kind == EventKind.Death) lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

        var row = new EventRow { El = lbl, Noise = EventText.IsNoise(e), Lower = line.ToLowerInvariant() };
        _eventRows.Add(row);
        _eventsContent.Add(lbl);
        ApplyEventRowVisibility(row);
    }

    /// <summary>Names for Describe: fold name, falling back to "storm" for the environment (-1) and
    /// "#id" for anything not in the fold.</summary>
    private string EventName(int id)
    {
        string n = _player != null ? _player.UnitName(id) : null;
        return n ?? (id < 0 ? "storm" : $"#{id}");
    }

    private void ApplyEventRowVisibility(EventRow r)
    {
        bool showNoise = _noiseToggle != null && _noiseToggle.value;
        string q = _eventFilter != null ? (_eventFilter.value ?? "").Trim().ToLowerInvariant() : "";
        bool pass = (showNoise || !r.Noise) && (q.Length == 0 || r.Lower.Contains(q));
        r.El.style.display = pass ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshEventVisibility()
    {
        foreach (var r in _eventRows) ApplyEventRowVisibility(r);
        if (_followToggle != null && _followToggle.value) ScrollEventsToBottom();
    }

    private void ClearEventLines()
    {
        _eventRows.Clear();
        _eventsContent?.Clear();
    }

    /// <summary>Cap the rendered lines to the ring-buffer size, dropping the oldest — the visible
    /// list and the player's buffer trim in lockstep.</summary>
    private void TrimEventLines()
    {
        while (_eventRows.Count > 256)
        {
            _eventsContent.RemoveAt(0);
            _eventRows.RemoveAt(0);
        }
    }

    /// <summary>Pin the scroll to the newest line. Uses the vertical Scroller (robust to filtered/
    /// hidden trailing rows); highValue settles a frame after a layout, and we poll every frame.</summary>
    private void ScrollEventsToBottom()
    {
        var scroller = _eventsScroll?.verticalScroller;
        if (scroller != null) scroller.value = scroller.highValue;
    }

    private static Color EventColor(BattleEvent e)
    {
        switch (e.Kind)
        {
            case EventKind.DamageDealt:
                return e.Crit ? EvCrit : e.Cause == Cause.Burn ? EvBurn : EvDamage;
            case EventKind.Heal: return EvHeal;
            case EventKind.Cast: return EvCast;
            case EventKind.StatusApplied:
            case EventKind.StatusExpired: return EvStatus;
            case EventKind.Death:
            case EventKind.CheatDeath: return EvDeath;
            case EventKind.AttackBlocked: return EvBlocked;
            case EventKind.FieldCreated:
                switch (e.Flavor)
                {
                    case FieldFlavor.Hazard: return EvFieldHazard;
                    case FieldFlavor.Boon: return EvFieldBoon;
                    case FieldFlavor.Debuff: return EvFieldDebuff;
                    default: return EvFieldElse;
                }
            default: return Muted;
        }
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
        HubPresentationConfig.Save();
        SaveRanges();
    }

    private void OnReload()
    {
        if (_config != null) _config.LoadFromJson();
        HubPresentationConfig.Reload();
        _uiConfig = HubPresentationConfig.Load();
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
