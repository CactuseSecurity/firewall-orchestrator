using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class StateMatrixTest
    {
        private sealed class StateMatrixApiConnection(List<WorkflowConfiguration> configurations) : SimulatedApiConnection
        {
            public string LastQuery { get; private set; } = "";
            public object? LastVariables { get; private set; }
            public int QueryCount { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                QueryCount++;
                if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>))
                {
                    return Task.FromResult((QueryResponseType)(object)configurations);
                }
                if (typeof(QueryResponseType) == typeof(object))
                {
                    return Task.FromResult((QueryResponseType)(object)new object());
                }
                throw new NotImplementedException();
            }
        }

        private sealed class TestGlobalStateMatrix : GlobalStateMatrix
        {
            public List<WfTaskType> InitializedTaskTypes { get; } = [];

            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
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
        public async Task GlobalStateMatrixInit_MergesTransitionGroupsAndKeepsSparseDerivedStates()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source);
            StateMatrixPhase requestPhase = configurations[0].Phases.Single(item => item.Phase == WorkflowPhases.request.ToString()).PhaseMatrix;
            requestPhase.DerivedStates.Add(new() { FromStateId = 2, DerivedStateId = 9 });
            requestPhase.TransitionGroups[0].TransitionGroup.Transitions.Add(new() { FromStateId = 1, ToStateId = 2 });
            requestPhase.TransitionGroups.Add(new()
            {
                SortOrder = 1,
                TransitionGroupId = 999,
                TransitionGroup = new()
                {
                    Id = 999,
                    Transitions = [new() { FromStateId = 1, ToStateId = 3 }]
                }
            });
            StateMatrixApiConnection apiConnection = new(configurations);
            GlobalStateMatrix matrix = new();

            await matrix.Init(apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(RequestQueries.getActiveStateMatrixConfiguration));
                Assert.That(matrix.GlobalMatrix[WorkflowPhases.request].Matrix[1], Is.EqualTo(new List<int> { 2, 3 }));
                Assert.That(matrix.GlobalMatrix[WorkflowPhases.request].DerivedStates, Is.EqualTo(new Dictionary<int, int> { [2] = 9 }));
            });
        }

        [Test]
        public async Task GlobalStateMatrixInitNamedConfiguration_UsesNamedConfigurationQuery()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source);
            configurations[0].Name = "candidate";
            configurations[0].IsActive = false;
            StateMatrixApiConnection apiConnection = new(configurations);
            GlobalStateMatrix matrix = new();

            await matrix.Init(apiConnection, WfTaskType.master, "candidate");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(RequestQueries.getStateMatrixConfigurationByName));
                Assert.That(matrix.ConfigurationName, Is.EqualTo("candidate"));
                Assert.That(apiConnection.LastVariables?.GetType().GetProperty("configurationName")?.GetValue(apiConnection.LastVariables), Is.EqualTo("candidate"));
            });
        }

        [Test]
        public async Task GlobalStateMatrixSave_WritesOnlyNonIdentityDerivedStates()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source);
            StateMatrixApiConnection apiConnection = new(configurations);
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);
            matrix.GlobalMatrix[WorkflowPhases.request].DerivedStates = new() { [1] = 1, [2] = 9 };

            await matrix.Save(apiConnection);

            object variables = apiConnection.LastVariables ?? throw new InvalidOperationException("Save variables were not captured.");
            IEnumerable<object> derivedStates = (IEnumerable<object>)(variables.GetType().GetProperty("derivedStates")?.GetValue(variables)
                ?? throw new InvalidOperationException("Derived states were not provided."));
            List<object> savedDerivedStates = derivedStates.ToList();
            await matrix.Save(apiConnection);
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(RequestQueries.replaceStateMatrixConfiguration));
                Assert.That(apiConnection.QueryCount, Is.EqualTo(2));
                Assert.That(savedDerivedStates, Has.Count.EqualTo(1));
                Assert.That(savedDerivedStates[0].GetType().GetProperty("from_state_id")?.GetValue(savedDerivedStates[0]), Is.EqualTo(2));
                Assert.That(savedDerivedStates[0].GetType().GetProperty("derived_state_id")?.GetValue(savedDerivedStates[0]), Is.EqualTo(9));
            });
        }

        [Test]
        public async Task GlobalStateMatrixSave_SkipsMutationWhenNothingChanged()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            StateMatrixApiConnection apiConnection = new(StateMatrixConfigurationTestHelper.FromGlobalMatrix(source));
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);

            await matrix.Save(apiConnection);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.QueryCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastQuery, Is.EqualTo(RequestQueries.getActiveStateMatrixConfiguration));
            });
        }

        [Test]
        public async Task HasUnsavedChanges_DetectsDeferredValuesAndResetsAfterSave()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            StateMatrixApiConnection apiConnection = new(StateMatrixConfigurationTestHelper.FromGlobalMatrix(source));
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);

            Assert.That(matrix.HasUnsavedChanges(), Is.False);
            matrix.GlobalMatrix[WorkflowPhases.request].LowestStartedState = 12;
            Assert.That(matrix.HasUnsavedChanges(), Is.True);

            await matrix.Save(apiConnection);

            Assert.That(matrix.HasUnsavedChanges(), Is.False);
        }

        [Test]
        public async Task HasUnsavedChanges_IgnoresIdentityDerivedStatesAndTransitions()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            GlobalStateMatrix matrix = new();
            await matrix.Init(new StateMatrixApiConnection(StateMatrixConfigurationTestHelper.FromGlobalMatrix(source)));
            StateMatrix requestMatrix = matrix.GlobalMatrix[WorkflowPhases.request];

            requestMatrix.DerivedStates[4] = 4;
            requestMatrix.Matrix[1] = [2];

            Assert.That(matrix.HasUnsavedChanges(), Is.False);
            requestMatrix.DerivedStates[4] = 7;
            Assert.That(matrix.HasUnsavedChanges(), Is.True);
        }

        [Test]
        public async Task GlobalStateMatrixSave_WritesOnlyAddedAndRemovedTransitions()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            source.GlobalMatrix[WorkflowPhases.request].Matrix[1] = [2, 3];
            StateMatrixApiConnection apiConnection = new(StateMatrixConfigurationTestHelper.FromGlobalMatrix(source));
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);
            matrix.GlobalMatrix[WorkflowPhases.request].Matrix[1] = [3, 4];

            await matrix.Save(apiConnection);

            object variables = apiConnection.LastVariables ?? throw new InvalidOperationException("Save variables were not captured.");
            List<object> removedTransitions = ReadObjectList(variables, "removedTransitions");
            List<object> transitions = ReadObjectList(variables, "transitions");
            Assert.Multiple(() =>
            {
                Assert.That(removedTransitions, Has.Count.EqualTo(1));
                Assert.That(transitions, Has.Count.EqualTo(1));
                Assert.That(transitions[0].GetType().GetProperty("to_state_id")?.GetValue(transitions[0]), Is.EqualTo(4));
            });
        }

        [Test]
        public async Task GlobalStateMatrixSave_RejectsFlatteningMultipleTransitionGroups()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source);
            StateMatrixPhase requestPhase = configurations[0].Phases.Single(item => item.Phase == WorkflowPhases.request.ToString()).PhaseMatrix;
            requestPhase.TransitionGroups.Add(new()
            {
                TransitionGroupId = 999,
                TransitionGroup = new() { Id = 999 }
            });
            StateMatrixApiConnection apiConnection = new(configurations);
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);
            matrix.GlobalMatrix[WorkflowPhases.request].Matrix[1] = [2];

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => matrix.Save(apiConnection));

            Assert.That(exception?.Message, Does.Contain("uses 2 transition groups"));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(RequestQueries.getActiveStateMatrixConfiguration));
        }

        [Test]
        public async Task GlobalStateMatrixSave_AllowsPhaseChangesWithMultipleTransitionGroups()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source);
            StateMatrixPhase requestPhase = configurations[0].Phases.Single(item => item.Phase == WorkflowPhases.request.ToString()).PhaseMatrix;
            requestPhase.TransitionGroups.Add(new()
            {
                TransitionGroupId = 999,
                TransitionGroup = new() { Id = 999 }
            });
            StateMatrixApiConnection apiConnection = new(configurations);
            GlobalStateMatrix matrix = new();
            await matrix.Init(apiConnection);
            matrix.GlobalMatrix[WorkflowPhases.request].Active = true;

            await matrix.Save(apiConnection);

            object variables = apiConnection.LastVariables ?? throw new InvalidOperationException("Save variables were not captured.");
            Assert.That(ReadObjectList(variables, "phaseMatrices"), Has.Count.EqualTo(1));
            Assert.That(ReadObjectList(variables, "transitions"), Is.Empty);
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

        private static List<object> ReadObjectList(object source, string propertyName)
        {
            IEnumerable<object> values = (IEnumerable<object>)(source.GetType().GetProperty(propertyName)?.GetValue(source)
                ?? throw new InvalidOperationException($"Property '{propertyName}' was not provided."));
            return values.ToList();
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
        public async Task StateMatrixInit_CopiesVisibilityGroupsFromLoadedSnapshot()
        {
            GlobalStateMatrix source = new()
            {
                GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix())
            };
            source.GlobalMatrix[WorkflowPhases.approval].Matrix[49] = [50];
            source.GlobalMatrix[WorkflowPhases.approval].LowestInputState = 49;
            source.GlobalMatrix[WorkflowPhases.approval].LowestStartedState = 50;
            source.GlobalMatrix[WorkflowPhases.approval].LowestEndState = 60;

            List<WorkflowConfiguration> configurations = StateMatrixConfigurationTestHelper.FromGlobalMatrix(source, WfTaskType.access);
            StateMatrixTransitionGroup approvalGroup = configurations[0].Phases
                .Single(phase => phase.Phase == WorkflowPhases.approval.ToString())
                .PhaseMatrix
                .TransitionGroups[0]
                .TransitionGroup;
            approvalGroup.VisibilityGroupId = 3;
            approvalGroup.Exclusive = true;

            StateMatrix matrix = new();
            await matrix.Init(WorkflowPhases.approval, new StateMatrixApiConnection(configurations), [new() { Id = 49 }, new() { Id = 50 }], WfTaskType.access);

            Assert.Multiple(() =>
            {
                Assert.That(matrix.ExclusiveVisibilityGroupIds, Does.Contain(3));
                Assert.That(matrix.GetVisibilityGroupIds(49), Does.Contain(3));
            });
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
