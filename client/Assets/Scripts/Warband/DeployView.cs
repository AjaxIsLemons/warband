using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Deployment. The BOARD is the screen here — this view is deliberately a thin frame around it:
/// a roster rail on the left, the enemy preview on the right, an instruction, and a commit bar.
/// Everything in the middle stays clear so the player can actually see the hexes they are
/// clicking.
///
/// Interaction supports both deliberate click/place and direct manipulation. Pointer capture
/// keeps a dragged champion attached across the clear board surface; the controller decides when
/// movement crosses the drag threshold and which highlighted hex is legal.
/// </summary>
internal sealed class DeployView : IRunScreenView
{
    private readonly RunShellActions _actions;
    private readonly VisualElement _root;
    private readonly Label _heading;
    private readonly Label _instruction;
    private readonly Label _progress;
    private readonly VisualElement _rail;
    private readonly VisualElement _enemyPanel;
    private readonly Label _enemyRule;
    private readonly Label _enemyRuleName;
    private readonly VisualElement _enemyCard;
    private readonly InspectorPanel _enemyInspector;
    private readonly VisualElement _enemyList;
    private readonly Button _commit;
    private readonly Button _clear;
    private int _boardPointerId = -1;

    public RunScreen Screen => RunScreen.Deploy;
    public VisualElement Root => _root;

    public DeployView(RunShellActions actions, RuntimeTooltipService tooltips = null)
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
            if (evt.button != 0 || _boardPointerId >= 0) return;
            _boardPointerId = evt.pointerId;
            boardSurface.CapturePointer(evt.pointerId);
            _actions?.BoardPointerDown?.Invoke(
                new Vector2(evt.position.x, evt.position.y), evt.pointerId);
            evt.StopPropagation();
        });
        boardSurface.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (evt.pointerId != _boardPointerId) return;
            _actions?.BoardPointerMoved?.Invoke(
                new Vector2(evt.position.x, evt.position.y), evt.pointerId);
            evt.StopPropagation();
        });
        boardSurface.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.pointerId != _boardPointerId) return;
            if (boardSurface.HasPointerCapture(evt.pointerId))
                boardSurface.ReleasePointer(evt.pointerId);
            _boardPointerId = -1;
            _actions?.BoardPointerUp?.Invoke(
                new Vector2(evt.position.x, evt.position.y), evt.pointerId);
            evt.StopPropagation();
        });
        boardSurface.RegisterCallback<PointerCancelEvent>(evt =>
        {
            if (evt.pointerId != _boardPointerId) return;
            if (boardSurface.HasPointerCapture(evt.pointerId))
                boardSurface.ReleasePointer(evt.pointerId);
            _boardPointerId = -1;
            _actions?.BoardPointerCanceled?.Invoke(evt.pointerId);
            evt.StopPropagation();
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
        // The rule leads with its AUTHORED NAME, which is the thing the player will recognise
        // again next act. The old trailing sentence ("Formation and rules are final...") described
        // the disclosure contract rather than this encounter, and the FULL INFO pill labelled the
        // contract too — both deleted.
        _enemyRuleName = MakeLabel("card-role");
        _enemyPanel.Add(_enemyRuleName);
        _enemyRule = MakeLabel("body-copy");
        _enemyPanel.Add(_enemyRule);
        _enemyList = new VisualElement();
        _enemyList.AddToClassList("deploy-enemy__list");
        _enemyPanel.Add(_enemyList);
        _root.Add(_enemyPanel);

        // The SAME unit card the fight uses — one component, two screens. It floats over the
        // deployment board rather than docking, and it never scrims: placement is a board
        // interaction and the board has to stay clickable underneath.
        _enemyCard = new VisualElement { name = "deploy-enemy-card" };
        _enemyCard.AddToClassList("fight-card");
        _enemyCard.AddToClassList("deploy-enemy-card");
        _enemyCard.pickingMode = PickingMode.Position;
        var closeCard = new Button(() => _actions?.SelectDeployEnemy?.Invoke("")) { text = "×" };
        closeCard.AddToClassList("btn");
        closeCard.AddToClassList("btn--ghost");
        closeCard.AddToClassList("fight-card__close");
        _enemyInspector = new InspectorPanel(_ => { }, null, tooltips);
        _enemyInspector.Root.AddToClassList("wb-inspector--combat");
        _enemyCard.Add(_enemyInspector.Root);
        _enemyCard.Add(closeCard);
        closeCard.BringToFront();
        SetDisplayed(_enemyCard, false);
        _root.Add(_enemyCard);

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

        // The shell-owned Warband Bar is the friendly roster in Deployment. Keeping the old
        // rail populated would duplicate controls and rebuild a second hero tree on every click.
        SetDisplayed(_rail, false);

        SetDisplayed(_enemyPanel, d.Enemies.Count > 0);
        _enemyRuleName.text = (d.EncounterRuleName ?? "").ToUpperInvariant();
        SetDisplayed(_enemyRuleName, !string.IsNullOrWhiteSpace(d.EncounterRuleName));
        MechanicPresentation.BindInline(_enemyRule, d.EncounterRule ?? "");
        SetDisplayed(_enemyRule, !string.IsNullOrWhiteSpace(d.EncounterRule));

        _enemyList.Clear();
        foreach (var enemy in d.Enemies)
            _enemyList.Add(BuildEnemyRow(enemy));

        SetDisplayed(_enemyCard, d.SelectedEnemy != null);
        if (d.SelectedEnemy != null) _enemyInspector.Bind(d.SelectedEnemy);
    }

    /// <summary>
    /// One enemy as a selectable row: name + role/row eyebrow on the left, the two facts that
    /// decide placement (HP and reach) on the right. Selecting it opens the shared unit card —
    /// three full cards side by side would eat the board during the one phase that needs it.
    /// </summary>
    private VisualElement BuildEnemyRow(DeployEnemyRowModel enemy)
    {
        string key = enemy.Key;
        var row = new Button(() => _actions?.SelectDeployEnemy?.Invoke(key));
        row.AddToClassList("deploy-enemy-row");
        row.EnableInClassList("deploy-enemy-row--selected", enemy.Selected);

        var copy = new VisualElement();
        copy.AddToClassList("deploy-enemy-row__copy");
        copy.pickingMode = PickingMode.Ignore;
        var eyebrow = MakeLabel("deploy-enemy-row__meta");
        eyebrow.text = string.IsNullOrEmpty(enemy.Role)
            ? $"ROW {enemy.Row + 1}"
            : $"{enemy.Role.ToUpperInvariant()} · ROW {enemy.Row + 1}";
        var name = MakeLabel("deploy-enemy-row__name");
        name.text = enemy.Name;
        copy.Add(eyebrow);
        copy.Add(name);
        row.Add(copy);

        var facts = new VisualElement();
        facts.AddToClassList("deploy-enemy-row__facts");
        facts.pickingMode = PickingMode.Ignore;
        facts.Add(MakeChip(new StatChipModel("HP", enemy.MaxHp.ToString(), "",
            PresentationFactId.Hp)));
        facts.Add(MakeChip(new StatChipModel("REACH", enemy.Range.ToString(), "",
            PresentationFactId.Reach)));
        row.Add(facts);
        return row;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string EditorResolvedLayoutReport(VisualElement permanentRail = null)
    {
        var report = new UiLayoutReport("Deploy");
        VisualElement header = _root.Q<VisualElement>(className: "deploy-header");
        VisualElement bar = _root.Q<VisualElement>(className: "deploy-bar");
        UiLayoutContract.RequireResolved(report, _root, "root");
        UiLayoutContract.RequireInside(report, header, _root, "deployment guidance");
        UiLayoutContract.RequireInside(report, _enemyPanel, _root, "enemy preview");
        UiLayoutContract.RequireInside(report, bar, _root, "deployment commands");
        if (permanentRail != null)
        {
            UiLayoutContract.RequireNoOverlap(
                report, header, permanentRail, "deployment guidance / permanent rail");
            UiLayoutContract.RequireNoOverlap(
                report, _enemyPanel, permanentRail, "enemy preview / permanent rail");
            UiLayoutContract.RequireNoOverlap(
                report, bar, permanentRail, "deployment commands / permanent rail");
        }
        UiLayoutContract.RequireNoScrollView(report, _root, "Deploy");
        UiLayoutContract.RequireMinimumHeight(report, _root, "btn", 44f);
        UiLayoutContract.RequireMinimumFont(report, _root, "body-copy", 16f);
        UiLayoutContract.RequireMinimumFont(report, _root, "card-body", 16f);
        UiLayoutContract.RequireMinimumRenderedFont(
            report, _root, "body-copy", 12.5f);
        UiLayoutContract.RequireWrappedTextFits(report, _root, "body-copy");
        UiLayoutContract.RequireWrappedTextFits(report, _root, "card-body");
        return report.ToString();
    }
#endif

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
