using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    /// <summary>Classic 4x4 board. Zero represents the empty cell.</summary>
    public sealed class SlidingBoard
    {
        public const int Width = 4;
        public const int CellCount = Width * Width;
        private readonly int[] cells = new int[CellCount];
        public int EmptyIndex { get; private set; }
        public int MoveCount { get; private set; }

        public SlidingBoard() => ResetSolved();

        public int GetTile(int index)
        {
            if (index < 0 || index >= CellCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return cells[index];
        }

        public int GetIndexOf(int tile) => Array.IndexOf(cells, tile);

        public void ResetMoveCount() => MoveCount = 0;

        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < CellCount - 1; i++)
                    if (cells[i] != i + 1) return false;
                return cells[CellCount - 1] == 0;
            }
        }

        public int CorrectTileCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < CellCount - 1; i++)
                    if (cells[i] == i + 1) count++;
                return count;
            }
        }

        public void ResetSolved()
        {
            for (int i = 0; i < CellCount - 1; i++) cells[i] = i + 1;
            cells[CellCount - 1] = 0;
            EmptyIndex = CellCount - 1;
            MoveCount = 0;
        }

        public bool CanMoveTile(int tile)
        {
            if (tile <= 0 || tile >= CellCount) return false;
            int index = GetIndexOf(tile);
            return Math.Abs(index / Width - EmptyIndex / Width)
                 + Math.Abs(index % Width - EmptyIndex % Width) == 1;
        }

        public bool TryMoveTile(int tile)
        {
            if (!CanMoveTile(tile)) return false;
            int index = GetIndexOf(tile);
            cells[EmptyIndex] = tile;
            cells[index] = 0;
            EmptyIndex = index;
            MoveCount++;
            return true;
        }

        /// <summary>Legal moves from the solution guarantee solvability.
        /// Steps are scramble moves, not the shortest solution length.</summary>
        public void Shuffle(Random random, int steps)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps));
            ResetSolved();
            int previousEmpty = -1;
            var candidates = new List<int>(4);
            for (int step = 0; step < steps; step++)
            {
                candidates.Clear();
                for (int tile = 1; tile < CellCount; tile++)
                    if (CanMoveTile(tile) && GetIndexOf(tile) != previousEmpty)
                        candidates.Add(tile);
                int oldEmpty = EmptyIndex;
                TryMoveTile(candidates[random.Next(candidates.Count)]);
                previousEmpty = oldEmpty;
            }
            // A longer walk can return to the solution. Never start there.
            if (IsSolved)
                for (int tile = 1; tile < CellCount; tile++)
                    if (CanMoveTile(tile))
                    {
                        TryMoveTile(tile);
                        break;
                    }
            MoveCount = 0;
        }
    }
}
