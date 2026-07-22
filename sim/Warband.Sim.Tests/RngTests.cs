using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class RngTests
    {
        [Fact]
        public void MatchesPcg32ReferenceOutput()
        {
            // First outputs of O'Neill's pcg32 reference demo, seed 42 / sequence 54.
            // If this ever fails, the sim is no longer bit-compatible with recorded
            // replays — treat as a determinism break, not a test to update casually.
            var rng = new Rng(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (uint e in expected)
                Assert.Equal(e, rng.NextUInt());
        }

        [Fact]
        public void SameSeedSameSequence()
        {
            var a = new Rng(1234);
            var b = new Rng(1234);
            for (int i = 0; i < 1000; i++)
                Assert.Equal(a.NextUInt(), b.NextUInt());
        }

        [Fact]
        public void DifferentSeedsDiverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            bool diverged = false;
            for (int i = 0; i < 10 && !diverged; i++)
                diverged = a.NextUInt() != b.NextUInt();
            Assert.True(diverged);
        }

        [Fact]
        public void BoundedNextStaysInRange()
        {
            var rng = new Rng(99);
            for (int i = 0; i < 10_000; i++)
            {
                uint v = rng.Next(6u);
                Assert.InRange(v, 0u, 5u);
            }
        }
    }
}
