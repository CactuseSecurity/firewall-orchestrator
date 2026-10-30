using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the object creation consistency check with a FortiManager and its ADOMs: the hints follow the targets
    /// that actually get implementation tasks.
    /// </summary>
    [TestFixture]
    internal class ObjectCreationConsistencyCheckTest
    {
        private const int kFortiManagerId = 100;
        private const int kFirstAdomId = 101;
        private const int kSecondAdomId = 102;
        private const int kRootAdomId = 103;
        private const int kStandaloneId = 200;

        private static readonly List<int> kAllAdoms = [kFirstAdomId, kSecondAdomId, kRootAdomId];
        private static readonly List<ObjectTaskKind> kAllKinds = [ObjectTaskKind.Network, ObjectTaskKind.Service, ObjectTaskKind.Time];
        private static readonly List<int> kFortiManagerAndRoot = [kFortiManagerId, kRootAdomId];

        private static List<Management> CreateManagements()
        {
            DeviceType managerType = new() { Id = 12, Name = "FortiManager", Version = "5ff" };
            DeviceType adomType = new() { Id = 11, Name = "FortiADOM", Version = "5ff" };
            return
            [
                new Management { Id = kFortiManagerId, Name = "fortimgr100", DeviceType = managerType, IsSupermanager = true },
                new Management { Id = kFirstAdomId, Name = "fortimgr100_adom1", DeviceType = adomType, SuperManagerId = kFortiManagerId },
                new Management { Id = kSecondAdomId, Name = "fortimgr100_my_adom", DeviceType = adomType, SuperManagerId = kFortiManagerId },
                new Management { Id = kRootAdomId, Name = "fortimgr100_root", DeviceType = adomType, SuperManagerId = kFortiManagerId },
                new Management { Id = kStandaloneId, Name = "fortigate", DeviceType = adomType }
            ];
        }

        /// <summary>
        /// Settings of every management with the given address object creation and implementation mode.
        /// </summary>
        private static Dictionary<int, GlobalDeviceSpecificSettings> CreateSettings(ProvisioningObjectCreationMode managerCreation,
            ProvisioningObjectCreationMode adomCreation)
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = [];
            foreach (Management management in CreateManagements())
            {
                ProvisioningObjectCreationMode creation = management.Id == kFortiManagerId ? managerCreation : adomCreation;
                settings[management.Id] = new GlobalDeviceSpecificSettings { AddressObjectCreation = creation };
            }
            return settings;
        }

        private static List<ObjectCreationFinding> AddressFindings(Dictionary<int, GlobalDeviceSpecificSettings> settings)
        {
            return [.. ObjectCreationConsistencyCheck.Check(CreateManagements(), settings).Where(finding => finding.Kinds.Contains(ObjectTaskKind.Network))];
        }

        private static List<int> Ids(ObjectCreationFinding finding)
        {
            return [.. finding.Managements.Select(management => management.Id)];
        }

        [Test]
        public void ConsistentSettings_GiveNoFinding()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> allSuper = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            Dictionary<int, GlobalDeviceSpecificSettings> allSub = CreateSettings(ProvisioningObjectCreationMode.Submanager, ProvisioningObjectCreationMode.Submanager);

            Assert.Multiple(() =>
            {
                Assert.That(ObjectCreationConsistencyCheck.Check(CreateManagements(), allSuper), Is.Empty);
                Assert.That(ObjectCreationConsistencyCheck.Check(CreateManagements(), allSub), Is.Empty);
            });
        }

        [Test]
        public void SuperSettingDifferentFromAllAdoms_HasNoEffect()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Submanager);

            ObjectCreationFinding finding = AddressFindings(settings).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Type, Is.EqualTo(ObjectCreationFindingType.SuperSettingWithoutEffect));
                Assert.That(finding.SuperManagement.Id, Is.EqualTo(kFortiManagerId));
                Assert.That(Ids(finding), Is.EqualTo(kAllAdoms));
            });
        }

        [Test]
        public void SubmanagerSettingOnSuperWhileAllAdomsPointToIt_HasNoEffect()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Submanager, ProvisioningObjectCreationMode.Supermanager);

            Assert.That(AddressFindings(settings).Single().Type, Is.EqualTo(ObjectCreationFindingType.SuperSettingWithoutEffect));
        }

        [Test]
        public void OneAdomLocal_IsMixedOperation()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kRootAdomId].AddressObjectCreation = ProvisioningObjectCreationMode.Submanager;

            ObjectCreationFinding finding = AddressFindings(settings).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Type, Is.EqualTo(ObjectCreationFindingType.MixedCreation));
                Assert.That(Ids(finding), Is.EquivalentTo(kFortiManagerAndRoot));
            });
        }

        [Test]
        public void MixedOperationWithManualLocalAdom_IsNoLongerMixed()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kRootAdomId].AddressObjectCreation = ProvisioningObjectCreationMode.Submanager;
            settings[kRootAdomId].ImplementationMode = ProvisioningImplementationMode.Manual;

            Assert.That(AddressFindings(settings), Is.Empty);
        }

        [Test]
        public void AdomsPointingToManualSuperManagement_GiveNoFinding()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kFortiManagerId].ImplementationMode = ProvisioningImplementationMode.Manual;

            Assert.That(ObjectCreationConsistencyCheck.Check(CreateManagements(), settings), Is.Empty,
                "the mode of the ADOMs has no effect when they create on the FortiManager, so this is intended");
        }

        [Test]
        public void KindsWithTheSameFinding_AreCombinedIntoOneFinding()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Submanager);
            foreach (int adomId in kAllAdoms)
            {
                settings[adomId].ServiceObjectCreation = ProvisioningObjectCreationMode.Submanager;
                settings[adomId].TimeObjectCreation = ProvisioningObjectCreationMode.Submanager;
            }

            ObjectCreationFinding finding = ObjectCreationConsistencyCheck.Check(CreateManagements(), settings).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Type, Is.EqualTo(ObjectCreationFindingType.SuperSettingWithoutEffect));
                Assert.That(finding.Kinds, Is.EqualTo(kAllKinds));
                Assert.That(Ids(finding), Is.EqualTo(kAllAdoms));
            });
        }

        [Test]
        public void ManualAdomsCreatingLocally_GiveNoFinding()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Submanager);
            foreach (int adomId in kAllAdoms)
            {
                settings[adomId].ImplementationMode = ProvisioningImplementationMode.Manual;
            }

            Assert.That(AddressFindings(settings), Is.Empty, "nothing is created at all, which is intended");
        }

        [Test]
        public void EveryKindIsCheckedOnItsOwn()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kFirstAdomId].ServiceObjectCreation = ProvisioningObjectCreationMode.Submanager;

            List<ObjectCreationFinding> findings = ObjectCreationConsistencyCheck.Check(CreateManagements(), settings);

            Assert.Multiple(() =>
            {
                Assert.That(findings.Single().Kinds.Single(), Is.EqualTo(ObjectTaskKind.Service));
                Assert.That(findings.Single().Type, Is.EqualTo(ObjectCreationFindingType.MixedCreation));
            });
        }
    }
}
