using System;

namespace Warband.Sim
{
    /// <summary>
    /// Axial hex coordinate (pointy-top). Rows run toward the enemy, odd-r offset
    /// converts to the board's row/col view. Math per the standard cube-coordinate
    /// identities (S = -Q-R).
    /// </summary>
    public readonly struct Hex : IEquatable<Hex>
    {
        public readonly int Q;
        public readonly int R;

        public Hex(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        public static readonly Hex[] Directions =
        {
            new Hex(1, 0), new Hex(1, -1), new Hex(0, -1),
            new Hex(-1, 0), new Hex(-1, 1), new Hex(0, 1),
        };

        public Hex Neighbor(int direction) => this + Directions[direction];

        public static Hex operator +(Hex a, Hex b) => new Hex(a.Q + b.Q, a.R + b.R);
        public static Hex operator -(Hex a, Hex b) => new Hex(a.Q - b.Q, a.R - b.R);

        public static int Distance(Hex a, Hex b)
        {
            Hex d = a - b;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        /// <summary>
        /// Deterministic integer hex line from a to b inclusive (cube-lerp with
        /// round-to-nearest, ties toward +inf, drift fixed on the largest component —
        /// no floats, identical on every platform). Projectile paths use this.
        /// </summary>
        public static System.Collections.Generic.List<Hex> Line(Hex a, Hex b)
        {
            int n = Distance(a, b);
            var result = new System.Collections.Generic.List<Hex>(n + 1);
            if (n == 0) { result.Add(a); return result; }
            for (int i = 0; i <= n; i++)
            {
                long pq = (long)a.Q * (n - i) + (long)b.Q * i;
                long pr = (long)a.R * (n - i) + (long)b.R * i;
                long ps = -(pq + pr);
                long rq = RoundDiv(pq, n), rr = RoundDiv(pr, n), rs = RoundDiv(ps, n);
                if (rq + rr + rs != 0)
                {
                    long dq = Math.Abs(rq * n - pq), dr = Math.Abs(rr * n - pr), ds = Math.Abs(rs * n - ps);
                    if (dq >= dr && dq >= ds) rq = -(rr + rs);
                    else if (dr >= ds) rr = -(rq + rs);
                    else rs = -(rq + rr);
                }
                result.Add(new Hex((int)rq, (int)rr));
            }
            return result;
        }

        private static long RoundDiv(long p, long n) // nearest, ties toward +inf; n > 0
        {
            long x = 2 * p + n, y = 2 * n;
            return x >= 0 ? x / y : -((-x + y - 1) / y);
        }

        /// <summary>All hexes within radius of center, in a fixed deterministic order.
        /// SHARED by sim field geometry and the playback fold — one implementation so
        /// attached-field views can never diverge.</summary>
        public static System.Collections.Generic.List<Hex> Range(Hex center, int radius)
        {
            var result = new System.Collections.Generic.List<Hex>();
            for (int dq = -radius; dq <= radius; dq++)
                for (int dr = Math.Max(-radius, -dq - radius); dr <= Math.Min(radius, -dq + radius); dr++)
                    result.Add(new Hex(center.Q + dq, center.R + dr));
            return result;
        }

        /// <summary>Board view (odd-r offset): row = R, col derived from Q.</summary>
        public static Hex FromRowCol(int row, int col) => new Hex(col - (row - (row & 1)) / 2, row);

        public int Row => R;
        public int Col => Q + (R - (R & 1)) / 2;

        public bool Equals(Hex other) => Q == other.Q && R == other.R;
        public override bool Equals(object? obj) => obj is Hex h && Equals(h);
        public override int GetHashCode() => (Q * 397) ^ R;
        public static bool operator ==(Hex a, Hex b) => a.Equals(b);
        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);
        public override string ToString() => $"({Row},{Col})";
    }
}
