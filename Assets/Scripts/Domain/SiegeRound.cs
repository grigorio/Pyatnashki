using System;

namespace Pyatnashki.Domain
{
    public enum SiegeRoundState { Ready, Running, Won, Lost }

    /// <summary>Castle capture and deadline are independent of solving the numbered board.</summary>
    public sealed class SiegeRound
    {
        public SiegeRoundState State { get; private set; }
        public LevelMode Mode { get; private set; }
        public int EnemyTotal { get; private set; }
        public int EnemyCount { get; private set; }
        public double EnemyInterval { get; private set; }
        public int Supply { get; private set; }
        public int Target { get; private set; }
        public int Delivered { get; private set; }
        public int Casualties { get; private set; }
        public double Duration { get; private set; }
        public double Remaining { get; private set; }
        public double Elapsed => Duration - Remaining;

        public void Start(int supply, int target, double duration)
        {
            if (supply < 1) throw new ArgumentOutOfRangeException(nameof(supply));
            if (target < 1 || target > supply) throw new ArgumentOutOfRangeException(nameof(target));
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            Mode = LevelMode.Capture;
            EnemyTotal = EnemyCount = 0;
            EnemyInterval = 1;
            Supply = supply;
            Target = target;
            Duration = Remaining = duration;
            Delivered = Casualties = 0;
            State = SiegeRoundState.Running;
        }

        public void Start(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            Start(level.Supply, level.CaptureTarget, level.Duration);
            Mode = level.Mode;
            EnemyTotal = level.EnemyTotal;
            EnemyInterval = level.EnemyInterval;
        }

        public void Reset()
        {
            Mode = LevelMode.Capture;
            EnemyTotal = EnemyCount = 0;
            EnemyInterval = 1;
            State = SiegeRoundState.Ready;
            Supply = Target = Delivered = Casualties = 0;
            Duration = Remaining = 0;
        }

        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != SiegeRoundState.Running) return;
            Remaining = Math.Max(0, Remaining - seconds);
            if (Mode == LevelMode.Defense)
                EnemyCount = (int)Math.Min(EnemyTotal, Math.Floor((Elapsed + 1e-9) / EnemyInterval));
            if (Remaining == 0)
                State = Mode == LevelMode.Defense && Delivered > EnemyCount
                    ? SiegeRoundState.Won : SiegeRoundState.Lost;
        }

        public bool RecordDelivery()
        {
            if (State != SiegeRoundState.Running || Delivered >= Supply) return false;
            Delivered++;
            if (Mode == LevelMode.Capture && Delivered >= Target) State = SiegeRoundState.Won;
            return true;
        }
        public bool RecordCasualty()
        {
            if (State != SiegeRoundState.Running || Delivered + Casualties >= Supply) return false;
            Casualties++;
            int needed = Mode == LevelMode.Capture ? Target
                : (int)Math.Min(EnemyTotal, Math.Floor((Duration + 1e-9) / EnemyInterval)) + 1;
            if (Supply - Casualties < needed) State = SiegeRoundState.Lost;
            return true;
        }
    }
}
