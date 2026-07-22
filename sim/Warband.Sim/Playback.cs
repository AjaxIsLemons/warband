using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>The client-facing view of one unit — exactly what a renderer shows.</summary>
    public sealed class PlaybackUnit
    {
        public int Id;
        public int Team;
        public string Name = "";
        public int MaxHp;
        public int Hp;
        public int Shield;
        public int Mana;
        public int ManaMax;
        public Hex Pos;
        public bool Dead;
        public List<(StatusKind Kind, int Mag)> Statuses = new List<(StatusKind, int)>();

        public PlaybackUnit Clone() => new PlaybackUnit
        {
            Id = Id, Team = Team, Name = Name, MaxHp = MaxHp, Hp = Hp, Shield = Shield,
            Mana = Mana, ManaMax = ManaMax, Pos = Pos, Dead = Dead,
            Statuses = new List<(StatusKind, int)>(Statuses),
        };
    }

    /// <summary>
    /// The log-reconstruction fold (render-contract.md): (initial snapshot, events) →
    /// per-tick view state. This IS the view-model — the sim-side guardrail test, the
    /// metrics fold, and the renderer all consume this one implementation. It applies
    /// absolutes carried on events and NEVER runs combat logic.
    /// </summary>
    public sealed class PlaybackState
    {
        public List<PlaybackUnit> Units = new List<PlaybackUnit>();
        public List<PlaybackField> Fields = new List<PlaybackField>();
        public int Tick;
        private int _next; // index into the event list

        public static PlaybackState From(IEnumerable<PlaybackUnit> initial)
        {
            var s = new PlaybackState();
            foreach (var u in initial)
                s.Units.Add(u.Clone());
            return s;
        }

        public PlaybackUnit? ById(int id)
        {
            foreach (var u in Units)
                if (u.Id == id)
                    return u;
            return null;
        }

        /// <summary>Apply every event with Tick ≤ tick, in log order.</summary>
        public void AdvanceToTick(List<BattleEvent> events, int tick)
        {
            while (_next < events.Count && events[_next].Tick <= tick)
                Apply(events[_next++]);
            Tick = tick;
        }

        private void Apply(BattleEvent e)
        {
            var src = ById(e.Source);
            var tgt = ById(e.Target);
            switch (e.Kind)
            {
                case EventKind.Move:
                    if (src != null) src.Pos = new Hex(e.Amount, e.Aux);
                    break;
                case EventKind.DamageDealt:
                    if (tgt != null)
                    {
                        if (e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                        if (e.PostShield != BattleEvent.Unset) tgt.Shield = e.PostShield;
                    }
                    break;
                case EventKind.Heal:
                    if (tgt != null && e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                    break;
                case EventKind.ShieldChanged:
                    if (tgt != null && e.PostShield != BattleEvent.Unset) tgt.Shield = e.PostShield;
                    break;
                case EventKind.ManaChanged:
                    if (tgt != null && e.PostMana != BattleEvent.Unset) tgt.Mana = e.PostMana;
                    break;
                case EventKind.Cast:
                    if (src != null && e.PostMana != BattleEvent.Unset) src.Mana = e.PostMana;
                    break;
                case EventKind.StatusApplied:
                    tgt?.Statuses.Add(((StatusKind)e.Aux, e.Amount));
                    break;
                case EventKind.StatusExpired:
                    if (tgt != null)
                        for (int i = 0; i < tgt.Statuses.Count; i++)
                            if (tgt.Statuses[i].Kind == (StatusKind)e.Aux && tgt.Statuses[i].Mag == e.Amount)
                            {
                                tgt.Statuses.RemoveAt(i);
                                break;
                            }
                    break;
                case EventKind.Death:
                    if (tgt != null)
                    {
                        tgt.Dead = true;
                        if (e.PostHp != BattleEvent.Unset) tgt.Hp = e.PostHp;
                    }
                    break;
                case EventKind.FieldCreated:
                    Fields.Add(new PlaybackField { Id = e.Target, IsWall = e.Amount == 1 });
                    break;
                case EventKind.FieldHex:
                    for (int i = 0; i < Fields.Count; i++)
                        if (Fields[i].Id == e.Target)
                        {
                            Fields[i].Hexes.Add(new Hex(e.Amount, e.Aux));
                            break;
                        }
                    break;
                case EventKind.FieldExpired:
                    for (int i = 0; i < Fields.Count; i++)
                        if (Fields[i].Id == e.Target)
                        {
                            Fields.RemoveAt(i);
                            break;
                        }
                    break;
            }
        }

        public ulong ViewHash() => HashView(Units, Fields);

        /// <summary>FNV-1a over the VIEW contract: everything a renderer shows, nothing
        /// internal (status countdowns and field timers stay sim-side; expiries arrive
        /// as events). Statuses hashed as a (kind, mag)-sorted multiset.</summary>
        public static ulong HashView(List<PlaybackUnit> units, List<PlaybackField> fields)
        {
            ulong h = 14695981039346656037UL;
            void Mix(int v) { unchecked { h ^= (uint)v; h *= 1099511628211UL; } }
            foreach (var u in units) // callers keep id order
            {
                Mix(u.Id); Mix(u.Team); Mix(u.Hp); Mix(u.Shield); Mix(u.Mana);
                Mix(u.Pos.Q); Mix(u.Pos.R); Mix(u.Dead ? 1 : 0);
                var sorted = new List<(StatusKind Kind, int Mag)>(u.Statuses);
                sorted.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Mag.CompareTo(b.Mag));
                foreach (var s in sorted) { Mix((int)s.Kind); Mix(s.Mag); }
            }
            foreach (var f in fields) // creation (= id) order on both sides
            {
                Mix(f.Id); Mix(f.IsWall ? 1 : 0);
                foreach (var hex in f.Hexes) { Mix(hex.Q); Mix(hex.R); } // emission order
            }
            return h;
        }
    }
}
