using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    public static class CampaignMapRules
    {
        public static bool IsAvailable(IList<LevelDefinition> levels, CampaignProgress progress, int index)
        {
            if (index < 0 || index >= levels.Count) return false;
            if (progress.GetBest(index) >= 3) return true;
            int[] neighbors = levels[index].UnlockFrom;
            if (neighbors.Length == 0) return true;
            foreach (int neighbor in neighbors)
                if (progress.GetBest(neighbor) >= 3) return true;
            return false;
        }

        public static void Validate(IList<LevelDefinition> levels)
        {
            var reachable = new bool[levels.Count];
            for (int pass = 0; pass < levels.Count; pass++)
                for (int i = 0; i < levels.Count; i++)
                {
                    int[] from = levels[i].UnlockFrom;
                    if (from.Length == 0) reachable[i] = true;
                    foreach (int id in from)
                    {
                        if (id < 0 || id >= levels.Count || id == i) throw new ArgumentException("Invalid campaign map neighbor.");
                        if (reachable[id]) reachable[i] = true;
                    }
                }
            foreach (bool value in reachable)
                if (!value) throw new ArgumentException("Campaign map contains an unreachable region.");
        }

        public static string RulerTitle(int settlements)
        {
            string[] titles = { "Малий феодал", "Землевласник", "Барон", "Віконт", "Граф", "Маркграф",
                "Герцог", "Великий герцог", "Князь", "Великий князь", "Король", "Верховний король",
                "Великий король", "Імператор", "Великий імператор" };
            int[] borders = { 0, 1, 2, 3, 5, 8, 12, 18, 25, 35, 50, 75, 100, 150, 200 };
            int rank = 0;
            for (int i = 1; i < borders.Length; i++) if (settlements >= borders[i]) rank = i;
            return titles[rank];
        }
    }

    public sealed class TrapLedger
    {
        private readonly Dictionary<int, int> remaining = new Dictionary<int, int>();
        public TrapLedger(LevelDefinition level)
        {
            foreach (int tile in level.TrapTiles) remaining[tile] = level.TrapCharges;
        }
        public int GetCharges(int tile) => remaining.TryGetValue(tile, out int count) ? count : 0;
        public bool Trigger(int tile)
        {
            int count = GetCharges(tile);
            if (count == 0) return false;
            remaining[tile] = count - 1; return true;
        }
    }
}
