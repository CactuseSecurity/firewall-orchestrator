using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
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
    public override RulePosition? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;
        if (!TryGetDiscriminator(root, out string discriminator))
        {
            Trace.TraceWarning("A stored rule position had no valid discriminator and was ignored.");
            return null;
        }
        Type? knownType = RulePositionTypeResolver.Resolve(discriminator);
        if (knownType != null)
        {
            try
            {
                return (RulePosition?)SystemTextJsonSerializer.Deserialize(root.GetRawText(), knownType, options)
                    ?? CreateUnknownPosition(root.GetRawText());
            }
            catch (Exception exception) when (IsInvalidStoredPosition(exception))
            {
                Trace.TraceWarning("A stored known rule position was invalid and was retained as unknown.");
                return CreateUnknownPosition(root.GetRawText());
            }
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
    private static bool TryGetDiscriminator(JsonElement root, out string discriminator)
    {
        discriminator = "";
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(RulePositionJsonProperties.kPositionType, out JsonElement discriminatorElement) ||
            discriminatorElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(discriminatorElement.GetString()))
        {
            return false;
        }
        discriminator = discriminatorElement.GetString()!;
        return true;
    }

    /// <summary>
    /// Identifies failures caused by invalid persisted values rather than process-fatal conditions.
    /// </summary>
    private static bool IsInvalidStoredPosition(Exception exception) =>
        exception is System.Text.Json.JsonException or ArgumentException or InvalidOperationException or NotSupportedException ||
        exception is System.Reflection.TargetInvocationException { InnerException: not null } invocationException &&
        IsInvalidStoredPosition(invocationException.InnerException!);

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

        try
        {
            JToken storedValue = JToken.Load(reader);
            if (storedValue is not JObject payload)
            {
                Trace.TraceWarning("A stored rule position was not a JSON object and was ignored.");
                return null;
            }
            return ReadObject(payload, serializer);
        }
        catch (Exception exception) when (IsInvalidStoredPosition(exception))
        {
            Trace.TraceWarning("A stored rule position was invalid and was ignored.");
            return null;
        }
    }

    /// <summary>
    /// Reads one stored object after the outer JSON shape has been validated.
    /// </summary>
    private static RulePosition? ReadObject(JObject payload, NewtonsoftJsonSerializer serializer)
    {
        string discriminator = payload.Value<string>(RulePositionJsonProperties.kPositionType) ?? "";
        if (string.IsNullOrWhiteSpace(discriminator))
        {
            Trace.TraceWarning("A stored rule position had no valid discriminator and was ignored.");
            return null;
        }
        Type? knownType = RulePositionTypeResolver.Resolve(discriminator);
        if (knownType != null)
        {
            try
            {
                RulePosition position = (RulePosition?)Activator.CreateInstance(knownType, nonPublic: true)
                    ?? throw new Newtonsoft.Json.JsonSerializationException(
                        "The known rule-position type could not be created.");
                using NewtonsoftJsonReader payloadReader = payload.CreateReader();
                serializer.Populate(payloadReader, position);
                return position;
            }
            catch (Exception exception) when (IsInvalidStoredPosition(exception))
            {
                Trace.TraceWarning("A stored known rule position was invalid and was retained as unknown.");
                return CreateUnknownPosition(payload);
            }
        }
        return CreateUnknownPosition(payload);
    }

    /// <summary>
    /// Identifies failures caused by invalid persisted values rather than process-fatal conditions.
    /// </summary>
    private static bool IsInvalidStoredPosition(Exception exception) =>
        exception is Newtonsoft.Json.JsonException or ArgumentException or InvalidOperationException or NotSupportedException ||
        exception is System.Reflection.TargetInvocationException { InnerException: not null } invocationException &&
        IsInvalidStoredPosition(invocationException.InnerException!);

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
        int schemaVersion = ReadInt(payload[RulePositionJsonProperties.kSchemaVersion]);
        ProvisioningPositioningAlgorithm algorithm = ParseAlgorithm(
            payload[RulePositionJsonProperties.kPositioningAlgorithm]);
        return new UnknownRulePosition(
            payload.Value<string>(RulePositionJsonProperties.kPositionType)!,
            schemaVersion,
            algorithm,
            rawPayload);
    }

    /// <summary>
    /// Reads compatible integer metadata without rejecting the retained object.
    /// </summary>
    private static int ReadInt(JToken? value)
    {
        return value?.Type is JTokenType.Integer or JTokenType.String &&
            int.TryParse(value.ToString(), out int result)
                ? result
                : 0;
    }

    /// <summary>
    /// Reads an algorithm from either its numeric or named representation.
    /// </summary>
    private static ProvisioningPositioningAlgorithm ParseAlgorithm(JToken? value)
    {
        try
        {
            if (value?.Type == JTokenType.Integer)
            {
                return int.TryParse(value.ToString(), out int numericValue)
                    ? RulePositioningAlgorithmJsonParser.Parse(numericValue)
                    : ProvisioningPositioningAlgorithm.Undefined;
            }
            return value?.Type == JTokenType.String
                ? RulePositioningAlgorithmJsonParser.Parse(value.Value<string>())
                : ProvisioningPositioningAlgorithm.Undefined;
        }
        catch (Exception exception) when (IsInvalidStoredPosition(exception))
        {
            return ProvisioningPositioningAlgorithm.Undefined;
        }
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
