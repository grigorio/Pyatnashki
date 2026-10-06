using System;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class RoadNetworkTests
    {
        [Test]
        public void EntranceIsBelowBoardAndCastleAbove()
        {
            Assert.That(RoadLayout.EntryCell / 4, Is.EqualTo(3));
            Assert.That(RoadLayout.CastleCell / 4, Is.Zero);
            Assert.That(RoadLayout.EntryPort, Is.EqualTo(RoadPorts.South));
            Assert.That(RoadLayout.CastlePort, Is.EqualTo(RoadPorts.North));
        }

        [Test]
        public void SolvedBoardNeedsFinalTileToOpenEntrance()
        {
            var route = RoadNetwork.Analyze(new SlidingBoard());
            Assert.That(route.ReachableTileCount, Is.Zero);
            Assert.That(route.HasCastleRoute, Is.False);
        }

        [Test]
        public void FinalTileCompletesEntireReferenceRoute()
        {
            var route = RoadNetwork.Analyze(new SlidingBoard(), true);
            Assert.That(route.HasCastleRoute, Is.True);
            Assert.That(route.ReachableTileCount, Is.EqualTo(16));
            Assert.That(route.CastlePath.Count, Is.EqualTo(16));
            for (int i = 0; i < 16; i++)
            {
                Assert.That(route.IsReachable(i), Is.True);
                Assert.That(route.CastlePath[i], Is.EqualTo(RoadLayout.SolutionTileOrder[i] - 1));
            }
        }

        [Test]
        public void RoadsIncludeStraightsTurnsAndCrossroads()
        {
            Assert.That(RoadLayout.GetPorts(14), Is.EqualTo(RoadPorts.East | RoadPorts.West));
            Assert.That(RoadLayout.GetPorts(8), Is.EqualTo(RoadPorts.North | RoadPorts.South));
            Assert.That(RoadLayout.GetPorts(13), Is.EqualTo(RoadPorts.North | RoadPorts.East));
            Assert.That(RoadLayout.GetPorts(11), Is.EqualTo(RoadPorts.All));
        }

        [TestCase(RoadPorts.North, RoadPorts.South)]
        [TestCase(RoadPorts.East, RoadPorts.West)]
        [TestCase(RoadPorts.South, RoadPorts.North)]
        [TestCase(RoadPorts.West, RoadPorts.East)]
        public void ConnectionRequiresBothFacingPorts(RoadPorts direction, RoadPorts opposite)
        {
            Assert.That(RoadNetwork.AreConnected(direction, opposite, direction), Is.True);
            Assert.That(RoadNetwork.AreConnected(direction, direction, direction), Is.False);
            Assert.That(RoadNetwork.AreConnected(RoadPorts.None, opposite, direction), Is.False);
        }

        [Test]
        public void NeighborSearchCannotWrapRowsOrLeaveBoard()
        {
            Assert.That(RoadNetwork.TryGetNeighbor(3, RoadPorts.East, out _), Is.False);
            Assert.That(RoadNetwork.TryGetNeighbor(4, RoadPorts.West, out _), Is.False);
            Assert.That(RoadNetwork.TryGetNeighbor(0, RoadPorts.North, out _), Is.False);
            Assert.That(RoadNetwork.TryGetNeighbor(15, RoadPorts.South, out _), Is.False);
            Assert.That(RoadNetwork.TryGetNeighbor(3, RoadPorts.South, out int next), Is.True);
            Assert.That(next, Is.EqualTo(7));
        }

        [Test]
        public void MovingTileCarriesItsRoadAndBlankStopsTraversal()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            var route = RoadNetwork.Analyze(board);
            Assert.That(route.IsReachable(15), Is.True);
            Assert.That(route.IsReachable(board.EmptyIndex), Is.False);
            Assert.That(route.ReachableTileCount, Is.EqualTo(1));
            Assert.That(route.HasCastleRoute, Is.False);
            Assert.That(RoadNetwork.Analyze(board, true).ReachableTileCount, Is.EqualTo(1),
                "The final tile must not appear on an unsolved board.");
            board.TryMoveTile(8);
            route = RoadNetwork.Analyze(board);
            Assert.That(route.ReachableTileCount, Is.GreaterThan(1));
            Assert.That(route.IsReachable(board.EmptyIndex), Is.False);
            board.TryMoveTile(8);
            board.TryMoveTile(12);
            Assert.That(RoadNetwork.Analyze(board, true).HasCastleRoute, Is.True);
        }

        [Test]
        public void ShuffledNetworksExcludeBlankAndReturnValidPaths()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var board = new SlidingBoard();
                board.Shuffle(new Random(seed), 50);
                var route = RoadNetwork.Analyze(board);
                Assert.That(route.IsReachable(board.EmptyIndex), Is.False);
                Assert.That(route.ReachableTileCount, Is.InRange(0, 15));
                if (!route.HasCastleRoute) continue;
                Assert.That(route.CastlePath[0], Is.EqualTo(RoadLayout.EntryCell));
                Assert.That(route.CastlePath[route.CastlePath.Count - 1], Is.EqualTo(RoadLayout.CastleCell));
                for (int i = 1; i < route.CastlePath.Count; i++)
                {
                    int a = route.CastlePath[i - 1], b = route.CastlePath[i];
                    RoadPorts direction = a / 4 == b / 4
                        ? (b > a ? RoadPorts.East : RoadPorts.West)
                        : (b > a ? RoadPorts.South : RoadPorts.North);
                    Assert.That(RoadNetwork.TryGetNeighbor(a, direction, out int next), Is.True);
                    Assert.That(next, Is.EqualTo(b));
                    Assert.That(RoadNetwork.AreConnected(RoadLayout.GetPorts(board.GetTile(a)),
                        RoadLayout.GetPorts(board.GetTile(b)), direction), Is.True);
                }
            }
        }

        [Test]
        public void InvalidInputsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => RoadNetwork.Analyze(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadLayout.GetPorts(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadLayout.GetPorts(17));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadNetwork.TryGetNeighbor(-1, RoadPorts.East, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => RoadNetwork.AreConnected(RoadPorts.All,
                RoadPorts.All, RoadPorts.All));
        }
    }
}
