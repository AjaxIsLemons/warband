using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Terminal screen for a finished run. Reads as a receipt: the outcome, one line of summary, the
/// run's numbers, and the warband that got there — read-only, no click targets, one way out.
///
/// Owns no state. Final cards are pooled and re-bound like the recruit offer, so a repeated Bind is
/// free. Cards drop the description on purpose: at the end of a run the build (rank, weapon, stats,
/// spec traits) is the story, not the pitch that was on the recruit card.
/// </summary>
internal sealed class RunOverView : IRunScreenView
{
    /// <summary>One survivor/casualty, compact and inert.</summary>
    private sealed class FinalCard
    {
        private readonly Label _name;
        private readonly Label _role;
        private readonly Label _detail;
        private readonly VisualElement _stats;
        private readonly VisualElement _traits;

        public readonly VisualElement Root;

        public FinalCard()
        {
            Root = new VisualElement();
            Root.AddToClassList("card");
            Root.AddToClassList("card--disabled");

            _name = new Label();
            _name.AddToClassList("card-title");
            Root.Add(_name);

            _role = new Label();
            _role.AddToClassList("card-role");
            Root.Add(_role);

            _detail = new Label();
            _detail.AddToClassList("card-body");
            Root.Add(_detail);

            _stats = new VisualElement();
            _stats.AddToClassList("stat-row");
            Root.Add(_stats);

            _traits = new VisualElement();
            _traits.AddToClassList("trait-row");
            Root.Add(_traits);
        }

        public void Bind(HeroCardModel model)
        {
            _name.text = model.Name;
            _role.text = model.Role.ToUpperInvariant();
            _detail.text = Join(model.RankLabel, model.WeaponName);

            SetDisplayed(_detail, !string.IsNullOrEmpty(_detail.text));

            SyncChips(_stats, model.Stats);
            SyncPills(_traits, model.Traits);
            SetDisplayed(_traits, model.Traits.Count > 0);
        }
    }

    private readonly RunShellActions _actions;
    private readonly List<FinalCard> _cards = new List<FinalCard>();

    private readonly VisualElement _root;
    private readonly Label _heading;
    private readonly Label _summary;
    private readonly VisualElement _stats;
    private readonly Label _warbandHeading;
    private readonly VisualElement _grid;

    public RunScreen Screen => RunScreen.RunOver;

    public VisualElement Root => _root;

    public RunOverView(RunShellActions actions)
    {
        _actions = actions;

        _root = new VisualElement();
        _root.AddToClassList("shell-screen");
        _root.AddToClassList("shell-center");

        var column = new VisualElement();
        column.AddToClassList("shell-column");
        column.AddToClassList("panel");
        _root.Add(column);

        _heading = new Label();
        _heading.AddToClassList("outcome-heading");
        column.Add(_heading);

        _summary = new Label();
        _summary.AddToClassList("body-copy");
        column.Add(_summary);

        _stats = new VisualElement();
        _stats.AddToClassList("stat-row");
        column.Add(_stats);

        _warbandHeading = new Label("FINAL WARBAND");
        _warbandHeading.AddToClassList("section-heading");
        column.Add(_warbandHeading);

        _grid = new VisualElement();
        _grid.AddToClassList("card-grid");
        column.Add(_grid);

        // MODEL GAP: ADR 0016 promises an optional endless continuation after a victory, but the
        // terminal surface only offers BackToMenu — there is no action or model flag to hang a
        // "keep going" affordance on, so this screen ends the run outright.
        var back = new Button(() => _actions?.BackToMenu?.Invoke()) { text = "BACK TO MENU" };
        back.AddToClassList("btn");
        back.AddToClassList("btn--primary");
        column.Add(back);
    }

    public void Bind(RunShellModel model)
    {
        RunOverModel over = model.RunOver;

        _heading.text = over.Heading;
        _heading.EnableInClassList("outcome--victory", over.Tone == RunOverTone.Victory);
        _heading.EnableInClassList("outcome--defeat", over.Tone == RunOverTone.Defeat);

        _summary.text = over.Summary;
        SetDisplayed(_summary, !string.IsNullOrEmpty(over.Summary));

        SyncChips(_stats, over.Stats);
        SetDisplayed(_stats, over.Stats.Count > 0);

        SyncCards(over.FinalWarband);
        SetDisplayed(_warbandHeading, over.FinalWarband.Count > 0);
        SetDisplayed(_grid, over.FinalWarband.Count > 0);
    }

    private void SyncCards(List<HeroCardModel> warband)
    {
        while (_cards.Count > warband.Count)
        {
            int last = _cards.Count - 1;
            _grid.Remove(_cards[last].Root);
            _cards.RemoveAt(last);
        }

        while (_cards.Count < warband.Count)
        {
            var card = new FinalCard();
            _cards.Add(card);
            _grid.Add(card.Root);
        }

        for (int i = 0; i < warband.Count; i++)
            _cards[i].Bind(warband[i]);
    }

    private static string Join(string left, string right)
    {
        if (string.IsNullOrEmpty(left)) return right;
        if (string.IsNullOrEmpty(right)) return left;
        return $"{left}  •  {right}";
    }

    private static void SyncChips(VisualElement row, List<StatChipModel> chips)
    {
        while (row.childCount > chips.Count)
            row.RemoveAt(row.childCount - 1);

        while (row.childCount < chips.Count)
        {
            var chip = new VisualElement();
            chip.AddToClassList("stat-chip");
            var label = new Label();
            label.AddToClassList("stat-chip__label");
            chip.Add(label);
            var value = new Label();
            value.AddToClassList("stat-chip__value");
            chip.Add(value);
            row.Add(chip);
        }

        for (int i = 0; i < chips.Count; i++)
        {
            VisualElement chip = row.ElementAt(i);
            ((Label)chip.ElementAt(0)).text = chips[i].Label;
            ((Label)chip.ElementAt(1)).text = chips[i].Value;
            chip.EnableInClassList("stat-chip--good", chips[i].Tone == "good");
            chip.EnableInClassList("stat-chip--bad", chips[i].Tone == "bad");
            chip.EnableInClassList("stat-chip--warn", chips[i].Tone == "warn");
        }
    }

    private static void SyncPills(VisualElement row, List<string> traits)
    {
        while (row.childCount > traits.Count)
            row.RemoveAt(row.childCount - 1);

        while (row.childCount < traits.Count)
        {
            var pill = new Label();
            pill.AddToClassList("trait-pill");
            row.Add(pill);
        }

        for (int i = 0; i < traits.Count; i++)
            ((Label)row.ElementAt(i)).text = traits[i];
    }

    private static void SetDisplayed(VisualElement element, bool displayed)
    {
        element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
