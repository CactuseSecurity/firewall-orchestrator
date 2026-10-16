using FWO.Data.DeviceSpecificSettings;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api.DeviceSpecificSettings;

/// <summary>
/// Converts typed device specific setting values to and from their JSONB representation.
/// Enum values use the documented wire names rather than ordinals. The <c>Undefined</c> member of the
/// setting enums is never stored: it has no meaning as a setting value, and inheriting a value is expressed by storing no
/// override at all.
/// </summary>
public static class DeviceSpecificSettingValueSerializer
{
    /// <summary>Name of the placeholder member every device specific setting enum starts with.</summary>
    public const string kUndefinedEnumName = nameof(ProvisioningLoggingMode.Undefined);

    /// <summary>Converts a typed setting value to its JSONB representation.</summary>
    public static JToken Serialize<TValue>(DeviceSpecificSettingKey<TValue> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return Serialize(key, (object)value);
    }

    /// <summary>Converts a setting value to its JSONB representation after checking it against the key's value type.</summary>
    public static JToken Serialize(DeviceSpecificSettingKey key, object value)
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

        if (key.ValueType == typeof(bool))
        {
            return new JValue((bool)value);
        }

        if (key.ValueType == typeof(int?))
        {
            int id = (int)value;
            if (id <= 0)
            {
                throw new ArgumentException($"Setting '{key.DatabaseKey}' requires a positive ID.", nameof(value));
            }
            return new JValue(id);
        }

        throw new NotSupportedException(
            $"Device specific setting type '{key.ValueType.Name}' has no JSONB serializer.");
    }

    /// <summary>Converts a stored JSONB value back to the typed setting value.</summary>
    public static TValue Deserialize<TValue>(DeviceSpecificSettingKey<TValue> key, JToken value)
    {
        object deserialized = Deserialize((DeviceSpecificSettingKey)key, value);
        return (TValue)deserialized;
    }

    /// <summary>
    /// Converts a stored JSONB value back to a value of the key's type. Throws when the stored value does not match that type.
    /// </summary>
    public static object Deserialize(DeviceSpecificSettingKey key, JToken value)
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
            return DeserializeString(key, value);
        }

        if (key.ValueType == typeof(List<string>))
        {
            return DeserializeStringList(key, value);
        }

        if (key.ValueType == typeof(bool))
        {
            return DeserializeBool(key, value);
        }

        if (key.ValueType == typeof(int?))
        {
            return DeserializePositiveId(key, value);
        }

        throw new NotSupportedException(
            $"Device specific setting type '{key.ValueType.Name}' has no JSONB deserializer.");
    }

    /// <summary>Reads a JSON string value.</summary>
    private static string DeserializeString(DeviceSpecificSettingKey key, JToken value)
    {
        if (value.Type != JTokenType.String)
        {
            throw InvalidValue(key, value, "a JSON string is required");
        }

        return value.Value<string>()!;
    }

    /// <summary>Reads a JSON array that contains only strings.</summary>
    private static List<string> DeserializeStringList(DeviceSpecificSettingKey key, JToken value)
    {
        if (value is not JArray array || array.Any(item => item.Type != JTokenType.String))
        {
            throw InvalidValue(key, value, "an array containing only JSON strings is required");
        }

        return array.Select(item => item.Value<string>()!).ToList();
    }

    /// <summary>Reads a JSON boolean value.</summary>
    private static bool DeserializeBool(DeviceSpecificSettingKey key, JToken value)
    {
        return value.Type == JTokenType.Boolean
            ? value.Value<bool>()
            : throw InvalidValue(key, value, "a JSON boolean is required");
    }

    /// <summary>Reads a positive JSON integer that fits into an <see cref="int"/>.</summary>
    private static int DeserializePositiveId(DeviceSpecificSettingKey key, JToken value)
    {
        return value.Type == JTokenType.Integer && value.Value<long>() is > 0 and <= int.MaxValue
            ? value.Value<int>()
            : throw InvalidValue(key, value, "a positive JSON integer is required");
    }

    /// <summary>Reads a storable, allowed enum value from its JSON string storage name.</summary>
    private static object DeserializeEnum(DeviceSpecificSettingKey key, JToken value)
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

    /// <summary>Uses the label-specific lowercase manager names from the provisioning contract.</summary>
    public static string StorageName(DeviceSpecificSettingKey key, object value) =>
        key == DeviceSpecificSettingKeys.LabelCreation
            ? EnumStorageName(key.ValueType, value).ToLowerInvariant()
            : EnumStorageName(key.ValueType, value);

    /// <summary>Returns the external setting value, independent of C# enum member naming.</summary>
    public static string EnumStorageName(Type enumType, object value) =>
        enumType.GetField(value.ToString()!)?.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? value.ToString()!;

    /// <summary>Ignore is only supported by time objects and labels.</summary>
    private static bool IsAllowedEnumValue(DeviceSpecificSettingKey key, object value) =>
        value is not ProvisioningObjectCreationMode.Ignore
        || key == DeviceSpecificSettingKeys.TimeObjectCreation || key == DeviceSpecificSettingKeys.LabelCreation;

    /// <summary>True for a defined enum member other than the <c>Undefined</c> placeholder.</summary>
    private static bool IsStorableEnumValue(Type enumType, object value)
    {
        return Enum.IsDefined(enumType, value)
            && !string.Equals(Enum.GetName(enumType, value), kUndefinedEnumName, StringComparison.Ordinal);
    }

    private static InvalidOperationException InvalidValue(
        DeviceSpecificSettingKey key,
        JToken value,
        string reason)
    {
        return new InvalidOperationException(
            $"Stored value for device specific setting '{key.DatabaseKey}' is invalid: {reason}. JSON: {value.ToString(Newtonsoft.Json.Formatting.None)}");
    }
}
