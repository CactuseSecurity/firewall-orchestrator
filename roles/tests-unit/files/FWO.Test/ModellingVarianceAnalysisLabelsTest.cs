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
        private static readonly ModellingService Dns = new() { Id = 2, Name = "Dns", Port = 53, PortEnd = 53, ProtoId = 17 };
        private static readonly List<ModellingConnection> ConnectionsWithServiceGroup =
        [
            new()
            {
                Id = 2,
                AppId = Application.Id,
                Name = "Conn2",
                SourceAppRoles = [new() { Content = UnchangedAppRole }],
                DestinationAppRoles = [new() { Content = MissingAppRole }],
                ServiceGroups = [new() { Content = new ModellingServiceGroup { Id = 7, Name = "DnsGroup", AppId = Application.Id, Services = [new() { Content = Dns }] } }]
            }
        ];
        private static readonly List<ModellingConnection> NoConnections = [];

        private static ModellingAppRole AppRole(long id, string idString, params ModellingAppServer[] appServers) =>
            new() { Id = id, Name = idString, IdString = idString, AppId = Application.Id, AppServers = [.. appServers.Select(server => new ModellingAppServerWrapper { Content = server })] };

        private static ModellingVarianceAnalysis Analysis(GuardicoreVarianceTestApiConn api) =>
            new(api, new ExtStateHandler(ExtStateApiConnection), UserConfig, Application, DefaultInit.DoNothing);

        private static ModellingVarianceAnalysis LabelModellingAnalysis(GuardicoreVarianceTestApiConn api) =>
            new(api, new ExtStateHandler(ExtStateApiConnection), new SimulatedUserConfig
            {
                ModNamingConvention = UserConfig.ModNamingConvention,
                CreateAppZones = true,
                RuleRecognitionOption = kRecognitionOption,
                LabelBasedModelling = true
            }, Application, DefaultInit.DoNothing);

        /// <summary>A connection whose source is (Stage: Prod AND OS: Linux) only.</summary>
        private static List<ModellingConnection> LabelOnlySourceConnections() =>
        [
            new()
            {
                Id = 3,
                AppId = Application.Id,
                Name = "Conn3",
                SourceLabels =
                [
                    new() { AndGroup = 0, Label = new() { Value = "Prod", Key = new() { Name = "Stage" } } },
                    new() { AndGroup = 0, Label = new() { Value = "Linux", Key = new() { Name = "OS" } } }
                ],
                DestinationAppRoles = [new() { Content = UnchangedAppRole }],
                Services = [new() { Content = new() { Id = 1, Name = "Service1", Port = 443, ProtoId = 6 } }]
            }
        ];

        private static readonly List<string?> kLabelGroupSource = ["OS: Linux & Stage: Prod"];
        private static readonly List<string?> kLabelGroupLabels = ["OS: Linux", "Stage: Prod"];

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
                Assert.That(Task(tasks, "New AppRole: AR504711-002").GetAddInfoValue(AdditionalInfoKeys.GrpType), Is.EqualTo("AppRole"),
                    "the firewall write takes the label key from the requested type, not from the name");
                Assert.That(removeMembers.GetAddInfoValue(AdditionalInfoKeys.GrpType), Is.EqualTo("AppRole"));
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
                Assert.That(area.GetAddInfoValue(AdditionalInfoKeys.GrpType), Is.EqualTo("NetworkArea"));
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
                Assert.That(obsolete.GetAddInfoValue(AdditionalInfoKeys.GrpType), Is.EqualTo("AppRole"));
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

        /// <summary>
        /// Guardicore knows no service groups: their services are requested directly even while resolveServiceGroups is
        /// switched off for the management.
        /// </summary>
        [Test]
        public async Task Request_ResolvesServiceGroupsOnLabelWritersWhateverTheSetting()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.Settings.SetValue(3, DeviceSpecificSettingKeys.ResolveServiceGroups, false);

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(ConnectionsWithServiceGroup);

            List<WfReqElement> services = [.. tasks.SelectMany(task => task.Elements).Where(element => element.Field == nameof(ElemFieldType.service))];
            Assert.Multiple(() =>
            {
                Assert.That(tasks.Exists(task => task.Title.StartsWith("New Servicegroup", StringComparison.Ordinal)), Is.False);
                Assert.That(services.Exists(element => element.Name == "Dns" && element.Port == 53 && string.IsNullOrEmpty(element.GroupName)), Is.True);
            });
        }

        /// <summary>Other managements follow the setting in effect for them: switched off, the service group is requested.</summary>
        [Test]
        public async Task Request_RequestsServiceGroupsOnOtherManagementsWhileResolvingIsOff()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ManagementDeviceTypeId = kCheckPointManagementTypeId;
            api.ManagementExtMgtData = kExternalManagementData;
            api.Settings.SetValue(1, DeviceSpecificSettingKeys.ResolveServiceGroups, false);

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(ConnectionsWithServiceGroup);

            Assert.That(Task(tasks, "New Servicegroup: DnsGroup").Elements.Single().Name, Is.EqualTo("Dns"));
        }

        /// <summary>
        /// The access task of a connection for Guardicore names the connection marker, by which the rules are written and
        /// found again, and its app roles and network areas by their labels.
        /// </summary>
        [Test]
        public async Task Request_AccessTaskNamesMarkerAndLabels()
        {
            List<WfReqTask> tasks = await Analysis(Api()).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask access = tasks.Single(task => task.TaskType == nameof(WfTaskType.access));
            List<string> sourceGroups = [.. access.Elements.Where(element => element.Field == nameof(ElemFieldType.source)).Select(element => element.GroupName ?? "")];
            Assert.Multiple(() =>
            {
                Assert.That(access.GetAddInfoValue(AdditionalInfoKeys.ConnMarker), Is.EqualTo("FWOC1"));
                Assert.That(sourceGroups, Does.Contain("AppRole: AR504711-001"));
                Assert.That(sourceGroups, Does.Contain("NetworkArea: NA50"));
                Assert.That(access.Elements.Where(element => element.Field == nameof(ElemFieldType.destination)).Select(element => element.GroupName),
                    Does.Contain("AppRole: AR504711-002"));
            });
        }

        /// <summary>The label AND-groups of a connection are requested for a management that writes labels.</summary>
        [Test]
        public async Task Request_LabelGroupsOfAConnection_AreRequestedForLabelWriters()
        {
            List<WfReqTask> tasks = await LabelModellingAnalysis(Api()).AnalyseModelledConnectionsForRequest(LabelOnlySourceConnections());

            WfReqTask access = tasks.Single(task => task.TaskType == nameof(WfTaskType.access));
            List<WfReqElement> source = [.. access.Elements.Where(element => element.Field == nameof(ElemFieldType.source))];
            Assert.Multiple(() =>
            {
                Assert.That(source.Select(element => element.GroupName).Distinct(), Is.EqualTo(kLabelGroupSource), "one AND-group");
                Assert.That(source.Select(element => element.Name), Is.EqualTo(kLabelGroupLabels), "one element per label of the group");
            });
        }

        /// <summary>A management that writes no labels cannot implement a side of labels only: the connection is not requested there.</summary>
        [Test]
        public async Task Request_LabelOnlySide_IsNotRequestedForOtherManagements()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ManagementDeviceTypeId = kCheckPointManagementTypeId;
            api.ManagementExtMgtData = kExternalManagementData;

            List<WfReqTask> tasks = await LabelModellingAnalysis(api).AnalyseModelledConnectionsForRequest(LabelOnlySourceConnections());

            Assert.That(tasks.Exists(task => task.TaskType == nameof(WfTaskType.access)), Is.False);
        }

        /// <summary>Network areas that are not written as labels are requested by the addresses of their networks.</summary>
        [Test]
        public async Task Request_NetworkAreasWithoutLabel_AreRequestedByTheirAddresses()
        {
            List<WfReqTask> tasks = await Analysis(Api(implementAreas: false)).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask access = tasks.Single(task => task.TaskType == nameof(WfTaskType.access));
            WfReqElement areaNetwork = access.Elements.Single(element => element.Name == "Net50");
            Assert.Multiple(() =>
            {
                Assert.That(areaNetwork.IpString, Is.EqualTo("10.50.0.0/16"));
                Assert.That(areaNetwork.GroupName, Is.Null);
                Assert.That(access.Elements.Exists(element => element.GroupName == "NA50" || element.GroupName == "NetworkArea: NA50"), Is.False);
            });
        }

        private const string kNameAndId = "{name} ({id})";

        private static NetworkObject Renamed(NetworkObject label, string name) =>
            new() { Id = label.Id, Name = name, Type = label.Type, ObjectGroupFlats = label.ObjectGroupFlats };

        /// <summary>The connection of the standard test with named app roles and the value format "{name} ({id})".</summary>
        private static ModellingVarianceAnalysis FormattedAnalysis(GuardicoreVarianceTestApiConn api) =>
            new(api, new ExtStateHandler(ExtStateApiConnection), new SimulatedUserConfig
            {
                ModNamingConvention = UserConfig.ModNamingConvention,
                CreateAppZones = true,
                RuleRecognitionOption = kRecognitionOption,
                GroupLabelValueFormat = kNameAndId
            }, Application, DefaultInit.DoNothing);

        private static List<ModellingConnection> NamedConnections()
        {
            ModellingAppRole Named(ModellingAppRole appRole, string name) => new(appRole) { Name = name, AppServers = appRole.AppServers };
            return
            [
                new()
                {
                    Id = 1,
                    Name = "Conn1",
                    SourceAppRoles = [new() { Content = Named(UnchangedAppRole, "Billing") }, new() { Content = Named(ChangedAppRole, "Payments") }],
                    SourceAreas = [new() { Content = Area }],
                    DestinationAppRoles = [new() { Content = Named(MissingAppRole, "Missing") }],
                    Services = [new() { Content = new() { Id = 1, Name = "Service1", Port = 443, ProtoId = 6 } }]
                }
            ];
        }

        /// <summary>
        /// With the value format "{name} ({id})" the labels of the former format are found by their id and renamed: an
        /// unchanged label by a rename task, a changed one by its update tasks, which name the label to rename.
        /// </summary>
        [Test]
        public async Task Request_RenamesLabelsOfTheFormerFormatInsteadOfCreatingThem()
        {
            List<WfReqTask> tasks = await FormattedAnalysis(Api()).AnalyseModelledConnectionsForRequest(NamedConnections());

            WfReqTask rename = Task(tasks, "Rename label: AppRole: AR504711-001 -> AppRole: Billing (AR504711-001)");
            WfReqTask removeMembers = Task(tasks, "Update AppRole: AR504711-003: Remove Members");
            WfReqTask newAppRole = Task(tasks, "New AppRole: AR504711-002");
            WfReqTask area = Task(tasks, "Update NetworkArea: NA50");
            WfReqTask access = tasks.Single(task => task.TaskType == nameof(WfTaskType.access));
            Assert.Multiple(() =>
            {
                Assert.That(rename.TaskType, Is.EqualTo(nameof(WfTaskType.group_modify)));
                Assert.That(rename.GetAddInfoValue(AdditionalInfoKeys.LabelName), Is.EqualTo("AppRole: Billing (AR504711-001)"));
                Assert.That(rename.GetAddInfoValue(AdditionalInfoKeys.FormerLabelName), Is.EqualTo("AppRole: AR504711-001"));
                Assert.That(rename.GetAddInfoValue(AdditionalInfoKeys.GrpType), Is.EqualTo("AppRole"));
                Assert.That(rename.Elements, Is.Not.Empty);
                Assert.That(rename.Elements.TrueForAll(element => element.RequestAction == nameof(RequestAction.unchanged)), Is.True);
                Assert.That(removeMembers.GetAddInfoValue(AdditionalInfoKeys.LabelName), Is.EqualTo("AppRole: Payments (AR504711-003)"));
                Assert.That(removeMembers.GetAddInfoValue(AdditionalInfoKeys.FormerLabelName), Is.EqualTo("AppRole: AR504711-003"));
                Assert.That(tasks.Exists(task => task.Title.StartsWith("Rename label: AppRole: AR504711-003")), Is.False, "renamed by its update");
                Assert.That(newAppRole.GetAddInfoValue(AdditionalInfoKeys.LabelName), Is.EqualTo("AppRole: Missing (AR504711-002)"));
                Assert.That(newAppRole.GetAddInfoValue(AdditionalInfoKeys.FormerLabelName), Is.Empty);
                Assert.That(area.GetAddInfoValue(AdditionalInfoKeys.LabelName), Is.EqualTo("NetworkArea: Area50 (NA50)"));
                Assert.That(area.GetAddInfoValue(AdditionalInfoKeys.FormerLabelName), Is.EqualTo("NetworkArea: NA50"));
                Assert.That(access.Elements.Select(element => element.GroupName), Does.Contain("AppRole: Billing (AR504711-001)"));
                Assert.That(DeletedLabels(tasks), Is.EqualTo(kObsoleteLabels), "the renamed labels are no obsolete ones");
            });
        }

        /// <summary>A label of the configured name is used as it is, even if a label of another name has the same id.</summary>
        [Test]
        public async Task Request_LeavesLabelsOfTheConfiguredNameAlone()
        {
            List<WfReqTask> tasks = await Analysis(Api()).AnalyseModelledConnectionsForRequest(Connections);

            Assert.That(tasks.Exists(task => task.Title.StartsWith("Rename label")), Is.False);
        }

        /// <summary>
        /// Labels written by others with the id in their last parentheses stand for the app role: they are renamed to the
        /// configured value and, if their app role is gone, deleted under their own name.
        /// </summary>
        [Test]
        public async Task Request_FindsLabelsOfOthersByTheIdInTheirValue()
        {
            GuardicoreVarianceTestApiConn api = Api();
            api.ImportedLabels = [.. GuardicoreVarianceTestApiConn.StandardLabels.Select(label => label.Name switch
            {
                "AppRole: AR504711-001" => Renamed(label, "AppRole: PostgreSQL (COM-1629) - Proxy (AR504711-001)"),
                "AppRole: AR504711-009" => Renamed(label, "AppRole: Gone (AR504711-009)"),
                _ => label
            })];

            List<WfReqTask> tasks = await Analysis(api).AnalyseModelledConnectionsForRequest(Connections);

            WfReqTask obsolete = Task(tasks, "Delete obsolete label: AppRole: Gone (AR504711-009)");
            Assert.Multiple(() =>
            {
                Assert.That(Task(tasks, "Rename label: AppRole: PostgreSQL (COM-1629) - Proxy (AR504711-001) -> AppRole: AR504711-001").TaskType,
                    Is.EqualTo(nameof(WfTaskType.group_modify)));
                Assert.That(tasks.Exists(task => task.Title == "New AppRole: AR504711-001"), Is.False);
                Assert.That(obsolete.GetAddInfoValue(AdditionalInfoKeys.LabelName), Is.EqualTo("AppRole: Gone (AR504711-009)"));
                Assert.That(obsolete.Elements.Single().GroupName, Is.EqualTo("AppRole: Gone (AR504711-009)"));
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
