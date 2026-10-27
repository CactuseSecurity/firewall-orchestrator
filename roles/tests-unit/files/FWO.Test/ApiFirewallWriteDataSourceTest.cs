using FWO.Basics.Exceptions;
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
        public async Task WithoutGatewayUsesManagementSettings()
        {
            WfImplTask task = Task(WfTaskType.group_delete);
            task.DeviceId = null;
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(Api()).LoadContext(task);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Gateway, Is.Null);
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
                Assert.ThrowsAsync<ConfigException>(() => dataSource.LoadContext(withoutManagement));
                Assert.ThrowsAsync<ConfigException>(() => dataSource.LoadContext(unknownManagement));
                Assert.ThrowsAsync<ConfigException>(() => dataSource.LoadContext(foreignGateway));
            }
        }
    }
}
