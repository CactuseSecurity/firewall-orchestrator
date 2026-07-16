using System.Text.Json.Serialization;

namespace FWO.Data.Workflow
{
    public class WorkflowConfigurationTransferPackage
    {
        public const string kFormat = "fwo-workflow-configuration";
        public const int kCurrentVersion = 1;

        [JsonPropertyName("format")]
        public string Format { get; set; } = kFormat;

        [JsonPropertyName("version")]
        public int Version { get; set; } = kCurrentVersion;

        [JsonPropertyName("configuration")]
        public WorkflowConfigurationTransferData Configuration { get; set; } = new();

        [JsonPropertyName("transition_groups")]
        public List<WorkflowTransitionGroupTransferData> TransitionGroups { get; set; } = [];

        [JsonPropertyName("visibility_groups"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<WorkflowVisibilityGroupTransferData>? VisibilityGroups { get; set; }
    }

    public class WorkflowConfigurationTransferData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("phases")]
        public List<WorkflowPhaseTransferData> Phases { get; set; } = [];
    }

    public class WorkflowPhaseTransferData
    {
        [JsonPropertyName("task_type")]
        public string TaskType { get; set; } = "";

        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "";

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("lowest_input_state")]
        public int LowestInputState { get; set; }

        [JsonPropertyName("lowest_start_state")]
        public int LowestStartState { get; set; }

        [JsonPropertyName("lowest_end_state")]
        public int LowestEndState { get; set; }

        [JsonPropertyName("derived_states")]
        public List<StateMatrixDerivedState> DerivedStates { get; set; } = [];

        [JsonPropertyName("transition_groups")]
        public List<string> TransitionGroups { get; set; } = [];
    }

    public class WorkflowTransitionGroupTransferData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("exclusive")]
        public bool Exclusive { get; set; }

        [JsonPropertyName("visibility_group")]
        public string? VisibilityGroup { get; set; }

        [JsonPropertyName("transitions")]
        public List<StateMatrixTransition> Transitions { get; set; } = [];
    }

    public class WorkflowVisibilityGroupTransferData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("members")]
        public List<string> Members { get; set; } = [];
    }
}
