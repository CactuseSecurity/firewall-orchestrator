using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Data.Workflow;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Request of <c>POST /api/WorkflowConfiguration/List</c>. The descriptions of all keys are kept in
/// <see cref="WorkflowConfigurationValidationSchema"/>.
/// </summary>
public sealed class ListWorkflowConfigurationsRequest : IRequestWithRootAdditionalData
{
    private ListWorkflowConfigurationsOptions options = new();

    /// <summary>Optional output options. Defaults to an empty object; an explicit null is treated like the default.</summary>
    [JsonPropertyName("options")]
    public ListWorkflowConfigurationsOptions Options
    {
        get => options;
        set => options = value ?? new ListWorkflowConfigurationsOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Output options of the workflow configuration list.</summary>
public sealed class ListWorkflowConfigurationsOptions : IRequestWithAdditionalData
{
    /// <summary>Optional response filter; null applies no restriction.</summary>
    [JsonPropertyName("filter")]
    public WorkflowConfigurationFilterRequest? Filter { get; set; }

    /// <summary>
    /// Maximum number of configurations returned, between 1 and the configured <c>maxRestResponseObjectLimit</c>; null
    /// returns a page of that size. The <c>X-Has-More</c> response header tells whether further configurations follow.
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>Number of configurations skipped, at least 0; null skips none.</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Response filter of the workflow configuration list: every key matches a field of
/// <see cref="WorkflowConfigurationSummary"/> and is nullable; a key that is omitted or null does not restrict the result.
/// </summary>
public sealed class WorkflowConfigurationFilterRequest : IRequestWithAdditionalData
{
    /// <summary>Optional exact configuration id.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Optional case-insensitive part of the name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Optional case-insensitive part of the description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Optional exact active flag.</summary>
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    /// <summary>Unsupported filter keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Request of <c>POST /api/WorkflowConfiguration/Export</c>. The descriptions of all keys are kept in
/// <see cref="WorkflowConfigurationValidationSchema"/>.
/// </summary>
public sealed class ExportWorkflowConfigurationRequest : IRequestWithRootAdditionalData
{
    private ExportWorkflowConfigurationOptions options = new();

    /// <summary>Required id of the configuration to export, as listed by <c>POST /api/WorkflowConfiguration/List</c>.</summary>
    [JsonPropertyName("configurationId")]
    public int? ConfigurationId { get; set; }

    /// <summary>Optional export options. Defaults to an empty object; an explicit null is treated like the default.</summary>
    [JsonPropertyName("options")]
    public ExportWorkflowConfigurationOptions Options
    {
        get => options;
        set => options = value ?? new ExportWorkflowConfigurationOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Options of the workflow configuration export.</summary>
public sealed class ExportWorkflowConfigurationOptions : IRequestWithAdditionalData
{
    /// <summary>True also exports the visibility groups with their members. Defaults to false; null means the default.</summary>
    [JsonPropertyName("includeVisibilityGroups")]
    public bool? IncludeVisibilityGroups { get; set; } = false;

    /// <summary>Phases to export; null exports all phases of the configuration.</summary>
    [JsonPropertyName("phases")]
    public List<WorkflowPhaseSelectionRequest?>? Phases { get; set; }

    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>One phase of a configuration, addressed like in the package by task type and phase name.</summary>
public sealed class WorkflowPhaseSelectionRequest : IRequestWithAdditionalData
{
    /// <summary>Required task type of the phase, e.g. <c>access</c>.</summary>
    [JsonPropertyName("task_type")]
    public string? TaskType { get; set; }

    /// <summary>Required name of the phase, e.g. <c>approval</c>.</summary>
    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    /// <summary>Unsupported keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Request of <c>POST /api/WorkflowConfiguration/Import</c>. The descriptions of all keys are kept in
/// <see cref="WorkflowConfigurationValidationSchema"/>.
/// </summary>
public sealed class ImportWorkflowConfigurationRequest : IRequestWithRootAdditionalData
{
    private ImportWorkflowConfigurationOptions options = new();

    /// <summary>Required workflow configuration package as returned by <c>POST /api/WorkflowConfiguration/Export</c>.</summary>
    [JsonPropertyName("package")]
    public WorkflowConfigurationTransferPackage? Package { get; set; }

    /// <summary>Optional import options. Defaults to an empty object; an explicit null is treated like the default.</summary>
    [JsonPropertyName("options")]
    public ImportWorkflowConfigurationOptions Options
    {
        get => options;
        set => options = value ?? new ImportWorkflowConfigurationOptions();
    }

    /// <summary>Unsupported root keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>Options of the workflow configuration import.</summary>
public sealed class ImportWorkflowConfigurationOptions : IRequestWithAdditionalData
{
    /// <summary>Name of the new configuration; null uses the configuration name of the package.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Id of an existing configuration to merge the package into; null creates a new configuration.</summary>
    [JsonPropertyName("targetConfigurationId")]
    public int? TargetConfigurationId { get; set; }

    /// <summary>Unsupported option keys, reported back to the caller.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
