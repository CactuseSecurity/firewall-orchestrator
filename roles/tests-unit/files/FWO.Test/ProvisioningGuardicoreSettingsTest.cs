using FWO.Config.Api;
using FWO.Config.Api.Provisioning;
using FWO.Data;
using FWO.Data.Provisioning;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>Guards the Guardicore provisioning contract, defaults and inheritance.</summary>
[TestFixture]
[Parallelizable]
internal class ProvisioningGuardicoreSettingsTest
{
    private static readonly DeviceType kDeviceType = new() { Id = 10, Name = "Guardicore" };
    private static readonly List<string> kExpectedKeys =
    [
        "implementationMode", "installOn", "positioningAlgorithm", "ruleLoggingOverride",
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleType", "ruleCategory", "ruleZoneFromOverride", "ruleZoneToOverride",
        "implementationModeOverrideForRules", "securityProfiles", "ownerIdCustomFieldName",
        "changeNumberCustomFieldName", "planImplementation",
        "planImplementationOverrideForRules", "modellingNamingConventions", "modellingCreateAppZones",
        "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups", "templates"
    ];
    private static readonly HashSet<string> kManagerKeys =
    [
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleCategory", "ownerIdCustomFieldName", "changeNumberCustomFieldName",
        "modellingCreateAppZones", "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups"
    ];
    private static readonly HashSet<string> kGlobalKeys = ["modellingNamingConventions"];
    private static readonly HashSet<string> kDeviceKeys = ["securityProfiles", "templates"];

    /// <summary>Creates a real four-level locator hierarchy for persistence tests.</summary>
    private static InMemoryProvisioningApiConnection CreateHierarchy()
    {
        InMemoryProvisioningApiConnection api = new();
        api.AddNode(1, ProvisioningScopeType.Global, "global");
        api.AddNode(2, ProvisioningScopeType.DeviceType, "10", 1);
        api.AddNode(3, ProvisioningScopeType.Management, "100", 2);
        api.AddNode(4, ProvisioningScopeType.Gateway, "200", 3);
        return api;
    }

    /// <summary>Each key is tested at every level independently of the catalog scope definitions.</summary>
    private static IEnumerable<TestCaseData> ScopeCases() =>
        from key in ProvisioningSettingKeys.All
        from scope in Enum.GetValues<ProvisioningScopeType>()
        select new TestCaseData(key, scope);

    /// <summary>The persisted keys match the issue contract.</summary>
    [Test]
    public void CatalogMatchesContract()
    {
        Assert.That(ProvisioningSettingKeys.All.Select(key => key.DatabaseKey), Is.EquivalentTo(kExpectedKeys));
        Assert.That(ProvisioningSettingsData.Fields.Select(field => field.Key), Is.EquivalentTo(ProvisioningSettingKeys.All));
    }

    /// <summary>Writes reject forbidden scopes, and editors use the same restrictions.</summary>
    [TestCaseSource(nameof(ScopeCases))]
    public void ScopeRestrictionsMatchContract(ProvisioningSettingKey key, ProvisioningScopeType scope)
    {
        bool expected = scope != ProvisioningScopeType.Undefined
            && (!kManagerKeys.Contains(key.DatabaseKey) || scope != ProvisioningScopeType.Gateway)
            && (!kGlobalKeys.Contains(key.DatabaseKey) || scope == ProvisioningScopeType.Global)
            && (!kDeviceKeys.Contains(key.DatabaseKey) || scope != ProvisioningScopeType.Global)
            && (key != ProvisioningSettingKeys.Templates || scope != ProvisioningScopeType.Gateway);
        Assert.That(key.IsAllowedAt(scope), Is.EqualTo(expected), $"{key}@{scope}");
        Assert.That(ProvisioningSettingsData.FindField(key)!.AppliesToLevel(scope), Is.EqualTo(expected));
    }

    /// <summary>New settings have the documented defaults.</summary>
    [Test]
    public void DefaultsMatchContract()
    {
        GlobalProvisioningSettings settings = new();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.TimeObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.LabelCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.ImplementationModeOverrideForRules, Is.EqualTo(ProvisioningRuleImplementationMode.Automatic));
            Assert.That(settings.OwnerIdCustomFieldName, Is.EqualTo("OwnerId"));
            Assert.That(settings.ChangeNumberCustomFieldName, Is.EqualTo("ChangeId"));
            Assert.That(settings.PlanImplementation, Is.EqualTo(ProvisioningPlanningMode.Enable));
            Assert.That(settings.PlanImplementationOverrideForRules, Is.EqualTo(ProvisioningPlanningMode.Enable));
            Assert.That(settings.ModellingCreateAppZones, Is.EqualTo(ProvisioningYesNo.Yes));
            Assert.That(settings.ModellingImplementNetworkAreaObjects, Is.EqualTo(ProvisioningYesNo.No));
            Assert.That(settings.ModellingDynIpLabelsVisibleAsGroups, Is.EqualTo(ProvisioningYesNo.Yes));
            Assert.That(settings.ModellingNamingConventions["appRole"], Is.EqualTo("{idString}"));
            Assert.That(settings.ModellingNamingConventions["appZone"], Is.EqualTo("{idString}"));
        }
        settings.ModellingNamingConventions["appRole"] = "custom";
        Assert.That(new GlobalProvisioningSettings().ModellingNamingConventions["appRole"], Is.EqualTo("{idString}"));
    }

    /// <summary>Every allowed UI value travels through persistence and the typed mapper.</summary>
    private static IEnumerable<TestCaseData> FieldCases() =>
        ProvisioningSettingsData.Fields.Select(field => new TestCaseData(field));

    /// <summary>All fields resolve at gateway even when their overrides are limited to ancestors.</summary>
    [TestCaseSource(nameof(FieldCases))]
    public async Task ValuesInheritThroughAllLevels(ProvisioningFieldDefinition field)
    {
        InMemoryProvisioningApiConnection api = CreateHierarchy();
        ProvisioningScopeType scope = field.MinLevel == ProvisioningScopeType.DeviceType
            ? ProvisioningScopeType.DeviceType : ProvisioningScopeType.Global;
        long nodeId = scope == ProvisioningScopeType.Global ? 1 : 2;
        string editorValue = field.Kind switch
        {
            ProvisioningFieldKind.SingleSelect => field.Options.Last().Value,
            ProvisioningFieldKind.NamingConventions => "{\"appRole\":\"AR-{idString}\",\"appZone\":\"AZ-{idString}\",\"network\":\"n\",\"host\":\"h\",\"networkArea\":\"a\",\"range\":\"r\"}",
            ProvisioningFieldKind.StringList => "profile1,profile2",
            _ => "custom value"
        };
        ProvisioningSettingsChangeSet changes = new(new ProvisioningSettingsScope
        {
            ScopeType = scope,
            ObjectKey = scope == ProvisioningScopeType.Global ? "global" : "10",
            NodeId = nodeId,
            ParentNodeId = scope == ProvisioningScopeType.Global ? null : 1
        });
        field.SetOverride(changes, editorValue);
        ProvisioningSettingsManager manager = new(api);
        await manager.ApplyChangesAsync(changes);
        Management management = new() { Id = 100, DeviceType = kDeviceType };
        Device gateway = new() { Id = 200, Management = management };
        ProvisioningSettingsLevel<GatewayProvisioningSettings> result =
            await manager.LoadLevelAsync<GatewayProvisioningSettings>(ProvisioningScopePath.ForGateway(management, gateway));
        Assert.That(field.Read(result.Settings), Is.EqualTo(editorValue));
        Assert.That(result.ValueSources[field.Key].Scope?.NodeId, Is.EqualTo(nodeId));
        Assert.That(result.DirectOverrides, Does.Not.Contain(field.Key));
    }

    /// <summary>Reads of settings with restricted write scopes still inherit at gateway.</summary>
    [Test]
    public async Task EffectiveReadSupportsGlobalOnlyKeysAtGateway()
    {
        InMemoryProvisioningApiConnection api = CreateHierarchy();
        Dictionary<string, string> conventions = ProvisioningModellingNamingConventions.CreateDefaults();
        conventions["appRole"] = "Role-{idString}";
        api.SetValue(1, ProvisioningSettingKeys.ModellingNamingConventions, conventions);
        Management management = new() { Id = 100, DeviceType = kDeviceType };
        Device gateway = new() { Id = 200, Management = management };
        ProvisioningSettingsManager manager = new(api);
        var value = await manager.LoadEffectiveValueAsync(ProvisioningScopePath.ForGateway(management, gateway),
            ProvisioningSettingKeys.ModellingNamingConventions);
        Assert.That(value.Value["appRole"], Is.EqualTo("Role-{idString}"));
        Assert.That(value.Source.Scope?.NodeId, Is.EqualTo(1));
    }

    /// <summary>Malformed naming JSON is rejected before a patch can be persisted.</summary>
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"appRole\":123}")]
    [TestCase("null")]
    public void NamingEditorRejectsInvalidJson(string json)
    {
        ProvisioningSettingsChangeSet changes = new(new GlobalProvisioningSettings().Scope);
        Assert.Catch(() => ProvisioningSettingsData.FindField(ProvisioningSettingKeys.ModellingNamingConventions)!
            .SetOverride(changes, json));
        Assert.That(changes.IsEmpty, Is.True);
    }

    /// <summary>Missing, unknown, null and empty naming values cannot enter storage.</summary>
    [TestCase("missing")]
    [TestCase("unknown")]
    [TestCase("null")]
    [TestCase("empty")]
    public void NamingSerializerRejectsIncompleteContracts(string mutation)
    {
        Dictionary<string, string> conventions = ProvisioningModellingNamingConventions.CreateDefaults();
        switch (mutation)
        {
            case "missing": conventions.Remove("appRole"); break;
            case "unknown": conventions.Add("unknown", "value"); break;
            case "null": conventions["appRole"] = null!; break;
            case "empty": conventions["appRole"] = " "; break;
        }
        Assert.Throws<ArgumentException>(() => ProvisioningSettingValueSerializer.Serialize(
            ProvisioningSettingKeys.ModellingNamingConventions, conventions));
        Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(
            ProvisioningSettingKeys.ModellingNamingConventions, JObject.FromObject(conventions)));
    }

    /// <summary>Wire values are independent of enum display names.</summary>
    [TestCase("installOn", "Any", "ANY")]
    [TestCase("installOn", "GatewaysInPath", "gatewaysInPath")]
    [TestCase("labelCreation", "Supermanager", "supermanager")]
    [TestCase("labelCreation", "Submanager", "submanager")]
    [TestCase("timeObjectCreation", "Ignore", "ignore")]
    [TestCase("labelCreation", "Ignore", "ignore")]
    [TestCase("ruleLoggingOverride", "LogTrack", "log_track")]
    [TestCase("ruleType", "AppSecurity", "AppSecurity")]
    [TestCase("ruleType", "HandleAccessAndIps", "accessAndIPS")]
    [TestCase("positioningAlgorithm", "AllowCategoryEndOfAppSectionDistinguishCommonServices", "allowCategoryEndOfAppSectionDistinguishCommonServices")]
    [TestCase("implementationMode", "RerouteToExternalTicketSystem", "reroute_to_external_ticket_system")]
    [TestCase("implementationModeOverrideForRules", "RerouteToExternalTicketSystem", "rerouteToExternalTicketSystem")]
    public void SerializedValuesMatchContract(string databaseKey, string enumName, string wireValue)
    {
        ProvisioningSettingKey key = ProvisioningSettingKeys.ByDatabaseKey[databaseKey];
        object value = Enum.Parse(key.ValueType, enumName);
        Assert.That(ProvisioningSettingValueSerializer.Serialize(key, value), Is.EqualTo(new JValue(wireValue)));
        Assert.That(ProvisioningSettingValueSerializer.Deserialize(key, new JValue(wireValue)), Is.EqualTo(value));
    }

    /// <summary>Ignore is forbidden for service and address object creation.</summary>
    [Test]
    public void IgnoreIsLimitedToTimesAndLabels()
    {
        Assert.Throws<ArgumentException>(() => ProvisioningSettingValueSerializer.Serialize(
            ProvisioningSettingKeys.ServiceObjectCreation, ProvisioningObjectCreationMode.Ignore));
        Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(
            ProvisioningSettingKeys.AddressObjectCreation, new JValue("ignore")));
    }
}
