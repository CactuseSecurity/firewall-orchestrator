using FWO.Api.Client;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class StateMatrixTest
    {
        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public List<WfTaskType> InitializedTaskTypes { get; } = [];

            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master, bool reset = false)
            {
                InitializedTaskTypes.Add(taskType);
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = CreateMatrix(0, 1, 10, true),
                    [WorkflowPhases.approval] = CreateMatrix(10, 11, 20, false),
                    [WorkflowPhases.implementation] = CreateMatrix(20, 21, 30, false)
                };
                return Task.CompletedTask;
            }
        }

        private static StateMatrix CreateMatrix(int lowestInputState, int lowestStartedState, int lowestEndState, bool active)
        {
            return new()
            {
                LowestInputState = lowestInputState,
                LowestStartedState = lowestStartedState,
                LowestEndState = lowestEndState,
                Active = active
            };
        }

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
        public void GetAllowedTransitions_FiltersAutomaticOnlyStates_ByDefault()
        {
            StateMatrix matrix = new()
            {
                AutomaticOnlyStates = [3]
            };
            matrix.Matrix[1] = [2, 3];

            Assert.That(matrix.getAllowedTransitions(1), Is.EqualTo(new List<int> { 2 }));
        }

        [Test]
        public void GetAllowedTransitions_CanIncludeAutomaticOnlyStates()
        {
            StateMatrix matrix = new()
            {
                AutomaticOnlyStates = [3]
            };
            matrix.Matrix[1] = [2, 3];

            Assert.That(matrix.getAllowedTransitions(1, allowAutomaticOnlyStates: true), Is.EqualTo(new List<int> { 2, 3 }));
        }

        [Test]
        public async Task StateMatrixDictInitWithPreloadedStates_ReusesStateListForEveryTaskType()
        {
            Func<GlobalStateMatrix> originalFactory = GlobalStateMatrix.Factory;
            List<TestGlobalStateMatrix> matrices = [];
            GlobalStateMatrix.Factory = () =>
            {
                TestGlobalStateMatrix matrix = new();
                matrices.Add(matrix);
                return matrix;
            };

            try
            {
                StateMatrixDict dict = new();
                List<WfState> states = [new() { Id = 3, AutomaticOnly = true }];

                await dict.Init(WorkflowPhases.request, new SimulatedApiConnection(), states);

                Assert.Multiple(() =>
                {
                    Assert.That(dict.Matrices, Has.Count.EqualTo(Enum.GetValues(typeof(WfTaskType)).Length));
                    Assert.That(dict.Matrices.Values.All(matrix => matrix.AutomaticOnlyStates.SetEquals([3])), Is.True);
                    Assert.That(matrices.SelectMany(matrix => matrix.InitializedTaskTypes), Is.EquivalentTo(Enum.GetValues(typeof(WfTaskType))));
                });
            }
            finally
            {
                GlobalStateMatrix.Factory = originalFactory;
            }
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
