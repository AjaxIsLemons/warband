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
        public string Choice => Compact;

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
            foreach (var (kind, mag) in node.SpawnStatuses)
                clauses.Add(StartStatus(kind, mag));
            foreach (var rule in node.StatRules)
                clauses.Add(StatRule(rule));
            if (TryRiposteSequence(node.Triggers, out string riposte))
                clauses.Add(riposte);
            else if (TryPhaseBurstChoice(node, out string phase))
                clauses.Add(phase);
            else
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

        /// <summary>
        /// Decision-sized node copy with access to the kit before and after the choice. Most rules
        /// can use the exact standalone grammar; deltas such as a line extension need both composed
        /// states so the sentence can name what changed without copying either value.
        /// </summary>
        public static MechanicalRule Node(SpecNode node, UnitDef before, UnitDef after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            MechanicalRule exact = Node(node);
            string choice;
            if (TryCounterReactionChoice(node, before, out choice) ||
                TryPhaseBurstChoice(node, out choice) ||
                TrySharedRiposteChoice(node, out choice) ||
                TryDeathFieldPatchChoice(node, before, after, out choice) ||
                TrySignatureDeltaChoice(node, before, after, out choice))
                return new MechanicalRule(exact.Change, choice, exact.Full);
            return new MechanicalRule(exact.Change, exact.Full, exact.Full);
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
                clauses.Add(StartStatus(kind, mag));
            foreach (var rule in trinket.StatRules)
                clauses.Add(StatRule(rule));
            foreach (var trigger in trinket.Triggers)
                clauses.Add(Trigger(trigger));
            RequireClauses(clauses, trinket.Name);
            return Rule(MechanicalChangeKind.Add, clauses);
        }

        public static MechanicalRule Inscription(InscriptionDef inscription)
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
        /// One muster: a rule that resolves ONCE at BattleStart against allies within a radius, so
        /// the set it catches is decided by deployment and nothing afterwards can change it
        /// (ADR 0014). The Cleric's Mercy Aura, the Phalanx's Unbroken Line and every rung of the
        /// Banneret's Company are all this shape.
        /// </summary>
        public readonly struct Muster
        {
            /// <summary>Reach in hexes, or <see cref="Unbounded"/> for "the whole warband" — a
            /// board-spanning radius is a different promise, not a very large ring.</summary>
            public readonly int Radius;
            /// <summary>What standing inside it buys, in the same grammar as the card.</summary>
            public readonly string Text;

            public Muster(int radius, string text) { Radius = radius; Text = text; }

            public const int Unbounded = -1;
            public bool IsUnbounded => Radius == Unbounded;
        }

        /// <summary>
        /// Every muster a composed unit brings to deployment, innermost reach first.
        ///
        /// This exists so the BOARD and the CARD cannot disagree. Deployment wants to draw the
        /// hexes a muster will catch, and the client is not allowed to infer that by pattern-
        /// matching triggers itself (render-contract law #1) — a kit that changed its reach would
        /// silently leave a lying ring behind. Both readouts resolve from the composed def here.
        ///
        /// Only UNCONDITIONAL BattleStart rules qualify: a conditional one is not a placement
        /// promise, because the player cannot see at deploy time whether it will hold.
        /// </summary>
        public static IReadOnlyList<Muster> Musters(UnitDef unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            // A radius that spans the board is authored as "everyone" (Last March writes 99), and
            // the two must not be conflated: one is a ring you can stand outside of.
            int boardSpan = Battle.BoardRows + Battle.BoardCols;

            var byRadius = new SortedDictionary<int, List<EffectDef>>();
            foreach (var trigger in unit.Triggers)
            {
                if (trigger == null || trigger.On != EventKind.BattleStart) continue;
                if (trigger.When.Count > 0) continue;
                foreach (var effect in trigger.Do)
                {
                    if (effect?.Select == null || effect.Select.Kind != SelKind.AlliesWithin) continue;
                    int radius = effect.Select.Range >= boardSpan ? Muster.Unbounded : effect.Select.Range;
                    if (!byRadius.TryGetValue(radius, out var effects))
                        byRadius[radius] = effects = new List<EffectDef>();
                    effects.Add(effect);
                }
            }

            // SortedDictionary puts Unbounded (-1) first; it belongs last, being the widest reach.
            var musters = byRadius.Where(pair => pair.Key != Muster.Unbounded)
                .Select(pair => new Muster(pair.Key, InlineEffects(pair.Value)))
                .ToList();
            if (byRadius.TryGetValue(Muster.Unbounded, out var all))
                musters.Add(new Muster(Muster.Unbounded, InlineEffects(all)));
            return musters;
        }

        /// <summary>
        /// The hexes a hero standing on <paramref name="center"/> offers to the rest of the warband
        /// — the seats its muster will catch — or an empty list if it has no placement promise.
        ///
        /// Only the OUTERMOST finite reach. A Banneret carrying Wide Banner musters at both r1 and
        /// r2, but two nested rings is a diagram, not a board read: which rung buys what is the
        /// card's job. An unbounded muster (Last March) offers no seats at all, because there is
        /// nowhere to stand that is outside it.
        ///
        /// Clipped to where a hero can actually be deployed. An unclipped radius around an edge hex
        /// reaches off the board and into the enemy half, and offering those would promise a seat
        /// nobody can ever take.
        /// </summary>
        public static IReadOnlyList<Hex> MusterSeats(UnitDef unit, Hex center)
        {
            int radius = -1;
            foreach (var muster in Musters(unit))
            {
                // An unbounded rung silences the ring outright rather than falling back to the
                // narrower one beneath it. Last March keeps its innate radius-1 muster, but once
                // the whole warband is the Company, drawing seven hexes would read as "stand here
                // to join" when standing anywhere joins.
                if (muster.IsUnbounded) return new List<Hex>();
                if (muster.Radius > radius) radius = muster.Radius;
            }
            if (radius < 0) return new List<Hex>();

            return Hex.Range(center, radius).Where(RunController.IsDeployable).ToList();
        }

        /// <summary>
        /// Exact innate/passive language for a composed unit. A hero may express a passive as an
        /// event trigger, a live stat rule, or both, so the overload keeps both authored channels
        /// visible without asking the Unity client to understand their grammar.
        /// </summary>
        public static string Passives(IReadOnlyList<Trigger> triggers)
        {
            if (triggers == null || triggers.Count == 0) return "No passive rule.";
            if (TryRiposteSequence(triggers, out string riposte)) return riposte;
            return string.Join(" ", triggers.Select(Trigger));
        }

        public static string Passives(IReadOnlyList<Trigger> triggers,
                                      IReadOnlyList<StatRule> statRules)
        {
            var clauses = new List<string>();
            if (triggers != null)
            {
                if (TryRiposteSequence(triggers, out string riposte))
                    clauses.Add(riposte);
                else
                    clauses.AddRange(triggers.Select(Trigger));
            }
            if (statRules != null) clauses.AddRange(statRules.Select(StatRule));
            return clauses.Count == 0 ? "No passive rule." : string.Join(" ", clauses);
        }

        public static string Trigger(Trigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (trigger.Do.Count == 0)
                throw new InvalidOperationException("A displayed trigger has no effects.");
            if (TryOverkillCarry(trigger, out string overkill)) return overkill;
            if (TryEngagedAttackBonus(trigger, out string engaged)) return engaged;
            if (TryKillLeap(trigger, out string killLeap)) return killLeap;
            if (TryDamageBonus(trigger, out string damageBonus)) return damageBonus;
            if (TryCritExecute(trigger, out string execute)) return execute;
            if (TryHitStatus(trigger, out string hitStatus)) return hitStatus;
            if (TryGlobalStatusFields(trigger, out string fields)) return fields;
            var remaining = trigger.When.ToList();
            string when = EventPhrase(trigger.On, remaining);
            if (trigger.EveryN > 1)
                when = $"every {Ordinal(trigger.EveryN)} time {when}";
            // Root guards prevent a rider from waking itself off an effect it just produced.
            // The player-facing event phrase ("basic attack", "Counter", "Signature") already
            // names the causal event; exposing cascade topology adds code language, not a choice.
            remaining.RemoveAll(condition =>
                condition.Kind == CondKind.IsRootEvent && !condition.Not);
            if (remaining.Count > 0)
                when += " if " + string.Join(" and ", remaining.Select(Condition));
            string prefix = trigger.On == EventKind.BattleStart ? "When" : "After";
            string tail = "";
            // Once-per-root is the UNIVERSAL Inscription default (ADR 0026) and would be tooltip
            // noise repeated twelve times; the Hourstone surface states the law once. It is spelled
            // out only on TriggerFired hooks, where echo semantics are exactly the question.
            if (trigger.OncePerRoot && trigger.On == EventKind.TriggerFired)
                tail = " At most once per chain of events.";
            return $"{prefix} {when}: {Effects(trigger.Do, trigger.On)}{tail}";
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

        private static bool TryCounterReactionChoice(
            SpecNode node, UnitDef before, out string choice)
        {
            choice = "";
            if (node.HpBonus != 0 || node.TargetPref.HasValue || node.Standoff.HasValue ||
                node.CleaveBonusPct != 0 || node.SpawnStatuses.Count > 0 ||
                node.StatRules.Count > 0 || node.SignatureOverride != null ||
                node.SignaturePatch != null || node.Triggers.Count != 2)
                return false;

            Trigger? incoming = node.Triggers.FirstOrDefault(IsIncomingBasicCounter);
            Trigger? leap = node.Triggers.FirstOrDefault(t => TryLeapCounterTaunt(t, out _, out _));
            if (incoming == null || leap == null ||
                !TryLeapCounterTaunt(leap, out int range, out int tauntTicks))
                return false;

            bool alreadyCounters = before.Triggers.Any(t =>
                t.Do.Any(e => e.Kind == EffectKind.Swing && e.AsCounter));
            string first = alreadyCounters
                ? "Gain an extra Counter against every basic attack targeting them."
                : "Counter every basic attack targeting them.";
            choice = $"{first} When an enemy Leaps within {range} " +
                     $"hex{(range == 1 ? "" : "es")}, Counter and Taunt it for " +
                     $"{Seconds(tauntTicks)}.";
            return true;
        }

        private static bool TryPhaseBurstChoice(SpecNode node, out string choice)
        {
            choice = "";
            if (node.Triggers.Count != 2 || node.StatRules.Count > 0 ||
                node.SpawnStatuses.Count > 0 || node.SignatureOverride != null ||
                node.SignaturePatch != null)
                return false;

            Trigger? burst = node.Triggers.FirstOrDefault(trigger =>
                trigger.On == EventKind.DamageDealt &&
                Has(trigger, CondKind.TargetIsOwner, false) &&
                trigger.When.Any(condition =>
                    condition.Kind == CondKind.OwnerHasStatus &&
                    condition.Status == StatusKind.Phase &&
                    condition.Not));
            Trigger? exit = node.Triggers.FirstOrDefault(trigger =>
                trigger.On == EventKind.StatusExpired &&
                Has(trigger, CondKind.TargetIsOwner, false) &&
                trigger.When.Any(condition =>
                    condition.Kind == CondKind.StatusIs &&
                    condition.Status == StatusKind.Phase &&
                    !condition.Not));
            Cond? threshold = burst?.When.FirstOrDefault(condition =>
                condition.Kind == CondKind.OwnerRecentDamageAbovePct && !condition.Not);
            EffectDef? phase = burst?.Do.SingleOrDefault(effect =>
                effect.Kind == EffectKind.ApplyStatus &&
                effect.Status == StatusKind.Phase &&
                effect.Select.Kind == SelKind.Self);
            EffectDef? leap = exit?.Do.FirstOrDefault();
            EffectDef? empower = exit?.Do.Skip(1).SingleOrDefault();
            if (threshold == null || phase == null || phase.StatusTicks <= 0 ||
                leap == null || leap.Kind != EffectKind.Leap ||
                leap.Select.Kind != SelKind.FarthestEnemy ||
                empower == null || empower.Kind != EffectKind.ApplyStatus ||
                empower.Status != StatusKind.AttackUp ||
                empower.Select.Kind != SelKind.Self)
                return false;

            choice =
                $"After taking at least {threshold.Amount}% maximum HP as damage within " +
                $"{Seconds(Battle.RecentWindow)}, gain Phase for {Seconds(phase.StatusTicks)}. " +
                $"When Phase ends, Leap to the farthest enemy and gain Empowered " +
                $"{empower.Amount} for the fight.";
            return true;
        }

        private static bool TrySharedRiposteChoice(SpecNode node, out string choice)
        {
            choice = "";
            if (node.StatRules.Count > 0 ||
                node.SpawnStatuses.Count > 0 || node.SignatureOverride != null ||
                node.SignaturePatch != null)
                return false;
            return TryRiposteSequence(node.Triggers, out choice);
        }

        /// <summary>
        /// Riposte is authored as three independent engine triggers: grant at battle start,
        /// grant on cast, then Counter-and-spend-one on an incoming attack. Present the one player
        /// rule those triggers jointly implement.
        /// </summary>
        private static bool TryRiposteSequence(
            IReadOnlyList<Trigger> triggers, out string copy)
        {
            copy = "";
            if (triggers == null || triggers.Count != 3) return false;

            Trigger? start = triggers.FirstOrDefault(
                trigger => trigger.On == EventKind.BattleStart);
            Trigger? cast = triggers.FirstOrDefault(
                trigger => trigger.On == EventKind.Cast &&
                           Has(trigger, CondKind.SourceIsOwner, false));
            Trigger? attacked = triggers.FirstOrDefault(
                trigger => trigger.On == EventKind.Attack);
            EffectDef? startCharge = start?.Do.SingleOrDefault();
            EffectDef? castCharge = cast?.Do.SingleOrDefault();
            if (start == null || start.When.Count != 0 ||
                startCharge == null || castCharge == null ||
                startCharge.Kind != EffectKind.ApplyStatus ||
                startCharge.Status != StatusKind.CounterCharge ||
                castCharge.Kind != EffectKind.ApplyStatus ||
                castCharge.Status != StatusKind.CounterCharge ||
                startCharge.Amount != castCharge.Amount ||
                !SameSelector(startCharge.Select, castCharge.Select) ||
                attacked == null || attacked.Do.Count != 2 ||
                !Has(attacked, CondKind.IsRootEvent, false) ||
                !IsCounterAtEventSource(attacked.Do[0]) ||
                attacked.Do[1].Kind != EffectKind.RemoveStatus ||
                attacked.Do[1].Status != StatusKind.CounterCharge ||
                attacked.Do[1].Amount != 1)
                return false;

            string recipient;
            if (startCharge.Select.Kind == SelKind.Self &&
                Has(attacked, CondKind.TargetIsOwner, false) &&
                Has(attacked, CondKind.OwnerHasStatus, false) &&
                attacked.Do[1].Select.Kind == SelKind.Self)
                recipient = "";
            else if (startCharge.Select.Kind == SelKind.AlliesWithin &&
                     Has(attacked, CondKind.TargetIsAllyOfOwner, false) &&
                     Has(attacked, CondKind.TargetHasStatus, false) &&
                     attacked.Do[1].Select.Kind == SelKind.EventTarget)
            {
                int range = startCharge.Select.Range;
                recipient = $"Allies within {range} hex{(range == 1 ? "" : "es")} ";
            }
            else
                return false;

            copy =
                $"Combat start or Signature cast: {recipient}" +
                $"{(recipient.Length == 0 ? "Gain" : "gain")} " +
                $"{startCharge.Amount} Riposte.";
            return true;
        }

        private static bool TryOverkillCarry(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.Death ||
                trigger.When.Count != 1 ||
                !Has(trigger, CondKind.SourceIsOwner, false) ||
                trigger.Do.Count != 1)
                return false;
            EffectDef effect = trigger.Do[0];
            if (effect.Kind != EffectKind.Damage ||
                effect.PctOfEventAmount <= 0 ||
                effect.Select.Kind != SelKind.NearestEnemy ||
                !effect.Select.AnchorEventTarget ||
                !effect.Select.ExcludeAnchorUnit)
                return false;

            string amount = effect.PctOfEventAmount == 100
                ? "excess damage"
                : $"{effect.PctOfEventAmount}% of excess damage";
            copy = $"On kill: Deal {amount} to the enemy nearest the corpse.";
            return true;
        }

        private static bool TryEngagedAttackBonus(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.DamageDealt ||
                trigger.When.Count != 4 ||
                !Has(trigger, CondKind.SourceIsOwner, false) ||
                !trigger.When.Any(condition =>
                    condition.Kind == CondKind.CauseIs &&
                    !condition.Not &&
                    condition.Cause == Cause.Attack) ||
                !Has(trigger, CondKind.TargetAdjacentToAllyOfOwner, false) ||
                !Has(trigger, CondKind.IsRootEvent, false) ||
                trigger.Do.Count != 1)
                return false;
            EffectDef effect = trigger.Do[0];
            if (effect.Kind != EffectKind.Damage ||
                effect.Select.Kind != SelKind.EventTarget ||
                effect.PctOfEventAmount <= 0)
                return false;
            copy =
                $"Basic attacks deal +{effect.PctOfEventAmount}% damage to enemies " +
                "adjacent to an ally.";
            return true;
        }

        private static bool TryKillLeap(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.Death ||
                trigger.When.Count != 1 ||
                !Has(trigger, CondKind.SourceIsOwner, false) ||
                trigger.Do.Count != 1)
                return false;
            EffectDef effect = trigger.Do[0];
            if (effect.Kind != EffectKind.Leap ||
                effect.Select.Kind != SelKind.FarthestEnemy)
                return false;
            copy = "On kill: Leap beside the farthest enemy.";
            return true;
        }

        private static bool TryDamageBonus(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.DamageDealt || trigger.Do.Count != 1)
                return false;
            var remaining = trigger.When.ToList();
            if (Take(remaining, CondKind.SourceIsOwner, false) == null)
                return false;
            Cond? cause = Take(remaining, CondKind.CauseIs, false);
            if (cause == null ||
                (cause.Cause != Cause.Attack &&
                 cause.Cause != Cause.Ability &&
                 cause.Cause != Cause.Counter))
                return false;
            Take(remaining, CondKind.IsRootEvent, false);

            EffectDef effect = trigger.Do[0];
            if (effect.Kind != EffectKind.Damage ||
                effect.Select.Kind != SelKind.EventTarget ||
                effect.PctOfEventAmount <= 0)
                return false;

            string gate = "";
            Cond? below = Take(remaining, CondKind.TargetBelowHpPct, false);
            Cond? targetStatus = Take(remaining, CondKind.TargetHasStatus, false);
            Cond? ownerStatus = Take(remaining, CondKind.OwnerHasStatus, false);
            Cond? exactRange = Take(remaining, CondKind.TargetAtRangeOfOwner, false);
            if (below != null)
                gate = $" against enemies below {below.Amount}% HP";
            if (targetStatus != null)
                gate += $" against enemies with {Lexicon.Of(targetStatus.Status).Name}";
            if (ownerStatus != null)
                gate += $" while this champion has {Lexicon.Of(ownerStatus.Status).Name}";
            if (exactRange != null)
                gate += $" at exactly {exactRange.Amount} " +
                        $"hex{(exactRange.Amount == 1 ? "" : "es")}";
            if (remaining.Count != 0) return false;

            string hit = cause.Cause == Cause.Attack
                ? "Basic-attack hits"
                : cause.Cause == Cause.Ability
                    ? "Signature hits"
                    : "Counter hits";
            copy = $"{hit}{gate} deal +{effect.PctOfEventAmount}% damage.";
            return true;
        }

        private static bool TryCritExecute(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.DamageDealt ||
                trigger.Do.Count != 1 ||
                trigger.Do[0].Kind != EffectKind.Execute ||
                trigger.Do[0].Select.Kind != SelKind.EventTarget)
                return false;
            var remaining = trigger.When.ToList();
            if (Take(remaining, CondKind.SourceIsOwner, false) == null)
                return false;
            Cond? cause = Take(remaining, CondKind.CauseIs, false);
            Cond? crit = Take(remaining, CondKind.IsCrit, false);
            Cond? below = Take(remaining, CondKind.TargetBelowHpPct, false);
            Take(remaining, CondKind.IsRootEvent, false);
            if (cause == null || cause.Cause != Cause.Attack ||
                crit == null || below == null || remaining.Count != 0)
                return false;
            copy =
                $"Basic-attack crits Execute enemies below {below.Amount}% HP.";
            return true;
        }

        private static bool TryHitStatus(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.DamageDealt ||
                trigger.Do.Count != 1)
                return false;
            EffectDef effect = trigger.Do[0];
            if (effect.Kind != EffectKind.ApplyStatus ||
                effect.Select.Kind != SelKind.EventTarget)
                return false;

            var remaining = trigger.When.ToList();
            if (Take(remaining, CondKind.SourceIsOwner, false) == null)
                return false;
            Cond? cause = Take(remaining, CondKind.CauseIs, false);
            if (cause == null ||
                (cause.Cause != Cause.Attack &&
                 cause.Cause != Cause.Ability &&
                 cause.Cause != Cause.Counter))
                return false;
            Take(remaining, CondKind.IsRootEvent, false);
            bool crit = Take(remaining, CondKind.IsCrit, false) != null;
            if (remaining.Count != 0) return false;

            string hit = cause.Cause == Cause.Attack
                ? crit ? "Basic-attack crits" : "Basic-attack hits"
                : cause.Cause == Cause.Ability
                    ? "Signature hits"
                    : "Counter hits";
            copy = $"{hit}: Apply {Status(effect.Status, effect.Amount)}" +
                   Duration(effect.StatusTicks, effect.StatusSwings, false) + ".";
            return true;
        }

        private static bool TryGlobalStatusFields(Trigger trigger, out string copy)
        {
            copy = "";
            if (trigger.On != EventKind.Cast ||
                trigger.When.Count != 1 ||
                !Has(trigger, CondKind.SourceIsOwner, false) ||
                trigger.Do.Count != 1)
                return false;
            EffectDef create = trigger.Do[0];
            if (create.Kind != EffectKind.CreateField ||
                create.Field == null ||
                create.Select.Kind != SelKind.EnemiesWithin ||
                create.Select.Range < Battle.BoardRows + Battle.BoardCols ||
                !create.Select.MustHave.HasValue ||
                create.Field.Pulse.Count != 1)
                return false;
            EffectDef pulse = create.Field.Pulse[0];
            if (pulse.Kind != EffectKind.ApplyStatus)
                return false;

            string fieldShape = create.Field.Radius <= 0
                ? "single-hex fields"
                : $"radius-{create.Field.Radius} fields";
            string duration = create.Field.Ticks < 0
                ? "lasting for the fight"
                : $"lasting {Seconds(create.Field.Ticks)}";
            copy =
                $"On Signature: Create {fieldShape} {duration} beneath every enemy with " +
                $"{Lexicon.Of(create.Select.MustHave.Value).Name}; each second, apply " +
                $"{Status(pulse.Status, pulse.Amount)} to enemies inside.";
            return true;
        }

        private static bool TryDeathFieldPatchChoice(
            SpecNode node, UnitDef before, UnitDef after, out string choice)
        {
            choice = "";
            if (node.SignaturePatch == null || node.SignatureOverride != null ||
                node.Triggers.Count != 1 || node.StatRules.Count > 0 ||
                node.SpawnStatuses.Count > 0 ||
                !TryFieldRadius(before.Signature, out int beforeRadius) ||
                !TryFieldRadius(after.Signature, out int afterRadius) ||
                beforeRadius == afterRadius)
                return false;

            Trigger trigger = node.Triggers[0];
            Cond? statusGate = trigger.When.FirstOrDefault(condition =>
                condition.Kind == CondKind.TargetHasStatus && !condition.Not);
            EffectDef? create = trigger.Do.SingleOrDefault();
            if (trigger.On != EventKind.Death || statusGate == null ||
                create == null || create.Kind != EffectKind.CreateField ||
                create.Select.Kind != SelKind.EventTarget || create.Field == null ||
                create.Field.Pulse.Count != 1)
                return false;
            EffectDef pulse = create.Field.Pulse[0];
            if (pulse.Kind != EffectKind.ApplyStatus)
                return false;

            string ability = AbilityIdentity.DisplayName(
                AbilityIdentity.Resolve(before.ChassisId, before.Traits));
            string possessive = ability.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? ability + "'"
                : ability + "'s";
            string fieldShape = create.Field.Radius <= 0
                ? "a single-hex field"
                : $"a radius-{create.Field.Radius} field";
            choice =
                $"{possessive} field radius expands from {beforeRadius} to {afterRadius} hexes. " +
                $"When an enemy with {Lexicon.Of(statusGate.Status).Name} dies, create " +
                $"{fieldShape} on its hex for {Seconds(create.Field.Ticks)}; each second it " +
                $"applies {Status(pulse.Status, pulse.Amount)} to enemies inside.";
            return true;
        }

        private static bool IsIncomingBasicCounter(Trigger trigger) =>
            trigger.On == EventKind.Attack &&
            Has(trigger, CondKind.TargetIsOwner, false) &&
            Has(trigger, CondKind.IsRootEvent, false) &&
            trigger.Do.Count == 1 &&
            IsCounterAtEventSource(trigger.Do[0]);

        private static bool TryLeapCounterTaunt(
            Trigger trigger, out int range, out int tauntTicks)
        {
            range = 0;
            tauntTicks = 0;
            if (trigger.On != EventKind.Leap ||
                !Has(trigger, CondKind.SourceIsEnemyOfOwner, false))
                return false;
            Cond? within = trigger.When.FirstOrDefault(
                c => c.Kind == CondKind.SourceWithinHexesOfOwner && !c.Not);
            if (within == null || trigger.Do.Count != 2 ||
                !IsCounterAtEventSource(trigger.Do[0]))
                return false;
            EffectDef taunt = trigger.Do[1];
            if (taunt.Kind != EffectKind.ApplyStatus ||
                taunt.Status != StatusKind.Taunt ||
                taunt.Select.Kind != SelKind.EventSource ||
                taunt.StatusTicks <= 0)
                return false;
            range = within.Amount;
            tauntTicks = taunt.StatusTicks;
            return true;
        }

        private static bool IsCounterAtEventSource(EffectDef effect) =>
            effect.Kind == EffectKind.Swing &&
            effect.AsCounter &&
            effect.Select.Kind == SelKind.EventSource &&
            (effect.Amount == 0 || effect.Amount == 100);

        private static bool Has(Trigger trigger, CondKind kind, bool not) =>
            trigger.When.Any(condition => condition.Kind == kind && condition.Not == not);

        private static bool TrySignatureDeltaChoice(
            SpecNode node, UnitDef before, UnitDef after, out string choice)
        {
            choice = "";
            if (node.SignaturePatch == null || node.SignatureOverride != null ||
                node.HpBonus != 0 || node.TargetPref.HasValue || node.Standoff.HasValue ||
                node.CleaveBonusPct != 0 || node.SpawnStatuses.Count > 0 ||
                node.StatRules.Count > 0 || node.Triggers.Count > 0)
                return false;

            string abilityId = AbilityIdentity.Resolve(before.ChassisId, before.Traits);
            string ability = AbilityIdentity.DisplayName(abilityId);
            string possessive = ability.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? ability + "'"
                : ability + "'s";

            if (TryLineRange(before.Signature, out int beforeLine) &&
                TryLineRange(after.Signature, out int afterLine) &&
                beforeLine != afterLine)
            {
                choice = beforeLine > 0 && afterLine > 0
                    ? $"{possessive} line extends from {beforeLine} to {afterLine} hexes."
                    : afterLine <= 0
                        ? $"{possessive} line extends from {beforeLine} hexes to board length."
                        : $"{possessive} line changes from board length to {afterLine} hexes.";
                return true;
            }

            if (TryRadius(before.Signature, out int beforeRadius) &&
                TryRadius(after.Signature, out int afterRadius) &&
                beforeRadius != afterRadius)
            {
                choice = $"{possessive} radius changes from {beforeRadius} to {afterRadius} " +
                         $"hex{(afterRadius == 1 ? "" : "es")}.";
                return true;
            }

            SignaturePatch patch = node.SignaturePatch;
            if (patch.Repeat > 1 &&
                patch.RadiusDelta == 0 &&
                !patch.LineRange.HasValue &&
                patch.AmountPct == 100 &&
                !patch.Escalate.HasValue &&
                !patch.FieldRadius.HasValue &&
                !patch.FieldTicks.HasValue &&
                patch.Add.Count == 0)
            {
                choice = $"{ability} resolves {patch.Repeat} times.";
                return true;
            }

            // Still exact and data-bound when a patch changes damage, repetition, fields, or
            // escalation rather than one of the common geometry axes.
            choice = $"{ability} now: {Signature(after.Signature)}";
            return true;
        }

        private static bool TryLineRange(IReadOnlyList<EffectDef> effects, out int range)
        {
            foreach (EffectDef effect in effects)
                if (effect.Select.Kind == SelKind.EnemiesOnLineThroughTarget ||
                    effect.Select.Kind == SelKind.EnemiesOnLineThroughFarthest)
                {
                    range = effect.Select.Range;
                    return true;
                }
            range = 0;
            return false;
        }

        private static bool TryRadius(IReadOnlyList<EffectDef> effects, out int range)
        {
            foreach (EffectDef effect in effects)
                if (effect.Select.Kind == SelKind.AlliesWithin ||
                    effect.Select.Kind == SelKind.EnemiesWithin)
                {
                    range = effect.Select.Range;
                    return true;
                }
            range = 0;
            return false;
        }

        private static bool TryFieldRadius(IReadOnlyList<EffectDef> effects, out int range)
        {
            foreach (EffectDef effect in effects)
                if (effect.Kind == EffectKind.CreateField && effect.Field != null)
                {
                    range = effect.Field.Radius;
                    return true;
                }
            range = 0;
            return false;
        }

        private static string Effects(IReadOnlyList<EffectDef> effects,
                                      EventKind? sourceEvent = null)
        {
            var clauses = new List<string>();
            for (int i = 0; i < effects.Count; i++)
            {
                EffectDef effect = effects[i];
                if (i + 1 < effects.Count &&
                    effects[i + 1].Kind == EffectKind.ApplyStatus &&
                    SameSelector(effect.Select, effects[i + 1].Select))
                {
                    string first = Effect(effect, sourceEvent).TrimEnd('.');
                    string rider = Effect(
                        effects[i + 1], sourceEvent, "it").TrimEnd('.');
                    clauses.Add(first + ", then " + LowerFirst(rider) + ".");
                    i++;
                    continue;
                }
                clauses.Add(Effect(effect, sourceEvent));
            }
            return string.Join(" Then ", clauses);
        }

        private static bool SameSelector(Selector a, Selector b) =>
            a.Kind == b.Kind &&
            a.Range == b.Range &&
            a.ExcludeSelf == b.ExcludeSelf &&
            a.AnchorEvent == b.AnchorEvent &&
            a.AnchorEventTarget == b.AnchorEventTarget &&
            a.ExcludeAnchorUnit == b.ExcludeAnchorUnit &&
            a.SkipCtxTarget == b.SkipCtxTarget &&
            a.BelowHpPct == b.BelowHpPct &&
            a.MustHave == b.MustHave &&
            a.AdjacentToAlly == b.AdjacentToAlly;

        private static string LowerFirst(string value) =>
            string.IsNullOrEmpty(value)
                ? value
                : char.ToLowerInvariant(value[0]) + value.Substring(1);

        private static string Effect(EffectDef effect, EventKind? sourceEvent,
                                     string? targetOverride = null)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            string target = targetOverride ?? Selector(effect.Select, sourceEvent);
            string amount;
            if (effect.PctOfEventAmount > 0)
                amount = sourceEvent == EventKind.DamageDealt
                    ? $"{effect.PctOfEventAmount}% of that damage"
                    : sourceEvent == EventKind.Death
                        ? $"{effect.PctOfEventAmount}% of excess damage"
                        : $"{effect.PctOfEventAmount}% of the triggering amount";
            else if (effect.ScaleByTargetStatus || effect.ScaleByEventTargetStatus)
                amount = $"{effect.Amount} per {Lexicon.Of(effect.ScaleStatus).Name} stack";
            else
                amount = effect.Amount.ToString();

            string clause;
            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    clause = effect.PctOfEventAmount > 0
                        ? $"Deal {amount} to {target}."
                        : $"Deal {amount} damage to {target}.";
                    break;
                case EffectKind.Heal:
                    clause = $"Heal {target} for {amount}.";
                    break;
                case EffectKind.ApplyStatus:
                    // Swearing in the Company is an act, not a debuff landing: "Apply Mustered 1"
                    // is the tag talking, and the tag is an implementation detail of the roster.
                    if (effect.Status == StatusKind.Mustered)
                        // A captain is never in his own Company, so the generic exclusion suffix
                        // is noise on this one clause and nowhere else.
                        clause = $"Swear {target.Replace(", excluding this champion", "")} " +
                                 "into the Company for the fight.";
                    else
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
                        ? pct == 100
                            ? $"Counter {target}."
                            : $"Counter {target} for {pct}% basic-attack damage."
                        : $"Swing at {target} for {pct}% basic-attack damage.";
                    break;
                case EffectKind.Execute:
                    clause = $"Execute {target}.";
                    break;
                case EffectKind.RemoveStatus:
                    clause = effect.Amount > 0
                        ? $"Spend {effect.Amount} {Lexicon.Of(effect.Status).Name} from {target}."
                        : $"Remove all {Lexicon.Of(effect.Status).Name} from {target}.";
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

        private static string Selector(Selector selector, EventKind? sourceEvent = null)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            // The Company is a roster, not a radius. Say its name rather than reading out the
            // tag-and-99-hexes machinery that implements it — "allies within 99 hexes, with
            // Mustered" describes the code, not the promise the player is choosing between.
            if (selector.Kind == SelKind.AlliesWithin && selector.MustHave == StatusKind.Mustered)
                return selector.BelowHpPct > 0
                    ? $"Company members below {selector.BelowHpPct}% HP"
                    : "the Company";

            int boardSpan = Battle.BoardRows + Battle.BoardCols;
            string value;
            bool allAllies = selector.Kind == SelKind.AlliesWithin &&
                             selector.Range >= boardSpan &&
                             !selector.MustHave.HasValue;
            bool allEnemies = selector.Kind == SelKind.EnemiesWithin &&
                              selector.Range >= boardSpan;
            if (allAllies)
                value = selector.ExcludeSelf ? "all other allies" : "all allies";
            else if (allEnemies)
                value = selector.MustHave.HasValue
                    ? $"every enemy with {Lexicon.Of(selector.MustHave.Value).Name}"
                    : "all enemies";
            else switch (selector.Kind)
            {
                case SelKind.Self: value = "this champion"; break;
                case SelKind.EventSource: value = "the source"; break;
                case SelKind.EventTarget: value = "the target"; break;
                case SelKind.CurrentTarget: value = "the target"; break;
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
                        ? "enemies on the board-length line through the target"
                        : $"enemies on the {selector.Range}-hex line through the target";
                    break;
                case SelKind.EnemiesOnLineThroughFarthest:
                    value = selector.Range <= 0
                        ? "enemies on the board-length line through the farthest enemy"
                        : $"enemies on the {selector.Range}-hex line through the farthest enemy";
                    break;
                default:
                    throw new InvalidOperationException($"No display grammar for SelKind.{selector.Kind}.");
            }

            if (selector.AnchorEventTarget)
            {
                string anchor = sourceEvent == EventKind.Death ? "the corpse" : "the target";
                switch (selector.Kind)
                {
                    case SelKind.NearestEnemy:
                        value = $"the enemy nearest {anchor}";
                        break;
                    case SelKind.AlliesWithin:
                        value = $"allies within {selector.Range} " +
                                $"hex{(selector.Range == 1 ? "" : "es")} of {anchor}";
                        break;
                    case SelKind.EnemiesWithin:
                        value = $"enemies within {selector.Range} " +
                                $"hex{(selector.Range == 1 ? "" : "es")} of {anchor}";
                        break;
                    default:
                        value += $" from {anchor}";
                        break;
                }
            }
            else if (selector.AnchorEvent) value += ", measured from the triggering source";
            if (selector.ExcludeSelf && !allAllies) value += ", excluding this champion";
            if (selector.ExcludeAnchorUnit && sourceEvent != EventKind.Death)
                value += ", excluding the target";
            if (selector.SkipCtxTarget) value += ", beyond the current target only";
            if (selector.BelowHpPct > 0) value += $", below {selector.BelowHpPct}% HP";
            if (selector.MustHave.HasValue && !allEnemies)
                value += $", with {Lexicon.Of(selector.MustHave.Value).Name}";
            if (selector.AdjacentToAlly) value += ", standing beside an ally";
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
                    value = $"this champion took at least {condition.Amount}% of maximum HP " +
                            $"as damage within {Seconds(Battle.RecentWindow)}"; break;
                case CondKind.IsRootEvent: value = "this is the original event, not a triggered echo"; break;
                case CondKind.AnyEnemyHasStatus:
                    value = $"any enemy has {Lexicon.Of(condition.Status).Name}"; break;
                case CondKind.TargetInFieldOfOwner:
                    value = "the target stands in this champion's field"; break;
                case CondKind.TargetIsEnemyOfOwner:
                    value = "the target is an enemy"; break;
                case CondKind.EventRuleIsTeamRule:
                    value = "the rule that fired is inscribed in the Hourstone"; break;
                default:
                    throw new InvalidOperationException(
                        $"No display grammar for CondKind.{condition.Kind}.");
            }
            if (!condition.Not) return value;
            switch (condition.Kind)
            {
                case CondKind.SourceIsEnemyOfOwner: return "the source is an ally";
                case CondKind.TargetIsAllyOfOwner: return "the target is an enemy";
                case CondKind.TargetIsEnemyOfOwner: return "the target is allied";
                case CondKind.CauseIs: return $"the event did not come from {CausePhrase(condition.Cause)}";
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
                clauses.Add($"radius {Signed(patch.RadiusDelta)}");
            if (patch.LineRange.HasValue)
                clauses.Add(patch.LineRange.Value <= 0
                    ? "line becomes board-length"
                    : $"line becomes {patch.LineRange.Value} hexes");
            if (patch.AmountPct != 100)
                clauses.Add($"all magnitudes become {patch.AmountPct}%");
            if (patch.Escalate.HasValue)
                clauses.Add($"each later target gains {patch.Escalate.Value}% effect");
            if (patch.FieldRadius.HasValue)
                clauses.Add($"field radius becomes {patch.FieldRadius.Value}");
            if (patch.FieldTicks.HasValue)
                clauses.Add(patch.FieldTicks.Value < 0
                    ? "fields last for the fight"
                    : $"fields last {Seconds(patch.FieldTicks.Value)}");
            if (patch.Repeat > 1)
                clauses.Add($"resolve {patch.Repeat} times");
            if (patch.Add.Count > 0)
                clauses.Add("add " + InlineEffects(patch.Add));
            RequireClauses(clauses, "signature patch");
            return "Signature changes: " + string.Join("; ", clauses) + ".";
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
                    Cond? nthSwing = Take(conditions, CondKind.EveryNthSwingOfOwner, false);
                    sourceOwner = Take(conditions, CondKind.SourceIsOwner, false);
                    targetOwner = Take(conditions, CondKind.TargetIsOwner, false);
                    targetSide = Take(conditions, CondKind.TargetIsAllyOfOwner);
                    if (sourceOwner != null)
                        return nthSwing == null
                            ? "this champion begins a basic attack"
                            : $"this champion begins every {Ordinal(nthSwing.Amount)} basic attack";
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

                case EventKind.TriggerFired:
                    // Living Inscription's hook: fold the team-rule condition into the phrase.
                    return Take(conditions, CondKind.EventRuleIsTeamRule, false) != null
                        ? "a law of the Hourstone activates"
                        : "a passive rule fires";

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
                case EventKind.TriggerFired: return "a passive rule fires";
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
                    return magnitude == 0 ? name : $"{name} {PercentFp(magnitude)}";
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

        private static string StartStatus(StatusKind kind, int magnitude)
        {
            if (kind == StatusKind.CritUp)
                return $"Start with {Lexicon.Of(kind).Name} " +
                       $"(+{magnitude} critical chance).";
            return $"Start with {Status(kind, magnitude)}.";
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
