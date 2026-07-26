using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Warband.Run;

/// <summary>
/// Stable geography for the between-fight Hourstone Table. Values are deliberately presentation
/// concepts rather than run phases: a station can be available, recommended, or quiet regardless
/// of which command most recently changed the run.
/// </summary>
internal enum HallStation
{
    Overview,
    Market,
    Warband,
    Armory,
    Hourstone,
    Breach,
}

internal enum HubEventKind
{
    RunEnded,
    SpecializationRequired,
    BossRewardRequired,
    ActAdvanced,
    HeroAcquired,
    EquipmentAcquired,
    InscriptionAcquired,
    SandChanged,
    MarketRefreshed,
}

internal sealed class HubEvent
{
    public HubEventKind Kind;
    public int Amount;
}

internal sealed class HubEventBatch
{
    public readonly List<HubEvent> Events = new List<HubEvent>();
}

internal sealed class HubStep
{
    public HubEventKind Kind;
    public HallStation Station;
    public bool Blocking;
}

internal sealed class HubSequencePlan
{
    public readonly List<HubStep> Steps = new List<HubStep>();
    public HallStation RecommendedStation = HallStation.Market;
    public bool Terminal;
}

/// <summary>
/// Small immutable-enough receipt around a run command. It intentionally captures only data that
/// affects presentation routing; the authoritative state remains RunController.State.
/// </summary>
internal sealed class RunMutationSnapshot
{
    public int Act;
    public int Node;
    public int Sand;
    public int Field;
    public int Bench;
    public int Inventory;
    public int Inscriptions;
    public int LiveMarketOffers;
    public int ShopRolls;
    public bool PendingSpec;
    public bool PendingBossReward;
    public bool Over;

    public static RunMutationSnapshot Capture(RunState state)
    {
        int offers = 0;
        foreach (var offer in state.ShopOffers)
            if (offer != null) offers++;

        return new RunMutationSnapshot
        {
            Act = state.Act,
            Node = state.NodeIndex,
            Sand = state.Sand,
            Field = state.Field.Count,
            Bench = state.Bench.Count,
            Inventory = state.Inventory.Count,
            Inscriptions = state.Inscriptions.Count,
            LiveMarketOffers = offers,
            ShopRolls = state.ShopRolls,
            PendingSpec = state.PendingSpec != null,
            PendingBossReward = state.Phase == RunPhase.Reward,
            Over = state.Over,
        };
    }
}

/// <summary>
/// Pure priority law for presentation after a mutation. Required choices outrank acquisitions;
/// ordinary completed fights recommend the refreshed Market.
/// </summary>
internal static class HubFlowPlanner
{
    public static HubSequencePlan Plan(RunMutationSnapshot before, RunMutationSnapshot after)
    {
        var plan = new HubSequencePlan();
        if (after.Over)
        {
            plan.Terminal = true;
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.RunEnded,
                Station = HallStation.Overview,
                Blocking = true,
            });
            return plan;
        }

        if (!before.PendingSpec && after.PendingSpec)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.SpecializationRequired,
                Station = HallStation.Warband,
                Blocking = true,
            });
        if (!before.PendingBossReward && after.PendingBossReward)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.BossRewardRequired,
                Station = HallStation.Hourstone,
                Blocking = true,
            });
        if (after.Act > before.Act)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.ActAdvanced,
                Station = HallStation.Breach,
            });

        int heroes = after.Field + after.Bench - before.Field - before.Bench;
        if (heroes > 0)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.HeroAcquired,
                Station = HallStation.Warband,
            });
        if (after.Inventory > before.Inventory)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.EquipmentAcquired,
                Station = HallStation.Armory,
            });
        if (after.Inscriptions > before.Inscriptions)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.InscriptionAcquired,
                Station = HallStation.Hourstone,
            });
        if (after.Sand != before.Sand)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.SandChanged,
                Station = HallStation.Market,
            });
        if (after.ShopRolls != before.ShopRolls)
            plan.Steps.Add(new HubStep
            {
                Kind = HubEventKind.MarketRefreshed,
                Station = HallStation.Market,
            });

        HubStep blocking = plan.Steps.Find(step => step.Blocking);
        if (blocking != null)
            plan.RecommendedStation = blocking.Station;
        else if (after.Inventory > before.Inventory)
            plan.RecommendedStation = HallStation.Armory;
        else if (after.Inscriptions > before.Inscriptions)
            plan.RecommendedStation = HallStation.Hourstone;
        else if (heroes > 0)
            plan.RecommendedStation = HallStation.Warband;
        else
            plan.RecommendedStation = HallStation.Market;
        return plan;
    }
}

/// <summary>
/// Fast contract checks that run inside Unity without needing a test-only assembly around the
/// predefined Assembly-CSharp project. The editor verification hook calls this and treats any
/// thrown exception as a failed Edit Mode contract.
/// </summary>
internal static class HubFlowContract
{
    public static void Validate()
    {
        var baseline = Snapshot();

        var terminal = Snapshot();
        terminal.Over = true;
        HubSequencePlan terminalPlan = HubFlowPlanner.Plan(baseline, terminal);
        Require(terminalPlan.Terminal, "terminal mutation must produce a terminal plan");
        Require(terminalPlan.Steps.Count == 1 && terminalPlan.Steps[0].Blocking,
            "terminal result must be the only blocking presentation step");

        var spec = Snapshot();
        spec.PendingSpec = true;
        spec.Inventory = 1;
        HubSequencePlan specPlan = HubFlowPlanner.Plan(baseline, spec);
        Require(specPlan.RecommendedStation == HallStation.Warband,
            "specialization must outrank an equipment acquisition");

        var equipment = Snapshot();
        equipment.Inventory = 1;
        HubSequencePlan equipmentPlan = HubFlowPlanner.Plan(baseline, equipment);
        Require(equipmentPlan.RecommendedStation == HallStation.Armory,
            "new equipment must recommend the Armory");

        var fight = Snapshot();
        fight.Sand = 8;
        fight.ShopRolls = 2;
        HubSequencePlan fightPlan = HubFlowPlanner.Plan(baseline, fight);
        Require(fightPlan.RecommendedStation == HallStation.Market,
            "ordinary fight receipts must recommend the refreshed Market");
    }

    private static RunMutationSnapshot Snapshot() => new RunMutationSnapshot
    {
        Act = 1,
        Node = 0,
        Sand = 3,
        Field = 3,
        Bench = 0,
        Inventory = 0,
        Inscriptions = 0,
        LiveMarketOffers = 5,
        ShopRolls = 1,
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[HubFlowContract] " + message);
    }
}

/// <summary>Unread station state. Entering a station clears it; new receipts can mark it again.</summary>
internal sealed class HubAttentionModel
{
    private readonly HashSet<HallStation> _stations = new HashSet<HallStation>();

    public void Apply(HubSequencePlan plan)
    {
        foreach (var step in plan.Steps)
            if (step.Station != HallStation.Overview && step.Station != HallStation.Breach)
                _stations.Add(step.Station);
    }

    public void Clear(HallStation station) => _stations.Remove(station);
    public bool Has(HallStation station) => _stations.Contains(station);
    public void Reset() => _stations.Clear();
}

/// <summary>
/// Cancellation-aware presentation director. It owns no route truth: each call projects the route
/// the model already chose and invalidates delayed cleanup from the previous transition.
/// </summary>
internal sealed class HubFlowDirector
{
    private readonly VisualElement _overview;
    private readonly VisualElement _workspace;
    private readonly HubPresentationConfig _config;
    private IVisualElementScheduledItem _start;
    private IVisualElementScheduledItem _cleanup;
    private int _generation;

    public HubFlowDirector(VisualElement overview, VisualElement workspace,
                           HubPresentationConfig config)
    {
        _overview = overview;
        _workspace = workspace;
        _config = config;
    }

    public void Show(bool overview, HallStation station, bool reducedMotion)
    {
        _generation++;
        _start?.Pause();
        _start = null;
        _cleanup?.Pause();
        _cleanup = null;

        _overview.style.display = overview ? DisplayStyle.Flex : DisplayStyle.None;
        _workspace.style.display = overview ? DisplayStyle.None : DisplayStyle.Flex;
        foreach (string direction in new[] { "left", "right", "up", "down", "fade" })
            _workspace.RemoveFromClassList("hub-enter--" + direction);
        ClearInlineTransition(_workspace);
        if (overview) return;

        string enter = reducedMotion ? "fade" :
            station == HallStation.Market ? "left" :
            station == HallStation.Armory ? "right" :
            station == HallStation.Warband ? "down" :
            station == HallStation.Breach ? "up" : "fade";
        ConfigureTransition(_workspace, 0);
        _workspace.AddToClassList("hub-enter--" + enter);

        int generation = _generation;
        int duration = reducedMotion ? _config.reducedFadeMs : _config.route.durationMs;
        // Let the hidden/offset entrance pose resolve for one panel tick, then remove it so the
        // workspace animates immediately toward its authored resting state. The previous version
        // held that entrance pose for the full duration and only began moving afterward.
        _start = _workspace.schedule.Execute(() =>
        {
            if (generation != _generation) return;
            ConfigureTransition(_workspace, duration);
            _workspace.RemoveFromClassList("hub-enter--" + enter);
        });
        _start.ExecuteLater(16);

        _cleanup = _workspace.schedule.Execute(() =>
        {
            if (generation != _generation) return;
            ClearInlineTransition(_workspace);
        });
        _cleanup.ExecuteLater(duration + _config.route.settleMs + 16);
    }

    public void Cancel()
    {
        _generation++;
        _start?.Pause();
        _start = null;
        _cleanup?.Pause();
        _cleanup = null;
        ClearInlineTransition(_workspace);
    }

    private static void ConfigureTransition(VisualElement target, int durationMs)
    {
        var properties = new List<StylePropertyName>
        {
            new StylePropertyName("opacity"),
            new StylePropertyName("translate"),
        };
        var duration = new TimeValue(Mathf.Max(0, durationMs), TimeUnit.Millisecond);
        target.style.transitionProperty = properties;
        target.style.transitionDuration = new List<TimeValue> { duration, duration };
        target.style.transitionTimingFunction = new List<EasingFunction>
        {
            new EasingFunction(EasingMode.EaseOut),
            new EasingFunction(EasingMode.EaseOut),
        };
    }

    private static void ClearInlineTransition(VisualElement target)
    {
        target.style.transitionProperty = StyleKeyword.Null;
        target.style.transitionDuration = StyleKeyword.Null;
        target.style.transitionTimingFunction = StyleKeyword.Null;
    }
}

/// <summary>Nonpersistent hand-off for a future no-power run archive or account layer.</summary>
internal sealed class RunConclusionReceipt
{
    public bool Victory;
    public int ActReached;
    public int FightsCompleted;
    public int Sand;
    public int FieldedHeroes;
    public string FinalCause = "";
}
