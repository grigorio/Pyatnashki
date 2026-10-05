using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    [Flags]
    public enum RoadPorts
    {
        None = 0, North = 1, East = 2, South = 4, West = 8,
        All = North | East | South | West
    }

    /// <summary>Roads belong to tile identities, never to their current cells.</summary>
    public static class RoadLayout
    {
        public const int FinalTile = 16;
        public const int EntryCell = 15;
        public const int CastleCell = 1;
        public const RoadPorts EntryPort = RoadPorts.South;
        public const RoadPorts CastlePort = RoadPorts.North;
        private static readonly int[] solution =
            { 16, 12, 8, 4, 3, 7, 11, 15, 14, 13, 9, 10, 6, 5, 1, 2 };
        private static readonly RoadPorts[] ports = BuildPorts();
        public static IReadOnlyList<int> SolutionTileOrder { get; } = Array.AsReadOnly(solution);

        public static RoadPorts GetPorts(int tile)
        {
            if (tile < 1 || tile > FinalTile)
                throw new ArgumentOutOfRangeException(nameof(tile));
            return ports[tile];
        }

        private static RoadPorts[] BuildPorts()
        {
            var result = new RoadPorts[FinalTile + 1];
            for (int i = 0; i < solution.Length; i++)
            {
                int tile = solution[i];
                result[tile] |= i == 0 ? EntryPort : DirectionTo(tile, solution[i - 1]);
                result[tile] |= i == solution.Length - 1
                    ? CastlePort : DirectionTo(tile, solution[i + 1]);
            }
            result[11] = RoadPorts.All;
            return result;
        }

        private static RoadPorts DirectionTo(int from, int to)
        {
            int a = from - 1, b = to - 1;
            if (a / 4 == b / 4) return b > a ? RoadPorts.East : RoadPorts.West;
            return b > a ? RoadPorts.South : RoadPorts.North;
        }
    }

    public sealed class RoadNetworkResult
    {
        private readonly bool[] reachable;
        public int ReachableTileCount { get; }
        public bool HasCastleRoute => CastlePath.Count > 0;
        /// <summary>Shortest connected path expressed as board cell indices.</summary>
        public IReadOnlyList<int> CastlePath { get; }

        internal RoadNetworkResult(bool[] reachable, int count, int[] path)
        {
            this.reachable = reachable;
            ReachableTileCount = count;
            CastlePath = Array.AsReadOnly(path);
        }

        public bool IsReachable(int cell)
        {
            if (cell < 0 || cell >= SlidingBoard.CellCount)
                throw new ArgumentOutOfRangeException(nameof(cell));
            return reachable[cell];
        }
    }

    public static class RoadNetwork
    {
        private static readonly RoadPorts[] directions =
            { RoadPorts.North, RoadPorts.East, RoadPorts.South, RoadPorts.West };

        public static int GetTileAt(SlidingBoard board, int cell, bool finalTilePresent = false)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            int tile = board.GetTile(cell);
            return tile == 0 && finalTilePresent && board.IsSolved ? RoadLayout.FinalTile : tile;
        }

        public static bool AreConnected(RoadPorts from, RoadPorts to, RoadPorts direction)
        {
            RoadPorts opposite = Opposite(direction);
            return (from & direction) != 0 && (to & opposite) != 0;
        }

        public static bool TryGetNeighbor(int cell, RoadPorts direction, out int neighbor)
        {
            if (cell < 0 || cell >= SlidingBoard.CellCount)
                throw new ArgumentOutOfRangeException(nameof(cell));
            Opposite(direction); // Reject composite flags and None.
            int row = cell / SlidingBoard.Width, column = cell % SlidingBoard.Width;
            switch (direction)
            {
                case RoadPorts.North: row--; break;
                case RoadPorts.East: column++; break;
                case RoadPorts.South: row++; break;
                case RoadPorts.West: column--; break;
            }
            neighbor = -1;
            if (row < 0 || row >= 4 || column < 0 || column >= 4) return false;
            neighbor = row * 4 + column;
            return true;
        }

        public static RoadNetworkResult Analyze(SlidingBoard board, bool finalTilePresent = false)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var cellPorts = new RoadPorts[SlidingBoard.CellCount];
            for (int cell = 0; cell < cellPorts.Length; cell++)
            {
                int tile = GetTileAt(board, cell, finalTilePresent);
                cellPorts[cell] = tile == 0 ? RoadPorts.None : RoadLayout.GetPorts(tile);
            }
            var reachable = new bool[SlidingBoard.CellCount];
            var parents = new int[SlidingBoard.CellCount];
            for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            var queue = new Queue<int>();
            int count = 0;
            if ((cellPorts[RoadLayout.EntryCell] & RoadLayout.EntryPort) != 0)
            {
                reachable[RoadLayout.EntryCell] = true;
                queue.Enqueue(RoadLayout.EntryCell);
                count = 1;
            }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (RoadPorts direction in directions)
                {
                    if (!TryGetNeighbor(current, direction, out int next) || reachable[next]) continue;
                    if (!AreConnected(cellPorts[current], cellPorts[next], direction)) continue;
                    reachable[next] = true;
                    parents[next] = current;
                    count++;
                    queue.Enqueue(next);
                }
            }
            var path = new List<int>();
            if (reachable[RoadLayout.CastleCell] &&
                (cellPorts[RoadLayout.CastleCell] & RoadLayout.CastlePort) != 0)
            {
                for (int cell = RoadLayout.CastleCell; cell >= 0; cell = parents[cell]) path.Add(cell);
                path.Reverse();
            }
            return new RoadNetworkResult(reachable, count, path.ToArray());
        }

        private static RoadPorts Opposite(RoadPorts direction)
        {
            switch (direction)
            {
                case RoadPorts.North: return RoadPorts.South;
                case RoadPorts.East: return RoadPorts.West;
                case RoadPorts.South: return RoadPorts.North;
                case RoadPorts.West: return RoadPorts.East;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
