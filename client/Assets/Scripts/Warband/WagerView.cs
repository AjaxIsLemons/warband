using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Stakes-first commitment. Exact enemies are intentionally absent until this decision is made.
/// </summary>
internal sealed class WagerView : IRunScreenView
{
    private readonly RunShellActions _actions;
    private readonly VisualElement _root;
    private readonly Label _act;
    private readonly Label _beat;
    private readonly Label _sand;
    private readonly Label _heading;
    private readonly Label _brief;
    private readonly Label _disclosure;
    private readonly VisualElement _track;
    private readonly VisualElement _risks;
    private readonly Button _continue;

    public RunScreen Screen => RunScreen.Wager;
    public VisualElement Root => _root;

    public WagerView(RunShellActions actions)
    {
        _actions = actions;
        var tree = UnityEngine.Resources.Load<VisualTreeAsset>("UI/WagerScreen");
        if (tree == null)
            throw new InvalidOperationException("[UI] Resources/UI/WagerScreen.uxml is required.");
        var host = new VisualElement();
        tree.CloneTree(host);
        _root = Required<VisualElement>(host, "wager-root");
        _root.RemoveFromHierarchy();
        _act = Required<Label>(_root, "act");
        _beat = Required<Label>(_root, "beat");
        _sand = Required<Label>(_root, "sand");
        _heading = Required<Label>(_root, "heading");
        _brief = Required<Label>(_root, "brief");
        _disclosure = Required<Label>(_root, "disclosure");
        _track = Required<VisualElement>(_root, "track");
        _risks = Required<VisualElement>(_root, "risks");
        _continue = Required<Button>(_root, "continue");
        Required<Button>(_root, "back").clicked += () => _actions.ReturnToManagement?.Invoke();
        _continue.clicked += () => _actions.ConfirmWager?.Invoke();
    }

    public void Bind(RunShellModel shell)
    {
        var model = shell.Wager;
        _act.text = model.Act;
        _beat.text = model.Beat;
        _sand.text = model.Sand;
        _heading.text = model.Heading;
        _brief.text = model.Brief;
        _disclosure.text = model.Disclosure;
        _continue.text = model.ContinueLabel;
        _continue.SetEnabled(model.CanContinue);
        BindTrack(model.Track);
        BindRisks(model.Risks);
    }

    private void BindRisks(IReadOnlyList<TierChoiceModel> risks)
    {
        _risks.Clear();
        foreach (var risk in risks)
        {
            int index = risk.Index;
            var button = new Button(() => _actions.ChooseTier?.Invoke(index));
            button.AddToClassList("wager-card");
            button.EnableInClassList("wager-card--selected", risk.Selected);
            var ordinal = new Label($"WAGER {index + 1}");
            ordinal.AddToClassList("wager-card__ordinal");
            var name = new Label(risk.Name);
            name.AddToClassList("wager-card__name");
            var danger = new Label(risk.Risk);
            danger.AddToClassList("wager-card__risk");
            var reward = new Label(risk.Reward);
            reward.AddToClassList("wager-card__reward");
            var action = new Label(risk.Selected ? "SELECTED  ✓" : "CHOOSE");
            action.AddToClassList("wager-card__action");
            button.Add(ordinal);
            button.Add(name);
            button.Add(danger);
            button.Add(reward);
            button.Add(action);
            _risks.Add(button);
        }
    }

    private void BindTrack(IReadOnlyList<PlanningTrackNodeModel> nodes)
    {
        _track.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = new VisualElement();
            node.AddToClassList("management-track-node");
            node.AddToClassList("management-track-node--" + nodes[i].State);
            node.Add(new Label(nodes[i].Kind == "Boss" ? "♛" :
                               nodes[i].Kind == "Interlude" ? "◇" : "⚔"));
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

    private static T Required<T>(VisualElement root, string name) where T : VisualElement
    {
        var element = root.Q<T>(name);
        if (element == null) throw new InvalidOperationException($"[Wager] Missing '{name}'.");
        return element;
    }
}
