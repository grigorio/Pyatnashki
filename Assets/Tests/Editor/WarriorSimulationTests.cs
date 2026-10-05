using System;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class WarriorSimulationTests
    {
        [TestCase(0.0, false)]
        [TestCase(61.999, false)]
        [TestCase(62.0, false)]
        [TestCase(62.001, true)]
        [TestCase(144.0, true)]
        public void OwnershipChangesOnlyWhenLeadingEdgeOverlapsDestination(double travelled, bool entered)
        {
            // Centres 144 apart, tile width 132, marker width 32: first overlap is after 62.
            Assert.That(WarriorTraversal.HasEnteredDestination(travelled, 144, 132, 32),
                Is.EqualTo(entered));
        }

        [Test]
        public void EnteredDestinationRemainsOwnerWhenThatTileSlides()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            board.TryMoveTile(8);
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, false);
            Assert.That(warrior.TryMoveTo(board, false, 8), Is.True);
            board.TryMoveTile(8);
            warrior.NotifyBoardChanged();
            Assert.That(warrior.CurrentTile, Is.EqualTo(8));
            Assert.That(warrior.GetCurrentCell(board, false), Is.EqualTo(7));
            Assert.That(warrior.Occupancy.GetCount(8), Is.EqualTo(1));
            Assert.That(warrior.Occupancy.GetCount(12), Is.Zero);
        }

        [Test]
        public void EntranceRequiresTileFacingDownAndAvailableSpace()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation();
            Assert.That(warrior.TryEnter(board, false), Is.False);
            board.TryMoveTile(15);
            Assert.That(warrior.TryEnter(board, false), Is.False);
            board.TryMoveTile(15);
            Assert.That(warrior.TryEnter(board, true), Is.True);
            Assert.That(warrior.CurrentTile, Is.EqualTo(16));
            Assert.That(warrior.TryEnter(board, true), Is.False);
        }

        [Test]
        public void SolvedRouteDeliversOneWarriorExactlyOnce()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation();
            Assert.That(warrior.TryEnter(board, true), Is.True);
            for (int i = 1; i < RoadLayout.SolutionTileOrder.Count; i++)
            {
                int previous = warrior.CurrentTile;
                int next = warrior.GetNextTile(board, true);
                Assert.That(next, Is.EqualTo(RoadLayout.SolutionTileOrder[i]));
                Assert.That(warrior.TryMoveTo(board, true, next), Is.True);
                Assert.That(warrior.Occupancy.GetCount(previous), Is.Zero);
                Assert.That(warrior.Occupancy.GetCount(next), Is.EqualTo(1));
            }
            Assert.That(warrior.TryDeliver(board, true), Is.True);
            Assert.That(warrior.TryDeliver(board, true), Is.False);
            Assert.That(warrior.Completed, Is.True);
            Assert.That(warrior.CurrentTile, Is.Zero);
            Assert.That(warrior.DeliveredCount, Is.EqualTo(1));
            for (int tile = 1; tile <= 16; tile++)
                Assert.That(warrior.Occupancy.GetCount(tile), Is.Zero);
        }

        [Test]
        public void WarriorWaitsAtBlankAndContinuesAfterRoadOpens()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            var warrior = new WarriorSimulation();
            Assert.That(warrior.TryEnter(board, false), Is.True);
            Assert.That(warrior.GetNextTile(board, false), Is.Zero);
            Assert.That(warrior.TryDeliver(board, false), Is.False);
            board.TryMoveTile(8);
            warrior.NotifyBoardChanged();
            Assert.That(warrior.GetNextTile(board, false), Is.EqualTo(8));
            Assert.That(warrior.TryMoveTo(board, false, 8), Is.True);
        }

        [Test]
        public void SlidingOccupiedTilePreservesOwnerAndOccupancy()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, false);
            Assert.That(warrior.GetCurrentCell(board, false), Is.EqualTo(15));
            board.TryMoveTile(12);
            warrior.NotifyBoardChanged();
            Assert.That(warrior.CurrentTile, Is.EqualTo(12));
            Assert.That(warrior.GetCurrentCell(board, false), Is.EqualTo(11));
            Assert.That(warrior.Occupancy.GetCount(12), Is.EqualTo(1));
            Assert.That(RoadNetwork.Analyze(board, false).ReachableTileCount, Is.Zero);
            Assert.That(warrior.GetNextTile(board, false), Is.EqualTo(8),
                "Routing must start at the warrior, even when the entrance is disconnected.");
        }

        [Test]
        public void StaleTransitionCannotCrossNewBlank()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            board.TryMoveTile(8);
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, false);
            int planned = warrior.GetNextTile(board, false);
            Assert.That(planned, Is.EqualTo(8));
            board.TryMoveTile(8);
            warrior.NotifyBoardChanged();
            Assert.That(warrior.TryMoveTo(board, false, planned), Is.False);
            Assert.That(warrior.CurrentTile, Is.EqualTo(12));
            Assert.That(warrior.Occupancy.GetCount(12), Is.EqualTo(1));
            Assert.That(warrior.Occupancy.GetCount(8), Is.Zero);
        }

        [Test]
        public void CapacityBlocksMoveUntilDestinationHasSpace()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, true);
            for (int i = 0; i < 12; i++) Assert.That(warrior.Occupancy.TryEnter(12), Is.True);
            Assert.That(warrior.GetNextTile(board, true), Is.Zero);
            Assert.That(warrior.TryMoveTo(board, true, 12), Is.False);
            Assert.That(warrior.Occupancy.GetCount(16), Is.EqualTo(1));
            warrior.Occupancy.Leave(12);
            Assert.That(warrior.GetNextTile(board, true), Is.EqualTo(12));
            Assert.That(warrior.TryMoveTo(board, true, 12), Is.True);
            Assert.That(warrior.Occupancy.GetCount(12), Is.EqualTo(12));
        }

        [Test]
        public void PartialExplorationEventuallyStopsInsteadOfBouncingForever()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(12);
            board.TryMoveTile(8);
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, false);
            int steps = 0, next;
            while ((next = warrior.GetNextTile(board, false)) != 0 && steps < 64)
            {
                Assert.That(warrior.TryMoveTo(board, false, next), Is.True);
                steps++;
            }
            Assert.That(steps, Is.GreaterThan(0));
            Assert.That(steps, Is.LessThan(64));
            Assert.That(warrior.GetNextTile(board, false), Is.Zero);
            Assert.That(warrior.Completed, Is.False);
        }

        [Test]
        public void QueueAndResetDoNotDuplicateWarriorOrKeepOccupancy()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation();
            Assert.That(warrior.QueueNext(), Is.False);
            warrior.TryEnter(board, true);
            for (int i = 0; i < 15; i++) warrior.TryMoveTo(board, true, warrior.GetNextTile(board, true));
            warrior.TryDeliver(board, true);
            Assert.That(warrior.QueueNext(), Is.True);
            Assert.That(warrior.QueueNext(), Is.False);
            Assert.That(warrior.DeliveredCount, Is.EqualTo(1));
            warrior.TryEnter(board, true);
            warrior.Reset();
            Assert.That(warrior.CurrentTile, Is.Zero);
            Assert.That(warrior.DeliveredCount, Is.Zero);
            Assert.That(warrior.Completed, Is.False);
            Assert.That(warrior.Occupancy.GetCount(16), Is.Zero);
        }

        [Test]
        public void OccupancyEnforcesCapacityForEveryTile()
        {
            var occupancy = new TileOccupancy();
            for (int tile = 1; tile <= 16; tile++)
            {
                for (int i = 0; i < tile; i++) Assert.That(occupancy.TryEnter(tile), Is.True);
                Assert.That(occupancy.TryEnter(tile), Is.False);
                Assert.That(occupancy.GetCount(tile), Is.EqualTo(tile));
                for (int i = 0; i < tile; i++) occupancy.Leave(tile);
            }
            Assert.Throws<InvalidOperationException>(() => occupancy.Leave(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => occupancy.TryEnter(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => occupancy.GetCount(17));
        }

        [Test]
        public void InvalidMovesDoNotChangeOwnerOrOccupancy()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation();
            warrior.TryEnter(board, true);
            foreach (int tile in new[] { -1, 0, 15, 13, 16, 17 })
                Assert.That(warrior.TryMoveTo(board, true, tile), Is.False);
            Assert.That(warrior.CurrentTile, Is.EqualTo(16));
            Assert.That(warrior.Occupancy.GetCount(16), Is.EqualTo(1));
        }
    }
}
