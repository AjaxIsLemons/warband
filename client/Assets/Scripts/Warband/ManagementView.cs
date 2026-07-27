using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The full-screen Hourstone Table. Stable spatial geography is the overview; each station opens
/// a purpose-built workspace with a persistent dossier. This view owns presentation state only:
/// every route and transaction still comes from RunShellModel / RunShellActions.
/// </summary>
internal sealed class ManagementView : IRunScreenView, IDisposable
{
    private sealed class CardPool
    {
        private static readonly string[] Variants =
        {
            "market", "warband", "armory", "armory-target", "hourstone",
        };

        private readonly VisualElement _host;
        private readonly Action<string> _select;
        private readonly UiFeedbackDirector _polish;
        private readonly List<WarbandCard> _cards = new List<WarbandCard>();
        private readonly List<string> _targetIds = new List<string>();
        private readonly List<string> _signatures = new List<string>();
        private readonly List<VisualElement> _pendingReveals = new List<VisualElement>();
        private string _variant = "warband";

        public CardPool(VisualElement host, Action<string> select, UiFeedbackDirector polish)
        {
            _host = host;
            _select = select;
            _polish = polish;
        }

        public void Bind(IReadOnlyList<CardModel> models, string variant)
        {
            _variant = variant;
            while (_cards.Count > models.Count)
            {
                int last = _cards.Count - 1;
                _polish.UnregisterTarget(_targetIds[last], _cards[last].Root);
                _cards[last].Root.RemoveFromHierarchy();
                _cards.RemoveAt(last);
                _targetIds.RemoveAt(last);
                _signatures.RemoveAt(last);
            }
            while (_cards.Count < models.Count)
            {
                var card = new WarbandCard(_select);
                card.Root.AddToClassList("wb-card--hub");
                _cards.Add(card);
                _targetIds.Add("");
                _signatures.Add("");
                _host.Add(card.Root);
                _polish.AttachInteractable(card.Root,
                    () => card.Root.userData as string ?? "");
            }
            for (int i = 0; i < models.Count; i++)
            {
                string oldTarget = _targetIds[i];
                string target = "card:" + models[i].Key;
                if (!string.Equals(oldTarget, target, StringComparison.Ordinal))
                    _polish.UnregisterTarget(oldTarget, _cards[i].Root);
                foreach (string value in Variants)
                    _cards[i].Root.EnableInClassList("wb-card--hub-" + value, value == _variant);
                _cards[i].Bind(models[i]);
                _cards[i].SetHallVariant(_variant);
                _targetIds[i] = target;
                _polish.RegisterTarget(target, _cards[i].Root);

                // Keys are stable slot addresses, so include content id: a Market reroll at
                // market:0 is still a genuinely new reveal when a different offer is dealt there.
                string signature = models[i].Key + "|" + models[i].ContentId;
                if (!string.Equals(_signatures[i], signature, StringComparison.Ordinal))
                {
                    _signatures[i] = signature;
                    _pendingReveals.Add(_cards[i].Root);
                }
            }
        }

        public List<VisualElement> TakePendingReveals()
        {
            var result = new List<VisualElement>(_pendingReveals);
            _pendingReveals.Clear();
            return result;
        }

    }

    /// <summary>Market-only pooled renderer. It deliberately does not share WarbandCard.</summary>
    private sealed class MarketOfferPool
    {
        private readonly VisualElement _host;
        private readonly Action<string> _select;
        private readonly UiFeedbackDirector _polish;
        private readonly Action<CardModel, VisualElement> _hover;
        private readonly Action _leave;
        private readonly List<MarketOfferCard> _cards = new List<MarketOfferCard>();
        private readonly List<string> _targetIds = new List<string>();
        private readonly List<string> _signatures = new List<string>();
        private readonly List<VisualElement> _pendingReveals = new List<VisualElement>();

        public MarketOfferPool(VisualElement host, Action<string> select,
                               UiFeedbackDirector polish,
                               Action<CardModel, VisualElement> hover, Action leave)
        {
            _host = host;
            _select = select;
            _polish = polish;
            _hover = hover;
            _leave = leave;
        }

        public void Bind(IReadOnlyList<MarketOfferCardModel> models)
        {
            while (_cards.Count > models.Count)
            {
                int last = _cards.Count - 1;
                _polish.UnregisterTarget(_targetIds[last], _cards[last].Root);
                _cards[last].Root.RemoveFromHierarchy();
                _cards.RemoveAt(last);
                _targetIds.RemoveAt(last);
                _signatures.RemoveAt(last);
            }
            while (_cards.Count < models.Count)
            {
                var card = new MarketOfferCard(_select, _hover, _leave);
                _cards.Add(card);
                _targetIds.Add("");
                _signatures.Add("");
                _host.Add(card.Root);
                _polish.AttachInteractable(card.Root,
                    () => card.Root.userData as string ?? "");
            }

            for (int i = 0; i < models.Count; i++)
            {
                string oldTarget = _targetIds[i];
                string target = "card:" + models[i].Key;
                if (!string.Equals(oldTarget, target, StringComparison.Ordinal))
                    _polish.UnregisterTarget(oldTarget, _cards[i].Root);
                _cards[i].Bind(models[i]);
                _targetIds[i] = target;
                _polish.RegisterTarget(target, _cards[i].Root);

                string signature = models[i].Key + "|" + models[i].ContentId + "|" +
                                   models[i].Kind;
                if (string.Equals(_signatures[i], signature, StringComparison.Ordinal)) continue;
                _signatures[i] = signature;
                _pendingReveals.Add(_cards[i].Root);
            }
        }

        public List<VisualElement> TakePendingReveals()
        {
            var result = new List<VisualElement>(_pendingReveals);
            _pendingReveals.Clear();
            return result;
        }

        public VisualElement FindSelected(IReadOnlyList<MarketOfferCardModel> models)
        {
            int count = Mathf.Min(models?.Count ?? 0, _cards.Count);
            for (int i = 0; i < count; i++)
                if (models[i].Selected) return _cards[i].Root;
            return null;
        }
    }

    private readonly RunShellActions _actions;
    private readonly VisualElement _root;
    private readonly VisualElement _safeFrame;
    private readonly VisualElement _overview;
    private readonly VisualElement _workspace;
    private readonly Label _act;
    private readonly Label _beat;
    private readonly Label _sand;
    private readonly Label _heading;
    private readonly Label _brief;
    private readonly Label _stationEyebrow;
    private readonly Label _overviewCopy;
    private readonly Label _recommendation;
    private readonly Label _breachAction;
    private readonly Label _tabNote;
    private readonly Label _feedback;
    private readonly Label _empty;
    private readonly Label _primaryLabel;
    private readonly Label _secondaryLabel;
    private readonly Label _workspaceHint;
    private readonly Label _marketPage;
    private readonly VisualElement _track;
    private readonly ScrollView _contentScroll;
    private readonly VisualElement _selectionTray;
    private readonly Label _selectionTrayEyebrow;
    private readonly Label _selectionTrayTitle;
    private readonly Label _selectionTraySummary;
    private readonly Label _selectionTrayBasic;
    private readonly Label _selectionTrayPrice;
    private readonly VisualElement _selectionTrayActions;
    private readonly Button _selectionTrayInspect;
    private readonly VisualElement _inspectorScrim;
    private readonly VisualElement _inspectorPane;
    private readonly VisualElement _inspectorActionDock;
    private readonly Button _inspectorExpand;
    private readonly Button _secondary;
    private readonly Button _continue;
    private readonly VisualElement _shelf;
    private readonly Label _shelfCapacity;
    private readonly Label _shelfReserveLabel;
    private readonly Label _shelfStoredCount;
    private readonly VisualElement _shelfField;
    private readonly VisualElement _shelfReserve;
    private readonly VisualElement _shelfStoredIcons;
    private readonly Button _shelfExpand;
    private readonly Button _shelfArmory;
    private readonly VisualElement _loadoutScrim;
    private readonly VisualElement _loadoutTable;
    private readonly VisualElement _loadoutField;
    private readonly VisualElement _loadoutReserve;
    private readonly VisualElement _loadoutInspectorSlot;
    private readonly VisualElement _loadoutActionDock;
    private readonly VisualElement _loadoutInventory;
    private readonly VisualElement _loadoutInventoryEmpty;
    private readonly Label _loadoutStoredCount;
    private readonly Button _loadoutClose;
    private readonly CardPool _primaryCards;
    private readonly CardPool _secondaryCards;
    private readonly CardPool _loadoutCards;
    private readonly MarketOfferPool _marketCards;
    private readonly InspectorPanel _inspector;
    private readonly InspectorPanel _loadoutInspector;
    private readonly CardRulesPopover _rules;
    private readonly VisualElement _choiceScrim;
    private readonly Label _choiceEyebrow;
    private readonly Label _choiceTitle;
    private readonly Label _choiceCopy;
    private readonly VisualElement _choiceOptions;
    private readonly VisualElement _rotateDevice;
    private readonly Dictionary<HallStation, Button> _stationButtons =
        new Dictionary<HallStation, Button>();
    private readonly Dictionary<HallStation, Button> _anchorButtons =
        new Dictionary<HallStation, Button>();
    private readonly HubFlowDirector _director;
    private readonly HubPresentationConfig _presentation;
    private readonly UiFxLayer _fxLayer;
    private readonly UiFeedbackDirector _polish;

    private HallStation _activeStation = HallStation.Overview;
    private bool _hallOverview = true;
    private bool _inspectorOpen;
    private bool _phone;
    private bool _forcePhone;
    private bool _routeInitialized;
    private bool _lastOverview = true;
    private HallStation _lastStation = HallStation.Overview;
    private bool _overviewRevealed;
    private string _choiceSignature = "";
    private readonly List<string> _trayTargetIds = new List<string>();
    private IVisualElementScheduledItem _marketScrollAnimation;
    private int _marketScrollGeneration;
    private bool _routePending;
    private bool _reducedMotion;
    private string _lastMarketSelectionKey = "";
    private string _lastDetailKey = "";
    private bool _lastLoadoutOpen;
    private string _lastLoadoutHeroKey = "";
    private string _partyShelfSignature = "";
    private readonly Dictionary<string, VisualElement> _shelfTargets =
        new Dictionary<string, VisualElement>();

    public RunScreen Screen => RunScreen.Management;
    public VisualElement Root => _root;

    public ManagementView(RunShellActions actions, UiFeedbackServices services = null)
    {
        _actions = actions;
        var tree = Resources.Load<VisualTreeAsset>("UI/ManagementHall");
        if (tree == null)
            throw new InvalidOperationException("[UI] Resources/UI/ManagementHall.uxml is required.");

        var host = new VisualElement();
        tree.CloneTree(host);
        _root = Required<VisualElement>(host, "management-root");
        _root.RemoveFromHierarchy();
        _safeFrame = Required<VisualElement>(_root, "safe-frame");
        _overview = Required<VisualElement>(_root, "hub-overview");
        _workspace = Required<VisualElement>(_root, "hub-workspace");
        _overview.usageHints |= UsageHints.GroupTransform;
        _workspace.usageHints |= UsageHints.GroupTransform;
        _act = Required<Label>(_root, "act");
        _beat = Required<Label>(_root, "station-eyebrow");
        _sand = Required<Label>(_root, "sand");
        _heading = Required<Label>(_root, "heading");
        _brief = Required<Label>(_root, "brief");
        _stationEyebrow = Required<Label>(_root, "station-eyebrow");
        _overviewCopy = Required<Label>(_root, "overview-copy");
        _recommendation = Required<Label>(_root, "recommendation");
        _breachAction = Required<Label>(_root, "breach-action");
        _tabNote = Required<Label>(_root, "tab-note");
        _feedback = Required<Label>(_root, "feedback");
        _empty = Required<Label>(_root, "empty");
        _primaryLabel = Required<Label>(_root, "primary-label");
        _secondaryLabel = Required<Label>(_root, "secondary-label");
        _workspaceHint = Required<Label>(_root, "workspace-hint");
        _marketPage = Required<Label>(_root, "market-page");
        _track = Required<VisualElement>(_root, "track");
        _contentScroll = Required<ScrollView>(_root, "content-scroll");
        _contentScroll.mode = ScrollViewMode.Vertical;
        _selectionTray = Required<VisualElement>(_root, "selection-tray");
        _selectionTrayEyebrow = Required<Label>(_root, "selection-tray-eyebrow");
        _selectionTrayTitle = Required<Label>(_root, "selection-tray-title");
        _selectionTraySummary = Required<Label>(_root, "selection-tray-summary");
        _selectionTrayBasic = Required<Label>(_root, "selection-tray-basic");
        _selectionTrayPrice = Required<Label>(_root, "selection-tray-price");
        _selectionTrayActions = Required<VisualElement>(_root, "selection-tray-actions");
        _selectionTrayInspect = Required<Button>(_root, "selection-tray-inspect");
        _inspectorScrim = Required<VisualElement>(_root, "inspector-scrim");
        _inspectorPane = Required<VisualElement>(_root, "inspector-pane");
        _inspectorActionDock = Required<VisualElement>(_root, "inspector-action-dock");
        _inspectorExpand = Required<Button>(_root, "inspector-close");
        _secondary = Required<Button>(_root, "secondary");
        _continue = Required<Button>(_root, "continue");
        _shelf = Required<VisualElement>(_root, "warband-shelf");
        _shelfCapacity = Required<Label>(_root, "shelf-capacity");
        _shelfReserveLabel = Required<Label>(_root, "shelf-reserve-label");
        _shelfStoredCount = Required<Label>(_root, "shelf-stored-count");
        _shelfField = Required<VisualElement>(_root, "shelf-field");
        _shelfReserve = Required<VisualElement>(_root, "shelf-reserve");
        _shelfStoredIcons = Required<VisualElement>(_root, "shelf-stored-icons");
        _shelfExpand = Required<Button>(_root, "shelf-expand");
        _shelfArmory = Required<Button>(_root, "shelf-armory");
        _loadoutScrim = Required<VisualElement>(_root, "loadout-scrim");
        _loadoutTable = Required<VisualElement>(_root, "loadout-table");
        _loadoutField = Required<VisualElement>(_root, "loadout-field");
        _loadoutReserve = Required<VisualElement>(_root, "loadout-reserve");
        _loadoutInspectorSlot = Required<VisualElement>(_root, "loadout-inspector-slot");
        _loadoutActionDock = Required<VisualElement>(_root, "loadout-action-dock");
        _loadoutInventory = Required<VisualElement>(_root, "loadout-inventory");
        _loadoutInventoryEmpty =
            Required<VisualElement>(_root, "loadout-inventory-empty");
        _loadoutStoredCount = Required<Label>(_root, "loadout-stored-count");
        _loadoutClose = Required<Button>(_root, "loadout-close");

        _inspector = new InspectorPanel(id => _actions.InspectorAction?.Invoke(id));
        _inspector.Root.AddToClassList("wb-inspector--hub");
        Required<VisualElement>(_root, "inspector-slot").Add(_inspector.Root);
        // Hall decisions should never hide their commit below dossier content. The reusable
        // inspector still owns and binds its actions; the Hall simply presents that action rail
        // as a pinned command dock outside the scroll view.
        _inspectorActionDock.Add(_inspector.ActionsRoot);
        _inspectorExpand.clicked += () =>
        {
            if (_inspectorOpen) _actions.CloseInspector?.Invoke();
            else _actions.OpenInspector?.Invoke();
        };
        _selectionTrayInspect.clicked += () => _actions.OpenInspector?.Invoke();
        _inspectorScrim.RegisterCallback<ClickEvent>(evt =>
        {
            if (_inspectorOpen && evt.target == _inspectorScrim)
                _actions.CloseInspector?.Invoke();
        });

        _loadoutInspector = new InspectorPanel(id => _actions.InspectorAction?.Invoke(id));
        _loadoutInspector.Root.AddToClassList("wb-inspector--loadout");
        _loadoutInspectorSlot.Add(_loadoutInspector.Root);
        _loadoutActionDock.Add(_loadoutInspector.ActionsRoot);
        _shelfExpand.clicked += () => _actions.OpenLoadout?.Invoke("");
        _shelfArmory.clicked += () => _actions.OpenLoadout?.Invoke("");
        _loadoutClose.clicked += () => _actions.CloseLoadout?.Invoke();
        _loadoutScrim.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _loadoutScrim) _actions.CloseLoadout?.Invoke();
        });

        _choiceScrim = Required<VisualElement>(_root, "choice-scrim");
        _choiceEyebrow = Required<Label>(_root, "choice-eyebrow");
        _choiceTitle = Required<Label>(_root, "choice-title");
        _choiceCopy = Required<Label>(_root, "choice-copy");
        _choiceOptions = Required<VisualElement>(_root, "choice-options");
        _rotateDevice = Required<VisualElement>(_root, "rotate-device");

        _presentation = HubPresentationConfig.Load();
        _fxLayer = new UiFxLayer(_presentation);
        // Safe frame is child 0 and blocking choices sit above it. The shared FX plane goes
        // between them, so normal feedback cannot paint over a required decision.
        _root.Insert(Mathf.Min(1, _root.childCount), _fxLayer);
        _polish = new UiFeedbackDirector(_root, _presentation, _fxLayer,
            services?.Haptics, services?.Audio);
        RegisterPolishTarget("hub-workspace", _workspace);
        RegisterPolishTarget("hub-overview", _overview);
        RegisterPolishTarget("ledger-sand", _sand);
        RegisterPolishTarget("warband-shelf", _shelf);
        RegisterPolishTarget("shelf-armory", _shelfArmory);
        RegisterPolishTarget("feedback", _feedback);
        RegisterPolishTarget("action-secondary", _secondary, true);
        RegisterPolishTarget("action-continue", _continue, true);
        RegisterPolishTarget("selected-detail", _inspector.Root);

        _rules = new CardRulesPopover(_root);
        VisualElement primaryGrid = Required<VisualElement>(_root, "primary-grid");
        _primaryCards = new CardPool(primaryGrid,
            key => _actions.SelectPlanningCard?.Invoke(key), _polish);
        _secondaryCards = new CardPool(Required<VisualElement>(_root, "secondary-grid"),
            key => _actions.SelectPlanningCard?.Invoke(key), _polish);
        _loadoutCards = new CardPool(_loadoutInventory,
            key => _actions.SelectLoadoutItem?.Invoke(key), _polish);
        _marketCards = new MarketOfferPool(primaryGrid,
            key => _actions.SelectPlanningCard?.Invoke(key), _polish,
            null, null);

        RegisterStation(HallStation.Breach, "station-breach", null);
        RegisterStation(HallStation.Market, "station-market", "anchor-market");
        RegisterStation(HallStation.Warband, "station-warband", "anchor-warband");
        RegisterStation(HallStation.Armory, "station-armory", "anchor-armory");
        RegisterStation(HallStation.Hourstone, "station-hourstone", "anchor-hourstone");

        var overviewBack = Required<Button>(_root, "overview-back");
        overviewBack.clicked +=
            () => BeginRoute(HallStation.Overview, () => _actions.OpenHallOverview?.Invoke());
        RegisterPolishTarget("action-table", overviewBack, true);
        _secondary.clicked += () => _actions.Reroll?.Invoke();
        _continue.clicked += () => _actions.Advance?.Invoke();
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        _root.RegisterCallback<GeometryChangedEvent>(_ => ApplyResponsiveLayout());

        _director = new HubFlowDirector(_overview, _workspace, _presentation);
    }

    public void Bind(RunShellModel shell)
    {
        var model = shell.Planning;
        bool routeChanged = !_routeInitialized ||
                            _lastOverview != model.HallOverview ||
                            _lastStation != model.ActiveStation;
        _activeStation = model.ActiveStation;
        _hallOverview = model.HallOverview;
        _inspectorOpen = model.InspectorOpen;
        _forcePhone = model.ForcePhoneLayout;
        _reducedMotion = model.ReducedMotion;

        _act.text = model.Act;
        _sand.text = model.Sand;
        _overviewCopy.text = $"{model.Beat}. The Table keeps every service in one remembered place.";
        _recommendation.text = StationName(model.RecommendedStation) + "  ›";
        _root.EnableInClassList("motion--reduced", model.ReducedMotion);
        SetStationClass(model.ActiveStation);
        _polish.SetReducedMotion(model.ReducedMotion);

        if (routeChanged)
        {
            _rules.Hide();
            CancelMarketScroll();
            // Presentation below this point changes geometry. Retire any effect snapshots from
            // the outgoing layout before showing the destination.
            _polish.BeginLayoutTransition();
            _director.Show(model.HallOverview, model.ActiveStation, model.ReducedMotion);
            _routeInitialized = true;
            _lastOverview = model.HallOverview;
            _lastStation = model.ActiveStation;
        }
        else
        {
            _overview.style.display = model.HallOverview ? DisplayStyle.Flex : DisplayStyle.None;
            _workspace.style.display = model.HallOverview ? DisplayStyle.None : DisplayStyle.Flex;
        }

        BindTrack(model.Track);
        BindStations(model.Stations, model.RecommendedStation);
        BindWorkspace(model);
        BindPartyShelf(model.PartyShelf);
        BindBlockingChoice(model);
        ApplyResponsiveLayout();
        FollowMarketSelection(model, routeChanged);

        if (model.HallOverview)
        {
            if (!_overviewRevealed)
            {
                _overviewRevealed = true;
                _polish.RevealBatch(new List<VisualElement>(_stationButtons.Values), -1);
            }
        }
        else
        {
            int direction = model.ActiveStation == HallStation.Warband ? -1 : 1;
            _polish.RevealBatch(_marketCards.TakePendingReveals(), direction);
            _polish.RevealBatch(_primaryCards.TakePendingReveals(), direction);
            _polish.RevealBatch(_secondaryCards.TakePendingReveals(), direction);
        }
    }

    private void RegisterStation(HallStation station, string stationName, string anchorName)
    {
        var button = Required<Button>(_root, stationName);
        button.clicked += () =>
        {
            BeginRoute(station, () =>
            {
                if (station == HallStation.Breach) _actions.Advance?.Invoke();
                else _actions.OpenHallStation?.Invoke((int)station);
            });
        };
        HallStationPresentationDefinition presentation =
            HallStationPresentationCatalog.Shared[station];
        button.tooltip = $"{presentation.title} · {presentation.motionVerb}";
        _stationButtons[station] = button;
        Label oldSigil = Required<Label>(_root, StationPrefix(station) + "-sigil");
        oldSigil.style.display = DisplayStyle.None;
        button.Insert(0, new UiStationSigil(station));
        RegisterPolishTarget(stationName, button, true);
        if (string.IsNullOrEmpty(anchorName)) return;
        var anchor = Required<Button>(_root, anchorName);
        anchor.clicked += () => BeginRoute(station,
            () => _actions.OpenHallStation?.Invoke((int)station));
        anchor.text = "";
        var compassSigil = new UiStationSigil(station);
        compassSigil.AddToClassList("hub-anchor__sigil");
        anchor.Add(compassSigil);
        var compassLabel = new Label(StationName(station).ToUpperInvariant());
        compassLabel.AddToClassList("hub-anchor__label");
        anchor.Add(compassLabel);
        _anchorButtons[station] = anchor;
        RegisterPolishTarget(anchorName, anchor, true);
    }

    private void BindStations(IReadOnlyList<HallStationModel> stations,
                              HallStation recommended)
    {
        foreach (var station in stations)
        {
            if (!_stationButtons.TryGetValue(station.Station, out var button)) continue;
            string prefix = StationPrefix(station.Station);
            Required<Label>(_root, prefix + "-sigil").text = station.Sigil;
            Required<Label>(_root, prefix + "-eyebrow").text = station.Eyebrow;
            Required<Label>(_root, prefix + "-name").text = station.Name;
            Required<Label>(_root, prefix + "-status").text = station.Status;
            if (station.Station == HallStation.Breach)
                _breachAction.text = station.Action;
            button.SetEnabled(station.Enabled);
            button.EnableInClassList("hub-station--attention", station.Attention);
            button.EnableInClassList("hub-station--recommended",
                station.Station == recommended);
            var attention = Required<Label>(_root, prefix + "-attention");
            attention.style.display = station.Station != HallStation.Breach &&
                                      (station.Attention || station.Station == recommended)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            attention.text = station.Station == recommended ? "NEXT" : "NEW";
        }

        foreach (var pair in _anchorButtons)
        {
            pair.Value.EnableInClassList("hub-anchor--active", pair.Key == _activeStation);
            HallStationModel station = FindStation(stations, pair.Key);
            pair.Value.EnableInClassList("hub-anchor--attention",
                station != null && station.Attention);
        }
    }

    private void BindWorkspace(PlanningModel model)
    {
        _stationEyebrow.text = model.Beat;
        _heading.text = model.Heading;
        _brief.text = model.Brief;
        _feedback.text = model.Feedback;
        _feedback.EnableInClassList("feedback-label--error", model.FeedbackIsError);
        SetDisplayed(_feedback, !string.IsNullOrEmpty(model.Feedback));

        IReadOnlyList<CardModel> primary;
        IReadOnlyList<CardModel> secondary;
        string variant;
        bool market;
        switch (model.ActiveStation)
        {
            case HallStation.Market:
                primary = model.Market;
                secondary = EmptyCards;
                variant = "market";
                market = true;
                _primaryLabel.text = "LIVE STOCK";
                _secondaryLabel.text = "";
                _tabNote.text = model.SlotOfferOpen
                    ? model.SlotOfferText
                    : "Tap or focus an offer to compare it. Buying is always a separate action.";
                _workspaceHint.text = "Stock refreshes after a resolved beat. Held stock survives a refresh.";
                break;
            case HallStation.Armory:
                primary = model.Armory;
                secondary = Combine(model.Field, model.Bench);
                variant = "armory";
                market = false;
                _primaryLabel.text = "EQUIPMENT RACK";
                _secondaryLabel.text = primary.Count > 0 ? "COMPATIBLE CHAMPIONS" : "";
                _tabNote.text = "Select equipment, then choose a champion. The dossier shows the exact action.";
                _workspaceHint.text = "Equipping can return the champion’s current item to this rack.";
                break;
            case HallStation.Hourstone:
                primary = model.Inscriptions;
                secondary = EmptyCards;
                variant = "hourstone";
                market = false;
                _primaryLabel.text = "BOUND INSCRIPTIONS";
                _secondaryLabel.text = "";
                _tabNote.text = "These run-wide laws remain active for the rest of the run.";
                _workspaceHint.text = "Only authored rules are shown; no speculative synergy claims.";
                break;
            default:
                primary = model.Field;
                secondary = model.Bench;
                variant = "warband";
                market = false;
                _primaryLabel.text = "FIELD";
                _secondaryLabel.text = "RESERVES";
                _tabNote.text = "Select a champion for exact attacks, Signature, Passive, equipment, and actions.";
                _workspaceHint.text = "Selection never moves or sells a champion. Use the dossier action rail.";
                break;
        }

        if (market)
        {
            _primaryCards.Bind(EmptyCards, variant);
            _marketCards.Bind(model.MarketOffers);
        }
        else
        {
            _marketCards.Bind(EmptyMarketOffers);
            _primaryCards.Bind(primary, variant);
        }
        _secondaryCards.Bind(secondary, variant == "armory" ? "armory-target" : variant);
        bool empty = (market ? model.MarketOffers.Count : primary.Count) == 0 &&
                     secondary.Count == 0;
        _empty.text = EmptyCopy(model.ActiveStation);
        SetDisplayed(_empty, empty);
        SetDisplayed(_primaryLabel, !empty);
        SetDisplayed(_secondaryLabel, secondary.Count > 0);
        BindMarketPage(model);

        if (model.RerollCost >= 0)
            MechanicPresentation.BindCurrencyButton(
                _secondary, model.RerollLabel, model.RerollCost);
        else
            _secondary.text = model.RerollLabel;
        _secondary.SetEnabled(model.CanReroll);
        SetDisplayed(_secondary, model.ActiveStation == HallStation.Market);
        _continue.text = model.CommitLabel;
        _continue.SetEnabled(model.CanCommit);
        bool showContinue = model.BeatKind == PlanningBeat.Fight ||
                            model.BeatKind == PlanningBeat.Boss;
        SetDisplayed(_continue, showContinue);
        SetDisplayed(_continue.parent, showContinue);

        _inspector.Bind(model.Inspector);
        BindSelectionTray(model);
        _root.EnableInClassList("hub--detail-empty", model.Inspector.Empty);
        SetDisplayed(_inspectorActionDock, !model.Inspector.Empty);
        SetDisplayed(_inspectorScrim, !model.HallOverview && !model.Inspector.Empty);
        SetDisplayed(_inspectorPane, true);
        _inspectorScrim.EnableInClassList("hub-inspector-scrim--expanded",
            model.InspectorOpen && !model.Inspector.Empty);
        _inspectorExpand.text = model.InspectorOpen ? "CLOSE  ×" : "EXPAND  ↗";
        _inspectorExpand.tooltip = model.InspectorOpen
            ? "Return to the split-stage view."
            : "Open the selected card as a full dossier.";

        if (!model.Inspector.Empty &&
            !string.Equals(_lastDetailKey, model.Inspector.Key, StringComparison.Ordinal))
        {
            _lastDetailKey = model.Inspector.Key;
            _polish.Reveal(_inspector.Root, _presentation.detailSwap);
        }
        else if (model.Inspector.Empty)
            _lastDetailKey = "";
    }

    private void BindSelectionTray(PlanningModel model)
    {
        // Detail and actions now live together in the persistent selected-card stage. Retain the
        // old UXML node as a compatibility seam for serialized captures, but never duplicate the
        // selected card into a second tray.
        SetDisplayed(_selectionTray, false);
        _selectionTrayActions.Clear();
        _trayTargetIds.Clear();
    }

    private void BindPartyShelf(PartyShelfModel model)
    {
        _shelfCapacity.text = $"FIELD  {model.FieldCount} / {model.FieldCapacity}";
        _shelfReserveLabel.text = $"RESERVE  {model.ReserveCount} / {model.ReserveCapacity}";
        _shelfStoredCount.text = model.StoredItems.Count == 1
            ? "1 STORED"
            : $"{model.StoredItems.Count} STORED";
        _loadoutStoredCount.text = _shelfStoredCount.text;
        SetDisplayed(_loadoutInventoryEmpty, model.StoredItems.Count == 0);

        // Most Hall actions rebuild the model even though the persistent party shelf did not
        // change. Retain its compact and expanded visual trees in that common case: clearing and
        // recreating both copies also reloaded every portrait and invalidated layout for the
        // entire shelf.
        string shelfSignature = PartyShelfSignature(model);
        if (!string.Equals(_partyShelfSignature, shelfSignature, StringComparison.Ordinal))
        {
            foreach (var target in _shelfTargets)
                _polish.UnregisterTarget(target.Key, target.Value);
            _shelfTargets.Clear();
            _shelfField.Clear();
            _shelfReserve.Clear();
            _shelfStoredIcons.Clear();
            _loadoutField.Clear();
            _loadoutReserve.Clear();

            foreach (PartySlotModel slot in model.Field)
            {
                _shelfField.Add(PartySlot(slot, expanded: false));
                _loadoutField.Add(PartySlot(slot, expanded: true));
            }
            foreach (PartySlotModel slot in model.Reserve)
            {
                _shelfReserve.Add(PartySlot(slot, expanded: false));
                _loadoutReserve.Add(PartySlot(slot, expanded: true));
            }

            int shown = Mathf.Min(3, model.StoredItems.Count);
            for (int i = 0; i < shown; i++)
            {
                StoredItemSummaryModel item = model.StoredItems[i];
                var icon = new Label(item.Icon);
                icon.AddToClassList("warband-shelf__stored-icon");
                icon.AddToClassList("accent--" + item.Accent);
                icon.tooltip = $"{item.Name} · {item.Kind}";
                _shelfStoredIcons.Add(icon);
            }
            if (model.StoredItems.Count > shown)
            {
                var overflow = new Label("+" + (model.StoredItems.Count - shown));
                overflow.AddToClassList("warband-shelf__stored-overflow");
                _shelfStoredIcons.Add(overflow);
            }

            _partyShelfSignature = shelfSignature;
        }

        _root.EnableInClassList("loadout--open", model.Expanded);
        SetDisplayed(_loadoutScrim, model.Expanded);
        if (model.Expanded)
        {
            _loadoutInspector.Bind(model.LoadoutInspector);
            _loadoutCards.Bind(model.LoadoutInventory, "armory");
            if (!_lastLoadoutOpen)
                _polish.Reveal(_loadoutTable, _presentation.shelfExpand);
            else if (model.FocusedHeroKey != _lastLoadoutHeroKey)
            {
                string targetId = "loadout-" + model.FocusedHeroKey;
                if (_shelfTargets.TryGetValue(targetId, out VisualElement focused))
                    _polish.Reveal(focused, _presentation.shelfFocus);
                _polish.Reveal(_loadoutInspector.Root, _presentation.detailSwap);
            }
        }
        else
        {
            if (_lastLoadoutOpen)
                _loadoutCards.Bind(EmptyCards, "armory");
            if (_lastLoadoutOpen)
                _polish.Reveal(_shelf, _presentation.shelfCollapse);
        }
        _lastLoadoutOpen = model.Expanded;
        _lastLoadoutHeroKey = model.Expanded ? model.FocusedHeroKey : "";
    }

    private static string PartyShelfSignature(PartyShelfModel model)
    {
        var signature = new StringBuilder(512);
        AppendSlots(signature, model.Field);
        AppendSlots(signature, model.Reserve);
        for (int i = 0; i < model.StoredItems.Count; i++)
        {
            StoredItemSummaryModel item = model.StoredItems[i];
            AppendSignatureText(signature, item.Key);
            AppendSignatureText(signature, item.Name);
            AppendSignatureText(signature, item.Kind);
            AppendSignatureText(signature, item.Icon);
            AppendSignatureText(signature, item.Accent);
        }
        return signature.ToString();
    }

    private static void AppendSlots(StringBuilder signature, IReadOnlyList<PartySlotModel> slots)
    {
        signature.Append(slots.Count).Append('|');
        for (int i = 0; i < slots.Count; i++)
        {
            PartySlotModel slot = slots[i];
            AppendSignatureText(signature, slot.Key);
            signature.Append(slot.Index).Append('|')
                .Append(slot.Reserve ? '1' : '0').Append('|')
                .Append((int)slot.State).Append('|');
            AppendSignatureText(signature, slot.Name);
            AppendSignatureText(signature, slot.Rank);
            AppendSignatureText(signature, slot.Role);
            AppendSignatureText(signature, slot.PortraitResource);
            AppendSignatureText(signature, slot.PortraitFallback);
            AppendSignatureText(signature, slot.Accent);
            AppendSignatureText(signature, slot.Weapon);
            AppendSignatureText(signature, slot.Trinket);
            signature.Append(slot.Focused ? '1' : '0').Append('|');
        }
    }

    private static void AppendSignatureText(StringBuilder signature, string value)
    {
        value = value ?? "";
        signature.Append(value.Length).Append(':').Append(value).Append('|');
    }

    private Button PartySlot(PartySlotModel model, bool expanded)
    {
        var button = new Button();
        button.AddToClassList("party-slot");
        button.EnableInClassList("party-slot--loadout", expanded);
        button.EnableInClassList("party-slot--reserve", model.Reserve);
        button.EnableInClassList("party-slot--occupied",
            model.State == PartySlotState.Occupied);
        button.EnableInClassList("party-slot--empty",
            model.State == PartySlotState.Empty);
        button.EnableInClassList("party-slot--locked",
            model.State == PartySlotState.Locked);
        button.EnableInClassList("party-slot--focused", model.Focused);
        button.userData = model.Key;

        string baseTargetId = model.State == PartySlotState.Occupied
            ? model.Key
            : model.Reserve ? $"shelf-reserve:{model.Index}" : $"shelf-field:{model.Index}";
        string targetId = expanded ? "loadout-" + baseTargetId : baseTargetId;
        if (model.State == PartySlotState.Occupied)
        {
            var portrait = new VisualElement();
            portrait.AddToClassList("party-slot__portrait");
            var texture = string.IsNullOrEmpty(model.PortraitResource)
                ? null
                : Resources.Load<Texture2D>(model.PortraitResource);
            portrait.style.backgroundImage = texture == null
                ? new StyleBackground(StyleKeyword.None)
                : new StyleBackground(Background.FromTexture2D(texture));
            var fallback = new Label(model.PortraitFallback);
            fallback.AddToClassList("party-slot__fallback");
            SetDisplayed(fallback, texture == null);
            portrait.Add(fallback);
            button.Add(portrait);

            var rank = new Label(model.Rank);
            rank.AddToClassList("party-slot__rank");
            button.Add(rank);

            var loadout = new VisualElement();
            loadout.AddToClassList("party-slot__loadout");
            var weapon = new Label("⚔");
            weapon.AddToClassList("party-slot__equipment");
            weapon.EnableInClassList("party-slot__equipment--empty",
                string.IsNullOrEmpty(model.Weapon));
            weapon.tooltip = string.IsNullOrEmpty(model.Weapon)
                ? "Starter weapon"
                : model.Weapon;
            var trinket = new Label("◇");
            trinket.AddToClassList("party-slot__equipment");
            trinket.EnableInClassList("party-slot__equipment--empty",
                string.IsNullOrEmpty(model.Trinket));
            trinket.tooltip = string.IsNullOrEmpty(model.Trinket)
                ? "Empty trinket socket"
                : model.Trinket;
            loadout.Add(weapon);
            loadout.Add(trinket);
            button.Add(loadout);
            button.tooltip =
                $"{model.Name} · Rank {model.Rank}\n{model.Role}\n" +
                $"Weapon: {(string.IsNullOrEmpty(model.Weapon) ? "Starter" : model.Weapon)}\n" +
                $"Trinket: {(string.IsNullOrEmpty(model.Trinket) ? "Empty" : model.Trinket)}";
            button.clicked += () =>
            {
                if (expanded) _actions.SelectLoadoutHero?.Invoke(model.Key);
                else _actions.OpenLoadout?.Invoke(model.Key);
            };
        }
        else
        {
            var mark = new Label(model.State == PartySlotState.Locked ? "◇" : "+");
            mark.AddToClassList("party-slot__empty-mark");
            button.Add(mark);
            var number = new Label((model.Index + 1).ToString());
            number.AddToClassList("party-slot__number");
            button.Add(number);
            button.tooltip = model.State == PartySlotState.Locked
                ? $"Field place {model.Index + 1} is locked. Capacity upgrades open it."
                : model.Reserve ? "Empty reserve place." : "Open field place.";
            button.clicked += () => _actions.OpenLoadout?.Invoke("");
        }

        _polish.AttachInteractable(button, () => targetId);
        _polish.RegisterTarget(targetId, button);
        _shelfTargets[targetId] = button;
        return button;
    }

    private static readonly IReadOnlyList<CardModel> EmptyCards = new List<CardModel>();
    private static readonly IReadOnlyList<MarketOfferCardModel> EmptyMarketOffers =
        new List<MarketOfferCardModel>();

    private void BindMarketPage(PlanningModel model)
    {
        bool shown = model.ActiveStation == HallStation.Market &&
                     model.MarketOffers.Count > 0;
        SetDisplayed(_marketPage, shown);
        if (!shown) return;

        int selected = 0;
        for (int i = 0; i < model.MarketOffers.Count; i++)
            if (model.MarketOffers[i].Selected)
            {
                selected = i;
                break;
            }
        _marketPage.text = $"{selected + 1} / {model.MarketOffers.Count}  ·  SWIPE OR FOCUS";
    }

    private static IReadOnlyList<CardModel> Combine(IReadOnlyList<CardModel> first,
                                                     IReadOnlyList<CardModel> second)
    {
        var result = new List<CardModel>(first.Count + second.Count);
        for (int i = 0; i < first.Count; i++) result.Add(first[i]);
        for (int i = 0; i < second.Count; i++) result.Add(second[i]);
        return result;
    }

    private string LedgerDetail(PlanningModel model)
    {
        if (model.ActiveStation == HallStation.Market)
        {
            CardModel selected = FindSelected(model.Market);
            if (selected != null)
                return selected.Title + " selected for inspection";
            return $"{model.Market.Count} offer{(model.Market.Count == 1 ? "" : "s")} visible";
        }
        if (model.ActiveStation == HallStation.Armory)
            return $"{model.Armory.Count} stored item{(model.Armory.Count == 1 ? "" : "s")}";
        if (model.ActiveStation == HallStation.Hourstone)
            return $"{model.Inscriptions.Count} law{(model.Inscriptions.Count == 1 ? "" : "s")} bound";
        return model.Capacity + " active field places";
    }

    private static CardModel FindSelected(IReadOnlyList<CardModel> cards)
    {
        for (int i = 0; i < cards.Count; i++)
            if (cards[i].Selected) return cards[i];
        return null;
    }

    private void BindTrack(IReadOnlyList<PlanningTrackNodeModel> nodes)
    {
        _track.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = new VisualElement();
            node.AddToClassList("management-track-node");
            node.AddToClassList("management-track-node--" + nodes[i].State);
            var icon = new Label(BeatIcon(nodes[i].Kind));
            icon.AddToClassList("management-track-node__icon");
            node.Add(icon);
            var label = new Label(nodes[i].Label);
            label.AddToClassList("management-track-node__label");
            node.Add(label);
            _track.Add(node);
            if (i + 1 >= nodes.Count) continue;
            var link = new VisualElement();
            link.AddToClassList("management-track-link");
            link.EnableInClassList("management-track-link--past", nodes[i].State == "past");
            _track.Add(link);
        }
    }

    private void BindBlockingChoice(PlanningModel model)
    {
        bool spec = model.SpecChoice.Pending;
        bool reward = model.BeatKind == PlanningBeat.Interlude ||
                      model.BeatKind == PlanningBeat.BossReward;
        SetDisplayed(_choiceScrim, spec || reward);
        if (!spec && !reward)
        {
            _choiceSignature = "";
            return;
        }

        _choiceOptions.Clear();
        var reveals = new List<VisualElement>();
        string signature;
        if (spec)
        {
            signature = "spec|" + model.SpecChoice.HeroName + "|" +
                        model.SpecChoice.OptionAName + "|" + model.SpecChoice.OptionBName;
            _choiceEyebrow.text = model.SpecChoice.RankLabel + " AWAKENING";
            _choiceTitle.text = model.SpecChoice.HeroName;
            MechanicPresentation.BindInline(_choiceCopy,
                "Choose one path before making another management decision.");
            reveals.Add(AddChoice(model.SpecChoice.OptionAName, model.SpecChoice.OptionAText,
                () => _actions.ChooseSpec?.Invoke(0), model.SpecChoice.OptionAChange,
                model.SpecChoice.OptionAComparisons));
            reveals.Add(AddChoice(model.SpecChoice.OptionBName, model.SpecChoice.OptionBText,
                () => _actions.ChooseSpec?.Invoke(1), model.SpecChoice.OptionBChange,
                model.SpecChoice.OptionBComparisons));
        }
        else
        {
            signature = (model.BeatKind == PlanningBeat.BossReward ? "boss|" : "interlude|") +
                        string.Join("|", model.Interlude.ConvertAll(choice => choice.Card.ContentId));
            _choiceEyebrow.text = model.BeatKind == PlanningBeat.BossReward ? "BOSS REWARD" : "INTERLUDE";
            _choiceTitle.text = model.BeatKind == PlanningBeat.BossReward
                ? "Bind one Inscription"
                : "Choose what this quiet Hour leaves behind";
            MechanicPresentation.BindInline(_choiceCopy, model.Brief);
            foreach (var choice in model.Interlude)
            {
                int path = choice.Path;
                int option = choice.Option;
                reveals.Add(AddChoice(choice.Card.Title, choice.Card.AbilitySummary, () =>
                {
                    if (model.BeatKind == PlanningBeat.BossReward)
                        _actions.ChooseBossReward?.Invoke(option);
                    else
                        _actions.ChooseInterlude?.Invoke(path, option);
                }));
            }
        }

        if (!string.Equals(_choiceSignature, signature, StringComparison.Ordinal))
        {
            _choiceSignature = signature;
            _polish.RevealBatch(reveals);
        }
    }

    private Button AddChoice(string title, string copy, Action action, string change = "",
                             IReadOnlyList<StatComparisonModel> comparisons = null)
    {
        var button = new Button(action);
        button.AddToClassList("management-choice");
        DecisionCardPresentation.ApplyProfile(button, DecisionCardProfile.Feature);
        if (!string.IsNullOrEmpty(change))
        {
            var badge = new Label(change);
            badge.AddToClassList("management-choice__change");
            button.Add(badge);
        }
        var name = new Label(title);
        name.AddToClassList("management-choice__title");
        var rule = new Label();
        rule.AddToClassList("management-choice__copy");
        MechanicPresentation.BindInline(rule, copy);
        button.Add(name);
        button.Add(rule);
        if (comparisons != null)
            foreach (var comparison in comparisons)
            {
                var row = new VisualElement();
                row.AddToClassList("management-choice__delta");
                PresentationFactId id =
                    DecisionCardPresentation.FactId(comparison.Label);
                DecisionFactDefinition definition =
                    DecisionCardPresentation.Fact(id);
                DecisionCardPresentation.ApplyFact(row, id);
                var icon = new WarbandGlyph(definition.Glyph);
                icon.SetColor(definition.Color);
                icon.AddToClassList("management-choice__delta-icon");
                var label = new Label(definition.Label.Length > 0
                    ? definition.Label
                    : comparison.Label);
                label.AddToClassList("management-choice__delta-label");
                var value = new Label(comparison.Before + "  →  " + comparison.After);
                value.AddToClassList("management-choice__delta-value");
                row.Add(icon);
                row.Add(label);
                row.Add(value);
                button.Add(row);
            }
        _choiceOptions.Add(button);
        string target = "choice:" + title;
        RegisterPolishTarget(target, button, true);
        return button;
    }

    public void Dispose()
    {
        CancelMarketScroll();
        _director.Cancel();
        _polish.Dispose();
    }

    /// <summary>
    /// A horizontal rail may be swiped freely until selection changes. At that point the selected
    /// object is eased into view using the shared Select duration; reduced motion snaps instead.
    /// This is intentionally presentation-only and reusable by future paged Hall rails.
    /// </summary>
    private void FollowMarketSelection(PlanningModel model, bool routeChanged)
    {
        if (model.HallOverview || model.ActiveStation != HallStation.Market || !_phone)
        {
            _lastMarketSelectionKey = "";
            CancelMarketScroll();
            return;
        }

        MarketOfferCardModel selected = null;
        for (int i = 0; i < model.MarketOffers.Count; i++)
            if (model.MarketOffers[i].Selected)
            {
                selected = model.MarketOffers[i];
                break;
            }
        if (selected == null) return;

        bool changed = !string.Equals(
            _lastMarketSelectionKey, selected.Key, StringComparison.Ordinal);
        if (!changed && !routeChanged) return;
        _lastMarketSelectionKey = selected.Key;

        VisualElement target = _marketCards.FindSelected(model.MarketOffers);
        if (target == null) return;
        AnimateHorizontalSelectionIntoView(target);
    }

    private void AnimateHorizontalSelectionIntoView(VisualElement target)
    {
        CancelMarketScroll();
        int generation = ++_marketScrollGeneration;
        float startOffset = 0f;
        float targetOffset = 0f;
        float startedAt = -1f;
        int unresolvedLayoutPasses = 0;

        IVisualElementScheduledItem animation = null;
        animation = _contentScroll.schedule.Execute(() =>
        {
            if (generation != _marketScrollGeneration ||
                target.panel == null ||
                _contentScroll.panel == null)
            {
                animation?.Pause();
                return;
            }

            Rect viewport = _contentScroll.contentViewport.worldBound;
            Rect bounds = target.worldBound;
            if (startedAt < 0f)
            {
                float maximum =
                    Mathf.Max(0f, _contentScroll.horizontalScroller.highValue);
                // A responsive class change can reach the scheduler one pass before Yoga has
                // expanded the horizontal rail. Give it a few retained-layout passes before
                // deciding that the old desktop geometry is already visible.
                if (maximum <= 0.5f && unresolvedLayoutPasses++ < 3) return;

                const float breathingRoom = 18f;
                bool visible = bounds.xMin >= viewport.xMin + breathingRoom &&
                               bounds.xMax <= viewport.xMax - breathingRoom;
                if (visible)
                {
                    animation?.Pause();
                    _marketScrollAnimation = null;
                    return;
                }

                startOffset = _contentScroll.scrollOffset.x;
                targetOffset = Mathf.Clamp(
                    startOffset + bounds.center.x - viewport.center.x, 0f, maximum);
                startedAt = Time.realtimeSinceStartup;
                if (_reducedMotion)
                {
                    _contentScroll.scrollOffset =
                        new Vector2(targetOffset, _contentScroll.scrollOffset.y);
                    animation?.Pause();
                    _marketScrollAnimation = null;
                    return;
                }
            }

            float duration = Mathf.Clamp(_presentation.select.durationMs, 120, 260) / 1000f;
            float t = Mathf.Clamp01((Time.realtimeSinceStartup - startedAt) / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _contentScroll.scrollOffset = new Vector2(
                Mathf.LerpUnclamped(startOffset, targetOffset, eased),
                _contentScroll.scrollOffset.y);
            if (t < 1f) return;
            animation?.Pause();
            _marketScrollAnimation = null;
        }).Every(16);
        _marketScrollAnimation = animation;
    }

    private void CancelMarketScroll()
    {
        _marketScrollGeneration++;
        _marketScrollAnimation?.Pause();
        _marketScrollAnimation = null;
    }

#if UNITY_EDITOR
    internal int EditorActiveEffectCount => _fxLayer.ActiveEffectCount;

    internal bool EditorValidateMarketOfferLayout()
    {
        List<VisualElement> cards =
            _root.Query<VisualElement>(className: "market-offer-card").ToList();
        if (_activeStation != HallStation.Market || _hallOverview) return cards.Count == 0;
        if (cards.Count == 0) return false;

        VisualElement collection =
            _root.Q<VisualElement>(className: "hub-collection");
        if (collection == null || _inspectorScrim.resolvedStyle.display == DisplayStyle.None)
            return false;
        if (!_phone && !_inspectorOpen &&
            collection.worldBound.xMax > _inspectorScrim.worldBound.xMin + 0.75f)
            return false;

        foreach (VisualElement card in cards)
        {
            VisualElement main =
                card.Q<VisualElement>(className: "market-offer-card__main");
            VisualElement read =
                card.Q<VisualElement>(className: "market-offer-card__read");
            VisualElement commerce = card.Q<VisualElement>("commerce");
            VisualElement qualifier = card.Q<VisualElement>("qualifier");
            Label title = card.Q<Label>("title");
            VisualElement price =
                card.Q<VisualElement>(className: "currency-amount");
            Label priceValue =
                price?.Q<Label>(className: "currency-amount__value");
            if (main == null || read == null || commerce == null ||
                qualifier == null || title == null || price == null ||
                priceValue == null)
                return false;

            // Stock cards are intentionally recognition + price only. Exact rules, metrics, and
            // numeric qualifiers belong to the selected Detail stage.
            if (read.resolvedStyle.display != DisplayStyle.None ||
                qualifier.resolvedStyle.display != DisplayStyle.None)
                return false;
            if (commerce.resolvedStyle.height < 44f) return false;
            if (main.worldBound.yMax > commerce.worldBound.yMin + 0.75f)
                return false;
            if (commerce.worldBound.yMax > card.worldBound.yMax + 0.75f)
                return false;
            if (title.resolvedStyle.fontSize < 17.5f ||
                priceValue.resolvedStyle.fontSize < 18f)
                return false;
            if (title.worldBound.xMin < card.worldBound.xMin - 0.75f ||
                title.worldBound.xMax > card.worldBound.xMax + 0.75f)
                return false;
        }
        return true;
    }
#endif

    private void ApplyResponsiveLayout()
    {
        float width = _root.resolvedStyle.width;
        float height = _root.resolvedStyle.height;
        if (width <= 0f || height <= 0f) return;

        bool touch = SystemInfo.deviceType == DeviceType.Handheld || Input.touchSupported;
        float diagonal = UnityEngine.Screen.dpi > 0f
            ? Mathf.Sqrt(UnityEngine.Screen.width * UnityEngine.Screen.width +
                         UnityEngine.Screen.height * UnityEngine.Screen.height) /
              UnityEngine.Screen.dpi
            : 0f;
        _phone = _forcePhone ||
                 (SystemInfo.deviceType == DeviceType.Handheld &&
                  (diagonal <= 0f || diagonal < 8f));
        bool tablet = SystemInfo.deviceType == DeviceType.Handheld && !_phone;
        bool compact = width < 1500f || height < 820f || _phone;
        bool shortLayout = height < 760f || _phone;

        _root.EnableInClassList("input--touch", touch);
        _root.EnableInClassList("layout--compact", compact);
        _root.EnableInClassList("layout--short", shortLayout);
        _root.EnableInClassList("layout--phone", _phone);
        _root.EnableInClassList("layout--tablet", tablet);
        _contentScroll.mode = _phone
            ? ScrollViewMode.Horizontal
            : ScrollViewMode.Vertical;
        if (_phone && !_inspectorOpen)
            SetDisplayed(_inspectorScrim, false);

        Rect safe = UnityEngine.Screen.safeArea;
        float scaleX = width / Mathf.Max(1f, UnityEngine.Screen.width);
        float scaleY = height / Mathf.Max(1f, UnityEngine.Screen.height);
        _safeFrame.style.paddingLeft = safe.xMin * scaleX;
        _safeFrame.style.paddingRight = (UnityEngine.Screen.width - safe.xMax) * scaleX;
        _safeFrame.style.paddingTop = (UnityEngine.Screen.height - safe.yMax) * scaleY;
        _safeFrame.style.paddingBottom = safe.yMin * scaleY;

        SetDisplayed(_rotateDevice, touch && height > width);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape)
        {
            if (_inspectorOpen) _actions.CloseInspector?.Invoke();
            else if (!_hallOverview)
                BeginRoute(HallStation.Overview, () => _actions.OpenHallOverview?.Invoke());
            evt.StopPropagation();
            return;
        }

        HallStation station =
            evt.keyCode == KeyCode.Alpha1 ? HallStation.Market :
            evt.keyCode == KeyCode.Alpha2 ? HallStation.Warband :
            evt.keyCode == KeyCode.Alpha3 ? HallStation.Armory :
            evt.keyCode == KeyCode.Alpha4 ? HallStation.Hourstone :
            HallStation.Overview;
        if (station == HallStation.Overview) return;
        BeginRoute(station, () => _actions.OpenHallStation?.Invoke((int)station));
        evt.StopPropagation();
    }

    private void BeginRoute(HallStation destination, Action handoff)
    {
        if (_routePending || handoff == null) return;
        _routePending = true;

        // RunShell owns the standard Hall route feedback. Breach advances through a different
        // action, so it still needs its cue here.
        if (destination == HallStation.Breach)
        {
            string source = _hallOverview
                ? "station-hourstone"
                : "anchor-" + _activeStation.ToString().ToLowerInvariant();
            UiPolishSignals.Emit(UiPolishSignals.Cue.Route, source, "station-breach",
                tone: UiFeedbackTone.Sand, receipt: StationName(destination) + " opened.");
        }

        try
        {
            // Change state in the click frame. HubFlowDirector still supplies the destination
            // entrance motion, but input no longer sits behind an artificial 40–160 ms hold.
            handoff();
        }
        finally
        {
            _routePending = false;
        }
    }

    private void SetStationClass(HallStation station)
    {
        foreach (HallStation value in new[]
                 {
                     HallStation.Market, HallStation.Warband,
                     HallStation.Armory, HallStation.Hourstone,
                 })
            _root.EnableInClassList("hub--" + value.ToString().ToLowerInvariant(), station == value);
    }

    private static HallStationModel FindStation(IReadOnlyList<HallStationModel> stations,
                                                 HallStation target)
    {
        for (int i = 0; i < stations.Count; i++)
            if (stations[i].Station == target) return stations[i];
        return null;
    }

    private static string EmptyCopy(HallStation station) =>
        station == HallStation.Armory ? "The equipment rack is empty." :
        station == HallStation.Hourstone ? "No Inscriptions are bound yet." :
        station == HallStation.Market ? "The Market has no remaining stock." :
        "No champions are mustered here.";

    private static string BeatIcon(string kind) =>
        kind == "Boss" ? "♛" : kind == "Interlude" || kind == "Event" ? "◇" : "⚔";

    private static string StationPrefix(HallStation station) =>
        station.ToString().ToLowerInvariant();

    private static string StationName(HallStation station) =>
        station == HallStation.Warband ? "Warband" :
        station == HallStation.Hourstone ? "Hourstone" :
        station == HallStation.Armory ? "Armory" :
        station == HallStation.Breach ? "Breach" : "Market";

    private void RegisterPolishTarget(string id, VisualElement target, bool interactable = false)
    {
        _polish.RegisterTarget(id, target);
        if (interactable) _polish.AttachInteractable(target, () => id);
    }

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null) throw new InvalidOperationException($"[Management] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
