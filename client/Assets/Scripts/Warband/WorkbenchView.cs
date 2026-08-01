using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Bounded out-of-combat decision surface. Market, dossier, Armory drawer, and the shell-owned
/// unit rail form one remembered workspace; there are no document scroll regions or route tabs.
/// </summary>
internal sealed class WorkbenchView : IRunScreenView, IRunScreenLifecycle, IDisposable
{
    private const int ArmoryPageSize = 6;
    private const float EquipmentDragThreshold = 7f;
    private static VisualTreeAsset s_template;

    private sealed class MarketPool
    {
        private readonly VisualElement _host;
        private readonly RunShellActions _actions;
        private readonly UiFeedbackDirector _polish;
        private readonly List<MarketOfferCard> _cards = new List<MarketOfferCard>();
        private readonly List<string> _targets = new List<string>();

        public MarketPool(VisualElement host, RunShellActions actions,
                          UiFeedbackDirector polish)
        {
            _host = host;
            _actions = actions;
            _polish = polish;
        }

        public void Bind(IReadOnlyList<MarketOfferCardModel> models)
        {
            while (_cards.Count > models.Count)
            {
                int last = _cards.Count - 1;
                _polish.UnregisterTarget(_targets[last], _cards[last].Root);
                _cards[last].Root.RemoveFromHierarchy();
                _cards.RemoveAt(last);
                _targets.RemoveAt(last);
            }
            while (_cards.Count < models.Count)
            {
                var card = new MarketOfferCard(
                    key => _actions.SelectPlanningCard?.Invoke(key),
                    onActivated: key => _actions.ActivatePlanningCard?.Invoke(key));
                card.Root.AddToClassList("market-offer-card--workbench");
                _polish.AttachInteractable(card.Root,
                    () => card.Root.userData as string ?? "");
                _cards.Add(card);
                _targets.Add("");
                _host.Add(card.Root);
            }

            for (int i = 0; i < models.Count; i++)
            {
                string target = "card:" + models[i].Key;
                if (!string.Equals(_targets[i], target, StringComparison.Ordinal))
                {
                    _polish.UnregisterTarget(_targets[i], _cards[i].Root);
                    _polish.RegisterTarget(target, _cards[i].Root);
                    _targets[i] = target;
                }
                _cards[i].Bind(models[i]);
                _cards[i].Root.tooltip = "";
                Label economy = _cards[i].Root.Q<Label>("economy-state");
                if (economy != null) economy.text = "";
            }
        }
    }

    private sealed class ArmoryTile
    {
        private readonly RuntimeTooltipService _tooltips;
        private readonly Action<string> _select;
        private readonly Label _classification;
        private readonly Label _icon;
        private readonly Label _title;
        private readonly VisualElement _facts;
        private CardModel _model = new CardModel();
        private bool _suppressClick;

        public VisualElement Root { get; }
        public CardModel Model => _model;
        public string Icon => _icon.text;

        public ArmoryTile(
            Action<string> select,
            RuntimeTooltipService tooltips,
            Action<PointerDownEvent, ArmoryTile> beginDrag,
            Action<PointerMoveEvent, ArmoryTile> moveDrag,
            Action<PointerUpEvent, ArmoryTile> endDrag,
            Action<PointerCancelEvent, ArmoryTile> cancelDrag)
        {
            _select = select;
            _tooltips = tooltips;
            Root = new VisualElement();
            Root.AddToClassList("workbench-item");
            Root.focusable = true;
            Root.tabIndex = 0;

            _classification = new Label();
            _classification.AddToClassList("workbench-item__classification");
            _icon = new Label();
            _icon.AddToClassList("workbench-item__icon");
            _title = new Label();
            _title.AddToClassList("workbench-item__title");
            _facts = new VisualElement();
            _facts.AddToClassList("workbench-item__facts");
            Root.Add(_classification);
            Root.Add(_icon);
            Root.Add(_title);
            Root.Add(_facts);
            Root.RegisterCallback<PointerDownEvent>(evt => beginDrag?.Invoke(evt, this));
            Root.RegisterCallback<PointerMoveEvent>(evt => moveDrag?.Invoke(evt, this));
            Root.RegisterCallback<PointerUpEvent>(evt => endDrag?.Invoke(evt, this));
            Root.RegisterCallback<PointerCancelEvent>(evt => cancelDrag?.Invoke(evt, this));
            Root.RegisterCallback<ClickEvent>(evt =>
            {
                if (_suppressClick)
                {
                    _suppressClick = false;
                    evt.StopPropagation();
                    return;
                }
                Select();
            });
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                Select();
                evt.StopPropagation();
            });
            _tooltips.Attach(Root, Tooltip);
        }

        public void SuppressNextClick()
        {
            _suppressClick = true;
            Root.schedule.Execute(() => _suppressClick = false);
        }

        public void Bind(CardModel model)
        {
            _model = model ?? new CardModel();
            Root.userData = _model.Key;
            _classification.text = string.IsNullOrEmpty(_model.Eyebrow)
                ? _model.Subtitle
                : _model.Eyebrow;
            _icon.text = string.IsNullOrEmpty(_model.RoleIcon)
                ? _model.PortraitFallback
                : _model.RoleIcon;
            _title.text = _model.Title;
            Root.EnableInClassList("workbench-item--selected",
                _model.Selected || _model.Pinned);
            DecisionCardPresentation.ApplyAccent(Root, _model.Accent);

            _facts.Clear();
            for (int i = 0; i < Mathf.Min(3, _model.Stats.Count); i++)
            {
                var fact = new MechanicStatTile(
                    "workbench-item-fact", "workbench-item-fact");
                fact.Bind(_model.Stats[i]);
                fact.tooltip = "";
                _facts.Add(fact);
            }
        }

        private RuntimeTooltipModel Tooltip()
        {
            string rule = !string.IsNullOrEmpty(_model.InspectorAbilitySummary)
                ? _model.InspectorAbilitySummary
                : !string.IsNullOrEmpty(_model.AbilitySummary)
                    ? _model.AbilitySummary
                    : _model.WeaponSummary;
            RuntimeTooltipModel tooltip = RuntimeTooltipModel.Equipment(
                _model.Eyebrow, _model.Title, rule, _model.Stats,
                "STORED IN ARMORY · INACTIVE");
            tooltip.Footer =
                "Click to pin · Drag to a champion or matching socket";
            return tooltip;
        }

        private void Select()
        {
            if (string.IsNullOrEmpty(_model.Key)) return;
            UiPolishSignals.Emit(
                _model.Pinned ? UiPolishSignals.Cue.Unpin : UiPolishSignals.Cue.Pin,
                sourceId: _model.Key, targetId: "workbench-dossier",
                tone: UiFeedbackTone.Preview);
            _select?.Invoke(_model.Key);
        }
    }

    private readonly RunShellActions _actions;
    private readonly RuntimeTooltipService _tooltips;
    private readonly HubPresentationConfig _presentation;
    private readonly UiFxLayer _fxLayer;
    private readonly UiFeedbackDirector _polish;
    private readonly VisualElement _root;
    private readonly Label _title;
    private readonly Label _act;
    private readonly VisualElement _track;
    private readonly HourstoneAmount _hourstone;
    private readonly Button _continue;
    private readonly VisualElement _market;
    private readonly Button _reroll;
    private readonly HourstoneAmount _rerollCost;
    private readonly Label _rerollFree;
    private readonly VisualElement _musterGhost;
    private readonly VisualElement _armory;
    private readonly Label _armoryStatus;
    private readonly Label _armoryPage;
    private readonly Button _armoryPrev;
    private readonly Button _armoryNext;
    private readonly Button _armoryClose;
    private readonly VisualElement _armoryGrid;
    private readonly VisualElement _armoryEmpty;
    private readonly VisualElement _equipmentDragGhost;
    private readonly Label _equipmentDragIcon;
    private readonly Label _equipmentDragName;
    private string _trackSignature = "";
    private readonly VisualElement _choiceScrim;
    private readonly Label _choiceEyebrow;
    private readonly Label _choiceTitle;
    private readonly Label _choiceCopy;
    private readonly VisualElement _choiceOptions;
    private readonly VisualElement _rankupScrim;
    private readonly VisualElement _rankupModal;
    private readonly Label _rankupEyebrow;
    private readonly Label _rankupTitle;
    private readonly Label _rankupBump;
    private readonly VisualElement _rankupRow;
    private string _rankupSignature = "";
    private VisualElement _rankupCard;
    private Label _rankupAwaitPreview;
    private string _rankupAwaitIdle = "";
    private readonly List<IVisualElementScheduledItem> _rankupSchedules =
        new List<IVisualElementScheduledItem>();
    private IVisualElementScheduledItem _rankupPulse;
    private readonly InspectorPanel _inspector;
    private readonly MarketPool _marketCards;
    private readonly List<ArmoryTile> _armoryTiles = new List<ArmoryTile>();

    private IReadOnlyList<CardModel> _armoryModels = Array.Empty<CardModel>();
    private int _armoryPageIndex;
    private ArmoryTile _equipmentDragSource;
    private int _equipmentDragPointerId = -1;
    private Vector2 _equipmentDragStart;
    private bool _equipmentDragging;
    private bool _canDragEquipment;
    private bool _active;
    private bool _drawerStateInitialized;
    private bool _lastDrawerOpen;

    public RunScreen Screen => RunScreen.Management;
    public VisualElement Root => _root;

    public WorkbenchView(RunShellActions actions, RuntimeTooltipService tooltips,
                         UiFeedbackServices services = null)
    {
        _actions = actions;
        _tooltips = tooltips;
        if (s_template == null)
            s_template = Resources.Load<VisualTreeAsset>("UI/Workbench");
        if (s_template == null)
            throw new InvalidOperationException("[Workbench] Resources/UI/Workbench.uxml is required.");

        var host = new VisualElement();
        s_template.CloneTree(host);
        _root = Required<VisualElement>(host, "workbench");
        _root.RemoveFromHierarchy();
        _title = Required<Label>(_root, "title");
        _act = Required<Label>(_root, "act");
        _track = Required<VisualElement>(_root, "track");
        _continue = Required<Button>(_root, "continue");
        _market = Required<VisualElement>(_root, "market");
        _reroll = Required<Button>(_root, "reroll");
        Required<Label>(_root, "reroll-letters").text = "R\nE\nR\nO\nL\nL";
        _rerollCost = new HourstoneAmount(0, "workbench-reroll-rail__currency");
        Required<VisualElement>(_root, "reroll-cost").Add(_rerollCost);
        _rerollFree = Required<Label>(_root, "reroll-free");
        _armory = Required<VisualElement>(_root, "armory");
        _armory.userData = new WarbandEquipmentDropTarget { Armory = true };
        _armoryStatus = Required<Label>(_root, "armory-status");
        _armoryPage = Required<Label>(_root, "armory-page");
        _armoryPrev = Required<Button>(_root, "armory-prev");
        _armoryNext = Required<Button>(_root, "armory-next");
        _armoryClose = Required<Button>(_root, "armory-close");
        _armoryGrid = Required<VisualElement>(_root, "armory-grid");
        _armoryEmpty = Required<VisualElement>(_root, "armory-empty");
        _equipmentDragGhost = new VisualElement();
        _equipmentDragGhost.AddToClassList("workbench-equipment-drag-ghost");
        _equipmentDragGhost.pickingMode = PickingMode.Ignore;
        _equipmentDragIcon = new Label();
        _equipmentDragIcon.AddToClassList("workbench-equipment-drag-ghost__icon");
        _equipmentDragName = new Label();
        _equipmentDragName.AddToClassList("workbench-equipment-drag-ghost__name");
        _equipmentDragGhost.Add(_equipmentDragIcon);
        _equipmentDragGhost.Add(_equipmentDragName);
        _root.Add(_equipmentDragGhost);
        SetDisplayed(_equipmentDragGhost, false);
        _choiceScrim = Required<VisualElement>(_root, "choice-scrim");
        _choiceEyebrow = Required<Label>(_root, "choice-eyebrow");
        _choiceTitle = Required<Label>(_root, "choice-title");
        _choiceCopy = Required<Label>(_root, "choice-copy");
        _choiceOptions = Required<VisualElement>(_root, "choice-options");
        _rankupScrim = Required<VisualElement>(_root, "rankup-scrim");
        _rankupModal = Required<VisualElement>(_root, "rankup-modal");
        _rankupEyebrow = Required<Label>(_root, "rankup-eyebrow");
        _rankupTitle = Required<Label>(_root, "rankup-title");
        _rankupBump = Required<Label>(_root, "rankup-bump");
        _rankupRow = Required<VisualElement>(_root, "rankup-row");

        // The muster offer is five cards on a six-cell grid: the spare cell carries the
        // instruction instead of wasting the space (workbench-frame approval).
        _musterGhost = new VisualElement();
        _musterGhost.AddToClassList("workbench-muster-ghost");
        var ghostMark = new Label("✶");
        ghostMark.AddToClassList("workbench-muster-ghost__mark");
        var ghostCopy = new Label("CHOOSE THREE CHAMPIONS\nHOVER A STAT OR RULE\nFOR EXACT MECHANICS");
        ghostCopy.AddToClassList("workbench-muster-ghost__copy");
        _musterGhost.Add(ghostMark);
        _musterGhost.Add(ghostCopy);
        _musterGhost.style.display = DisplayStyle.None;

        _hourstone = new HourstoneAmount(0, "workbench-header__currency");
        Required<VisualElement>(_root, "hourstone-host").Add(_hourstone);
        _inspector = new InspectorPanel(
            id => _actions.InspectorAction?.Invoke(id),
            key => _actions.SelectComparisonTarget?.Invoke(key),
            _tooltips);
        _inspector.Root.AddToClassList("wb-inspector--workbench");
        Required<VisualElement>(_root, "inspector-slot").Add(_inspector.Root);

        _presentation = HubPresentationConfig.Load();
        _fxLayer = new UiFxLayer(_presentation);
        _root.Add(_fxLayer);
        _polish = new UiFeedbackDirector(
            _root, _presentation, _fxLayer, services?.Haptics, services?.Audio);
        _marketCards = new MarketPool(
            Required<VisualElement>(_root, "market-grid"), _actions, _polish);
        Required<VisualElement>(_root, "market-grid").Add(_musterGhost);
        RegisterTarget("ledger-sand", _hourstone);
        RegisterTarget("action-continue", _continue, true);
        RegisterTarget("action-reroll", _reroll, true);
        RegisterTarget("workbench-dossier", _inspector.Root);
        RegisterTarget("workbench-armory", _armory);
        RegisterTarget("workbench-market", _market);

        _continue.clicked += () => _actions.Advance?.Invoke();
        _reroll.clicked += () => _actions.Reroll?.Invoke();
        _armoryClose.clicked += () => _actions.CloseLoadout?.Invoke();
        _armoryPrev.clicked += () => ChangeArmoryPage(-1);
        _armoryNext.clicked += () => ChangeArmoryPage(1);
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    public void Bind(RunShellModel shell)
    {
        PlanningModel model = shell.Planning;
        bool drawerOpen = model.PartyShelf.Expanded;
        _canDragEquipment = drawerOpen && shell.WarbandBar != null &&
                            shell.WarbandBar.CanEdit;
        if (!_canDragEquipment && _equipmentDragPointerId >= 0)
            CancelEquipmentDrag();
        _root.EnableInClassList("workbench--armory-open", drawerOpen);
        _root.EnableInClassList("motion--reduced", model.ReducedMotion);
        _polish.SetReducedMotion(model.ReducedMotion);
        _tooltips.SetReducedMotion(model.ReducedMotion);

        _root.EnableInClassList("workbench--muster", model.MusterMode);
        _title.text = string.IsNullOrWhiteSpace(model.Title) ? "WORKBENCH" : model.Title;
        _act.text = model.Act;
        BindTrack(model.Track);
        _hourstone.Bind(int.TryParse(model.Sand, out int amount) ? amount : 0);
        // Hide the HOST, not just the amount — the wrapper carries the gold pill chrome.
        SetDisplayed(_hourstone.parent ?? _hourstone, !model.MusterMode);
        _continue.text = string.IsNullOrWhiteSpace(model.CommitLabel)
            ? "TO THE BREACH  →"
            : model.CommitLabel + "  →";
        _continue.SetEnabled(model.CanCommit);

        _marketCards.Bind(model.MarketOffers);
        SetDisplayed(_musterGhost, model.MusterMode);
        if (model.MusterMode) _musterGhost.BringToFront();
        // The rail spends horizontal space, never vertical: cost rides under the stacked
        // letters, and the button never carries generated text over its children.
        _rerollCost.Bind(model.RerollCost >= 0 ? model.RerollCost : 0);
        SetDisplayed(_rerollCost, model.RerollCost >= 0);
        SetDisplayed(_rerollFree, model.MusterMode);
        _reroll.SetEnabled(model.CanReroll);
        _reroll.tooltip = model.RerollCost >= 0
            ? $"Reroll every unsold offer · {model.RerollCost} Sand"
            : model.RerollLabel;

        // The Market never yields to the drawer: the armory opens as a footer band under the
        // dossier, so browsing and equipping are one workspace (Design/workbench-dossier.md).
        // Opening lives on the warband bar's ARMORY chip — the drawer sits directly above it.
        SetDisplayed(_armory, drawerOpen);
        if (_drawerStateInitialized && drawerOpen != _lastDrawerOpen)
        {
            if (drawerOpen)
                UiPolishSignals.Emit(UiPolishSignals.Cue.DrawerExpand,
                    sourceId: "workbench-market",
                    targetId: "workbench-armory", tone: UiFeedbackTone.Preview);
            else
                UiPolishSignals.Emit(UiPolishSignals.Cue.DrawerCollapse,
                    sourceId: "workbench-armory",
                    targetId: "workbench-market", tone: UiFeedbackTone.Preview);
        }
        _drawerStateInitialized = true;
        _lastDrawerOpen = drawerOpen;

        InspectorModel dossier = drawerOpen
            ? model.PartyShelf.LoadoutInspector
            : model.Inspector;
        // The header brief moved here: instructional copy belongs to the empty dossier,
        // not to a permanent 42px band (workbench-refactor approval).
        _inspector.Bind(dossier ?? new InspectorModel
        {
            Empty = true,
            EmptyHint = model.Brief,
        });

        _armoryModels = model.PartyShelf.LoadoutInventory;
        _armoryPageIndex = Mathf.Clamp(
            _armoryPageIndex, 0, Mathf.Max(0, ArmoryPageCount() - 1));
        BindArmory();
        BindBlockingChoice(model);
        BindRankUpModal(model);
    }

    public void OnScreenEntered()
    {
        _active = true;
        _polish.SetActive(true);
    }

    public void OnScreenExited()
    {
        _active = false;
        CancelEquipmentDrag();
        _tooltips.Hide();
        _polish.SetActive(false);
        // Screen exit is a cancellation boundary: no rank-up choreography survives into the
        // next view. A still-pending choice replays its entrance on return — intended.
        if (_rankupSignature.Length > 0) HideRankUpModal();
    }

    /// <summary>The approval condition: the act's beat track lives in the header. Pips are
    /// rebuilt only when the run actually advances.</summary>
    private void BindTrack(IReadOnlyList<PlanningTrackNodeModel> track)
    {
        var signature = new System.Text.StringBuilder();
        foreach (PlanningTrackNodeModel node in track)
            signature.Append(node.Label).Append(':').Append(node.Kind)
                .Append(':').Append(node.State).Append('|');
        string value = signature.ToString();
        if (string.Equals(value, _trackSignature, StringComparison.Ordinal)) return;
        _trackSignature = value;
        _track.Clear();
        foreach (PlanningTrackNodeModel node in track)
        {
            var pip = new Label(
                node.Kind == "Boss" ? "BOSS" :
                node.Kind == "Interlude" ? "◆" : node.Label);
            pip.AddToClassList("wb-track-node");
            pip.AddToClassList("wb-track-node--" + node.State.ToLowerInvariant());
            pip.AddToClassList("wb-track-node--" + node.Kind.ToLowerInvariant());
            pip.tooltip = node.Kind == "Boss"
                ? $"Act boss · {node.State}"
                : $"Beat {node.Label} · {node.Kind} · {node.State}";
            _track.Add(pip);
        }
    }

    private void BindArmory()
    {
        int count = _armoryModels?.Count ?? 0;
        int first = _armoryPageIndex * ArmoryPageSize;
        int shown = Mathf.Min(ArmoryPageSize, Mathf.Max(0, count - first));
        while (_armoryTiles.Count < shown)
        {
            var tile = new ArmoryTile(
                key => _actions.SelectLoadoutItem?.Invoke(key),
                _tooltips,
                BeginEquipmentDrag,
                MoveEquipmentDrag,
                EndEquipmentDrag,
                CancelEquipmentDrag);
            _armoryTiles.Add(tile);
            _armoryGrid.Add(tile.Root);
            _polish.AttachInteractable(tile.Root,
                () => tile.Root.userData as string ?? "");
        }
        for (int i = 0; i < _armoryTiles.Count; i++)
        {
            bool visible = i < shown;
            SetDisplayed(_armoryTiles[i].Root, visible);
            if (visible) _armoryTiles[i].Bind(_armoryModels[first + i]);
        }

        int pages = ArmoryPageCount();
        _armoryStatus.text = count == 1 ? "1 STORED ITEM" : $"{count} STORED ITEMS";
        _armoryPage.text = pages <= 1 ? "" : $"{_armoryPageIndex + 1} / {pages}";
        _armoryPrev.SetEnabled(_armoryPageIndex > 0);
        _armoryNext.SetEnabled(_armoryPageIndex + 1 < pages);
        SetDisplayed(_armoryPrev, pages > 1);
        SetDisplayed(_armoryNext, pages > 1);
        SetDisplayed(_armoryPage, pages > 1);
        SetDisplayed(_armoryGrid, count > 0);
        SetDisplayed(_armoryEmpty, count == 0);
    }

    private void BeginEquipmentDrag(PointerDownEvent evt, ArmoryTile tile)
    {
        if (evt.button != 0 || _equipmentDragPointerId >= 0 ||
            !_canDragEquipment || tile?.Model == null ||
            tile.Model.ItemInstanceId <= 0 ||
            (tile.Model.EquipmentKind != 0 && tile.Model.EquipmentKind != 1))
            return;

        _equipmentDragSource = tile;
        _equipmentDragPointerId = evt.pointerId;
        _equipmentDragStart = new Vector2(evt.position.x, evt.position.y);
        _equipmentDragging = false;
        _equipmentDragIcon.text = tile.Icon;
        _equipmentDragName.text = tile.Model.Title;
        tile.Root.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void MoveEquipmentDrag(PointerMoveEvent evt, ArmoryTile tile)
    {
        if (evt.pointerId != _equipmentDragPointerId ||
            !ReferenceEquals(tile, _equipmentDragSource))
            return;

        Vector2 position = new Vector2(evt.position.x, evt.position.y);
        if (!_equipmentDragging &&
            Vector2.Distance(position, _equipmentDragStart) >= EquipmentDragThreshold)
        {
            _equipmentDragging = true;
            _tooltips.Hide();
            tile.Root.EnableInClassList("workbench-item--drag-source", true);
            SetEquipmentDropHighlights(true, tile.Model.EquipmentKind);
            _equipmentDragGhost.BringToFront();
        }
        if (_equipmentDragging)
        {
            _equipmentDragGhost.style.left = position.x + 14f;
            _equipmentDragGhost.style.top = position.y + 14f;
            SetDisplayed(_equipmentDragGhost, true);
        }
        evt.StopPropagation();
    }

    private void EndEquipmentDrag(PointerUpEvent evt, ArmoryTile tile)
    {
        if (evt.pointerId != _equipmentDragPointerId ||
            !ReferenceEquals(tile, _equipmentDragSource))
            return;

        Vector2 position = new Vector2(evt.position.x, evt.position.y);
        bool dragged = _equipmentDragging;
        long itemInstanceId = tile.Model.ItemInstanceId;
        int kind = tile.Model.EquipmentKind;
        WarbandEquipmentDropTarget target = dragged
            ? PickEquipmentTarget(position, kind)
            : null;
        if (dragged) tile.SuppressNextClick();
        CancelEquipmentDrag();

        if (target != null && target.HeroInstanceId > 0)
        {
            UiPolishSignals.Emit(UiPolishSignals.Cue.ProjectedTarget,
                sourceId: "workbench-armory", targetId: "warband-roster",
                tone: UiFeedbackTone.Positive,
                transaction: UiTransactionKind.Equip);
            _actions.EquipWarbandItem?.Invoke(
                itemInstanceId, target.HeroInstanceId);
        }
        evt.StopPropagation();
    }

    private void CancelEquipmentDrag(PointerCancelEvent evt, ArmoryTile tile)
    {
        if (evt.pointerId != _equipmentDragPointerId ||
            !ReferenceEquals(tile, _equipmentDragSource))
            return;
        if (_equipmentDragging) tile.SuppressNextClick();
        CancelEquipmentDrag();
        evt.StopPropagation();
    }

    private void CancelEquipmentDrag()
    {
        if (_equipmentDragSource != null &&
            _equipmentDragPointerId >= 0 &&
            _equipmentDragSource.Root.HasPointerCapture(_equipmentDragPointerId))
            _equipmentDragSource.Root.ReleasePointer(_equipmentDragPointerId);
        _equipmentDragSource?.Root.EnableInClassList(
            "workbench-item--drag-source", false);
        _equipmentDragSource = null;
        _equipmentDragPointerId = -1;
        _equipmentDragging = false;
        SetDisplayed(_equipmentDragGhost, false);
        SetEquipmentDropHighlights(false, -1);
    }

    private WarbandEquipmentDropTarget PickEquipmentTarget(
        Vector2 panelPosition, int kind)
    {
        VisualElement picked = _root.panel?.Pick(panelPosition);
        while (picked != null)
        {
            if (picked.userData is WarbandEquipmentDropTarget socket &&
                !socket.Armory && socket.Kind == kind &&
                socket.HeroInstanceId > 0)
                return socket;
            if (picked.userData is WarbandRosterDropTarget hero &&
                hero.HeroInstanceId > 0 && !hero.Locked)
                return new WarbandEquipmentDropTarget
                {
                    HeroInstanceId = hero.HeroInstanceId,
                    Kind = kind,
                };
            picked = picked.parent;
        }
        return null;
    }

    private void SetEquipmentDropHighlights(bool enabled, int kind)
    {
        VisualElement panelRoot = _root;
        while (panelRoot.parent != null) panelRoot = panelRoot.parent;
        panelRoot.Query<VisualElement>(className: "warband-gear").ForEach(element =>
        {
            bool legal = enabled &&
                         element.userData is WarbandEquipmentDropTarget target &&
                         !target.Armory &&
                         target.HeroInstanceId > 0 &&
                         target.Kind == kind;
            element.EnableInClassList("warband-gear--drop-target", legal);
        });
        panelRoot.Query<VisualElement>(className: "warband-hero").ForEach(element =>
        {
            bool legal = enabled &&
                         element.userData is WarbandRosterDropTarget target &&
                         !target.Locked &&
                         target.HeroInstanceId > 0;
            element.EnableInClassList("warband-hero--gear-drop-target", legal);
        });
    }

    private void ChangeArmoryPage(int delta)
    {
        CancelEquipmentDrag();
        int next = Mathf.Clamp(_armoryPageIndex + delta, 0,
            Mathf.Max(0, ArmoryPageCount() - 1));
        if (next == _armoryPageIndex) return;
        _armoryPageIndex = next;
        _tooltips.Hide();
        BindArmory();
        UiPolishSignals.Emit(UiPolishSignals.Cue.Tab,
            targetId: "workbench-armory", tone: UiFeedbackTone.Preview);
    }

    private int ArmoryPageCount()
    {
        int count = _armoryModels?.Count ?? 0;
        return Mathf.Max(1, Mathf.CeilToInt(count / (float)ArmoryPageSize));
    }

    private void BindBlockingChoice(PlanningModel model)
    {
        // The rank-up choice no longer renders here — it owns the dedicated modal
        // (workbench-frame approval). This scrim carries the beat choices.
        bool reward = model.BeatKind == PlanningBeat.RevisionUpgrade ||
                      model.BeatKind == PlanningBeat.Interlude ||
                      model.BeatKind == PlanningBeat.BossReward ||
                      model.BeatKind == PlanningBeat.EndlessChoice ||
                      model.BeatKind == PlanningBeat.StartingRevision;
        SetDisplayed(_choiceScrim, reward);
        _choiceScrim.EnableInClassList(
            "workbench-choice-scrim--endless",
            model.BeatKind == PlanningBeat.EndlessChoice);
        _choiceOptions.Clear();
        if (!reward) return;

        _choiceEyebrow.text = model.BeatKind switch
        {
            PlanningBeat.BossReward => "BOSS REWARD",
            PlanningBeat.RevisionUpgrade => "REVISION EVOLUTION",
            PlanningBeat.EndlessChoice => "VICTORY BANKED · THE WANING CROWN HAS FALLEN",
            PlanningBeat.StartingRevision => "FIRST REVISION",
            _ => "INTERLUDE",
        };
        _choiceTitle.text = model.BeatKind switch
        {
            PlanningBeat.BossReward => "Bind one Inscription",
            PlanningBeat.RevisionUpgrade => "Choose how your Revision changes",
            PlanningBeat.EndlessChoice => "The Hour held. What happens next?",
            PlanningBeat.StartingRevision => "Bind one way to alter a battle",
            _ => "Choose what this quiet Hour leaves behind",
        };
        MechanicPresentation.BindInline(_choiceCopy, model.Brief);
        foreach (InterludeChoiceModel choice in model.Interlude)
        {
            int path = choice.Path;
            int option = choice.Option;
            _choiceOptions.Add(Choice(
                choice.Card.Eyebrow, choice.Card.Title, choice.Card.AbilitySummary,
                () =>
                {
                    if (model.BeatKind == PlanningBeat.BossReward)
                        _actions.ChooseBossReward?.Invoke(option);
                    else if (model.BeatKind == PlanningBeat.RevisionUpgrade)
                        _actions.ChooseRevisionUpgrade?.Invoke(option);
                    else if (model.BeatKind == PlanningBeat.StartingRevision)
                        _actions.ChooseStartingRevision?.Invoke(choice.Card.ContentId);
                    else if (model.BeatKind == PlanningBeat.EndlessChoice)
                        _actions.ChooseEndless?.Invoke(option == 1);
                    else
                        _actions.ChooseInterlude?.Invoke(path, option);
                },
                facts: choice.Facts,
                actionLabel: choice.ActionLabel,
                accent: choice.Card.Accent));
        }
    }

    /// <summary>The rank-up modal (workbench-frame approval): the hero's progression card
    /// center-stage between the option panels; binding fills its awaiting path slot. The
    /// entrance is choreographed — this is the run's payoff moment, not a form.</summary>
    private void BindRankUpModal(PlanningModel model)
    {
        SpecChoiceModel spec = model.SpecChoice;
        if (!spec.Pending)
        {
            if (_rankupSignature.Length > 0) HideRankUpModal();
            return;
        }

        var signature = new System.Text.StringBuilder();
        signature.Append(spec.HeroName).Append('|').Append(spec.RankLabel).Append('|');
        foreach (SpecOptionModel option in spec.Options)
            signature.Append(option.Name).Append('|');
        signature.Append(model.ReducedMotion ? "rm" : "fm");
        string value = signature.ToString();
        if (string.Equals(value, _rankupSignature, StringComparison.Ordinal)) return;
        bool entering = _rankupSignature.Length == 0;
        _rankupSignature = value;

        SetDisplayed(_rankupScrim, true);
        _rankupEyebrow.text = spec.Fork ? "RANK UP · THE FORK" : "RANK UP";
        _rankupTitle.text = string.IsNullOrEmpty(spec.FromRank)
            ? spec.HeroName
            : $"{spec.HeroName} · {spec.FromRank} → {spec.ToRank}";
        _rankupBump.text = spec.BumpText;
        SetDisplayed(_rankupBump, !string.IsNullOrEmpty(spec.BumpText));

        CancelRankUpSchedules();
        _rankupRow.Clear();
        _rankupAwaitPreview = null;
        _rankupCard = RankUpHeroCard(spec);
        // Never assume two options (model contract): the card takes the middle seat and
        // however many options the run layer drew flank it in one row.
        int cardIndex = spec.Options.Count / 2;
        for (int i = 0; i <= spec.Options.Count; i++)
        {
            if (i == cardIndex)
            {
                _rankupRow.Add(_rankupCard);
                continue;
            }
            int optionIndex = i < cardIndex ? i : i - 1;
            _rankupRow.Add(RankUpOption(
                spec.Options[optionIndex], optionIndex, fromLeft: i < cardIndex));
        }

        if (entering) PlayRankUpEntrance(model.ReducedMotion);
        else SettleRankUpInstantly();
    }

    private VisualElement RankUpHeroCard(SpecChoiceModel spec)
    {
        var card = new VisualElement();
        card.AddToClassList("workbench-rankup-card");
        DecisionCardPresentation.ApplyAccent(card, spec.Accent);

        var portrait = new VisualElement();
        portrait.AddToClassList("workbench-rankup-card__portrait");
        Texture2D texture = string.IsNullOrEmpty(spec.PortraitResource)
            ? null
            : Resources.Load<Texture2D>(spec.PortraitResource);
        if (texture != null)
            portrait.style.backgroundImage =
                new StyleBackground(Background.FromTexture2D(texture));
        else
        {
            var fallback = new Label(spec.PortraitFallback);
            fallback.AddToClassList("workbench-rankup-card__fallback");
            portrait.Add(fallback);
        }
        var ranks = new VisualElement();
        ranks.AddToClassList("workbench-rankup-card__ranks");
        var from = new Label(spec.FromRank);
        from.AddToClassList("workbench-rankup-card__rank-from");
        var arrow = new Label("→");
        arrow.AddToClassList("workbench-rankup-card__rank-arrow");
        var to = new Label(spec.ToRank);
        to.AddToClassList("workbench-rankup-card__rank-to");
        ranks.Add(from);
        ranks.Add(arrow);
        ranks.Add(to);
        portrait.Add(ranks);
        card.Add(portrait);

        var name = new Label(spec.HeroName.ToUpperInvariant());
        name.AddToClassList("workbench-rankup-card__name");
        card.Add(name);

        var gear = new VisualElement();
        gear.AddToClassList("workbench-rankup-card__row");
        gear.Add(RankUpGearSlot(
            string.IsNullOrEmpty(spec.SignatureIcon) ? "✦" : spec.SignatureIcon,
            filled: true, tag: ""));
        gear.Add(RankUpGearSlot("⚔", spec.WeaponFilled, "W"));
        gear.Add(RankUpGearSlot("◆", spec.TrinketFilled, "T"));
        card.Add(gear);

        var path = new VisualElement();
        path.AddToClassList("workbench-rankup-card__row");
        foreach (RankTierSlotModel tier in spec.PathTiers)
        {
            var slot = new VisualElement();
            slot.AddToClassList("workbench-rankup-slot");
            slot.AddToClassList("workbench-rankup-slot--perk");
            var glyph = new Label();
            glyph.AddToClassList("workbench-rankup-slot__glyph");
            slot.Add(glyph);
            switch (tier.State)
            {
                case RankTierSlotState.Selected:
                    slot.AddToClassList("workbench-rankup-slot--filled");
                    glyph.text = tier.Icon;
                    break;
                case RankTierSlotState.Pending:
                    slot.AddToClassList("workbench-rankup-slot--await");
                    glyph.text = tier.Rank;
                    _rankupAwaitPreview = glyph;
                    _rankupAwaitIdle = tier.Rank;
                    break;
                default:
                    slot.AddToClassList("workbench-rankup-slot--empty");
                    glyph.text = tier.Rank;
                    break;
            }
            path.Add(slot);
        }
        card.Add(path);

        var foot = new Label("THE CARD REMEMBERS");
        foot.AddToClassList("workbench-rankup-card__foot");
        card.Add(foot);
        return card;
    }

    private static VisualElement RankUpGearSlot(string glyphText, bool filled, string tag)
    {
        var slot = new VisualElement();
        slot.AddToClassList("workbench-rankup-slot");
        slot.AddToClassList(filled
            ? "workbench-rankup-slot--filled"
            : "workbench-rankup-slot--empty");
        var glyph = new Label(filled ? glyphText : "◇");
        glyph.AddToClassList("workbench-rankup-slot__glyph");
        slot.Add(glyph);
        if (!string.IsNullOrEmpty(tag))
        {
            var corner = new Label(tag);
            corner.AddToClassList("workbench-rankup-slot__tag");
            slot.Add(corner);
        }
        return slot;
    }

    private VisualElement RankUpOption(SpecOptionModel option, int index, bool fromLeft)
    {
        var panel = new Button(() => _actions.ChooseSpec?.Invoke(index));
        panel.AddToClassList("workbench-rankup-option");
        panel.AddToClassList(fromLeft
            ? "workbench-rankup-option--from-left"
            : "workbench-rankup-option--from-right");

        var head = new VisualElement();
        head.AddToClassList("workbench-rankup-option__head");
        var icon = new Label(option.Icon);
        icon.AddToClassList("workbench-rankup-option__icon");
        var id = new VisualElement();
        id.AddToClassList("workbench-rankup-option__id");
        var kind = new Label(option.Change);
        kind.AddToClassList("workbench-rankup-option__kind");
        var name = new Label(option.Name);
        name.AddToClassList("workbench-rankup-option__name");
        id.Add(kind);
        id.Add(name);
        head.Add(icon);
        head.Add(id);
        panel.Add(head);

        var rule = new Label();
        rule.AddToClassList("workbench-rankup-option__rule");
        MechanicPresentation.BindInline(rule, option.Text);
        panel.Add(rule);
        for (int i = 0; i < Mathf.Min(3, option.Comparisons.Count); i++)
        {
            var delta = new Label(
                $"{option.Comparisons[i].Label}  " +
                $"{option.Comparisons[i].Before} → {option.Comparisons[i].After}");
            delta.AddToClassList("workbench-rankup-option__delta");
            panel.Add(delta);
        }
        var bind = new Label("BIND " + option.Name.ToUpperInvariant());
        bind.AddToClassList("workbench-rankup-option__bind");
        panel.Add(bind);

        // Hovering an option previews its mark inside the card's awaiting path slot —
        // the card visibly remembers before the player commits.
        panel.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (_rankupAwaitPreview != null) _rankupAwaitPreview.text = option.Icon;
        });
        panel.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (_rankupAwaitPreview != null) _rankupAwaitPreview.text = _rankupAwaitIdle;
        });
        _polish.AttachInteractable(panel, () => "rankup:" + option.Name);
        return panel;
    }

    /// <summary>The dopamine shot (Jake's approval condition): scrim up, the card overshoots
    /// into place, options slide in from the sides, the awaiting slot pulses until bound.
    /// Reduced motion: one short fade, slot statically gold.</summary>
    private void PlayRankUpEntrance(bool reducedMotion)
    {
        UiPolishSignals.Emit(UiPolishSignals.Cue.RankUp,
            sourceId: "workbench-market", targetId: "workbench-rankup",
            tone: UiFeedbackTone.Positive);
        if (reducedMotion)
        {
            _rankupScrim.AddToClassList("workbench-rankup-scrim--reduced");
            SettleRankUpInstantly(live: false);
            ScheduleRankUp(() =>
                _rankupScrim.AddToClassList("workbench-rankup-scrim--live"), 16);
            return;
        }
        _rankupScrim.RemoveFromClassList("workbench-rankup-scrim--reduced");
        _rankupScrim.RemoveFromClassList("workbench-rankup-scrim--live");
        _rankupCard.AddToClassList("workbench-rankup-card--pre");
        ScheduleRankUp(() =>
            _rankupScrim.AddToClassList("workbench-rankup-scrim--live"), 16);
        ScheduleRankUp(() =>
        {
            _rankupCard.RemoveFromClassList("workbench-rankup-card--pre");
            _rankupCard.AddToClassList("workbench-rankup-card--pop");
        }, 90);
        ScheduleRankUp(() =>
            _rankupCard.RemoveFromClassList("workbench-rankup-card--pop"), 440);
        ScheduleRankUp(() =>
        {
            foreach (VisualElement child in _rankupRow.Children())
                child.AddToClassList("workbench-rankup-option--in");
        }, 190);
        _rankupPulse = _rankupRow.schedule.Execute(() =>
        {
            VisualElement slot = _rankupAwaitPreview?.parent;
            slot?.ToggleInClassList("workbench-rankup-slot--pulse");
        }).Every(700);
    }

    private void SettleRankUpInstantly(bool live = true)
    {
        _rankupCard?.RemoveFromClassList("workbench-rankup-card--pre");
        _rankupCard?.RemoveFromClassList("workbench-rankup-card--pop");
        foreach (VisualElement child in _rankupRow.Children())
            child.AddToClassList("workbench-rankup-option--in");
        if (live) _rankupScrim.AddToClassList("workbench-rankup-scrim--live");
    }

    private void HideRankUpModal()
    {
        CancelRankUpSchedules();
        _rankupSignature = "";
        _rankupAwaitPreview = null;
        _rankupCard = null;
        _rankupScrim.RemoveFromClassList("workbench-rankup-scrim--live");
        _rankupScrim.RemoveFromClassList("workbench-rankup-scrim--reduced");
        SetDisplayed(_rankupScrim, false);
    }

    private void ScheduleRankUp(Action action, long delayMs)
    {
        IVisualElementScheduledItem item = _rankupRow.schedule.Execute(action);
        item.ExecuteLater(delayMs);
        _rankupSchedules.Add(item);
    }

    private void CancelRankUpSchedules()
    {
        foreach (IVisualElementScheduledItem item in _rankupSchedules) item.Pause();
        _rankupSchedules.Clear();
        _rankupPulse?.Pause();
        _rankupPulse = null;
    }

    private VisualElement Choice(string eyebrow, string title, string copy, Action action,
                                 IReadOnlyList<StatComparisonModel> comparisons = null,
                                 IReadOnlyList<string> facts = null,
                                 string actionLabel = "", string accent = "")
    {
        var button = new Button(action);
        button.AddToClassList("workbench-choice-card");
        if (!string.IsNullOrWhiteSpace(accent))
            button.AddToClassList("workbench-choice-card--" + accent);
        var tag = new Label(eyebrow);
        tag.AddToClassList("workbench-choice-card__eyebrow");
        var name = new Label(title);
        name.AddToClassList("workbench-choice-card__title");
        var rule = new Label();
        rule.AddToClassList("workbench-choice-card__copy");
        MechanicPresentation.BindInline(rule, copy);
        button.Add(tag);
        button.Add(name);
        button.Add(rule);
        if (comparisons != null)
            for (int i = 0; i < Mathf.Min(3, comparisons.Count); i++)
            {
                var delta = new Label(
                    $"{comparisons[i].Label}  {comparisons[i].Before} → {comparisons[i].After}");
                delta.AddToClassList("workbench-choice-card__delta");
                button.Add(delta);
            }
        if (facts != null && facts.Count > 0)
        {
            var factRow = new VisualElement();
            factRow.AddToClassList("workbench-choice-card__facts");
            for (int i = 0; i < facts.Count; i++)
            {
                var fact = new Label(facts[i]);
                fact.AddToClassList("workbench-choice-card__fact");
                factRow.Add(fact);
            }
            button.Add(factRow);
        }
        if (!string.IsNullOrWhiteSpace(actionLabel))
        {
            var footer = new Label(actionLabel);
            footer.AddToClassList("workbench-choice-card__action");
            button.Add(footer);
        }
        _polish.AttachInteractable(button, () => "choice:" + title);
        return button;
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (!_active || evt.keyCode != KeyCode.Escape) return;
        if (_root.ClassListContains("workbench--armory-open"))
        {
            _actions.CloseLoadout?.Invoke();
            evt.StopPropagation();
        }
    }

    private void RegisterTarget(string id, VisualElement target, bool interactable = false)
    {
        _polish.RegisterTarget(id, target);
        if (interactable) _polish.AttachInteractable(target, () => id);
    }

    public void Dispose()
    {
        CancelEquipmentDrag();
        CancelRankUpSchedules();
        _tooltips.Hide();
        _polish.Dispose();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string EditorEquipmentDragReport()
    {
        int stableItems = 0;
        foreach (ArmoryTile tile in _armoryTiles)
            if (tile.Root.resolvedStyle.display != DisplayStyle.None &&
                tile.Model.ItemInstanceId > 0 &&
                (tile.Model.EquipmentKind == 0 || tile.Model.EquipmentKind == 1))
                stableItems++;

        int heroTargets = 0;
        int weaponTargets = 0;
        int trinketTargets = 0;
        VisualElement panelRoot = _root;
        while (panelRoot.parent != null) panelRoot = panelRoot.parent;
        panelRoot.Query<VisualElement>(className: "warband-hero").ForEach(element =>
        {
            if (element.userData is WarbandRosterDropTarget target &&
                target.HeroInstanceId > 0 && !target.Locked)
                heroTargets++;
        });
        panelRoot.Query<VisualElement>(className: "warband-gear").ForEach(element =>
        {
            if (!(element.userData is WarbandEquipmentDropTarget target) ||
                target.HeroInstanceId <= 0)
                return;
            if (target.Kind == 0) weaponTargets++;
            else if (target.Kind == 1) trinketTargets++;
        });
        bool armoryTarget =
            _armory.userData is WarbandEquipmentDropTarget armory &&
            armory.Armory;
        bool passed = stableItems > 0 && heroTargets > 0 &&
                      weaponTargets > 0 && trinketTargets > 0 &&
                      armoryTarget;
        return $"Equipment drag: {(passed ? "PASS" : "FAIL")} · " +
               $"{stableItems} stable Armory items · {heroTargets} champion targets · " +
               $"{weaponTargets} weapon sockets · {trinketTargets} trinket sockets · " +
               $"Armory return target {(armoryTarget ? "ready" : "missing")}";
    }

    public bool EditorValidateResolvedLayout()
    {
        UiLayoutReport report = EditorLayoutReport();
        if (report.Passed) return true;
        Debug.LogError("[Workbench Layout] " + report);
        return false;
    }

    public string EditorResolvedLayoutReport() => EditorLayoutReport().ToString();

    public void EditorSetChoiceCardMinHeight(float pixels)
    {
        for (int i = 0; i < _choiceOptions.childCount; i++)
        {
            VisualElement card = _choiceOptions[i];
            card.style.minHeight = pixels >= 0f
                ? new StyleLength(pixels)
                : new StyleLength(StyleKeyword.Null);
        }
    }

    public string EditorSemanticLayoutSnapshot()
    {
        VisualElement marketGrid = _root.Q<VisualElement>("market-grid");
        VisualElement rankUpBody = _inspector.Root.Q<VisualElement>("rank-up-body");
        VisualElement ladder = _inspector.Root.Q<VisualElement>("rank-up-ladder");
        VisualElement options = _inspector.Root.Q<VisualElement>("rank-up-options");
        VisualElement deferred = _inspector.Root.Q<VisualElement>("deferred");
        List<VisualElement> inlineTraits =
            _inspector.Root.Query<VisualElement>(
                className: "wb-trait-chip--inline").ToList();
        List<VisualElement> semanticTokens =
            _inspector.Root.Query<VisualElement>(
                className: "semantic-text--interactive").ToList();
        List<VisualElement> detachedKeywords =
            _inspector.Root.Query<VisualElement>(
                className: "wb-keyword-chip").ToList();
        List<VisualElement> art =
            _root.Query<VisualElement>(className: "market-offer-card__art").ToList();
        float minArt = float.PositiveInfinity;
        for (int i = 0; i < art.Count; i++)
            if (art[i].resolvedStyle.display != DisplayStyle.None)
                minArt = Mathf.Min(minArt, art[i].resolvedStyle.height);
        if (float.IsPositiveInfinity(minArt)) minArt = 0f;
        float scale = _root.panel?.scaledPixelsPerPoint ?? 1f;
        return
            $"viewport={_root.resolvedStyle.width:0.#}x{_root.resolvedStyle.height:0.#};" +
            $"scale={scale:0.###};classes={string.Join(",", _root.GetClasses())};" +
            $"market={Bounds(marketGrid)};minArt={minArt:0.#};" +
            $"rankUp={Visible(rankUpBody)};deferred={VisibleChildren(deferred)};" +
            $"tiers={VisibleChildren(ladder)};options={VisibleChildren(options)};" +
            $"inlineTraits={inlineTraits.Count};semanticText={semanticTokens.Count};" +
            $"detachedKeywords={detachedKeywords.Count}";
    }

    private UiLayoutReport EditorLayoutReport()
    {
        var report = new UiLayoutReport("Workbench");
        VisualElement safe = Required<VisualElement>(_root, "safe-frame");
        VisualElement header = Required<VisualElement>(_root, "header");
        VisualElement body = Required<VisualElement>(_root, "body");
        VisualElement marketGrid = Required<VisualElement>(_root, "market-grid");
        VisualElement dossier = Required<VisualElement>(_root, "dossier-frame");
        VisualElement inspectorContent =
            _inspector.Root.Q<VisualElement>("content");
        VisualElement inspectorTraitTab =
            _inspector.Root.Q<VisualElement>("page-traits");
        VisualElement inspectorBody =
            _inspector.Root.Q<VisualElement>("decision-body");
        VisualElement inspectorActions =
            _inspector.Root.Q<VisualElement>("actions");
        VisualElement inspectorTags =
            _inspector.Root.Q<VisualElement>("tags");
        VisualElement inspectorDeferred =
            _inspector.Root.Q<VisualElement>("deferred");
        VisualElement rankUpBody =
            _inspector.Root.Q<VisualElement>("rank-up-body");
        VisualElement rankUpLadder =
            _inspector.Root.Q<VisualElement>("rank-up-ladder");
        VisualElement rankUpOptions =
            _inspector.Root.Q<VisualElement>("rank-up-options");
        Label inspectorSubtitle = _inspector.Root.Q<Label>("subtitle");
        VisualElement inspectorStats = _inspector.Root.Q<VisualElement>("stats");
        UiLayoutContract.RequireResolved(report, _root, "root");
        UiLayoutContract.RequireResolved(report, safe, "safe frame");
        UiLayoutContract.RequireAbove(report, header, body, "header / body");
        UiLayoutContract.RequireInside(report, _market, body, "Market column", 2f);
        UiLayoutContract.RequireInside(report, dossier, body, "dossier column", 2f);
        // Columns, not rows: the market ends before the dossier begins.
        if (_market.worldBound.xMax > dossier.worldBound.xMin + 1f)
            report.Fail("Market column overlaps the dossier column");
        // The approval condition: the node map rides in the header (hidden only on the
        // narrow 4:3 trim, where the act label still names the position).
        if (_track.resolvedStyle.display != DisplayStyle.None && _track.childCount < 3)
            report.Fail("header node map lost its beats");
        if (inspectorStats != null &&
            inspectorStats.resolvedStyle.display != DisplayStyle.None)
            UiLayoutContract.RequireInside(
                report, inspectorStats, inspectorContent, "dossier stat rail");
        // The rack floats over the dossier's edge — the market and the rail must stay
        // fully live while it is open (the equip flow needs source and sockets at once).
        if (_armory.resolvedStyle.display != DisplayStyle.None)
        {
            if (_market.resolvedStyle.display == DisplayStyle.None)
                report.Fail("Market hidden while the Armory rack is open");
            UiLayoutContract.RequireInside(report, _armory, _root, "Armory rack", 4f);
            if (_armory.worldBound.xMin < _market.worldBound.xMax - 4f)
                report.Fail("Armory rack covers the Market column");
        }
        if (inspectorDeferred != null &&
            inspectorDeferred.resolvedStyle.display != DisplayStyle.None)
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-deferred-row",
                inspectorBody, inspectorActions);
        // The PATH promise must be fully on stage (workbench-frame): rows sliding under
        // the pinned actions was invisible to every other contract.
        if (_inspector.Root.Q<VisualElement>(className: "wb-path-row") != null)
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-path-row",
                inspectorBody, inspectorActions);
        if (inspectorBody != null && inspectorActions != null &&
            inspectorBody.worldBound.yMax > inspectorActions.worldBound.yMin + 1f)
            report.Fail("dossier detail runs behind its pinned actions");
        if (inspectorTags != null &&
            inspectorTags.resolvedStyle.display != DisplayStyle.None)
        {
            UiLayoutContract.RequireInside(
                report, inspectorTags, inspectorContent, "dossier mechanic labels");
            UiLayoutContract.RequireAbove(
                report, inspectorTags, inspectorActions, "mechanic labels / pinned actions");
        }
        if (inspectorTraitTab != null)
            report.Fail("dossier still exposes a separate Traits tab");
        if (_inspector.Root.Q<VisualElement>(className: "wb-keyword-chip") != null)
            report.Fail("dossier still exposes detached keyword chips");
        ValidateDossierContentBounds(
            _inspector.Root, inspectorBody, inspectorActions, report);
        if (_inspector.Root.ClassListContains("wb-inspector--rankup"))
        {
            UiLayoutContract.RequireInside(
                report, rankUpBody, inspectorContent, "Rank Up body");
            UiLayoutContract.RequireAbove(
                report, rankUpBody, inspectorActions, "Rank Up body / pinned actions");
            UiLayoutContract.RequireVisibleChildCount(
                report, rankUpLadder, 3, "Rank Up B/A/S ladder");
            UiLayoutContract.RequireVisibleChildCount(
                report, rankUpOptions, 2, "Rank Up exact options");
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-rank-tier", rankUpBody, inspectorActions);
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-rank-option", rankUpBody, inspectorActions);
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-rank-option__rule",
                rankUpBody, inspectorActions);
            UiLayoutContract.RequireWrappedTextFits(
                report, _inspector.Root, "wb-rank-tier__name");
            UiLayoutContract.RequireWrappedTextFits(
                report, _inspector.Root, "wb-rank-option__name");
            UiLayoutContract.RequireWrappedTextFits(
                report, _inspector.Root, "wb-rank-option__rule");
        }
        UiLayoutContract.RequireClassInside(
            report, _inspector.Root, "wb-trait-chip--inline",
            inspectorBody, inspectorActions);
        UiLayoutContract.RequireClassInside(
            report, _inspector.Root, "semantic-text--interactive",
            inspectorBody, inspectorActions, tolerance: 2f);
        UiLayoutContract.RequireNoScrollView(report, _root, "Workbench");
        for (int i = 0; i < marketGrid.childCount; i++)
        {
            VisualElement card = marketGrid[i];
            if (card.resolvedStyle.display == DisplayStyle.None) continue;
            UiLayoutContract.RequireInside(
                report, card, _market, $"Market card {i}", 4f);
        }
        // Blocking choices are clipped by their row on purpose. That makes a card min-height
        // larger than the resolved row look like a cut-off action button instead of an obvious
        // overflow, so validate both the card boundary and the authored footer inset.
        if (_choiceScrim.resolvedStyle.display != DisplayStyle.None)
        {
            UiLayoutContract.RequireInside(
                report, _choiceOptions, _choiceScrim, "Choice options", 2f);
            for (int i = 0; i < _choiceOptions.childCount; i++)
            {
                VisualElement card = _choiceOptions[i];
                if (card.resolvedStyle.display == DisplayStyle.None) continue;
                UiLayoutContract.RequireInside(
                    report, card, _choiceOptions, $"Choice card {i}", 2f);
                VisualElement action =
                    card.Q<VisualElement>(className: "workbench-choice-card__action");
                if (action == null)
                {
                    report.Fail($"Choice card {i} lost its action footer");
                    continue;
                }
                UiLayoutContract.RequireInside(
                    report, action, card, $"Choice action {i}", 2f);
                if (action.worldBound.yMax > card.worldBound.yMax - 12f)
                    report.Fail($"Choice action {i} lost its bottom inset");
            }
            UiLayoutContract.RequireMinimumHeight(
                report, _choiceOptions, "workbench-choice-card__action", 44f);
            UiLayoutContract.RequireSingleLineTextFits(
                report, _choiceOptions, "workbench-choice-card__action", 3f);
        }
        if (_armory.resolvedStyle.display != DisplayStyle.None)
            for (int i = 0; i < _armoryGrid.childCount; i++)
            {
                VisualElement item = _armoryGrid[i];
                if (item.resolvedStyle.display == DisplayStyle.None) continue;
                UiLayoutContract.RequireInside(
                    report, item, _armoryGrid, $"Armory item {i}", 3f);
            }
        UiLayoutContract.RequireMinimumFont(
            report, _inspector.Root, "wb-choice-preview__rule", 16f);
        UiLayoutContract.RequireMinimumFont(
            report, _inspector.Root, "wb-rank-option__rule", 16f);
        UiLayoutContract.RequireMinimumFont(
            report, _inspector.Root, "wb-inspector__line-copy", 16f);
        UiLayoutContract.RequireMinimumFont(
            report, _inspector.Root, "wb-trait-chip__name", 13f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, _inspector.Root, "wb-inspector__line-copy", 12.5f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, _inspector.Root, "wb-choice-preview__rule", 12.5f);
        UiLayoutContract.RequireMinimumHeight(
            report, _inspector.Root, "wb-inspector__section", 36f);
        UiLayoutContract.RequireMinimumHeight(
            report, _root, "market-offer-card__art", 72f);
        UiLayoutContract.RequireMinimumWidth(
            report, _inspector.Root, "wb-inspector__column", 120f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _inspector.Root, "wb-inspector__line-copy");
        UiLayoutContract.RequireWrappedTextFits(
            report, _inspector.Root, "wb-inspector__line-title", 3.5f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _inspector.Root, "wb-inspector__subtitle");
        UiLayoutContract.RequireWrappedTextFits(
            report, _root, "workbench-item__title");
        UiLayoutContract.RequireSingleLineTextFits(
            report, _root, "workbench-header__continue", 3f);
        UiLayoutContract.RequireSingleLineTextFits(
            report, _root, "market-offer-card__title", 3f);
        UiLayoutContract.RequireSingleLineTextFits(
            report, _inspector.Root, "btn", 3f);

        // Muster state (workbench-frame): pre-run shows no economy, the instruction cell
        // rides in the grid, and the continue slot is the gated BEGIN RUN.
        if (_root.ClassListContains("workbench--muster"))
        {
            VisualElement sandHost = _hourstone.parent ?? _hourstone;
            if (sandHost.resolvedStyle.display != DisplayStyle.None)
                report.Fail("Muster still shows the hourstone chip");
            if (_musterGhost.resolvedStyle.display == DisplayStyle.None)
                report.Fail("Muster lost its instruction cell");
            if (!(_continue.text ?? "").Contains("BEGIN RUN"))
                report.Fail("Muster continue button is not BEGIN RUN");
            if (_armory.resolvedStyle.display != DisplayStyle.None)
                report.Fail("Muster shows the Armory rack");
        }

        // Rank-up modal (workbench-frame): hero card center, at least two options, all of
        // it inside the safe area with readable rules.
        if (_rankupScrim.resolvedStyle.display != DisplayStyle.None)
        {
            UiLayoutContract.RequireInside(
                report, _rankupModal, _root, "Rank-up modal", 4f);
            int optionCount = 0;
            bool cardSeen = false;
            for (int i = 0; i < _rankupRow.childCount; i++)
            {
                VisualElement child = _rankupRow[i];
                if (child.resolvedStyle.display == DisplayStyle.None) continue;
                UiLayoutContract.RequireInside(
                    report, child, _rankupModal, $"Rank-up row item {i}", 3f);
                if (child.ClassListContains("workbench-rankup-option")) optionCount++;
                if (child.ClassListContains("workbench-rankup-card")) cardSeen = true;
            }
            if (optionCount < 2)
                report.Fail("Rank-up modal shows fewer than two options");
            if (!cardSeen)
                report.Fail("Rank-up modal lost its hero card");
            UiLayoutContract.RequireWrappedTextFits(
                report, _rankupRow, "workbench-rankup-option__rule");
            UiLayoutContract.RequireMinimumFont(
                report, _rankupRow, "workbench-rankup-option__rule", 14f);
            UiLayoutContract.RequireSingleLineTextFits(
                report, _rankupRow, "workbench-rankup-option__bind", 3f);
        }
        return report;
    }

    private static void ValidateDossierContentBounds(
        VisualElement inspector, VisualElement body, VisualElement actions,
        UiLayoutReport report)
    {
        if (inspector == null || body == null ||
            body.resolvedStyle.display == DisplayStyle.None)
            return;
        foreach (string className in new[]
                 {
                     "wb-inspector__section",
                     "wb-unit-weapon",
                     "wb-weapon-stat",
                     "wb-unit-weapon-property",
                     "wb-unit-spec",
                     "wb-comparison",
                     "wb-choice-preview",
                     "wb-choice-preview__rule",
                 })
        {
            UiLayoutContract.RequireClassInside(
                report, inspector, className, body, actions);
        }
        UiLayoutContract.RequireWrappedTextFits(
            report, inspector, "wb-choice-preview__rule");
        UiLayoutContract.RequireWrappedTextFits(
            report, inspector, "wb-unit-weapon-property__summary");
        UiLayoutContract.RequireWrappedTextFits(
            report, inspector, "wb-inspector__line-copy");
        UiLayoutContract.RequireClassInsideNearestAncestor(
            report, inspector, "wb-inspector__line-copy", "wb-inspector__section");

        List<VisualElement> sections =
            inspector.Query<VisualElement>(
                className: "wb-inspector__section").ToList();
        VisualElement previous = null;
        foreach (VisualElement section in sections)
        {
            if (!Visible(section)) continue;
            if (previous != null)
                UiLayoutContract.RequireAbove(
                    report, previous, section, "dossier rule sections");
            previous = section;
        }
        VisualElement path = inspector.Q<VisualElement>("path");
        if (previous != null && Visible(path))
            UiLayoutContract.RequireAbove(
                report, previous, path, "dossier rules / Specs");
    }

    public bool EditorShowFirstKeywordTooltip()
    {
        return _inspector.EditorShowSemanticKeywordTooltip("RIPOSTE") ||
               _inspector.EditorShowSemanticKeywordTooltip();
    }

    public bool EditorShowFirstRankTierTooltip() =>
        _inspector.EditorShowFirstRankTierTooltip();

    public bool EditorShowFirstWeaponFactTooltip() =>
        _inspector.EditorShowFirstWeaponFactTooltip();

    public bool EditorShowWeaponPropertyTooltip() =>
        _inspector.EditorShowWeaponPropertyTooltip();

    private static string Bounds(VisualElement element) =>
        element == null
            ? "missing"
            : $"{element.worldBound.xMin:0.#},{element.worldBound.yMin:0.#}," +
              $"{element.worldBound.width:0.#},{element.worldBound.height:0.#}";

    private static bool Visible(VisualElement element) =>
        element != null &&
        element.resolvedStyle.display != DisplayStyle.None &&
        element.resolvedStyle.visibility == Visibility.Visible;

    private static int VisibleChildren(VisualElement element)
    {
        if (element == null) return 0;
        int count = 0;
        for (int i = 0; i < element.childCount; i++)
            if (Visible(element[i])) count++;
        return count;
    }
#endif

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        T element = root.Q<T>(name);
        if (element == null)
            throw new InvalidOperationException($"[Workbench] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool shown) =>
        element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
