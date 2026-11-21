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
        /// <summary>Rule tasks take the marker and the app of the modelled connection from their request task.</summary>
        [Test]
        public async Task RuleTasksTakeTheConnectionMarkerAndAppOfTheirRequestTask()
        {
            const long kModelledRequestTaskId = 9;
            const long kUnmodelledRequestTaskId = 10;
            InMemoryDeviceSpecificSettingsApiConnection api = Api();
            api.RequestTaskAdditionalInfo[kModelledRequestTaskId] = "{\"ConnId\":\"12\",\"ConnMarker\":\"FWOC12\",\"ExtAppId\":\" APP-1234 \"}";
            api.RequestTaskAdditionalInfo[kUnmodelledRequestTaskId] = "{\"ConnMarker\":\"  \"}";
            WfImplTask modelled = Task(WfTaskType.access);
            modelled.ReqTaskId = kModelledRequestTaskId;
            WfImplTask unmodelled = Task(WfTaskType.rule_modify);
            unmodelled.ReqTaskId = kUnmodelledRequestTaskId;
            ApiFirewallWriteDataSource dataSource = new(api);

            FirewallWriteContext modelledContext = await dataSource.LoadContext(modelled);
            FirewallWriteContext unmodelledContext = await dataSource.LoadContext(unmodelled);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(modelledContext.ConnectionMarker, Is.EqualTo("FWOC12"));
                Assert.That(modelledContext.ExtAppId, Is.EqualTo("APP-1234"));
                Assert.That(unmodelledContext.ConnectionMarker, Is.Null, "a blank marker is none");
                Assert.That(unmodelledContext.ExtAppId, Is.Null);
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
