using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Modelling;
using FWO.Data.RulePositioning.CheckPoint;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests how the workflow handler builds the provisioning work list of a ticket from the implementation modes in
    /// effect: per management, with alerts for tasks nobody implements, without provisioning when the settings cannot
    /// be resolved, and limited to the scope of the action.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class WfHandlerProvisioningTest
    {
        private const long kGlobalNodeId = 1;
        private const long kDeviceTypeNodeId = 2;
        private const long kAutomaticManagementNodeId = 3;
        private const long kExternalManagementNodeId = 4;
        private const long kManualManagementNodeId = 5;
        private const long kManualGatewayNodeId = 6;
        private const int kManualGatewayId = 1;
        private const int kAutomaticGatewayId = 2;
        private const int kOtherVendorGatewayId = 3;
        private const int kOtherVendorDeviceTypeId = 99;
        private const int kDeviceTypeId = 9;
        private const int kAutomaticManagementId = 20;
        private const int kExternalManagementId = 30;
        private const int kManualManagementId = 40;
        private const long kTicketId = 5;
        private const int kOpenStateId = 210;
        private const int kEndStateId = 249;
        private const string kBundleId = "bundle-1";

        private static readonly List<ProvisioningWay> kFirewallWriteThenExternal = [ProvisioningWay.FirewallWrite, ProvisioningWay.ExternalTicketSystem];
        private static readonly List<long> kBundleReqTaskIds = [1, 2];
        private static readonly List<long> kSecondReqTaskId = [2];
        private static readonly List<long?> kAutomaticGatewayImplTask = [kAutomaticGatewayId * 100];
        private static readonly CheckPointRulePosition kPosition =
            new("layer-uid", CheckPointPositionAnchorKind.Bottom, null, ProvisioningPositioningAlgorithm.DefaultEndOfRulebase);

        private static InMemoryDeviceSpecificSettingsApiConnection CreateApi()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            DeviceType deviceType = new() { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" };
            api.Managements.Add(new Management { Id = kAutomaticManagementId, Name = "cp1", DeviceType = deviceType });
            api.Managements.Add(new Management { Id = kExternalManagementId, Name = "cp2", DeviceType = deviceType });
            api.Managements.Add(new Management { Id = kManualManagementId, Name = "cp3", DeviceType = deviceType });
            api.AddNode(kGlobalNodeId, DeviceSpecificScopeType.Global, DeviceSpecificScopePath.GlobalObjectKey, displayName: "Global");
            api.AddNode(kDeviceTypeNodeId, DeviceSpecificScopeType.DeviceType, kDeviceTypeId.ToString(), kGlobalNodeId);
            api.AddNode(kAutomaticManagementNodeId, DeviceSpecificScopeType.Management, kAutomaticManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kExternalManagementNodeId, DeviceSpecificScopeType.Management, kExternalManagementId.ToString(), kDeviceTypeNodeId);
            api.AddNode(kManualManagementNodeId, DeviceSpecificScopeType.Management, kManualManagementId.ToString(), kDeviceTypeNodeId);
            api.SetValue(kExternalManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.RerouteToExternalTicketSystem);
            api.SetValue(kExternalManagementNodeId, DeviceSpecificSettingKeys.RerouteToExternalTicketSystem, ExternalTicketSystemKind.TufinSecureChange);
            api.SetValue(kManualManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);
            return api;
        }

        private static WfHandler CreateHandler(InMemoryDeviceSpecificSettingsApiConnection api, bool considerBundling = false)
        {
            WfHandler handler = new(new SimulatedUserConfig { ReqConsiderBundling = considerBundling }, api, WorkflowPhases.request, null);
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            StateMatrixDict matrices = (StateMatrixDict)field.GetValue(handler)!;
            matrices.Matrices[WfTaskType.group_create.ToString()] = new StateMatrix { ImplementationLowestEndState = kEndStateId };
            return handler;
        }

        private static WfReqTask ReqTask(long id, int taskNumber, int managementId, string bundleId = "", int stateId = kOpenStateId)
        {
            WfReqTask reqTask = new()
            {
                Id = id,
                TicketId = kTicketId,
                TaskNumber = taskNumber,
                TaskType = WfTaskType.group_create.ToString(),
                ManagementId = managementId,
                StateId = stateId
            };
            if (bundleId.Length > 0)
            {
                Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.FlowBundleId, bundleId } };
                reqTask.AdditionalInfo = JsonSerializer.Serialize(addInfo);
            }
            reqTask.ImplementationTasks.Add(new WfImplTask
            {
                Id = id * 10,
                ReqTaskId = id,
                TaskNumber = 1,
                TaskType = reqTask.TaskType,
                ManagementId = managementId,
                StateId = kOpenStateId
            });
            return reqTask;
        }

        /// <summary>
        /// A handler knowing three gateways of the automatic management: one with the implementation mode Manual for
        /// rules, one inheriting Automatic, and one whose management has a device type rules are not written to.
        /// </summary>
        private static WfHandler CreateRuleHandler(InMemoryDeviceSpecificSettingsApiConnection api)
        {
            api.AddNode(kManualGatewayNodeId, DeviceSpecificScopeType.Gateway, kManualGatewayId.ToString(), kAutomaticManagementNodeId);
            api.SetValue(kManualGatewayNodeId, DeviceSpecificSettingKeys.ImplementationModeForRules, ProvisioningRuleImplementationMode.Manual);
            WfHandler handler = CreateHandler(api);
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[WfTaskType.access.ToString()] = new StateMatrix { ImplementationLowestEndState = kEndStateId };
            Management checkPoint = new() { Id = kAutomaticManagementId, Name = "cp1", DeviceType = new DeviceType { Id = kDeviceTypeId, Name = "Check Point", Version = "R8x" } };
            Management otherVendor = new() { Id = kAutomaticManagementId, Name = "cp1", DeviceType = new DeviceType { Id = kOtherVendorDeviceTypeId, Name = "Other" } };
            handler.Devices =
            [
                new Device { Id = kManualGatewayId, Name = "gw-manual", Management = checkPoint },
                new Device { Id = kAutomaticGatewayId, Name = "gw-automatic", Management = checkPoint },
                new Device { Id = kOtherVendorGatewayId, Name = "gw-other", Management = otherVendor }
            ];
            return handler;
        }

        private static WfReqTask AccessTask(params int[] gatewayIds)
        {
            WfReqTask reqTask = new()
            {
                Id = 7,
                TicketId = kTicketId,
                TaskNumber = 1,
                TaskType = WfTaskType.access.ToString(),
                ManagementId = kAutomaticManagementId,
                StateId = kOpenStateId
            };
            foreach (int gatewayId in gatewayIds)
            {
                reqTask.ImplementationTasks.Add(new WfImplTask
                {
                    Id = gatewayId * 100,
                    ReqTaskId = reqTask.Id,
                    TaskNumber = reqTask.ImplementationTasks.Count + 1,
                    TaskType = reqTask.TaskType,
                    ManagementId = kAutomaticManagementId,
                    DeviceId = gatewayId,
                    StateId = kOpenStateId,
                    RulePosition = kPosition
                });
            }
            return reqTask;
        }

        private static WfTicket Ticket(params WfReqTask[] reqTasks)
        {
            return new WfTicket { Id = kTicketId, Tasks = reqTasks.ToList() };
        }

        private static List<string> AlertDescriptions(InMemoryDeviceSpecificSettingsApiConnection api)
        {
            return [.. api.Calls.Where(call => call.Query == MonitorQueries.addAlert)
                .Select(call => call.Variables?.GetType().GetProperty("description")?.GetValue(call.Variables) as string ?? "")];
        }

        [Test]
        public async Task BuildProvisioningWorkList_TakesTheModeOfEachManagement()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateHandler(api);
            WfTicket ticket = Ticket(ReqTask(1, 1, kAutomaticManagementId), ReqTask(2, 2, kExternalManagementId), ReqTask(3, 3, kManualManagementId));

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(ticket);

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items.Select(item => item.Way), Is.EqualTo(kFirewallWriteThenExternal));
                Assert.That(workList.Items[0].ImplTask?.Id, Is.EqualTo(10));
                Assert.That(workList.Items[1].ExternalTicketSystem, Is.EqualTo(ExternalTicketSystemKind.TufinSecureChange));
                Assert.That(AlertDescriptions(api), Is.Empty);
            });
        }

        [Test]
        public async Task BuildProvisioningWorkList_FollowsAChangedMode()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateHandler(api);
            WfTicket ticket = Ticket(ReqTask(1, 1, kAutomaticManagementId));
            await handler.BuildProvisioningWorkList(ticket);
            api.SetValue(kAutomaticManagementNodeId, DeviceSpecificSettingKeys.ImplementationMode, ProvisioningImplementationMode.Manual);

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(ticket);

            Assert.That(workList.Items, Is.Empty);
        }

        [Test]
        public async Task BuildProvisioningWorkList_ImplementationTaskOnAnExternalTarget_RaisesAnAlert()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateHandler(api);
            WfReqTask reqTask = ReqTask(1, 1, kAutomaticManagementId);
            reqTask.ImplementationTasks[0].ManagementId = kExternalManagementId;

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(reqTask));

            List<string> descriptions = AlertDescriptions(api);
            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(descriptions, Has.Count.EqualTo(1));
                Assert.That(descriptions[0], Does.Contain($"Ticket {kTicketId}, request task 1, implementation task 10: not provisioned"));
            });
        }

        [Test]
        public async Task BuildProvisioningWorkList_RuleTasks_TakeTheModeOfTheirGateway()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateRuleHandler(api);

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(AccessTask(kManualGatewayId, kAutomaticGatewayId)));

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items.Select(item => item.ImplTask?.Id), Is.EqualTo(kAutomaticGatewayImplTask));
                Assert.That(workList.Gaps, Is.Empty);
            });
        }

        [Test]
        public async Task BuildProvisioningWorkList_RuleTaskOfAnotherVendor_RaisesAnAlert()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateRuleHandler(api);

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(AccessTask(kOtherVendorGatewayId)));

            Assert.Multiple(() =>
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(AlertDescriptions(api), Has.Some.Contains("rules are not written to the vendor of gateway 3"));
            });
        }

        /// <summary>A rule_delete task on the automatic gateway deleting the rule "r1".</summary>
        private static WfReqTask RuleDeleteTask(WfHandler handler)
        {
            FieldInfo field = typeof(WfHandler).GetField("stateMatrixDict", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(WfHandler), "stateMatrixDict");
            ((StateMatrixDict)field.GetValue(handler)!).Matrices[WfTaskType.rule_delete.ToString()] = new StateMatrix { ImplementationLowestEndState = kEndStateId };
            WfReqTask reqTask = AccessTask(kAutomaticGatewayId);
            reqTask.TaskType = WfTaskType.rule_delete.ToString();
            WfImplTask implTask = reqTask.ImplementationTasks[0];
            implTask.TaskType = reqTask.TaskType;
            implTask.RulePosition = null;
            implTask.ImplElements.Add(new WfImplElement { Field = ElemFieldType.rule.ToString(), RuleUid = "r1" });
            return reqTask;
        }

        private static Rule EnforcedRule(params int[] gatewayIds) => new()
        {
            Uid = "r1",
            EnforcingGateways = [.. gatewayIds.Select(id => new DeviceWrapper { Content = new Device { Id = id, Name = $"gw-{id}" } })]
        };

        [Test]
        public async Task BuildProvisioningWorkList_ExistingRuleAlsoOnAManualGateway_IsLeftToAPerson()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            WfHandler handler = CreateRuleHandler(api);
            WfReqTask onAutomaticOnly = RuleDeleteTask(handler);
            api.EnforcingRules.Add(EnforcedRule(kAutomaticGatewayId));
            ProvisioningWorkList written = await handler.BuildProvisioningWorkList(Ticket(onAutomaticOnly));
            api.EnforcingRules.Clear();
            api.EnforcingRules.Add(EnforcedRule(kAutomaticGatewayId, kManualGatewayId));

            ProvisioningWorkList leftToAPerson = await handler.BuildProvisioningWorkList(Ticket(RuleDeleteTask(handler)));

            Assert.Multiple(() =>
            {
                Assert.That(written.Items.Select(item => item.ImplTask?.Id), Is.EqualTo(kAutomaticGatewayImplTask));
                Assert.That(leftToAPerson.Items, Is.Empty);
                Assert.That(AlertDescriptions(api), Has.Some.Contains($"the rule also takes effect on gateway {kManualGatewayId}, which is set to manual"));
            });
        }

        [Test]
        public void BuildProvisioningWorkList_GatewaysOfTheRulesCannotBeLoaded_ProvisionsNothing()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.FailingQuery = RuleQueries.getRuleEnforcingGateways;
            WfHandler handler = CreateRuleHandler(api);

            Assert.ThrowsAsync<InvalidOperationException>(() => handler.BuildProvisioningWorkList(Ticket(RuleDeleteTask(handler))));
        }

        [Test]
        public void BuildProvisioningWorkList_SettingsCannotBeResolved_ProvisionsNothing()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.FailingQuery = DeviceSpecificSettingsQueries.getNodesByNaturalKeys;
            WfHandler handler = CreateHandler(api);

            Assert.ThrowsAsync<InvalidOperationException>(() => handler.BuildProvisioningWorkList(Ticket(ReqTask(1, 1, kExternalManagementId))));
        }

        [Test]
        public async Task BuildProvisioningWorkList_RequestTaskScope_OnlyThisTask()
        {
            WfHandler handler = CreateHandler(CreateApi());
            WfReqTask scopeTask = ReqTask(2, 2, kAutomaticManagementId);
            WfTicket ticket = Ticket(ReqTask(1, 1, kAutomaticManagementId), scopeTask);

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(ticket, scopeTask);

            Assert.That(workList.Items.Select(item => item.ReqTask.Id), Is.EqualTo(kSecondReqTaskId));
        }

        [Test]
        public async Task BuildProvisioningWorkList_RequestTaskScope_TakesTheWholeBundle()
        {
            WfHandler handler = CreateHandler(CreateApi(), true);
            WfReqTask scopeTask = ReqTask(2, 2, kAutomaticManagementId, kBundleId);
            WfTicket ticket = Ticket(ReqTask(1, 1, kAutomaticManagementId, kBundleId), scopeTask, ReqTask(3, 3, kAutomaticManagementId));

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(ticket, scopeTask);

            Assert.That(workList.Items.Select(item => item.ReqTask.Id), Is.EqualTo(kBundleReqTaskIds));
        }

        [Test]
        public async Task BuildProvisioningWorkList_RequestTaskScope_WaitsForTheBundle()
        {
            WfHandler handler = CreateHandler(CreateApi(), true);
            WfReqTask scopeTask = ReqTask(2, 2, kAutomaticManagementId, kBundleId);
            WfTicket ticket = Ticket(ReqTask(1, 1, kAutomaticManagementId, kBundleId, kOpenStateId - 1), scopeTask);

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(ticket, scopeTask);

            Assert.That(workList.Items, Is.Empty);
        }

        private const int kOwnerId = 77;
        private const int kConnectionId = 300;
        private const int kInterfaceId = 301;
        private const int kUserAreaId = 5;
        private const string kUserAreaName = "NA_Users";
        private static readonly List<string> kAccessRoles = ["a_testUser", "AR_Sales"];

        /// <summary>
        /// A handler for a Check Point gateway writing rules, whose settings name one placeholder area for users, allowed
        /// in the source, and an api knowing that area and a connection of the owner with the given extra configurations.
        /// </summary>
        private static (WfHandler Handler, InMemoryDeviceSpecificSettingsApiConnection Api) CreatePlaceholderHandler(params ModellingExtraConfig[] extraConfigs)
        {
            InMemoryDeviceSpecificSettingsApiConnection api = CreateApi();
            api.NetworkAreas.Add(new ModellingNetworkArea { Id = kUserAreaId, IdString = kUserAreaName, Name = "Users" });
            api.Connections.Add(new ModellingConnection { Id = kConnectionId, AppId = kOwnerId, ExtraConfigs = extraConfigs.ToList() });
            WfHandler handler = CreateRuleHandler(api);
            handler.userConfig.ModSpecUserAreas = $"[{{\"area_id\":{kUserAreaId},\"use_in_src\":true,\"use_in_dst\":false}}]";
            return (handler, api);
        }

        /// <summary>An access request task of the owner and the connection, whose rule has the user placeholder in its source.</summary>
        private static WfReqTask PlaceholderAccessTask(int ownerId = kOwnerId)
        {
            WfReqTask reqTask = AccessTask(kAutomaticGatewayId);
            Dictionary<string, string> addInfo = new() { { AdditionalInfoKeys.ConnId, kConnectionId.ToString() } };
            reqTask.AdditionalInfo = JsonSerializer.Serialize(addInfo);
            reqTask.Owners.Add(new FwoOwnerDataHelper { Owner = new FwoOwner { Id = ownerId } });
            reqTask.ImplementationTasks[0].ImplElements.Add(new WfImplElement { GroupName = kUserAreaName, Field = ElemFieldType.source.ToString() });
            reqTask.ImplementationTasks[0].ImplElements.Add(new WfImplElement { GroupName = "NA_Server", Field = ElemFieldType.destination.ToString() });
            return reqTask;
        }

        private static ModellingExtraConfig UserConfig(string name) => new() { ExtraConfigType = "IDA-User", ExtraConfigText = name };

        [Test]
        public async Task BuildProvisioningWorkList_SubstitutablePlaceholder_IsWritten()
        {
            (WfHandler handler, InMemoryDeviceSpecificSettingsApiConnection api) = CreatePlaceholderHandler(UserConfig("a_testUser"), UserConfig("AR_Sales"));
            WfReqTask reqTask = PlaceholderAccessTask();

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(reqTask));
            ModellingPlaceholderResult result = await handler.SubstituteModellingPlaceholders(reqTask, reqTask.ImplementationTasks[0]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items.Select(item => item.ImplTask?.Id), Is.EqualTo(kAutomaticGatewayImplTask));
                Assert.That(AlertDescriptions(api), Is.Empty);
                Assert.That(result.ImplTask.ImplElements.Where(e => e.Field == ElemFieldType.source.ToString()).Select(e => e.GroupName), Is.EqualTo(kAccessRoles));
                Assert.That(result.Note, Does.Contain("NA_Users by a_testUser, AR_Sales"));
            }
        }

        [Test]
        public async Task BuildProvisioningWorkList_ConnectionOfAnotherOwner_IsNotUsed()
        {
            (WfHandler handler, InMemoryDeviceSpecificSettingsApiConnection api) = CreatePlaceholderHandler(UserConfig("a_testUser"));

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(PlaceholderAccessTask(kOwnerId + 1)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().Reason, Does.Contain("no modelled connection"));
                Assert.That(AlertDescriptions(api).Single(), Does.Contain("NA_Users"));
            }
        }

        [Test]
        public async Task BuildProvisioningWorkList_ConnectionWithNat_IsReportedAndNotWritten()
        {
            (WfHandler handler, _) = CreatePlaceholderHandler(UserConfig("a_testUser"), new ModellingExtraConfig { ExtraConfigType = "NAT" });

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(PlaceholderAccessTask()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().Reason, Does.Contain("'NAT'"));
            }
        }

        [Test]
        public async Task BuildProvisioningWorkList_ExtraConfigOfTheUsedInterface_IsReportedAndNotWritten()
        {
            (WfHandler handler, InMemoryDeviceSpecificSettingsApiConnection api) = CreatePlaceholderHandler(UserConfig("a_testUser"));
            api.Connections[0].UsedInterfaceId = kInterfaceId;
            api.Connections.Add(new ModellingConnection { Id = kInterfaceId, AppId = kOwnerId + 1, ExtraConfigs = [UserConfig("AR_Interface")] });

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(PlaceholderAccessTask()));

            Assert.That(workList.Gaps.Single().Reason, Does.Contain("interface"));
        }

        [Test]
        public async Task BuildProvisioningWorkList_ConnectionCannotBeLoaded_IsReportedAndNotWritten()
        {
            (WfHandler handler, InMemoryDeviceSpecificSettingsApiConnection api) = CreatePlaceholderHandler(UserConfig("a_testUser"));
            api.FailingQuery = ModellingQueries.getConnectionById;

            ProvisioningWorkList workList = await handler.BuildProvisioningWorkList(Ticket(PlaceholderAccessTask()));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(workList.Items, Is.Empty);
                Assert.That(workList.Gaps.Single().Reason, Does.Contain("could not be loaded"));
            }
        }
    }
}
