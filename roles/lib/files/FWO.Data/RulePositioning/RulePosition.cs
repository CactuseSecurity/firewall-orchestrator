using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Assigns a stable persisted discriminator to a rule-position subtype.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RulePositionTypeAttribute : Attribute
{
    public string Discriminator { get; }

    /// <summary>
    /// Creates a discriminator declaration for a rule-position subtype.
    /// </summary>
    public RulePositionTypeAttribute(string discriminator)
    {
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new ArgumentException("A rule-position discriminator must not be empty.", nameof(discriminator));
        }
        Discriminator = discriminator;
    }
}

/// <summary>
/// Base contract for a calculated, persistable firewall-rule position.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftRulePositionJsonConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(SystemTextRulePositionJsonConverter))]
public abstract class RulePosition
{
    private const int kInitialSchemaVersion = 1;

    [JsonProperty("position_type"), JsonPropertyName("position_type")]
    public string PositionType { get; protected set; }

    [JsonProperty("schema_version"), JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = kInitialSchemaVersion;

    [JsonProperty("positioning_algorithm"), JsonPropertyName("positioning_algorithm")]
    [Newtonsoft.Json.JsonConverter(typeof(NewtonsoftProvisioningPositioningAlgorithmJsonConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(SystemTextProvisioningPositioningAlgorithmJsonConverter))]
    public ProvisioningPositioningAlgorithm PositioningAlgorithm { get; set; }

    /// <summary>
    /// Creates a known position using the subtype's stable discriminator.
    /// </summary>
    protected RulePosition()
    {
        PositionType = RulePositionTypeResolver.GetRequiredDiscriminator(GetType());
    }

    /// <summary>
    /// Creates a position whose discriminator is supplied by persisted data.
    /// </summary>
    protected RulePosition(
        string positionType,
        int schemaVersion,
        ProvisioningPositioningAlgorithm positioningAlgorithm)
    {
        PositionType = positionType;
        SchemaVersion = schemaVersion;
        PositioningAlgorithm = positioningAlgorithm;
    }

    /// <summary>
    /// Creates an independent copy, including all subtype payload data.
    /// </summary>
    public virtual RulePosition DeepCopy()
    {
        string serializedPosition = System.Text.Json.JsonSerializer.Serialize<RulePosition>(this);
        return System.Text.Json.JsonSerializer.Deserialize<RulePosition>(serializedPosition)
            ?? throw new System.Text.Json.JsonException("The rule position could not be copied.");
    }
}

/// <summary>
/// Represents a position subtype that is not known to the current client.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(NewtonsoftUnknownRulePositionJsonConverter))]
public sealed class UnknownRulePosition : RulePosition
{
    [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
    public JsonObject RawPayload { get; }

    /// <summary>
    /// Creates an unknown position while retaining its complete JSON payload.
    /// </summary>
    public UnknownRulePosition(
        string positionType,
        int schemaVersion,
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        JsonObject rawPayload)
        : base(positionType, schemaVersion, positioningAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(rawPayload);
        RawPayload = (JsonObject)rawPayload.DeepClone();
    }

    /// <summary>
    /// Creates an independent copy of the retained JSON payload.
    /// </summary>
    public override RulePosition DeepCopy()
    {
        return new UnknownRulePosition(PositionType, SchemaVersion, PositioningAlgorithm, RawPayload);
    }

    /// <summary>
    /// Builds a JSON object containing the retained vendor data and current common metadata.
    /// </summary>
    internal JsonObject CreateSerializationPayload()
    {
        JsonObject payload = (JsonObject)RawPayload.DeepClone();
        payload[RulePositionJsonProperties.kPositionType] = PositionType;
        payload[RulePositionJsonProperties.kSchemaVersion] = SchemaVersion;
        payload[RulePositionJsonProperties.kPositioningAlgorithm] = (int)PositioningAlgorithm;
        return payload;
    }
}

internal static class RulePositionJsonProperties
{
    internal const string kPositionType = "position_type";
    internal const string kSchemaVersion = "schema_version";
    internal const string kPositioningAlgorithm = "positioning_algorithm";
}
