using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class CampaignExpansionTests
    {
        private static LevelDefinition Level(int[] from = null, int[] traps = null) => new LevelDefinition(
            "Village", LevelMode.Capture, 20, 12, 120, 24, trapTiles: traps, unlockFrom: from);

        [Test]
        public void AConqueredRegionOpensTwoBranchesWithoutSequentialDefense()
        {
            var levels = new[] { Level(), Level(new[] { 0 }), Level(new[] { 0 }) };
            CampaignMapRules.Validate(levels);
            var progress = new CampaignProgress(new[] { 3, 0, 0 });
            Assert.That(CampaignMapRules.IsAvailable(levels, progress, 1), Is.True);
            Assert.That(CampaignMapRules.IsAvailable(levels, progress, 2), Is.True);
            Assert.That(CampaignMapRules.IsAvailable(levels, new CampaignProgress(new[] { 2, 0, 0 }), 2), Is.False);
        }

        [Test]
        public void UnreachableCyclesAndInvalidMapLinksAreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => CampaignMapRules.Validate(new[] { Level(new[] { 1 }), Level(new[] { 0 }) }));
            Assert.Throws<System.ArgumentException>(() => CampaignMapRules.Validate(new[] { Level(new[] { 2 }) }));
        }

        [Test]
        public void MinimumDeploymentIncludesFiniteTrapLosses()
        {
            var level = Level(traps: new[] { 6, 10 });
            Assert.That(level.MinimumSupply, Is.EqualTo(14));
            Assert.Throws<System.ArgumentException>(() => level.WithSupply(13));
            Assert.That(level.WithSupply(14).Supply, Is.EqualTo(14));
            var ledger = new TrapLedger(level);
            Assert.That(ledger.Trigger(6), Is.True);
            Assert.That(ledger.Trigger(6), Is.False);
            Assert.That(ledger.Trigger(10), Is.True);
            Assert.That(new TrapLedger(level).GetCharges(6), Is.EqualTo(1));
        }

        [Test]
        public void TrapDeathReleasesOccupancyAndReservationExactlyOnce()
        {
            var board = new SlidingBoard(); board.ResetSolved();
            var warrior = new WarriorSimulation();
            Assert.That(warrior.TryEnter(board, true), Is.True);
            Assert.That(warrior.ReserveDestination(board, true, 12), Is.True);
            Assert.That(warrior.Kill(), Is.True);
            Assert.That(warrior.Dead, Is.True);
            Assert.That(warrior.Occupancy.GetCount(16), Is.Zero);
            Assert.That(warrior.Occupancy.GetReservedCount(12), Is.Zero);
            Assert.That(warrior.DeliveredCount, Is.Zero);
            Assert.That(warrior.Kill(), Is.False);
            Assert.That(warrior.QueueNext(), Is.False);
        }

        [Test]
        public void TrainingAndPermanentCasualtiesPersistAndRejectOverspending()
        {
            var army = new KingdomEconomy(new SettlementDefinition[0], 0);
            Assert.That(army.TrainedWarriors, Is.EqualTo(20));
            Assert.That(army.Train(1), Is.False);
            var save = army.Export(); save.gold = 25;
            army = new KingdomEconomy(new SettlementDefinition[0], 0, save);
            Assert.That(army.Train(5), Is.True);
            Assert.That(army.Gold, Is.Zero);
            Assert.That(army.LoseWarrior(), Is.True);
            army.RecordBattle(true);
            var reloaded = new KingdomEconomy(new SettlementDefinition[0], 0, army.Export());
            Assert.That(reloaded.TrainedWarriors, Is.EqualTo(24));
            Assert.That(reloaded.Casualties, Is.EqualTo(1));
            Assert.That(reloaded.Victories, Is.EqualTo(1));
            Assert.That(reloaded.Train(0), Is.False);
        }

        [Test]
        public void ImpossibleCaptureAfterLossEndsWithoutWaitingForDeadline()
        {
            var round = new SiegeRound(); round.Start(12, 12, 120);
            Assert.That(round.RecordCasualty(), Is.True);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Lost));
            Assert.That(round.Delivered, Is.Zero);
        }

        [Test]
        public void RulerRanksUseTerritoryRatherThanRepeatVictories()
        {
            Assert.That(CampaignMapRules.RulerTitle(0), Is.EqualTo("Малий феодал"));
            Assert.That(CampaignMapRules.RulerTitle(50), Is.EqualTo("Король"));
            Assert.That(CampaignMapRules.RulerTitle(150), Is.EqualTo("Імператор"));
            Assert.That(CampaignMapRules.RulerTitle(999), Is.EqualTo("Великий імператор"));
        }
    }
}
