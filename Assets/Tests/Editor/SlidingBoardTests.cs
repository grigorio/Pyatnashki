using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class SlidingBoardTests
    {
        [Test]
        public void FreshBoardHasClassicSolvedOrder()
        {
            var board = new SlidingBoard();
            for (int i = 0; i < 15; i++) Assert.That(board.GetTile(i), Is.EqualTo(i + 1));
            Assert.That(board.GetTile(15), Is.Zero);
            Assert.That(board.IsSolved, Is.True);
            Assert.That(board.CorrectTileCount, Is.EqualTo(15));
        }

        [Test]
        public void InvalidMovesDoNotMutateBoard()
        {
            var board = new SlidingBoard();
            foreach (int tile in new[] { -1, 0, 1, 11, 13, 16 })
                Assert.That(board.TryMoveTile(tile), Is.False);
            Assert.That(board.IsSolved, Is.True);
            Assert.That(board.MoveCount, Is.Zero);
        }

        [Test]
        public void HorizontalMoveCannotWrapAcrossRows()
        {
            var board = new SlidingBoard();
            board.TryMoveTile(15);
            board.TryMoveTile(14);
            board.TryMoveTile(13);
            Assert.That(board.EmptyIndex, Is.EqualTo(12));
            Assert.That(board.CanMoveTile(12), Is.False);
        }

        [Test]
        public void ValidMoveCanBeReversed()
        {
            var board = new SlidingBoard();
            Assert.That(board.TryMoveTile(15), Is.True);
            Assert.That(board.EmptyIndex, Is.EqualTo(14));
            Assert.That(board.GetTile(15), Is.EqualTo(15));
            Assert.That(board.IsSolved, Is.False);
            Assert.That(board.TryMoveTile(15), Is.True);
            Assert.That(board.IsSolved, Is.True);
            Assert.That(board.MoveCount, Is.EqualTo(2));
        }

        [Test]
        public void PracticeHasOneMoveSolution()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var board = new SlidingBoard();
                board.Shuffle(new Random(seed), 1);
                Assert.That(board.MoveCount, Is.Zero);
                Assert.That(board.TryMoveTile(board.GetTile(15)), Is.True);
                Assert.That(board.IsSolved, Is.True);
            }
        }

        [Test]
        public void ShufflesPreserveTilesAndSolvabilityParity()
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                var board = new SlidingBoard();
                board.Shuffle(new Random(seed), 1 + seed % 200);
                var seen = new HashSet<int>();
                int inversions = 0;
                for (int i = 0; i < 16; i++)
                {
                    int tile = board.GetTile(i);
                    Assert.That(seen.Add(tile), Is.True);
                    if (tile == 0) continue;
                    for (int j = i + 1; j < 16; j++)
                        if (board.GetTile(j) != 0 && board.GetTile(j) < tile) inversions++;
                }
                int rowFromBottom = 4 - board.EmptyIndex / 4;
                Assert.That((inversions + rowFromBottom) % 2, Is.EqualTo(1));
                Assert.That(seen.Count, Is.EqualTo(16));
                Assert.That(board.IsSolved, Is.False);
                Assert.That(board.MoveCount, Is.Zero);
            }
        }

        [Test]
        public void ShuffleRejectsInvalidArguments()
        {
            var board = new SlidingBoard();
            Assert.Throws<ArgumentNullException>(() => board.Shuffle(null, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.Shuffle(new Random(), 0));
        }
    }
}
