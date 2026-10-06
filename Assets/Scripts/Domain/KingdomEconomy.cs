using System;
using System.Collections.Generic;

namespace Pyatnashki.Domain
{
    public enum CapitalStat { Strength, Terrain, Economy, Diplomacy, Reinforcements, Marching }
    public enum SettlementKind { Village, TradingTown, BorderFort }

    public sealed class SettlementDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public SettlementKind Kind { get; }
        public int CaptureLevel { get; }
        public int DefenseLevel { get; }
        public double BaseHourlyIncome { get; }
        public SettlementDefinition(string id, string name, SettlementKind kind, int captureLevel,
            int defenseLevel, double baseHourlyIncome)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Settlement identity is required.");
            if ((int)kind < 0 || (int)kind > 2) throw new ArgumentOutOfRangeException(nameof(kind));
            if (captureLevel < 0 || defenseLevel < -1) throw new ArgumentOutOfRangeException(nameof(captureLevel));
            if (baseHourlyIncome <= 0 || double.IsNaN(baseHourlyIncome) || double.IsInfinity(baseHourlyIncome)) throw new ArgumentOutOfRangeException(nameof(baseHourlyIncome));
            Id = id; Name = name; Kind = kind; CaptureLevel = captureLevel;
            DefenseLevel = defenseLevel; BaseHourlyIncome = baseHourlyIncome;
        }
    }

    [Serializable]
    public sealed class SettlementSave
    {
        public string id;
        public int captureStars, defenseStars;
        public double stored;
    }

    [Serializable]
    public sealed class KingdomSave
    {
        public int version = 1;
        public long gold, lastUtcSeconds;
        public int[] capital = new int[6];
        public SettlementSave[] settlements;
    }

    /// <summary>Passive tribute, capped storage and atomic purchases. Time is injected for reproducible checks.</summary>
    public sealed class KingdomEconomy
    {
        private readonly SettlementDefinition[] definitions;
        private readonly SettlementSave[] states;
        private readonly int[] capital = new int[6];
        public long Gold { get; private set; }
        public long LastUtcSeconds { get; private set; }
        public int Count => definitions.Length;
        public double StorageHours => 12 + GetCapital(CapitalStat.Economy) * 2;
        public double SpawnSpeedMultiplier => 1 + GetCapital(CapitalStat.Reinforcements) * 0.15;
        public double MovementSpeedMultiplier => 1 + GetCapital(CapitalStat.Marching) * 0.10;

        public KingdomEconomy(SettlementDefinition[] settlements, long nowUtcSeconds, KingdomSave save = null)
        {
            if (settlements == null) throw new ArgumentNullException(nameof(settlements));
            if (nowUtcSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUtcSeconds));
            definitions = (SettlementDefinition[])settlements.Clone();
            states = new SettlementSave[definitions.Length];
            var ids = new HashSet<string>();
            for (int i = 0; i < definitions.Length; i++)
                if (definitions[i] == null || !ids.Add(definitions[i].Id)) throw new ArgumentException("Settlement IDs must be unique.");
            if (save != null)
            {
                // Four-track saves predate army upgrades; new tracks migrate at tier zero.
                if (save.version != 1 || save.gold < 0 || save.lastUtcSeconds < 0 || save.capital == null
                    || (save.capital.Length != 4 && save.capital.Length != 6))
                    throw new ArgumentException("Invalid kingdom save.");
                Gold = save.gold;
                for (int i = 0; i < save.capital.Length; i++)
                {
                    if (save.capital[i] < 0 || save.capital[i] > 3) throw new ArgumentException("Invalid capital tier.");
                    capital[i] = save.capital[i];
                }
            }
            LastUtcSeconds = save == null ? nowUtcSeconds : save.lastUtcSeconds;
            for (int i = 0; i < Count; i++)
            {
                SettlementSave old = null;
                if (save?.settlements != null)
                    foreach (SettlementSave candidate in save.settlements)
                        if (candidate != null && candidate.id == definitions[i].Id) { old = candidate; break; }
                if (old != null && (old.captureStars < 0 || old.captureStars > 5 || old.defenseStars < 0 || old.defenseStars > 5
                    || old.stored < 0 || double.IsNaN(old.stored) || double.IsInfinity(old.stored)))
                    throw new ArgumentException("Invalid settlement save.");
                states[i] = old == null ? new SettlementSave { id = definitions[i].Id }
                    : new SettlementSave { id = old.id, captureStars = old.captureStars, defenseStars = old.defenseStars, stored = old.stored };
                states[i].stored = Math.Min(states[i].stored, GetHourlyIncome(i) * StorageHours);
            }
        }

        public SettlementDefinition GetSettlement(int index) { Check(index); return definitions[index]; }
        public bool IsOwned(int index) { Check(index); return states[index].captureStars >= 3; }
        public bool IsSecured(int index) { Check(index); return IsOwned(index) && states[index].defenseStars >= 3; }
        public double GetStored(int index) { Check(index); return states[index].stored; }
        public int GetControlStars(int index) { Check(index); return Math.Max(states[index].captureStars, states[index].defenseStars); }
        public int GetCapital(CapitalStat stat)
        {
            if ((int)stat < 0 || (int)stat >= capital.Length) throw new ArgumentOutOfRangeException(nameof(stat));
            return capital[(int)stat];
        }
        public double GetHourlyIncome(int index)
        {
            Check(index);
            if (!IsOwned(index)) return 0;
            int stars = GetControlStars(index);
            double control = stars >= 5 ? 1.3 : stars >= 4 ? 1.15 : 1;
            return definitions[index].BaseHourlyIncome * control * (IsSecured(index) ? 1.25 : 1)
                * (1 + GetCapital(CapitalStat.Economy) * 0.1);
        }
        public double TotalHourlyIncome
        {
            get { double total = 0; for (int i = 0; i < Count; i++) total += GetHourlyIncome(i); return total; }
        }
        public long Collectable
        {
            get { long total = 0; foreach (SettlementSave state in states) total = checked(total + (long)Math.Floor(state.stored)); return total; }
        }

        public void Advance(long nowUtcSeconds)
        {
            if (nowUtcSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUtcSeconds));
            if (nowUtcSeconds <= LastUtcSeconds) return; // Backward clock changes never create negative income or duplicate time.
            double hours = Math.Min(((double)nowUtcSeconds - LastUtcSeconds) / 3600, StorageHours);
            for (int i = 0; i < Count; i++)
            {
                double rate = GetHourlyIncome(i);
                states[i].stored = Math.Min(rate * StorageHours, states[i].stored + hours * rate);
            }
            LastUtcSeconds = nowUtcSeconds;
        }

        public void Synchronize(CampaignProgress progress, long nowUtcSeconds)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            foreach (SettlementDefinition d in definitions)
                if (d.CaptureLevel >= progress.Count || d.DefenseLevel >= progress.Count) throw new ArgumentException("Settlement level is outside the campaign.");
            Advance(nowUtcSeconds); // Settle old rates before any newly earned stars or territory.
            for (int i = 0; i < Count; i++)
            {
                states[i].captureStars = Math.Max(states[i].captureStars, progress.GetBest(definitions[i].CaptureLevel));
                if (definitions[i].DefenseLevel >= 0)
                    states[i].defenseStars = Math.Max(states[i].defenseStars, progress.GetBest(definitions[i].DefenseLevel));
            }
        }

        public long Collect(long nowUtcSeconds)
        {
            Advance(nowUtcSeconds);
            long amount = Collectable;
            long nextBalance = checked(Gold + amount);
            foreach (SettlementSave state in states) state.stored -= Math.Floor(state.stored);
            Gold = nextBalance;
            return amount;
        }

        public int UpgradeCost(CapitalStat stat)
        {
            int tier = GetCapital(stat);
            return tier >= 3 ? 0 : 150 << tier;
        }
        public bool TryUpgrade(CapitalStat stat, long nowUtcSeconds)
        {
            Advance(nowUtcSeconds);
            int price = UpgradeCost(stat);
            if (price == 0 || Gold < price) return false;
            Gold -= price;
            capital[(int)stat]++;
            return true;
        }

        public KingdomSave Export()
        {
            var copy = new SettlementSave[Count];
            for (int i = 0; i < Count; i++) copy[i] = new SettlementSave { id = states[i].id,
                captureStars = states[i].captureStars, defenseStars = states[i].defenseStars, stored = states[i].stored };
            return new KingdomSave { gold = Gold, lastUtcSeconds = LastUtcSeconds,
                capital = (int[])capital.Clone(), settlements = copy };
        }
        private void Check(int index) { if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index)); }
    }
}
