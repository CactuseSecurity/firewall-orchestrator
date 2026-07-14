using System.Collections.Generic;
using FWO.Data;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class OwnerAdditionalInfoJsonTest
    {
        [Test]
        public void Serialize_ReturnsIndentedJson_ForExistingData()
        {
            Dictionary<string, string> additionalInfo = new()
            {
                ["cost_center"] = "4711",
                ["owner_type"] = "shared"
            };

            string serialized = OwnerAdditionalInfoJson.Serialize(additionalInfo);

            Assert.That(serialized, Does.Contain("\n"));
            Assert.That(serialized, Does.Contain("\"cost_center\": \"4711\""));
            Assert.That(serialized, Does.Contain("\"owner_type\": \"shared\""));
        }

        [Test]
        public void TryDeserialize_ReturnsDictionary_ForValidJson()
        {
            bool parsed = OwnerAdditionalInfoJson.TryDeserialize(
                "{\n  \"cost_center\": \"4711\",\n  \"owner_type\": \"shared\"\n}",
                out Dictionary<string, string>? additionalInfo);

            Assert.That(parsed, Is.True);
            Assert.That(additionalInfo, Is.Not.Null);
            Assert.That(additionalInfo?["cost_center"], Is.EqualTo("4711"));
            Assert.That(additionalInfo?["owner_type"], Is.EqualTo("shared"));
        }

        [Test]
        public void TryDeserialize_ReturnsNull_ForEmptyInput()
        {
            bool parsed = OwnerAdditionalInfoJson.TryDeserialize("   ", out Dictionary<string, string>? additionalInfo);

            Assert.That(parsed, Is.True);
            Assert.That(additionalInfo, Is.Null);
        }

        [Test]
        public void TryDeserialize_ReturnsFalse_ForInvalidJson()
        {
            bool parsed = OwnerAdditionalInfoJson.TryDeserialize("{ invalid json }", out Dictionary<string, string>? additionalInfo);

            Assert.That(parsed, Is.False);
            Assert.That(additionalInfo, Is.Null);
        }
    }
}
