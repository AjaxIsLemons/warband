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
                    (_, anchor) =>
                    {
                        if (anchor?.userData is string key && !string.IsNullOrEmpty(key))
                            _actions.SelectPlanningCard?.Invoke(key);
                    });
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

        public VisualElement Root { get; }

        public ArmoryTile(Action<string> select, RuntimeTooltipService tooltips)
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
            Root.RegisterCallback<ClickEvent>(_ => Select());
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space) return;
                Select();
                evt.StopPropagation();
            });
            _tooltips.Attach(Root, Tooltip);
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
            WarbandCard.SetAccent(Root, _model.Accent);

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
            tooltip.Footer = "Select to pin · then choose a matching unit socket";
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
    private readonly Label _act;
    private readonly Label _beat;
    private readonly Label _brief;
    private readonly HourstoneAmount _hourstone;
    private readonly Button _continue;
    private readonly VisualElement _market;
    private readonly Label _marketStatus;
    private readonly Button _reroll;
    private readonly VisualElement _armory;
    private readonly Label _armoryStatus;
    private readonly Label _armoryPage;
    private readonly Button _armoryPrev;
    private readonly Button _armoryNext;
    private readonly Button _armoryClose;
    private readonly VisualElement _armoryGrid;
    private readonly VisualElement _armoryEmpty;
    private readonly Label _feedback;
    private readonly VisualElement _choiceScrim;
    private readonly Label _choiceEyebrow;
    private readonly Label _choiceTitle;
    private readonly Label _choiceCopy;
    private readonly VisualElement _choiceOptions;
    private readonly InspectorPanel _inspector;
    private readonly MarketPool _marketCards;
    private readonly List<ArmoryTile> _armoryTiles = new List<ArmoryTile>();

    private IReadOnlyList<CardModel> _armoryModels = Array.Empty<CardModel>();
    private int _armoryPageIndex;
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
        _act = Required<Label>(_root, "act");
        _beat = Required<Label>(_root, "beat");
        _brief = Required<Label>(_root, "brief");
        _continue = Required<Button>(_root, "continue");
        _market = Required<VisualElement>(_root, "market");
        _marketStatus = Required<Label>(_root, "market-status");
        _reroll = Required<Button>(_root, "reroll");
        _armory = Required<VisualElement>(_root, "armory");
        _armoryStatus = Required<Label>(_root, "armory-status");
        _armoryPage = Required<Label>(_root, "armory-page");
        _armoryPrev = Required<Button>(_root, "armory-prev");
        _armoryNext = Required<Button>(_root, "armory-next");
        _armoryClose = Required<Button>(_root, "armory-close");
        _armoryGrid = Required<VisualElement>(_root, "armory-grid");
        _armoryEmpty = Required<VisualElement>(_root, "armory-empty");
        _feedback = Required<Label>(_root, "feedback");
        // Feedback is presentation, never an interaction layer. It is absolute so it can occupy
        // otherwise dead edge space, but must never consume purchase clicks beneath it.
        _feedback.pickingMode = PickingMode.Ignore;
        _choiceScrim = Required<VisualElement>(_root, "choice-scrim");
        _choiceEyebrow = Required<Label>(_root, "choice-eyebrow");
        _choiceTitle = Required<Label>(_root, "choice-title");
        _choiceCopy = Required<Label>(_root, "choice-copy");
        _choiceOptions = Required<VisualElement>(_root, "choice-options");

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
        RegisterTarget("ledger-sand", _hourstone);
        RegisterTarget("action-continue", _continue, true);
        RegisterTarget("action-reroll", _reroll, true);
        RegisterTarget("workbench-dossier", _inspector.Root);
        RegisterTarget("workbench-armory", _armory);
        RegisterTarget("workbench-market", _market);
        RegisterTarget("feedback", _feedback);

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
        _root.EnableInClassList("workbench--armory-open", drawerOpen);
        _root.EnableInClassList("motion--reduced", model.ReducedMotion);
        _polish.SetReducedMotion(model.ReducedMotion);
        _tooltips.SetReducedMotion(model.ReducedMotion);

        _act.text = model.Act;
        _beat.text = model.Beat;
        _brief.text = model.Brief;
        _hourstone.Bind(int.TryParse(model.Sand, out int amount) ? amount : 0);
        _continue.text = string.IsNullOrWhiteSpace(model.CommitLabel)
            ? "TO THE BREACH  →"
            : model.CommitLabel + "  →";
        _continue.SetEnabled(model.CanCommit);

        int live = 0;
        foreach (MarketOfferCardModel offer in model.MarketOffers)
            if (!offer.Sold) live++;
        _marketStatus.text =
            $"{live} LIVE OFFER{(live == 1 ? "" : "S")} · SELECT FOR FULL DOSSIER";
        _marketCards.Bind(model.MarketOffers);
        if (model.RerollCost >= 0)
            MechanicPresentation.BindCurrencyButton(
                _reroll, "REROLL", model.RerollCost);
        else
            _reroll.text = model.RerollLabel;
        _reroll.SetEnabled(model.CanReroll);

        _feedback.text = model.Feedback;
        _feedback.EnableInClassList("workbench-feedback--error", model.FeedbackIsError);
        SetDisplayed(_feedback, !string.IsNullOrWhiteSpace(model.Feedback));
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
        _inspector.Bind(dossier ?? new InspectorModel { Empty = true });

        _armoryModels = model.PartyShelf.LoadoutInventory;
        _armoryPageIndex = Mathf.Clamp(
            _armoryPageIndex, 0, Mathf.Max(0, ArmoryPageCount() - 1));
        BindArmory();
        BindBlockingChoice(model);
    }

    public void OnScreenEntered()
    {
        _active = true;
        _polish.SetActive(true);
    }

    public void OnScreenExited()
    {
        _active = false;
        _tooltips.Hide();
        _polish.SetActive(false);
    }

    private void BindArmory()
    {
        int count = _armoryModels?.Count ?? 0;
        int first = _armoryPageIndex * ArmoryPageSize;
        int shown = Mathf.Min(ArmoryPageSize, Mathf.Max(0, count - first));
        while (_armoryTiles.Count < shown)
        {
            var tile = new ArmoryTile(
                key => _actions.SelectLoadoutItem?.Invoke(key), _tooltips);
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

    private void ChangeArmoryPage(int delta)
    {
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
        bool spec = model.SpecChoice.Pending;
        bool reward = model.BeatKind == PlanningBeat.Interlude ||
                      model.BeatKind == PlanningBeat.BossReward;
        SetDisplayed(_choiceScrim, spec || reward);
        _choiceOptions.Clear();
        if (!spec && !reward) return;

        if (spec)
        {
            _choiceEyebrow.text = model.SpecChoice.RankLabel + " AWAKENING";
            _choiceTitle.text = model.SpecChoice.HeroName;
            MechanicPresentation.BindInline(
                _choiceCopy, "Choose one path before making another decision.");
            for (int i = 0; i < model.SpecChoice.Options.Count; i++)
            {
                int index = i;
                SpecOptionModel option = model.SpecChoice.Options[i];
                _choiceOptions.Add(Choice(
                    option.Change, option.Name, option.Text,
                    () => _actions.ChooseSpec?.Invoke(index), option.Comparisons));
            }
            return;
        }

        _choiceEyebrow.text = model.BeatKind == PlanningBeat.BossReward
            ? "BOSS REWARD"
            : "INTERLUDE";
        _choiceTitle.text = model.BeatKind == PlanningBeat.BossReward
            ? "Bind one Inscription"
            : "Choose what this quiet Hour leaves behind";
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
                    else
                        _actions.ChooseInterlude?.Invoke(path, option);
                }));
        }
    }

    private VisualElement Choice(string eyebrow, string title, string copy, Action action,
                                 IReadOnlyList<StatComparisonModel> comparisons = null)
    {
        var button = new Button(action);
        button.AddToClassList("workbench-choice-card");
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
        _tooltips.Hide();
        _polish.Dispose();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public bool EditorValidateResolvedLayout()
    {
        UiLayoutReport report = EditorLayoutReport();
        if (report.Passed) return true;
        Debug.LogError("[Workbench Layout] " + report);
        return false;
    }

    public string EditorResolvedLayoutReport() => EditorLayoutReport().ToString();

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
        UiLayoutContract.RequireAbove(report, header, _market, "header / Market");
        UiLayoutContract.RequireAbove(report, _market, body, "Market / dossier body");
        UiLayoutContract.RequireInside(report, dossier, body, "dossier");
        if (inspectorStats != null &&
            inspectorStats.resolvedStyle.display != DisplayStyle.None)
            UiLayoutContract.RequireInside(
                report, inspectorStats, inspectorContent, "dossier stat rail");
        // The Market must stay live while the drawer is open — the drawer and the market
        // share one workspace now, never swap (Design/workbench-dossier.md).
        if (_armory.resolvedStyle.display != DisplayStyle.None)
        {
            if (_market.resolvedStyle.display == DisplayStyle.None)
                report.Fail("Market hidden while the Armory drawer is open");
            UiLayoutContract.RequireInside(report, _armory, body, "Armory drawer");
            UiLayoutContract.RequireAbove(
                report, dossier, _armory, "dossier / Armory drawer");
        }
        if (inspectorDeferred != null &&
            inspectorDeferred.resolvedStyle.display != DisplayStyle.None)
            UiLayoutContract.RequireClassInside(
                report, _inspector.Root, "wb-deferred-row",
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
        UiLayoutContract.RequireNonBlocking(report, _feedback, "Workbench feedback");
        for (int i = 0; i < marketGrid.childCount; i++)
        {
            VisualElement card = marketGrid[i];
            if (card.resolvedStyle.display == DisplayStyle.None) continue;
            UiLayoutContract.RequireInside(
                report, card, _market, $"Market card {i}", 4f);
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
            report, _root, "workbench-header__brief", 16f);
        UiLayoutContract.RequireWrappedTextFits(
            report, _root, "workbench-header__brief", 3f);
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
            report, _root, "workbench-market__reroll", 3f);
        UiLayoutContract.RequireSingleLineTextFits(
            report, _root, "market-offer-card__title", 3f);
        UiLayoutContract.RequireSingleLineTextFits(
            report, _inspector.Root, "btn", 3f);
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
    }

    public bool EditorShowFirstKeywordTooltip()
    {
        return _inspector.EditorShowSemanticKeywordTooltip("RIPOSTE") ||
               _inspector.EditorShowSemanticKeywordTooltip();
    }

    public bool EditorShowFirstRankTierTooltip() =>
        _inspector.EditorShowFirstRankTierTooltip();

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
