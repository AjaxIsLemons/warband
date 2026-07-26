using System;
using System.Collections.Generic;
using System.Linq;
using Warband.Run;
using Warband.Sim;

namespace Warband.Content
{
    /// <summary>
    /// How a choice changes an existing kit. This is presentation metadata derived from the
    /// authored primitives, not a second gameplay rule.
    /// </summary>
    public enum MechanicalChangeKind
    {
        Add,
        Swap,
        Deepen,
    }

    /// <summary>
    /// Exact, headless display language generated from the same primitives the simulation reads.
    /// Compact is card-sized; Full retains every authored clause for dossiers and choices.
    /// </summary>
    public readonly struct MechanicalRule
    {
        public readonly MechanicalChangeKind Change;
        public readonly string Compact;
        public readonly string Full;

        public MechanicalRule(MechanicalChangeKind change, string compact, string full)
        {
            Change = change;
            Compact = compact;
            Full = full;
        }
    }

    /// <summary>
    /// Mechanical grammar for nodes, items, Inscriptions, signatures, and mastery riders.
    /// Numbers are never copied into presentation data: changing content changes this output.
    /// Unsupported authored primitives throw so a CI contract catches the gap before raw or vague
    /// copy can reach the Unity client.
    /// </summary>
    public static class MechanicalRulePresenter
    {
        private const int TicksPerSecond = 10;

        public static MechanicalRule Node(SpecNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            var clauses = new List<string>();

            if (node.HpBonus != 0)
                clauses.Add($"Maximum HP {Signed(node.HpBonus)}.");
            if (node.TargetPref.HasValue)
                clauses.Add(TargetPreference(node.TargetPref.Value));
            if (node.Standoff.HasValue)
                clauses.Add(node.Standoff.Value <= 0
                    ? "Closes to weapon reach and holds ground."
                    : $"Maintains {node.Standoff.Value}-hex standoff while the target remains in weapon reach.");
            if (node.CleaveBonusPct != 0)
                clauses.Add($"Basic attack cleave {Signed(node.CleaveBonusPct)} percentage points.");
            if (node.DoublesBanners)
                clauses.Add("Every bound Inscription triggers twice.");

            foreach (var (kind, mag) in node.SpawnStatuses)
                clauses.Add($"Starts combat with {Status(kind, mag)} for the rest of the fight.");
            foreach (var rule in node.StatRules)
                clauses.Add(StatRule(rule));
            foreach (var trigger in node.Triggers)
                clauses.Add(Trigger(trigger));

            if (node.SignatureOverride != null)
                clauses.Add(node.SignatureOverride.Count == 0
                    ? "Signature is removed."
                    : "Signature becomes: " + Effects(node.SignatureOverride));
            if (node.SignaturePatch != null)
                clauses.Add(SignaturePatch(node.SignaturePatch));

            RequireClauses(clauses, node.Name);
            MechanicalChangeKind change =
                node.SignatureOverride != null || node.TargetPref.HasValue || node.Standoff.HasValue
                    ? MechanicalChangeKind.Swap
                    : node.SignaturePatch != null
                        ? MechanicalChangeKind.Deepen
                        : MechanicalChangeKind.Add;
            return Rule(change, clauses);
        }

        public static MechanicalRule Trinket(TrinketDef trinket)
        {
            if (trinket == null) throw new ArgumentNullException(nameof(trinket));
            var clauses = new List<string>();
            if (trinket.HpBonus != 0)
                clauses.Add($"Maximum HP {Signed(trinket.HpBonus)}.");
            if (trinket.ManaMaxDelta != 0)
                clauses.Add($"Signature threshold {Signed(trinket.ManaMaxDelta)} Mana.");
            foreach (var (kind, mag) in trinket.SpawnStatuses)
                clauses.Add($"Starts combat with {Status(kind, mag)} for the rest of the fight.");
            foreach (var rule in trinket.StatRules)
                clauses.Add(StatRule(rule));
            foreach (var trigger in trinket.Triggers)
                clauses.Add(Trigger(trigger));
            RequireClauses(clauses, trinket.Name);
            return Rule(MechanicalChangeKind.Add, clauses);
        }

        public static MechanicalRule Inscription(BannerDef inscription)
        {
            if (inscription == null) throw new ArgumentNullException(nameof(inscription));
            var clauses = inscription.TeamTriggers.Select(Trigger).ToList();
            RequireClauses(clauses, inscription.Name);
            return Rule(MechanicalChangeKind.Add, clauses);
        }

        public static MechanicalRule WeaponMastery(WeaponDef weapon)
        {
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));
            var clauses = new List<string>();
            if (weapon.MasteryRangeBonus != 0)
                clauses.Add($"Weapon reach {Signed(weapon.MasteryRangeBonus)}.");
            foreach (var rule in weapon.MasteryStatRules)
                clauses.Add(StatRule(rule));
            foreach (var trigger in weapon.MasteryTriggers)
                clauses.Add(Trigger(trigger));
            RequireClauses(clauses, weapon.Name + " mastery");
            return Rule(MechanicalChangeKind.Add, clauses);
        }

        public static string Signature(IReadOnlyList<EffectDef> effects)
        {
            if (effects == null || effects.Count == 0) return "No signature effect.";
            return Effects(effects);
        }

        /// <summary>
        /// Number of completed basic attacks required to fill an empty signature meter using the
        /// weapon's own Mana-per-swing axis. Null means attacks do not produce a finite cast.
        /// </summary>
        public static int? BasicAttacksToSignature(UnitDef unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (unit.ManaMax <= 0 || unit.ManaPerSwing <= 0) return null;
            return (unit.ManaMax + unit.ManaPerSwing - 1) / unit.ManaPerSwing;
        }

        /// <summary>
        /// Exact innate/passive language for a composed unit. A hero may express a passive as an
        /// event trigger, a live stat rule, or both, so the overload keeps both authored channels
        /// visible without asking the Unity client to understand their grammar.
        /// </summary>
        public static string Passives(IReadOnlyList<Trigger> triggers)
        {
            if (triggers == null || triggers.Count == 0) return "No passive rule.";
            return string.Join(" ", triggers.Select(Trigger));
        }

        public static string Passives(IReadOnlyList<Trigger> triggers,
                                      IReadOnlyList<StatRule> statRules)
        {
            var clauses = new List<string>();
            if (triggers != null) clauses.AddRange(triggers.Select(Trigger));
            if (statRules != null) clauses.AddRange(statRules.Select(StatRule));
            return clauses.Count == 0 ? "No passive rule." : string.Join(" ", clauses);
        }

        public static string Trigger(Trigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (trigger.Do.Count == 0)
                throw new InvalidOperationException("A displayed trigger has no effects.");
            var remaining = trigger.When.ToList();
            string when = EventPhrase(trigger.On, remaining);
            if (remaining.Count > 0)
                when += " if " + string.Join(" and ", remaining.Select(Condition));
            string prefix = trigger.On == EventKind.BattleStart ? "When" : "After";
            return $"{prefix} {when}: {Effects(trigger.Do, trigger.On)}";
        }

        private static MechanicalRule Rule(MechanicalChangeKind change, List<string> clauses)
        {
            string full = string.Join(" ", clauses);
            string compact = clauses[0];
            if (clauses.Count > 1)
                compact = compact.TrimEnd('.') + $" · +{clauses.Count - 1} rule" +
                          (clauses.Count == 2 ? "." : "s.");
            return new MechanicalRule(change, compact, full);
        }

        private static string Effects(IReadOnlyList<EffectDef> effects,
                                      EventKind? sourceEvent = null) =>
            string.Join(" Then ", effects.Select(effect => Effect(effect, sourceEvent)));

        private static string Effect(EffectDef effect, EventKind? sourceEvent,
                                     string? targetOverride = null)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            string target = targetOverride ?? Selector(effect.Select);
            string amount;
            if (effect.PctOfEventAmount > 0)
                amount = sourceEvent == EventKind.DamageDealt
                    ? $"{effect.PctOfEventAmount}% of the triggering damage"
                    : sourceEvent == EventKind.Death
                        ? $"{effect.PctOfEventAmount}% of the recorded overkill"
                        : $"{effect.PctOfEventAmount}% of the triggering amount";
            else if (effect.ScaleByTargetStatus || effect.ScaleByEventTargetStatus)
                amount = $"{effect.Amount} per {Lexicon.Of(effect.ScaleStatus).Name} stack";
            else
                amount = effect.Amount.ToString();

            string clause;
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    clause = $"Deal {amount} damage to {target}.";
                    break;
                case EffectKind.Heal:
                    clause = $"Heal {target} for {amount}.";
                    break;
                case EffectKind.ApplyStatus:
                    clause = target == "this champion"
                        ? $"Gain {Status(effect.Status, effect.Amount)}" +
                          Duration(effect.StatusTicks, effect.StatusSwings, true) + "."
                        : $"Apply {Status(effect.Status, effect.Amount)} to {target}" +
                          Duration(effect.StatusTicks, effect.StatusSwings, false) + ".";
                    break;
                case EffectKind.GrantShield:
                    clause = $"Grant {amount} Shield to {target}.";
                    break;
                case EffectKind.GrantMana:
                    clause = $"Grant {effect.Amount} Mana to {target}.";
                    break;
                case EffectKind.CreateField:
                    if (effect.Field == null)
                        throw new InvalidOperationException("CreateField is missing its FieldDef.");
                    clause = $"Create {Field(effect.Field, target)}.";
                    break;
                case EffectKind.Leap:
                    clause = $"Leap to a free hex adjacent to {target}.";
                    break;
                case EffectKind.Swing:
                    int pct = effect.Amount == 0 ? 100 : effect.Amount;
                    clause = effect.AsCounter
                        ? $"Counter-swing at {target} for {pct}% basic-attack damage."
                        : $"Swing at {target} for {pct}% basic-attack damage.";
                    break;
                case EffectKind.Execute:
                    clause = $"Execute {target}.";
                    break;
                case EffectKind.RemoveStatus:
                    clause = $"Remove all {Lexicon.Of(effect.Status).Name} from {target}.";
                    break;
                case EffectKind.Recast:
                    clause = $"Recast the signature on {target}.";
                    break;
                default:
                    throw new InvalidOperationException($"No display grammar for EffectKind.{effect.Kind}.");
            }

            if (effect.EscalatePctPerIndex != 0)
                clause = clause.TrimEnd('.') +
                         $" Each later resolved target takes {Signed(effect.EscalatePctPerIndex)}% effect.";
            return clause;
        }

        private static string Field(FieldDef field, string center)
        {
            var clauses = new List<string>();
            string radius = field.Radius <= 0 ? "a single-hex field" : $"a radius-{field.Radius} field";
            string duration = field.Ticks < 0
                ? "for the rest of the fight"
                : $"for {Seconds(field.Ticks)}";
            clauses.Add($"{radius} centered on {center} {duration}");
            if (field.IsWall) clauses.Add("it blocks movement and projectile paths");
            if (field.AttachToOwner) clauses.Add("it follows its owner");
            if (field.Pulse.Count > 0)
                clauses.Add($"each second: {FieldEffects(field.Pulse, field.PulseAffects)}");
            if (field.Presence.Count > 0)
            {
                string statuses = string.Join(", ",
                    field.Presence.Select(s => Status(s.Kind, s.Mag)));
                clauses.Add($"{Affects(field.PresenceAffects)} inside have {statuses}");
            }
            if (field.ProjectileBonus != 0)
                clauses.Add($"crossing projectiles affecting {Affects(field.ProjectileAffects)} gain " +
                            $"{Signed(field.ProjectileBonus)} damage");
            if (field.ProjectileRiders.Count > 0)
                clauses.Add($"crossing projectiles apply: {InlineEffects(field.ProjectileRiders)}");
            return string.Join("; ", clauses);
        }

        private static string FieldEffects(IReadOnlyList<EffectDef> effects, Affects affects)
        {
            string target;
            switch (affects)
            {
                case Warband.Sim.Affects.All: target = "every unit in the field"; break;
                case Warband.Sim.Affects.Allies: target = "every ally in the field"; break;
                case Warband.Sim.Affects.Enemies: target = "every enemy in the field"; break;
                default:
                    throw new InvalidOperationException(
                        $"No field-target grammar for Affects.{affects}.");
            }
            return string.Join(" Then ",
                effects.Select(effect => Effect(effect, null, target).TrimEnd('.')));
        }

        private static string InlineEffects(IReadOnlyList<EffectDef> effects) =>
            string.Join(" Then ",
                effects.Select(effect => Effect(effect, null).TrimEnd('.')));

        private static string Selector(Selector selector)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            string value;
            switch (selector.Kind)
            {
                case SelKind.Self: value = "this champion"; break;
                case SelKind.EventSource: value = "the triggering source"; break;
                case SelKind.EventTarget: value = "the triggering target"; break;
                case SelKind.CurrentTarget: value = "the current target"; break;
                case SelKind.NearestEnemy: value = "the nearest enemy"; break;
                case SelKind.FarthestEnemy: value = "the farthest enemy"; break;
                case SelKind.LowestHpAlly: value = "the lowest-HP ally"; break;
                case SelKind.AlliesWithin:
                    value = $"allies within {selector.Range} hex" + (selector.Range == 1 ? "" : "es");
                    break;
                case SelKind.EnemiesWithin:
                    value = $"enemies within {selector.Range} hex" + (selector.Range == 1 ? "" : "es");
                    break;
                case SelKind.EnemiesOnLineThroughTarget:
                    value = selector.Range <= 0
                        ? "enemies on the board-length line through the current target"
                        : $"enemies on the {selector.Range}-hex line through the current target";
                    break;
                case SelKind.EnemiesOnLineThroughFarthest:
                    value = selector.Range <= 0
                        ? "enemies on the board-length line through the farthest enemy"
                        : $"enemies on the {selector.Range}-hex line through the farthest enemy";
                    break;
                default:
                    throw new InvalidOperationException($"No display grammar for SelKind.{selector.Kind}.");
            }

            if (selector.AnchorEventTarget) value += ", measured from the triggering target";
            else if (selector.AnchorEvent) value += ", measured from the triggering source";
            if (selector.ExcludeSelf) value += ", excluding this champion";
            if (selector.ExcludeAnchorUnit) value += ", excluding the anchor unit";
            if (selector.SkipCtxTarget) value += ", beyond the current target only";
            if (selector.BelowHpPct > 0) value += $", below {selector.BelowHpPct}% HP";
            if (selector.MustHave.HasValue)
                value += $", with {Lexicon.Of(selector.MustHave.Value).Name}";
            return value;
        }

        private static string Condition(Cond condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            string value;
            switch (condition.Kind)
            {
                case CondKind.SourceIsOwner: value = "this champion is the source"; break;
                case CondKind.TargetIsOwner: value = "this champion is the target"; break;
                case CondKind.SourceIsEnemyOfOwner: value = "the source is an enemy"; break;
                case CondKind.TargetIsAllyOfOwner: value = "the target is another ally"; break;
                case CondKind.CauseIs: value = $"the event came from {CausePhrase(condition.Cause)}"; break;
                case CondKind.OwnerBelowHpPct: value = $"this champion is below {condition.Amount}% HP"; break;
                case CondKind.TargetWithinHexesOfOwner:
                    value = $"the target is within {condition.Amount} hexes of this champion"; break;
                case CondKind.SourceWithinHexesOfOwner:
                    value = $"the source is within {condition.Amount} hexes of this champion"; break;
                case CondKind.IsCrit: value = "the hit is critical"; break;
                case CondKind.TargetBelowHpPct: value = $"the target is below {condition.Amount}% HP"; break;
                case CondKind.TargetAtRangeOfOwner:
                    value = $"the target is exactly {condition.Amount} hexes from this champion"; break;
                case CondKind.NoEnemyWithinHexesOfOwner:
                    value = $"no enemy is within {condition.Amount} hexes of this champion"; break;
                case CondKind.TargetAdjacentToAllyOfOwner:
                    value = "the target is adjacent to another ally"; break;
                case CondKind.AnyEnemyTauntedByOwner:
                    value = "any enemy is Taunted by this champion"; break;
                case CondKind.OwnerHasStatus:
                    value = $"this champion has {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.TargetHasStatus:
                    value = $"the target has {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.SourceHasStatus:
                    value = $"the source has {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.EveryNthSwingOfOwner:
                    value = $"this is this champion's every {Ordinal(condition.Amount)} swing"; break;
                case CondKind.StatusIs:
                    value = $"the status is {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.OwnerRecentDamageAbovePct:
                    value = $"recent damage to this champion is at least {condition.Amount}% of maximum HP"; break;
                case CondKind.IsRootEvent: value = "this is the original event, not a triggered echo"; break;
                case CondKind.AnyEnemyHasStatus:
                    value = $"any enemy has {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.TargetInFieldOfOwner:
                    value = "the target stands in this champion's field"; break;
                default:
                    throw new InvalidOperationException(
                        $"No display grammar for CondKind.{condition.Kind}.");
            }
            if (!condition.Not) return value;
            switch (condition.Kind)
            {
                case CondKind.SourceIsEnemyOfOwner: return "the source is an ally";
                case CondKind.TargetIsAllyOfOwner: return "the target is an enemy";
                case CondKind.OwnerHasStatus:
                    return $"this champion does not have {Lexicon.Of(condition.Status).Name}";
                case CondKind.IsRootEvent: return "this is a triggered echo";
                default: return "it is not true that " + value;
            }
        }

        private static string StatRule(StatRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            string stat;
            switch (rule.Stat)
            {
                case StatKind.AttackFlat: stat = $"{Signed(rule.Amount)} basic power"; break;
                case StatKind.AttackSpeed: stat = $"{SignedPercentFp(rule.Amount)} attack speed"; break;
                default:
                    throw new InvalidOperationException($"No display grammar for StatKind.{rule.Stat}.");
            }

            string scale;
            switch (rule.ScaleBy)
            {
                case StatScale.None: scale = ""; break;
                case StatScale.DistanceToTarget: scale = " per hex to the target"; break;
                case StatScale.MissingHpPct10: scale = " per 10% missing HP"; break;
                case StatScale.ShieldPer10: scale = " per 10 Shield held"; break;
                default:
                    throw new InvalidOperationException($"No display grammar for StatScale.{rule.ScaleBy}.");
            }

            string when = rule.When.Count == 0
                ? "Always"
                : "While " + string.Join(" and ", rule.When.Select(Condition));
            return $"{when}: {stat}{scale}.";
        }

        private static string SignaturePatch(SignaturePatch patch)
        {
            var clauses = new List<string>();
            if (patch.RadiusDelta != 0)
                clauses.Add($"signature radius {Signed(patch.RadiusDelta)}");
            if (patch.LineRange.HasValue)
                clauses.Add(patch.LineRange.Value <= 0
                    ? "signature line becomes board-length"
                    : $"signature line becomes {patch.LineRange.Value} hexes");
            if (patch.AmountPct != 100)
                clauses.Add($"all signature magnitudes become {patch.AmountPct}%");
            if (patch.Escalate.HasValue)
                clauses.Add($"each later resolved target gains {patch.Escalate.Value}% effect");
            if (patch.FieldRadius.HasValue)
                clauses.Add($"created-field radius becomes {patch.FieldRadius.Value}");
            if (patch.FieldTicks.HasValue)
                clauses.Add(patch.FieldTicks.Value < 0
                    ? "created fields last for the rest of the fight"
                    : $"created fields last {Seconds(patch.FieldTicks.Value)}");
            if (patch.Repeat > 1)
                clauses.Add($"resolve the full signature {patch.Repeat} times");
            if (patch.Add.Count > 0)
                clauses.Add("then add: " + Effects(patch.Add));
            RequireClauses(clauses, "signature patch");
            return "Deepen the inherited signature: " + string.Join("; ", clauses) + ".";
        }

        private static string EventPhrase(EventKind kind, List<Cond> conditions)
        {
            Cond? sourceOwner;
            Cond? targetOwner;
            Cond? sourceSide;
            Cond? targetSide;
            Cond? cause;
            switch (kind)
            {
                case EventKind.DamageDealt:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    sourceSide = Take(conditions, CondKind.SourceIsEnemyOfOwner);
                    targetSide = Take(conditions, CondKind.TargetIsAllyOfOwner);
                    cause = Take(conditions, CondKind.CauseIs, false);
                    string damage = DamageName(cause?.Cause);
                    if (sourceOwner != null) return $"this champion deals {damage}";
                    if (targetOwner != null) return $"this champion takes {damage}";
                    if (sourceSide != null)
                        return $"{(sourceSide.Not ? "an ally" : "an enemy")} deals {damage}";
                    if (targetSide != null)
                        return $"{(targetSide.Not ? "an enemy" : "another ally")} takes {damage}";
                    return cause == null ? "damage is dealt" : $"{damage} is dealt";

                case EventKind.Death:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    targetSide = Take(conditions, CondKind.TargetIsAllyOfOwner);
                    if (sourceOwner != null) return "this champion kills a unit";
                    if (targetOwner != null) return "this champion dies";
                    if (targetSide != null)
                        return targetSide.Not ? "an enemy dies" : "another ally dies";
                    return "a unit dies";

                case EventKind.Attack:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    targetSide = Take(conditions, CondKind.TargetIsAllyOfOwner);
                    if (sourceOwner != null) return "this champion begins a basic attack";
                    if (targetOwner != null) return "a basic attack targets this champion";
                    if (targetSide != null)
                        return targetSide.Not
                            ? "a basic attack targets an enemy"
                            : "a basic attack targets another ally";
                    return "a basic attack begins";

                case EventKind.Cast:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    sourceSide = Take(conditions, CondKind.SourceIsEnemyOfOwner);
                    if (sourceOwner != null) return "this champion casts their signature";
                    if (sourceSide != null)
                        return $"{(sourceSide.Not ? "an ally" : "an enemy")} casts their signature";
                    return "a signature is cast";

                case EventKind.Heal:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    if (sourceOwner != null) return "this champion heals a unit";
                    if (targetOwner != null) return "this champion is healed";
                    return "healing resolves";

                case EventKind.Move:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    sourceSide = Take(conditions, CondKind.SourceIsEnemyOfOwner);
                    if (sourceOwner != null) return "this champion finishes moving";
                    if (sourceSide != null)
                        return $"{(sourceSide.Not ? "an ally" : "an enemy")} finishes moving";
                    return "a unit finishes moving";

                case EventKind.MoveStart:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    sourceSide = Take(conditions, CondKind.SourceIsEnemyOfOwner);
                    if (sourceOwner != null) return "this champion starts moving";
                    if (sourceSide != null)
                        return $"{(sourceSide.Not ? "an ally" : "an enemy")} starts moving";
                    return "a unit starts moving";

                case EventKind.Leap:
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    sourceSide = Take(conditions, CondKind.SourceIsEnemyOfOwner);
                    if (sourceOwner != null) return "this champion Leaps";
                    if (sourceSide != null)
                        return $"{(sourceSide.Not ? "an ally" : "an enemy")} Leaps";
                    return "a unit Leaps";

                case EventKind.StatusApplied:
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    return targetOwner != null
                        ? "a status is applied to this champion"
                        : "a status is applied";

                case EventKind.StatusExpired:
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    return targetOwner != null
                        ? "a status on this champion expires"
                        : "a status expires";

                case EventKind.CheatDeath:
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    return targetOwner != null
                        ? "this champion refuses a lethal hit"
                        : "a lethal hit is refused";

                default:
                    return Event(kind);
            }
        }

        private static Cond? Take(List<Cond> conditions, CondKind kind,
                                  bool? negated = null)
        {
            int index = conditions.FindIndex(condition =>
                condition.Kind == kind &&
                (!negated.HasValue || condition.Not == negated.Value));
            if (index < 0) return null;
            Cond result = conditions[index];
            conditions.RemoveAt(index);
            return result;
        }

        private static string DamageName(Cause? cause)
        {
            if (!cause.HasValue || cause.Value == Warband.Sim.Cause.None) return "damage";
            switch (cause.Value)
            {
                case Warband.Sim.Cause.Attack: return "basic-attack damage";
                case Warband.Sim.Cause.Ability: return "signature damage";
                case Warband.Sim.Cause.Dot: return "damage-over-time";
                case Warband.Sim.Cause.Storm: return "overtime-storm damage";
                case Warband.Sim.Cause.Trigger: return "triggered damage";
                case Warband.Sim.Cause.Field: return "field damage";
                case Warband.Sim.Cause.Burn: return "Burn damage";
                case Warband.Sim.Cause.Counter: return "counter damage";
                default:
                    throw new InvalidOperationException(
                        $"No display grammar for Cause.{cause.Value}.");
            }
        }

        private static string CausePhrase(Cause cause)
        {
            switch (cause)
            {
                case Warband.Sim.Cause.None: return "an untagged effect";
                case Warband.Sim.Cause.Attack: return "a basic attack";
                case Warband.Sim.Cause.Ability: return "a signature";
                case Warband.Sim.Cause.Dot: return "damage over time";
                case Warband.Sim.Cause.Storm: return "the overtime storm";
                case Warband.Sim.Cause.Trigger: return "a triggered effect";
                case Warband.Sim.Cause.Field: return "a field";
                case Warband.Sim.Cause.Burn: return "Burn";
                case Warband.Sim.Cause.Counter: return "a counter";
                default:
                    throw new InvalidOperationException($"No display grammar for Cause.{cause}.");
            }
        }

        private static string Event(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.BattleStart: return "combat starts";
                case EventKind.Move: return "a unit arrives after moving";
                case EventKind.Attack: return "a basic attack begins";
                case EventKind.DamageDealt: return "damage is dealt";
                case EventKind.Heal: return "healing resolves";
                case EventKind.Cast: return "a signature is cast";
                case EventKind.StatusApplied: return "a status is applied";
                case EventKind.StatusExpired: return "a status expires";
                case EventKind.ShieldChanged: return "Shield changes";
                case EventKind.ManaChanged: return "Mana changes";
                case EventKind.Death: return "a unit dies";
                case EventKind.StormTick: return "the overtime storm ticks";
                case EventKind.End: return "combat ends";
                case EventKind.FieldCreated: return "a field is created";
                case EventKind.FieldHex: return "a field claims a hex";
                case EventKind.FieldExpired: return "a field expires";
                case EventKind.AttackBlocked: return "an attack is blocked by a wall";
                case EventKind.Leap: return "a unit Leaps";
                case EventKind.CheatDeath: return "a lethal hit is refused";
                case EventKind.MoveStart: return "a unit starts moving";
                default:
                    throw new InvalidOperationException($"No display grammar for EventKind.{kind}.");
            }
        }

        private static string Status(StatusKind kind, int magnitude)
        {
            string name = Lexicon.Of(kind).Name;
            switch (kind)
            {
                case StatusKind.Haste:
                case StatusKind.Slow:
                case StatusKind.Frenzied:
                    return $"{name} {PercentFp(magnitude)}";
                case StatusKind.DamageTakenDown:
                case StatusKind.DamageTakenUp:
                case StatusKind.BurnAmp:
                case StatusKind.SwingAmpPct:
                    return $"{name} {magnitude}%";
                case StatusKind.CritUp:
                    return $"{name} +{magnitude} percentage points";
                case StatusKind.CritMultUp:
                    return $"{name} +{PercentFp(magnitude)} critical damage";
                case StatusKind.AttackUp:
                case StatusKind.AttackDown:
                case StatusKind.Regen:
                case StatusKind.Dot:
                case StatusKind.Burn:
                case StatusKind.MultiShotRamp:
                case StatusKind.MultiShotWindow:
                    return $"{name} {magnitude}";
                default:
                    return magnitude == 0 ? name : $"{name} {magnitude}";
            }
        }

        private static string Duration(int ticks, int swings, bool self)
        {
            if (swings > 0)
            {
                string subject = self ? "the next" : "their next";
                return swings == 1
                    ? $" for {subject} basic attack"
                    : $" for {subject} {swings} basic attacks";
            }
            if (ticks < 0) return " for the rest of the fight";
            if (ticks == 0) return "";
            return $" for {Seconds(ticks)}";
        }

        private static string TargetPreference(TargetPref preference)
        {
            switch (preference)
            {
                case TargetPref.Nearest: return "Acquires the nearest enemy.";
                case TargetPref.Farthest: return "Acquires the farthest enemy.";
                case TargetPref.LowestHp: return "Acquires the lowest-HP enemy.";
                case TargetPref.HighestHp: return "Acquires the highest-HP enemy.";
                default:
                    throw new InvalidOperationException(
                        $"No display grammar for TargetPref.{preference}.");
            }
        }

        private static string Affects(Affects affects)
        {
            switch (affects)
            {
                case Warband.Sim.Affects.All: return "all occupants";
                case Warband.Sim.Affects.Allies: return "allied occupants";
                case Warband.Sim.Affects.Enemies: return "enemy occupants";
                default:
                    throw new InvalidOperationException($"No display grammar for Affects.{affects}.");
            }
        }

        private static string Seconds(int ticks)
        {
            decimal seconds = ticks / (decimal)TicksPerSecond;
            return seconds == decimal.Truncate(seconds)
                ? $"{seconds:0}s"
                : $"{seconds:0.0}s";
        }

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
        private static string PercentFp(int value) => $"{value / 10m:0.#}%";
        private static string SignedPercentFp(int value) =>
            value >= 0 ? $"+{value / 10m:0.#}%" : $"{value / 10m:0.#}%";

        private static string Ordinal(int value)
        {
            int abs = Math.Abs(value);
            int mod100 = abs % 100;
            string suffix = mod100 >= 11 && mod100 <= 13
                ? "th"
                : abs % 10 == 1 ? "st"
                : abs % 10 == 2 ? "nd"
                : abs % 10 == 3 ? "rd"
                : "th";
            return value + suffix;
        }

        private static void RequireClauses(IReadOnlyCollection<string> clauses, string name)
        {
            if (clauses.Count == 0)
                throw new InvalidOperationException($"{name} has no displayable mechanical rules.");
        }
    }
}
