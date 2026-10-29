using System.Text.Json;
using System.Text.RegularExpressions;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.Tufin.SecureChange;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>The SecureChange ticket templates as device specific settings: keys, shipped defaults and their resolution.</summary>
    [TestFixture]
    [Parallelizable]
    internal class SecureChangeTemplateSettingsTest
    {
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kManagementNodeId = 3;
        private const int kDeviceTypeId = 10;
        private const int kInheritingManagementId = 1;
        private const int kOverridingManagementId = 2;
        private const int kUnknownManagementId = 99;
        private const int kTemplateKeyCount = 23;
        private static readonly List<SecureChangeTemplatePart> kAccessRequestParts =
        [
            SecureChangeTemplatePart.Ticket, SecureChangeTemplatePart.Tasks, SecureChangeTemplatePart.Ip, SecureChangeTemplatePart.NwObjGroup,
            SecureChangeTemplatePart.Service, SecureChangeTemplatePart.Icmp, SecureChangeTemplatePart.IpProtocol
        ];
        private static readonly List<SecureChangeTemplatePart> kNetworkServiceParts =
        [
            SecureChangeTemplatePart.Ticket, SecureChangeTemplatePart.Tasks, SecureChangeTemplatePart.Service, SecureChangeTemplatePart.Icmp,
            SecureChangeTemplatePart.IpProtocol
        ];
        private static readonly List<int> kRequestedManagements = [kInheritingManagementId, kOverridingManagementId, kUnknownManagementId];
        private static readonly List<int> kNoManagements = [];

        private static DeviceSpecificSettingKey<string> Key(SCTaskType taskType, SecureChangeTemplatePart part) =>
            SecureChangeTemplateSettingKeys.Keys[(taskType, part)];

        private static ExternalTicketTemplate TemplateOf(List<ExternalTicketTemplate> templates, SCTaskType taskType) =>
            templates.Single(template => template.TaskType == taskType.ToString());

        [Test]
        public void Keys_CoverThePartsOfEveryTaskTypeDownToTheManagement()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SecureChangeTemplateSettingKeys.All, Has.Count.EqualTo(kTemplateKeyCount));
                Assert.That(SecureChangeTemplateSettingKeys.PartsOf(SCTaskType.AccessRequest), Is.EqualTo(kAccessRequestParts));
                Assert.That(SecureChangeTemplateSettingKeys.PartsOf(SCTaskType.NetworkServiceUpdate), Is.EqualTo(kNetworkServiceParts));
                Assert.That(Key(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.ObjectShort).DatabaseKey,
                    Is.EqualTo("scTemplateNetworkObjectModifyObjectShort"));
                Assert.That(SecureChangeTemplateSettingKeys.Keys.ContainsKey((SCTaskType.AccessRequest, SecureChangeTemplatePart.Object)), Is.False);
                Assert.That(DeviceSpecificSettingKeys.All, Is.SupersetOf(SecureChangeTemplateSettingKeys.All));
            }
            foreach (DeviceSpecificSettingKey key in SecureChangeTemplateSettingKeys.All)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(key.IsAllowedAt(DeviceSpecificScopeType.Global), Is.True, key.DatabaseKey);
                    Assert.That(key.IsAllowedAt(DeviceSpecificScopeType.DeviceType), Is.True, key.DatabaseKey);
                    Assert.That(key.IsAllowedAt(DeviceSpecificScopeType.Management), Is.True, key.DatabaseKey);
                    Assert.That(key.IsAllowedAt(DeviceSpecificScopeType.Gateway), Is.False, key.DatabaseKey);
                }
            }
        }

        [Test]
        public void PartAccessors_ReadWhatTheyWrite()
        {
            ExternalTicketTemplate template = new();
            foreach (SecureChangeTemplatePart part in Enum.GetValues<SecureChangeTemplatePart>())
            {
                SecureChangeTemplateSettingKeys.SetPart(template, part, part.ToString());
            }
            using (Assert.EnterMultipleScope())
            {
                foreach (SecureChangeTemplatePart part in Enum.GetValues<SecureChangeTemplatePart>())
                {
                    Assert.That(SecureChangeTemplateSettingKeys.GetPart(template, part), Is.EqualTo(part.ToString()));
                }
                Assert.That(template.ObjectTemplateShort, Is.EqualTo(nameof(SecureChangeTemplatePart.ObjectShort)));
                Assert.That(template.IpProtocolTemplate, Is.EqualTo(nameof(SecureChangeTemplatePart.IpProtocol)));
                Assert.Throws<ArgumentOutOfRangeException>(() => SecureChangeTemplateSettingKeys.GetPart(template, (SecureChangeTemplatePart)99));
                Assert.Throws<ArgumentOutOfRangeException>(() => SecureChangeTemplateSettingKeys.SetPart(template, (SecureChangeTemplatePart)99, ""));
            }
        }

        [Test]
        public void DtoAccessors_KeepTheTemplatesPerTaskType()
        {
            GlobalDeviceSpecificSettings settings = new();
            DeviceSpecificSettingKey<string> ticket = Key(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Ticket);
            DeviceSpecificSettingKey<string> objectTemplate = Key(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Object);

            Assert.That(ticket.DtoGetter!(settings), Is.Empty);
            ticket.DtoSetter!(settings, "ticket");
            objectTemplate.DtoSetter!(settings, "object");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(settings.SecureChangeTemplates, Has.Count.EqualTo(1));
                Assert.That(settings.SecureChangeTemplates[SCTaskType.NetworkObjectModify].TaskType, Is.EqualTo(nameof(SCTaskType.NetworkObjectModify)));
                Assert.That(ticket.DtoGetter!(settings), Is.EqualTo("ticket"));
                Assert.That(objectTemplate.DtoGetter!(settings), Is.EqualTo("object"));
                Assert.That(Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket).DtoGetter!(settings), Is.Empty);
            }
        }

        [Test]
        public void ShippedDefaults_OnlyExistForTheAccessRequestTicketAndTasks()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket), Does.Contain("@@TASKS@@"));
                Assert.That(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks), Does.Contain("@@SOURCES@@"));
                Assert.That(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ip), Is.Null);
                Assert.That(SCTemplateDefaults.Get(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.Ticket), Is.Null);
            }
        }

        /// <summary>
        /// The upgrade does not store template parts equal to the shipped default, which it compares with the default shipped
        /// up to 9.8.4; the code default must stay the same text, otherwise such installations would change their tickets.
        /// </summary>
        [Test]
        public void ShippedDefaults_EqualTheDefaultTheUpgradeCompares()
        {
            string upgrade = File.ReadAllText(LocateRepositoryFile("roles", "database", "files", "upgrade", "9.8.5.sql"));
            Match literal = Regex.Match(upgrade, @"j_shipped jsonb := \('(?<json>.*?)'::jsonb\)", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.That(literal.Success, Is.True, "The upgrade must compare with the shipped default.");
            List<ExternalTicketSystem> shippedSystems = JsonSerializer.Deserialize<List<ExternalTicketSystem>>(literal.Groups["json"].Value.Replace("''", "'")) ?? [];
            ExternalTicketTemplate shipped = shippedSystems.Single().Templates.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket), Is.EqualTo(shipped.TicketTemplate));
                Assert.That(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks), Is.EqualTo(shipped.TasksTemplate));
            }
        }

        [Test]
        public void FromSettings_UsesOverridesAndTheShippedDefaultForEmptyParts()
        {
            GlobalDeviceSpecificSettings settings = new();
            Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks).DtoSetter!(settings, "own tasks");
            Key(SCTaskType.NetworkObjectModify, SecureChangeTemplatePart.ObjectShort).DtoSetter!(settings, "short");

            List<ExternalTicketTemplate> templates = SCTemplateLookup.FromSettings(settings);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(templates.Select(template => template.TaskType), Is.EqualTo(Enum.GetNames<SCTaskType>()));
                Assert.That(TemplateOf(templates, SCTaskType.AccessRequest).TicketTemplate,
                    Is.EqualTo(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket)));
                Assert.That(TemplateOf(templates, SCTaskType.AccessRequest).TasksTemplate, Is.EqualTo("own tasks"));
                Assert.That(TemplateOf(templates, SCTaskType.AccessRequest).IpTemplate, Is.Empty);
                Assert.That(TemplateOf(templates, SCTaskType.NetworkObjectModify).ObjectTemplateShort, Is.EqualTo("short"));
                Assert.That(TemplateOf(templates, SCTaskType.NetworkObjectModify).TicketTemplate, Is.Empty);
            }
        }

        [Test]
        public void Lookup_FallsBackToGlobalAndComparesByContent()
        {
            List<ExternalTicketTemplate> global = SCTemplateLookup.FromSettings(new GlobalDeviceSpecificSettings());
            GlobalDeviceSpecificSettings changed = new();
            Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ip).DtoSetter!(changed, "ip");
            SCTemplateLookup lookup = new(global, new Dictionary<int, List<ExternalTicketTemplate>>
            {
                [kInheritingManagementId] = SCTemplateLookup.FromSettings(new GlobalDeviceSpecificSettings()),
                [kOverridingManagementId] = SCTemplateLookup.FromSettings(changed)
            });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(lookup.For(null), Is.SameAs(global));
                Assert.That(lookup.For(kUnknownManagementId), Is.SameAs(global));
                Assert.That(TemplateOf(lookup.For(kOverridingManagementId), SCTaskType.AccessRequest).IpTemplate, Is.EqualTo("ip"));
                Assert.That(lookup.HaveSameTemplates(kInheritingManagementId, kUnknownManagementId), Is.True);
                Assert.That(lookup.HaveSameTemplates(null, kUnknownManagementId), Is.True);
                Assert.That(lookup.HaveSameTemplates(kInheritingManagementId, kOverridingManagementId), Is.False);
            }
        }

        [Test]
        public async Task ApiSource_ResolvesGlobalAndManagementOverrides()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            DeviceType deviceType = new() { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
            api.Managements.Add(new Management { Id = kInheritingManagementId, Name = "inheriting", DeviceType = deviceType });
            api.Managements.Add(new Management { Id = kOverridingManagementId, Name = "overriding", DeviceType = deviceType });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kManagementNodeId, DeviceSpecificScopeType.Management, kOverridingManagementId.ToString(), kDeviceTypeNodeId);
            api.SetValue(kGlobalNodeId, Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket), "global ticket");
            api.SetValue(kManagementNodeId, Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket), "management ticket");
            // an empty override on the management restores the shipped default below a global override
            api.SetValue(kGlobalNodeId, Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks), "global tasks");
            api.SetValue(kManagementNodeId, Key(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks), "");

            SCTemplateLookup lookup = await new ApiSCTemplateSource(api).Load(kRequestedManagements);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(TemplateOf(lookup.For(kInheritingManagementId), SCTaskType.AccessRequest).TicketTemplate, Is.EqualTo("global ticket"));
                Assert.That(TemplateOf(lookup.For(kOverridingManagementId), SCTaskType.AccessRequest).TicketTemplate, Is.EqualTo("management ticket"));
                Assert.That(TemplateOf(lookup.For(kUnknownManagementId), SCTaskType.AccessRequest).TicketTemplate, Is.EqualTo("global ticket"));
                Assert.That(TemplateOf(lookup.For(kInheritingManagementId), SCTaskType.AccessRequest).TasksTemplate, Is.EqualTo("global tasks"));
                Assert.That(TemplateOf(lookup.For(kOverridingManagementId), SCTaskType.AccessRequest).TasksTemplate,
                    Is.EqualTo(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Tasks)));
            }
        }

        [Test]
        public async Task ApiSource_LoadsNoManagementsWithoutRequestedOnes()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();

            SCTemplateLookup lookup = await new ApiSCTemplateSource(api).Load(kNoManagements);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(api.Calls.Any(call => call.Query == DeviceQueries.getManagementHierarchy), Is.False);
                Assert.That(TemplateOf(lookup.For(kInheritingManagementId), SCTaskType.AccessRequest).TicketTemplate,
                    Is.EqualTo(SCTemplateDefaults.Get(SCTaskType.AccessRequest, SecureChangeTemplatePart.Ticket)));
            }
        }

        private static string LocateRepositoryFile(params string[] pathParts)
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                FileInfo candidate = new(Path.Combine(directory.FullName, Path.Combine(pathParts)));
                if (candidate.Exists)
                {
                    return candidate.FullName;
                }
                directory = directory.Parent;
            }
            Assert.Ignore("The repository sources are not reachable in this environment.");
            return string.Empty;
        }
    }
}
