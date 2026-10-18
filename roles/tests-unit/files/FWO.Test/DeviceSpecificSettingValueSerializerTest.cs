using FWO.Config.Api.DeviceSpecificSettings;
using FWO.Data.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[Parallelizable]
internal class DeviceSpecificSettingValueSerializerTest
{
    private static IEnumerable<TestCaseData> RoundTripCases()
    {
        foreach (DeviceSpecificSettingKey key in DeviceSpecificSettingKeys.All)
        {
            if (key.ValueType.IsEnum)
            {
                foreach (object value in StorableEnumValues(key.ValueType).Where(value =>
                    value is not ProvisioningObjectCreationMode.Ignore
                    || key == DeviceSpecificSettingKeys.TimeObjectCreation || key == DeviceSpecificSettingKeys.LabelCreation))
                {
                    yield return new TestCaseData(key, value)
                        .SetName($"RoundTrip_{key.DatabaseKey}_{value}");
                }
            }
            else if (key.ValueType == typeof(string))
            {
                yield return new TestCaseData(key, $"{key.DatabaseKey}-value")
                    .SetName($"RoundTrip_{key.DatabaseKey}_String");
            }
            else if (key.ValueType == typeof(bool))
            {
                yield return new TestCaseData(key, true).SetName($"RoundTrip_{key.DatabaseKey}_True");
                yield return new TestCaseData(key, false).SetName($"RoundTrip_{key.DatabaseKey}_False");
            }
            else if (key.ValueType == typeof(int?))
            {
                yield return new TestCaseData(key, 7).SetName($"RoundTrip_{key.DatabaseKey}_Id");
            }
            else if (key.ValueType == typeof(List<string>))
            {
                yield return new TestCaseData(key, new List<string> { "Strict", "ScanAll" })
                    .SetName($"RoundTrip_{key.DatabaseKey}_StringList");
            }
        }
    }

    private static IEnumerable<TestCaseData> EnumKeyCases()
    {
        return DeviceSpecificSettingKeys.All
            .Where(key => key.ValueType.IsEnum)
            .Select(key => new TestCaseData(key).SetName($"Undefined_{key.DatabaseKey}"));
    }

    private static IEnumerable<object> StorableEnumValues(Type enumType)
    {
        return Enum.GetValues(enumType)
            .Cast<object>()
            .Where(value => Enum.GetName(enumType, value) != DeviceSpecificSettingValueSerializer.kUndefinedEnumName);
    }

    [TestCaseSource(nameof(RoundTripCases))]
    public void SerializeAndDeserialize_RoundTripsEverySupportedValue(
        DeviceSpecificSettingKey key,
        object originalValue)
    {
        JToken serialized = DeviceSpecificSettingValueSerializer.Serialize(key, originalValue);
        object roundTripped = DeviceSpecificSettingValueSerializer.Deserialize(key, serialized);

        if (key.ValueType == typeof(bool) || key.ValueType == typeof(int?))
        {
            Assert.That(serialized.Type, Is.EqualTo(key.ValueType == typeof(bool) ? JTokenType.Boolean : JTokenType.Integer));
            Assert.That(roundTripped, Is.EqualTo(originalValue));
        }
        else if (key.ValueType == typeof(List<string>))
        {
            Assert.That(serialized.Type, Is.EqualTo(JTokenType.Array));
            Assert.That((List<string>)roundTripped, Is.EqualTo((List<string>)originalValue));
        }
        else
        {
            Assert.That(serialized.Type, Is.EqualTo(JTokenType.String));
            Assert.That(roundTripped, Is.EqualTo(originalValue));
        }

        if (key.ValueType.IsEnum)
        {
            Assert.That(serialized.Value<string>(), Is.EqualTo(DeviceSpecificSettingValueSerializer.StorageName(key, originalValue)));
        }
    }

    [TestCaseSource(nameof(EnumKeyCases))]
    public void Serialize_RejectsUndefined(DeviceSpecificSettingKey key)
    {
        object undefined = Enum.Parse(key.ValueType, DeviceSpecificSettingValueSerializer.kUndefinedEnumName);

        Assert.Throws<ArgumentException>(() => DeviceSpecificSettingValueSerializer.Serialize(key, undefined));
    }

    [TestCaseSource(nameof(EnumKeyCases))]
    public void Deserialize_RejectsStoredUndefined(DeviceSpecificSettingKey key)
    {
        JToken stored = new JValue(DeviceSpecificSettingValueSerializer.kUndefinedEnumName);

        Assert.Throws<InvalidOperationException>(() => DeviceSpecificSettingValueSerializer.Deserialize(key, stored));
    }

    [Test]
    public void Serialize_RejectsUndefinedThroughTypedKey()
    {
        Assert.Throws<ArgumentException>(() => DeviceSpecificSettingValueSerializer.Serialize(
            DeviceSpecificSettingKeys.RuleLoggingOverride, ProvisioningLoggingMode.Undefined));
    }
}
