using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Deployment. The BOARD is the screen here — this view is deliberately a thin frame around it:
/// a roster rail on the left, the enemy preview on the right, an instruction, and a commit bar.
/// Everything in the middle stays clear so the player can actually see the hexes they are
/// clicking.
///
/// Interaction is two-step and explicit: select a hero in the rail, then click a hex. No drag,
/// no modes — the same thing works with a mouse or a thumb, which matters because the board is
/// mobile-ready/desktop-first.
/// </summary>
internal sealed class DeployView : IRunScreenView
{
    private readonly RunShellActions _actions;
    private readonly VisualElement _root;
    private readonly Label _heading;
    private readonly Label _instruction;
    private readonly Label _progress;
    private readonly Label _feedback;
    private readonly VisualElement _rail;
    private readonly VisualElement _enemyPanel;
    private readonly Label _enemyRule;
    private readonly VisualElement _enemyList;
    private readonly Button _commit;
    private readonly Button _clear;

    public RunScreen Screen => RunScreen.Deploy;
    public VisualElement Root => _root;

    public DeployView(RunShellActions actions)
    {
        _actions = actions;

        _root = new VisualElement { name = "deploy-screen" };
        _root.AddToClassList("shell-screen");
        // The board must remain clickable THROUGH this screen — placement is a board interaction.
        // Only the rail, the panels and the buttons pick; the empty middle never does.
        _root.pickingMode = PickingMode.Ignore;

        // Full-bleed surface UNDER the panels: placement is a board interaction, and this is what
        // turns a click in empty space into a hex. It sits first so every panel overlays it.
        var boardSurface = new VisualElement();
        boardSurface.AddToClassList("board-hit-surface");
        boardSurface.pickingMode = PickingMode.Position;
        boardSurface.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            _actions?.BoardClicked?.Invoke(new Vector2(evt.position.x, evt.position.y));
        });
        _root.Add(boardSurface);

        var header = new VisualElement();
        header.AddToClassList("panel");
        header.AddToClassList("deploy-header");
        header.pickingMode = PickingMode.Position;
        _heading = MakeLabel("eyebrow");
        _instruction = MakeLabel("body-copy");
        _progress = MakeLabel("card-role");
        header.Add(_heading);
        header.Add(_instruction);
        header.Add(_progress);
        _root.Add(header);

        _rail = new VisualElement();
        _rail.AddToClassList("panel");
        _rail.AddToClassList("deploy-rail");
        _rail.pickingMode = PickingMode.Position;
        _root.Add(_rail);

        _enemyPanel = new VisualElement();
        _enemyPanel.AddToClassList("panel");
        _enemyPanel.AddToClassList("deploy-enemy");
        _enemyPanel.pickingMode = PickingMode.Position;
        var enemyEyebrow = MakeLabel("eyebrow");
        enemyEyebrow.AddToClassList("eyebrow--danger");
        enemyEyebrow.text = "ENEMY FORMATION";
        _enemyPanel.Add(enemyEyebrow);
        _enemyList = new VisualElement();
        _enemyPanel.Add(_enemyList);
        _enemyRule = MakeLabel("body-copy");
        _enemyPanel.Add(_enemyRule);
        var pill = MakeLabel("public-info-pill");
        pill.text = "FULL INFO";
        _enemyPanel.Add(pill);
        _root.Add(_enemyPanel);

        var bar = new VisualElement();
        bar.AddToClassList("deploy-bar");
        bar.pickingMode = PickingMode.Position;
        _clear = new Button(() => _actions?.ClearDeployment?.Invoke()) { text = "CLEAR" };
        _clear.AddToClassList("btn");
        _clear.AddToClassList("btn--ghost");
        _commit = new Button(() => _actions?.CommitDeployment?.Invoke());
        _commit.AddToClassList("btn");
        _commit.AddToClassList("btn--primary");
        bar.Add(_clear);
        bar.Add(_commit);
        _root.Add(bar);

        _feedback = MakeLabel("feedback-label");
        _feedback.pickingMode = PickingMode.Ignore;
        _root.Add(_feedback);
    }

    public void Bind(RunShellModel model)
    {
        var d = model.Deploy;

        _heading.text = d.Heading;
        _instruction.text = d.Instruction;
        _progress.text = $"{d.Placed} / {d.Total} PLACED";

        _commit.text = string.IsNullOrEmpty(d.PrimaryText) ? "LOCK IN" : d.PrimaryText;
        _commit.SetEnabled(d.CanCommit);
        _clear.SetEnabled(d.Placed > 0);

        _feedback.text = d.Feedback ?? "";
        SetDisplayed(_feedback, !string.IsNullOrWhiteSpace(d.Feedback));
        _feedback.EnableInClassList("feedback-label--error", d.FeedbackIsError);

        // The shell-owned Warband Bar is the friendly roster in Deployment. Keeping the old
        // rail populated would duplicate controls and rebuild a second hero tree on every click.
        SetDisplayed(_rail, false);

        SetDisplayed(_enemyPanel, d.EnemyPreview.Count > 0);
        _enemyList.Clear();
        foreach (var line in d.EnemyPreview)
        {
            var l = MakeLabel("card-body");
            l.text = line;
            _enemyList.Add(l);
        }
        MechanicPresentation.BindInline(_enemyRule, d.EncounterRule ?? "");
        SetDisplayed(_enemyRule, !string.IsNullOrWhiteSpace(d.EncounterRule));
    }

    /// <summary>
    /// The rail is rebuilt wholesale: it is at most a handful of chips, and rebuilding keeps the
    /// click closures bound to the right index without a stale-capture class of bug.
    /// </summary>
    private void RebuildRail(List<HeroCardModel> roster)
    {
        _rail.Clear();
        var title = MakeLabel("section-heading");
        title.text = "YOUR WARBAND";
        _rail.Add(title);

        if (roster.Count == 0)
        {
            var empty = MakeLabel("empty-note");
            empty.text = "No one to deploy.";
            _rail.Add(empty);
            return;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            var hero = roster[i];
            int index = hero.Index;

            var chip = new VisualElement();
            chip.AddToClassList("card");
            chip.AddToClassList("deploy-chip");
            chip.EnableInClassList("card--selected", hero.Selected);
            // "Placed" reads as done, not disabled — it can still be picked up and moved.
            chip.EnableInClassList("deploy-chip--placed", !hero.Interactable);

            var name = MakeLabel("card-title");
            name.text = hero.Name;
            chip.Add(name);

            var role = MakeLabel("card-role");
            role.text = string.IsNullOrEmpty(hero.WeaponName) ? hero.Role : hero.WeaponName;
            chip.Add(role);

            if (hero.Stats.Count > 0)
            {
                var stats = new VisualElement();
                stats.AddToClassList("stat-row");
                foreach (var s in hero.Stats) stats.Add(MakeChip(s));
                chip.Add(stats);
            }

            chip.RegisterCallback<ClickEvent>(_ => _actions?.SelectForDeploy?.Invoke(index));
            _rail.Add(chip);
        }
    }

    private static VisualElement MakeChip(StatChipModel s)
    {
        var chip = new MechanicStatTile("stat-chip", "stat-chip");
        chip.Bind(s);
        return chip;
    }

    private static Label MakeLabel(string cls)
    {
        var l = new Label();
        l.AddToClassList(cls);
        return l;
    }

    private static void SetDisplayed(VisualElement e, bool shown) =>
        e.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
}
