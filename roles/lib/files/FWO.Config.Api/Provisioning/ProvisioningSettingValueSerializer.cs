using FWO.Data.Provisioning;
using Newtonsoft.Json.Linq;

namespace FWO.Config.Api.Provisioning;

/// <summary>
/// Converts typed provisioning values to and from their JSONB representation.
/// Enum values are deliberately represented by their names rather than ordinals.
/// </summary>
public sealed class ProvisioningSettingValueSerializer
{
    public JToken Serialize<TValue>(ProvisioningSettingKey<TValue> key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return Serialize(key, (object)value);
    }

    public JToken Serialize(ProvisioningSettingKey key, object value)
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
            if (!Enum.IsDefined(key.ValueType, value))
            {
                throw new ArgumentException(
                    $"Value '{value}' is not defined by enum '{key.ValueType.Name}'.",
                    nameof(value));
            }

            return new JValue(value.ToString());
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

        throw new NotSupportedException(
            $"Provisioning setting type '{key.ValueType.Name}' has no JSONB serializer.");
    }

    public TValue Deserialize<TValue>(ProvisioningSettingKey<TValue> key, JToken value)
    {
        object deserialized = Deserialize((ProvisioningSettingKey)key, value);
        return (TValue)deserialized;
    }

    public object Deserialize(ProvisioningSettingKey key, JToken value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Type is JTokenType.Null or JTokenType.Undefined)
        {
            throw InvalidValue(key, value, "null is not supported");
        }

        if (key.ValueType.IsEnum)
        {
            if (value.Type != JTokenType.String)
            {
                throw InvalidValue(key, value, "enum values must be JSON strings");
            }

            string enumName = value.Value<string>() ?? "";
            if (!Enum.TryParse(key.ValueType, enumName, ignoreCase: false, out object? parsed)
                || !Enum.IsDefined(key.ValueType, parsed))
            {
                throw InvalidValue(key, value, $"'{enumName}' is not a defined {key.ValueType.Name} value");
            }

            return parsed;
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

        throw new NotSupportedException(
            $"Provisioning setting type '{key.ValueType.Name}' has no JSONB deserializer.");
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
