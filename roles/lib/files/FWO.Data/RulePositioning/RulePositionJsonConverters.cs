using System.Text.Json;
using System.Text.Json.Nodes;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;
using NewtonsoftJsonReader = Newtonsoft.Json.JsonReader;
using NewtonsoftJsonSerializer = Newtonsoft.Json.JsonSerializer;
using NewtonsoftJsonWriter = Newtonsoft.Json.JsonWriter;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Polymorphic rule-position converter for System.Text.Json.
/// </summary>
public sealed class SystemTextRulePositionJsonConverter : System.Text.Json.Serialization.JsonConverter<RulePosition>
{
    /// <summary>
    /// Reads a known subtype or retains an unknown subtype's complete JSON object.
    /// </summary>
    public override RulePosition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        string discriminator = GetDiscriminator(root);
        Type? knownType = RulePositionTypeResolver.Resolve(discriminator);
        if (knownType != null)
        {
            return (RulePosition?)SystemTextJsonSerializer.Deserialize(root.GetRawText(), knownType, options)
                ?? throw new System.Text.Json.JsonException("The known rule position could not be deserialized.");
        }
        return CreateUnknownPosition(root.GetRawText());
    }

    /// <summary>
    /// Writes a known subtype or the retained payload of an unknown subtype.
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer,
        RulePosition value,
        JsonSerializerOptions options)
    {
        if (value is UnknownRulePosition unknownPosition)
        {
            unknownPosition.CreateSerializationPayload().WriteTo(writer, options);
            return;
        }
        SystemTextJsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    /// <summary>
    /// Reads and validates the persisted discriminator.
    /// </summary>
    private static string GetDiscriminator(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(RulePositionJsonProperties.kPositionType, out JsonElement discriminatorElement) ||
            discriminatorElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(discriminatorElement.GetString()))
        {
            throw new System.Text.Json.JsonException("The rule position has no valid position_type discriminator.");
        }
        return discriminatorElement.GetString()!;
    }

    /// <summary>
    /// Creates an unknown position from its complete raw JSON object.
    /// </summary>
    private static UnknownRulePosition CreateUnknownPosition(string rawJson)
    {
        JsonObject payload = JsonNode.Parse(rawJson)?.AsObject()
            ?? throw new System.Text.Json.JsonException("The unknown rule position is not a JSON object.");
        int schemaVersion = ReadInt(payload[RulePositionJsonProperties.kSchemaVersion]);
        ProvisioningPositioningAlgorithm algorithm = ReadAlgorithm(
            payload[RulePositionJsonProperties.kPositioningAlgorithm]);
        return new UnknownRulePosition(
            payload[RulePositionJsonProperties.kPositionType]!.GetValue<string>(),
            schemaVersion,
            algorithm,
            payload);
    }

    /// <summary>
    /// Reads an integer when common metadata has a compatible representation.
    /// </summary>
    private static int ReadInt(JsonNode? value)
    {
        return value is JsonValue jsonValue && jsonValue.TryGetValue(out int result) ? result : 0;
    }

    /// <summary>
    /// Reads an algorithm from either its numeric or named representation.
    /// </summary>
    private static ProvisioningPositioningAlgorithm ReadAlgorithm(JsonNode? value)
    {
        if (value is JsonValue jsonValue && jsonValue.TryGetValue(out int numericValue))
        {
            return RulePositioningAlgorithmJsonParser.Parse(numericValue);
        }
        if (value is JsonValue stringValue &&
            stringValue.TryGetValue(out string? namedValue))
        {
            return RulePositioningAlgorithmJsonParser.Parse(namedValue);
        }
        return ProvisioningPositioningAlgorithm.Undefined;
    }
}

/// <summary>
/// Polymorphic rule-position converter for Newtonsoft.Json.
/// </summary>
public sealed class NewtonsoftRulePositionJsonConverter : Newtonsoft.Json.JsonConverter<RulePosition>
{
    public override bool CanWrite => false;

    /// <summary>
    /// Reads a known subtype or retains an unknown subtype's complete JSON object.
    /// </summary>
    public override RulePosition? ReadJson(
        NewtonsoftJsonReader reader,
        Type objectType,
        RulePosition? existingValue,
        bool hasExistingValue,
        NewtonsoftJsonSerializer serializer)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.Null)
        {
            return null;
        }
        JObject payload = JObject.Load(reader);
        string discriminator = payload.Value<string>(RulePositionJsonProperties.kPositionType) ?? "";
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            throw new Newtonsoft.Json.JsonSerializationException(
                "The rule position has no valid position_type discriminator.");
        }
        Type? knownType = RulePositionTypeResolver.Resolve(discriminator);
        if (knownType != null)
        {
            RulePosition position = (RulePosition?)Activator.CreateInstance(knownType, nonPublic: true)
                ?? throw new Newtonsoft.Json.JsonSerializationException(
                    "The known rule-position type could not be created.");
            using NewtonsoftJsonReader payloadReader = payload.CreateReader();
            serializer.Populate(payloadReader, position);
            return position;
        }
        return CreateUnknownPosition(payload);
    }

    /// <summary>
    /// Writes a known subtype or the retained payload of an unknown subtype.
    /// </summary>
    public override void WriteJson(
        NewtonsoftJsonWriter writer,
        RulePosition? value,
        NewtonsoftJsonSerializer serializer)
    {
        throw new NotSupportedException("Known rule positions use Newtonsoft.Json's default writer.");
    }

    /// <summary>
    /// Creates an unknown position from its complete raw JSON object.
    /// </summary>
    private static UnknownRulePosition CreateUnknownPosition(JObject payload)
    {
        string rawJson = payload.ToString(Newtonsoft.Json.Formatting.None);
        JsonObject rawPayload = JsonNode.Parse(rawJson)?.AsObject()
            ?? throw new Newtonsoft.Json.JsonSerializationException(
                "The unknown rule position is not a JSON object.");
        int schemaVersion = payload.Value<int?>(RulePositionJsonProperties.kSchemaVersion) ?? 0;
        ProvisioningPositioningAlgorithm algorithm = ParseAlgorithm(
            payload[RulePositionJsonProperties.kPositioningAlgorithm]);
        return new UnknownRulePosition(
            payload.Value<string>(RulePositionJsonProperties.kPositionType)!,
            schemaVersion,
            algorithm,
            rawPayload);
    }

    /// <summary>
    /// Reads an algorithm from either its numeric or named representation.
    /// </summary>
    private static ProvisioningPositioningAlgorithm ParseAlgorithm(JToken? value)
    {
        if (value?.Type == JTokenType.Integer)
        {
            return int.TryParse(value.ToString(), out int numericValue)
                ? RulePositioningAlgorithmJsonParser.Parse(numericValue)
                : ProvisioningPositioningAlgorithm.Undefined;
        }
        return RulePositioningAlgorithmJsonParser.Parse(value?.Value<string>());
    }
}

/// <summary>
/// Writes the retained payload of an unknown position through Newtonsoft.Json.
/// </summary>
public sealed class NewtonsoftUnknownRulePositionJsonConverter :
    Newtonsoft.Json.JsonConverter<UnknownRulePosition>
{
    public override bool CanRead => false;

    /// <summary>
    /// Direct unknown-position reads are handled through the base rule-position converter.
    /// </summary>
    public override UnknownRulePosition? ReadJson(
        NewtonsoftJsonReader reader,
        Type objectType,
        UnknownRulePosition? existingValue,
        bool hasExistingValue,
        NewtonsoftJsonSerializer serializer)
    {
        throw new NotSupportedException("Unknown rule positions are read through the base converter.");
    }

    /// <summary>
    /// Writes the complete retained payload and current common metadata.
    /// </summary>
    public override void WriteJson(
        NewtonsoftJsonWriter writer,
        UnknownRulePosition? value,
        NewtonsoftJsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        JObject.Parse(value.CreateSerializationPayload().ToJsonString()).WriteTo(writer);
    }
}
