using FWO.Config.Api.Provisioning;
using FWO.Data.Provisioning;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
[Parallelizable]
internal class ProvisioningSettingValueSerializerTest
{
    private static IEnumerable<TestCaseData> RoundTripCases()
    {
        foreach (ProvisioningSettingKey key in ProvisioningSettingKeys.All)
        {
            if (key.ValueType.IsEnum)
            {
                foreach (object value in StorableEnumValues(key.ValueType))
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
            else if (key.ValueType == typeof(List<string>))
            {
                yield return new TestCaseData(key, new List<string> { "Strict", "ScanAll" })
                    .SetName($"RoundTrip_{key.DatabaseKey}_StringList");
            }
        }
    }

    private static IEnumerable<TestCaseData> EnumKeyCases()
    {
        return ProvisioningSettingKeys.All
            .Where(key => key.ValueType.IsEnum)
            .Select(key => new TestCaseData(key).SetName($"Undefined_{key.DatabaseKey}"));
    }

    private static IEnumerable<object> StorableEnumValues(Type enumType)
    {
        return Enum.GetValues(enumType)
            .Cast<object>()
            .Where(value => Enum.GetName(enumType, value) != ProvisioningSettingValueSerializer.kUndefinedEnumName);
    }

    [TestCaseSource(nameof(RoundTripCases))]
    public void SerializeAndDeserialize_RoundTripsEverySupportedValue(
        ProvisioningSettingKey key,
        object originalValue)
    {
        JToken serialized = ProvisioningSettingValueSerializer.Serialize(key, originalValue);
        object roundTripped = ProvisioningSettingValueSerializer.Deserialize(key, serialized);

        if (key.ValueType == typeof(List<string>))
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
            Assert.That(serialized.Value<string>(), Is.EqualTo(originalValue.ToString()));
        }
    }

    [TestCaseSource(nameof(EnumKeyCases))]
    public void Serialize_RejectsUndefined(ProvisioningSettingKey key)
    {
        object undefined = Enum.Parse(key.ValueType, ProvisioningSettingValueSerializer.kUndefinedEnumName);

        Assert.Throws<ArgumentException>(() => ProvisioningSettingValueSerializer.Serialize(key, undefined));
    }

    [TestCaseSource(nameof(EnumKeyCases))]
    public void Deserialize_RejectsStoredUndefined(ProvisioningSettingKey key)
    {
        JToken stored = new JValue(ProvisioningSettingValueSerializer.kUndefinedEnumName);

        Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(key, stored));
    }

    [Test]
    public void Serialize_RejectsUndefinedThroughTypedKey()
    {
        Assert.Throws<ArgumentException>(() => ProvisioningSettingValueSerializer.Serialize(
            ProvisioningSettingKeys.Logging, ProvisioningLoggingMode.Undefined));
    }
}
