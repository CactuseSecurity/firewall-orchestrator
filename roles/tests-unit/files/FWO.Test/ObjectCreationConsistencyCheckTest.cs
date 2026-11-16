using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the object creation consistency check with a FortiManager and its ADOMs: a sub-management whose setting
    /// differs from the one of its super-management is reported, unless it is set to ignore.
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
        private static readonly List<int> kRootAdomOnly = [kRootAdomId];
        private static readonly List<ObjectTaskKind> kAllKinds = [ObjectTaskKind.Network, ObjectTaskKind.Service, ObjectTaskKind.Time];

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
        /// Settings of every management with the given creation setting for all kinds of objects.
        /// </summary>
        private static Dictionary<int, GlobalDeviceSpecificSettings> CreateSettings(ProvisioningObjectCreationMode managerCreation,
            ProvisioningObjectCreationMode adomCreation)
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = [];
            foreach (Management management in CreateManagements())
            {
                ProvisioningObjectCreationMode creation = management.Id == kFortiManagerId ? managerCreation : adomCreation;
                settings[management.Id] = new GlobalDeviceSpecificSettings
                {
                    AddressObjectCreation = creation,
                    ServiceObjectCreation = creation,
                    TimeObjectCreation = creation
                };
            }
            return settings;
        }

        private static List<ObjectCreationFinding> Check(Dictionary<int, GlobalDeviceSpecificSettings> settings)
        {
            return ObjectCreationConsistencyCheck.Check(CreateManagements(), settings);
        }

        private static List<int> Ids(ObjectCreationFinding finding)
        {
            return [.. finding.Managements.Select(management => management.Id)];
        }

        [TestCase(ProvisioningObjectCreationMode.Supermanager)]
        [TestCase(ProvisioningObjectCreationMode.Submanager)]
        public void SameSettingOnSuperAndSubs_GivesNoFinding(ProvisioningObjectCreationMode mode)
        {
            Assert.That(Check(CreateSettings(mode, mode)), Is.Empty);
        }

        [TestCase(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Submanager)]
        [TestCase(ProvisioningObjectCreationMode.Submanager, ProvisioningObjectCreationMode.Supermanager)]
        public void SubsDifferingFromSuper_AreReportedForAllKindsInOneFinding(ProvisioningObjectCreationMode managerCreation,
            ProvisioningObjectCreationMode adomCreation)
        {
            ObjectCreationFinding finding = Check(CreateSettings(managerCreation, adomCreation)).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.SuperManagement.Id, Is.EqualTo(kFortiManagerId));
                Assert.That(Ids(finding), Is.EqualTo(kAllAdoms));
                Assert.That(finding.Kinds, Is.EqualTo(kAllKinds));
            });
        }

        [Test]
        public void SingleDifferingSub_IsReportedAlone()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kRootAdomId].AddressObjectCreation = ProvisioningObjectCreationMode.Submanager;

            ObjectCreationFinding finding = Check(settings).Single();

            Assert.Multiple(() =>
            {
                Assert.That(Ids(finding), Is.EqualTo(kRootAdomOnly));
                Assert.That(finding.Kinds.Single(), Is.EqualTo(ObjectTaskKind.Network), "every kind is checked on its own");
            });
        }

        [TestCase(ProvisioningObjectCreationMode.Supermanager)]
        [TestCase(ProvisioningObjectCreationMode.Submanager)]
        [TestCase(ProvisioningObjectCreationMode.Ignore)]
        public void SubsSetToIgnore_AreNotReported(ProvisioningObjectCreationMode managerCreation)
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(managerCreation, ProvisioningObjectCreationMode.Supermanager);
            foreach (int adomId in kAllAdoms)
            {
                settings[adomId].TimeObjectCreation = ProvisioningObjectCreationMode.Ignore;
            }

            List<ObjectCreationFinding> timeFindings = [.. Check(settings).Where(finding => finding.Kinds.Contains(ObjectTaskKind.Time))];

            Assert.That(timeFindings, Is.Empty, "a sub-management set to ignore wants no objects at all");
        }

        [Test]
        public void SuperSetToIgnore_ReportsSubsCreatingObjects()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kFortiManagerId].TimeObjectCreation = ProvisioningObjectCreationMode.Ignore;

            ObjectCreationFinding finding = Check(settings).Single();

            Assert.Multiple(() =>
            {
                Assert.That(finding.Kinds.Single(), Is.EqualTo(ObjectTaskKind.Time));
                Assert.That(Ids(finding), Is.EqualTo(kAllAdoms));
            });
        }

        [Test]
        public void UndefinedSetting_CountsAsSupermanager()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Undefined, ProvisioningObjectCreationMode.Supermanager);

            Assert.That(Check(settings), Is.Empty);
        }

        [Test]
        public void ImplementationModeDoesNotHideADifference()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Submanager);
            foreach (GlobalDeviceSpecificSettings managementSettings in settings.Values)
            {
                managementSettings.ImplementationMode = ProvisioningImplementationMode.Manual;
            }

            Assert.That(Check(settings), Has.Count.EqualTo(1), "the setting differs wherever the task is ordered");
        }

        [Test]
        public void ManagementsWithoutSubManagements_GiveNoFinding()
        {
            Dictionary<int, GlobalDeviceSpecificSettings> settings = CreateSettings(ProvisioningObjectCreationMode.Supermanager, ProvisioningObjectCreationMode.Supermanager);
            settings[kStandaloneId].AddressObjectCreation = ProvisioningObjectCreationMode.Submanager;

            Assert.That(Check(settings), Is.Empty);
        }
    }
}
