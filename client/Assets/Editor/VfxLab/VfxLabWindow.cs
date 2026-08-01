#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Sim;

/// <summary>
/// Dockable PC authoring surface for every combat/Revision effect. The browser discovers code
/// recipes, applied recipe assets, replay fixtures, and optional scenario bookmarks automatically.
/// Nothing writes until an Apply button is pressed.
/// </summary>
public sealed class VfxLabWindow : EditorWindow
{
    private enum BrowserMode
    {
        Recipes,
        Combat,
        Revision,
        Bookmarks,
    }

    private enum EntryKind
    {
        Recipe,
        Fixture,
        Revision,
        Scenario,
    }

    private sealed class BrowserEntry
    {
        internal EntryKind Kind;
        internal string Key;
        internal string Label;
        internal string Note;
        internal UnityEngine.Object Asset;
        internal RevisionEffectKind RevisionLineage;
        internal bool ReducedMotion;
    }

    private sealed class TellBinding
    {
        internal int Index;
        internal string Slot;
    }

    private sealed class AudioCue
    {
        internal string Id;
        internal string AssetPath;
        internal bool Ui;
    }

    [SerializeField] private BrowserMode browserMode = BrowserMode.Recipes;
    [SerializeField] private string recipeCategory = "All";
    [SerializeField] private string lastSelectedKey = "recipe:revision-land-future";
    [SerializeField] private bool loop = true;
    [SerializeField] private float playbackSpeed = 1f;
    [SerializeField] private VfxLabEnvironmentMode environment =
        VfxLabEnvironmentMode.ProductionShard;
    [SerializeField] private VfxLabRecipeContext recipeContext =
        VfxLabRecipeContext.AtTarget;
    [SerializeField] private Color previewColor = Color.white;
    [SerializeField] private float previewGlow = VfxLibrary.GlowRef;
    [SerializeField] private float previewScale = 1f;
    [SerializeField] private RevisionEffectKind revisionLineage =
        RevisionEffectKind.BorrowedFuture;
    [SerializeField] private bool revisionFullRupture = true;
    [SerializeField] private bool revisionReducedMotion;
    [SerializeField] private int revisionPresentTick = 40;
    [SerializeField] private int revisionBranchTick = 20;
    [SerializeField] private string revisionFixture = "replays/hourstone.bytes";
    [SerializeField] private int selectedAudioCue;
    [SerializeField] private VfxLabAudioBus audioBus = VfxLabAudioBus.State;
    [SerializeField] private float audioVolume = 0.8f;

    private readonly List<BrowserEntry> _entries = new List<BrowserEntry>();
    private readonly List<TellBinding> _bindings = new List<TellBinding>();
    private readonly Dictionary<string, VfxLabFixtureInfo> _fixtureCache =
        new Dictionary<string, VfxLabFixtureInfo>(StringComparer.Ordinal);
    private readonly List<AudioCue> _audioCues = new List<AudioCue>();

    private VfxLabStage _stage;
    private RenderTexture _renderTexture;
    private Image _viewportImage;
    private Label _viewportOverlay;
    private Label _sourceBadge;
    private Label _dirtyBadge;
    private Label _status;
    private Button _playButton;
    private Slider _timeline;
    private Label _timeLabel;
    private ScrollView _browserScroll;
    private ScrollView _inspectorScroll;
    private IMGUIContainer _inspectorGui;
    private ToolbarSearchField _search;
    private ToolbarMenu _modeMenu;
    private ToolbarMenu _categoryMenu;
    private EnumField _environmentField;

    private BrowserEntry _selectedEntry;
    private VfxRecipeAsset _recipeDraft;
    private SerializedObject _recipeSerialized;
    private string _recipeId;
    private int _selectedElement;
    private bool _recipeDirty;
    private VfxLabTellDraft _tellDraft;
    private SerializedObject _tellSerialized;
    private bool _tellDirty;
    private VfxLabRevisionDraft _revisionDraft;
    private SerializedObject _revisionSerialized;
    private bool _revisionDirty;
    private VfxLabFixtureInfo _fixtureInfo;
    private bool _playing;
    private double _lastUpdate;
    private double _lastRender;
    private bool _needsRender = true;

    [MenuItem("Warband/VFX Lab/VFX Lab", priority = 99)]
    public static void ShowWindow()
    {
        VfxLabWindow window = GetWindow<VfxLabWindow>();
        window.titleContent = new GUIContent("VFX Lab");
        window.minSize = new Vector2(1050f, 640f);
        window.Show();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.AddToClassList("vfx-lab");
        rootVisualElement.style.flexGrow = 1f;
        StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            VfxLabPaths.StyleSheet);
        if (style != null) rootVisualElement.styleSheets.Add(style);

        BuildHeader();
        BuildToolbar();
        BuildMainArea();
        BuildStatus();
        ConnectStage();
        LoadAudioCues();
        UpdateStatusInventory();
        RebuildBrowser();

        _lastUpdate = EditorApplication.timeSinceStartup;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        rootVisualElement.schedule.Execute(RestoreSelection).ExecuteLater(40);
    }

    private void BuildHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("vfx-lab__header");
        header.Add(new Label("VFX LAB // TEMPORAL FORGE")
        {
            tooltip = "Combat + Revision effect authoring. PC editor first.",
        }.WithClass("vfx-lab__title"));
        header.Add(new Label("shipping runtime · deterministic scrub · explicit apply")
            .WithClass("vfx-lab__subtitle"));
        header.Add(new VisualElement().WithClass("vfx-lab__grow"));
        _sourceBadge = new Label("NO SELECTION");
        _sourceBadge.AddToClassList("vfx-lab__badge");
        header.Add(_sourceBadge);
        _dirtyBadge = new Label("CLEAN");
        _dirtyBadge.AddToClassList("vfx-lab__badge");
        header.Add(_dirtyBadge);
        rootVisualElement.Add(header);
    }

    private void BuildToolbar()
    {
        var toolbar = new VisualElement();
        toolbar.AddToClassList("vfx-lab__toolbar");
        _playButton = new Button(TogglePlay) { text = "▶", tooltip = "Play / pause" };
        toolbar.Add(_playButton);
        toolbar.Add(new Button(() =>
        {
            _playing = false;
            _playButton.text = "▶";
            EvaluateAt(0f);
        })
        {
            text = "↺",
            tooltip = "Restart",
        });
        toolbar.Add(new Button(() =>
        {
            _playing = false;
            _playButton.text = "▶";
            float step = _stage != null && _stage.Mode == VfxLabStageMode.CombatFixture
                ? 0.1f
                : 1f / 60f;
            EvaluateAt((_stage?.CurrentTime ?? 0f) + step);
        })
        {
            text = "▸|",
            tooltip = "Step one preview frame (one combat beat in fixture mode)",
        });

        var speed = new ToolbarMenu { text = $"Speed {playbackSpeed:0.##}×" };
        foreach (float value in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
        {
            float captured = value;
            speed.menu.AppendAction(
                $"{value:0.##}×",
                _ =>
                {
                    playbackSpeed = captured;
                    speed.text = $"Speed {playbackSpeed:0.##}×";
                },
                _ => Mathf.Approximately(playbackSpeed, captured)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }
        toolbar.Add(speed);

        var loopToggle = new ToolbarToggle { text = "Loop", value = loop };
        loopToggle.RegisterValueChangedCallback(evt => loop = evt.newValue);
        toolbar.Add(loopToggle);

        toolbar.Add(new VisualElement().WithClass("vfx-lab__grow"));
        _environmentField = new EnumField(environment)
        {
            tooltip = "A/B the live shard, a neutral value studio, or effect isolation.",
        };
        _environmentField.style.width = 178f;
        _environmentField.RegisterValueChangedCallback(evt =>
        {
            environment = (VfxLabEnvironmentMode)evt.newValue;
            _stage?.SetEnvironment(environment);
            RequestRender();
        });
        toolbar.Add(new Label("Environment"));
        toolbar.Add(_environmentField);
        toolbar.Add(new Button(() =>
        {
            ConnectStage();
            if (_stage == null) VfxLabSceneTools.OpenScene();
            else EditorGUIUtility.PingObject(_stage.gameObject);
        })
        {
            text = "SCENE",
            tooltip = "Open or reveal the dedicated VfxLab scene.",
        });
        rootVisualElement.Add(toolbar);
    }

    private void BuildMainArea()
    {
        var leftSplit = new TwoPaneSplitView(
            0, 244f, TwoPaneSplitViewOrientation.Horizontal);
        leftSplit.style.flexGrow = 1f;
        var rightSplit = new TwoPaneSplitView(
            1, 402f, TwoPaneSplitViewOrientation.Horizontal);
        rightSplit.style.flexGrow = 1f;

        VisualElement sidebar = BuildSidebar();
        VisualElement center = BuildViewport();
        VisualElement inspector = BuildInspector();
        rightSplit.Add(center);
        rightSplit.Add(inspector);
        leftSplit.Add(sidebar);
        leftSplit.Add(rightSplit);
        rootVisualElement.Add(leftSplit);
    }

    private VisualElement BuildSidebar()
    {
        var sidebar = new VisualElement();
        sidebar.AddToClassList("vfx-lab__sidebar");
        sidebar.style.minWidth = 190f;

        _modeMenu = new ToolbarMenu { text = browserMode.ToString() };
        _modeMenu.style.marginLeft = 6f;
        _modeMenu.style.marginRight = 6f;
        _modeMenu.style.marginTop = 7f;
        foreach (BrowserMode mode in Enum.GetValues(typeof(BrowserMode)))
        {
            BrowserMode captured = mode;
            _modeMenu.menu.AppendAction(
                mode.ToString(),
                _ => SetBrowserMode(captured),
                _ => browserMode == captured
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }
        sidebar.Add(_modeMenu);

        _search = new ToolbarSearchField();
        _search.style.marginLeft = 6f;
        _search.style.marginRight = 6f;
        _search.RegisterValueChangedCallback(_ => RebuildBrowser());
        sidebar.Add(_search);

        _categoryMenu = new ToolbarMenu { text = $"Filter: {recipeCategory}" };
        _categoryMenu.style.marginLeft = 6f;
        _categoryMenu.style.marginRight = 6f;
        foreach (string category in new[]
                 {
                     "All", "Weapons", "Casts", "Ground + Field",
                     "Revision", "Systems", "Unbound",
                 })
        {
            string captured = category;
            _categoryMenu.menu.AppendAction(
                category,
                _ =>
                {
                    recipeCategory = captured;
                    _categoryMenu.text = $"Filter: {recipeCategory}";
                    RebuildBrowser();
                },
                _ => recipeCategory == captured
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
        }
        sidebar.Add(_categoryMenu);

        var titleRow = new VisualElement();
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.Add(new Label("CONTENT").WithClass("vfx-lab__section-title"));
        titleRow.Add(new VisualElement().WithClass("vfx-lab__grow"));
        var create = new Button(CreateNewRecipe) { text = "+", tooltip = "New recipe asset" };
        create.style.width = 28f;
        create.style.marginRight = 7f;
        create.style.marginTop = 5f;
        titleRow.Add(create);
        sidebar.Add(titleRow);

        _browserScroll = new ScrollView();
        _browserScroll.style.flexGrow = 1f;
        sidebar.Add(_browserScroll);
        return sidebar;
    }

    private VisualElement BuildViewport()
    {
        var column = new VisualElement();
        column.style.flexGrow = 1f;
        var viewport = new VisualElement();
        viewport.AddToClassList("vfx-lab__viewport");
        viewport.style.flexGrow = 1f;
        _viewportImage = new Image
        {
            scaleMode = ScaleMode.ScaleToFit,
            pickingMode = PickingMode.Ignore,
        };
        _viewportImage.AddToClassList("vfx-lab__viewport-image");
        viewport.Add(_viewportImage);
        _viewportOverlay = new Label("Connect the VfxLab scene.");
        _viewportOverlay.AddToClassList("vfx-lab__viewport-overlay");
        viewport.Add(_viewportOverlay);
        column.Add(viewport);

        var timelineBlock = new VisualElement();
        timelineBlock.AddToClassList("vfx-lab__timeline");
        var timelineRow = new VisualElement();
        timelineRow.AddToClassList("vfx-lab__timeline-row");
        _timeline = new Slider(0f, 1f) { value = 0f };
        _timeline.RegisterValueChangedCallback(evt =>
        {
            if (_stage == null) return;
            _playing = false;
            _playButton.text = "▶";
            _stage.Evaluate(evt.newValue);
            UpdateTransport();
            RequestRender();
        });
        _timeLabel = new Label("0.00 / 1.00 s");
        _timeLabel.AddToClassList("vfx-lab__time-label");
        timelineRow.Add(_timeline);
        timelineRow.Add(_timeLabel);
        timelineBlock.Add(timelineRow);
        timelineBlock.Add(new Label(
            "Drag for deterministic evaluation. Fixture mode snaps to replay ticks; recipe mode steps particles at 60 Hz.")
        {
            style =
            {
                color = new StyleColor(new Color(0.46f, 0.52f, 0.60f)),
                fontSize = 9f,
            },
        });
        column.Add(timelineBlock);
        return column;
    }

    private VisualElement BuildInspector()
    {
        var inspector = new VisualElement();
        inspector.AddToClassList("vfx-lab__inspector");
        inspector.style.minWidth = 310f;
        inspector.Add(new Label("CONTEXTUAL TUNING")
            .WithClass("vfx-lab__section-title"));
        _inspectorScroll = new ScrollView();
        _inspectorScroll.AddToClassList("vfx-lab__inspector-scroll");
        _inspectorScroll.style.flexGrow = 1f;
        _inspectorGui = new IMGUIContainer(DrawInspector);
        _inspectorScroll.Add(_inspectorGui);
        inspector.Add(_inspectorScroll);
        return inspector;
    }

    private void BuildStatus()
    {
        _status = new Label("VFX Lab starting…");
        _status.AddToClassList("vfx-lab__status");
        rootVisualElement.Add(_status);
    }

    private void ConnectStage()
    {
        VfxLabStage found = UnityEngine.Object.FindFirstObjectByType<VfxLabStage>();
        if (found == _stage && _renderTexture != null) return;
        if (_stage != null && _renderTexture != null)
            _stage.DetachRenderTarget(_renderTexture);
        _stage = found;
        EnsureRenderTexture();
        if (_stage != null)
        {
            _stage.Initialize();
            _stage.AttachRenderTarget(_renderTexture);
            _stage.SetEnvironment(environment);
            _viewportOverlay.text = "READY";
            EnsureRevisionDraft();
            RequestRender();
        }
        else
        {
            _viewportOverlay.text =
                "VFX LAB SCENE NOT LOADED · click SCENE to open it";
        }
        UpdateStatusInventory();
    }

    private void EnsureRenderTexture()
    {
        if (_renderTexture != null) return;
        _renderTexture = new RenderTexture(
            1280, 720, 24, RenderTextureFormat.ARGBHalf)
        {
            name = "~VfxLabViewport",
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _renderTexture.Create();
        if (_viewportImage != null) _viewportImage.image = _renderTexture;
    }

    private void SetBrowserMode(BrowserMode mode)
    {
        browserMode = mode;
        _modeMenu.text = mode.ToString();
        _categoryMenu.style.display =
            mode == BrowserMode.Recipes ? DisplayStyle.Flex : DisplayStyle.None;
        RebuildBrowser();
        SelectFirstVisible();
    }

    private void RebuildBrowser()
    {
        if (_browserScroll == null) return;
        _entries.Clear();
        _browserScroll.Clear();
        string search = (_search?.value ?? "").Trim();
        switch (browserMode)
        {
            case BrowserMode.Combat:
                BuildFixtureEntries(search);
                break;
            case BrowserMode.Revision:
                BuildRevisionEntries(search);
                break;
            case BrowserMode.Bookmarks:
                BuildScenarioEntries(search);
                break;
            default:
                BuildRecipeEntries(search);
                break;
        }

        foreach (BrowserEntry entry in _entries)
            _browserScroll.Add(BuildBrowserRow(entry));
        if (_entries.Count == 0)
            _browserScroll.Add(new Label("No matching content.")
                .WithClass("vfx-lab__empty"));
    }

    private void BuildRecipeEntries(string search)
    {
        foreach (string id in VfxLibrary.AllIds)
        {
            if (!MatchesSearch(id, search) || !MatchesRecipeCategory(id)) continue;
            int bindings = CountBindings(id);
            string source = VfxLibrary.HasAssetOverride(id) ? "override" : "built-in";
            _entries.Add(new BrowserEntry
            {
                Kind = EntryKind.Recipe,
                Key = "recipe:" + id,
                Label = id,
                Note = $"{source} · {bindings} tell binding{(bindings == 1 ? "" : "s")}",
            });
        }
    }

    private void BuildFixtureEntries(string search)
    {
        string folder = Path.Combine(Application.streamingAssetsPath, "replays");
        if (!Directory.Exists(folder)) return;
        foreach (string path in Directory.GetFiles(folder, "*.bytes").OrderBy(v => v))
        {
            string relative = "replays/" + Path.GetFileName(path);
            string label = Path.GetFileNameWithoutExtension(path);
            if (!MatchesSearch(label, search)) continue;
            VfxLabFixtureInfo info = GetFixtureInfo(relative);
            _entries.Add(new BrowserEntry
            {
                Kind = EntryKind.Fixture,
                Key = "fixture:" + relative,
                Label = label,
                Note = info != null
                    ? $"{info.UnitCount} units · {info.EventCount} events · {info.EndTick} ticks"
                    : "fixture could not be inspected",
            });
        }
    }

    private void BuildRevisionEntries(string search)
    {
        AddRevisionEntry(
            "Borrowed Future · full ceremony",
            RevisionEffectKind.BorrowedFuture,
            false,
            search);
        AddRevisionEntry(
            "Recall to Formation · full ceremony",
            RevisionEffectKind.RecallToFormation,
            false,
            search);
        AddRevisionEntry(
            "Reduced Motion · Borrowed Future",
            RevisionEffectKind.BorrowedFuture,
            true,
            search);
    }

    private void AddRevisionEntry(
        string label,
        RevisionEffectKind lineage,
        bool reduced,
        string search)
    {
        if (!MatchesSearch(label, search)) return;
        _entries.Add(new BrowserEntry
        {
            Kind = EntryKind.Revision,
            Key = $"revision:{lineage}:{reduced}",
            Label = label,
            Note = "split → held Hour → tear → rewind → vacuum → landing → receipt",
            RevisionLineage = lineage,
            ReducedMotion = reduced,
        });
    }

    private void BuildScenarioEntries(string search)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:VfxLabScenarioAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            VfxLabScenarioAsset scenario =
                AssetDatabase.LoadAssetAtPath<VfxLabScenarioAsset>(path);
            if (scenario == null || !MatchesSearch(scenario.displayName, search)) continue;
            _entries.Add(new BrowserEntry
            {
                Kind = EntryKind.Scenario,
                Key = "scenario:" + guid,
                Label = string.IsNullOrWhiteSpace(scenario.displayName)
                    ? scenario.name
                    : scenario.displayName,
                Note = $"{scenario.kind} · {scenario.environment}",
                Asset = scenario,
            });
        }
    }

    private VisualElement BuildBrowserRow(BrowserEntry entry)
    {
        var button = new Button(() => SelectEntry(entry));
        button.AddToClassList("vfx-lab__browser-row");
        if (_selectedEntry != null && _selectedEntry.Key == entry.Key)
            button.AddToClassList("vfx-lab__browser-row--selected");
        button.style.flexDirection = FlexDirection.Column;
        button.style.alignItems = Align.FlexStart;
        button.Add(new Label(entry.Label));
        button.Add(new Label(entry.Note).WithClass("vfx-lab__row-note"));
        return button;
    }

    private void SelectEntry(BrowserEntry entry)
    {
        if (entry == null) return;
        if (_selectedEntry != null &&
            _selectedEntry.Key != entry.Key &&
            !ConfirmDiscardDrafts())
            return;
        _selectedEntry = entry;
        lastSelectedKey = entry.Key;
        _playing = false;
        _playButton.text = "▶";
        ClearSelectionDrafts(entry.Kind == EntryKind.Recipe);

        switch (entry.Kind)
        {
            case EntryKind.Fixture:
                _fixtureInfo = GetFixtureInfo(entry.Key.Substring("fixture:".Length));
                _stage?.SelectFixture(_fixtureInfo?.RelativePath, 0);
                _sourceBadge.text = "REPLAY FIXTURE";
                break;
            case EntryKind.Revision:
                revisionLineage = entry.RevisionLineage;
                revisionReducedMotion = entry.ReducedMotion;
                ConfigureRevisionStage();
                _sourceBadge.text = "FLAGSHIP SEQUENCE";
                break;
            case EntryKind.Scenario:
                _stage?.SelectScenario(entry.Asset as VfxLabScenarioAsset);
                _sourceBadge.text = "SCENARIO ASSET";
                break;
            default:
                LoadRecipeDraft(entry.Key.Substring("recipe:".Length));
                break;
        }
        _stage?.SetEnvironment(environment);
        UpdateTimelineRange();
        RebuildBrowser();
        UpdateDirtyBadge();
        _inspectorGui?.MarkDirtyRepaint();
        RequestRender();
    }

    private void RestoreSelection()
    {
        ConnectStage();
        BrowserEntry entry = _entries.FirstOrDefault(item => item.Key == lastSelectedKey);
        if (entry == null && _entries.Count > 0) entry = _entries[0];
        if (entry != null) SelectEntry(entry);
    }

    private void SelectFirstVisible()
    {
        if (_entries.Count > 0) SelectEntry(_entries[0]);
    }

    private void LoadRecipeDraft(string id)
    {
        if (_recipeDraft != null) UnityEngine.Object.DestroyImmediate(_recipeDraft);
        VfxLibrary.ClearPreviewOverride(_recipeId);
        _recipeId = id;
        VfxDef source = VfxLibrary.Get(id);
        if (source == null)
        {
            SetStatus($"Recipe '{id}' does not resolve.");
            return;
        }
        _recipeDraft = VfxRecipeAsset.CreateDraft(source);
        _recipeSerialized = new SerializedObject(_recipeDraft);
        _selectedElement = 0;
        _recipeDirty = false;
        RebuildBindings();
        previewColor = Color.white;
        previewGlow = VfxLibrary.GlowRef;
        previewScale = 1f;
        recipeContext = GuessContext(id);
        VfxLibrary.SetPreviewOverride(id, _recipeDraft.Compile());
        _stage?.SelectRecipe(
            VfxLibrary.Get(id),
            recipeContext,
            previewColor,
            previewGlow,
            previewScale);
        _sourceBadge.text = VfxLibrary.HasAssetOverride(id)
            ? "ASSET OVERRIDE"
            : "C# BUILT-IN";
        SetStatus(
            $"{id} · {_recipeDraft.elements.Count} elements · {_bindings.Count} contextual tell bindings");
    }

    private void ClearSelectionDrafts(bool keepRecipe)
    {
        if (!keepRecipe)
        {
            VfxLibrary.ClearPreviewOverride(_recipeId);
            if (_recipeDraft != null) UnityEngine.Object.DestroyImmediate(_recipeDraft);
            _recipeDraft = null;
            _recipeSerialized = null;
            _recipeId = null;
            _recipeDirty = false;
        }
        if (_tellDraft != null) UnityEngine.Object.DestroyImmediate(_tellDraft);
        _tellDraft = null;
        _tellSerialized = null;
        _tellDirty = false;
        _fixtureInfo = null;
    }

    private void RebuildBindings()
    {
        _bindings.Clear();
        if (_stage?.Tuning?.data?.tells == null || string.IsNullOrEmpty(_recipeId)) return;
        List<TellDef> tells = _stage.Tuning.data.tells;
        for (int i = 0; i < tells.Count; i++)
        {
            TellDef tell = tells[i];
            if (tell.vfx == _recipeId) _bindings.Add(new TellBinding { Index = i, Slot = "source" });
            if (tell.projectileVfx == _recipeId)
                _bindings.Add(new TellBinding { Index = i, Slot = "projectile" });
            if (tell.impactVfx == _recipeId)
                _bindings.Add(new TellBinding { Index = i, Slot = "impact" });
            if (tell.groundVfx == _recipeId)
                _bindings.Add(new TellBinding { Index = i, Slot = "ground" });
        }
    }

    private int CountBindings(string id)
    {
        if (_stage?.Tuning?.data?.tells == null) return 0;
        int count = 0;
        foreach (TellDef tell in _stage.Tuning.data.tells)
        {
            if (tell.vfx == id) count++;
            if (tell.projectileVfx == id) count++;
            if (tell.impactVfx == id) count++;
            if (tell.groundVfx == id) count++;
        }
        return count;
    }

    private void SelectTell(TellBinding binding)
    {
        if (_stage?.Tuning?.data?.tells == null ||
            binding.Index < 0 ||
            binding.Index >= _stage.Tuning.data.tells.Count)
            return;
        if (_tellDraft != null) UnityEngine.Object.DestroyImmediate(_tellDraft);
        _tellDraft = CreateInstance<VfxLabTellDraft>();
        _tellDraft.hideFlags = HideFlags.HideAndDontSave;
        _tellDraft.Load(binding.Index, _stage.Tuning.data.tells[binding.Index]);
        _tellSerialized = new SerializedObject(_tellDraft);
        _tellDirty = false;
        recipeContext = binding.Slot switch
        {
            "projectile" => VfxLabRecipeContext.Projectile,
            "source" => VfxLabRecipeContext.AtSource,
            "ground" => VfxLabRecipeContext.GroundTarget,
            _ => VfxLabRecipeContext.AtTarget,
        };
        previewColor = _tellDraft.tell.motionColor;
        previewGlow = _tellDraft.tell.motionGlow;
        previewScale = _tellDraft.tell.motionScale;
        RefreshRecipeContext();
        UpdateDirtyBadge();
    }

    private void DrawInspector()
    {
        if (_stage == null)
        {
            EditorGUILayout.HelpBox(
                "Open the dedicated VfxLab scene to start authoring.",
                MessageType.Info);
            if (GUILayout.Button("OPEN VFX LAB SCENE")) VfxLabSceneTools.OpenScene();
            return;
        }
        if (_selectedEntry == null)
        {
            EditorGUILayout.HelpBox("Select content from the browser.", MessageType.Info);
            return;
        }
        switch (_selectedEntry.Kind)
        {
            case EntryKind.Fixture:
                DrawFixtureInspector();
                break;
            case EntryKind.Revision:
                DrawRevisionInspector();
                break;
            case EntryKind.Scenario:
                DrawScenarioInspector();
                break;
            default:
                DrawRecipeInspector();
                break;
        }
        EditorGUILayout.Space(12f);
        DrawAudioAudition();
    }

    private void DrawRecipeInspector()
    {
        if (_recipeDraft == null || _recipeSerialized == null) return;
        EditorGUILayout.LabelField(_recipeId, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            VfxLibrary.HasAssetOverride(_recipeId)
                ? "Resolved source: applied ScriptableObject override"
                : "Resolved source: C# fallback",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(5f);

        EditorGUI.BeginChangeCheck();
        recipeContext = (VfxLabRecipeContext)EditorGUILayout.EnumPopup(
            "Preview context", recipeContext);
        previewColor = EditorGUILayout.ColorField("Tell color", previewColor);
        previewGlow = EditorGUILayout.Slider("Tell glow", previewGlow, 0f, 8f);
        previewScale = EditorGUILayout.Slider("Tell scale", previewScale, 0.2f, 4f);
        if (EditorGUI.EndChangeCheck()) RefreshRecipeContext();

        EditorGUILayout.Space(8f);
        _recipeSerialized.UpdateIfRequiredOrScript();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_recipeSerialized.FindProperty("duration"));
        EditorGUILayout.PropertyField(_recipeSerialized.FindProperty("sustained"));
        SerializedProperty elements = _recipeSerialized.FindProperty("elements");
        DrawElementSelector(elements);
        if (elements.arraySize > 0)
        {
            _selectedElement = Mathf.Clamp(_selectedElement, 0, elements.arraySize - 1);
            SerializedProperty element = elements.GetArrayElementAtIndex(_selectedElement);
            DrawElement(element);
        }
        if (EditorGUI.EndChangeCheck())
        {
            _recipeSerialized.ApplyModifiedProperties();
            MarkRecipeChanged();
        }
        else
        {
            _recipeSerialized.ApplyModifiedProperties();
        }

        DrawRecipeValidation();
        DrawRecipeActions();
        DrawBindings();
        if (_tellDraft != null) DrawTellDraft();
    }

    private void DrawElementSelector(SerializedProperty elements)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            $"ELEMENT STACK · {elements.arraySize}",
            EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < elements.arraySize; i++)
            {
                SerializedProperty kind = elements.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("kind");
                string label = $"{i + 1} {(VfxRecipeElementKind)kind.enumValueIndex}";
                if (GUILayout.Toggle(_selectedElement == i, label, EditorStyles.miniButton))
                    _selectedElement = i;
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ ADD", EditorStyles.miniButton))
            {
                var menu = new GenericMenu();
                foreach (VfxRecipeElementKind kind in Enum.GetValues(
                             typeof(VfxRecipeElementKind)))
                {
                    VfxRecipeElementKind captured = kind;
                    menu.AddItem(new GUIContent(kind.ToString()), false, () =>
                    {
                        _recipeDraft.elements.Add(VfxRecipeElementData.Default(captured));
                        _selectedElement = _recipeDraft.elements.Count - 1;
                        _recipeSerialized.Update();
                        MarkRecipeChanged();
                    });
                }
                menu.ShowAsContext();
            }
            using (new EditorGUI.DisabledScope(elements.arraySize == 0))
            {
                if (GUILayout.Button("DUPLICATE", EditorStyles.miniButton))
                {
                    _recipeDraft.elements.Insert(
                        _selectedElement + 1,
                        _recipeDraft.elements[_selectedElement].DeepCopy());
                    _selectedElement++;
                    _recipeSerialized.Update();
                    MarkRecipeChanged();
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("REMOVE", EditorStyles.miniButton))
                {
                    _recipeDraft.elements.RemoveAt(_selectedElement);
                    _selectedElement = Mathf.Max(0, _selectedElement - 1);
                    _recipeSerialized.Update();
                    MarkRecipeChanged();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    private static void DrawElement(SerializedProperty element)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(element.FindPropertyRelative("kind"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("anchor"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("offset"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("delay"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("tier"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative("overrideTint"));
        if (element.FindPropertyRelative("overrideTint").boolValue)
            EditorGUILayout.PropertyField(element.FindPropertyRelative("tint"));

        VfxRecipeElementKind kind = (VfxRecipeElementKind)element
            .FindPropertyRelative("kind").enumValueIndex;
        EditorGUILayout.Space(4f);
        switch (kind)
        {
            case VfxRecipeElementKind.Quad:
                DrawProperties(element,
                    "shader", "hex", "orientation", "quadSize",
                    "thickness", "softness", "intensity", "edgeFade", "falloff",
                    "quadTexture", "requireTexture", "noise",
                    "radius", "arc", "alpha", "rotation", "phase", "scale");
                break;
            case VfxRecipeElementKind.Light:
                DrawProperties(element, "range", "lightIntensity");
                break;
            default:
                DrawProperties(element,
                    "burst", "rate", "lifeMin", "lifeMax",
                    "speedMin", "speedMax", "sizeMin", "sizeMax",
                    "gravity", "drag", "shape", "shapeAngle", "shapeRadius",
                    "shapeRotation", "local", "stretch", "stretchScale",
                    "fade", "sizeOverLife", "trails");
                if (element.FindPropertyRelative("trails").boolValue)
                    DrawProperties(element,
                        "trailRatio", "trailLifetime", "trailWidth");
                DrawProperties(element,
                    "particleTexture", "tilesX", "tilesY", "maxParticles");
                break;
        }
    }

    private static void DrawProperties(
        SerializedProperty parent,
        params string[] names)
    {
        foreach (string name in names)
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name));
    }

    private void DrawRecipeValidation()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        _recipeDraft.Validate(errors, warnings);
        foreach (string warning in warnings)
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        foreach (string error in errors)
            EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    private void DrawRecipeActions()
    {
        var errors = new List<string>();
        bool valid = _recipeDraft.Validate(errors);
        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!valid || !_recipeDirty))
            {
                if (GUILayout.Button("APPLY RECIPE", GUILayout.Height(28f)))
                    ApplyRecipe();
            }
            if (GUILayout.Button("REVERT DRAFT", GUILayout.Height(28f)))
                ReloadRecipeDraft(false);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(
                       VfxLibrary.GetBuiltIn(_recipeId) == null))
            {
                if (GUILayout.Button("LOAD C# FALLBACK"))
                    ReloadRecipeDraft(true);
            }
            VfxRecipeAsset asset = VfxLabAssetTools.FindOverride(_recipeId);
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("REVEAL OVERRIDE"))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }
        EditorGUILayout.HelpBox(
            "Draft changes affect only this Lab session. APPLY RECIPE creates or updates the override asset.",
            MessageType.Info);
    }

    private void DrawBindings()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            $"TELL BINDINGS · {_bindings.Count}",
            EditorStyles.boldLabel);
        if (_bindings.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "This recipe is currently direct-only. It may be a Revision/system effect or a future binding.",
                MessageType.None);
            return;
        }
        foreach (TellBinding binding in _bindings)
        {
            TellDef tell = _stage.Tuning.data.tells[binding.Index];
            string label =
                $"{VfxLabTellTools.Describe(tell, binding.Index)}  [{binding.Slot}]";
            if (GUILayout.Button(label, EditorStyles.miniButton))
                SelectTell(binding);
        }
    }

    private void DrawTellDraft()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("SELECTED TELL DRAFT", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            VfxLabTellTools.Describe(
                _tellDraft.tell,
                _tellDraft.sourceIndex),
            EditorStyles.miniLabel);
        _tellSerialized.UpdateIfRequiredOrScript();
        SerializedProperty tell = _tellSerialized.FindProperty("tell");
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Recipe slots", EditorStyles.miniBoldLabel);
        DrawProperties(tell, "vfx", "projectileVfx", "impactVfx", "groundVfx");
        EditorGUILayout.LabelField("Motion context", EditorStyles.miniBoldLabel);
        DrawProperties(tell,
            "motion", "motionSeconds", "motionPerHexSeconds", "motionMaxSeconds",
            "windupSeconds", "defer", "motionColor", "motionGlow", "motionScale");
        EditorGUILayout.LabelField("Audio", EditorStyles.miniBoldLabel);
        DrawProperties(tell, "castSound", "sound", "critSound");
        EditorGUILayout.LabelField("Impact riders", EditorStyles.miniBoldLabel);
        DrawProperties(tell,
            "flash", "flashColor", "flashSeconds",
            "punch", "punchAmount", "punchSeconds",
            "hitAnim", "pulseGround", "bigImpact", "announce");

        if (EditorGUI.EndChangeCheck())
        {
            _tellSerialized.ApplyModifiedProperties();
            _tellDirty = true;
            previewColor = _tellDraft.tell.motionColor;
            previewGlow = _tellDraft.tell.motionGlow;
            previewScale = _tellDraft.tell.motionScale;
            RefreshRecipeContext();
            UpdateDirtyBadge();
        }
        else
        {
            _tellSerialized.ApplyModifiedProperties();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!_tellDirty))
            {
                if (GUILayout.Button("APPLY TELL"))
                {
                    try
                    {
                        VfxLabTellTools.Apply(_stage.Tuning, _tellDraft);
                        _tellDirty = false;
                        RebuildBindings();
                        RebuildBrowser();
                        SetStatus("Tell applied to tuning.json.");
                        UpdateDirtyBadge();
                    }
                    catch (Exception exception)
                    {
                        SetStatus(exception.Message);
                        Debug.LogException(exception);
                    }
                }
            }
            if (GUILayout.Button("REVERT TELL"))
            {
                int index = _tellDraft.sourceIndex;
                _tellDraft.Load(index, _stage.Tuning.data.tells[index]);
                _tellSerialized = new SerializedObject(_tellDraft);
                _tellDirty = false;
                UpdateDirtyBadge();
            }
        }
    }

    private void DrawFixtureInspector()
    {
        if (_fixtureInfo == null)
        {
            EditorGUILayout.HelpBox("Fixture could not be read.", MessageType.Error);
            return;
        }
        EditorGUILayout.LabelField(_fixtureInfo.DisplayName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"{_fixtureInfo.UnitCount} units · {_fixtureInfo.EventCount} events · end tick {_fixtureInfo.EndTick}",
            EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(
            "This is the shipping replay path. Jump to the first occurrence of a presentation signature, then scrub around it.",
            MessageType.Info);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("EVENT SIGNATURE BOOKMARKS", EditorStyles.boldLabel);
        float tps = Mathf.Max(
            0.01f,
            _stage.Tuning?.data?.playback?.ticksPerSecond ?? 10f);
        foreach (ReplayInspector.Stat stat in _fixtureInfo.Signatures)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{stat.Label}  ×{stat.Count}",
                    GUILayout.MinWidth(150f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        $"T{stat.FirstTick}",
                        EditorStyles.miniButton,
                        GUILayout.Width(64f)))
                    EvaluateAt(stat.FirstTick / tps);
            }
        }
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("RELOAD FIXTURE"))
        {
            _fixtureCache.Remove(_fixtureInfo.RelativePath);
            _fixtureInfo = GetFixtureInfo(_fixtureInfo.RelativePath);
            _stage.SelectFixture(_fixtureInfo.RelativePath, 0);
            UpdateTimelineRange();
            RequestRender();
        }
    }

    private void DrawRevisionInspector()
    {
        EditorGUILayout.LabelField("TIMELINE SPLIT CEREMONY", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The viewport captures the witnessed future, then renders the rejected earlier fold through the shipping URP fracture pass.",
            MessageType.Info);
        EditorGUI.BeginChangeCheck();
        revisionLineage = (RevisionEffectKind)EditorGUILayout.EnumPopup(
            "Lineage", revisionLineage);
        revisionFullRupture = EditorGUILayout.Toggle(
            "First full rupture", revisionFullRupture);
        revisionReducedMotion = EditorGUILayout.Toggle(
            "Reduced motion", revisionReducedMotion);
        revisionPresentTick = Mathf.Max(
            0,
            EditorGUILayout.IntField("Witnessed tick", revisionPresentTick));
        revisionBranchTick = Mathf.Clamp(
            EditorGUILayout.IntField("Branch tick", revisionBranchTick),
            0,
            revisionPresentTick);
        if (EditorGUI.EndChangeCheck()) ConfigureRevisionStage();

        EditorGUILayout.Space(6f);
        DrawRevisionJumpButtons();
        EditorGUILayout.Space(8f);
        DrawRevisionTuneDraft();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("CEREMONY AUDIO", EditorStyles.boldLabel);
        string[] cues =
        {
            "revision_split", "revision_tear", "revision_rewind_riser",
            "revision_rewind_bed", "revision_scrub", "revision_land_borrowed",
            "revision_land_recall", "revision_return",
        };
        foreach (string cue in cues)
        {
            if (GUILayout.Button("AUDITION · " + cue, EditorStyles.miniButton))
                AuditionCue(cue, VfxLabAudioBus.Revision);
        }
    }

    private void DrawRevisionJumpButtons()
    {
        RevisionPresentationTune tune = _revisionDraft?.tune ??
            _stage.Tuning.data.revision;
        float open = tune.firstOpenSeconds;
        float held = open + 0.32f;
        float tear = held + tune.tearSeconds;
        float rewind = tear + Mathf.Min(
            tune.rewindMaxSeconds,
            tune.rewindBaseSeconds +
            Mathf.Max(0f, revisionPresentTick - revisionBranchTick) /
            Mathf.Max(0.01f, _stage.Tuning.data.playback.ticksPerSecond) *
            tune.rewindPerSecond);
        float landing = rewind + tune.vacuumSeconds;
        using (new EditorGUILayout.HorizontalScope())
        {
            JumpButton("OPEN", 0f);
            JumpButton("TEAR", held + 0.01f);
            JumpButton("REWIND", tear + 0.01f);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            JumpButton("VACUUM", rewind + 0.001f);
            JumpButton("LAND", landing + 0.001f);
            JumpButton("RECEIPT", landing + tune.landingSeconds + 0.001f);
        }
    }

    private void JumpButton(string label, float time)
    {
        if (GUILayout.Button(label, EditorStyles.miniButton))
            EvaluateAt(time);
    }

    private void DrawRevisionTuneDraft()
    {
        EnsureRevisionDraft();
        if (_revisionSerialized == null) return;
        EditorGUILayout.LabelField("REVISION PRESENTATION DRAFT", EditorStyles.boldLabel);
        _revisionSerialized.UpdateIfRequiredOrScript();
        SerializedProperty tune = _revisionSerialized.FindProperty("tune");
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Timing (unscaled seconds)", EditorStyles.miniBoldLabel);
        DrawProperties(tune,
            "firstOpenSeconds", "reopenSeconds", "tearSeconds",
            "rewindBaseSeconds", "rewindPerSecond", "rewindMaxSeconds",
            "vacuumSeconds", "landingSeconds",
            "receiptSeconds", "receiptTailSeconds");
        EditorGUILayout.LabelField("Fault image", EditorStyles.miniBoldLabel);
        DrawProperties(tune,
            "heldLight", "heldSaturation", "heldVignette",
            "fractureStrength", "fractureEdgeWidthPx", "fractureEdgeGlow",
            "fractureRefractionPx", "fracturePlateSlipPx", "fractureChromaticPx",
            "fractureFutureOpacity", "fractureHeldSeamStrength",
            "fractureSandFlow", "futureEchoAlpha", "rewindEchoCount",
            "landingPunch", "landingShake");
        EditorGUILayout.LabelField("Reduced motion", EditorStyles.miniBoldLabel);
        DrawProperties(tune,
            "reducedOpenSeconds", "reducedRewindSeconds", "reducedReceiptSeconds");

        if (EditorGUI.EndChangeCheck())
        {
            _revisionSerialized.ApplyModifiedProperties();
            _revisionDirty = true;
            _stage.SetRevisionTunePreview(_revisionDraft.tune);
            UpdateTimelineRange();
            UpdateDirtyBadge();
            RequestRender();
        }
        else
        {
            _revisionSerialized.ApplyModifiedProperties();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!_revisionDirty))
            {
                if (GUILayout.Button("APPLY REVISION TUNE"))
                {
                    try
                    {
                        VfxLabRevisionTuneTools.Apply(_stage.Tuning, _revisionDraft);
                        _revisionDirty = false;
                        SetStatus("Revision presentation tune applied to tuning.json.");
                        UpdateDirtyBadge();
                    }
                    catch (Exception exception)
                    {
                        SetStatus(exception.Message);
                        Debug.LogException(exception);
                    }
                }
            }
            if (GUILayout.Button("REVERT REVISION TUNE"))
            {
                _revisionDraft.Load(_stage.Tuning.data.revision);
                _revisionSerialized = new SerializedObject(_revisionDraft);
                _stage.SetRevisionTunePreview(_revisionDraft.tune);
                _revisionDirty = false;
                ConfigureRevisionStage();
                UpdateDirtyBadge();
            }
        }
    }

    private void DrawScenarioInspector()
    {
        VfxLabScenarioAsset scenario = _selectedEntry.Asset as VfxLabScenarioAsset;
        if (scenario == null) return;
        EditorGUILayout.LabelField(scenario.displayName, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(scenario.notes, MessageType.Info);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Scenario", scenario, typeof(VfxLabScenarioAsset), false);
        if (GUILayout.Button("REPLAY SCENARIO"))
        {
            _stage.SelectScenario(scenario);
            UpdateTimelineRange();
            RequestRender();
        }
        if (GUILayout.Button("OPEN IN INSPECTOR"))
        {
            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
        }
    }

    private void DrawAudioAudition()
    {
        EditorGUILayout.LabelField("AUDIO AUDITION", EditorStyles.boldLabel);
        if (_audioCues.Count == 0)
        {
            EditorGUILayout.HelpBox("No Board/UI SFX assets found.", MessageType.Warning);
            return;
        }
        selectedAudioCue = Mathf.Clamp(selectedAudioCue, 0, _audioCues.Count - 1);
        string[] labels = _audioCues.Select(cue => cue.Id).ToArray();
        selectedAudioCue = EditorGUILayout.Popup(
            "Cue", selectedAudioCue, labels);
        audioBus = (VfxLabAudioBus)EditorGUILayout.EnumPopup("Bus", audioBus);
        audioVolume = EditorGUILayout.Slider("Gain", audioVolume, 0f, 1f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(
                    Application.isPlaying ? "AUDITION IN MIX" : "RAW EDITOR PREVIEW"))
                AuditionCue(_audioCues[selectedAudioCue].Id, audioBus);
            if (GUILayout.Button("STOP"))
            {
                VfxLabAudioPreview.StopAll();
                _stage.StopRevisionLoop();
            }
        }
        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Play Mode uses the shipping voice pool, priority, bus routing, and mixer."
                : "Edit Mode previews the raw clip. Enter Play Mode to judge the real bus/mix.",
            MessageType.None);
    }

    private void ApplyRecipe()
    {
        try
        {
            VfxRecipeAsset applied = VfxLabAssetTools.ApplyDraft(_recipeDraft);
            _recipeDirty = false;
            VfxLibrary.SetPreviewOverride(_recipeId, _recipeDraft.Compile());
            _sourceBadge.text = "ASSET OVERRIDE";
            SetStatus($"Applied {AssetDatabase.GetAssetPath(applied)}");
            RebuildBrowser();
            UpdateDirtyBadge();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message);
            Debug.LogException(exception);
        }
    }

    private void ReloadRecipeDraft(bool builtIn)
    {
        VfxLibrary.ClearPreviewOverride(_recipeId);
        VfxDef source = builtIn
            ? VfxLibrary.GetBuiltIn(_recipeId)
            : VfxLibrary.Get(_recipeId);
        if (source == null) return;
        _recipeDraft.CopyFrom(source);
        _recipeSerialized = new SerializedObject(_recipeDraft);
        _selectedElement = Mathf.Clamp(
            _selectedElement,
            0,
            Mathf.Max(0, _recipeDraft.elements.Count - 1));
        _recipeDirty = builtIn && VfxLibrary.HasAssetOverride(_recipeId);
        VfxDef compiled = _recipeDraft.Compile();
        VfxLibrary.SetPreviewOverride(_recipeId, compiled);
        _stage?.SelectRecipe(
            compiled,
            recipeContext,
            previewColor,
            previewGlow,
            previewScale);
        UpdateTimelineRange();
        UpdateDirtyBadge();
        RequestRender();
        SetStatus(builtIn
            ? "Loaded C# fallback into the draft. Apply to replace the asset override."
            : "Draft reverted to the currently applied source.");
    }

    private void MarkRecipeChanged()
    {
        if (_recipeDraft == null) return;
        _recipeDirty = true;
        VfxDef compiled = _recipeDraft.Compile();
        VfxLibrary.SetPreviewOverride(_recipeId, compiled);
        _stage?.SelectRecipe(
            compiled,
            recipeContext,
            previewColor,
            previewGlow,
            previewScale);
        UpdateTimelineRange();
        UpdateDirtyBadge();
        RequestRender();
    }

    private void RefreshRecipeContext()
    {
        _stage?.UpdateRecipeContext(
            recipeContext,
            previewColor,
            previewGlow,
            previewScale);
        RequestRender();
    }

    private void ConfigureRevisionStage()
    {
        EnsureRevisionDraft();
        _stage?.SetRevisionTunePreview(_revisionDraft?.tune);
        _stage?.ConfigureRevision(
            revisionFixture,
            revisionLineage,
            revisionFullRupture,
            revisionReducedMotion,
            revisionPresentTick,
            revisionBranchTick);
        UpdateTimelineRange();
        RequestRender();
    }

    private void EnsureRevisionDraft()
    {
        if (_revisionDraft != null || _stage?.Tuning?.data?.revision == null) return;
        _revisionDraft = CreateInstance<VfxLabRevisionDraft>();
        _revisionDraft.hideFlags = HideFlags.HideAndDontSave;
        _revisionDraft.Load(_stage.Tuning.data.revision);
        _revisionSerialized = new SerializedObject(_revisionDraft);
    }

    private void CreateNewRecipe()
    {
        VfxLabAssetTools.EnsureFolder(VfxLabPaths.RecipeFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Create VFX Recipe Override",
            "new-effect",
            "asset",
            "Choose an id-like filename. The Lab will select the new recipe.",
            VfxLabPaths.RecipeFolder);
        if (string.IsNullOrEmpty(path)) return;
        if (!path.StartsWith(VfxLabPaths.RecipeFolder + "/", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog(
                "Recipe must live in the VFX override folder",
                $"Save it under {VfxLabPaths.RecipeFolder} so the runtime can discover it.",
                "OK");
            return;
        }
        string id = VfxLabAssetTools.Sanitize(Path.GetFileNameWithoutExtension(path));
        VfxLabAssetTools.CreateNewAtPath(path, id);
        browserMode = BrowserMode.Recipes;
        _modeMenu.text = browserMode.ToString();
        RebuildBrowser();
        BrowserEntry entry = _entries.FirstOrDefault(value => value.Key == "recipe:" + id);
        if (entry != null) SelectEntry(entry);
    }

    private void LoadAudioCues()
    {
        _audioCues.Clear();
        var byId = new Dictionary<string, AudioCue>(StringComparer.Ordinal);
        LoadAudioFolder("Assets/Resources/Board/SFX", false, byId);
        LoadAudioFolder("Assets/Resources/UI/SFX", true, byId);
        _audioCues.AddRange(byId.Values.OrderBy(cue => cue.Id));
    }

    private static void LoadAudioFolder(
        string folder,
        bool ui,
        Dictionary<string, AudioCue> byId)
    {
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:AudioClip",
                     new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string id = Path.GetFileNameWithoutExtension(path);
            if (id.Length > 2 &&
                id[id.Length - 2] == '_' &&
                char.IsDigit(id[id.Length - 1]))
                id = id.Substring(0, id.Length - 2);
            if (!byId.ContainsKey(id))
                byId[id] = new AudioCue { Id = id, AssetPath = path, Ui = ui };
        }
    }

    private void AuditionCue(string id, VfxLabAudioBus bus)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (Application.isPlaying)
        {
            _stage?.AuditionSfx(id, bus, audioVolume);
            return;
        }
        AudioCue cue = _audioCues.FirstOrDefault(value => value.Id == id);
        if (cue == null)
        {
            SetStatus($"No imported AudioClip named '{id}'.");
            return;
        }
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(cue.AssetPath);
        if (!VfxLabAudioPreview.PlayRaw(clip))
            SetStatus("Unity's raw editor audio preview API was unavailable.");
    }

    private VfxLabFixtureInfo GetFixtureInfo(string relative)
    {
        if (_fixtureCache.TryGetValue(relative, out VfxLabFixtureInfo hit)) return hit;
        try
        {
            VfxLabFixtureInfo info = VfxLabFixtureInfo.Load(relative);
            _fixtureCache[relative] = info;
            return info;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[VfxLab] Could not inspect {relative}: {exception.Message}");
            return null;
        }
    }

    private void TogglePlay()
    {
        if (_stage == null) return;
        _playing = !_playing;
        _playButton.text = _playing ? "Ⅱ" : "▶";
        _lastUpdate = EditorApplication.timeSinceStartup;
    }

    private void EvaluateAt(float time)
    {
        if (_stage == null) return;
        _stage.Evaluate(Mathf.Clamp(time, 0f, _stage.Duration));
        UpdateTimelineRange();
        UpdateTransport();
        RequestRender();
    }

    private void UpdateTimelineRange()
    {
        if (_timeline == null || _stage == null) return;
        _timeline.lowValue = 0f;
        _timeline.highValue = Mathf.Max(0.02f, _stage.Duration);
        _timeline.SetValueWithoutNotify(
            Mathf.Clamp(_stage.CurrentTime, 0f, _timeline.highValue));
        UpdateTransport();
    }

    private void UpdateTransport()
    {
        if (_stage == null || _timeline == null || _timeLabel == null) return;
        _timeline.SetValueWithoutNotify(_stage.CurrentTime);
        if (_stage.Mode == VfxLabStageMode.CombatFixture)
        {
            float tps = Mathf.Max(
                0.01f,
                _stage.Tuning?.data?.playback?.ticksPerSecond ?? 10f);
            _timeLabel.text =
                $"T{Mathf.RoundToInt(_stage.CurrentTime * tps)} · {_stage.CurrentTime:0.00} s";
        }
        else
        {
            _timeLabel.text =
                $"{_stage.CurrentTime:0.00} / {_stage.Duration:0.00} s";
        }
        _viewportOverlay.text = _selectedEntry == null
            ? "READY"
            : $"{_selectedEntry.Label.ToUpperInvariant()}  ·  {_stage.Mode}  ·  {environment}";
    }

    private void OnEditorUpdate()
    {
        if (this == null) return;
        if (_stage == null)
        {
            ConnectStage();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)Math.Min(0.1, Math.Max(0.0, now - _lastUpdate));
        _lastUpdate = now;
        if (_playing)
        {
            float before = _stage.CurrentTime;
            _stage.Advance(dt * playbackSpeed, loop);
            if (!loop &&
                Mathf.Approximately(_stage.CurrentTime, _stage.Duration) &&
                Mathf.Approximately(before, _stage.CurrentTime))
            {
                _playing = false;
                _playButton.text = "▶";
            }
            UpdateTransport();
            _needsRender = true;
        }
        if (_needsRender || now - _lastRender >= 1.0 / 30.0)
        {
            _stage.RenderNow();
            _viewportImage?.MarkDirtyRepaint();
            _lastRender = now;
            _needsRender = false;
            if (_playing) Repaint();
        }
    }

    private void RequestRender()
    {
        _needsRender = true;
        _inspectorGui?.MarkDirtyRepaint();
    }

    private void UpdateDirtyBadge()
    {
        if (_dirtyBadge == null) return;
        var dirty = new List<string>();
        if (_recipeDirty) dirty.Add("RECIPE");
        if (_tellDirty) dirty.Add("TELL");
        if (_revisionDirty) dirty.Add("REVISION");
        _dirtyBadge.text = dirty.Count == 0
            ? "CLEAN"
            : "DRAFT · " + string.Join(" + ", dirty);
        _dirtyBadge.EnableInClassList(
            "vfx-lab__badge--dirty",
            dirty.Count > 0);
    }

    private void UpdateStatusInventory()
    {
        int tells = _stage?.Tuning?.data?.tells?.Count ?? 0;
        SetStatus(
            $"{VfxLibrary.AllIds.Count} recipes · {tells} tells · {_audioCues.Count} audio cues · " +
            (_stage != null ? "runtime connected" : "scene disconnected"));
    }

    private void SetStatus(string message)
    {
        if (_status != null) _status.text = message;
    }

    private bool ConfirmDiscardDrafts()
    {
        if (!_recipeDirty && !_tellDirty && !_revisionDirty) return true;
        var dirty = new List<string>();
        if (_recipeDirty) dirty.Add("recipe");
        if (_tellDirty) dirty.Add("tell");
        if (_revisionDirty) dirty.Add("Revision tune");
        return EditorUtility.DisplayDialog(
            "Discard unapplied VFX Lab drafts?",
            $"Switching content will discard the {string.Join(", ", dirty)} draft. Applied assets and tuning are safe.",
            "Discard and switch",
            "Stay here");
    }

    private bool MatchesRecipeCategory(string id)
    {
        switch (recipeCategory)
        {
            case "Weapons":
                return id.Contains("slash") || id.Contains("nick") ||
                       id.Contains("thrust") || id.Contains("muzzle") ||
                       id.Contains("smoke") || id.Contains("arrow") ||
                       id.Contains("staff") || id.Contains("censer") ||
                       id.Contains("shield") || id.Contains("pole");
            case "Casts":
                return id.Contains("cast") || id.Contains("release") ||
                       id.Contains("fire-bolt") || id.Contains("spark-link");
            case "Ground + Field":
                return id.Contains("ground") || id.Contains("heal") ||
                       id.Contains("leap");
            case "Revision":
                return id.StartsWith("revision-", StringComparison.Ordinal);
            case "Systems":
                return id.Contains("status") || id.Contains("death") ||
                       id.Contains("impact");
            case "Unbound":
                return CountBindings(id) == 0;
            default:
                return true;
        }
    }

    private static bool MatchesSearch(string value, string search) =>
        string.IsNullOrWhiteSpace(search) ||
        (value ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    private static VfxLabRecipeContext GuessContext(string id)
    {
        if (id.Contains("arrow") || id.Contains("bolt") ||
            id.Contains("smoke-line") || id.Contains("staff-wisp") ||
            id.Contains("censer-mote"))
            return VfxLabRecipeContext.Projectile;
        if (id.Contains("ground") || id.Contains("leap-dust"))
            return VfxLabRecipeContext.GroundTarget;
        if (id.Contains("cast-aura"))
            return VfxLabRecipeContext.FollowSource;
        if (id.Contains("slash") || id.Contains("nick") ||
            id.Contains("muzzle") || id.Contains("thrust") ||
            id.Contains("shield") || id.Contains("pole"))
            return VfxLabRecipeContext.AtSource;
        return VfxLabRecipeContext.AtTarget;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        VfxLabAudioPreview.StopAll();
        VfxLibrary.ClearPreviewOverrides();
        if (_stage != null)
        {
            _stage.Stop();
            _stage.ClearRevisionTunePreview();
            if (_renderTexture != null) _stage.DetachRenderTarget(_renderTexture);
        }
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(_renderTexture);
            _renderTexture = null;
        }
        if (_recipeDraft != null) UnityEngine.Object.DestroyImmediate(_recipeDraft);
        if (_tellDraft != null) UnityEngine.Object.DestroyImmediate(_tellDraft);
        if (_revisionDraft != null) UnityEngine.Object.DestroyImmediate(_revisionDraft);
    }
}

internal static class VfxLabVisualElementExtensions
{
    internal static T WithClass<T>(this T element, string className)
        where T : VisualElement
    {
        element.AddToClassList(className);
        return element;
    }
}
#endif
