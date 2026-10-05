using System;

namespace Pyatnashki.Domain
{
    public enum LevelMode { Capture, Defense }

    /// <summary>Validated, immutable rules for one campaign level.</summary>
    public sealed class LevelDefinition
    {
        public string Name { get; }
        public LevelMode Mode { get; }
        public int Supply { get; }
        public int CaptureTarget { get; }
        public double Duration { get; }
        public int ScrambleMoves { get; }
        public int EnemyTotal { get; }
        public double EnemyInterval { get; }

        public double FourStarTimeFraction { get; }
        public double FiveStarTimeFraction { get; }
        public int FourStarAdvantage { get; }
        public int FiveStarAdvantage { get; }

        public LevelDefinition(string name, LevelMode mode, int supply, int captureTarget,
            double duration, int scrambleMoves, int enemyTotal = 0, double enemyInterval = 1,
            double fourStarTimeFraction = 0.25, double fiveStarTimeFraction = 0.5,
            int fourStarAdvantage = 3, int fiveStarAdvantage = 6)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Level name is required.", nameof(name));
            if (mode != LevelMode.Capture && mode != LevelMode.Defense) throw new ArgumentOutOfRangeException(nameof(mode));
            if (supply < 1 || supply > 99) throw new ArgumentOutOfRangeException(nameof(supply));
            if (mode == LevelMode.Capture && (captureTarget < 1 || captureTarget > supply)) throw new ArgumentOutOfRangeException(nameof(captureTarget));
            if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
            if (scrambleMoves < 1) throw new ArgumentOutOfRangeException(nameof(scrambleMoves));
            if (enemyTotal < 0 || enemyTotal > 99 || (mode == LevelMode.Defense && enemyTotal == 0)) throw new ArgumentOutOfRangeException(nameof(enemyTotal));
            if (enemyInterval <= 0 || double.IsNaN(enemyInterval) || double.IsInfinity(enemyInterval)) throw new ArgumentOutOfRangeException(nameof(enemyInterval));
            if (mode == LevelMode.Defense && Math.Min(enemyTotal, Math.Floor((duration + 1e-9) / enemyInterval)) >= supply)
                throw new ArgumentException("Defender supply must allow a numerical advantage at the deadline.");
            if (fourStarTimeFraction <= 0 || fiveStarTimeFraction > 1
                || fourStarTimeFraction >= fiveStarTimeFraction
                || double.IsNaN(fourStarTimeFraction) || double.IsNaN(fiveStarTimeFraction))
                throw new ArgumentException("Time thresholds must satisfy 0 < four stars < five stars <= 1.");
            if (fourStarAdvantage < 2 || fiveStarAdvantage <= fourStarAdvantage)
                throw new ArgumentException("Defense advantage thresholds must satisfy 2 <= four stars < five stars.");
            FourStarTimeFraction = fourStarTimeFraction; FiveStarTimeFraction = fiveStarTimeFraction;
            FourStarAdvantage = fourStarAdvantage; FiveStarAdvantage = fiveStarAdvantage;
            Name = name; Mode = mode; Supply = supply; CaptureTarget = mode == LevelMode.Capture ? captureTarget : 1;
            Duration = duration; ScrambleMoves = scrambleMoves;
            EnemyTotal = mode == LevelMode.Defense ? enemyTotal : 0;
            EnemyInterval = enemyInterval;
        }
    }
}
