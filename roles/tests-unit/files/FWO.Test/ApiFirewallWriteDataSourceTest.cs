using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using System.Globalization;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ApiFirewallWriteDataSourceTest
    {
        private static InMemoryDeviceSpecificSettingsApiConnection Api()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.Managements.Add(Management(FirewallVendors.kCheckPointR8x));
            api.AddNode(1, DeviceSpecificScopeType.Global, "global");
            api.AddNode(2, DeviceSpecificScopeType.DeviceType, FirewallVendors.kCheckPointR8x.ToString(CultureInfo.InvariantCulture), 1);
            api.AddNode(3, DeviceSpecificScopeType.Management, kManagementId.ToString(CultureInfo.InvariantCulture), 2);
            api.AddNode(4, DeviceSpecificScopeType.Gateway, kGatewayId.ToString(CultureInfo.InvariantCulture), 3);
            api.SetValue(3, FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.Publish], "POST management-publish");
            api.SetValue(4, FirewallWriteSettingKeys.Endpoints[FirewallWriteOperation.Publish], "POST gateway-publish");
            return api;
        }

        [Test]
        public async Task LoadsManagementGatewayAndGatewaySettings()
        {
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(Api()).LoadContext(Task(WfTaskType.group_delete));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Management.Id, Is.EqualTo(kManagementId));
                Assert.That(context.Gateway?.Id, Is.EqualTo(kGatewayId));
                Assert.That(context.Vendor, Is.EqualTo(FirewallVendor.CheckPoint));
                Assert.That(context.GetEndpoint(FirewallWriteOperation.Publish), Is.EqualTo("POST gateway-publish"));
            }
        }

        [Test]
        public async Task LoadsPackageAndLayerFromTheGatewayRulebaseLinks()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            Device gateway = api.Managements[0].Devices[0];
            gateway.Package = null;
            gateway.LocalRulebase = null;
            gateway.RulebaseLinks = [.. FirewallGatewayRulebasesTest.CheckPointLinks()];
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(api).LoadContext(Task(WfTaskType.group_delete));
            IReadOnlyDictionary<string, FirewallPlaceholderValue> values = ImplTaskStepPlanner.ContextValues(Task(WfTaskType.group_delete), context, null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Rulebases.PolicyName, Is.EqualTo("Standard"));
                Assert.That(context.Rulebases.FirstLayerName, Is.EqualTo("Network"));
                Assert.That(values[FirewallWritePlaceholders.PolicyName].Value, Is.EqualTo("Standard"));
                Assert.That(values[FirewallWritePlaceholders.LayerName].Value, Is.EqualTo("Network"));
            }
        }

        [Test]
        public async Task WithoutGatewayUsesManagementSettings()
        {
            WfImplTask task = Task(WfTaskType.group_delete);
            task.DeviceId = null;
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(Api()).LoadContext(task);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Gateway, Is.Null);
                Assert.That(context.Rulebases, Is.EqualTo(FirewallGatewayRulebases.None));
                Assert.That(context.GetEndpoint(FirewallWriteOperation.Publish), Is.EqualTo("POST management-publish"));
            }
        }

        [Test]
        public void MissingManagementOrForeignGatewayFails()
        {
            WfImplTask withoutManagement = Task(WfTaskType.group_delete);
            withoutManagement.ManagementId = null;
            WfImplTask unknownManagement = Task(WfTaskType.group_delete);
            unknownManagement.ManagementId = 999;
            WfImplTask foreignGateway = Task(WfTaskType.group_delete);
            foreignGateway.DeviceId = 999;
            ApiFirewallWriteDataSource dataSource = new(Api());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(withoutManagement))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(unknownManagement))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(foreignGateway))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
            }
        }
    }
}
