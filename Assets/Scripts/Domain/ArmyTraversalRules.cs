using System;

namespace Pyatnashki.Domain
{
    public enum WarriorRole { Infantry, Commander, Cartographer }

    public static class ArmyTraversalRules
    {
        public const float SolvedStepSeconds = 0.08f;
        public const float SolvedSpawnSeconds = 0.05f;
        public const float SolvedCooldownSeconds = 0.01f;

        public static void ValidateChances(double commander, double cartographer)
        {
            if (double.IsNaN(commander) || double.IsNaN(cartographer) || commander < 0 || cartographer < 0
                || commander + cartographer > 1) throw new ArgumentException("Specialist chances must be nonnegative and sum to at most one.");
        }
        public static WarriorRole Roll(double sample, double commander, double cartographer)
        {
            ValidateChances(commander, cartographer);
            if (double.IsNaN(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
            return sample < commander ? WarriorRole.Commander
                : sample < commander + cartographer ? WarriorRole.Cartographer : WarriorRole.Infantry;
        }
        public static float StepSeconds(float baseSeconds, float multiplier, bool solved)
        {
            float normal = Math.Max(0.05f, baseSeconds / Math.Max(1, multiplier));
            return solved ? Math.Min(normal, SolvedStepSeconds) : normal;
        }
    }
}
