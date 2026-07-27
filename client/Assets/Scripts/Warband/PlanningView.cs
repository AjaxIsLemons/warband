using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// One persistent board-first workspace for intel, deployment, roster, market, armory, and
/// Hourstone decisions. Tabs change the bottom tool, never the place the player is standing.
/// </summary>
internal sealed class PlanningView : IRunScreenView
{
    private sealed class CardPool
    {
        private readonly VisualElement _host;
        private readonly Action<string> _onSelect;
        private readonly List<WarbandCard> _cards = new List<WarbandCard>();
        private readonly List<string> _keys = new List<string>();

        public CardPool(VisualElement host, Action<string> onSelect)
        {
            _host = host;
            _onSelect = onSelect;
        }

        public void Bind(IReadOnlyList<CardModel> models)
        {
            while (_cards.Count > models.Count)
            {
                int last = _cards.Count - 1;
                _cards[last].Root.RemoveFromHierarchy();
                _cards.RemoveAt(last);
                _keys.RemoveAt(last);
            }
            while (_cards.Count < models.Count)
            {
                var card = new WarbandCard(_onSelect);
                _cards.Add(card);
                _keys.Add("");
                _host.Add(card.Root);
            }
            for (int i = 0; i < models.Count; i++)
            {
                bool entering = _keys[i] != models[i].Key;
                _keys[i] = models[i].Key;
                _cards[i].Bind(models[i]);
                if (!entering) continue;
                var root = _cards[i].Root;
                root.AddToClassList("wb-card--entering");
                root.schedule.Execute(() => root.RemoveFromClassList("wb-card--entering"))
                    .StartingIn(Math.Min(300, i * 40 + 1));
            }
        }
    }

    private readonly RunShellActions _actions;
    private readonly VisualElement _root;
    private readonly VisualElement _boardSurface;
    private readonly Label _act;
    private readonly Label _beat;
    private readonly Label _sand;
    private readonly Label _capacity;
    private readonly Label _heading;
    private readonly Label _brief;
    private readonly VisualElement _ruleBox;
    private readonly Label _rule;
    private readonly VisualElement _track;
    private readonly VisualElement _risks;
    private readonly VisualElement _enemyList;
    private readonly VisualElement _dockContent;
    private readonly Label _dockNote;
    private readonly Label _feedback;
    private readonly Button _musterTab;
    private readonly Button _marketTab;
    private readonly Button _armoryTab;
    private readonly Button _hourstoneTab;
    private readonly Button _secondary;
    private readonly Button _commit;
    private readonly VisualElement _modalScrim;
    private readonly Label _modalEyebrow;
    private readonly Label _modalTitle;
    private readonly Label _modalCopy;
    private readonly VisualElement _modalOptions;
    private readonly InspectorPanel _inspector;
    private readonly CardPool _enemyCards;
    private readonly CardPool _dockCards;

    public RunScreen Screen => RunScreen.Planning;
    public VisualElement Root => _root;

    public PlanningView(RunShellActions actions)
    {
        _actions = actions;
        var tree = Resources.Load<VisualTreeAsset>("UI/PlanningWorkspace");
        if (tree == null)
            throw new InvalidOperationException("[UI] Resources/UI/PlanningWorkspace.uxml is required.");

        var host = new VisualElement();
        tree.CloneTree(host);
        _root = Required<VisualElement>(host, "planning-root");
        _root.RemoveFromHierarchy();

        _boardSurface = Required<VisualElement>(_root, "board-hit-surface");
        _act = Required<Label>(_root, "act");
        _beat = Required<Label>(_root, "beat");
        _sand = Required<Label>(_root, "sand");
        _capacity = Required<Label>(_root, "capacity");
        _heading = Required<Label>(_root, "heading");
        _brief = Required<Label>(_root, "brief");
        _ruleBox = Required<VisualElement>(_root, "rule-box");
        _rule = Required<Label>(_root, "rule");
        _track = Required<VisualElement>(_root, "track");
        _risks = Required<VisualElement>(_root, "risk-list");
        _enemyList = Required<VisualElement>(_root, "enemy-list");
        _dockContent = Required<VisualElement>(_root, "dock-content");
        _dockNote = Required<Label>(_root, "dock-note");
        _feedback = Required<Label>(_root, "feedback");
        _musterTab = Required<Button>(_root, "tab-muster");
        _marketTab = Required<Button>(_root, "tab-market");
        _armoryTab = Required<Button>(_root, "tab-armory");
        _hourstoneTab = Required<Button>(_root, "tab-hourstone");
        _secondary = Required<Button>(_root, "secondary");
        _commit = Required<Button>(_root, "commit");
        _modalScrim = Required<VisualElement>(_root, "modal-scrim");
        _modalEyebrow = Required<Label>(_root, "modal-eyebrow");
        _modalTitle = Required<Label>(_root, "modal-title");
        _modalCopy = Required<Label>(_root, "modal-copy");
        _modalOptions = Required<VisualElement>(_root, "modal-options");

        _inspector = new InspectorPanel(id => _actions.InspectorAction?.Invoke(id));
        Required<VisualElement>(_root, "inspector-slot").Add(_inspector.Root);
        _enemyCards = new CardPool(_enemyList, key => _actions.SelectPlanningCard?.Invoke(key));
        _dockCards = new CardPool(_dockContent, key => _actions.SelectPlanningCard?.Invoke(key));

        _musterTab.clicked += () => _actions.SetPlanningTab?.Invoke((int)PlanningTab.Muster);
        _marketTab.clicked += () => _actions.SetPlanningTab?.Invoke((int)PlanningTab.Market);
        _armoryTab.clicked += () => _actions.SetPlanningTab?.Invoke((int)PlanningTab.Armory);
        _hourstoneTab.clicked += () => _actions.SetPlanningTab?.Invoke((int)PlanningTab.Hourstone);
        _secondary.clicked += () => _actions.Reroll?.Invoke();
        _commit.clicked += () => _actions.CommitDeployment?.Invoke();
        _boardSurface.RegisterCallback<ClickEvent>(evt =>
            _actions.BoardClicked?.Invoke(new Vector2(evt.position.x, evt.position.y)));
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        _root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        bool reduced = PlayerPrefs.GetInt("ui.reducedMotion", 0) != 0 ||
                       Array.Exists(Environment.GetCommandLineArgs(), a => a == "--reduced-motion");
        _root.EnableInClassList("motion--reduced", reduced);
        _root.EnableInClassList("input--touch", SystemInfo.deviceType == DeviceType.Handheld);
    }

    public void Bind(RunShellModel shell)
    {
        var model = shell.Planning;
        _act.text = model.Act;
        _beat.text = model.Beat;
        _sand.text = model.Sand;
        _capacity.text = model.Capacity;
        _heading.text = model.Heading;
        _brief.text = model.Brief;
        MechanicPresentation.BindInline(_rule, model.Rule);
        SetDisplayed(_ruleBox, !string.IsNullOrEmpty(model.Rule));
        _feedback.text = model.Feedback;
        _feedback.EnableInClassList("feedback-label--error", model.FeedbackIsError);
        SetDisplayed(_feedback, !string.IsNullOrEmpty(model.Feedback));

        BindTrack(model.Track);
        BindRisk(model);
        _enemyCards.Bind(model.Enemies);
        BindTabs(model.ActiveTab);
        BindDock(model);
        _inspector.Bind(model.Inspector);

        _commit.text = model.CommitLabel;
        _commit.SetEnabled(model.CanCommit);
        SetDisplayed(_commit, model.BeatKind == PlanningBeat.Fight ||
                               model.BeatKind == PlanningBeat.Boss);
        _secondary.text = model.RerollLabel;
        _secondary.SetEnabled(model.CanReroll);
        SetDisplayed(_secondary, model.ActiveTab == PlanningTab.Market);

        BindBlockingChoice(model);
    }

    private void BindTrack(IReadOnlyList<PlanningTrackNodeModel> models)
    {
        _track.Clear();
        for (int i = 0; i < models.Count; i++)
        {
            var node = new VisualElement();
            node.AddToClassList("planning-track-node");
            node.AddToClassList("planning-track-node--" + models[i].State);
            node.tooltip = models[i].Kind;
            var icon = new Label(BeatIcon(models[i].Kind));
            icon.AddToClassList("planning-track-node__icon");
            var label = new Label(models[i].Label);
            label.AddToClassList("planning-track-node__label");
            node.Add(icon);
            node.Add(label);
            _track.Add(node);
            if (i + 1 < models.Count)
            {
                var link = new VisualElement();
                link.AddToClassList("planning-track-link");
                link.EnableInClassList("planning-track-link--past", models[i].State == "past");
                _track.Add(link);
            }
        }
    }

    private void BindRisk(PlanningModel model)
    {
        _risks.Clear();
        SetDisplayed(_risks, model.ShowRisk);
        if (!model.ShowRisk) return;
        foreach (var risk in model.Risks)
        {
            int index = risk.Index;
            var button = new Button(() => _actions.ChooseTier?.Invoke(index));
            button.AddToClassList("planning-risk");
            button.EnableInClassList("planning-risk--selected", risk.Selected);
            var name = new Label(risk.Name);
            name.AddToClassList("planning-risk__name");
            var read = new Label(risk.Risk);
            read.AddToClassList("planning-risk__read");
            var reward = new Label(risk.Reward);
            reward.AddToClassList("planning-risk__reward");
            button.Add(name);
            button.Add(read);
            button.Add(reward);
            _risks.Add(button);
        }
    }

    private void BindTabs(PlanningTab tab)
    {
        _musterTab.EnableInClassList("planning-tab--active", tab == PlanningTab.Muster);
        _marketTab.EnableInClassList("planning-tab--active", tab == PlanningTab.Market);
        _armoryTab.EnableInClassList("planning-tab--active", tab == PlanningTab.Armory);
        _hourstoneTab.EnableInClassList("planning-tab--active", tab == PlanningTab.Hourstone);
    }

    private void BindDock(PlanningModel model)
    {
        IReadOnlyList<CardModel> cards;
        switch (model.ActiveTab)
        {
            case PlanningTab.Market:
                cards = model.Market;
                _dockNote.text = model.SlotOfferOpen
                    ? model.SlotOfferText
                    : "Select stock to compare it. Purchase from the inspector.";
                break;
            case PlanningTab.Armory:
                cards = model.Armory;
                _dockNote.text = cards.Count == 0
                    ? "No equipment stowed."
                    : "Select equipment, then equip it from a champion inspector.";
                break;
            case PlanningTab.Hourstone:
                cards = model.Inscriptions;
                _dockNote.text = cards.Count == 0
                    ? "No Inscriptions bound yet."
                    : "Run-wide rules remain visible here.";
                break;
            default:
                var roster = new List<CardModel>(model.Field.Count + model.Bench.Count);
                roster.AddRange(model.Field);
                roster.AddRange(model.Bench);
                cards = roster;
                _dockNote.text = "Select a champion, then place them on the board or tune their loadout.";
                break;
        }
        _dockCards.Bind(cards);
    }

    private void BindBlockingChoice(PlanningModel model)
    {
        bool spec = model.SpecChoice.Pending;
        bool reward = model.BeatKind == PlanningBeat.Interlude ||
                      model.BeatKind == PlanningBeat.BossReward;
        _modalScrim.EnableInClassList("choice--reward", reward && !spec);
        _modalScrim.EnableInClassList("choice--rank", spec);
        SetDisplayed(_modalScrim, spec || reward);
        if (!spec && !reward) return;

        _modalOptions.Clear();
        if (spec)
        {
            _modalEyebrow.text = model.SpecChoice.RankLabel + " AWAKENING";
            _modalTitle.text = model.SpecChoice.HeroName;
            _modalCopy.text = "Choose one path. The rest of Planning waits for this decision.";
            AddChoiceButton(model.SpecChoice.OptionAName, model.SpecChoice.OptionAText,
                            () => _actions.ChooseSpec?.Invoke(0));
            AddChoiceButton(model.SpecChoice.OptionBName, model.SpecChoice.OptionBText,
                            () => _actions.ChooseSpec?.Invoke(1));
            return;
        }

        _modalEyebrow.text = model.BeatKind == PlanningBeat.BossReward
            ? "BOSS REWARD"
            : "INTERLUDE";
        _modalTitle.text = model.BeatKind == PlanningBeat.BossReward
            ? "Bind one Inscription"
            : "Choose how this quiet Hour changes the run";
        _modalCopy.text = model.BeatKind == PlanningBeat.BossReward
            ? "The act is won. Choose one visible run-wide rule before the road opens."
            : "Treasury is certain Sand. Armory and Hourstone show every offered choice.";

        foreach (var choice in model.Interlude)
        {
            int path = choice.Path;
            int option = choice.Option;
            string title = choice.Card.Title;
            string copy = choice.Card.AbilitySummary;
            AddChoiceButton(title, copy, () =>
            {
                if (model.BeatKind == PlanningBeat.BossReward)
                    _actions.ChooseBossReward?.Invoke(option);
                else
                    _actions.ChooseInterlude?.Invoke(path, option);
            });
        }
    }

    private void AddChoiceButton(string title, string copy, Action action)
    {
        var button = new Button(action);
        button.AddToClassList("planning-choice");
        var name = new Label(title);
        name.AddToClassList("planning-choice__title");
        var summary = new Label(copy);
        summary.AddToClassList("planning-choice__copy");
        button.Add(name);
        button.Add(summary);
        _modalOptions.Add(button);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        int tab = evt.keyCode == KeyCode.Alpha1 ? 0 :
                  evt.keyCode == KeyCode.Alpha2 ? 1 :
                  evt.keyCode == KeyCode.Alpha3 ? 2 :
                  evt.keyCode == KeyCode.Alpha4 ? 3 : -1;
        if (tab >= 0)
        {
            _actions.SetPlanningTab?.Invoke(tab);
            evt.StopPropagation();
        }
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        _root.EnableInClassList("layout--compact", evt.newRect.width < 1500f);
        _root.EnableInClassList("layout--short", evt.newRect.height < 850f);
    }

    private static string BeatIcon(string kind)
    {
        if (kind == "Boss") return "♛";
        if (kind == "Interlude" || kind == "Event") return "◇";
        return "⚔";
    }

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null) throw new InvalidOperationException($"[Planning] Missing '{name}'.");
        return element;
    }

    private static void SetDisplayed(VisualElement element, bool value) =>
        element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
}
