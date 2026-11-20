using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Modelling;
using FWO.Services.Workflow;
using FWO.Data.Report;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Variance analysis of a Guardicore management: modelled app roles, app zones and network areas are compared with
    /// their labels by address, missing, changed and obsolete labels are requested, and excluded object types are left out.
    /// </summary>
    [TestFixture]
    internal class ModellingVarianceAnalysisLabelsTest
    {
        private const int kCheckPointManagementTypeId = 9;
        private const string kExternalManagementData = "{\"id\":\"1\",\"name\":\"CheckpointExt\"}";
        private const string kRecognitionOption = "{\"nwRegardIp\":true,\"nwRegardName\":false,\"nwSeparateGroupAnalysis\":true,\"svcRegardPortAndProt\":true}";

        private static readonly SimulatedUserConfig UserConfig = new()
        {
            ModNamingConvention = "{\"networkAreaRequired\":false,\"fixedPartLength\":4,\"freePartLength\":5,\"networkAreaPattern\":\"NA\",\"appRolePattern\":\"AR\",\"applicationZone\":\"AZ\"}",
            ModRolloutResolveServiceGroups = true,
            CreateAppZones = true,
            RuleRecognitionOption = kRecognitionOption
        };
        private static readonly FwoOwner Application = new() { Id = 1, Name = "App1", ExtAppId = "4711" };
        private static readonly ExtStateTestApiConn ExtStateApiConnection = new();
        private static readonly List<string> kExcludedAppRoles = [nameof(ModellingTypes.ModObjectType.AppRole)];
        private static readonly List<string> kMissingAppRoles = ["AR504711-002"];
        private static readonly List<string> kExcludedZonesAndAreas = [nameof(ModellingTypes.ModObjectType.AppZone), nameof(ModellingTypes.ModObjectType.NetworkArea)];
        private static readonly List<string> kObsoleteLabels = ["AppRole: AR504711-009", "AppZone: AZ0815", "NetworkArea: NA60"];
        private static readonly List<string> kObsoleteAppRoleLabels = ["AppRole: AR504711-009"];

        private static readonly ModellingAppRole UnchangedAppRole = AppRole(1, "AR504711-001",
            GuardicoreVarianceTestApiConn.AppServer1, GuardicoreVarianceTestApiConn.AppServerRange);
        private static readonly ModellingAppRole MissingAppRole = AppRole(2, "AR504711-002", GuardicoreVarianceTestApiConn.AppServer3);
        private static readonly ModellingAppRole ChangedAppRole = AppRole(3, "AR504711-003", GuardicoreVarianceTestApiConn.AppServer1);
        private static readonly ModellingNetworkArea Area = new()
        {
            Id = 50,
            Name = "Area50",
            IdString = "NA50",
            IpData = [new() { Content = new() { Id = 1, Name = "Net50", Ip = "10.50.0.0/16", IpEnd = "10.50.255.255/32" } }]
        };
        private static readonly ModellingConnection Connection = new()
        {
            Id = 1,
            Name = "Conn1",
            SourceAppRoles = [new() { Content = UnchangedAppRole }, new() { Content = ChangedAppRole }],
            SourceAreas = [new() { Content = Area }],
            DestinationAppRoles = [new() { Content = MissingAppRole }],
            Services = [new() { Content = new() { Id = 1, Name = "Service1", Port = 443, ProtoId = 6 } }]
        };
        private static readonly List<ModellingConnection> Connections = [Connection];
        private static readonly List<ModellingConnection> NoConnections = [];

        private static ModellingAppRole AppRole(long id, string idString, params ModellingAppServer[] appServers) =>
            new() { Id = id, Name = idString, IdString = idString, AppId = Application.Id, AppServers = [.. appServers.Select(server => new ModellingAppServerWrapper { Content = server })] };

        private static ModellingVarianceAnalysis Analysis(GuardicoreVarianceTestApiConn api) =>
            new(api, new ExtStateHandler(ExtStateApiConnection), UserConfig, Application, DefaultInit.DoNothing);

        private static GuardicoreVarianceTestApiConn Api(bool implementAreas = true, bool deleteObjects = true)
        {
            GuardicoreVarianceTestApiConn api = new();
            api.Settings.SetValue(1, DeviceSpecificSettingKeys.ModellingImplementNetworkAreaObjects, implementAreas);
            api.Settings.SetValue(1, FirewallWriteSettingKeys.DeleteObjects, deleteObjects);
            return api;
        }

        private static WfReqTask Task(List<WfReqTask> tasks, string title) =>
            tasks.SingleOrDefault(task => task.Title == title) ?? throw new AssertionException($"No task '{title}' in: {string.Join(" | ", tasks.Select(task => task.Title))}");

        private static List<string> DeletedLabels(List<WfReqTask> tasks) =>
            [.. tasks.Where(task => task.TaskType == nameof(WfTaskType.group_delete)).Select(task => task.Elements.Single().GroupName ?? "")];

        [Test]
        public async Task Request_WritesToHostBasedManagementWithoutExternalManagementData()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ManagementExtMgtData = null;

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            Assert.That(Task(tasks, "New AppRole: AR504711-002").OnManagement?.Id, Is.EqualTo(GuardicoreVarianceTestApiConn.kManagementId));
        }

        [Test]
        public async Task Request_SkipsOtherManagementWithoutExternalManagementData()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ManagementDeviceTypeId = kCheckPointManagementTypeId;
            api.ManagementExtMgtData = null;

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            Assert.That(tasks, Is.Empty);
        }

        [Test]
        public async Task Request_WritesToOtherManagementWithExternalManagementData()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ManagementDeviceTypeId = kCheckPointManagementTypeId;
            api.ManagementExtMgtData = kExternalManagementData;

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            Assert.That(tasks, Is.Not.Empty);
        }

        [Test]
        public async Task Request_ComparesLabelsByTheirAddresses()
        {
            List<WfReqTask> tasks = await Analysis(Api()).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask removeMembers = Task(tasks, "Update AppRole: AR504711-003: Remove Members");
            Assert.Multiple(() =>
            {
                Assert.That(tasks.Exists(task => task.Title.Contains("AR504711-001")), Is.False, "same addresses in other notation");
                Assert.That(Task(tasks, "New AppRole: AR504711-002").TaskType, Is.EqualTo(nameof(WfTaskType.group_create)));
                Assert.That(removeMembers.Elements.Single(element => element.RequestAction == nameof(RequestAction.delete)).IpString, Is.EqualTo("9.9.9.9/32"));
                Assert.That(tasks.Exists(task => task.Title.Contains("AZ4711")), Is.False, "the app zone label has all app servers");
            });
        }

        [Test]
        public async Task Request_ChangesTheNetworkAreaLabelWithAllItsMembersInOneTask()
        {
            List<WfReqTask> tasks = await Analysis(Api()).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask area = Task(tasks, "Update NetworkArea: NA50");
            Assert.Multiple(() =>
            {
                Assert.That(area.TaskType, Is.EqualTo(nameof(WfTaskType.group_modify)));
                Assert.That(area.ManagementId, Is.EqualTo(GuardicoreVarianceTestApiConn.kManagementId));
                Assert.That(area.Elements.Single(element => element.RequestAction == nameof(RequestAction.create)).IpString, Is.EqualTo("10.50.0.0/16"));
                Assert.That(area.Elements.Single(element => element.RequestAction == nameof(RequestAction.unchanged)).IpString, Is.EqualTo("10.50.0.0/24"));
                Assert.That(area.Elements.TrueForAll(element => element.GroupName == "NA50"), Is.True);
            });
        }

        [Test]
        public async Task Request_LeavesNetworkAreasAloneUnlessTheyAreImplemented()
        {
            List<WfReqTask> tasks = await Analysis(Api(false)).AnalyseModelledConnectionsForRequest(Connections);
            Assert.That(tasks.Exists(task => task.Title.Contains("NetworkArea")), Is.False);
        }

        [Test]
        public async Task Request_DeletesObsoleteLabelsRequestedForTheOwner()
        {
            List<WfReqTask> tasks = await Analysis(Api()).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask obsolete = Task(tasks, "Delete obsolete label: AppRole: AR504711-009");
            List<string> deletedLabels = DeletedLabels(tasks);
            Assert.Multiple(() =>
            {
                Assert.That(obsolete.Elements.Single().GroupName, Is.EqualTo("AppRole: AR504711-009"));
                Assert.That(deletedLabels, Is.EqualTo(kObsoleteLabels), "app role, app zone and network area labels, in this order");
                Assert.That(tasks.IndexOf(obsolete), Is.EqualTo(tasks.Count - kObsoleteLabels.Count), "deletions come last");
                Assert.That(tasks.Exists(task => task.Title.Contains("Stage: Prod")), Is.False, "labels never requested by FWO stay");
            });
        }

        [Test]
        public async Task Request_DeletesNoObsoleteLabelsWhileObjectDeletionIsOff()
        {
            List<WfReqTask> tasks = await Analysis(Api(deleteObjects: false)).AnalyseModelledConnectionsForRequest(Connections);

            Assert.That(DeletedLabels(tasks), Is.Empty, "the writer would skip the deletion, so every run would request it again");
        }

        [Test]
        public async Task Request_KeepsLabelsOfTypesThatAreNotImplemented()
        {
            GuardicoreVarianceTestApiConn api = Api(false);
            api.Settings.SetValue(1, DeviceSpecificSettingKeys.ModellingCreateAppZones, false);
            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            List<string> deletedLabels = DeletedLabels(tasks);
            Assert.That(deletedLabels, Is.EqualTo(kObsoleteAppRoleLabels));
        }

        [Test]
        public async Task Request_KeepsLabelsOfExcludedTypes()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.Settings.SetValue(3, DeviceSpecificSettingKeys.ProvisioningExcludedObjectTypes, kExcludedZonesAndAreas);
            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            List<string> deletedLabels = DeletedLabels(tasks);
            Assert.That(deletedLabels, Is.EqualTo(kObsoleteAppRoleLabels));
        }

        [Test]
        public async Task Request_LeavesOutExcludedObjectTypes()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.Settings.SetValue(3, DeviceSpecificSettingKeys.ProvisioningExcludedObjectTypes, kExcludedAppRoles);
            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            Assert.Multiple(() =>
            {
                Assert.That(tasks.Exists(task => task.Title.Contains("AppRole")), Is.False);
                Assert.That(tasks.Exists(task => task.Title.Contains("NetworkArea")), Is.True);
            });
        }

        [Test]
        public async Task Report_ShowsMissingAndDifferingLabels()
        {
            ModellingVarianceResult result = await Analysis(Api()).AnalyseRulesVsModelledConnections(Connections, new ModellingFilter());

            Assert.Multiple(() =>
            {
                Assert.That(result.MissingAppRoles[GuardicoreVarianceTestApiConn.kManagementId].Select(appRole => appRole.IdString), Is.EqualTo(kMissingAppRoles));
                ModellingAppRole differing = result.DifferingAppRoles[GuardicoreVarianceTestApiConn.kManagementId].Single();
                Assert.That(differing.IdString, Is.EqualTo("AR504711-003"));
                Assert.That(differing.SurplusAppServers.Single().Content.Ip, Is.EqualTo("9.9.9.9/32"));
            });
        }

        [Test]
        public async Task AppZoneCheck_ComparesTheLabelByAddresses()
        {
            ModellingVarianceAnalysis analysis = Analysis(Api());
            await analysis.AnalyseModelledConnectionsForRequest(NoConnections);

            (bool found, List<ModellingAppZone?> managementsWithVariance) = await analysis.CheckExistingAppZone();
            Assert.Multiple(() =>
            {
                Assert.That(found, Is.True);
                Assert.That(managementsWithVariance, Is.Empty);
            });
        }
    }
}
