using System;

namespace Pyatnashki.Domain
{
    public enum SiegeRoundState { Ready, Running, Won, Lost }

    /// <summary>Castle capture and deadline are independent of solving the numbered board.</summary>
    public sealed class SiegeRound
    {
        public SiegeRoundState State { get; private set; }
        public int Supply { get; private set; }
        public int Target { get; private set; }
        public int Delivered { get; private set; }
        public double Duration { get; private set; }
        public double Remaining { get; private set; }
        public double Elapsed => Duration - Remaining;

        public void Start(int supply, int target, double duration)
        {
            if (supply < 1) throw new ArgumentOutOfRangeException(nameof(supply));
            if (target < 1 || target > supply) throw new ArgumentOutOfRangeException(nameof(target));
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            Supply = supply;
            Target = target;
            Duration = Remaining = duration;
            Delivered = 0;
            State = SiegeRoundState.Running;
        }

        public void Reset()
        {
            State = SiegeRoundState.Ready;
            Supply = Target = Delivered = 0;
            Duration = Remaining = 0;
        }

        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (State != SiegeRoundState.Running) return;
            Remaining = Math.Max(0, Remaining - seconds);
            if (Remaining == 0) State = SiegeRoundState.Lost;
        }

        public bool RecordDelivery()
        {
            if (State != SiegeRoundState.Running) return false;
            Delivered++;
            if (Delivered >= Target) State = SiegeRoundState.Won;
            return true;
        }
    }
}
