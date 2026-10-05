using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class WarriorWaveTests
    {
        [Test]
        public void ReservationsPreventTwoActorsClaimingLastSlot()
        {
            var ledger = new TileOccupancy();
            Assert.That(ledger.TryReserve(1), Is.True);
            Assert.That(ledger.TryReserve(1), Is.False);
            Assert.That(ledger.TryEnter(1), Is.False);
            Assert.That(ledger.TryEnterReserved(1), Is.True);
            Assert.That(ledger.GetCount(1), Is.EqualTo(1));
            Assert.That(ledger.GetReservedCount(1), Is.Zero);
            Assert.That(ledger.TryEnterReserved(1), Is.False);
        }

        [Test]
        public void CancelledReservationImmediatelyMakesSlotAvailable()
        {
            var ledger = new TileOccupancy();
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation(ledger);
            warrior.TryEnter(board, true);
            Assert.That(warrior.ReserveDestination(board, true, 15), Is.True);
            Assert.That(ledger.GetReservedCount(15), Is.EqualTo(1));
            warrior.CancelReservation();
            Assert.That(warrior.ReservedTile, Is.Zero);
            Assert.That(ledger.GetReservedCount(15), Is.Zero);
            Assert.That(warrior.CurrentTile, Is.EqualTo(16));
            Assert.That(ledger.GetCount(16), Is.EqualTo(1));
        }

        [Test]
        public void BoundaryTransferConsumesReservationExactlyOnce()
        {
            var ledger = new TileOccupancy();
            var board = new SlidingBoard();
            var warrior = new WarriorSimulation(ledger);
            warrior.ReserveDestination(board, true, 16);
            Assert.That(warrior.TryEnter(board, true), Is.True);
            warrior.ReserveDestination(board, true, 15);
            Assert.That(warrior.TryMoveTo(board, true, 15), Is.True);
            Assert.That(warrior.TryMoveTo(board, true, 15), Is.False);
            warrior.CancelReservation();
            Assert.That(ledger.GetCount(16), Is.Zero);
            Assert.That(ledger.GetCount(15), Is.EqualTo(1));
            Assert.That(ledger.GetReservedCount(15), Is.Zero);
        }

        [Test]
        public void ResettingOneActorDoesNotEraseAnotherActorsOccupancy()
        {
            var board = new SlidingBoard();
            var ledger = new TileOccupancy();
            var first = new WarriorSimulation(ledger);
            var second = new WarriorSimulation(ledger);
            first.TryEnter(board, true);
            second.TryEnter(board, true);
            first.ReserveDestination(board, true, 15);
            first.Reset();
            Assert.That(ledger.GetCount(16), Is.EqualTo(1));
            Assert.That(ledger.GetReservedCount(15), Is.Zero);
            Assert.That(second.CurrentTile, Is.EqualTo(16));
            Assert.That(second.TryMoveTo(board, true, 15), Is.True);
        }

        [TestCase(12)]
        [TestCase(16)]
        public void CompleteWavesDeliverAllActorsWithoutExceedingCapacity(int count)
        {
            var ledger = new TileOccupancy();
            var board = new SlidingBoard();
            var warriors = new WarriorSimulation[count];
            var planned = new int[count];
            for (int i = 0; i < count; i++) warriors[i] = new WarriorSimulation(ledger);
            int delivered = 0;
            for (int tick = 0; tick < 1000 && delivered < count; tick++)
            {
                for (int i = 0; i < count; i++)
                {
                    var w = warriors[i];
                    if (w.Completed) continue;
                    if (planned[i] != 0)
                    {
                        bool moved = w.CurrentTile == 0 ? w.TryEnter(board, true)
                            : w.TryMoveTo(board, true, planned[i]);
                        Assert.That(moved, Is.True);
                        planned[i] = 0;
                    }
                    else if (w.CanDeliver(board, true))
                    {
                        Assert.That(w.TryDeliver(board, true), Is.True);
                        delivered++;
                    }
                    else
                    {
                        int next = w.CurrentTile == 0 ? 16 : w.GetNextTile(board, true);
                        if (next != 0 && w.ReserveDestination(board, true, next)) planned[i] = next;
                    }
                }
                for (int tile = 1; tile <= 16; tile++)
                    Assert.That(ledger.GetCount(tile) + ledger.GetReservedCount(tile),
                        Is.InRange(0, tile), "Capacity exceeded on tile " + tile);
            }
            Assert.That(delivered, Is.EqualTo(count));
            for (int tile = 1; tile <= 16; tile++)
            {
                Assert.That(ledger.GetCount(tile), Is.Zero);
                Assert.That(ledger.GetReservedCount(tile), Is.Zero);
            }
        }

        [Test]
        public void FullCastlePathTileCausesWaitingInsteadOfMovingBackwards()
        {
            var board = new SlidingBoard();
            var ledger = new TileOccupancy();
            var w = new WarriorSimulation(ledger);
            w.TryEnter(board, true);
            w.TryMoveTo(board, true, 15);
            for (int i = 0; i < 14; i++) ledger.TryEnter(14);
            Assert.That(w.GetNextTile(board, true), Is.Zero);
            ledger.Leave(14);
            Assert.That(w.GetNextTile(board, true), Is.EqualTo(14));
        }
    }
}
