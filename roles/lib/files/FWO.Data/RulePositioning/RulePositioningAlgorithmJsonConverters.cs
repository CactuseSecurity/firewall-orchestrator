using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using FWO.Data.DeviceSpecificSettings;
using NewtonsoftJsonReader = Newtonsoft.Json.JsonReader;
using NewtonsoftJsonSerializer = Newtonsoft.Json.JsonSerializer;
using NewtonsoftJsonWriter = Newtonsoft.Json.JsonWriter;

namespace FWO.Data.RulePositioning;

/// <summary>
/// Parses every persisted name historically used for a rule-positioning algorithm.
/// </summary>
internal static class RulePositioningAlgorithmJsonParser
{
    private static readonly Dictionary<string, ProvisioningPositioningAlgorithm> kNamedAlgorithms =
        BuildNamedAlgorithms();

    /// <summary>
    /// Parses a numeric enum representation without rejecting future values.
    /// </summary>
    internal static ProvisioningPositioningAlgorithm Parse(int value) =>
        (ProvisioningPositioningAlgorithm)value;

    /// <summary>
    /// Parses numeric strings, C# names, EnumMember values, and separator-normalized GraphQL names.
    /// </summary>
    internal static ProvisioningPositioningAlgorithm Parse(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericValue))
        {
            return Parse(numericValue);
        }

        string normalizedValue = Normalize(value);
        return kNamedAlgorithms.TryGetValue(normalizedValue, out ProvisioningPositioningAlgorithm algorithm)
            ? algorithm
            : ProvisioningPositioningAlgorithm.Undefined;
    }

    /// <summary>
    /// Builds the immutable lookup once from enum field names and explicit persisted values.
    /// </summary>
    private static Dictionary<string, ProvisioningPositioningAlgorithm> BuildNamedAlgorithms()
    {
        Dictionary<string, ProvisioningPositioningAlgorithm> algorithms =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (ProvisioningPositioningAlgorithm algorithm in
            Enum.GetValues<ProvisioningPositioningAlgorithm>())
        {
            FieldInfo field = typeof(ProvisioningPositioningAlgorithm).GetField(algorithm.ToString())!;
            algorithms[Normalize(field.Name)] = algorithm;
            string? persistedName = field.GetCustomAttribute<EnumMemberAttribute>()?.Value;
            if (!string.IsNullOrWhiteSpace(persistedName))
            {
                algorithms[Normalize(persistedName)] = algorithm;
            }
        }
        return algorithms;
    }

    /// <summary>
    /// Removes naming-policy separators before case-insensitive comparison.
    /// </summary>
    private static string Normalize(string? value) =>
        string.Concat((value ?? "").Where(char.IsLetterOrDigit));
}

/// <summary>
/// Writes numeric rule-positioning algorithms and accepts legacy names through System.Text.Json.
/// </summary>
public sealed class SystemTextProvisioningPositioningAlgorithmJsonConverter :
    System.Text.Json.Serialization.JsonConverter<ProvisioningPositioningAlgorithm>
{
    /// <summary>
    /// Reads a numeric or named algorithm, returning Undefined for an unrecognized representation.
    /// </summary>
    public override ProvisioningPositioningAlgorithm Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int numericValue))
        {
            return RulePositioningAlgorithmJsonParser.Parse(numericValue);
        }
        return reader.TokenType == JsonTokenType.String
            ? RulePositioningAlgorithmJsonParser.Parse(reader.GetString())
            : ProvisioningPositioningAlgorithm.Undefined;
    }

    /// <summary>
    /// Writes the stable numeric enum value independently of the surrounding naming policy.
    /// </summary>
    public override void Write(
        Utf8JsonWriter writer,
        ProvisioningPositioningAlgorithm value,
        JsonSerializerOptions options) => writer.WriteNumberValue((int)value);
}

/// <summary>
/// Writes numeric rule-positioning algorithms and accepts legacy names through Newtonsoft.Json.
/// </summary>
public sealed class NewtonsoftProvisioningPositioningAlgorithmJsonConverter :
    Newtonsoft.Json.JsonConverter<ProvisioningPositioningAlgorithm>
{
    /// <summary>
    /// Reads a numeric or named algorithm, returning Undefined for an unrecognized representation.
    /// </summary>
    public override ProvisioningPositioningAlgorithm ReadJson(
        NewtonsoftJsonReader reader,
        Type objectType,
        ProvisioningPositioningAlgorithm existingValue,
        bool hasExistingValue,
        NewtonsoftJsonSerializer serializer)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.Integer &&
            reader.Value is IConvertible numericValue)
        {
            try
            {
                return RulePositioningAlgorithmJsonParser.Parse(
                    numericValue.ToInt32(CultureInfo.InvariantCulture));
            }
            catch (OverflowException)
            {
                return ProvisioningPositioningAlgorithm.Undefined;
            }
        }
        return reader.TokenType == Newtonsoft.Json.JsonToken.String
            ? RulePositioningAlgorithmJsonParser.Parse(reader.Value?.ToString())
            : ProvisioningPositioningAlgorithm.Undefined;
    }

    /// <summary>
    /// Writes the stable numeric enum value independently of the surrounding naming policy.
    /// </summary>
    public override void WriteJson(
        NewtonsoftJsonWriter writer,
        ProvisioningPositioningAlgorithm value,
        NewtonsoftJsonSerializer serializer) => writer.WriteValue((int)value);
}
