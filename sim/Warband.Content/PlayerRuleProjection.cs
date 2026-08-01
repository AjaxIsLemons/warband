using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;

namespace Warband.Content
{
    /// <summary>
    /// One composed champion's canonical card rules. Identity is authored in
    /// <see cref="ContentLexicon"/>; every mechanical word is projected from the resolved
    /// <see cref="UnitDef"/>.
    /// </summary>
    public sealed class ChampionRuleProjection
    {
        public string SignatureId { get; }
        public string SignatureName { get; }
        public string SignatureText { get; }
        public string PassiveName { get; }
        public string PassiveText { get; }

        public ChampionRuleProjection(
            string signatureId,
            string signatureName,
            string signatureText,
            string passiveName,
            string passiveText)
        {
            SignatureId = signatureId;
            SignatureName = signatureName;
            SignatureText = signatureText;
            PassiveName = passiveName;
            PassiveText = passiveText;
        }
    }

    /// <summary>An authored rank row, exposed without leaking Kits' string-key encoding to UI.</summary>
    public sealed class SpecializationTierProjection
    {
        public Rank Rank { get; }
        public bool IsFork { get; }
        public string? PathId { get; }
        public IReadOnlyList<string> OptionIds { get; }
        public bool NeedsPath { get; }

        public SpecializationTierProjection(
            Rank rank,
            bool isFork,
            string? pathId,
            IReadOnlyList<string> optionIds,
            bool needsPath)
        {
            Rank = rank;
            IsFork = isFork;
            PathId = pathId;
            OptionIds = optionIds;
            NeedsPath = needsPath;
        }
    }

    /// <summary>
    /// The exact identity and two display tiers for one specialization choice.
    /// <see cref="Choice"/> is decision-sized; <see cref="Full"/> is the exhaustive rule.
    /// </summary>
    public sealed class SpecializationRuleProjection
    {
        public string Id { get; }
        public string Name { get; }
        public LexKind Kind { get; }
        public Rank Rank { get; }
        public MechanicalChangeKind Change { get; }
        public string Choice { get; }
        public string Full { get; }

        public SpecializationRuleProjection(
            string id,
            string name,
            LexKind kind,
            Rank rank,
            MechanicalChangeKind change,
            string choice,
            string full)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Rank = rank;
            Change = change;
            Choice = choice;
            Full = full;
        }
    }

    /// <summary>
    /// The single headless door from authored combat data to champion, Signature, passive, and
    /// specialization copy. Unity and fixtures consume these projections; neither interprets
    /// effects, parses offer keys, or keeps a second set of tuning values.
    /// </summary>
    public static class PlayerRuleProjection
    {
        public static ChampionRuleProjection Champion(UnitDef unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (string.IsNullOrWhiteSpace(unit.ChassisId))
                throw new ArgumentException(
                    "A champion rules projection requires UnitDef.ChassisId.", nameof(unit));

            string signatureId = AbilityIdentity.Resolve(unit.ChassisId, unit.Traits);
            List<Trigger> signatureTriggers = unit.Signature.Count == 0
                ? unit.Triggers.Where(trigger =>
                    trigger.On == EventKind.Cast &&
                    (SourceId(trigger.RuleId) == signatureId ||
                     SourceId(trigger.RuleId) == signatureId + "/signature")).ToList()
                : new List<Trigger>();
            string signatureText = signatureTriggers.Count > 0
                ? string.Join(" ", signatureTriggers.Select(MechanicalRulePresenter.Trigger))
                : MechanicalRulePresenter.Signature(unit.Signature);

            var passiveTriggers = unit.Triggers.Where(trigger =>
                SourceId(trigger.RuleId) == unit.ChassisId &&
                !signatureTriggers.Contains(trigger)).ToList();
            var passiveStats = unit.StatRules.Where(rule =>
                SourceId(rule.RuleId) == unit.ChassisId).ToList();

            return new ChampionRuleProjection(
                signatureId,
                ContentLexicon.Signature(signatureId).Name,
                signatureText,
                ContentLexicon.Innate(unit.ChassisId).Name,
                MechanicalRulePresenter.Passives(passiveTriggers, passiveStats));
        }

        public static SpecializationRuleProjection Specialization(
            string chassisId, string nodeId, UnitDef before, UnitDef after)
        {
            if (!Kits.Nodes.TryGetValue(nodeId, out SpecNode? node))
                throw new InvalidOperationException(
                    $"'{nodeId}' is not a live specialization node.");
            LexEntry identity = ContentLexicon.Node(nodeId);
            MechanicalRule rule = MechanicalRulePresenter.Node(node, before, after);
            return new SpecializationRuleProjection(
                nodeId,
                identity.Name,
                identity.Kind,
                RankOf(chassisId, nodeId),
                rule.Change,
                rule.Choice,
                rule.Full);
        }

        /// <summary>
        /// Glossary notes for mechanics the composed kit actually references. This replaces the
        /// old per-chassis JSON list: adding or removing a Status, line, field, Leap, Counter,
        /// Shield, or Mana effect changes the notes with the rule data.
        /// </summary>
        public static IReadOnlyList<string> Keywords(UnitDef unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            var notes = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (EffectDef effect in unit.Signature)
                AddEffectKeywords(effect, notes, seen);
            foreach (Trigger trigger in unit.Triggers)
            {
                if (trigger.On == EventKind.Leap)
                    AddNote("LEAP", "Reposition instantly; no movement path is taken.",
                            notes, seen);
                foreach (Cond condition in trigger.When)
                    AddConditionKeyword(condition, notes, seen);
                foreach (EffectDef effect in trigger.Do)
                    AddEffectKeywords(effect, notes, seen);
            }
            foreach (StatRule rule in unit.StatRules)
                foreach (Cond condition in rule.When)
                    AddConditionKeyword(condition, notes, seen);
            return notes;
        }

        public static Rank RankOf(string chassisId, string nodeId)
        {
            SpecOfferRow? match = null;
            foreach (SpecOfferRow row in Kits.OfferRows)
            {
                if (!string.Equals(row.ChassisId, chassisId, StringComparison.Ordinal) ||
                    !row.NodeIds.Contains(nodeId))
                    continue;
                if (match != null)
                    throw new InvalidOperationException(
                        $"Specialization node '{nodeId}' appears in more than one live tier.");
                match = row;
            }
            if (match == null)
                throw new InvalidOperationException(
                    $"Specialization node '{nodeId}' is not offered by '{chassisId}'.");
            return match.Rank;
        }

        public static IReadOnlyList<SpecializationTierProjection> Tiers(
            string chassisId, string? pathId)
        {
            if (!Kits.ForkRanks.TryGetValue(chassisId, out Rank fork))
                throw new InvalidOperationException(
                    $"No specialization fork is authored for '{chassisId}'.");

            var result = new List<SpecializationTierProjection>();
            for (int value = (int)Rank.B; value <= (int)Rank.S; value++)
            {
                Rank rank = (Rank)value;
                bool needsPath = rank > fork && string.IsNullOrEmpty(pathId);
                string? rowPath = rank > fork ? pathId : null;
                IReadOnlyList<string> options = Array.Empty<string>();
                if (!needsPath)
                {
                    SpecOfferRow? row = Kits.OfferRows.SingleOrDefault(candidate =>
                        candidate.ChassisId == chassisId &&
                        candidate.Rank == rank &&
                        candidate.PathId == rowPath);
                    if (row == null)
                        throw new InvalidOperationException(
                            $"No live specialization tier is authored for " +
                            $"'{chassisId}|{rank}|{rowPath ?? "-"}'.");
                    options = row.NodeIds;
                }
                result.Add(new SpecializationTierProjection(
                    rank, rank == fork, rowPath, options, needsPath));
            }
            return result;
        }

        private static string SourceId(string ruleId)
        {
            if (string.IsNullOrEmpty(ruleId)) return "";
            int suffix = ruleId.LastIndexOf('#');
            return suffix > 0 ? ruleId.Substring(0, suffix) : ruleId;
        }

        private static void AddEffectKeywords(
            EffectDef effect, List<string> notes, HashSet<string> seen)
        {
            if (effect.Select.Kind == SelKind.EnemiesOnLineThroughTarget ||
                effect.Select.Kind == SelKind.EnemiesOnLineThroughFarthest)
                AddNote("LINE", "Affects every valid enemy on the traced hex line.",
                        notes, seen);

            switch (effect.Kind)
            {
                case EffectKind.ApplyStatus:
                case EffectKind.RemoveStatus:
                    AddStatus(effect.Status, notes, seen);
                    break;
                case EffectKind.GrantShield:
                    AddNote("SHIELD", "Absorbs damage before HP.", notes, seen);
                    break;
                case EffectKind.GrantMana:
                    AddNote(
                        "MANA",
                        "A Signature casts automatically when Mana reaches its maximum.",
                        notes,
                        seen);
                    break;
                case EffectKind.Leap:
                    AddNote("LEAP", "Reposition instantly; no movement path is taken.",
                            notes, seen);
                    break;
                case EffectKind.Swing when effect.AsCounter:
                    AddNote("COUNTER", Lexicon.Of(Cause.Counter).Text, notes, seen);
                    break;
                case EffectKind.CreateField when effect.Field != null:
                    AddNote(
                        "FIELD",
                        "A persistent ground effect; leaving its hexes avoids later pulses.",
                        notes,
                        seen);
                    foreach (var presence in effect.Field.Presence)
                        AddStatus(presence.Kind, notes, seen);
                    foreach (EffectDef pulse in effect.Field.Pulse)
                        AddEffectKeywords(pulse, notes, seen);
                    foreach (EffectDef rider in effect.Field.ProjectileRiders)
                        AddEffectKeywords(rider, notes, seen);
                    break;
            }

            if (effect.ScaleByTargetStatus || effect.ScaleByEventTargetStatus)
                AddStatus(effect.ScaleStatus, notes, seen);
        }

        private static void AddConditionKeyword(
            Cond condition, List<string> notes, HashSet<string> seen)
        {
            switch (condition.Kind)
            {
                case CondKind.OwnerHasStatus:
                case CondKind.TargetHasStatus:
                case CondKind.SourceHasStatus:
                case CondKind.StatusIs:
                case CondKind.AnyEnemyHasStatus:
                    AddStatus(condition.Status, notes, seen);
                    break;
                case CondKind.CauseIs when condition.Cause == Cause.Counter:
                    AddNote("COUNTER", Lexicon.Of(Cause.Counter).Text, notes, seen);
                    break;
            }
        }

        private static void AddStatus(
            StatusKind status, List<string> notes, HashSet<string> seen)
        {
            LexEntry entry = Lexicon.Of(status);
            AddNote(entry.Name.ToUpperInvariant(), entry.Text, notes, seen);
        }

        private static void AddNote(
            string name, string text, List<string> notes, HashSet<string> seen)
        {
            if (!seen.Add(name)) return;
            notes.Add($"{name} · {text}");
        }
    }
}
