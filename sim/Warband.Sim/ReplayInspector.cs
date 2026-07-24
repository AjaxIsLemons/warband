using System.Collections.Generic;
using System.Text;

namespace Warband.Sim
{
    /// <summary>
    /// Reads a replay's event log and reports every distinct PRESENTATION signature that
    /// appears — the render layer keys tells on more than <see cref="EventKind"/> alone
    /// (DamageDealt splits by <see cref="Cause"/> + crit, Status* by <see cref="StatusKind"/>,
    /// fields by wall/zone). This is the "what tells does this fight need?" tool: run it on a
    /// replay and you see exactly which event signatures must have an authored tell, with
    /// counts, amount ranges and tick spans. Pure + dependency-free so the offline generator
    /// AND the Unity client share one implementation (render-contract discipline).
    /// </summary>
    public static class ReplayInspector
    {
        /// <summary>A presentation-relevant grouping of events. Equality/hash are value-based
        /// so it works as a dictionary key.</summary>
        public readonly struct Signature
        {
            public readonly EventKind Kind;
            public readonly Cause Cause;       // meaningful for DamageDealt; None elsewhere
            public readonly StatusKind Status; // meaningful for Status* events
            public readonly bool Crit;         // a critical auto-attack DamageDealt
            public readonly bool Wall;         // FieldCreated that raised a wall

            public Signature(EventKind kind, Cause cause, StatusKind status, bool crit, bool wall)
            { Kind = kind; Cause = cause; Status = status; Crit = crit; Wall = wall; }

            /// <summary>The renderer-facing label for this signature (also the tuning key we'd
            /// author a tell against).</summary>
            public string Label()
            {
                switch (Kind)
                {
                    case EventKind.DamageDealt: return $"Damage/{Cause}{(Crit ? "/crit" : "")}";
                    case EventKind.StatusApplied: return $"Status+/{Status}";
                    case EventKind.StatusExpired: return $"Status-/{Status}";
                    case EventKind.FieldCreated: return $"Field/{(Wall ? "wall" : "zone")}";
                    default: return Kind.ToString();
                }
            }
        }

        public sealed class Stat
        {
            public Signature Sig;
            public int Count;
            public int MinAmount = int.MaxValue;
            public int MaxAmount = int.MinValue;
            public int FirstTick = int.MaxValue;
            public int LastTick = int.MinValue;
            public string Label => Sig.Label();
        }

        private static Signature SigOf(BattleEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.DamageDealt:
                    return new Signature(e.Kind, e.Cause, default, e.Crit, false);
                case EventKind.StatusApplied:
                case EventKind.StatusExpired:
                    return new Signature(e.Kind, Cause.None, (StatusKind)e.Aux, false, false);
                case EventKind.FieldCreated:
                    return new Signature(e.Kind, Cause.None, default, false, e.Amount == 1);
                default:
                    return new Signature(e.Kind, Cause.None, default, false, false);
            }
        }

        /// <summary>Fold the event log into per-signature stats. Deterministic order: by
        /// first appearance in the log (so the report reads chronologically-ish and stably).</summary>
        public static List<Stat> Summarize(IReadOnlyList<BattleEvent> events)
        {
            var byKey = new Dictionary<Signature, Stat>();
            var order = new List<Stat>();
            foreach (var e in events)
            {
                var sig = SigOf(e);
                if (!byKey.TryGetValue(sig, out var s))
                {
                    s = new Stat { Sig = sig };
                    byKey[sig] = s;
                    order.Add(s);
                }
                s.Count++;
                // Amount only carries meaning for value-bearing events; track it regardless —
                // meaningless kinds just report a flat 0..0 which is itself a readable signal.
                if (e.Amount < s.MinAmount) s.MinAmount = e.Amount;
                if (e.Amount > s.MaxAmount) s.MaxAmount = e.Amount;
                if (e.Tick < s.FirstTick) s.FirstTick = e.Tick;
                if (e.Tick > s.LastTick) s.LastTick = e.Tick;
            }
            return order;
        }

        /// <summary>A compact markdown coverage table for one replay.</summary>
        public static string Report(string name, IReadOnlyList<PlaybackUnit> initial, IReadOnlyList<BattleEvent> events)
        {
            var stats = Summarize(events);
            var sb = new StringBuilder();
            int endTick = events.Count > 0 ? events[events.Count - 1].Tick : 0;
            sb.AppendLine($"### {name} — {initial.Count} units, {events.Count} events, {endTick} ticks ({endTick / 10.0:0.0}s)");
            sb.AppendLine("| signature | count | amount | ticks |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var s in stats)
            {
                string amount = s.MinAmount == s.MaxAmount ? $"{s.MinAmount}" : $"{s.MinAmount}..{s.MaxAmount}";
                sb.AppendLine($"| {s.Label} | {s.Count} | {amount} | {s.FirstTick}..{s.LastTick} |");
            }
            return sb.ToString();
        }
    }
}
