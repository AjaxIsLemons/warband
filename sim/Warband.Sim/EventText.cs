using System;

namespace Warband.Sim
{
    /// <summary>
    /// One compact human line per <see cref="BattleEvent"/>, for the debug Event viewer — the
    /// scan-reading counterpart to <see cref="TellMatch"/> (which owns the VISUAL signature). Kept
    /// in the sim so it's headless-testable and shares the event model with everything else (no
    /// drift): the field semantics decoded here are exactly the ones documented on
    /// <see cref="EventKind"/>. Names are resolved through a caller callback (the client passes a
    /// fold lookup); the callback owns id fallbacks, so a null return just reads as "?".
    /// Zero UnityEngine refs; no allocations beyond the returned string.
    /// </summary>
    public static class EventText
    {
        /// <summary>A terse one-liner describing <paramref name="e"/>. <paramref name="name"/> maps a
        /// unit id to a display name (callers resolve -1/unknown to "storm"/"#id"/etc).</summary>
        public static string Describe(BattleEvent e, Func<int, string> name)
        {
            string Nm(int id) => name(id) ?? "?";
            string Hex(int q, int r) => $"({q},{r})";

            switch (e.Kind)
            {
                case EventKind.BattleStart:
                    return "Battle start";
                case EventKind.End:
                    return "Battle end";
                case EventKind.StormTick:
                    return "Storm tick";

                // Rule index, not name: the id table lives on the snapshot (BattleResult.RuleIds),
                // which this formatter has no handle on. The client resolves it off the fold.
                case EventKind.TriggerFired:
                    return $"Trigger: {Nm(e.Source)} rule #{e.Aux} → {Nm(e.Target)}";
                case EventKind.RuleChanged:
                    return e.Amount != 0
                        ? $"Rule ON: {Nm(e.Source)} rule #{e.Aux} ({e.Aux2:+#;-#;0})"
                        : $"Rule OFF: {Nm(e.Source)} rule #{e.Aux}";
                case EventKind.RuleProgress:
                    return $"Count: rule #{e.Aux} at {e.Amount}/{e.Aux2}";

                case EventKind.MoveStart:
                    return $"Step: {Nm(e.Source)} → {Hex(e.Amount, e.Aux)} ({e.Aux2}t)";
                case EventKind.Move:
                    return $"Move: {Nm(e.Source)} → {Hex(e.Amount, e.Aux)}";
                case EventKind.Leap:
                    return $"Leap: {Nm(e.Source)} {Hex(e.Aux2, e.Aux3)} → {Hex(e.Amount, e.Aux)}";

                case EventKind.Attack:
                    return $"Attack{CauseSuffix(e.Cause)}: {Nm(e.Source)} → {Nm(e.Target)}";

                case EventKind.DamageDealt:
                {
                    string crit = e.Crit ? " crit" : "";
                    string absorbed = e.Aux > 0 ? $" [{e.Aux} absorbed]" : "";
                    return $"Damage {e.Amount}{crit}{CauseSuffix(e.Cause)}: {Nm(e.Source)} → {Nm(e.Target)}{absorbed}";
                }

                case EventKind.Heal:
                    return $"Heal {e.Amount}: {Nm(e.Source)} → {Nm(e.Target)}";

                case EventKind.Cast:
                    return $"Cast: {Nm(e.Source)}";

                case EventKind.StatusApplied:
                {
                    var kind = (StatusKind)e.Aux;
                    string mag = e.Amount > 1 ? $" ×{e.Amount}" : "";
                    string from = e.Source >= 0 ? $"{Nm(e.Source)} → " : "";
                    return $"+{Lexicon.Of(kind).Name}{mag}: {from}{Nm(e.Target)}";
                }
                case EventKind.StatusExpired:
                    return $"-{Lexicon.Of((StatusKind)e.Aux).Name}: {Nm(e.Target)}";

                case EventKind.ShieldChanged:
                {
                    string from = e.Source >= 0 && e.Source != e.Target ? $"{Nm(e.Source)} → " : "";
                    return $"Shield {Signed(e.Amount)}: {from}{Nm(e.Target)}";
                }
                case EventKind.ManaChanged:
                    return $"Mana {Signed(e.Amount)}: {Nm(e.Target)}";

                case EventKind.Death:
                {
                    string by = e.Source < 0 ? "storm" : Nm(e.Source);
                    string over = e.Amount > 0 ? $", overkill {e.Amount}" : "";
                    return $"DEATH: {Nm(e.Target)} (by {by}{over})";
                }
                case EventKind.CheatDeath:
                    return $"CHEAT DEATH: {Nm(e.Target)}";

                case EventKind.FieldCreated:
                {
                    bool wall = e.Amount == 1;
                    string label = wall
                        ? (e.Flavor == FieldFlavor.Neutral ? "wall" : $"wall {e.Flavor}")
                        : e.Flavor.ToString();
                    string rad = e.Aux2 >= 0 ? $" r{e.Aux2}" : "";
                    string aura = e.Aux >= 0 ? " aura" : "";
                    return $"Field {label}{rad}{aura}: {Nm(e.Source)}";
                }
                case EventKind.FieldHex:
                    return $"Field hex {Hex(e.Amount, e.Aux)}";
                case EventKind.FieldExpired:
                    return "Field expired";

                case EventKind.AttackBlocked:
                    return $"BLOCKED: {Nm(e.Source)} → {Nm(e.Target)} (wall at {e.Amount},{e.Aux})";

                default:
                    return e.Kind.ToString();
            }
        }

        /// <summary>The default-hidden spam kinds — Move/Mana/FieldHex/BattleStart. The viewer's
        /// "noise" toggle keys off this so the loud, per-tick chatter can be folded away.</summary>
        public static bool IsNoise(BattleEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.Move:
                case EventKind.MoveStart:
                case EventKind.ManaChanged:
                case EventKind.FieldHex:
                case EventKind.BattleStart:
                    return true;
                default:
                    return false;
            }
        }

        // A parenthetical cause tag, shown only when it adds information (a plain auto-attack is the
        // silent default; Burn/Storm/Counter/Ability/etc. get named).
        private static string CauseSuffix(Cause cause)
            => cause == Cause.Attack || cause == Cause.None ? "" : $" ({Lexicon.Of(cause).Name})";

        private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();
    }
}
