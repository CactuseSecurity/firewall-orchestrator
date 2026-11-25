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
        private static readonly List<string> kGatewayTargetQueries =
            [FWO.Api.Client.Queries.DeviceQueries.getImplementationTaskGatewayTarget];

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

        /// <summary>
        /// A group task takes the object type its request task names for the group; other tasks and request tasks without
        /// a type have none, and other tasks do not ask for it.
        /// </summary>
        private const long kRuleRequestTaskId = 9;
        private const int kConnectionOwnerId = 5;

        /// <summary>A Guardicore management with request task 9 for connection 12 of owner 5, marked "FWOC12".</summary>
        private static InMemoryDeviceSpecificSettingsApiConnection GuardicoreApi(string marker = "FWOC12", int requestOwnerId = kConnectionOwnerId)
        {
            InMemoryDeviceSpecificSettingsApiConnection api = new();
            api.Managements.Add(Management(FirewallVendors.kGuardicoreManagement));
            api.AddNode(1, DeviceSpecificScopeType.Global, "global");
            api.RequestTaskAdditionalInfo[kRuleRequestTaskId] = $"{{\"ConnId\":\"12\",\"ConnMarker\":\"{marker}\"}}";
            api.RequestTaskOwners[kRuleRequestTaskId] = [requestOwnerId];
            api.Connections.Add(new() { Id = 12, AppId = kConnectionOwnerId, App = new() { Id = kConnectionOwnerId, ExtAppId = " APP-1234 " } });
            return api;
        }

        private static WfImplTask RuleTask(WfTaskType taskType = WfTaskType.access)
        {
            WfImplTask task = Task(taskType);
            task.ReqTaskId = kRuleRequestTaskId;
            return task;
        }

        private static async Task<FirewallWriteErrorKind?> LoadError(InMemoryDeviceSpecificSettingsApiConnection api, ModelledObjectNaming? naming = null,
            WfImplTask? task = null)
        {
            try
            {
                await new ApiFirewallWriteDataSource(api, null, naming).LoadContext(task ?? RuleTask());
                return null;
            }
            catch (FirewallWriteException exception)
            {
                return exception.Error.Kind;
            }
        }

        /// <summary>
        /// A rule task of a management that writes labels takes the marker of its request task, checked to name the
        /// connection of the request task, and the app of the owner of that connection as ruleset.
        /// </summary>
        [Test]
        public async Task RuleTasksOfLabelWriters_TakeTheCheckedMarkerAndTheAppOfTheConnection()
        {
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(GuardicoreApi()).LoadContext(RuleTask(WfTaskType.rule_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ConnectionMarker, Is.EqualTo("FWOC12"));
                Assert.That(context.ExtAppId, Is.EqualTo("APP-1234"), "from the owner of the connection, not from the request task");
            }
        }

        /// <summary>
        /// The ruleset of the rules of a connection is named by the app name, its common service flag and the configured
        /// App label value format and common service prefix, which the context passes on.
        /// </summary>
        [Test]
        public async Task RuleTasksOfLabelWriters_TakeWhatNamesTheAppRuleset()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = GuardicoreApi();
            api.Connections[0].App.Name = " Exchange ";
            api.Connections[0].IsCommonService = true;
            GuardicoreRulesetFormat format = new("{name} ({id})", "CS");

            FirewallWriteContext context = await new ApiFirewallWriteDataSource(api, format).LoadContext(RuleTask());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.AppName, Is.EqualTo("Exchange"));
                Assert.That(context.IsCommonService, Is.True);
                Assert.That(context.RulesetFormat, Is.EqualTo(format));
            }
        }

        /// <summary>
        /// The request task data can be changed by several roles: a marker that does not name the connection of the request
        /// task, or a connection of another owner, must not let the writer change the rules of another connection.
        /// </summary>
        [Test]
        public async Task RuleTasksOfLabelWriters_RefuseMarkersOfOtherConnectionsAndOwners()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await LoadError(GuardicoreApi(marker: "FWOC13")), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "another connection");
                Assert.That(await LoadError(GuardicoreApi(marker: "F")), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "a shortened marker");
                Assert.That(await LoadError(GuardicoreApi(marker: "12")), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "no prefix");
                Assert.That(await LoadError(GuardicoreApi(marker: "CHG12")), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "another prefix");
                Assert.That(await LoadError(GuardicoreApi(requestOwnerId: 6)), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "another owner");
                Assert.That(await LoadError(GuardicoreApi()), Is.Null);
            }
        }

        /// <summary>
        /// The marker has to be the configured one, which may contain digits: with the marker "FW1C" a task marked "FW1C12"
        /// is accepted and one marked with the default "FWOC12" is refused.
        /// </summary>
        [Test]
        public async Task RuleTasksOfLabelWriters_TakeTheConfiguredMarker()
        {
            ModelledObjectNaming naming = new("FW1C", null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(await LoadError(GuardicoreApi(marker: "FW1C12"), naming), Is.Null);
                Assert.That(await LoadError(GuardicoreApi(), naming), Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
            }
        }

        private const long kGroupRequestTaskId = 11;

        /// <summary>A Guardicore management with group request task 11 of app role AR1 naming the given labels.</summary>
        private static InMemoryDeviceSpecificSettingsApiConnection GuardicoreGroupApi(string labelName, string? formerLabelName)
        {
            InMemoryDeviceSpecificSettingsApiConnection api = GuardicoreApi();
            string former = formerLabelName == null ? "" : $",\"FormerLabelName\":\"{formerLabelName}\"";
            api.RequestTaskAdditionalInfo[kGroupRequestTaskId] = $"{{\"GrpName\":\"AR1\",\"GrpType\":\"AppRole\",\"LabelName\":\"{labelName}\"{former}}}";
            return api;
        }

        private static WfImplTask GroupTask()
        {
            WfImplTask task = Task(WfTaskType.group_modify);
            task.ReqTaskId = kGroupRequestTaskId;
            return task;
        }

        /// <summary>
        /// A group task writes and renames only labels that stand for its group by the configured format: the request task
        /// data can be changed by several roles, and a label of another group must not be renamed or rewritten this way.
        /// </summary>
        [Test]
        public async Task GroupTasks_TakeOnlyLabelsOfTheirGroup()
        {
            ModelledObjectNaming naming = new(null, "{name} ({id})");
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(GuardicoreGroupApi("AppRole: Web (AR1)", "AppRole: AR1"), null, naming)
                .LoadContext(GroupTask());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.LabelName, Is.EqualTo("AppRole: Web (AR1)"));
                Assert.That(context.FormerLabelName, Is.EqualTo("AppRole: AR1"), "the label of the former default format stands for the group");
                Assert.That(await LoadError(GuardicoreGroupApi("AppRole: Web (AR1)", "AppRole: AR9"), naming, GroupTask()),
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "the former label of another group");
                Assert.That(await LoadError(GuardicoreGroupApi("AppRole: Other (AR9)", null), naming, GroupTask()),
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "the label of another group");
                Assert.That(await LoadError(GuardicoreGroupApi("AppRole AR1", null), naming, GroupTask()),
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData), "no label name");
            }
        }

        [Test]
        public async Task RuleTasksOfOtherVendors_LoadNoConnection()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            api.RequestTaskAdditionalInfo[kRuleRequestTaskId] = "{\"ConnId\":\"12\",\"ConnMarker\":\"F\"}";

            FirewallWriteContext context = await new ApiFirewallWriteDataSource(api).LoadContext(RuleTask());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.ConnectionMarker, Is.Null);
                Assert.That(api.Calls.Exists(call => call.Query == FWO.Api.Client.Queries.ModellingQueries.getConnectionById), Is.False);
            }
        }

        [Test]
        public async Task GroupTasksTakeTheObjectTypeOfTheirRequestTask()
        {
            const long kTypedRequestTaskId = 7;
            const long kUntypedRequestTaskId = 8;
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            api.RequestTaskAdditionalInfo[kTypedRequestTaskId] = "{\"GrpName\":\"AZ4711\",\"GrpType\":\"AppZone\"}";
            api.RequestTaskAdditionalInfo[kUntypedRequestTaskId] = "{\"GrpName\":\"AZ4711\"}";
            WfImplTask typed = Task(WfTaskType.group_modify);
            typed.ReqTaskId = kTypedRequestTaskId;
            WfImplTask untyped = Task(WfTaskType.group_create);
            untyped.ReqTaskId = kUntypedRequestTaskId;
            WfImplTask rule = Task(WfTaskType.rule_delete, Rule("a"));
            rule.ReqTaskId = kTypedRequestTaskId;
            ApiFirewallWriteDataSource dataSource = new(api);

            FirewallWriteContext typedContext = await dataSource.LoadContext(typed);
            FirewallWriteContext untypedContext = await dataSource.LoadContext(untyped);
            int typeQueriesBeforeRule = api.Calls.Count(call => call.Query == FWO.Api.Client.Queries.RequestQueries.getRequestTaskAdditionalInfo);
            FirewallWriteContext ruleContext = await dataSource.LoadContext(rule);
            int typeQueries = api.Calls.Count(call => call.Query == FWO.Api.Client.Queries.RequestQueries.getRequestTaskAdditionalInfo);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(typedContext.GroupObjectType, Is.EqualTo(FWO.Data.Modelling.ModellingTypes.ModObjectType.AppZone));
                Assert.That(untypedContext.GroupObjectType, Is.Null);
                Assert.That(ruleContext.GroupObjectType, Is.Null, "a rule task has no group type, even if its request task names one");
                Assert.That(typeQueries, Is.EqualTo(typeQueriesBeforeRule + 1), "rule tasks read the connection of their request task");
            }
        }

        [Test]
        public async Task GatewayWithoutExplicitManagementLoadsOwnerAndGatewaySettings()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            WfImplTask task = Task(WfTaskType.group_delete);
            task.ManagementId = null;

            FirewallWriteContext context = await new ApiFirewallWriteDataSource(api).LoadContext(task);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Management.Id, Is.EqualTo(kManagementId));
                Assert.That(context.Gateway?.Id, Is.EqualTo(kGatewayId));
                Assert.That(context.GetEndpoint(FirewallWriteOperation.Publish), Is.EqualTo("POST gateway-publish"));
            }
        }

        [Test]
        public void ExplicitManagementThatDiffersFromGatewayOwnerFailsBeforeManagementOrSettingsRead()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            WfImplTask task = Task(WfTaskType.group_delete);
            task.ManagementId = kManagementId + 1;

            FirewallWriteException? exception = Assert.ThrowsAsync<FirewallWriteException>(
                () => new ApiFirewallWriteDataSource(api).LoadContext(task));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception?.Error.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(api.Calls.Select(call => call.Query),
                    Is.EqualTo(kGatewayTargetQueries));
            }
        }

        [Test]
        public void MissingGatewayTargetFailsBeforeManagementOrSettingsRead()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            WfImplTask task = Task(WfTaskType.group_delete);
            task.DeviceId = kGatewayId + 1;

            FirewallWriteException? exception = Assert.ThrowsAsync<FirewallWriteException>(
                () => new ApiFirewallWriteDataSource(api).LoadContext(task));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception?.Error.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(api.Calls.Select(call => call.Query),
                    Is.EqualTo(kGatewayTargetQueries));
            }
        }

        [Test]
        public void DuplicateGatewayTargetsFailBeforeManagementOrSettingsRead()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            Device gateway = api.Managements[0].Devices[0];
            api.Managements[0].Devices = [gateway, new Device(gateway)];

            FirewallWriteException? exception = Assert.ThrowsAsync<FirewallWriteException>(
                () => new ApiFirewallWriteDataSource(api).LoadContext(Task(WfTaskType.group_delete)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(exception?.Error.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(api.Calls.Select(call => call.Query),
                    Is.EqualTo(kGatewayTargetQueries));
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
        public async Task WithoutManagementTakesTheManagementOfTheGateway()
        {
            WfImplTask task = Task(WfTaskType.rule_delete);
            task.ManagementId = null;
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(Api()).LoadContext(task);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(context.Management.Id, Is.EqualTo(kManagementId));
                Assert.That(context.Gateway?.Id, Is.EqualTo(kGatewayId));
            }
        }

        [Test]
        public async Task LoadsTheImportedLayersOfTheRulesToModifyOrDelete()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            Rulebase layer = new() { Id = 1, Uid = "layer-uid" };
            Rulebase section = new() { Id = 2, Uid = "section-uid" };
            List<Rule> rules = [FirewallRuleLayersTest.RuleIn("a", section)];
            List<RulebaseLink> sectionLinks = [FirewallRuleLayersTest.SectionLink(layer, section)];
            api.RuleLayers = new FirewallRuleLayerQueryResult { Rules = rules, SectionLinks = sectionLinks };
            ApiFirewallWriteDataSource dataSource = new(api);

            FirewallWriteContext deleteContext = await dataSource.LoadContext(Task(WfTaskType.rule_delete, Rule("a")));
            FirewallWriteContext accessContext = await dataSource.LoadContext(Task(WfTaskType.access, Rule("a")));
            int ruleLayerQueries = api.Calls.Count(call => call.Query == FWO.Api.Client.Queries.RuleQueries.getRuleLayers);
            int sectionLinkQueries = api.Calls.Count(call => call.Query == FWO.Api.Client.Queries.RuleQueries.getSectionLinks);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(deleteContext.RuleLayerUids["a"], Is.EqualTo("layer-uid"));
                Assert.That(accessContext.RuleLayerUids, Is.Empty);
                Assert.That(ruleLayerQueries, Is.EqualTo(1));
                Assert.That(sectionLinkQueries, Is.EqualTo(1), "each query has one root field, as the api returns the first one only");
            }
        }

        [Test]
        public async Task LoadsNoRuleLayersForVendorsThatDoNotAddressRulesInTheirLayer()
        {
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            api.Managements[0].DeviceType = new DeviceType { Id = FirewallVendors.kOpnSense, Name = "OPNsense" };
            api.FailingQuery = FWO.Api.Client.Queries.RuleQueries.getRuleLayers;
            FirewallWriteContext context = await new ApiFirewallWriteDataSource(api).LoadContext(Task(WfTaskType.rule_delete, Rule("a")));
            Assert.That(context.RuleLayerUids, Is.Empty);
        }

        [Test]
        public void MissingManagementOrForeignGatewayFails()
        {
            WfImplTask withoutManagement = Task(WfTaskType.group_delete);
            withoutManagement.ManagementId = null;
            withoutManagement.DeviceId = null;
            WfImplTask unknownGatewayWithoutManagement = Task(WfTaskType.rule_delete);
            unknownGatewayWithoutManagement.ManagementId = null;
            unknownGatewayWithoutManagement.DeviceId = 999;
            WfImplTask unknownManagement = Task(WfTaskType.group_delete);
            unknownManagement.ManagementId = 999;
            WfImplTask foreignGateway = Task(WfTaskType.group_delete);
            foreignGateway.DeviceId = 999;
            ApiFirewallWriteDataSource dataSource = new(Api());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(withoutManagement))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(unknownGatewayWithoutManagement))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(unknownManagement))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
                Assert.That(Assert.ThrowsAsync<FirewallWriteException>(() => dataSource.LoadContext(foreignGateway))?.Error.Kind,
                    Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
            }
        }
    }
}
