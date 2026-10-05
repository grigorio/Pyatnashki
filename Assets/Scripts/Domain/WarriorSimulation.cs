using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    /// <summary>Occupancy is keyed by tile identity, so sliding never changes it.</summary>
    public sealed class TileOccupancy
    {
        private readonly int[] counts = new int[RoadLayout.FinalTile + 1];
        private readonly int[] reservations = new int[RoadLayout.FinalTile + 1];
        public bool UnlimitedCapacity { get; private set; }

        public void UpdateCapacityMode(SlidingBoard board, bool finalTilePresent)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            UnlimitedCapacity = board.IsSolved && finalTilePresent;
        }

        public int GetCount(int tile) { Validate(tile); return counts[tile]; }
        public int GetReservedCount(int tile) { Validate(tile); return reservations[tile]; }
        public bool HasSpace(int tile)
        {
            Validate(tile);
            return UnlimitedCapacity || counts[tile] + reservations[tile] < tile;
        }
        public bool TryReserve(int tile)
        {
            if (!HasSpace(tile)) return false;
            reservations[tile]++;
            return true;
        }
        public void ReleaseReservation(int tile)
        {
            Validate(tile);
            if (reservations[tile] == 0) throw new InvalidOperationException("No reservation to release.");
            reservations[tile]--;
        }
        public bool TryEnterReserved(int tile)
        {
            Validate(tile);
            if (reservations[tile] == 0) return false;
            reservations[tile]--;
            counts[tile]++;
            return true;
        }
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
        public void Clear()
        {
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(reservations, 0, reservations.Length);
            UnlimitedCapacity = false;
        }
        private static void Validate(int tile)
        {
            if (tile < 1 || tile > RoadLayout.FinalTile)
                throw new ArgumentOutOfRangeException(nameof(tile));
        }
    }

    public static class WarriorTraversal
    {
        /// <summary>A tile is entered when the leading edge overlaps its near edge.
        /// The gap between tiles is included in centreDistance.</summary>
        public static bool HasEnteredDestination(double travelled, double centreDistance,
            double destinationSize, double markerSize)
        {
            return travelled > centreDistance - destinationSize * 0.5 - markerSize * 0.5;
        }
    }

    /// <summary>One actor with a shared ledger. Ownership changes at the tile boundary.</summary>
    public sealed class WarriorSimulation
    {
        private static readonly RoadPorts[] directions =
            { RoadPorts.West, RoadPorts.North, RoadPorts.South, RoadPorts.East };
        private readonly HashSet<int> visited = new HashSet<int>();
        public TileOccupancy Occupancy { get; }
        public int ReservedTile { get; private set; }
        public int CurrentTile { get; private set; }
        public int DeliveredCount { get; private set; }
        public bool Completed { get; private set; }

        private readonly bool reverseRoute;
        private int SourceCell => reverseRoute ? RoadLayout.CastleCell : RoadLayout.EntryCell;
        private RoadPorts SourcePort => reverseRoute ? RoadLayout.CastlePort : RoadLayout.EntryPort;
        private int DestinationCell => reverseRoute ? RoadLayout.EntryCell : RoadLayout.CastleCell;
        private RoadPorts DestinationPort => reverseRoute ? RoadLayout.EntryPort : RoadLayout.CastlePort;

        public WarriorSimulation(TileOccupancy occupancy = null, bool reverseRoute = false)
        {
            Occupancy = occupancy ?? new TileOccupancy();
            this.reverseRoute = reverseRoute;
        }

        public void Reset()
        {
            CancelReservation();
            if (CurrentTile != 0) Occupancy.Leave(CurrentTile);
            CurrentTile = DeliveredCount = 0;
            Completed = false;
            visited.Clear();
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
            int tile = RoadNetwork.GetTileAt(board, SourceCell, finalTilePresent);
            return tile != 0 && (RoadLayout.GetPorts(tile) & SourcePort) != 0
                && (ReservedTile == tile || Occupancy.HasSpace(tile));
        }

        public bool TryEnter(SlidingBoard board, bool finalTilePresent)
        {
            if (!CanEnter(board, finalTilePresent)) return false;
            int tile = RoadNetwork.GetTileAt(board, SourceCell, finalTilePresent);
            if (!EnterDestination(tile)) return false;
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
            return CurrentTile != 0 && GetCurrentCell(board, finalTilePresent) == DestinationCell
                && (RoadLayout.GetPorts(CurrentTile) & DestinationPort) != 0;
        }

        public bool TryDeliver(SlidingBoard board, bool finalTilePresent)
        {
            if (!CanDeliver(board, finalTilePresent)) return false;
            CancelReservation();
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

        public bool ReserveDestination(SlidingBoard board, bool finalTilePresent, int tile)
        {
            if (ReservedTile != 0 || Completed || tile < 1 || tile > RoadLayout.FinalTile) return false;
            bool valid = CurrentTile == 0
                ? CanEnter(board, finalTilePresent) &&
                    RoadNetwork.GetTileAt(board, SourceCell, finalTilePresent) == tile
                : CanMoveTo(board, finalTilePresent, tile);
            if (!valid || !Occupancy.TryReserve(tile)) return false;
            ReservedTile = tile;
            return true;
        }

        public void CancelReservation()
        {
            if (ReservedTile == 0) return;
            Occupancy.ReleaseReservation(ReservedTile);
            ReservedTile = 0;
        }

        private bool EnterDestination(int tile)
        {
            if (ReservedTile != 0 && ReservedTile != tile) return false;
            bool entered = ReservedTile == tile ? Occupancy.TryEnterReserved(tile) : Occupancy.TryEnter(tile);
            if (entered) ReservedTile = 0;
            return entered;
        }

        private bool CanMoveTo(SlidingBoard board, bool finalTilePresent, int tile)
        {
            int from = GetCurrentCell(board, finalTilePresent);
            if (from < 0 || tile < 1 || tile > RoadLayout.FinalTile || tile == CurrentTile) return false;
            foreach (RoadPorts direction in directions)
                if (RoadNetwork.TryGetNeighbor(from, direction, out int next)
                    && RoadNetwork.GetTileAt(board, next, finalTilePresent) == tile)
                    return RoadNetwork.AreConnected(RoadLayout.GetPorts(CurrentTile),
                        RoadLayout.GetPorts(tile), direction);
            return false;
        }

        public bool TryMoveTo(SlidingBoard board, bool finalTilePresent, int tile)
        {
            if (!CanMoveTo(board, finalTilePresent, tile) || !EnterDestination(tile)) return false;
            Occupancy.Leave(CurrentTile);
            CurrentTile = tile;
            visited.Add(tile);
            return true;
        }

        /// <summary>Prefer a castle path; otherwise explore the nearest unvisited tile.
        /// Revisit cells only to reach unexplored branches, avoiding endless bouncing.</summary>
        public int GetNextTile(SlidingBoard board, bool finalTilePresent)
        {
            int next = FindNextTile(board, finalTilePresent, false, false);
            // A full castle-path tile causes waiting, not exploration backwards.
            if (next != 0) return Occupancy.HasSpace(next) ? next : 0;
            return FindNextTile(board, finalTilePresent, true, true);
        }

        private int FindNextTile(SlidingBoard board, bool finalTilePresent,
            bool respectCapacity, bool allowExploration)
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
                if (cell == DestinationCell && (RoadLayout.GetPorts(tile) & DestinationPort) != 0)
                {
                    destination = cell;
                    break;
                }
                if (cell != start && unexplored < 0 && !visited.Contains(tile)) unexplored = cell;
                foreach (RoadPorts direction in directions)
                {
                    if (!RoadNetwork.TryGetNeighbor(cell, direction, out int next) || parents[next] >= 0) continue;
                    int nextTile = RoadNetwork.GetTileAt(board, next, finalTilePresent);
                    if (nextTile == 0 || (respectCapacity && !Occupancy.HasSpace(nextTile))
                        || !RoadNetwork.AreConnected(RoadLayout.GetPorts(tile),
                            RoadLayout.GetPorts(nextTile), direction)) continue;
                    parents[next] = cell;
                    queue.Enqueue(next);
                }
            }
            if (destination < 0 && allowExploration) destination = unexplored;
            if (destination < 0) return 0;
            while (parents[destination] != start) destination = parents[destination];
            return RoadNetwork.GetTileAt(board, destination, finalTilePresent);
        }
    }
}
