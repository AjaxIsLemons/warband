using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    public class HexTests
    {
        [Fact]
        public void DistanceToSelfIsZero()
        {
            var h = new Hex(3, -2);
            Assert.Equal(0, Hex.Distance(h, h));
        }

        [Fact]
        public void AllSixNeighborsAreDistanceOne()
        {
            var origin = new Hex(2, 1);
            for (int d = 0; d < 6; d++)
                Assert.Equal(1, Hex.Distance(origin, origin.Neighbor(d)));
        }

        [Fact]
        public void NeighborsAreDistinct()
        {
            var origin = new Hex(0, 0);
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                    Assert.NotEqual(origin.Neighbor(a), origin.Neighbor(b));
        }

        [Theory]
        [InlineData(0, 0, 0, 3, 3)]   // straight along r
        [InlineData(0, 0, 3, 0, 3)]   // straight along q
        [InlineData(0, 0, 3, -3, 3)]  // straight along the third axis
        [InlineData(-2, 1, 3, -2, 5)]
        public void KnownDistances(int q1, int r1, int q2, int r2, int expected)
        {
            Assert.Equal(expected, Hex.Distance(new Hex(q1, r1), new Hex(q2, r2)));
            Assert.Equal(expected, Hex.Distance(new Hex(q2, r2), new Hex(q1, r1)));
        }

        [Fact]
        public void RowColRoundTripsAcrossTheFullBoard()
        {
            // 6 cols x 8 rows: rows 0-3 = our half, 4-7 = enemy half (pitch v0.3).
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 6; col++)
                {
                    var h = Hex.FromRowCol(row, col);
                    Assert.Equal(row, h.Row);
                    Assert.Equal(col, h.Col);
                }
        }

        [Fact]
        public void AdjacentRowsInSameColumnAreNeighbors()
        {
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 6; col++)
                    Assert.Equal(1, Hex.Distance(Hex.FromRowCol(row, col), Hex.FromRowCol(row + 1, col)));
        }
    }
}
