using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Sim;

/// <summary>
/// THE COMBAT RECAP (roadmap item 1c) — the three charts on the result gate: per-hero
/// contribution, the damage composition, and the death timeline.
///
/// This class draws and computes NOTHING. Every number, share, bar width and marker position
/// arrives already folded in <see cref="CombatRecap"/>, which is headlessly tested; a chart's
/// bugs are arithmetic bugs, and arithmetic does not belong in a Unity panel. If a value looks
/// wrong on screen, the fix is in the sim's fold and there is a test for it.
/// </summary>
internal sealed class CombatRecapPanel
{
    private readonly VisualElement _rows;
    private readonly VisualElement _compBar;
    private readonly VisualElement _compLegend;
    private readonly VisualElement _track;
    private readonly Label _axisStart;
    private readonly Label _axisWaning;
    private readonly Label _axisEnd;
    private readonly Label _empty;

    private readonly List<VisualElement> _rowPool = new List<VisualElement>();
    private readonly List<VisualElement> _segPool = new List<VisualElement>();
    private readonly List<VisualElement> _keyPool = new List<VisualElement>();
    private readonly List<VisualElement> _markPool = new List<VisualElement>();
    private readonly VisualElement _waningLine;

    private readonly VisualElement _contribution;
    private readonly VisualElement _composition;
    private readonly VisualElement _timeline;

    public VisualElement Root { get; }

    public CombatRecapPanel()
    {
        Root = new VisualElement { name = "combat-recap" };
        Root.AddToClassList("recap");

        _contribution = Section("CONTRIBUTION");
        _rows = new VisualElement();
        _contribution.Add(_rows);
        _empty = Text("recap__empty");
        _empty.text = "No damage was dealt.";
        _contribution.Add(_empty);
        Root.Add(_contribution);

        _composition = Section("DAMAGE COMPOSITION");
        _compBar = new VisualElement();
        _compBar.AddToClassList("recap-comp");
        _composition.Add(_compBar);
        _compLegend = new VisualElement();
        _compLegend.AddToClassList("recap-comp__legend");
        _composition.Add(_compLegend);
        Root.Add(_composition);

        _timeline = Section("TIMELINE");
        _track = new VisualElement();
        _track.AddToClassList("recap-track");
        var mid = new VisualElement();
        mid.AddToClassList("recap-track__mid");
        _track.Add(mid);
        _waningLine = new VisualElement();
        _waningLine.AddToClassList("recap-track__waning");
        _track.Add(_waningLine);
        _timeline.Add(_track);

        var axis = new VisualElement();
        axis.AddToClassList("recap-track__axis");
        _axisStart = Text("recap-axis__label");
        _axisWaning = Text("recap-axis__waning");
        _axisEnd = Text("recap-axis__label");
        axis.Add(_axisStart);
        axis.Add(_axisWaning);
        axis.Add(_axisEnd);
        _timeline.Add(axis);
        Root.Add(_timeline);
    }

    public void Bind(CombatRecap recap)
    {
        Root.style.display = recap == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (recap == null) return;

        SyncRows(recap);
        SyncComposition(recap);
        SyncTimeline(recap);
    }

    private void SyncRows(CombatRecap recap)
    {
        var models = recap.Rows;
        while (_rowPool.Count > models.Count)
        {
            int last = _rowPool.Count - 1;
            _rowPool[last].RemoveFromHierarchy();
            _rowPool.RemoveAt(last);
        }
        while (_rowPool.Count < models.Count)
        {
            var row = new VisualElement();
            row.AddToClassList("recap-row");
            row.Add(Text("recap-row__name"));
            row.Add(Text("recap-row__dagger"));
            var track = new VisualElement();
            track.AddToClassList("recap-row__track");
            var fill = new VisualElement();
            fill.AddToClassList("recap-row__fill");
            track.Add(fill);
            row.Add(track);
            row.Add(Text("recap-row__value"));
            row.Add(Text("recap-row__sub"));
            _rowPool.Add(row);
            _rows.Add(row);
        }

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            var row = _rowPool[i];
            row.EnableInClassList("recap-row--dead", model.Died);
            ((Label)row.ElementAt(0)).text = model.Name;
            ((Label)row.ElementAt(1)).text = model.Died ? "†" : "";
            row.ElementAt(2).ElementAt(0).style.width = Percent(model.BarFill * 100.0);
            ((Label)row.ElementAt(3)).text = $"{model.Damage} · {model.PctOfTeam:0}%";
            ((Label)row.ElementAt(4)).text = Secondary(model);
            row.tooltip = Tooltip(model);
        }

        // A support hero deals no damage at all, so "no damage was dealt" must mean the TEAM
        // dealt none — otherwise a cleric's fight reads as an empty chart.
        _empty.style.display = recap.CompositionTotal > 0 ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>The one secondary fact worth the row's remaining width. Healing leads: a cleric's
    /// entire contribution is invisible in a damage chart, and showing it here is the difference
    /// between "did nothing" and "kept everyone alive".</summary>
    private static string Secondary(RecapRow row)
    {
        if (row.Healing > 0) return $"{row.Healing} healed";
        if (row.Absorbed > 0) return $"{row.Absorbed} absorbed";
        if (row.Kills > 0) return row.Kills == 1 ? "1 kill" : $"{row.Kills} kills";
        return "";
    }

    private static string Tooltip(RecapRow row)
    {
        string died = row.Died ? $"fell at {row.DeathTick / 10f:0.0}s" : "survived";
        return $"{row.Name} · {row.Damage} dealt ({row.PctOfTeam:0}% of the team) · " +
               $"{row.Taken} taken · {row.Absorbed} absorbed · {row.Healing} healed · " +
               $"{row.Kills} kills · {died}";
    }

    private void SyncComposition(CombatRecap recap)
    {
        var models = recap.Composition;
        _composition.style.display = models.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (models.Count == 0) return;

        while (_segPool.Count > models.Count)
        {
            int last = _segPool.Count - 1;
            _segPool[last].RemoveFromHierarchy();
            _segPool.RemoveAt(last);
            _keyPool[last].RemoveFromHierarchy();
            _keyPool.RemoveAt(last);
        }
        while (_segPool.Count < models.Count)
        {
            var seg = new VisualElement();
            seg.AddToClassList("recap-comp__seg");
            _segPool.Add(seg);
            _compBar.Add(seg);

            var key = new VisualElement();
            key.AddToClassList("recap-key");
            var swatch = new VisualElement();
            swatch.AddToClassList("recap-key__swatch");
            key.Add(swatch);
            key.Add(Text("recap-key__label"));
            _keyPool.Add(key);
            _compLegend.Add(key);
        }

        for (int i = 0; i < models.Count; i++)
        {
            var model = models[i];
            string tint = CauseClass(model.Cause);

            // flex-grow BY AMOUNT: the bar fills its width exactly and needs no percentage
            // arithmetic on this side — the segments are the shares.
            var seg = _segPool[i];
            seg.style.flexGrow = model.Amount;
            Tint(seg, tint);
            seg.tooltip = $"{model.Name} · {model.Amount} damage · {model.Pct:0.#}%";

            var key = _keyPool[i];
            Tint(key.ElementAt(0), tint);
            ((Label)key.ElementAt(1)).text = $"{model.Name} {model.Pct:0}%";
        }
    }

    private void SyncTimeline(CombatRecap recap)
    {
        var models = recap.Beats;
        while (_markPool.Count > models.Count)
        {
            int last = _markPool.Count - 1;
            _markPool[last].RemoveFromHierarchy();
            _markPool.RemoveAt(last);
        }
        while (_markPool.Count < models.Count)
        {
            var mark = new VisualElement();
            mark.AddToClassList("recap-mark");
            _markPool.Add(mark);
            _track.Add(mark);
        }

        for (int i = 0; i < models.Count; i++)
        {
            var beat = models[i];
            var mark = _markPool[i];
            mark.EnableInClassList("recap-mark--ours", beat.Friendly);
            mark.EnableInClassList("recap-mark--theirs", !beat.Friendly);
            mark.style.left = Percent(Track(beat.At));
            mark.tooltip = $"{beat.Victim} fell to {beat.Killer} · {beat.Cause} · " +
                           $"{beat.Tick / 10f:0.0}s" +
                           (beat.Overkill > 0 ? $" · {beat.Overkill} overkill" : "");
        }

        _waningLine.style.display = recap.ReachedWaning ? DisplayStyle.Flex : DisplayStyle.None;
        if (recap.ReachedWaning) _waningLine.style.left = Percent(Track(recap.WaningAt));
        _axisWaning.style.display = recap.ReachedWaning ? DisplayStyle.Flex : DisplayStyle.None;
        _axisWaning.text = $"THE WANING · {Battle.OvertimeStartTick / 10f:0}s";

        _axisStart.text = "0s";
        _axisEnd.text = $"{recap.Seconds:0.0}s";
        _timeline.style.display = models.Count > 0 || recap.ReachedWaning
            ? DisplayStyle.Flex
            : DisplayStyle.None;
    }

    /// <summary>Markers are 3px wide and placed from their left edge, so a beat at the very end
    /// of the fight would hang past the track. Clamped to keep the mark inside its own clock.</summary>
    private static double Track(double at) => Mathf.Clamp((float)at * 100f, 0f, 99f);

    private static void Tint(VisualElement element, string tint)
    {
        foreach (string cause in CauseClasses)
            element.EnableInClassList(cause, cause == tint);
    }

    private static readonly string[] CauseClasses =
    {
        "cause--attack", "cause--ability", "cause--dot", "cause--burn",
        "cause--field", "cause--trigger", "cause--counter", "cause--storm",
    };

    private static string CauseClass(Cause cause) =>
        cause switch
        {
            Cause.Attack => "cause--attack",
            Cause.Ability => "cause--ability",
            Cause.Dot => "cause--dot",
            Cause.Burn => "cause--burn",
            Cause.Field => "cause--field",
            Cause.Trigger => "cause--trigger",
            Cause.Counter => "cause--counter",
            Cause.Storm => "cause--storm",
            _ => "",              // Cause.None keeps the stylesheet's neutral default
        };

    private static StyleLength Percent(double value) =>
        new StyleLength(new Length((float)value, LengthUnit.Percent));

    private VisualElement Section(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("recap__section");
        var label = Text("recap__title");
        label.text = title;
        section.Add(label);
        return section;
    }

    private static Label Text(string className)
    {
        var label = new Label();
        label.AddToClassList(className);
        return label;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// The UI QA matrix's recap. Deliberately NOT a nominal fight — the automated gate is
    /// UiLayoutContract, and a tidy fixture would pass it while the real thing clipped. So this
    /// is the worst plausible case measured off a real act-3 boss fight: a support hero whose
    /// only contribution is a four-digit heal, a name long enough to need its ellipsis, a fallen
    /// hero, five composition slices (including the Counter and Trigger the balance harness
    /// never names), deaths clustered tightly enough to overlap, and the Waning on the track.
    /// </summary>
    public static CombatRecap EditorFixture()
    {
        var recap = new CombatRecap
        {
            Team = 0,
            Winner = Winner.Team0,
            Victory = true,
            EndTick = 1022,
            Seconds = 102.2,
            Survivors = 3,
            Losses = 1,
            HealingDone = 2093,
            ShieldAbsorbed = 168,
            CompositionTotal = 1461,
            WaningAt = Battle.OvertimeStartTick / 1022.0,
        };

        recap.Rows.Add(Row(0, "Berserker of the Vigil", "berserker", 865, 59.2, 1.0,
            healing: 416, kills: 0));
        recap.Rows.Add(Row(1, "Pyromancer", "pyromancer", 347, 23.8, 347.0 / 865, kills: 4));
        recap.Rows.Add(Row(2, "Bulwark", "bulwark", 149, 10.2, 149.0 / 865, absorbed: 168));
        recap.Rows.Add(Row(3, "Shade", "shade", 100, 6.8, 100.0 / 865, kills: 1,
            died: true, deathTick: 604));
        recap.Rows.Add(Row(4, "Cleric", "cleric", 0, 0, 0, healing: 2093));

        Seg(recap, Cause.Attack, 827);
        Seg(recap, Cause.Ability, 314);
        Seg(recap, Cause.Burn, 190);
        Seg(recap, Cause.Counter, 82);
        Seg(recap, Cause.Trigger, 48);

        Beat(recap, 180, "Ash Warden", "Berserker of the Vigil", "Attack", friendly: false);
        Beat(recap, 195, "Dune Reaver", "Pyromancer", "Burn", friendly: false);
        Beat(recap, 210, "Glass Seer", "Pyromancer", "Burn", friendly: false);
        Beat(recap, 604, "Shade", "Ash Warden", "Ability", friendly: true, overkill: 31);
        Beat(recap, 970, "Sand Choir", "Storm", "Storm", friendly: false);
        return recap;
    }

    private static RecapRow Row(int id, string name, string chassis, int damage, double pct,
                                double fill, int healing = 0, int absorbed = 0, int kills = 0,
                                bool died = false, int deathTick = -1) =>
        new RecapRow
        {
            UnitId = id, Name = name, ChassisId = chassis, Damage = damage,
            PctOfTeam = pct, BarFill = fill, Healing = healing, Absorbed = absorbed,
            Kills = kills, Died = died, DeathTick = deathTick,
            Taken = died ? 640 : 210,
        };

    private static void Seg(CombatRecap recap, Cause cause, int amount) =>
        recap.Composition.Add(new RecapSegment
        {
            Cause = cause,
            Name = Lexicon.Of(cause).Name,
            Amount = amount,
            Pct = 100.0 * amount / recap.CompositionTotal,
        });

    private static void Beat(CombatRecap recap, int tick, string victim, string killer,
                             string cause, bool friendly, int overkill = 0) =>
        recap.Beats.Add(new RecapBeat
        {
            Tick = tick, At = tick / 1022.0, Victim = victim, Killer = killer,
            Cause = cause, Friendly = friendly, Overkill = overkill,
        });
#endif
}
