using System;
using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class SiegeRoundTests
    {
        [Test]
        public void StartSetsSupplyTargetAndDeadline()
        {
            var round = new SiegeRound();
            round.Start(20, 12, 120);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            Assert.That(round.Supply, Is.EqualTo(20));
            Assert.That(round.Target, Is.EqualTo(12));
            Assert.That(round.Remaining, Is.EqualTo(120));
            Assert.That(round.Delivered, Is.Zero);
        }

        [Test]
        public void TargetDeliveryWinsWithoutWaitingForAllSupply()
        {
            var round = new SiegeRound();
            round.Start(20, 12, 120);
            for (int i = 0; i < 11; i++) Assert.That(round.RecordDelivery(), Is.True);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            round.Advance(25);
            Assert.That(round.RecordDelivery(), Is.True);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Won));
            Assert.That(round.Delivered, Is.EqualTo(12));
            Assert.That(round.Remaining, Is.EqualTo(95));
        }

        [Test]
        public void DeadlineLosesEvenWithSomeDeliveries()
        {
            var round = new SiegeRound();
            round.Start(20, 12, 120);
            round.RecordDelivery();
            round.Advance(119.75);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            round.Advance(0.25);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Lost));
            Assert.That(round.Remaining, Is.Zero);
            Assert.That(round.Delivered, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FinishedRoundRejectsDeliveriesAndFreezesClock(bool win)
        {
            var round = new SiegeRound();
            round.Start(20, 1, 120);
            if (win) round.RecordDelivery(); else round.Advance(120);
            double remaining = round.Remaining;
            int delivered = round.Delivered;
            SiegeRoundState state = round.State;
            round.Advance(1000);
            Assert.That(round.RecordDelivery(), Is.False);
            Assert.That(round.Remaining, Is.EqualTo(remaining));
            Assert.That(round.Delivered, Is.EqualTo(delivered));
            Assert.That(round.State, Is.EqualTo(state));
        }

        [Test]
        public void ExpiredFrameCannotAcceptLastDelivery()
        {
            var round = new SiegeRound();
            round.Start(20, 1, 1);
            round.Advance(1);
            Assert.That(round.RecordDelivery(), Is.False);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Lost));
        }

        [Test]
        public void OvershootingDeadlineClampsClockAtZero()
        {
            var round = new SiegeRound();
            round.Start(20, 12, 1);
            round.Advance(10);
            Assert.That(round.Remaining, Is.Zero);
            Assert.That(round.Elapsed, Is.EqualTo(1));
        }

        [Test]
        public void RestartAndResetClearPreviousResult()
        {
            var round = new SiegeRound();
            round.Start(20, 1, 120);
            round.RecordDelivery();
            round.Start(30, 15, 90);
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            Assert.That(round.Delivered, Is.Zero);
            Assert.That(round.Remaining, Is.EqualTo(90));
            round.Reset();
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Ready));
            Assert.That(round.RecordDelivery(), Is.False);
            round.Advance(10);
            Assert.That(round.Remaining, Is.Zero);
        }

        [Test]
        public void InvalidRestartDoesNotMutateRunningRound()
        {
            var round = new SiegeRound();
            round.Start(20, 12, 120);
            round.RecordDelivery();
            round.Advance(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 21, 90));
            Assert.That(round.State, Is.EqualTo(SiegeRoundState.Running));
            Assert.That(round.Target, Is.EqualTo(12));
            Assert.That(round.Remaining, Is.EqualTo(110));
            Assert.That(round.Delivered, Is.EqualTo(1));
        }

        [Test]
        public void InvalidConfigurationAndTimeAreRejected()
        {
            var round = new SiegeRound();
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(0, 1, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 0, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 21, 120));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 12, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 12, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Start(20, 12, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Advance(double.NaN));
        }
    }
}
