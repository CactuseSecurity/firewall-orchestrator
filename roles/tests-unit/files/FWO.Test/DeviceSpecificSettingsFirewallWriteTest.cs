using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>The firewall write fields of the device specific settings page.</summary>
    [TestFixture]
    [Parallelizable]
    internal class DeviceSpecificSettingsFirewallWriteTest
    {
        private const int kGuardicoreDeviceTypeId = 31;
        private static readonly List<DeviceSpecificSettingsScope> kNoPersistedScopes = [];
        private static readonly List<string> kSectionTextKeys =
            ["dss_fw_write_section", "dss_fw_write_section_intro", "dss_fw_write_not_supported", "dss_fw_write_uses_default",
             "dss_reset_to_default", "dss_copy_default", "H5199r", "H5199s"];

        private static DeviceSpecificNode Hierarchy(params Management[] managements) =>
            DeviceSpecificSettingsData.BuildHierarchy(managements, kNoPersistedScopes);

        private static Management Management(int id, int deviceTypeId) => new()
        {
            Id = id,
            Name = $"m{id}",
            DeviceType = new DeviceType { Id = deviceTypeId, Name = $"t{deviceTypeId}", Manufacturer = "x" },
            Devices = [new Device { Id = id * 10, Name = $"g{id}" }]
        };

        private static DeviceSpecificNode Node(DeviceSpecificNode root, DeviceSpecificScopeType level, int deviceTypeId) =>
            root.SelfAndDescendants().First(node => node.Level == level && node.DeviceTypeId == deviceTypeId);

        private static DeviceSpecificFieldDefinition Field(DeviceSpecificSettingKey key) => DeviceSpecificSettingsData.FindField(key)!;

        [Test]
        public void FirewallWriteFields_AreOfferedForSupportedDeviceTypesOnly()
        {
            DeviceSpecificNode root = Hierarchy(Management(1, FirewallVendors.kCheckPointR8x), Management(2, kGuardicoreDeviceTypeId));
            DeviceSpecificFieldDefinition endpoint = Field(FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate]);
            DeviceSpecificFieldDefinition certificates = Field(FirewallWriteSettingKeys.CheckCertificates);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoint.AppliesTo(root), Is.False);
                Assert.That(certificates.AppliesTo(root), Is.True);
                Assert.That(endpoint.AppliesTo(Node(root, DeviceSpecificScopeType.Gateway, FirewallVendors.kCheckPointR8x)), Is.True);
                Assert.That(certificates.AppliesTo(Node(root, DeviceSpecificScopeType.Gateway, FirewallVendors.kCheckPointR8x)), Is.False);
                Assert.That(endpoint.AppliesTo(Node(root, DeviceSpecificScopeType.Management, kGuardicoreDeviceTypeId)), Is.False);
                Assert.That(Node(root, DeviceSpecificScopeType.Management, FirewallVendors.kCheckPointR8x).SupportsFirewallWrite, Is.True);
                Assert.That(root.SupportsFirewallWrite, Is.False);
            }
        }

        [Test]
        public void ShippedDefault_FollowsTheDeviceTypeOfTheNode()
        {
            DeviceSpecificNode root = Hierarchy(Management(1, FirewallVendors.kCheckPointR8x), Management(2, FirewallVendors.kFortiOsGateway));
            DeviceSpecificFieldDefinition endpoint = Field(FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate]);
            DeviceSpecificFieldDefinition template = Field(FirewallWriteSettingKeys.Templates[FirewallWriteOperation.Publish]);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(endpoint.ShippedDefaultFor(Node(root, DeviceSpecificScopeType.Gateway, FirewallVendors.kCheckPointR8x)), Is.EqualTo("POST add-host"));
                Assert.That(endpoint.ShippedDefaultFor(Node(root, DeviceSpecificScopeType.DeviceType, FirewallVendors.kFortiOsGateway)),
                    Is.EqualTo("POST /api/v2/cmdb/firewall/address"));
                Assert.That(template.ShippedDefaultFor(Node(root, DeviceSpecificScopeType.Management, FirewallVendors.kFortiOsGateway)), Is.Null);
                Assert.That(endpoint.ShippedDefaultFor(root), Is.Null);
                Assert.That(Field(FirewallWriteSettingKeys.InstallPolicy).ShippedDefaultFor(root), Is.Null);
            }
        }

        [Test]
        public void EndpointOverrides_AreValidatedAndNormalized()
        {
            DeviceSpecificFieldDefinition endpoint = Field(FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate]);
            DeviceSpecificFieldDefinition template = Field(FirewallWriteSettingKeys.Templates[FirewallWriteOperation.HostCreate]);
            DeviceSpecificSettingsChangeSet changes = new(new DeviceSpecificSettingsScope { ScopeType = DeviceSpecificScopeType.DeviceType, ObjectKey = "9" });

            endpoint.SetOverride(changes, "post   add-host");
            template.SetOverride(changes, "{not json yet: @@MEMBERS@@}");
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteEndpoints[FirewallWriteOperation.HostCreate] = "PUT x";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(changes.Upserts.Single(upsert => upsert.Key == endpoint.Key).Value, Is.EqualTo("POST add-host"));
                Assert.That(changes.Upserts.Single(upsert => upsert.Key == template.Key).Value, Is.EqualTo("{not json yet: @@MEMBERS@@}"));
                Assert.Throws<ArgumentException>(() => endpoint.SetOverride(changes, "SEND add-host"));
                Assert.DoesNotThrow(() => endpoint.SetOverride(changes, ""));
                Assert.That(endpoint.Read(settings), Is.EqualTo("PUT x"));
                Assert.That(template.Read(settings), Is.Empty);
            }
        }

        private static string LocateTextsFile()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                FileInfo candidate = new(Path.Combine(directory.FullName, "roles", "database", "files", "sql", "idempotent", "fworch-texts.sql"));
                if (candidate.Exists)
                {
                    return candidate.FullName;
                }
                directory = directory.Parent;
            }
            Assert.Ignore("The database sources are not reachable in this environment.");
            return string.Empty;
        }

        [TestCase(FirewallWriteOperation.HostCreate, "dss_fw_op_host_create")]
        [TestCase(FirewallWriteOperation.GroupAddMembers, "dss_fw_op_group_add_members")]
        [TestCase(FirewallWriteOperation.InstallPolicy, "dss_fw_op_install_policy")]
        public void OperationTextKey_IsSnakeCase(FirewallWriteOperation operation, string textKey)
        {
            Assert.That(DeviceSpecificSettingsData.OperationTextKey(operation), Is.EqualTo(textKey));
        }

        [Test]
        public void AllFirewallWriteTextsAreLocalized()
        {
            string texts = File.ReadAllText(LocateTextsFile());
            List<DeviceSpecificFieldDefinition> fields = [.. DeviceSpecificSettingsData.Fields.Where(field => field.Section == DeviceSpecificFieldSection.FirewallWrite)];
            List<string> textKeys = [.. kSectionTextKeys];
            textKeys.AddRange(fields.Select(field => field.LabelTextKey));
            textKeys.AddRange(fields.Select(field => field.HelpTextKey));
            textKeys.AddRange(fields.Select(field => field.LabelDetailTextKey).OfType<string>());
            foreach (string textKey in textKeys.Distinct())
            {
                Assert.That(texts, Does.Contain($"('{textKey}', 'German'"), textKey);
                Assert.That(texts, Does.Contain($"('{textKey}', 'English'"), textKey);
            }
        }
    }
}
