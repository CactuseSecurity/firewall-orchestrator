using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>The SecureChange template fields of the device specific settings page.</summary>
    [TestFixture]
    [Parallelizable]
    internal class DeviceSpecificSettingsSecureChangeTest
    {
        private const int kDeviceTypeId = 9;
        private static readonly List<DeviceSpecificSettingsScope> kNoPersistedScopes = [];
        private static readonly List<string> kSectionTextKeys =
            ["dss_sc_template_section", "dss_sc_template_section_intro", "dss_sc_template_uses_default", "dss_sc_template_no_default",
             "ext_ticket_sc_templates_moved", "H5199v", "H5599a"];
        private static readonly List<Management> kManagements =
        [
            new()
            {
                Id = 1,
                Name = "m1",
                DeviceType = new DeviceType { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x", Manufacturer = "Check Point" },
                Devices = [new Device { Id = 10, Name = "g1" }]
            }
        ];

        private static List<DeviceSpecificFieldDefinition> SecureChangeFields() =>
            [.. DeviceSpecificSettingsData.Fields.Where(field => field.Section == DeviceSpecificFieldSection.SecureChange)];

        private static DeviceSpecificFieldDefinition Field(SCTaskType taskType, SecureChangeTemplatePart part) =>
            DeviceSpecificSettingsData.FindField(SecureChangeTemplateSettingKeys.Keys[(taskType, part)])!;

        private static DeviceSpecificNode Node(DeviceSpecificNode root, DeviceSpecificScopeType level) =>
            root.SelfAndDescendants().First(node => node.Level == level);

        [Test]
        public void Fields_AreOfferedFromGlobalDownToTheManagement()
        {
            DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(kManagements, kNoPersistedScopes);
            List<DeviceSpecificFieldDefinition> fields = SecureChangeFields();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(fields.Select(field => field.Key), Is.EquivalentTo(SecureChangeTemplateSettingKeys.All));
                Assert.That(fields.All(field => field.Kind == DeviceSpecificFieldKind.SecureChangeTemplate), Is.True);
                Assert.That(fields.All(field => field.AppliesTo(root)), Is.True);
                Assert.That(fields.All(field => field.AppliesTo(Node(root, DeviceSpecificScopeType.DeviceType))), Is.True);
                Assert.That(fields.All(field => field.AppliesTo(Node(root, DeviceSpecificScopeType.Management))), Is.True);
                Assert.That(fields.Any(field => field.AppliesTo(Node(root, DeviceSpecificScopeType.Gateway))), Is.False);
            }
        }

        [Test]
        public void ShippedDefault_IsTheSameOnEveryLevel()
        {
            DeviceSpecificNode root = DeviceSpecificSettingsData.BuildHierarchy(kManagements, kNoPersistedScopes);
            DeviceSpecificFieldDefinition ticket = Field(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket);
            string? shippedTicket = SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ticket.ShippedDefaultFor(root), Is.EqualTo(shippedTicket));
                Assert.That(ticket.ShippedDefaultFor(Node(root, DeviceSpecificScopeType.Management)), Is.EqualTo(shippedTicket));
                Assert.That(Field(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Ticket).ShippedDefault, Is.Not.Null);
                Assert.That(Field(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Ticket).ShippedDefaultFor(root), Is.Null);
            }
        }

        [Test]
        public void Override_StoresTheTextIncludingAnEmptyValue()
        {
            DeviceSpecificFieldDefinition objectShort = Field(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.ObjectShort);
            DeviceSpecificSettingsChangeSet changes = new(new DeviceSpecificSettingsScope { ScopeType = DeviceSpecificScopeType.Management, ObjectKey = "1" });
            ManagementDeviceSpecificSettings settings = new();
            objectShort.Key.DtoSetter!(settings, "short");

            objectShort.SetOverride(changes, "");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(changes.Upserts.Single(upsert => upsert.Key == objectShort.Key).Value, Is.Empty);
                Assert.That(objectShort.Read(settings), Is.EqualTo("short"));
                Assert.That(objectShort.LabelTextKey, Is.EqualTo("ext_obj_short_template"));
                Assert.That(objectShort.HelpTextKey, Is.EqualTo("H5596b"));
                Assert.That(objectShort.LabelDetailTextKey, Is.EqualTo("dss_sc_task_network_object_modify"));
            }
        }

        [TestCase(SCTaskType.AccessRequest, "dss_sc_task_access_request")]
        [TestCase(SCTaskType.NetworkServiceUpdate, "dss_sc_task_network_service_update")]
        public void SecureChangeTaskTextKey_IsSnakeCase(SCTaskType taskType, string textKey)
        {
            Assert.That(DeviceSpecificSettingsData.SecureChangeTaskTextKey(taskType), Is.EqualTo(textKey));
        }

        [Test]
        public void AllSecureChangeTemplateTextsAreLocalized()
        {
            string texts = File.ReadAllText(LocateTextsFile());
            List<DeviceSpecificFieldDefinition> fields = SecureChangeFields();
            List<string> textKeys = [.. kSectionTextKeys];
            textKeys.AddRange(fields.Select(field => field.LabelTextKey));
            textKeys.AddRange(fields.Select(field => field.HelpTextKey));
            textKeys.AddRange(fields.Select(field => field.LabelDetailTextKey).OfType<string>());
            foreach (string textKey in textKeys.Distinct())
            {
                Assert.That(texts, Does.Match($@"\('{textKey}',\s*'German'"), textKey);
                Assert.That(texts, Does.Match($@"\('{textKey}',\s*'English'"), textKey);
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
    }
}
