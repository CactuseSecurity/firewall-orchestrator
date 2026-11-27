using FWO.Data.DeviceSpecificSettings;
using FWO.Services.DeviceSpecificSettings;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Covers the value rules shared by the device specific settings page and the import.</summary>
[TestFixture]
internal class DeviceSpecificSettingValueRulesTest
{
    [Test]
    public void NumberSettingsHaveTheirMinimum()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DeviceSpecificSettingValueRules.MinimumNumber(DeviceSpecificSettingKeys.RequestResponseTimeout), Is.EqualTo(1));
            Assert.That(DeviceSpecificSettingValueRules.MinimumNumber(DeviceSpecificSettingKeys.RequestMaxAttempts), Is.EqualTo(1));
            Assert.That(DeviceSpecificSettingValueRules.MinimumNumber(DeviceSpecificSettingKeys.RequestCyclesBetweenAttempts), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => DeviceSpecificSettingValueRules.Normalize(DeviceSpecificSettingKeys.RequestMaxAttempts, 0));
            Assert.That(DeviceSpecificSettingValueRules.Normalize(DeviceSpecificSettingKeys.RequestCyclesBetweenAttempts, 0), Is.Zero);
        });
    }

    [Test]
    public void EndpointsAreNormalizedAndAnEmptyEndpointIsKept()
    {
        DeviceSpecificSettingKey<string> key = FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate];

        Assert.Multiple(() =>
        {
            Assert.That(DeviceSpecificSettingValueRules.Normalize(key, " post /add-host "), Is.EqualTo("POST /add-host"));
            Assert.That(DeviceSpecificSettingValueRules.Normalize(key, ""), Is.EqualTo(""));
            Assert.Throws<ArgumentException>(() => DeviceSpecificSettingValueRules.Normalize(key, "FETCH /x"));
        });
    }

    [Test]
    public void AnInvalidSectionNameTemplateNamesItsKey()
    {
        ArgumentException? exception = Assert.Throws<ArgumentException>(() =>
            DeviceSpecificSettingValueRules.Normalize(DeviceSpecificSettingKeys.CheckPointLayerGuardSectionNameTemplate, "@@BROKEN"));

        Assert.That(exception!.ParamName, Is.EqualTo(DeviceSpecificSettingKeys.CheckPointLayerGuardSectionNameTemplate.DatabaseKey));
    }

    [Test]
    public void OtherValuesAreReturnedUnchanged()
    {
        Assert.That(DeviceSpecificSettingValueRules.Normalize(DeviceSpecificSettingKeys.ScUrl, " https://sc "), Is.EqualTo(" https://sc "));
    }
}
