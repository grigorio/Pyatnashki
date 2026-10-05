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

        public LevelDefinition(string name, LevelMode mode, int supply, int captureTarget,
            double duration, int scrambleMoves, int enemyTotal = 0, double enemyInterval = 1)
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
            Name = name; Mode = mode; Supply = supply; CaptureTarget = mode == LevelMode.Capture ? captureTarget : 1;
            Duration = duration; ScrambleMoves = scrambleMoves;
            EnemyTotal = mode == LevelMode.Defense ? enemyTotal : 0;
            EnemyInterval = enemyInterval;
        }
    }
}
