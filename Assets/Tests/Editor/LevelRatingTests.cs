using System;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class LevelRatingTests
    {
        [TestCase(0, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(11, 2)]
        public void CaptureLossRatesPartialCompletion(int delivered, int expected)
        {
            var level = new LevelDefinition("Capture", LevelMode.Capture, 20, 12, 120, 1);
            var round = new SiegeRound();
            round.Start(level);
            for (int i = 0; i < delivered; i++) round.RecordDelivery();
            round.Advance(120);
            Assert.That(LevelRating.Calculate(level, round), Is.EqualTo(expected));
        }

        [TestCase(60, 5)]
        [TestCase(60.001, 4)]
        [TestCase(90, 4)]
        [TestCase(90.001, 3)]
        [TestCase(119, 3)]
        public void CaptureVictoryUsesInclusiveTimeThresholds(double elapsed, int expected)
        {
            var level = new LevelDefinition("Capture", LevelMode.Capture, 20, 1, 120, 1);
            var round = new SiegeRound();
            round.Start(level);
            round.Advance(elapsed);
            round.RecordDelivery();
            Assert.That(LevelRating.Calculate(level, round), Is.EqualTo(expected));
        }

        [TestCase(0, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(9, 2)]
        [TestCase(10, 2)]
        [TestCase(11, 3)]
        [TestCase(12, 3)]
        [TestCase(13, 4)]
        [TestCase(15, 4)]
        [TestCase(16, 5)]
        [TestCase(20, 5)]
        public void DefenseRatesAgainstRequiredVictoryAndActualMargin(int delivered, int expected)
        {
            var level = new LevelDefinition("Defense", LevelMode.Defense, 20, 1, 120, 1, 10, 8);
            var round = new SiegeRound();
            round.Start(level);
            for (int i = 0; i < delivered; i++) round.RecordDelivery();
            Assert.That(LevelRating.Calculate(level, round), Is.Zero, "An unfinished round has no rating.");
            round.Advance(120);
            Assert.That(LevelRating.Calculate(level, round), Is.EqualTo(expected));
        }

        [Test]
        public void CustomThresholdsAreUsed()
        {
            var level = new LevelDefinition("Capture", LevelMode.Capture, 20, 1, 120, 1,
                fourStarTimeFraction: 0.1, fiveStarTimeFraction: 0.2);
            var round = new SiegeRound();
            round.Start(level);
            round.Advance(90);
            round.RecordDelivery();
            Assert.That(LevelRating.Calculate(level, round), Is.EqualTo(5));
            var defense = new LevelDefinition("Defense", LevelMode.Defense, 20, 1, 120, 1, 10, 8,
                fourStarAdvantage: 2, fiveStarAdvantage: 4);
            round.Start(defense);
            for (int i = 0; i < 14; i++) round.RecordDelivery();
            round.Advance(120);
            Assert.That(LevelRating.Calculate(defense, round), Is.EqualTo(5));
        }

        [Test]
        public void BestResultSurvivesWeakerReplayAndUnlockRequiresThree()
        {
            var progress = new CampaignProgress(new int[3]);
            Assert.That(progress.IsUnlocked(0), Is.True);
            Assert.That(progress.IsUnlocked(1), Is.False);
            progress.Record(0, 2);
            Assert.That(progress.IsUnlocked(1), Is.False);
            progress.Record(0, 3);
            Assert.That(progress.IsUnlocked(1), Is.True);
            progress.Record(0, 5);
            Assert.That(progress.Record(0, 1), Is.False);
            Assert.That(progress.Record(0, 0), Is.False);
            Assert.That(progress.GetBest(0), Is.EqualTo(5));
            Assert.That(progress.IsUnlocked(2), Is.False);
            var reloaded = new CampaignProgress(new[] { progress.GetBest(0), progress.GetBest(1), progress.GetBest(2) });
            Assert.That(reloaded.GetBest(0), Is.EqualTo(5));
            Assert.That(reloaded.IsUnlocked(1), Is.True);
        }

        [Test]
        public void MigrationPreservesUnlockedLevelsWithoutInventingHighRatings()
        {
            var progress = new CampaignProgress(new[] { 5, 0, 0, 0 });
            progress.MigrateLegacyUnlocks(3);
            Assert.That(progress.GetBest(0), Is.EqualTo(5));
            Assert.That(progress.GetBest(1), Is.EqualTo(3));
            Assert.That(progress.GetBest(2), Is.Zero);
            Assert.That(progress.IsUnlocked(2), Is.True);
            Assert.That(progress.IsUnlocked(3), Is.False);
        }

        [Test]
        public void InvalidRatingsAndThresholdsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignProgress(new[] { 6 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignProgress(new int[1]).Record(0, -1));
            Assert.Throws<ArgumentException>(() => new LevelDefinition("Bad", LevelMode.Capture, 20, 1, 120, 1,
                fourStarTimeFraction: 0.6, fiveStarTimeFraction: 0.5));
            Assert.Throws<ArgumentException>(() => new LevelDefinition("Bad", LevelMode.Defense, 20, 1, 120, 1, 10, 8,
                fourStarAdvantage: 6, fiveStarAdvantage: 6));
        }
    }
}
