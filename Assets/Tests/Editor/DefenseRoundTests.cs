using System;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class DefenseRoundTests
    {
        private static LevelDefinition Defense(int enemies = 2, double interval = 2) =>
            new LevelDefinition("Defense", LevelMode.Defense, 5, 1, 10, 1, enemies, interval);

        [Test]
        public void EnemyArrivalsFollowIntervalsAndStopAtCap()
        {
            var round = new SiegeRound();
            round.Start(Defense());
            round.Advance(1.99);
            Assert.That(round.EnemyCount, Is.Zero);
            round.Advance(0.01);
            Assert.That(round.EnemyCount, Is.EqualTo(1));
            round.Advance(4);
            Assert.That(round.EnemyCount, Is.EqualTo(2));
        }

        [TestCase(3, SiegeRoundState.Won)]
        [TestCase(2, SiegeRoundState.Lost)]
        [TestCase(1, SiegeRoundState.Lost)]
        public void OnlyDeadlineResolvesNumericalAdvantage(int defenders, SiegeRoundState result)
        {
            var round = new SiegeRound();
            round.Start(Defense());
            for (int i = 0; i < defenders; i++) round.RecordDelivery();
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            round.Advance(10);
            Assert.That(round.State, Is.EqualTo(result));
            Assert.That(round.RecordDelivery(), Is.False);
            round.Advance(50);
            Assert.That(round.EnemyCount, Is.EqualTo(2));
        }

        [Test]
        public void OvershootCannotSpawnEnemiesAfterDeadline()
        {
            var round = new SiegeRound();
            round.Start(Defense(4, 3));
            round.Advance(100);
            Assert.That(round.EnemyCount, Is.EqualTo(3));
            Assert.That(round.Elapsed, Is.EqualTo(10));
        }

        [Test]
        public void FractionalIntervalBoundaryIsIncluded()
        {
            var round = new SiegeRound();
            round.Start(Defense(3, 0.1));
            round.Advance(0.3);
            Assert.That(round.EnemyCount, Is.EqualTo(3));
        }

        [Test]
        public void RestartCaptureClearsEnemyState()
        {
            var round = new SiegeRound();
            round.Start(Defense());
            round.Advance(4);
            round.Start(20, 12, 120);
            Assert.That(round.Mode, Is.EqualTo(LevelMode.Capture));
            Assert.That(round.EnemyCount, Is.Zero);
            Assert.That(round.Delivered, Is.Zero);
            round.Reset();
            Assert.That(round.EnemyTotal, Is.Zero);
        }

        [Test]
        public void ReverseRouteDeliversFromCastleToBorderOnce()
        {
            var board = new SlidingBoard();
            var ledger = new TileOccupancy();
            ledger.UpdateCapacityMode(board, true);
            var warrior = new WarriorSimulation(ledger, true);
            Assert.That(warrior.TryEnter(board, true), Is.True);
            Assert.That(warrior.CurrentTile, Is.EqualTo(2));
            var route = RoadNetwork.Analyze(board, true, true);
            Assert.That(route.CastlePath.Count, Is.EqualTo(16));
            Assert.That(route.CastlePath[0], Is.EqualTo(RoadLayout.CastleCell));
            for (int i = RoadLayout.SolutionTileOrder.Count - 2; i >= 0; i--)
            {
                int next = warrior.GetNextTile(board, true);
                Assert.That(next, Is.EqualTo(RoadLayout.SolutionTileOrder[i]));
                Assert.That(warrior.ReserveDestination(board, true, next), Is.True);
                Assert.That(warrior.TryMoveTo(board, true, next), Is.True);
            }
            Assert.That(warrior.TryDeliver(board, true), Is.True);
            Assert.That(warrior.TryDeliver(board, true), Is.False);
            for (int tile = 1; tile <= 16; tile++)
            {
                Assert.That(ledger.GetCount(tile), Is.Zero);
                Assert.That(ledger.GetReservedCount(tile), Is.Zero);
            }
        }

        [Test]
        public void DefendersEnterBeforeCompletionAndWaitAtMissingBorderTile()
        {
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation(null, true);
            Assert.That(warrior.TryEnter(board, false), Is.True);
            int next, moves = 0;
            while ((next = warrior.GetNextTile(board, false)) != 0 && moves < 20)
            {
                Assert.That(warrior.TryMoveTo(board, false, next), Is.True);
                moves++;
            }
            Assert.That(moves, Is.EqualTo(14));
            Assert.That(warrior.CurrentTile, Is.EqualTo(12));
            Assert.That(warrior.CanDeliver(board, false), Is.False);
            Assert.That(warrior.GetNextTile(board, true), Is.EqualTo(16));
            Assert.That(warrior.TryMoveTo(board, true, 16), Is.True);
            Assert.That(warrior.TryDeliver(board, true), Is.True);
        }

        [Test]
        public void DeliveryCannotExceedFiniteSupply()
        {
            var round = new SiegeRound();
            round.Start(Defense());
            for (int i = 0; i < 5; i++) Assert.That(round.RecordDelivery(), Is.True);
            Assert.That(round.RecordDelivery(), Is.False);
            Assert.That(round.Delivered, Is.EqualTo(5));
        }

        [Test]
        public void UnwinnableDefenseAndInvalidEnemyRateAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new LevelDefinition("Bad", LevelMode.Defense,
                2, 1, 10, 1, 2, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Defense(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Defense(2, 0));
        }
    }
}
