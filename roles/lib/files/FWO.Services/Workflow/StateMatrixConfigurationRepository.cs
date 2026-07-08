using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    public static class StateMatrixConfigurationRepository
    {
        /// <summary>
        /// Loads one task type from either the active workflow configuration or a named configuration.
        /// </summary>
        public static async Task<StateMatrixConfigurationSnapshot> Load(ApiConnection apiConnection, WfTaskType taskType, string? configurationName = null)
        {
            object variables = configurationName == null
                ? new { taskType = taskType.ToString() }
                : new { taskType = taskType.ToString(), configurationName };
            string query = configurationName == null
                ? RequestQueries.getActiveStateMatrixConfiguration
                : RequestQueries.getStateMatrixConfigurationByName;

            List<WorkflowConfiguration> configurations = await apiConnection.SendQueryAsync<List<WorkflowConfiguration>>(query, variables);
            if (configurations.Count != 1)
            {
                string selector = configurationName == null ? "active" : $"named '{configurationName}'";
                throw new InvalidOperationException($"Expected exactly one {selector} workflow configuration, found {configurations.Count}.");
            }
            return BuildSnapshot(configurations[0]);
        }

        /// <summary>
        /// Persists changed matrix values while preserving phase and transition-group identities.
        /// </summary>
        public static async Task Update(ApiConnection apiConnection, GlobalStateMatrix stateMatrix)
        {
            ValidateEditableBindings(stateMatrix);
            StateMatrixConfigurationChanges changes = BuildChanges(stateMatrix);
            if (!changes.HasChanges)
            {
                return;
            }

            await apiConnection.SendQueryAsync<object>(RequestQueries.replaceStateMatrixConfiguration, new
            {
                removedDerivedStates = changes.RemovedDerivedStates,
                removedTransitions = changes.RemovedTransitions,
                phaseMatrices = changes.PhaseMatrices,
                derivedStates = changes.DerivedStates,
                transitions = changes.Transitions
            });
            stateMatrix.AcceptChanges(changes.TransitionSortOrders);
        }

        private static StateMatrixConfigurationSnapshot BuildSnapshot(WorkflowConfiguration configuration)
        {
            Dictionary<WorkflowPhases, StateMatrix> matrices = [];
            Dictionary<WorkflowPhases, StateMatrixPhaseBinding> bindings = [];

            foreach (WorkflowConfigurationPhase configurationPhase in configuration.Phases)
            {
                (WorkflowPhases phase, StateMatrix matrix, StateMatrixPhaseBinding binding) = BuildPhaseSnapshot(configurationPhase);
                if (matrices.ContainsKey(phase))
                {
                    throw new InvalidOperationException($"Workflow phase '{phase}' is configured more than once for task type '{configurationPhase.TaskType}'.");
                }

                matrices.Add(phase, matrix);
                bindings.Add(phase, binding);
            }

            List<WorkflowPhases> missingPhases = Enum.GetValues<WorkflowPhases>().Where(phase => !matrices.ContainsKey(phase)).ToList();
            foreach (WorkflowPhases missingPhase in missingPhases)
            {
                (StateMatrix matrix, StateMatrixPhaseBinding binding) = CreateMissingPhaseSnapshot(configuration.Name, missingPhase);
                matrices.Add(missingPhase, matrix);
                bindings.Add(missingPhase, binding);
            }

            return new(configuration.Id, configuration.Name, matrices, bindings);
        }

        private static (StateMatrix Matrix, StateMatrixPhaseBinding Binding) CreateMissingPhaseSnapshot(string configurationName, WorkflowPhases phase)
        {
            StateMatrix matrix = new()
            {
                Active = false,
                LowestInputState = 0,
                LowestStartedState = 0,
                LowestEndState = 0,
                DerivedStates = new Dictionary<int, int> { [0] = 0 }
            };

            return (matrix, new(
                0,
                $"{configurationName}_{phase}_missing",
                [],
                []));
        }

        private static (WorkflowPhases Phase, StateMatrix Matrix, StateMatrixPhaseBinding Binding) BuildPhaseSnapshot(WorkflowConfigurationPhase configurationPhase)
        {
            if (!Enum.TryParse(configurationPhase.Phase, true, out WorkflowPhases phase))
            {
                throw new InvalidOperationException($"Unknown workflow phase '{configurationPhase.Phase}'.");
            }

            StateMatrixPhase phaseData = configurationPhase.PhaseMatrix;
            StateMatrix matrix = CreateStateMatrix(phaseData);
            Dictionary<int, HashSet<int>> stateVisibilityGroupIds = [];
            HashSet<int> exclusiveVisibilityGroupIds = [];

            foreach (StateMatrixPhaseTransitionGroup groupLink in phaseData.TransitionGroups.OrderBy(group => group.SortOrder))
            {
                ApplyTransitionGroup(groupLink, matrix, stateVisibilityGroupIds, exclusiveVisibilityGroupIds);
            }

            matrix.StateVisibilityGroupIds = stateVisibilityGroupIds.ToDictionary(entry => entry.Key, entry => entry.Value.ToList());
            matrix.ExclusiveVisibilityGroupIds = exclusiveVisibilityGroupIds;

            return (phase, matrix, new(
                phaseData.Id,
                phaseData.Name,
                phaseData.TransitionGroups.OrderBy(group => group.SortOrder).Select(group => group.TransitionGroupId).ToList(),
                BuildTransitionSortOrders(phaseData)));
        }

        private static StateMatrix CreateStateMatrix(StateMatrixPhase phaseData)
        {
            return new StateMatrix
            {
                Active = phaseData.Active,
                LowestInputState = phaseData.LowestInputState,
                LowestStartedState = phaseData.LowestStartState,
                LowestEndState = phaseData.LowestEndState,
                DerivedStates = phaseData.DerivedStates.ToDictionary(item => item.FromStateId, item => item.DerivedStateId)
            };
        }

        private static void ApplyTransitionGroup(
            StateMatrixPhaseTransitionGroup groupLink,
            StateMatrix matrix,
            Dictionary<int, HashSet<int>> stateVisibilityGroupIds,
            HashSet<int> exclusiveVisibilityGroupIds)
        {
            int? visibilityGroupId = groupLink.TransitionGroup.VisibilityGroupId;
            if (visibilityGroupId != null && groupLink.TransitionGroup.Exclusive)
            {
                exclusiveVisibilityGroupIds.Add(visibilityGroupId.Value);
            }

            foreach (StateMatrixTransition transition in groupLink.TransitionGroup.Transitions
                .OrderBy(transition => transition.FromStateId)
                .ThenBy(transition => transition.SortOrder))
            {
                if (visibilityGroupId != null)
                {
                    AddStateVisibilityGroupId(stateVisibilityGroupIds, transition.FromStateId, visibilityGroupId.Value);
                    AddStateVisibilityGroupId(stateVisibilityGroupIds, transition.ToStateId, visibilityGroupId.Value);
                }

                AddTransition(matrix, transition.FromStateId, transition.ToStateId);
            }
        }

        private static void AddTransition(StateMatrix matrix, int fromStateId, int toStateId)
        {
            if (!matrix.Matrix.TryGetValue(fromStateId, out List<int>? targets))
            {
                targets = [];
                matrix.Matrix[fromStateId] = targets;
            }

            if (!targets.Contains(toStateId))
            {
                targets.Add(toStateId);
            }
        }

        private static void ValidateEditableBindings(GlobalStateMatrix stateMatrix)
        {
            foreach (WorkflowPhases phase in stateMatrix.GlobalMatrix.Keys)
            {
                if (!stateMatrix.PhaseBindings.TryGetValue(phase, out StateMatrixPhaseBinding? binding))
                {
                    throw new InvalidOperationException($"Workflow phase '{phase}' has no persistence binding.");
                }
                if (!stateMatrix.OriginalGlobalMatrix.ContainsKey(phase))
                {
                    throw new InvalidOperationException($"Workflow phase '{phase}' has no loaded comparison snapshot.");
                }
            }
        }

        private static StateMatrixConfigurationChanges BuildChanges(GlobalStateMatrix stateMatrix)
        {
            StateMatrixConfigurationChanges changes = new();
            foreach ((WorkflowPhases phase, StateMatrix matrix) in stateMatrix.GlobalMatrix)
            {
                StateMatrix original = stateMatrix.OriginalGlobalMatrix[phase];
                StateMatrixPhaseBinding binding = stateMatrix.PhaseBindings[phase];
                if (binding.PhaseMatrixId <= 0)
                {
                    if (!HasEqualPhaseValues(matrix, original)
                        || !HasEqualTransitionValues(matrix, original)
                        || !HasEqualDerivedStateValues(matrix, original))
                    {
                        throw new InvalidOperationException(
                            $"Workflow phase '{phase}' is missing a persistence binding and cannot be edited directly.");
                    }

                    continue;
                }
                if (!HasEqualPhaseValues(matrix, original))
                {
                    changes.PhaseMatrices.Add(new
                    {
                        id = binding.PhaseMatrixId,
                        name = binding.PhaseMatrixName,
                        phase = phase.ToString(),
                        active = matrix.Active,
                        lowest_input_state = matrix.LowestInputState,
                        lowest_start_state = matrix.LowestStartedState,
                        lowest_end_state = matrix.LowestEndState
                    });
                }
                AddDerivedStateChanges(changes, binding.PhaseMatrixId, original, matrix);
                if (!HasEqualTransitionValues(matrix, original))
                {
                    if (binding.TransitionGroupIds.Count != 1)
                    {
                        throw new InvalidOperationException(
                            $"Workflow phase '{phase}' uses {binding.TransitionGroupIds.Count} transition groups and its merged transitions cannot be edited directly.");
                    }
                    AddTransitionChanges(changes, phase, binding, original, matrix);
                }
            }
            return changes;
        }

        private static void AddDerivedStateChanges(StateMatrixConfigurationChanges changes, int phaseMatrixId, StateMatrix original, StateMatrix current)
        {
            Dictionary<int, int> originalStates = original.DerivedStates
                .Where(entry => entry.Key != entry.Value)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            Dictionary<int, int> currentStates = current.DerivedStates
                .Where(entry => entry.Key != entry.Value)
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            foreach (int fromStateId in originalStates.Keys.Except(currentStates.Keys))
            {
                changes.RemovedDerivedStates.Add(new
                {
                    phase_matrix_id = new { _eq = phaseMatrixId },
                    from_state_id = new { _eq = fromStateId }
                });
            }
            foreach ((int fromStateId, int derivedStateId) in currentStates.Where(entry =>
                !originalStates.TryGetValue(entry.Key, out int originalStateId) || originalStateId != entry.Value))
            {
                changes.DerivedStates.Add(new
                {
                    phase_matrix_id = phaseMatrixId,
                    from_state_id = fromStateId,
                    derived_state_id = derivedStateId
                });
            }
        }

        private static void AddTransitionChanges(StateMatrixConfigurationChanges changes, WorkflowPhases phase,
            StateMatrixPhaseBinding binding, StateMatrix original, StateMatrix current)
        {
            int transitionGroupId = binding.TransitionGroupIds.Single();
            Dictionary<(int FromStateId, int ToStateId), int> currentSortOrders = BuildCurrentSortOrders(binding, original, current);
            changes.TransitionSortOrders[phase] = currentSortOrders;

            HashSet<(int FromStateId, int ToStateId)> originalEdges = original.Matrix
                .SelectMany(entry => entry.Value.Distinct().Select(target => (entry.Key, target)))
                .ToHashSet();
            foreach ((int fromStateId, int toStateId) in originalEdges.Except(currentSortOrders.Keys))
            {
                changes.RemovedTransitions.Add(new
                {
                    transition_group_id = new { _eq = transitionGroupId },
                    from_state_id = new { _eq = fromStateId },
                    to_state_id = new { _eq = toStateId }
                });
            }
            foreach (((int fromStateId, int toStateId), int sortOrder) in currentSortOrders.Where(entry =>
                !originalEdges.Contains(entry.Key)
                || !binding.TransitionSortOrders.TryGetValue(entry.Key, out int originalSortOrder)
                || originalSortOrder != entry.Value))
            {
                changes.Transitions.Add(new
                {
                    transition_group_id = transitionGroupId,
                    from_state_id = fromStateId,
                    to_state_id = toStateId,
                    sort_order = sortOrder
                });
            }
        }

        private static Dictionary<(int FromStateId, int ToStateId), int> BuildCurrentSortOrders(
            StateMatrixPhaseBinding binding, StateMatrix original, StateMatrix current)
        {
            Dictionary<(int FromStateId, int ToStateId), int> sortOrders = [];
            foreach ((int fromStateId, List<int> targets) in current.Matrix)
            {
                List<int> distinctTargets = targets.Distinct().ToList();
                bool orderUnchanged = original.Matrix.TryGetValue(fromStateId, out List<int>? originalTargets)
                    && originalTargets.SequenceEqual(distinctTargets);
                foreach ((int toStateId, int index) in distinctTargets.Select((target, index) => (target, index)))
                {
                    (int FromStateId, int ToStateId) edge = (fromStateId, toStateId);
                    sortOrders[edge] = orderUnchanged && binding.TransitionSortOrders.TryGetValue(edge, out int originalSortOrder)
                        ? originalSortOrder
                        : index + 1;
                }
            }
            return sortOrders;
        }

        private static Dictionary<(int FromStateId, int ToStateId), int> BuildTransitionSortOrders(StateMatrixPhase phaseData)
        {
            Dictionary<(int FromStateId, int ToStateId), int> sortOrders = [];
            foreach (StateMatrixTransition transition in phaseData.TransitionGroups
                .OrderBy(group => group.SortOrder)
                .SelectMany(group => group.TransitionGroup.Transitions))
            {
                sortOrders.TryAdd((transition.FromStateId, transition.ToStateId), transition.SortOrder);
            }
            return sortOrders;
        }

        private static bool HasEqualPhaseValues(StateMatrix left, StateMatrix right)
        {
            return left.Active == right.Active
                && left.LowestInputState == right.LowestInputState
                && left.LowestStartedState == right.LowestStartedState
                && left.LowestEndState == right.LowestEndState;
        }

        private static bool HasEqualDerivedStateValues(StateMatrix left, StateMatrix right)
        {
            Dictionary<int, int> leftStates = left.DerivedStates
                .Where(entry => entry.Key != entry.Value)
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            Dictionary<int, int> rightStates = right.DerivedStates
                .Where(entry => entry.Key != entry.Value)
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            return leftStates.Count == rightStates.Count && leftStates.All(entry =>
                rightStates.TryGetValue(entry.Key, out int rightStateId) && rightStateId == entry.Value);
        }

        private static bool HasEqualTransitionValues(StateMatrix left, StateMatrix right)
        {
            return left.Matrix.Count == right.Matrix.Count && left.Matrix.All(entry =>
                right.Matrix.TryGetValue(entry.Key, out List<int>? targets) && entry.Value.SequenceEqual(targets));
        }

        private static void AddStateVisibilityGroupId(Dictionary<int, HashSet<int>> stateVisibilityGroupIds, int stateId, int visibilityGroupId)
        {
            if (!stateVisibilityGroupIds.TryGetValue(stateId, out HashSet<int>? visibilityGroupIds))
            {
                visibilityGroupIds = [];
                stateVisibilityGroupIds[stateId] = visibilityGroupIds;
            }

            visibilityGroupIds.Add(visibilityGroupId);
        }
    }

    public record StateMatrixConfigurationSnapshot(
        int ConfigurationId,
        string ConfigurationName,
        Dictionary<WorkflowPhases, StateMatrix> Matrices,
        Dictionary<WorkflowPhases, StateMatrixPhaseBinding> PhaseBindings);

    public record StateMatrixPhaseBinding(
        int PhaseMatrixId,
        string PhaseMatrixName,
        List<int> TransitionGroupIds,
        Dictionary<(int FromStateId, int ToStateId), int> TransitionSortOrders);

    internal class StateMatrixConfigurationChanges
    {
        public List<object> PhaseMatrices { get; } = [];
        public List<object> RemovedDerivedStates { get; } = [];
        public List<object> DerivedStates { get; } = [];
        public List<object> RemovedTransitions { get; } = [];
        public List<object> Transitions { get; } = [];
        public Dictionary<WorkflowPhases, Dictionary<(int FromStateId, int ToStateId), int>> TransitionSortOrders { get; } = [];

        public bool HasChanges => PhaseMatrices.Count > 0
            || RemovedDerivedStates.Count > 0
            || DerivedStates.Count > 0
            || RemovedTransitions.Count > 0
            || Transitions.Count > 0;
    }
}
