using FWO.Config.Api;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FirewallWriteDefaultsTest
    {
        private const int kGuardicoreDeviceTypeId = 31;
        private const string kListValue = "[\"a\",\"b\"]";
        private const string kNameObjectsValue = "[{\"name\":\"a\"}]";

        private static readonly List<int> kSupportedDeviceTypes =
        [
            FirewallVendors.kCheckPointR8x, FirewallVendors.kCheckPointMds, FirewallVendors.kFortiGate5ff, FirewallVendors.kFortiAdom5ff,
            FirewallVendors.kFortiManager5ff, FirewallVendors.kFortiOsManagement, FirewallVendors.kFortiOsGateway, FirewallVendors.kOpnSense,
            FirewallVendors.kAzure, FirewallVendors.kAzureFirewall
        ];

        /// <summary>Operations every vendor family ships, besides the session steps and services that not all vendors have.</summary>
        private static readonly List<FirewallWriteOperation> kCommonOperations =
        [
            FirewallWriteOperation.HostCreate, FirewallWriteOperation.HostModify, FirewallWriteOperation.HostDelete,
            FirewallWriteOperation.NetworkCreate, FirewallWriteOperation.NetworkModify, FirewallWriteOperation.NetworkDelete,
            FirewallWriteOperation.AddressRangeCreate, FirewallWriteOperation.AddressRangeModify, FirewallWriteOperation.AddressRangeDelete,
            FirewallWriteOperation.GroupCreate, FirewallWriteOperation.GroupAddMembers, FirewallWriteOperation.GroupRemoveMembers,
            FirewallWriteOperation.GroupDelete, FirewallWriteOperation.RuleCreate, FirewallWriteOperation.RuleModify, FirewallWriteOperation.RuleDelete
        ];

        private static readonly List<string> kListPlaceholders =
        [
            FirewallWritePlaceholders.Members, FirewallWritePlaceholders.MembersToAdd, FirewallWritePlaceholders.MembersToRemove,
            FirewallWritePlaceholders.MemberAddresses, FirewallWritePlaceholders.ObjectAddresses, FirewallWritePlaceholders.RuleSources,
            FirewallWritePlaceholders.RuleDestinations, FirewallWritePlaceholders.RuleServices, FirewallWritePlaceholders.RuleSourcesToAdd,
            FirewallWritePlaceholders.RuleSourcesToRemove, FirewallWritePlaceholders.RuleDestinationsToAdd, FirewallWritePlaceholders.RuleDestinationsToRemove,
            FirewallWritePlaceholders.RuleServicesToAdd, FirewallWritePlaceholders.RuleServicesToRemove, FirewallWritePlaceholders.RuleSourceAddresses,
            FirewallWritePlaceholders.RuleDestinationAddresses, FirewallWritePlaceholders.RuleServicePorts, FirewallWritePlaceholders.RuleServiceProtocols
        ];

        private static readonly List<string> kNameObjectPlaceholders =
        [
            FirewallWritePlaceholders.MembersNameObjects, FirewallWritePlaceholders.RuleSourcesNameObjects,
            FirewallWritePlaceholders.RuleDestinationsNameObjects, FirewallWritePlaceholders.RuleServicesNameObjects
        ];

        /// <summary>A step with a sample value for every placeholder the planner can set.</summary>
        private static FirewallWriteStep FullStep(FirewallWriteOperation operation)
        {
            FirewallWriteStep step = new(operation) { TargetName = "target" };
            IEnumerable<string> placeholders = typeof(FirewallWritePlaceholders).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral).Select(field => (string)field.GetValue(null)!)
                .Where(placeholder => !FirewallWritePlaceholders.RuntimePlaceholders.Contains(placeholder));
            foreach (string placeholder in placeholders)
            {
                if (kListPlaceholders.Contains(placeholder))
                {
                    step.SetJson(placeholder, kListValue);
                }
                else if (kNameObjectPlaceholders.Contains(placeholder))
                {
                    step.SetJson(placeholder, kNameObjectsValue);
                }
                else if (placeholder == FirewallWritePlaceholders.RulePosition)
                {
                    step.SetJson(placeholder, FirewallRulePlacement.EndOfRulebase.ToJson());
                }
                else
                {
                    step.SetText(placeholder, placeholder == FirewallWritePlaceholders.MaskLength ? "24" : "value");
                }
            }
            return step;
        }

        private static IEnumerable<TestCaseData> DefaultCases() =>
            from deviceTypeId in kSupportedDeviceTypes
            from operation in Enum.GetValues<FirewallWriteOperation>()
            where FirewallWriteDefaults.Get(deviceTypeId, operation) != null
            select new TestCaseData(deviceTypeId, operation);

        [TestCaseSource(nameof(DefaultCases))]
        public void EveryShippedDefaultRendersToAValidRequest(int deviceTypeId, FirewallWriteOperation operation)
        {
            FirewallWriteDefault shipped = FirewallWriteDefaults.Get(deviceTypeId, operation)!;
            RenderedFirewallRequest request = FirewallTemplateRenderer.Render(FullStep(operation), shipped.Endpoint, shipped.Template);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallWriteEndpoint.AllowedMethods, Does.Contain(request.Method));
                Assert.That(request.Path, Does.Not.Contain("@@VALUE"));
                if (request.Body != null && FirewallWritePlaceholders.RuntimePlaceholders.All(placeholder => !request.Body.Contains(placeholder)))
                {
                    Assert.That(JsonNode.Parse(request.Body), Is.Not.Null);
                }
            }
        }

        private static IEnumerable<TestCaseData> Ipv6DefaultCases() =>
            from deviceTypeId in kSupportedDeviceTypes
            from operation in Enum.GetValues<FirewallWriteOperation>()
            where FirewallWriteDefaults.Get(deviceTypeId, operation, true) != null
            select new TestCaseData(deviceTypeId, operation);

        /// <summary>IPv6 objects have no dotted netmask, so the defaults used for them must not need one.</summary>
        [TestCaseSource(nameof(Ipv6DefaultCases))]
        public void EveryShippedDefaultForIpv6ObjectsRendersWithoutNetmask(int deviceTypeId, FirewallWriteOperation operation)
        {
            FirewallWriteDefault shipped = FirewallWriteDefaults.Get(deviceTypeId, operation, true)!;
            FirewallWriteStep step = FullStep(operation).SetMissing(FirewallWritePlaceholders.Netmask, "IPv6");
            Assert.DoesNotThrow(() => FirewallTemplateRenderer.Render(step, shipped.Endpoint, shipped.Template));
        }

        [TestCase(FirewallVendors.kFortiOsGateway, "/api/v2/cmdb/firewall/address6")]
        [TestCase(FirewallVendors.kFortiManager5ff, "POST /jsonrpc")]
        public void FortinetShipsIpv6Objects(int deviceTypeId, string expectedEndpointPart)
        {
            FirewallWriteDefault ipv6 = FirewallWriteDefaults.Get(deviceTypeId, FirewallWriteOperation.NetworkCreate, true)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ipv6.Endpoint, Does.Contain(expectedEndpointPart));
                Assert.That(ipv6.Endpoint + ipv6.Template, Does.Contain("address6"));
                Assert.That(FirewallWriteDefaults.Get(deviceTypeId, FirewallWriteOperation.NetworkCreate)!.Template, Does.Not.Contain("ip6"));
                Assert.That(FirewallWriteDefaults.Get(deviceTypeId, FirewallWriteOperation.GroupCreate, true),
                    Is.EqualTo(FirewallWriteDefaults.Get(deviceTypeId, FirewallWriteOperation.GroupCreate)));
            }
        }

        [Test]
        public void Ipv6FallsBackToTheCommonDefaultForOtherVendors()
        {
            Assert.That(FirewallWriteDefaults.Get(FirewallVendors.kCheckPointR8x, FirewallWriteOperation.HostCreate, true),
                Is.EqualTo(FirewallWriteDefaults.Get(FirewallVendors.kCheckPointR8x, FirewallWriteOperation.HostCreate)));
        }

        [TestCaseSource(nameof(kSupportedDeviceTypes))]
        public void EverySupportedDeviceTypeShipsObjectGroupAndRuleOperations(int deviceTypeId)
        {
            foreach (FirewallWriteOperation operation in kCommonOperations)
            {
                Assert.That(FirewallWriteDefaults.GetEndpoint(deviceTypeId, operation), Is.Not.Null.And.Not.Empty, $"{deviceTypeId} {operation}");
            }
        }

        [Test]
        public void VendorSpecificSessionAndServiceDefaults()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(FirewallWriteDefaults.GetEndpoint(FirewallVendors.kCheckPointMds, FirewallWriteOperation.Publish), Is.EqualTo("POST publish"));
                Assert.That(FirewallWriteDefaults.GetEndpoint(FirewallVendors.kOpnSense, FirewallWriteOperation.InstallPolicy), Is.EqualTo("POST /api/firewall/filter/apply"));
                Assert.That(FirewallWriteDefaults.GetEndpoint(FirewallVendors.kFortiManager5ff, FirewallWriteOperation.InstallPolicy), Is.EqualTo("POST /jsonrpc"));
                Assert.That(FirewallWriteDefaults.Get(FirewallVendors.kFortiOsGateway, FirewallWriteOperation.Publish), Is.Null);
                Assert.That(FirewallWriteDefaults.Get(FirewallVendors.kAzure, FirewallWriteOperation.ServiceCreate), Is.Null);
                Assert.That(FirewallWriteDefaults.GetTemplate(FirewallVendors.kFortiOsGateway, FirewallWriteOperation.HostDelete), Is.Empty);
                Assert.That(FirewallWriteDefaults.Get(kGuardicoreDeviceTypeId, FirewallWriteOperation.HostCreate), Is.Null);
                Assert.That(FirewallVendors.FromDeviceTypeId(kGuardicoreDeviceTypeId), Is.EqualTo(FirewallVendor.Unsupported));
                Assert.That(FirewallVendors.SupportedDeviceTypeIds, Is.EquivalentTo(kSupportedDeviceTypes));
            }
        }

        [Test]
        public void Context_PrefersConfiguredValuesOverShippedDefaults()
        {
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteEndpoints[FirewallWriteOperation.HostCreate] = "POST custom-host";
            settings.FwWriteTemplates[FirewallWriteOperation.HostCreate] = "{\"n\":\"@@OBJECT_NAME@@\"}";
            settings.FwWriteEndpoints[FirewallWriteOperation.HostModify] = " ";
            FirewallWriteContext context = FirewallWriteTestSupport.Context(FirewallVendors.kCheckPointR8x, settings);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.GetEndpoint(FirewallWriteOperation.HostCreate), Is.EqualTo("POST custom-host"));
                Assert.That(context.GetTemplate(FirewallWriteOperation.HostCreate), Is.EqualTo("{\"n\":\"@@OBJECT_NAME@@\"}"));
                Assert.That(context.GetEndpoint(FirewallWriteOperation.HostModify), Is.EqualTo("POST set-host"));
                Assert.That(context.GetTemplate(FirewallWriteOperation.HostModify), Does.Contain("ip-address"));
            }
        }

        /// <summary>Endpoints resolve Global, device type, management, gateway, the most specific override winning.</summary>
        [Test]
        public async Task EndpointResolutionHonoursTheHierarchy()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.AddNode(1, DeviceSpecificScopeType.Global, "global");
            api.AddNode(2, DeviceSpecificScopeType.DeviceType, FirewallVendors.kCheckPointR8x.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            api.AddNode(3, DeviceSpecificScopeType.Management, FirewallWriteTestSupport.kManagementId.ToString(System.Globalization.CultureInfo.InvariantCulture), 2);
            api.AddNode(4, DeviceSpecificScopeType.Gateway, FirewallWriteTestSupport.kGatewayId.ToString(System.Globalization.CultureInfo.InvariantCulture), 3);
            api.SetValue(2, FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate], "POST device-type-host");
            api.SetValue(4, FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.HostCreate], "POST gateway-host");
            api.SetValue(3, FirewallWriteSettingKeys.Templates[FirewallWriteOperation.GroupCreate], "{\"g\":\"@@GROUP_NAME@@\"}");
            api.SetValue(3, FirewallWriteSettingKeys.CheckCertificates, false);
            api.SetValue(1, FirewallWriteSettingKeys.InstallPolicy, true);

            Management management = FirewallWriteTestSupport.Management(FirewallVendors.kCheckPointR8x);
            DeviceSpecificScopePath managementPath = DeviceSpecificScopePath.ForManagement(management);
            DeviceSpecificScopePath gatewayPath = DeviceSpecificScopePath.ForGateway(management, management.Devices[0]);
            List<DeviceSpecificScopePath> paths = [managementPath, gatewayPath];
            List<GlobalDeviceSpecificSettings> settings = await new DeviceSpecificSettingsManager(api).LoadEffectiveSettingsAsync(paths);
            FirewallWriteContext managementContext = new() { Management = management, Settings = settings[0] };
            FirewallWriteContext gatewayContext = new() { Management = management, Gateway = management.Devices[0], Settings = settings[1] };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(managementContext.GetEndpoint(FirewallWriteOperation.HostCreate), Is.EqualTo("POST device-type-host"));
                Assert.That(gatewayContext.GetEndpoint(FirewallWriteOperation.HostCreate), Is.EqualTo("POST gateway-host"));
                Assert.That(gatewayContext.GetTemplate(FirewallWriteOperation.GroupCreate), Is.EqualTo("{\"g\":\"@@GROUP_NAME@@\"}"));
                Assert.That(gatewayContext.GetEndpoint(FirewallWriteOperation.GroupCreate), Is.EqualTo("POST add-group"));
                Assert.That(gatewayContext.Settings.FwWriteCheckCertificates, Is.False);
                Assert.That(gatewayContext.Settings.FwWriteInstallPolicy, Is.True);
                Assert.That(new GlobalDeviceSpecificSettings().FwWriteCheckCertificates, Is.True);
            }
        }
    }
}
