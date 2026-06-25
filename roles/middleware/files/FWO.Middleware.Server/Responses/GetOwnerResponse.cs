using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Responses;

/// <summary>
/// Represents an owner returned by the owners/get endpoint.
/// Detail fields are only populated when <c>showDetails</c> is requested.
/// </summary>
public sealed class GetOwnerResponse
{
    /// <summary>
    /// Gets or sets the owner database id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the owner name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external application id.
    /// </summary>
    [JsonPropertyName("appIdExternal")]
    public string? AppIdExternal { get; set; }

    /// <summary>
    /// Gets or sets the owner type, derived from <see cref="AppIdExternal"/>:
    /// <c>standard</c> when the external app id contains <c>app</c> (case-insensitive), <c>infrastructure</c> otherwise.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owner lifecycle state.
    /// </summary>
    [JsonPropertyName("ownerLifecycleState")]
    public OwnerLifecycleStateResponse? OwnerLifecycleState { get; set; }

    /// <summary>
    /// Gets or sets the distinguished names responsible for the owner.
    /// </summary>
    [JsonPropertyName("ownerResponsibles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OwnerResponsibleResponse>? OwnerResponsibles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the owner is the default owner.
    /// </summary>
    [JsonPropertyName("isDefault")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the tenant id.
    /// </summary>
    [JsonPropertyName("tenantId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the recertification interval in days.
    /// </summary>
    [JsonPropertyName("recertInterval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RecertInterval { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last recertification check.
    /// </summary>
    [JsonPropertyName("lastRecertCheck")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastRecertCheck { get; set; }

    /// <summary>
    /// Gets or sets the recertification check parameters.
    /// </summary>
    [JsonPropertyName("recertCheckParams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecertCheckParams { get; set; }

    /// <summary>
    /// Gets or sets the owner criticality.
    /// </summary>
    [JsonPropertyName("criticality")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Criticality { get; set; }

    /// <summary>
    /// Gets or sets the owner lifecycle state id.
    /// </summary>
    [JsonPropertyName("ownerLifecycleStateId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OwnerLifecycleStateId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the owner is active.
    /// </summary>
    [JsonPropertyName("active")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Active { get; set; }

    /// <summary>
    /// Gets or sets the import source.
    /// </summary>
    [JsonPropertyName("importSource")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImportSource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether common services are possible.
    /// </summary>
    [JsonPropertyName("commonServicePossible")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CommonServicePossible { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last recertification.
    /// </summary>
    [JsonPropertyName("lastRecertified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastRecertified { get; set; }

    /// <summary>
    /// Gets or sets the id of the last recertifier.
    /// </summary>
    [JsonPropertyName("lastRecertifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LastRecertifier { get; set; }

    /// <summary>
    /// Gets or sets the distinguished name of the last recertifier.
    /// </summary>
    [JsonPropertyName("lastRecertifierDn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastRecertifierDn { get; set; }

    /// <summary>
    /// Gets or sets the next recertification date.
    /// </summary>
    [JsonPropertyName("nextRecertDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? NextRecertDate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recertification is active.
    /// </summary>
    [JsonPropertyName("recertActive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RecertActive { get; set; }

    /// <summary>
    /// Gets or sets the decommission date.
    /// </summary>
    [JsonPropertyName("decommDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DecommDate { get; set; }

    /// <summary>
    /// Gets or sets additional info stored for the owner.
    /// </summary>
    [JsonPropertyName("additionalInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? AdditionalInfo { get; set; }
}

/// <summary>
/// Represents one responsible distinguished name assigned to an owner.
/// </summary>
public sealed class OwnerResponsibleResponse
{
    /// <summary>
    /// Gets or sets the responsible distinguished name.
    /// </summary>
    [JsonPropertyName("dn")]
    public string Dn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the owner responsible type id.
    /// </summary>
    [JsonPropertyName("responsibleType")]
    public int ResponsibleType { get; set; }
}

/// <summary>
/// Represents the lifecycle state of an owner.
/// </summary>
public sealed class OwnerLifecycleStateResponse
{
    /// <summary>
    /// Gets or sets the lifecycle state id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
