using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class KingdomEconomyTests
    {
        [Test]
        public void FourTrackSavesMigrateWithoutLosingGoldOrCapital()
        {
            var save = Owned().Export();
            save.gold = 5000;
            save.capital = new[] { 2, 1, 3, 2 };
            var economy = new KingdomEconomy(Definitions(), 0, save);
            Assert.That(economy.Gold, Is.EqualTo(5000));
            Assert.That(economy.GetCapital(CapitalStat.Strength), Is.EqualTo(2));
            Assert.That(economy.StorageHours, Is.EqualTo(18));
            Assert.That(economy.SpawnSpeedMultiplier, Is.EqualTo(1));
            Assert.That(economy.MovementSpeedMultiplier, Is.EqualTo(1));
            Assert.That(economy.Export().capital.Length, Is.EqualTo(6));
        }

        [Test]
        public void ArmyTracksAreIndependentCappedAndPersistWithoutChangingIncome()
        {
            var save = Owned().Export(); save.gold = 5000;
            var economy = new KingdomEconomy(Definitions(), 0, save);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(economy.TryUpgrade(CapitalStat.Reinforcements, 0), Is.True);
                Assert.That(economy.TryUpgrade(CapitalStat.Marching, 0), Is.True);
            }
            Assert.That(economy.Gold, Is.EqualTo(2900));
            Assert.That(economy.SpawnSpeedMultiplier, Is.EqualTo(1.45).Within(0.00001));
            Assert.That(economy.MovementSpeedMultiplier, Is.EqualTo(1.3).Within(0.00001));
            Assert.That(economy.GetHourlyIncome(0), Is.EqualTo(20));
            Assert.That(economy.TryUpgrade(CapitalStat.Marching, 0), Is.False);
            var reloaded = new KingdomEconomy(Definitions(), 0, economy.Export());
            Assert.That(reloaded.GetCapital(CapitalStat.Reinforcements), Is.EqualTo(3));
            Assert.That(reloaded.MovementSpeedMultiplier, Is.EqualTo(1.3).Within(0.00001));
        }

        private static SettlementDefinition[] Definitions() => new[]
        { new SettlementDefinition("village", "Village", SettlementKind.Village, 0, 1, 20) };
        private static KingdomEconomy Owned(long time = 0, int capture = 3, int defense = 0)
        {
            var economy = new KingdomEconomy(Definitions(), time);
            economy.Synchronize(new CampaignProgress(new[] { capture, defense }), time);
            return economy;
        }

        [Test]
        public void UnownedSettlementsDoNotEarnAndAcquisitionIsNotRetroactive()
        {
            var economy = new KingdomEconomy(Definitions(), 0);
            economy.Synchronize(new CampaignProgress(new[] { 2, 0 }), 3600);
            Assert.That(economy.IsOwned(0), Is.False);
            Assert.That(economy.GetStored(0), Is.Zero);
            economy.Synchronize(new CampaignProgress(new[] { 3, 0 }), 7200);
            Assert.That(economy.GetStored(0), Is.Zero);
            economy.Advance(9000);
            Assert.That(economy.GetStored(0), Is.EqualTo(10).Within(0.00001));
        }

        [Test]
        public void FractionalTributeSurvivesCollectionAndRepeatedCollectionPaysNothing()
        {
            var economy = Owned();
            Assert.That(economy.Collect(270), Is.EqualTo(1));
            Assert.That(economy.GetStored(0), Is.EqualTo(0.5).Within(0.00001));
            Assert.That(economy.Collect(270), Is.Zero);
            Assert.That(economy.Gold, Is.EqualTo(1));
            Assert.That(economy.Collect(360), Is.EqualTo(1));
            Assert.That(economy.Gold, Is.EqualTo(2));
        }

        [Test]
        public void OfflineIncomeCapsAndBackwardClockDoesNotDuplicateTime()
        {
            var economy = Owned();
            economy.Advance(48 * 3600);
            Assert.That(economy.GetStored(0), Is.EqualTo(240));
            economy.Collect(48 * 3600);
            economy.Advance(47 * 3600);
            Assert.That(economy.GetStored(0), Is.Zero);
            economy.Advance(49 * 3600);
            Assert.That(economy.GetStored(0), Is.EqualTo(20));
        }

        [Test]
        public void DefenseAndBetterStarsChangeOnlyFutureIncome()
        {
            var economy = Owned();
            economy.Synchronize(new CampaignProgress(new[] { 5, 3 }), 3600);
            Assert.That(economy.GetStored(0), Is.EqualTo(20));
            Assert.That(economy.GetHourlyIncome(0), Is.EqualTo(32.5).Within(0.00001));
            economy.Advance(7200);
            Assert.That(economy.GetStored(0), Is.EqualTo(52.5).Within(0.00001));
            economy.Synchronize(new CampaignProgress(new[] { 1, 0 }), 7200);
            Assert.That(economy.IsSecured(0), Is.True);
        }

        [Test]
        public void EconomicUpgradeSpendsGoldOnceAndUsesOldRateBeforePurchase()
        {
            var economy = Owned();
            economy.Collect(8 * 3600);
            Assert.That(economy.Gold, Is.EqualTo(160));
            Assert.That(economy.TryUpgrade(CapitalStat.Economy, 9 * 3600), Is.True);
            Assert.That(economy.Gold, Is.EqualTo(10));
            Assert.That(economy.GetStored(0), Is.EqualTo(20));
            Assert.That(economy.GetHourlyIncome(0), Is.EqualTo(22).Within(0.00001));
            Assert.That(economy.StorageHours, Is.EqualTo(14));
            Assert.That(economy.TryUpgrade(CapitalStat.Economy, 9 * 3600), Is.False);
            Assert.That(economy.GetCapital(CapitalStat.Economy), Is.EqualTo(1));
            economy.Advance(10 * 3600);
            Assert.That(economy.GetStored(0), Is.EqualTo(42).Within(0.00001));
        }

        [Test]
        public void CapitalHasThreeTiersAndVisualStatsDoNotChangeIncome()
        {
            var save = Owned().Export(); save.gold = 2000;
            var economy = new KingdomEconomy(Definitions(), 0, save);
            Assert.That(economy.TryUpgrade(CapitalStat.Strength, 0), Is.True);
            Assert.That(economy.TryUpgrade(CapitalStat.Strength, 0), Is.True);
            Assert.That(economy.TryUpgrade(CapitalStat.Strength, 0), Is.True);
            Assert.That(economy.Gold, Is.EqualTo(950));
            Assert.That(economy.TryUpgrade(CapitalStat.Strength, 0), Is.False);
            Assert.That(economy.GetHourlyIncome(0), Is.EqualTo(20));
        }

        [Test]
        public void SaveReloadPreservesRatesStockGoldAndAccruesOfflineOnce()
        {
            var first = Owned(100, 5, 3);
            first.Advance(3700); first.Collect(3700);
            var saved = first.Export();
            var reloaded = new KingdomEconomy(Definitions(), 7300, saved);
            reloaded.Synchronize(new CampaignProgress(new[] { 5, 3 }), 7300);
            Assert.That(reloaded.Gold, Is.EqualTo(32));
            Assert.That(reloaded.GetStored(0), Is.EqualTo(33).Within(0.00001));
            reloaded.Advance(7300);
            Assert.That(reloaded.GetStored(0), Is.EqualTo(33).Within(0.00001));
            saved.capital[2] = 3;
            Assert.That(reloaded.GetCapital(CapitalStat.Economy), Is.Zero, "Save objects must not alias live state.");
        }
    }
}
