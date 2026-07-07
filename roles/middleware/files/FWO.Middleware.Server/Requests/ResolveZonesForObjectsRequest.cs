using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Represents the request body for resolving zones for object trees.
/// </summary>
public sealed class ResolveZonesForObjectsRequest : IRequestWithRootAdditionalData
{
    /// <summary>
    /// Gets the root objects to evaluate.
    /// </summary>
    [JsonPropertyName("objects")]
    public List<ObjectRequest> Objects { get; set; } = [];

    /// <summary>
    /// Gets the additional data payload.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    /// <summary>
    /// Represents one object in the zone resolution tree.
    /// </summary>
    [JsonConverter(typeof(ResolveZonesForObjectsRequestObjectConverter))]
    public abstract class ObjectRequest : IRequestWithAdditionalData
    {
        /// <summary>
        /// Gets the display name of the object.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets the additional data payload.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    /// <summary>
    /// Represents a leaf object with IP data.
    /// </summary>
    public sealed class LeafObjectRequest : ObjectRequest
    {
        /// <summary>
        /// Gets the object type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets the start IP or range value.
        /// </summary>
        [JsonPropertyName("ipStart")]
        public string IpStart { get; set; } = string.Empty;

        /// <summary>
        /// Gets the end IP or range value.
        /// </summary>
        [JsonPropertyName("ipEnd")]
        public string IpEnd { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a group object with nested members.
    /// </summary>
    public sealed class GroupObjectRequest : ObjectRequest
    {
        /// <summary>
        /// Gets the child members.
        /// </summary>
        [JsonPropertyName("members")]
        public List<ObjectRequest> Members { get; set; } = [];
    }
}

/// <summary>
/// Serializes and deserializes zone resolution object nodes without a synthetic discriminator.
/// </summary>
public sealed class ResolveZonesForObjectsRequestObjectConverter : JsonConverter<ResolveZonesForObjectsRequest.ObjectRequest>
{
    /// <inheritdoc />
    public override ResolveZonesForObjectsRequest.ObjectRequest? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected an object.");
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        Type targetType = root.TryGetProperty("members", out _) ? typeof(ResolveZonesForObjectsRequest.GroupObjectRequest) : typeof(ResolveZonesForObjectsRequest.LeafObjectRequest);
        return (ResolveZonesForObjectsRequest.ObjectRequest?)JsonSerializer.Deserialize(root.GetRawText(), targetType, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ResolveZonesForObjectsRequest.ObjectRequest value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case ResolveZonesForObjectsRequest.GroupObjectRequest group:
                JsonSerializer.Serialize(writer, group, options);
                break;
            case ResolveZonesForObjectsRequest.LeafObjectRequest leaf:
                JsonSerializer.Serialize(writer, leaf, options);
                break;
            default:
                throw new JsonException($"Unsupported object node type '{value.GetType().Name}'.");
        }
    }
}
