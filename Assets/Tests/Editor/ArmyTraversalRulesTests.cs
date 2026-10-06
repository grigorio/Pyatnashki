using NUnit.Framework;
using Pyatnashki.Domain;

namespace Pyatnashki.Tests
{
    public class ArmyTraversalRulesTests
    {
        [Test]
        public void SpecialistRollUsesDisjointProbabilityIntervals()
        {
            Assert.That(ArmyTraversalRules.Roll(0, 0.08, 0.06), Is.EqualTo(WarriorRole.Commander));
            Assert.That(ArmyTraversalRules.Roll(0.08, 0.08, 0.06), Is.EqualTo(WarriorRole.Cartographer));
            Assert.That(ArmyTraversalRules.Roll(0.08 + 0.06, 0.08, 0.06), Is.EqualTo(WarriorRole.Infantry));
            Assert.That(ArmyTraversalRules.Roll(0.999, 0, 0), Is.EqualTo(WarriorRole.Infantry));
            Assert.Throws<System.ArgumentException>(() => ArmyTraversalRules.ValidateChances(0.7, 0.4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ArmyTraversalRules.Roll(1, 0.08, 0.06));
        }

        [Test]
        public void SpecialistWaitsBeforeActiveTrapThenContinuesWhenDisarmed()
        {
            var board = new SlidingBoard();
            bool armed = true;
            var specialist = new WarriorSimulation(avoidTile: tile => armed && tile == 12);
            Assert.That(specialist.TryEnter(board, true), Is.True);
            Assert.That(specialist.GetNextTile(board, true), Is.Zero);
            Assert.That(specialist.InspectNextRoadTile(board, true), Is.EqualTo(12));
            Assert.That(specialist.ReserveDestination(board, true, 12), Is.False);
            Assert.That(specialist.TryMoveTo(board, true, 12), Is.False);
            Assert.That(specialist.Occupancy.GetReservedCount(12), Is.Zero);
            armed = false;
            Assert.That(specialist.GetNextTile(board, true), Is.EqualTo(12));
            Assert.That(specialist.TryMoveTo(board, true, 12), Is.True);
        }

        [Test]
        public void SpecialistRejectsTrappedEntranceWhileInfantryCanEnter()
        {
            var board = new SlidingBoard();
            var specialist = new WarriorSimulation(avoidTile: tile => tile == 16);
            Assert.That(specialist.CanEnter(board, true), Is.False);
            Assert.That(new WarriorSimulation().CanEnter(board, true), Is.True);
        }

        [Test]
        public void SolvedFieldUsesFastestStepWithoutSlowingAlreadyFasterConfiguration()
        {
            Assert.That(ArmyTraversalRules.StepSeconds(0.6f, 1.3f, false), Is.EqualTo(0.6f / 1.3f).Within(0.00001));
            Assert.That(ArmyTraversalRules.StepSeconds(0.6f, 1, true), Is.EqualTo(0.08f));
            Assert.That(ArmyTraversalRules.StepSeconds(0.05f, 1, true), Is.EqualTo(0.05f));
        }
    }
}
