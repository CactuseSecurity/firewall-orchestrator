using System.Text.Json;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Covers reading and writing device specific settings packages and the value conversions between the JSON libraries.</summary>
[TestFixture]
internal class DeviceSpecificSettingsPackageJsonTest
{
    private const string kDateLikeText = "2026-11-21T10:00:00";

    [Test]
    public void APackageSurvivesSerializationAndStorage()
    {
        DeviceSpecificSettingsPackage package = new()
        {
            FwoVersion = "9.9.4",
            ExportedAt = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero),
            Nodes =
            [
                new DeviceSpecificSettingsPackageNode
                {
                    Level = "management",
                    ManagementName = "m",
                    Values = new Dictionary<string, JsonElement> { ["scUrl"] = JsonDocument.Parse("\"https://sc\"").RootElement.Clone() }
                }
            ]
        };

        string json = DeviceSpecificSettingsPackageJson.Serialize(package);
        DeviceSpecificSettingsPackage read = DeviceSpecificSettingsPackageJson.Deserialize(json);
        DeviceSpecificSettingsPackage stored = DeviceSpecificSettingsPackageJson.FromStorage(DeviceSpecificSettingsPackageJson.ToStorage(package));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"management_name\": \"m\"").And.Not.Contains("gateway_name"), "unset name fields are left out");
            Assert.That(read.Nodes.Single().Values["scUrl"].GetString(), Is.EqualTo("https://sc"));
            Assert.That(stored.FwoVersion, Is.EqualTo("9.9.4"));
            Assert.That(stored.ExportedAt, Is.EqualTo(package.ExportedAt));
            Assert.That(stored.Nodes.Single().ManagementName, Is.EqualTo("m"));
        });
    }

    [TestCase("{\"format\":\"fwo-device-specific-settings\",\"version\":1,\"nodes\":[],\"unknown\":1}")]
    [TestCase("{\"nodes\":[{\"level\":\"global\",\"manangement_name\":\"m\",\"values\":{}}]}")]
    [TestCase("not json")]
    [TestCase("null")]
    public void InvalidPackagesAreRejected(string json)
    {
        Assert.Throws<InvalidDataException>(() => DeviceSpecificSettingsPackageJson.Deserialize(json));
    }

    [Test]
    public void APackageLargerThanTheLimitIsRejected()
    {
        string json = new(' ', (int)DeviceSpecificSettingsPackage.kMaxPackageBytes + 1);

        InvalidDataException? exception = Assert.Throws<InvalidDataException>(() => DeviceSpecificSettingsPackageJson.Deserialize(json));

        Assert.That(exception!.Message, Does.Contain(DeviceSpecificSettingsPackage.kMaxPackageBytes.ToString()));
    }

    [Test]
    public void ValueConversionKeepsStringsThatLookLikeDates()
    {
        JsonElement element = JsonDocument.Parse($"\"{kDateLikeText}\"").RootElement.Clone();

        JToken token = DeviceSpecificSettingsPackageJson.ToJToken(element);
        JsonElement back = DeviceSpecificSettingsPackageJson.ToJsonElement(token);

        Assert.Multiple(() =>
        {
            Assert.That(token.Type, Is.EqualTo(JTokenType.String));
            Assert.That(token.Value<string>(), Is.EqualTo(kDateLikeText));
            Assert.That(back.GetString(), Is.EqualTo(kDateLikeText));
        });
    }
}
