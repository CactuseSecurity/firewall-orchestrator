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
                foreach (object value in Enum.GetValues(key.ValueType))
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

    [TestCaseSource(nameof(RoundTripCases))]
    public void SerializeAndDeserialize_RoundTripsEverySupportedValue(
        ProvisioningSettingKey key,
        object originalValue)
    {
        ProvisioningSettingValueSerializer serializer = new();

        JToken serialized = serializer.Serialize(key, originalValue);
        object roundTripped = serializer.Deserialize(key, serialized);

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
}
