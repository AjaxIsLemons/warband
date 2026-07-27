using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// The act map: where the run stands and what it is about to face. The WHOLE act track is on
/// screen from the first node — ADR 0016 / pve-encounters says the player is never surprised by
/// the route, and an enemy formation is previewed before deployment, never after.
///
/// Pure renderer per IRunScreenView: the tree is built once here, Bind re-renders it from
/// MapModel, and the view owns no run state and formats no content id.
/// </summary>
internal sealed class MapView : IRunScreenView
{
    private readonly RunShellActions _actions;

    private readonly VisualElement _root;
    private readonly Label _actLabel;
    private readonly Label _nodeLabel;
    private readonly Label _gold;
    private readonly VisualElement _track;
    private readonly Label _nodeKind;
    private readonly Label _nodeHeading;
    private readonly Label _nodeBlurb;
    private readonly VisualElement _tierRow;
    private readonly VisualElement _enemyPanel;
    private readonly VisualElement _enemyList;
    private readonly Label _encounterRule;
    private readonly Button _primary;
    private readonly VisualElement _warband;

    public RunScreen Screen => RunScreen.Map;

    public VisualElement Root => _root;

    public MapView(RunShellActions actions)
    {
        _actions = actions;

        _root = new VisualElement();
        _root.AddToClassList("shell-screen");
        _root.AddToClassList("shell-column");

        var topbar = new VisualElement();
        topbar.AddToClassList("topbar");
        _root.Add(topbar);

        _actLabel = MakeLabel("topbar__item");
        topbar.Add(_actLabel);
        _nodeLabel = MakeLabel("topbar__item");
        topbar.Add(_nodeLabel);
        _gold = MakeLabel("gold-pill");
        topbar.Add(_gold);

        // Track + warband can outgrow the panel. `shell-screen` is inset-0 absolute, so the root
        // has a definite height and this one structural inline style is what makes the middle
        // band take the leftover space and scroll instead of running off the bottom.
        var scroll = new ScrollView();
        scroll.style.flexGrow = 1f;
        _root.Add(scroll);

        var column = new VisualElement();
        column.AddToClassList("shell-column");
        scroll.Add(column);

        // MODEL GAP: MapModel carries no heading strings for the act track / warband sections, so
        // these two are static chrome rather than hydrated copy.
        column.Add(MakeLabel("section-heading", "THE ACT"));

        _track = new VisualElement();
        _track.AddToClassList("track");
        column.Add(_track);

        var nodePanel = new VisualElement();
        nodePanel.AddToClassList("panel");
        column.Add(nodePanel);

        _nodeKind = MakeLabel("eyebrow");
        nodePanel.Add(_nodeKind);
        _nodeHeading = MakeLabel("shell-title");
        nodePanel.Add(_nodeHeading);
        _nodeBlurb = MakeLabel("body-copy");
        nodePanel.Add(_nodeBlurb);

        _tierRow = new VisualElement();
        _tierRow.AddToClassList("tier-row");
        nodePanel.Add(_tierRow);

        _enemyPanel = new VisualElement();
        _enemyPanel.AddToClassList("panel");
        column.Add(_enemyPanel);

        var enemyHeader = new VisualElement();
        enemyHeader.AddToClassList("stat-row");
        _enemyPanel.Add(enemyHeader);

        // MODEL GAP: no model string names the enemy panel; the pill text is the fixed promise
        // that formations are public information.
        var enemyEyebrow = MakeLabel("eyebrow", "ENEMY FORMATION");
        enemyEyebrow.AddToClassList("eyebrow--danger");
        enemyHeader.Add(enemyEyebrow);
        enemyHeader.Add(MakeLabel("public-info-pill", "FULL INFO"));

        _enemyList = new VisualElement();
        _enemyList.AddToClassList("card-grid");
        _enemyPanel.Add(_enemyList);

        _encounterRule = MakeLabel("body-copy");
        _enemyPanel.Add(_encounterRule);

        column.Add(MakeLabel("section-heading", "YOUR WARBAND"));

        _warband = new VisualElement();
        _warband.AddToClassList("card-grid");
        column.Add(_warband);

        // The one commitment on this screen stays pinned outside the scroll.
        var footer = new VisualElement();
        footer.AddToClassList("topbar");
        _root.Add(footer);

        _primary = new Button(() => _actions.Advance?.Invoke());
        _primary.AddToClassList("btn");
        _primary.AddToClassList("btn--primary");
        footer.Add(_primary);
    }

    public void Bind(RunShellModel model)
    {
        MapModel map = model.Map;

        _actLabel.text = map.ActLabel;
        _nodeLabel.text = map.NodeLabel;
        _gold.text = map.Gold;

        _nodeKind.text = map.NodeKind;
        SetDisplayed(_nodeKind, !string.IsNullOrEmpty(map.NodeKind));
        _nodeHeading.text = map.NodeHeading;
        _nodeBlurb.text = map.NodeBlurb;
        SetDisplayed(_nodeBlurb, !string.IsNullOrEmpty(map.NodeBlurb));

        _primary.text = map.PrimaryText;
        _primary.SetEnabled(map.PrimaryEnabled);

        RebuildTrack(map.Track);
        RebuildTiers(map);
        RebuildEnemyPreview(map);

        _warband.Clear();
        foreach (var hero in map.Warband)
            _warband.Add(BuildHeroCard(hero));
    }

    private void RebuildTrack(List<MapNodeModel> track)
    {
        _track.Clear();
        foreach (var node in track)
        {
            var entry = new VisualElement();
            entry.AddToClassList("track-node");
            entry.EnableInClassList("track-node--current", node.IsCurrent);
            entry.EnableInClassList("track-node--past", node.IsPast);
            // MODEL GAP: MapNodeModel has no IsBoss flag, so the boss modifier reads the display
            // Kind. A flag on the model would be sturdier than this string compare.
            entry.EnableInClassList("track-node--boss", node.Kind.ToUpperInvariant() == "BOSS");

            entry.Add(MakeLabel("card-role", node.Kind));
            entry.Add(MakeLabel("card-title", node.Label));
            _track.Add(entry);
        }
    }

    private void RebuildTiers(MapModel map)
    {
        SetDisplayed(_tierRow, map.ShowTiers);
        _tierRow.Clear();
        if (!map.ShowTiers) return;

        foreach (var tier in map.Tiers)
        {
            int index = tier.Index;
            var card = new Button(() => _actions.ChooseTier?.Invoke(index));
            card.AddToClassList("tier-card");
            card.EnableInClassList("tier-card--selected", tier.Selected);

            card.Add(MakeLabel("card-title", tier.Name));
            card.Add(MakeLabel("card-role", tier.Risk));
            card.Add(MakeLabel("card-body", tier.Reward));
            _tierRow.Add(card);
        }
    }

    private void RebuildEnemyPreview(MapModel map)
    {
        SetDisplayed(_enemyPanel, map.EnemyPreview.Count > 0);

        _enemyList.Clear();
        foreach (var enemy in map.EnemyPreview)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.AddToClassList("card--disabled");
            card.Add(MakeLabel("card-title", enemy));
            _enemyList.Add(card);
        }

        MechanicPresentation.BindInline(_encounterRule, map.EncounterRule);
        SetDisplayed(_encounterRule, !string.IsNullOrEmpty(map.EncounterRule));
    }

    /// <summary>Read-only on the map: the run offers no roster intent here, so every card is
    /// inert by construction rather than by the model's Interactable flag.</summary>
    private static VisualElement BuildHeroCard(HeroCardModel hero)
    {
        var card = new VisualElement();
        card.AddToClassList("card");
        card.AddToClassList("card--disabled");

        if (!string.IsNullOrEmpty(hero.RankLabel))
            card.Add(MakeLabel("eyebrow", hero.RankLabel));
        card.Add(MakeLabel("card-title", hero.Name));
        card.Add(MakeLabel("card-role", hero.Role));
        if (!string.IsNullOrEmpty(hero.WeaponName))
            card.Add(MakeLabel("card-body", hero.WeaponName));

        if (hero.Stats.Count > 0)
        {
            var stats = new VisualElement();
            stats.AddToClassList("stat-row");
            foreach (var stat in hero.Stats)
                stats.Add(BuildStatChip(stat));
            card.Add(stats);
        }

        if (hero.Traits.Count > 0)
        {
            var traits = new VisualElement();
            traits.AddToClassList("trait-row");
            foreach (var trait in hero.Traits)
                traits.Add(MakeLabel("trait-pill", trait));
            card.Add(traits);
        }

        return card;
    }

    private static VisualElement BuildStatChip(StatChipModel stat)
    {
        var chip = new MechanicStatTile("stat-chip", "stat-chip");
        chip.Bind(stat);
        return chip;
    }

    private static Label MakeLabel(string className, string text = "")
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    private static void SetDisplayed(VisualElement element, bool displayed)
    {
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
