using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImplTaskManagementResolverTest
    {
        private const int kSuperManagementId = 20;
        private const int kFirstSubManagementId = 21;
        private const int kSecondSubManagementId = 22;
        private const int kHiddenSubManagementId = 23;
        private const int kStandaloneManagementId = 30;
        private const int kUnknownManagementId = 99;
        private const int kMdsDeviceTypeId = 13;
        private const int kDomainDeviceTypeId = 9;
        private const long kGlobalNodeId = 1;
        private const long kDomainDeviceTypeNodeId = 2;
        private const long kSecondSubManagementNodeId = 3;

        private static readonly List<int> kSuperOnly = [kSuperManagementId];
        private static readonly List<int> kAllSubs = [kFirstSubManagementId, kSecondSubManagementId];
        private static readonly List<int> kSuperAndSecondSub = [kSuperManagementId, kSecondSubManagementId];
        private static readonly List<int> kStandaloneOnly = [kStandaloneManagementId];
        private static readonly List<int> kUnknownOnly = [kUnknownManagementId];

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
        {
            DeviceType mdsType = new() { Id = kMdsDeviceTypeId, Name = "Check Point MDS", Version = "R8x" };
            DeviceType domainType = new() { Id = kDomainDeviceTypeId, Name = "Check Point", Version = "R8x" };
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.Managements.Add(new Management { Id = kSuperManagementId, Name = "mds", DeviceType = mdsType, IsSupermanager = true });
            api.Managements.Add(new Management { Id = kSecondSubManagementId, Name = "domain-b", DeviceType = domainType, SuperManagerId = kSuperManagementId });
            api.Managements.Add(new Management { Id = kFirstSubManagementId, Name = "domain-a", DeviceType = domainType, SuperManagerId = kSuperManagementId });
            api.Managements.Add(new Management { Id = kHiddenSubManagementId, Name = "domain-hidden", DeviceType = domainType, SuperManagerId = kSuperManagementId, HideInUi = true });
            api.Managements.Add(new Management { Id = kStandaloneManagementId, Name = "sms", DeviceType = domainType });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            return api;
        }

        private static async Task<List<int>> Resolve(InMemoryDeviceSpecificSettingsApiConnection api, int managementId, bool isServiceObject = false)
        {
            List<Management> targets = await new ImplTaskManagementResolver(api).ResolveObjectTargets(managementId, isServiceObject);
            return [.. targets.Select(target => target.Id)];
        }

        [Test]
        public async Task ResolveObjectTargets_DefaultsToSuperManagement()
        {
            Assert.That(await Resolve(CreateApi(), kSuperManagementId), Is.EqualTo(kSuperOnly));
        }

        [Test]
        public async Task ResolveObjectTargets_SubmanagerSettingTargetsEveryVisibleSubManagementOrderedByName()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);

            Assert.That(await Resolve(api, kSuperManagementId), Is.EqualTo(kAllSubs));
        }

        [Test]
        public async Task ResolveObjectTargets_RequestOnSubManagementUsesItsSuperManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);

            Assert.That(await Resolve(api, kFirstSubManagementId), Is.EqualTo(kAllSubs));
        }

        [Test]
        public async Task ResolveObjectTargets_HonoursOverrideOnSingleSubManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.AddNode(kDomainDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDomainDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kSecondSubManagementNodeId, DeviceSpecificScopeType.Management, kSecondSubManagementId.ToString(), kDomainDeviceTypeNodeId);
            api.SetValue(kSecondSubManagementNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);

            Assert.That(await Resolve(api, kSuperManagementId), Is.EqualTo(kSuperAndSecondSub));
        }

        [Test]
        public async Task ResolveObjectTargets_ServiceObjectsUseServiceSetting()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);

            List<int> serviceTargets = await Resolve(api, kSuperManagementId, true);
            List<int> addressTargets = await Resolve(api, kSuperManagementId, false);

            Assert.Multiple(() =>
            {
                Assert.That(serviceTargets, Is.EqualTo(kSuperOnly));
                Assert.That(addressTargets, Is.EqualTo(kAllSubs));
            });
        }

        [Test]
        public async Task ResolveObjectTargets_StandaloneManagementIsItsOwnTarget()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.SetValue(kGlobalNodeId, DeviceSpecificSettingKeys.AddressObjectCreation, ProvisioningObjectCreationMode.Submanager);

            Assert.That(await Resolve(api, kStandaloneManagementId), Is.EqualTo(kStandaloneOnly));
        }

        [Test]
        public async Task ResolveObjectTargets_UnknownManagementIsReturnedUnchanged()
        {
            Assert.That(await Resolve(CreateApi(), kUnknownManagementId), Is.EqualTo(kUnknownOnly));
        }

        [Test]
        public async Task ResolveObjectTargets_ReturnsManagementNames()
        {
            List<Management> targets = await new ImplTaskManagementResolver(CreateApi()).ResolveObjectTargets(kSuperManagementId, false);

            Assert.That(targets.Single().Name, Is.EqualTo("mds"));
        }
    }
}
