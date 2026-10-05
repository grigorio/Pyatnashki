using System;
using Pyatnashki.Domain;
using UnityEngine;

namespace Pyatnashki
{
    [Serializable]
    public sealed class LevelSettings
    {
        public string name = "Новий рівень";
        public LevelMode mode;
        [Range(1, 99)] public int supply = 20;
        [Min(1)] public int captureTarget = 12;
        [Min(0.1f)] public float duration = 120;
        [Min(1)] public int scrambleMoves = 24;
        [Range(0, 99)] public int enemyTotal = 10;
        [Min(0.1f)] public float enemyInterval = 5;

        [Range(0.01f, 1)] public float fourStarTimeFraction = 0.25f;
        [Range(0.01f, 1)] public float fiveStarTimeFraction = 0.5f;
        [Min(2)] public int fourStarAdvantage = 3;
        [Min(3)] public int fiveStarAdvantage = 6;

        public LevelDefinition ToDefinition() => new LevelDefinition(name, mode, supply,
            captureTarget, duration, scrambleMoves, enemyTotal, enemyInterval,
            fourStarTimeFraction, fiveStarTimeFraction, fourStarAdvantage, fiveStarAdvantage);

        public static LevelSettings[] Defaults() => new[]
        {
            new LevelSettings { name = "Перша облога" },
            new LevelSettings { name = "Збір захисників", mode = LevelMode.Defense,
                supply = 20, duration = 120, scrambleMoves = 16, enemyTotal = 10, enemyInterval = 8 },
            new LevelSettings { name = "Укріплений замок", supply = 24, captureTarget = 16,
                duration = 105, scrambleMoves = 32 },
            new LevelSettings { name = "Ворог на кордоні", mode = LevelMode.Defense,
                supply = 24, duration = 105, scrambleMoves = 28, enemyTotal = 16, enemyInterval = 5 },
            new LevelSettings { name = "Велика облога", supply = 32, captureTarget = 22,
                duration = 90, scrambleMoves = 40 },
            new LevelSettings { name = "Останній рубіж", mode = LevelMode.Defense,
                supply = 32, duration = 90, scrambleMoves = 36, enemyTotal = 22, enemyInterval = 3 }
        };
    }
}
