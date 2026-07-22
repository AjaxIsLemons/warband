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
