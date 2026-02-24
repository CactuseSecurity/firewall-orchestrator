using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class StateMatrixTest
    {
        [Test]
        public void GetNextActivePhase_ReturnsNextActivePhase()
        {
            StateMatrix matrix = new()
            {
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = false,
                    [WorkflowPhases.planning] = true,
                    [WorkflowPhases.implementation] = true
                }
            };
            WorkflowPhases phase = WorkflowPhases.request;

            bool moved = matrix.getNextActivePhase(ref phase);

            Assert.That(moved, Is.True);
            Assert.That(phase, Is.EqualTo(WorkflowPhases.planning));
        }

        [Test]
        public void GetNextActivePhase_ReturnsFalse_WhenNoneAvailable()
        {
            StateMatrix matrix = new()
            {
                PhaseActive =
                {
                    [WorkflowPhases.request] = true,
                    [WorkflowPhases.approval] = false
                }
            };
            WorkflowPhases phase = WorkflowPhases.approval;

            bool moved = matrix.getNextActivePhase(ref phase);

            Assert.That(moved, Is.False);
            Assert.That(phase, Is.EqualTo(WorkflowPhases.approval));
        }

        [Test]
        public void GetAllowedTransitions_ReturnsMatrixOrEmpty()
        {
            StateMatrix matrix = new();
            matrix.Matrix[1] = [2, 3];

            Assert.That(matrix.getAllowedTransitions(1), Is.EqualTo(new List<int> { 2, 3 }));
            Assert.That(matrix.getAllowedTransitions(99), Is.Empty);
        }

        [Test]
        public void GetDerivedStateFromSubStates_ReturnsZeroWhenEmpty()
        {
            StateMatrix matrix = new();

            int derived = matrix.getDerivedStateFromSubStates([]);

            Assert.That(derived, Is.EqualTo(0));
        }

        [Test]
        public void GetDerivedStateFromSubStates_UsesBackAssignedState()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30
            };

            int derived = matrix.getDerivedStateFromSubStates([5, 15, 25]);

            Assert.That(derived, Is.EqualTo(5));
        }

        [Test]
        public void GetDerivedStateFromSubStates_UsesInWorkState()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30
            };

            int derived = matrix.getDerivedStateFromSubStates([12, 22, 24]);

            Assert.That(derived, Is.EqualTo(22));
        }

        [Test]
        public void GetDerivedStateFromSubStates_UsesMinFinishedState_WhenAllFinished()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30
            };

            int derived = matrix.getDerivedStateFromSubStates([35, 40]);

            Assert.That(derived, Is.EqualTo(35));
        }

        [Test]
        public void GetDerivedStateFromSubStates_UsesInitState_WhenAllOpen()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30
            };

            int derived = matrix.getDerivedStateFromSubStates([10, 11]);

            Assert.That(derived, Is.EqualTo(11));
        }

        [Test]
        public void GetDerivedStateFromSubStates_UsesLowestStarted_WhenMixedOpenAndFinished()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30
            };

            int derived = matrix.getDerivedStateFromSubStates([10, 31]);

            Assert.That(derived, Is.EqualTo(20));
        }

        [Test]
        public void GetDerivedStateFromSubStates_AppliesDerivedStatesMapping()
        {
            StateMatrix matrix = new()
            {
                LowestInputState = 10,
                LowestStartedState = 20,
                LowestEndState = 30,
                DerivedStates =
                {
                    [11] = 99
                }
            };

            int derived = matrix.getDerivedStateFromSubStates([10, 11]);

            Assert.That(derived, Is.EqualTo(99));
        }
    }
}
