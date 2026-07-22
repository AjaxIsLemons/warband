using System;
using System.Collections.Generic;
using System.Linq;

namespace Warband.Sim
{
    public enum Winner { Team0, Team1, Draw }

    public sealed class BattleResult
    {
        public Winner Winner;
        public int EndTick;
        public List<BattleEvent> Events = new List<BattleEvent>();
        public ulong FinalHash;
    }

    /// <summary>
    /// The deterministic tick loop. Invariants (ADR 0001/0003):
    /// - Frozen-read / buffer / apply: decisions read start-of-tick state only; mutations
    ///   land together at end of tick, so within-tick iteration order can never leak into
    ///   outcomes (simultaneous mirror swings kill both, mutual KO = Draw).
    /// - All iteration is by ascending unit Id; every tie-break is explicit and fixed.
    /// - The storm (overtime) guarantees resolution — deterministic stalemates are a
    ///   design failure we hard-counter here (beltwars finding).
    /// </summary>
    public sealed class Battle
    {
        public const int ManaPerAttack = 10;
        public const int ManaPerHitTaken = 5;
        public const int OvertimeStartTick = 900;      // ~90s at 10 ticks/s
        public const int StormRampInterval = 30;       // +1 storm damage per 3s
        public const int SafetyCapTick = 20_000;

        private readonly List<UnitState> _units;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private int _tick;

        public Battle(IEnumerable<UnitState> units)
        {
            _units = units.OrderBy(u => u.Id).ToList();
        }

        public BattleResult Run()
        {
            while (true)
            {
                Winner? done = CheckEnd();
                if (done != null || _tick >= SafetyCapTick)
                {
                    var result = new BattleResult
                    {
                        Winner = done ?? Winner.Draw,
                        EndTick = _tick,
                        Events = _events,
                        FinalHash = StateHash(),
                    };
                    _events.Add(new BattleEvent(_tick, EventType.End, -1, -1, (int)result.Winner));
                    return result;
                }
                Step();
            }
        }

        private void Step()
        {
            AcquireTargets();

            // ---- decide (frozen state) ----
            var damage = new Dictionary<int, int>();
            var manaGain = new Dictionary<int, int>();
            var moves = new List<(UnitState unit, Hex dest)>();
            var occupied = new HashSet<Hex>(_units.Where(u => u.Alive).Select(u => u.Pos));

            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                var target = ById(u.TargetId);
                if (target == null) continue;

                // Placeholder signature: full mana = instant nuke on current target.
                if (u.Def.ManaMax > 0 && u.Mana >= u.Def.ManaMax)
                {
                    Add(damage, target.Id, u.Def.CastDamage);
                    u.Mana = 0; // own-clock bookkeeping, not world state — safe to set now
                    _events.Add(new BattleEvent(_tick, EventType.Cast, u.Id, target.Id, u.Def.CastDamage));
                }

                int dist = Hex.Distance(u.Pos, target.Pos);
                if (dist <= u.Def.Range)
                {
                    if (_tick >= u.NextAttackTick)
                    {
                        Add(damage, target.Id, u.Def.Attack);
                        Add(manaGain, u.Id, ManaPerAttack);
                        Add(manaGain, target.Id, ManaPerHitTaken);
                        u.NextAttackTick = _tick + u.Def.AttackInterval;
                        _events.Add(new BattleEvent(_tick, EventType.Attack, u.Id, target.Id, u.Def.Attack));
                    }
                }
                else if (_tick >= u.NextMoveTick)
                {
                    // Greedy step: strictly closer, unoccupied (origins stay blocked this
                    // tick — no same-tick train-following), fixed direction order breaks ties.
                    Hex? best = null;
                    int bestDist = dist;
                    for (int d = 0; d < 6; d++)
                    {
                        Hex n = u.Pos.Neighbor(d);
                        if (occupied.Contains(n)) continue;
                        int nd = Hex.Distance(n, target.Pos);
                        if (nd < bestDist)
                        {
                            bestDist = nd;
                            best = n;
                        }
                    }
                    if (best != null)
                    {
                        occupied.Add(best.Value); // claim so later ids can't take it
                        moves.Add((u, best.Value));
                        u.NextMoveTick = _tick + u.Def.MoveInterval;
                    }
                }
            }

            // ---- apply ----
            foreach (var (unit, dest) in moves)
            {
                unit.Pos = dest;
                _events.Add(new BattleEvent(_tick, EventType.Move, unit.Id, -1, dest.Q * 1000 + dest.R));
            }
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                if (damage.TryGetValue(u.Id, out int dmg)) u.Hp -= dmg;
                if (manaGain.TryGetValue(u.Id, out int mana) && u.Def.ManaMax > 0)
                    u.Mana = Math.Min(u.Def.ManaMax, u.Mana + mana);
                if (_tick >= OvertimeStartTick)
                    u.Hp -= 1 + (_tick - OvertimeStartTick) / StormRampInterval;
            }
            foreach (var u in _units)
                if (u.Hp <= 0 && damageOrStormThisTick(u))
                    _events.Add(new BattleEvent(_tick, EventType.Death, u.Id, -1, 0));

            _tick++;

            bool damageOrStormThisTick(UnitState u) =>
                damage.ContainsKey(u.Id) || _tick >= OvertimeStartTick;
        }

        private void AcquireTargets()
        {
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                var current = ById(u.TargetId);
                if (current != null) continue; // sticky until the target dies
                UnitState? best = null;
                int bestDist = int.MaxValue;
                foreach (var e in _units)
                {
                    if (!e.Alive || e.Team == u.Team) continue;
                    int d = Hex.Distance(u.Pos, e.Pos);
                    if (d < bestDist) { bestDist = d; best = e; }
                }
                u.TargetId = best?.Id ?? -1;
            }
        }

        private UnitState? ById(int id)
        {
            if (id < 0) return null;
            foreach (var u in _units)
                if (u.Id == id)
                    return u.Alive ? u : null;
            return null;
        }

        private Winner? CheckEnd()
        {
            bool t0 = _units.Any(u => u.Alive && u.Team == 0);
            bool t1 = _units.Any(u => u.Alive && u.Team == 1);
            if (t0 && t1) return null;
            if (t0) return Winner.Team0;
            if (t1) return Winner.Team1;
            return Winner.Draw;
        }

        /// <summary>FNV-1a over living-unit state — the determinism/replay guardrail.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            void Mix(int v)
            {
                unchecked
                {
                    h ^= (uint)v;
                    h *= 1099511628211UL;
                }
            }
            Mix(_tick);
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                Mix(u.Id); Mix(u.Pos.Q); Mix(u.Pos.R); Mix(u.Hp); Mix(u.Mana);
            }
            return h;
        }

        private static void Add(Dictionary<int, int> map, int key, int value) =>
            map[key] = map.TryGetValue(key, out int cur) ? cur + value : value;
    }
}
