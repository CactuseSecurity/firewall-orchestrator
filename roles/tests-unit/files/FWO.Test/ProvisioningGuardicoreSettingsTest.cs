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
        "installOn", "positioningAlgorithm", "ruleLoggingOverride",
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleType", "commonServiceSeparation", "ruleZoneFromOverride", "ruleZoneToOverride",
        "implementationMode", "implementationModeOverrideForRules", "rerouteToExternalTicketSystem",
        "securityProfiles", "ownerIdCustomFieldName", "changeIdCustomFieldName", "importerHostName",
        "planImplementation", "planImplementationOverrideForRules", "modellingCreateAppZones",
        "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups",
        "modellingConnectionIdField", "modellingConnectionIdTemplate", "templates"
    ];
    private static readonly HashSet<string> kManagerConfigKeys =
    [
        "ownerIdCustomFieldName", "changeIdCustomFieldName", "importerHostName",
        "modellingCreateAppZones", "modellingImplementNetworkAreaObjects", "modellingDynIpLabelsVisibleAsGroups",
        "modellingConnectionIdField", "modellingConnectionIdTemplate"
    ];
    private static readonly HashSet<string> kManagerUiKeys =
    [
        "serviceObjectCreation", "addressObjectCreation", "timeObjectCreation", "labelCreation",
        "ruleType", "commonServiceSeparation", "ruleZoneFromOverride", "ruleZoneToOverride", "securityProfiles"
    ];
    private static readonly List<ProvisioningScopeType> kAllLevels =
        [ProvisioningScopeType.Global, ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management, ProvisioningScopeType.Gateway];
    private static readonly List<ProvisioningScopeType> kManagerLevels =
        [ProvisioningScopeType.Global, ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management];
    private static readonly List<ProvisioningScopeType> kDeviceLevels =
        [ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management, ProvisioningScopeType.Gateway];
    private static readonly List<ProvisioningScopeType> kDeviceUiLevels =
        [ProvisioningScopeType.DeviceType, ProvisioningScopeType.Management];
    private static readonly List<ProvisioningScopeType> kDeviceTypeLevel = [ProvisioningScopeType.DeviceType];
    private const string kTicketSystems = "[{\"Id\":3,\"Name\":\"Ticketing\"}]";
    private static readonly List<JValue> kInvalidTicketSystemIds = [new("3"), new(3.5), new(long.MaxValue)];
    private static readonly List<(string Value, string Name)> kConfiguredTicketSystemOptions = [("3", "Ticketing (3)")];
    private static readonly List<(string Value, string Name)> kOptionsWithRemovedTicketSystem = [("3", "Ticketing (3)"), ("9", "9")];

    /// <summary>Levels at which the API accepts an override, per the issue's config column.</summary>
    private static List<ProvisioningScopeType> ConfigLevels(ProvisioningSettingKey key) => key.DatabaseKey switch
    {
        "templates" => kDeviceTypeLevel,
        "securityProfiles" => kDeviceLevels,
        _ => kManagerConfigKeys.Contains(key.DatabaseKey) ? kManagerLevels : kAllLevels
    };

    /// <summary>Levels at which the settings page offers the field, per the issue's UI max level column.</summary>
    private static List<ProvisioningScopeType> UiLevels(ProvisioningSettingKey key) => key.DatabaseKey switch
    {
        "templates" => kDeviceTypeLevel,
        "securityProfiles" => kDeviceUiLevels,
        _ => kManagerConfigKeys.Contains(key.DatabaseKey) || kManagerUiKeys.Contains(key.DatabaseKey) ? kManagerLevels : kAllLevels
    };

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

    /// <summary>Writes reject forbidden scopes, and editors are offered no deeper than the UI max level.</summary>
    [TestCaseSource(nameof(ScopeCases))]
    public void ScopeRestrictionsMatchContract(ProvisioningSettingKey key, ProvisioningScopeType scope)
    {
        Assert.That(key.IsAllowedAt(scope), Is.EqualTo(ConfigLevels(key).Contains(scope)), $"{key}@{scope}");
        Assert.That(ProvisioningSettingsData.FindField(key)!.AppliesToLevel(scope), Is.EqualTo(UiLevels(key).Contains(scope)), $"UI {key}@{scope}");
    }

    /// <summary>New settings have the documented defaults.</summary>
    [Test]
    public void DefaultsMatchContract()
    {
        GlobalProvisioningSettings settings = new();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ImplementationMode, Is.EqualTo(ProvisioningImplementationMode.Automatic));
            Assert.That(settings.TimeObjectCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.LabelCreation, Is.EqualTo(ProvisioningObjectCreationMode.Supermanager));
            Assert.That(settings.CommonServiceSeparation, Is.False);
            Assert.That(settings.ImplementationModeOverrideForRules, Is.EqualTo(ProvisioningRuleImplementationMode.Automatic));
            Assert.That(settings.RerouteToExternalTicketSystem, Is.Null);
            Assert.That(settings.OwnerIdCustomFieldName, Is.EqualTo("OwnerId"));
            Assert.That(settings.ChangeIdCustomFieldName, Is.EqualTo("ChangeId"));
            Assert.That(settings.ImporterHostName, Is.Empty);
            Assert.That(settings.PlanImplementation, Is.True);
            Assert.That(settings.PlanImplementationOverrideForRules, Is.True);
            Assert.That(settings.ModellingCreateAppZones, Is.True);
            Assert.That(settings.ModellingImplementNetworkAreaObjects, Is.False);
            Assert.That(settings.ModellingDynIpLabelsVisibleAsGroups, Is.True);
            Assert.That(settings.ModellingConnectionIdField, Is.EqualTo(ProvisioningConnectionIdField.Name));
            Assert.That(settings.ModellingConnectionIdTemplate, Is.EqualTo("FWOC{ID}"));
            Assert.That(settings.Templates, Is.EqualTo("{}"));
        }
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
            ProvisioningFieldKind.ExtTicketSystem => "7",
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
    public async Task EffectiveReadSupportsManagerOnlyKeysAtGateway()
    {
        InMemoryProvisioningApiConnection api = CreateHierarchy();
        api.SetValue(1, ProvisioningSettingKeys.ModellingConnectionIdTemplate, "CONN-{ID}");
        Management management = new() { Id = 100, DeviceType = kDeviceType };
        Device gateway = new() { Id = 200, Management = management };
        ProvisioningSettingsManager manager = new(api);
        ResolvedProvisioningValue<string> value = await manager.LoadEffectiveValueAsync(ProvisioningScopePath.ForGateway(management, gateway),
            ProvisioningSettingKeys.ModellingConnectionIdTemplate);
        Assert.That(value.Value, Is.EqualTo("CONN-{ID}"));
        Assert.That(value.Source.Scope?.NodeId, Is.EqualTo(1));
    }

    /// <summary>An undefined ticket system resolves to null from the compiled default.</summary>
    [Test]
    public async Task EffectiveReadOfUndefinedTicketSystemIsNull()
    {
        ProvisioningSettingsManager manager = new(CreateHierarchy());
        ResolvedProvisioningValue<int?> value = await manager.LoadEffectiveValueAsync(ProvisioningScopePath.Global(),
            ProvisioningSettingKeys.RerouteToExternalTicketSystem);
        Assert.That(value.Value, Is.Null);
        Assert.That(value.Source.UsesCompiledDefault, Is.True);
    }

    /// <summary>Booleans are stored as JSON booleans only.</summary>
    [Test]
    public void BooleansUseJsonBooleans()
    {
        Assert.That(ProvisioningSettingValueSerializer.Serialize(ProvisioningSettingKeys.PlanImplementation, false), Is.EqualTo(new JValue(false)));
        Assert.That(ProvisioningSettingValueSerializer.Deserialize(ProvisioningSettingKeys.PlanImplementation, new JValue(true)), Is.True);
        Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(
            ProvisioningSettingKeys.ModellingCreateAppZones, new JValue("Yes")));
    }

    /// <summary>Ticket system IDs are positive JSON integers; undefined is never stored.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void TicketSystemRejectsNonPositiveIds(int id)
    {
        Assert.Throws<ArgumentException>(() => ProvisioningSettingValueSerializer.Serialize(
            ProvisioningSettingKeys.RerouteToExternalTicketSystem, (int?)id));
        Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(
            ProvisioningSettingKeys.RerouteToExternalTicketSystem, new JValue(id)));
    }

    /// <summary>Strings, fractions and values outside the integer range are not ticket system IDs.</summary>
    [Test]
    public void TicketSystemRejectsNonIntegerValues()
    {
        Assert.Throws<ArgumentNullException>(() => ProvisioningSettingValueSerializer.Serialize(
            ProvisioningSettingKeys.RerouteToExternalTicketSystem, null));
        foreach (JValue stored in kInvalidTicketSystemIds)
        {
            Assert.Throws<InvalidOperationException>(() => ProvisioningSettingValueSerializer.Deserialize(
                ProvisioningSettingKeys.RerouteToExternalTicketSystem, stored));
        }
    }

    /// <summary>Selecting undefined in the editor removes the override instead of storing a value.</summary>
    [Test]
    public void TicketSystemEditorTreatsEmptyAsInherit()
    {
        ProvisioningSettingsChangeSet changes = new(new GlobalProvisioningSettings().Scope);
        ProvisioningSettingsData.FindField(ProvisioningSettingKeys.RerouteToExternalTicketSystem)!.SetOverride(changes, "");
        Assert.That(changes.Upserts, Is.Empty);
        Assert.That(changes.Removals, Does.Contain(ProvisioningSettingKeys.RerouteToExternalTicketSystem));
    }

    /// <summary>Configured ticket systems are offered by name; a stored but removed ID stays selectable.</summary>
    [Test]
    public void TicketSystemOptionsListConfiguredSystems()
    {
        Assert.That(ProvisioningSettingsData.ExtTicketSystemOptions(kTicketSystems, ""),
            Is.EqualTo(kConfiguredTicketSystemOptions));
        Assert.That(ProvisioningSettingsData.ExtTicketSystemOptions(kTicketSystems, "9"),
            Is.EqualTo(kOptionsWithRemovedTicketSystem));
        Assert.That(ProvisioningSettingsData.ExtTicketSystemOptions("", ""), Is.Empty);
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
    [TestCase("implementationMode", "Automatic", "Automatic")]
    [TestCase("modellingConnectionIdField", "CustomField", "CustomField")]
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
