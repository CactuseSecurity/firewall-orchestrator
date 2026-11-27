using System.Text.Json.Serialization;
using FWO.Data.Workflow;

namespace FWO.Middleware.Server.Responses;

/// <summary>Response of <c>POST /api/WorkflowConfiguration/Import</c>.</summary>
public sealed class ImportWorkflowConfigurationResponse
{
    /// <summary>Id of the created configuration, or of the configuration the package was merged into.</summary>
    [JsonPropertyName("configurationId")]
    public int ConfigurationId { get; set; }

    /// <summary>True if a new, inactive configuration was created; false if the package was merged into an existing one.</summary>
    [JsonPropertyName("created")]
    public bool Created { get; set; }

    /// <summary>Number of created states, transition groups and visibility groups and of added or merged phase content.</summary>
    [JsonPropertyName("statistics")]
    public WorkflowConfigurationImportStatistics Statistics { get; set; } = new();
}
