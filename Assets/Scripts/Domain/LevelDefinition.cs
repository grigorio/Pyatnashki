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
        private readonly int[] trapTiles, unlockFrom;
        public int[] TrapTiles => (int[])trapTiles.Clone();
        public int[] UnlockFrom => (int[])unlockFrom.Clone();
        public int TrapCharges { get; }
        public int MinimumSupply => (Mode == LevelMode.Capture ? CaptureTarget
            : (int)Math.Min(EnemyTotal, Math.Floor((Duration + 1e-9) / EnemyInterval)) + 1)
            + trapTiles.Length * TrapCharges;

        public LevelDefinition(string name, LevelMode mode, int supply, int captureTarget,
            double duration, int scrambleMoves, int enemyTotal = 0, double enemyInterval = 1,
            double fourStarTimeFraction = 0.25, double fiveStarTimeFraction = 0.5,
            int fourStarAdvantage = 3, int fiveStarAdvantage = 6,
            int[] trapTiles = null, int trapCharges = 1, int[] unlockFrom = null)
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
            this.trapTiles = trapTiles == null ? new int[0] : (int[])trapTiles.Clone();
            this.unlockFrom = unlockFrom == null ? new int[0] : (int[])unlockFrom.Clone();
            if (trapCharges < 1 || trapCharges > 10) throw new ArgumentOutOfRangeException(nameof(trapCharges));
            var unique = new System.Collections.Generic.HashSet<int>();
            foreach (int tile in this.trapTiles)
                if (tile < 1 || tile > 15 || !unique.Add(tile)) throw new ArgumentException("Trap tile IDs must be unique and within 1–15.");
            foreach (int prerequisite in this.unlockFrom)
                if (prerequisite < 0) throw new ArgumentException("Unlock level IDs must be nonnegative.");
            TrapCharges = trapCharges;
            if (MinimumSupply > Supply) throw new ArgumentException("Supply must cover the objective and all possible trap losses.");
        }

        public LevelDefinition WithSupply(int supply) => new LevelDefinition(Name, Mode, supply, CaptureTarget,
            Duration, ScrambleMoves, EnemyTotal, EnemyInterval, FourStarTimeFraction, FiveStarTimeFraction,
            FourStarAdvantage, FiveStarAdvantage, trapTiles, TrapCharges, unlockFrom);
    }
}
