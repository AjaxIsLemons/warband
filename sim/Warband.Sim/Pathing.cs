using System.Collections.Generic;

namespace Warband.Sim
{
    /// <summary>
    /// Board routing (2026-07-26). Movement used to be a greedy hill-climb on straight-line hex
    /// distance: a unit stepped only to a STRICTLY CLOSER free neighbour. That has local minima
    /// everywhere on a 6-wide board — one ally standing in the single closing direction froze a
    /// unit for the rest of the fight, and anything that landed boxed in never moved again.
    ///
    /// The replacement is a flow field. Dijkstra runs outward from every hex the unit could
    /// ATTACK FROM (the engage ring, see Battle.RouteTo) and the unit steps to the neighbouring
    /// free hex lowest in the field. Two properties matter:
    ///
    /// - A wall is impassable. A BODY is a detour, not a wall: entering an occupied hex costs
    ///   <see cref="BodyCost"/>, so the field keeps a gradient straight through a crowd. A unit
    ///   therefore walks AROUND a queue whenever going around is cheaper than pushing through it,
    ///   and holds position when it is not — which is what queueing should look like.
    /// - The field prices the whole remaining route, not one hex of it, so a unit never has to
    ///   guess: "closer" always means "closer along a road that exists".
    ///
    /// Determinism: integer costs only, Dial's algorithm with circular buckets (settled in
    /// ascending cost, then ascending board index), fixed direction order for ties. No floats,
    /// no rng, no unordered iteration.
    /// </summary>
    public static class Pathing
    {
        /// <summary>What routing through a living body costs. It is also, exactly, a unit's
        /// patience for a queue: the step gate accepts a detour whose remaining route is no
        /// dearer than waiting out one body, and rejects one that is worse than that.</summary>
        public const int BodyCost = 6;

        public const int Unreachable = int.MaxValue;

        /// <summary>Cost of ENTERING a hex; negative means impassable.</summary>
        public delegate int EnterCost(Hex h);

        public static int Cells => Battle.BoardRows * Battle.BoardCols;

        public static int Index(Hex h) => h.Row * Battle.BoardCols + h.Col;

        private static Hex FromIndex(int i) => Hex.FromRowCol(i / Battle.BoardCols, i % Battle.BoardCols);

        /// <summary>
        /// Cost from every board hex to the nearest goal. A goal is seeded at its own entry toll
        /// minus the travel step you do not take — free goal 0, goal with a body on it BodyCost-1.
        /// That last part is load-bearing: a melee unit's engage ring is the target's own
        /// neighbourhood, which is exactly where its allies are already standing. Seeding those at
        /// 0 tells everyone queueing behind them that they have arrived, and the whole team stops
        /// one hex short of a target with free slots on its far side.
        /// </summary>
        public static int[] Field(List<Hex> goals, EnterCost enter)
        {
            int n = Cells;
            var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = Unreachable;

            // Circular bucket queue: valid because every edge weight is in [1, BodyCost], so a
            // relaxation from cost d always lands in (d, d + BodyCost] and can never collide with
            // the bucket being drained.
            int wheel = BodyCost + 1;
            var buckets = new List<int>[wheel];
            for (int i = 0; i < wheel; i++) buckets[i] = new List<int>();

            foreach (var g in goals)
            {
                if (!Battle.InBounds(g)) continue;
                int toll = enter(g);
                if (toll < 0) continue;                  // a goal inside a wall is not a goal
                int seed = toll > 1 ? toll - 1 : 0;
                int gi = Index(g);
                if (seed < dist[gi]) { dist[gi] = seed; buckets[seed % wheel].Add(gi); }
            }

            int maxCost = n * BodyCost;
            for (int d = 0; d <= maxCost; d++)
            {
                var bucket = buckets[d % wheel];
                for (int k = 0; k < bucket.Count; k++)
                {
                    int ci = bucket[k];
                    if (dist[ci] != d) continue;         // stale: already settled cheaper
                    Hex h = FromIndex(ci);
                    for (int dir = 0; dir < 6; dir++)
                    {
                        Hex nb = h.Neighbor(dir);
                        if (!Battle.InBounds(nb)) continue;
                        int w = enter(nb);
                        if (w < 0) continue;             // impassable
                        int nd = d + w;
                        int ni = Index(nb);
                        if (nd >= dist[ni]) continue;
                        dist[ni] = nd;
                        buckets[nd % wheel].Add(ni);
                    }
                }
                bucket.Clear();
            }
            return dist;
        }

        /// <summary>
        /// A unit's own route cost, with its own body toll removed. The field charges BodyCost to
        /// enter the hex the unit is standing on (it is a body like any other), so the raw value
        /// there is one toll dearer than the best route actually available from it. Backing that
        /// out is what makes <see cref="Step"/>'s comparison honest.
        /// </summary>
        public static int SelfCost(int[] field, Hex pos)
        {
            int raw = field[Index(pos)];
            if (raw == Unreachable) return Unreachable;
            return raw >= BodyCost ? raw - BodyCost : 0;
        }

        /// <summary>
        /// The step to take, or null to hold. Free neighbours whose remaining route is no dearer
        /// than the unit's own best route win; ties go to the lowest direction index, so the choice
        /// is order-independent.
        ///
        /// The "no dearer" gate is the whole behaviour contract. Strictly-cheaper would refuse the
        /// last step into an engage ring (a ring hex is already at cost 0, same as the route
        /// through it); anything looser makes a unit orbit a full scrum forever, sidestepping
        /// between hexes that are equally far from a ring it can never enter.
        /// </summary>
        public static Hex? Step(int[] field, Hex pos, System.Func<Hex, bool> free)
        {
            int self = SelfCost(field, pos);
            Hex? best = null;
            int bestCost = Unreachable;
            for (int dir = 0; dir < 6; dir++)
            {
                Hex nb = pos.Neighbor(dir);
                if (!Battle.InBounds(nb) || !free(nb)) continue;
                int cost = field[Index(nb)];
                if (cost == Unreachable || cost > self) continue;
                if (cost < bestCost) { bestCost = cost; best = nb; }
            }
            return best;
        }
    }
}
