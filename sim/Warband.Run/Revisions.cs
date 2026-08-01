using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Sim;

namespace Warband.Run
{
    public sealed class RevisionUpgradeDef
    {
        public string Id = "";
        public string Name = "";
        public string Summary = "";
        public int Tier;
        public RevisionModifier Modifier;
    }

    public sealed class RevisionDef
    {
        public string Id = "";
        public string Name = "";
        public string Summary = "";
        public RevisionEffectKind Effect;
        public List<RevisionUpgradeDef[]> Tiers = new List<RevisionUpgradeDef[]>();
    }

    public sealed class RevisionState
    {
        public string RevisionId = RevisionCatalog.BorrowedFutureId;
        public List<string> UpgradeIds = new List<string>();
    }

    /// <summary>Two deliberately authored lineages; no currency, random pool or meta layer.</summary>
    public static class RevisionCatalog
    {
        public const string BorrowedFutureId = "revision.borrowed_future";
        public const string RecallToFormationId = "revision.recall_to_formation";

        private static RevisionUpgradeDef Upgrade(
            string id, string name, string summary, int tier, RevisionModifier modifier) =>
            new RevisionUpgradeDef
            {
                Id = id,
                Name = name,
                Summary = summary,
                Tier = tier,
                Modifier = modifier,
            };

        private static readonly RevisionDef Borrowed = new RevisionDef
        {
            Id = BorrowedFutureId,
            Name = "Borrowed Future",
            Summary = "Carry one champion's future Mana into an earlier hour.",
            Effect = RevisionEffectKind.BorrowedFuture,
            Tiers =
            {
                new[]
                {
                    Upgrade("revision_upgrade.borrowed.shared_premonition", "Shared Premonition",
                        "The nearest other champion gains half the carried Mana.", 1,
                        RevisionModifier.SharedPremonition),
                    Upgrade("revision_upgrade.borrowed.deep_reserve", "Deep Reserve",
                        "The minimum carried Mana rises from 15 to 25.", 1,
                        RevisionModifier.DeepReserve),
                },
                new[]
                {
                    Upgrade("revision_upgrade.borrowed.clear_intention", "Clear Intention",
                        "Revised champions shed Silence and Disarm.", 2,
                        RevisionModifier.ClearIntention),
                    Upgrade("revision_upgrade.borrowed.long_memory", "Long Memory",
                        "The selectable rewind grows from four to six battle-seconds.", 2,
                        RevisionModifier.LongMemory),
                },
                new[]
                {
                    Upgrade("revision_upgrade.borrowed.convergence", "Convergence",
                        "Revise up to two allied champions.", 3,
                        RevisionModifier.Convergence),
                    Upgrade("revision_upgrade.borrowed.afterthought", "Afterthought",
                        "The first revised signature refunds half the Mana added.", 3,
                        RevisionModifier.Afterthought),
                },
            },
        };

        private static readonly RevisionDef Recall = new RevisionDef
        {
            Id = RecallToFormationId,
            Name = "Recall to Formation",
            Summary = "Return one enemy to where this battle began. Disarm it.",
            Effect = RevisionEffectKind.RecallToFormation,
            Tiers =
            {
                new[]
                {
                    Upgrade("revision_upgrade.recall.fixed_point", "Fixed Point",
                        "Root the primary target for 15 ticks.", 1,
                        RevisionModifier.FixedPoint),
                    Upgrade("revision_upgrade.recall.long_peace", "Long Peace",
                        "The primary Disarm lasts 25 ticks.", 1,
                        RevisionModifier.LongPeace),
                },
                new[]
                {
                    Upgrade("revision_upgrade.recall.roll_call", "Roll Call",
                        "Recall the nearest second enemy and Disarm it for 10 ticks.", 2,
                        RevisionModifier.RollCall),
                    Upgrade("revision_upgrade.recall.empty_hands", "Empty Hands",
                        "The primary target returns with no Mana.", 2,
                        RevisionModifier.EmptyHands),
                },
                new[]
                {
                    Upgrade("revision_upgrade.recall.general_recall", "General Recall",
                        "Return every living enemy to its deployment formation.", 3,
                        RevisionModifier.GeneralRecall),
                    Upgrade("revision_upgrade.recall.missing_hour", "Missing Hour",
                        "Omit the primary enemy for 20 ticks, then return it Disarmed.", 3,
                        RevisionModifier.MissingHour),
                },
            },
        };

        public static IReadOnlyList<RevisionDef> Starting => new[] { Borrowed, Recall };

        public static RevisionDef Get(string id)
        {
            if (id == Borrowed.Id) return Borrowed;
            if (id == Recall.Id) return Recall;
            throw new KeyNotFoundException($"unknown Revision '{id}'");
        }

        public static RevisionUpgradeDef Upgrade(string id)
        {
            foreach (var revision in Starting)
                foreach (var tier in revision.Tiers)
                    foreach (var upgrade in tier)
                        if (upgrade.Id == id) return upgrade;
            throw new KeyNotFoundException($"unknown Revision upgrade '{id}'");
        }

        public static RevisionModifier Modifiers(RevisionState state)
        {
            RevisionModifier result = RevisionModifier.None;
            foreach (string id in state.UpgradeIds)
                result |= Upgrade(id).Modifier;
            return result;
        }

        public static IReadOnlyList<RevisionUpgradeDef> NextOptions(RevisionState state)
        {
            var revision = Get(state.RevisionId);
            int tier = state.UpgradeIds.Count;
            return tier >= revision.Tiers.Count
                ? Array.Empty<RevisionUpgradeDef>()
                : revision.Tiers[tier];
        }

        public static void Validate(RevisionState state)
        {
            var revision = Get(state.RevisionId);
            if (state.UpgradeIds.Count > revision.Tiers.Count)
                throw new InvalidOperationException("Revision has more upgrades than tiers");
            for (int i = 0; i < state.UpgradeIds.Count; i++)
                if (!revision.Tiers[i].Any(u => u.Id == state.UpgradeIds[i]))
                    throw new InvalidOperationException(
                        $"Revision upgrade '{state.UpgradeIds[i]}' is not a tier {i + 1} option");
        }
    }

    public sealed class RevisionChoice
    {
        public int PresentTick;
        public int BranchTick;
        public List<int> TargetIds = new List<int>();
    }

    public enum PreparedFightKind { Encounter, Boss }

    public sealed class PreparedFight
    {
        public FightOutcome Original { get; internal set; } = null!;
        public PreparedFightKind Kind { get; internal set; }
        public FightTier Tier { get; internal set; }
        public int Act { get; internal set; }
        public int NodeIndex { get; internal set; }
        internal List<Hex> Placement = new List<Hex>();
        internal List<(UnitDef Def, Hex Pos)> Enemies = new List<(UnitDef, Hex)>();
        internal List<(UnitDef Def, Hex Pos, List<Status> Earned)> Players =
            new List<(UnitDef, Hex, List<Status>)>();
        internal List<(int Team, Trigger Trigger)> TeamTriggers =
            new List<(int, Trigger)>();

        /// <summary>How many team triggers this fight was built with. Public because it is the only
        /// honest way to assert that an inscription actually REACHED battle prep — checking run
        /// state only proves the id is held, not that its rules ride. The list itself stays internal.</summary>
        public int TeamTriggerCount => TeamTriggers.Count;
        internal List<long> HeroInstanceIds = new List<long>();
        internal ulong BattleSeed;
        internal bool Committed;
    }
}
