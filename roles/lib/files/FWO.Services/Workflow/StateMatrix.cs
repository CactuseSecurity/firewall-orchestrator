using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Services.Workflow
{
    public class StateMatrix
    {
        [JsonProperty("matrix"), JsonPropertyName("matrix")]
        public Dictionary<int, List<int>> Matrix { get; set; } = [];

        [JsonProperty("derived_states"), JsonPropertyName("derived_states")]
        public Dictionary<int, int> DerivedStates { get; set; } = [];

        [JsonProperty("lowest_input_state"), JsonPropertyName("lowest_input_state")]
        public int LowestInputState { get; set; }

        [JsonProperty("lowest_start_state"), JsonPropertyName("lowest_start_state")]
        public int LowestStartedState { get; set; }

        [JsonProperty("lowest_end_state"), JsonPropertyName("lowest_end_state")]
        public int LowestEndState { get; set; }

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<int, List<int>> StateVisibilityGroupIds { get; set; } = [];

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public HashSet<int> ExclusiveVisibilityGroupIds { get; set; } = [];

        public HashSet<int> AutomaticOnlyStates { get; set; } = [];
        public Dictionary<WorkflowPhases, bool> PhaseActive = [];
        public bool IsLastActivePhase = true;
        public int MinImplTasksNeeded;
        public int MinTicketCompleted;
        public int ApprovalLowestEndState;

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
        {
            await Init(phase, apiConnection, await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates), taskType);
        }

        /// <summary>
        /// Initializes the state matrix from workflow config and an already loaded state list.
        /// </summary>
        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection, List<WfState> preloadedStates, WfTaskType taskType = WfTaskType.master)
        {
            GlobalStateMatrix glbStateMatrix = GlobalStateMatrix.Create();
            await glbStateMatrix.Init(apiConnection, taskType);
            Matrix = glbStateMatrix.GlobalMatrix[phase].Matrix;
            DerivedStates = glbStateMatrix.GlobalMatrix[phase].DerivedStates;
            LowestInputState = glbStateMatrix.GlobalMatrix[phase].LowestInputState;
            LowestStartedState = glbStateMatrix.GlobalMatrix[phase].LowestStartedState;
            LowestEndState = glbStateMatrix.GlobalMatrix[phase].LowestEndState;
            Active = glbStateMatrix.GlobalMatrix[phase].Active;
            StateVisibilityGroupIds = glbStateMatrix.GlobalMatrix[phase].StateVisibilityGroupIds.ToDictionary(entry => entry.Key, entry => entry.Value.ToList());
            ExclusiveVisibilityGroupIds = [.. glbStateMatrix.GlobalMatrix[phase].ExclusiveVisibilityGroupIds];
            AutomaticOnlyStates = preloadedStates.Where(state => state.AutomaticOnly).Select(state => state.Id).ToHashSet();
            ApprovalLowestEndState = glbStateMatrix.GlobalMatrix[WorkflowPhases.approval].LowestEndState;
            foreach (var phas in glbStateMatrix.GlobalMatrix)
            {
                PhaseActive.Add(phas.Key, glbStateMatrix.GlobalMatrix[phas.Key].Active);
                if (glbStateMatrix.GlobalMatrix[phas.Key].Active && phas.Key > phase)
                {
                    IsLastActivePhase = false;
                }
            }
            MinImplTasksNeeded = glbStateMatrix.GlobalMatrix[WorkflowPhases.implementation].LowestInputState;
            MinTicketCompleted = glbStateMatrix.GlobalMatrix[PhaseActive.LastOrDefault(p => p.Value == true).Key].LowestEndState;
        }

        public bool getNextActivePhase(ref WorkflowPhases phase)
        {
            foreach (var tmpPhase in PhaseActive)
            {
                if (tmpPhase.Key > phase && tmpPhase.Value)
                {
                    phase = tmpPhase.Key;
                    return true;
                }
            }
            return false;
        }

        public List<int> getAllowedTransitions(int stateIn, bool allowAutomaticOnlyStates = false)
        {
            if (!Matrix.TryGetValue(stateIn, out List<int>? value))
            {
                return [];
            }

            if (allowAutomaticOnlyStates)
            {
                return value;
            }

            return value.Where(stateId => !AutomaticOnlyStates.Contains(stateId)).ToList();
        }

        /// <summary>
        /// Returns the visibility groups that protect the given state.
        /// </summary>
        public List<int> GetVisibilityGroupIds(int stateId)
        {
            return StateVisibilityGroupIds.TryGetValue(stateId, out List<int>? visibilityGroupIds)
                ? visibilityGroupIds
                : [];
        }

        /// <summary>
        /// Returns true when the given visibility-group ids grant access to the state.
        /// </summary>
        public bool CanAccessState(int stateId, IEnumerable<int> userVisibilityGroupIds)
        {
            return CanAccessState(stateId, userVisibilityGroupIds, ExclusiveVisibilityGroupIds);
        }

        /// <summary>
        /// Returns true when the given visibility-group ids grant access to the state.
        /// </summary>
        public bool CanAccessState(int stateId, IEnumerable<int> userVisibilityGroupIds, IEnumerable<int>? exclusiveVisibilityGroupIds)
        {
            List<int> requiredGroupIds = GetVisibilityGroupIds(stateId);
            if (requiredGroupIds.Count == 0)
            {
                // Untagged states stay visible unless the user belongs to any exclusive visibility group.
                return !HasExclusiveVisibilityGroupMembership(userVisibilityGroupIds, exclusiveVisibilityGroupIds);
            }

            HashSet<int> allowedGroupIds = userVisibilityGroupIds as HashSet<int> ?? userVisibilityGroupIds.ToHashSet();
            return requiredGroupIds.Any(allowedGroupIds.Contains);
        }

        private bool HasExclusiveVisibilityGroupMembership(IEnumerable<int> userVisibilityGroupIds, IEnumerable<int>? exclusiveVisibilityGroupIds)
        {
            IEnumerable<int> groupsToCheck = exclusiveVisibilityGroupIds ?? ExclusiveVisibilityGroupIds;
            if (!groupsToCheck.Any())
            {
                return false;
            }

            HashSet<int> allowedGroupIds = userVisibilityGroupIds as HashSet<int> ?? userVisibilityGroupIds.ToHashSet();
            return groupsToCheck.Any(allowedGroupIds.Contains);
        }

        public int getDerivedStateFromSubStates(List<int> statesIn)
        {
            if (statesIn.Count == 0)
            {
                return 0;
            }
            int stateOut;
            DerivedStateTracking tracking = new(LowestInputState, 0, LowestEndState, 999);
            TaskCounters counters = new();
            foreach (int state in statesIn)
            {
                UpdateDerivedStateTracking(state, ref tracking, ref counters);
            }

            if (counters.BackAssignedTasks > 0)
            {
                stateOut = tracking.BackAssignedState;
            }
            else if (counters.InWorkTasks > 0)
            {
                stateOut = tracking.InWorkState;
            }
            else if (counters.FinishedTasks == statesIn.Count)
            {
                stateOut = tracking.MinFinishedState;
            }
            else if (counters.OpenTasks == statesIn.Count)
            {
                stateOut = tracking.InitState;
            }
            else
            {
                stateOut = LowestStartedState;
            }

            if (DerivedStates.ContainsKey(stateOut))
            {
                return DerivedStates[stateOut];
            }
            return stateOut;
        }

        private readonly record struct DerivedStateTracking(int BackAssignedState, int InitState, int InWorkState, int MinFinishedState);

        private struct TaskCounters
        {
            public int BackAssignedTasks;
            public int OpenTasks;
            public int InWorkTasks;
            public int FinishedTasks;
        }

        private void UpdateDerivedStateTracking(int state, ref DerivedStateTracking tracking, ref TaskCounters counters)
        {
            if (state < LowestInputState)
            {
                counters.BackAssignedTasks++;
                tracking = tracking with { BackAssignedState = Math.Min(state, tracking.BackAssignedState) };
            }
            else if (state < LowestStartedState)
            {
                counters.OpenTasks++;
                tracking = tracking with { InitState = state };
            }
            else if (state < LowestEndState)
            {
                counters.InWorkTasks++;
                tracking = tracking with { InWorkState = Math.Min(state, tracking.InWorkState) };
            }
            else
            {
                counters.FinishedTasks++;
                tracking = tracking with { MinFinishedState = Math.Min(state, tracking.MinFinishedState) };
            }
        }
    }

    public class GlobalStateMatrix
    {
        public static Func<GlobalStateMatrix> Factory { get; set; } = () => new GlobalStateMatrix();

        public static GlobalStateMatrix Create()
        {
            return Factory();
        }

        [JsonProperty("config_value"), JsonPropertyName("config_value")]
        public Dictionary<WorkflowPhases, StateMatrix> GlobalMatrix { get; set; } = [];

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public int ConfigurationId { get; private set; }

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public string ConfigurationName { get; private set; } = "";

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public Dictionary<WorkflowPhases, StateMatrixPhaseBinding> PhaseBindings { get; private set; } = [];

        internal Dictionary<WorkflowPhases, StateMatrix> OriginalGlobalMatrix { get; private set; } = [];

        public virtual async Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
        {
            await Load(apiConnection, taskType, null);
        }

        /// <summary>
        /// Initializes the matrix from a specifically named workflow configuration.
        /// </summary>
        public virtual async Task Init(ApiConnection apiConnection, WfTaskType taskType, string configurationName)
        {
            await Load(apiConnection, taskType, configurationName);
        }

        private async Task Load(ApiConnection apiConnection, WfTaskType taskType, string? configurationName)
        {
            StateMatrixConfigurationSnapshot snapshot = await StateMatrixConfigurationRepository.Load(apiConnection, taskType, configurationName);
            ConfigurationId = snapshot.ConfigurationId;
            ConfigurationName = snapshot.ConfigurationName;
            GlobalMatrix = snapshot.Matrices;
            PhaseBindings = snapshot.PhaseBindings;
            OriginalGlobalMatrix = CloneMatrices(GlobalMatrix);
        }

        /// <summary>
        /// Persists this task-type matrix into its currently bound workflow configuration.
        /// </summary>
        public virtual async Task Save(ApiConnection apiConnection)
        {
            await StateMatrixConfigurationRepository.Update(apiConnection, this);
        }

        /// <summary>
        /// Determines whether deferred phase or derived-state values differ from the loaded snapshot.
        /// Transition values are persisted separately and are intentionally ignored.
        /// </summary>
        public bool HasUnsavedChanges()
        {
            if (OriginalGlobalMatrix.Count == 0)
            {
                return false;
            }
            if (GlobalMatrix.Count != OriginalGlobalMatrix.Count)
            {
                return true;
            }
            return GlobalMatrix.Any(entry => !OriginalGlobalMatrix.TryGetValue(entry.Key, out StateMatrix? original)
                || HasChangedDeferredValues(entry.Value, original));
        }

        internal void AcceptChanges(Dictionary<WorkflowPhases, Dictionary<(int FromStateId, int ToStateId), int>> transitionSortOrders)
        {
            foreach ((WorkflowPhases phase, Dictionary<(int FromStateId, int ToStateId), int> sortOrders) in transitionSortOrders)
            {
                PhaseBindings[phase].TransitionSortOrders.Clear();
                foreach (((int fromStateId, int toStateId), int sortOrder) in sortOrders)
                {
                    PhaseBindings[phase].TransitionSortOrders[(fromStateId, toStateId)] = sortOrder;
                }
            }
            OriginalGlobalMatrix = CloneMatrices(GlobalMatrix);
        }

        private static Dictionary<WorkflowPhases, StateMatrix> CloneMatrices(Dictionary<WorkflowPhases, StateMatrix> source)
        {
            return source.ToDictionary(
                entry => entry.Key,
                entry => CloneMatrix(entry.Value));
        }

        private static StateMatrix CloneMatrix(StateMatrix source)
        {
            return new()
            {
                Matrix = source.Matrix.ToDictionary(entry => entry.Key, entry => entry.Value.ToList()),
                DerivedStates = new Dictionary<int, int>(source.DerivedStates),
                LowestInputState = source.LowestInputState,
                LowestStartedState = source.LowestStartedState,
                LowestEndState = source.LowestEndState,
                Active = source.Active,
                StateVisibilityGroupIds = source.StateVisibilityGroupIds.ToDictionary(entry => entry.Key, entry => entry.Value.ToList()),
                ExclusiveVisibilityGroupIds = [.. source.ExclusiveVisibilityGroupIds]
            };
        }

        private static bool HasChangedDeferredValues(StateMatrix current, StateMatrix original)
        {
            return current.Active != original.Active
                || current.LowestInputState != original.LowestInputState
                || current.LowestStartedState != original.LowestStartedState
                || current.LowestEndState != original.LowestEndState
                || !SparseDerivedStates(current).OrderBy(entry => entry.Key).SequenceEqual(
                    SparseDerivedStates(original).OrderBy(entry => entry.Key));
        }

        private static IEnumerable<KeyValuePair<int, int>> SparseDerivedStates(StateMatrix matrix) =>
            matrix.DerivedStates.Where(entry => entry.Key != entry.Value);
    }

    public class StateMatrixDict
    {
        public Dictionary<string, StateMatrix> Matrices { get; set; } = [];

        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection)
        {
            await Init(phase, apiConnection, await apiConnection.SendQueryAsync<List<WfState>>(RequestQueries.getStates));
        }

        /// <summary>
        /// Initializes all task-type matrices with an already loaded state list.
        /// </summary>
        public async Task Init(WorkflowPhases phase, ApiConnection apiConnection, List<WfState> preloadedStates)
        {
            Dictionary<string, StateMatrix> matrices = [];
            foreach (WfTaskType taskType in Enum.GetValues(typeof(WfTaskType)))
            {
                StateMatrix stateMatrix = new();
                matrices.Add(taskType.ToString(), stateMatrix);
                await stateMatrix.Init(phase, apiConnection, preloadedStates, taskType);
            }
            Matrices = matrices;
        }
    }
}
