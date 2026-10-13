using FWO.Data.Provisioning;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api.Provisioning;

/// <summary>
/// Converts typed provisioning values to and from their JSONB representation.
/// Enum values use the documented wire names rather than ordinals. The <c>Undefined</c> member of the
/// setting enums is never stored: it has no meaning for provisioning, and inheriting a value is expressed by storing no
/// override at all.
/// </summary>
public static class ProvisioningSettingValueSerializer
{
    /// <summary>Name of the placeholder member every provisioning setting enum starts with.</summary>
    public const string kUndefinedEnumName = nameof(ProvisioningLoggingMode.Undefined);

    /// <summary>Converts a typed setting value to its JSONB representation.</summary>
    public static JToken Serialize<TValue>(ProvisioningSettingKey<TValue> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return Serialize(key, (object)value);
    }

    /// <summary>Converts a setting value to its JSONB representation after checking it against the key's value type.</summary>
    public static JToken Serialize(ProvisioningSettingKey key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (!key.ValueType.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"Setting '{key.DatabaseKey}' requires a value of type '{key.ValueType.Name}', not '{value.GetType().Name}'.",
                nameof(value));
        }

        if (key.ValueType.IsEnum)
        {
            if (!IsStorableEnumValue(key.ValueType, value) || !IsAllowedEnumValue(key, value))
            {
                throw new ArgumentException(
                    $"Value '{value}' is not a storable value of enum '{key.ValueType.Name}'.",
                    nameof(value));
            }

            return new JValue(StorageName(key, value));
        }

        if (key.ValueType == typeof(string))
        {
            return new JValue((string)value);
        }

        if (key.ValueType == typeof(List<string>))
        {
            List<string> values = (List<string>)value;
            if (values.Any(item => item is null))
            {
                throw new ArgumentException(
                    $"Setting '{key.DatabaseKey}' cannot contain null list entries.",
                    nameof(value));
            }

            return new JArray(values);
        }

        if (key.ValueType == typeof(Dictionary<string, string>))
        {
            Dictionary<string, string> templates = (Dictionary<string, string>)value;
            if (!ValidNamingConventions(templates))
            {
                throw new ArgumentException("Naming conventions require one non-empty string for each supported object type.", nameof(value));
            }
            return JObject.FromObject(templates);
        }

        throw new NotSupportedException(
            $"Provisioning setting type '{key.ValueType.Name}' has no JSONB serializer.");
    }

    /// <summary>Converts a stored JSONB value back to the typed setting value.</summary>
    public static TValue Deserialize<TValue>(ProvisioningSettingKey<TValue> key, JToken value)
    {
        object deserialized = Deserialize((ProvisioningSettingKey)key, value);
        return (TValue)deserialized;
    }

    /// <summary>
    /// Converts a stored JSONB value back to a value of the key's type. Throws when the stored value does not match that type.
    /// </summary>
    public static object Deserialize(ProvisioningSettingKey key, JToken value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Type is JTokenType.Null or JTokenType.Undefined)
        {
            throw InvalidValue(key, value, "null is not supported");
        }

        if (key.ValueType.IsEnum)
        {
            return DeserializeEnum(key, value);
        }

        if (key.ValueType == typeof(string))
        {
            if (value.Type != JTokenType.String)
            {
                throw InvalidValue(key, value, "a JSON string is required");
            }

            return value.Value<string>()!;
        }

        if (key.ValueType == typeof(List<string>))
        {
            if (value is not JArray array || array.Any(item => item.Type != JTokenType.String))
            {
                throw InvalidValue(key, value, "an array containing only JSON strings is required");
            }

            return array.Select(item => item.Value<string>()!).ToList();
        }

        if (key.ValueType == typeof(Dictionary<string, string>))
        {
            return DeserializeNamingConventions(key, value);
        }

        throw new NotSupportedException(
            $"Provisioning setting type '{key.ValueType.Name}' has no JSONB deserializer.");
    }

    /// <summary>Reads a storable, allowed enum value from its JSON string storage name.</summary>
    private static object DeserializeEnum(ProvisioningSettingKey key, JToken value)
    {
        if (value.Type != JTokenType.String)
        {
            throw InvalidValue(key, value, "enum values must be JSON strings");
        }

        string enumName = value.Value<string>() ?? "";
        object? parsed = Enum.GetValues(key.ValueType).Cast<object>()
            .FirstOrDefault(candidate => IsStorableEnumValue(key.ValueType, candidate) && IsAllowedEnumValue(key, candidate)
                && StorageName(key, candidate) == enumName);
        if (parsed is null)
        {
            throw InvalidValue(key, value, $"'{enumName}' is not a storable {key.ValueType.Name} value");
        }

        return parsed;
    }

    /// <summary>Validates and reads the complete naming JSON object.</summary>
    private static Dictionary<string, string> DeserializeNamingConventions(ProvisioningSettingKey key, JToken value)
    {
        if (value is not JObject templates || templates.Properties().Any(property => property.Value.Type != JTokenType.String))
        {
            throw InvalidValue(key, value, "a JSON object containing string naming conventions is required");
        }
        Dictionary<string, string> conventions = templates.ToObject<Dictionary<string, string>>()!;
        if (!ValidNamingConventions(conventions))
        {
            throw InvalidValue(key, value, "one non-empty string for each supported object type is required");
        }
        return conventions;
    }

    /// <summary>Uses the label-specific lowercase manager names from the provisioning contract.</summary>
    public static string StorageName(ProvisioningSettingKey key, object value) =>
        key == ProvisioningSettingKeys.LabelCreation
            ? EnumStorageName(key.ValueType, value).ToLowerInvariant()
            : EnumStorageName(key.ValueType, value);

    /// <summary>Returns the external setting value, independent of C# enum member naming.</summary>
    public static string EnumStorageName(Type enumType, object value) =>
        enumType.GetField(value.ToString()!)?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? value.ToString()!;

    /// <summary>Checks the complete modelling naming convention contract.</summary>
    private static bool ValidNamingConventions(Dictionary<string, string> templates) =>
        templates.Count == ProvisioningModellingNamingConventions.ObjectTypes.Count
        && ProvisioningModellingNamingConventions.ObjectTypes.All(key =>
            templates.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value));

    /// <summary>Ignore is only supported by time objects and labels.</summary>
    private static bool IsAllowedEnumValue(ProvisioningSettingKey key, object value) =>
        value is not ProvisioningObjectCreationMode.Ignore
        || key == ProvisioningSettingKeys.TimeObjectCreation || key == ProvisioningSettingKeys.LabelCreation;

    /// <summary>True for a defined enum member other than the <c>Undefined</c> placeholder.</summary>
    private static bool IsStorableEnumValue(Type enumType, object value)
    {
        return Enum.IsDefined(enumType, value)
            && !string.Equals(Enum.GetName(enumType, value), kUndefinedEnumName, StringComparison.Ordinal);
    }

    private static InvalidOperationException InvalidValue(
        ProvisioningSettingKey key,
        JToken value,
        string reason)
    {
        return new InvalidOperationException(
            $"Stored value for provisioning setting '{key.DatabaseKey}' is invalid: {reason}. JSON: {value.ToString(Newtonsoft.Json.Formatting.None)}");
    }
}
