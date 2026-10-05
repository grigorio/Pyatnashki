using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    /// <summary>Occupancy is keyed by tile identity, so sliding never changes it.</summary>
    public sealed class TileOccupancy
    {
        private readonly int[] counts = new int[RoadLayout.FinalTile + 1];

        public int GetCount(int tile) { Validate(tile); return counts[tile]; }
        public bool HasSpace(int tile) { Validate(tile); return counts[tile] < tile; }
        public bool TryEnter(int tile)
        {
            if (!HasSpace(tile)) return false;
            counts[tile]++;
            return true;
        }
        public void Leave(int tile)
        {
            Validate(tile);
            if (counts[tile] == 0) throw new InvalidOperationException("Tile is already empty.");
            counts[tile]--;
        }
        public void Clear() => Array.Clear(counts, 0, counts.Length);
        private static void Validate(int tile)
        {
            if (tile < 1 || tile > RoadLayout.FinalTile)
                throw new ArgumentOutOfRangeException(nameof(tile));
        }
    }

    /// <summary>One warrior. Animation plans do not transfer ownership until arrival.</summary>
    public sealed class WarriorSimulation
    {
        private static readonly RoadPorts[] directions =
            { RoadPorts.West, RoadPorts.North, RoadPorts.South, RoadPorts.East };
        private readonly HashSet<int> visited = new HashSet<int>();
        public TileOccupancy Occupancy { get; } = new TileOccupancy();
        public int CurrentTile { get; private set; }
        public int DeliveredCount { get; private set; }
        public bool Completed { get; private set; }

        public void Reset()
        {
            CurrentTile = DeliveredCount = 0;
            Completed = false;
            visited.Clear();
            Occupancy.Clear();
        }

        public bool QueueNext()
        {
            if (!Completed) return false;
            Completed = false;
            visited.Clear();
            return true;
        }

        public bool CanEnter(SlidingBoard board, bool finalTilePresent)
        {
            if (CurrentTile != 0 || Completed) return false;
            int tile = RoadNetwork.GetTileAt(board, RoadLayout.EntryCell, finalTilePresent);
            return tile != 0 && (RoadLayout.GetPorts(tile) & RoadPorts.East) != 0
                && Occupancy.HasSpace(tile);
        }

        public bool TryEnter(SlidingBoard board, bool finalTilePresent)
        {
            if (!CanEnter(board, finalTilePresent)) return false;
            int tile = RoadNetwork.GetTileAt(board, RoadLayout.EntryCell, finalTilePresent);
            if (!Occupancy.TryEnter(tile)) return false;
            CurrentTile = tile;
            visited.Add(tile);
            return true;
        }

        public int GetCurrentCell(SlidingBoard board, bool finalTilePresent)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (CurrentTile == 0) return -1;
            if (CurrentTile == RoadLayout.FinalTile)
                return finalTilePresent && board.IsSolved ? board.EmptyIndex : -1;
            return board.GetIndexOf(CurrentTile);
        }

        public bool CanDeliver(SlidingBoard board, bool finalTilePresent)
        {
            return CurrentTile != 0 && GetCurrentCell(board, finalTilePresent) == RoadLayout.CastleCell
                && (RoadLayout.GetPorts(CurrentTile) & RoadPorts.West) != 0;
        }

        public bool TryDeliver(SlidingBoard board, bool finalTilePresent)
        {
            if (!CanDeliver(board, finalTilePresent)) return false;
            Occupancy.Leave(CurrentTile);
            CurrentTile = 0;
            Completed = true;
            DeliveredCount++;
            return true;
        }

        public void NotifyBoardChanged()
        {
            visited.Clear();
            if (CurrentTile != 0) visited.Add(CurrentTile);
        }

        public bool TryMoveTo(SlidingBoard board, bool finalTilePresent, int tile)
        {
            int from = GetCurrentCell(board, finalTilePresent);
            if (from < 0 || tile < 1 || tile > RoadLayout.FinalTile || tile == CurrentTile) return false;
            foreach (RoadPorts direction in directions)
            {
                if (!RoadNetwork.TryGetNeighbor(from, direction, out int next)
                    || RoadNetwork.GetTileAt(board, next, finalTilePresent) != tile) continue;
                if (!RoadNetwork.AreConnected(RoadLayout.GetPorts(CurrentTile),
                    RoadLayout.GetPorts(tile), direction) || !Occupancy.TryEnter(tile)) return false;
                Occupancy.Leave(CurrentTile);
                CurrentTile = tile;
                visited.Add(tile);
                return true;
            }
            return false;
        }

        /// <summary>Prefer a castle path; otherwise explore the nearest unvisited tile.
        /// Revisit cells only to reach unexplored branches, avoiding endless bouncing.</summary>
        public int GetNextTile(SlidingBoard board, bool finalTilePresent)
        {
            int start = GetCurrentCell(board, finalTilePresent);
            if (start < 0 || CanDeliver(board, finalTilePresent)) return 0;
            var parents = new int[SlidingBoard.CellCount];
            for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            parents[start] = start;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            int unexplored = -1, destination = -1;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                int tile = RoadNetwork.GetTileAt(board, cell, finalTilePresent);
                if (cell == RoadLayout.CastleCell && (RoadLayout.GetPorts(tile) & RoadPorts.West) != 0)
                {
                    destination = cell;
                    break;
                }
                if (cell != start && unexplored < 0 && !visited.Contains(tile)) unexplored = cell;
                foreach (RoadPorts direction in directions)
                {
                    if (!RoadNetwork.TryGetNeighbor(cell, direction, out int next) || parents[next] >= 0) continue;
                    int nextTile = RoadNetwork.GetTileAt(board, next, finalTilePresent);
                    if (nextTile == 0 || !Occupancy.HasSpace(nextTile)
                        || !RoadNetwork.AreConnected(RoadLayout.GetPorts(tile),
                            RoadLayout.GetPorts(nextTile), direction)) continue;
                    parents[next] = cell;
                    queue.Enqueue(next);
                }
            }
            if (destination < 0) destination = unexplored;
            if (destination < 0) return 0;
            while (parents[destination] != start) destination = parents[destination];
            return RoadNetwork.GetTileAt(board, destination, finalTilePresent);
        }
    }
}
