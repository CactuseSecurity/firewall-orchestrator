using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    public class WorkflowConfiguration
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("is_active"), JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonProperty("workflow_configuration_phases"), JsonPropertyName("workflow_configuration_phases")]
        public List<WorkflowConfigurationPhase> Phases { get; set; } = [];
    }

    public class WorkflowConfigurationPhase
    {
        [JsonProperty("task_type"), JsonPropertyName("task_type")]
        public string TaskType { get; set; } = "";

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonProperty("phase_matrix_id"), JsonPropertyName("phase_matrix_id")]
        public int PhaseMatrixId { get; set; }

        [JsonProperty("state_matrix_phase"), JsonPropertyName("state_matrix_phase")]
        public StateMatrixPhase PhaseMatrix { get; set; } = new();
    }

    public class StateMatrixPhase
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonProperty("active"), JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonProperty("lowest_input_state"), JsonPropertyName("lowest_input_state")]
        public int LowestInputState { get; set; }

        [JsonProperty("lowest_start_state"), JsonPropertyName("lowest_start_state")]
        public int LowestStartState { get; set; }

        [JsonProperty("lowest_end_state"), JsonPropertyName("lowest_end_state")]
        public int LowestEndState { get; set; }

        [JsonProperty("state_matrix_derived_states"), JsonPropertyName("state_matrix_derived_states")]
        public List<StateMatrixDerivedState> DerivedStates { get; set; } = [];

        [JsonProperty("state_matrix_phase_transition_groups"), JsonPropertyName("state_matrix_phase_transition_groups")]
        public List<StateMatrixPhaseTransitionGroup> TransitionGroups { get; set; } = [];
    }

    public class StateMatrixDerivedState
    {
        [JsonProperty("from_state_id"), JsonPropertyName("from_state_id")]
        public int FromStateId { get; set; }

        [JsonProperty("derived_state_id"), JsonPropertyName("derived_state_id")]
        public int DerivedStateId { get; set; }
    }

    public class StateMatrixPhaseTransitionGroup
    {
        [JsonProperty("phase_matrix_id"), JsonPropertyName("phase_matrix_id")]
        public int PhaseMatrixId { get; set; }

        [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }

        [JsonProperty("transition_group_id"), JsonPropertyName("transition_group_id")]
        public int TransitionGroupId { get; set; }

        [JsonProperty("state_matrix_transition_group"), JsonPropertyName("state_matrix_transition_group")]
        public StateMatrixTransitionGroup TransitionGroup { get; set; } = new();

        [JsonProperty("state_matrix_phase"), JsonPropertyName("state_matrix_phase")]
        public StateMatrixPhase PhaseMatrix { get; set; } = new();
    }

    public class StateMatrixTransitionGroup
    {
        private List<StateMatrixTransition> transitions = [];
        private List<StateMatrixPhaseTransitionGroup> phaseMatrixUsages = [];

        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("phase"), JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonProperty("visibility_group_id"), JsonPropertyName("visibility_group_id")]
        public int? VisibilityGroupId { get; set; }

        [JsonProperty("exclusive"), JsonPropertyName("exclusive")]
        public bool Exclusive { get; set; }

        [JsonProperty("workflow_visibility_group"), JsonPropertyName("workflow_visibility_group")]
        public WorkflowVisibilityGroup? VisibilityGroup { get; set; }

        [JsonProperty("state_matrix_transitions"), JsonPropertyName("state_matrix_transitions")]
        public List<StateMatrixTransition> Transitions
        {
            get => transitions;
            set => transitions = value ?? [];
        }

        [JsonProperty("state_matrix_phase_transition_groups"), JsonPropertyName("state_matrix_phase_transition_groups")]
        public List<StateMatrixPhaseTransitionGroup> PhaseMatrixUsages
        {
            get => phaseMatrixUsages;
            set => phaseMatrixUsages = value ?? [];
        }

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public int PhaseMatrixUsageCount => PhaseMatrixUsages.Count;

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public int TransitionCount => Transitions.Count;
    }

    public class WorkflowVisibilityGroup
    {
        private List<WorkflowVisibilityGroupMember> members = [];

        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("description"), JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("workflow_visibility_group_members"), JsonPropertyName("workflow_visibility_group_members")]
        public List<WorkflowVisibilityGroupMember> Members
        {
            get => members;
            set => members = value ?? [];
        }
    }

    public class WorkflowVisibilityGroupMember
    {
        [JsonProperty("visibility_group_id"), JsonPropertyName("visibility_group_id")]
        public int VisibilityGroupId { get; set; }

        [JsonProperty("member_dn"), JsonPropertyName("member_dn")]
        public string MemberDn { get; set; } = "";
    }

    public class StateMatrixTransition
    {
        [JsonProperty("from_state_id"), JsonPropertyName("from_state_id")]
        public int FromStateId { get; set; }

        [JsonProperty("to_state_id"), JsonPropertyName("to_state_id")]
        public int ToStateId { get; set; }

        [JsonProperty("sort_order"), JsonPropertyName("sort_order")]
        public int SortOrder { get; set; }
    }
}
