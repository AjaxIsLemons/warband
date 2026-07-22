using System;

namespace Warband.Sim
{
    /// <summary>
    /// PCG32 (O'Neill reference implementation). The sim's only randomness source —
    /// deterministic across machines/runtimes, which is what makes replay = re-sim
    /// and client/server hash agreement possible. Never use System.Random in the sim.
    /// </summary>
    public sealed class Rng
    {
        private ulong _state;
        private readonly ulong _inc;

        public Rng(ulong seed, ulong sequence = 54)
        {
            _state = 0;
            _inc = (sequence << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = old * 6364136223846793005UL + _inc;
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << (-rot & 31));
        }

        /// <summary>Unbiased [0, bound) via rejection sampling.</summary>
        public uint Next(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
            uint threshold = (uint)(-bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return r % bound;
            }
        }

        public int Next(int bound) => (int)Next((uint)bound);
    }
}
