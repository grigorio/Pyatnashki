using System;

namespace Pyatnashki.Domain
{
    public static class LevelRating
    {
        public static int Calculate(LevelDefinition level, SiegeRound round)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (round == null) throw new ArgumentNullException(nameof(round));
            if (round.State != SiegeRoundState.Won && round.State != SiegeRoundState.Lost) return 0;
            if (round.Mode != level.Mode) throw new ArgumentException("Level and round modes must match.");
            if (round.State == SiegeRoundState.Lost)
            {
                int required = level.Mode == LevelMode.Capture ? level.CaptureTarget : round.EnemyCount + 1;
                double completion = (double)round.Delivered / required;
                return completion >= 0.8 ? 2 : completion >= 0.5 ? 1 : 0;
            }
            if (level.Mode == LevelMode.Capture)
            {
                double remainingFraction = round.Remaining / round.Duration;
                return remainingFraction >= level.FiveStarTimeFraction ? 5
                    : remainingFraction >= level.FourStarTimeFraction ? 4 : 3;
            }
            int advantage = round.Delivered - round.EnemyCount;
            return advantage >= level.FiveStarAdvantage ? 5
                : advantage >= level.FourStarAdvantage ? 4 : 3;
        }
    }

    /// <summary>Best ratings never decrease. Only the preceding level's three-star result unlocks a level.</summary>
    public sealed class CampaignProgress
    {
        private readonly int[] best;
        public int Count => best.Length;

        public CampaignProgress(int[] ratings)
        {
            if (ratings == null || ratings.Length == 0) throw new ArgumentException("Campaign ratings are required.", nameof(ratings));
            foreach (int rating in ratings)
                if (rating < 0 || rating > 5) throw new ArgumentOutOfRangeException(nameof(ratings));
            best = (int[])ratings.Clone();
        }

        public int GetBest(int index) { ValidateIndex(index); return best[index]; }
        public bool IsUnlocked(int index) { ValidateIndex(index); return index == 0 || best[index - 1] >= 3; }

        public bool Record(int index, int stars)
        {
            ValidateIndex(index);
            if (stars < 0 || stars > 5) throw new ArgumentOutOfRangeException(nameof(stars));
            if (stars <= best[index]) return false;
            best[index] = stars;
            return true;
        }

        public void MigrateLegacyUnlocks(int unlockedCount)
        {
            if (unlockedCount < 1 || unlockedCount > Count) throw new ArgumentOutOfRangeException(nameof(unlockedCount));
            for (int i = 0; i < unlockedCount - 1; i++) Record(i, 3);
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
