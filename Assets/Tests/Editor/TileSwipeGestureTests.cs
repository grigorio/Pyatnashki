using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class TileSwipeGestureTests
    {
        [TestCase(5, 4, -30, 0)]
        [TestCase(5, 6, 30, 0)]
        [TestCase(5, 1, 0, 30)]
        [TestCase(5, 9, 0, -30)]
        public void SwipeTowardBlankMovesOnlyOnce(int source, int empty, double x, double y)
        {
            var g = new TileSwipeGesture();
            Assert.That(g.Begin(source, empty, 132, 0.15, 7), Is.True);
            Assert.That(g.TryDrag(x, y, 7), Is.True);
            Assert.That(g.TryDrag(x * 2, y * 2, 7), Is.False);
            Assert.That(g.TryRelease(x, y, 7), Is.False);
        }

        [Test]
        public void SmallMovementRemainsTap()
        {
            var g = new TileSwipeGesture();
            g.Begin(5, 6, 132, 0.15, 1);
            Assert.That(g.TryDrag(5, 2, 1), Is.False);
            Assert.That(g.TryRelease(5, 2, 1), Is.True);
            Assert.That(g.TryRelease(0, 0, 1), Is.False);
        }

        [TestCase(-30, 0)]
        [TestCase(0, 30)]
        [TestCase(30, 30)]
        public void WrongDirectionCannotBecomeTapOnRelease(double x, double y)
        {
            var g = new TileSwipeGesture();
            g.Begin(5, 6, 132, 0.15, 1);
            Assert.That(g.TryDrag(x, y, 1), Is.False);
            Assert.That(g.TryRelease(0, 0, 1), Is.False);
        }

        [Test]
        public void BoardChangeInvalidatesHeldGesture()
        {
            var g = new TileSwipeGesture();
            g.Begin(5, 6, 132, 0.15, 1);
            Assert.That(g.TryDrag(30, 0, 2), Is.False);
            Assert.That(g.TryRelease(0, 0, 2), Is.False);
        }

        [TestCase(3, 4)]
        [TestCase(5, 7)]
        [TestCase(5, 5)]
        public void NonAdjacentCellsCannotBegin(int source, int empty)
        {
            var g = new TileSwipeGesture();
            Assert.That(g.Begin(source, empty, 132, 0.15, 1), Is.False);
            Assert.That(g.TryRelease(0, 0, 1), Is.False);
        }

        [Test]
        public void ThresholdScalesWithTileSize()
        {
            var g = new TileSwipeGesture();
            g.Begin(5, 6, 200, 0.15, 1);
            Assert.That(g.TryDrag(29, 0, 1), Is.False);
            Assert.That(g.TryDrag(30, 0, 1), Is.True);
        }

        [Test]
        public void CancelPreventsReleaseMove()
        {
            var g = new TileSwipeGesture();
            g.Begin(5, 6, 132, 0.15, 1);
            g.Cancel();
            Assert.That(g.TryRelease(0, 0, 1), Is.False);
        }
    }
}
