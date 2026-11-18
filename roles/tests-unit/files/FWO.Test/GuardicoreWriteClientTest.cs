using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    /// <summary>
    /// Writing groups as Guardicore labels through the firewall writer: labels are looked up by key and value, created with
    /// their IP criteria, changed only where they differ, deleted by id, and nothing is written twice.
    /// </summary>
    [TestFixture]
    internal class GuardicoreWriteClientTest
    {
        private const string kAuthenticatePath = "/api/v3.0/authenticate";
        private const string kLabelsPath = "/api/v4.0/labels";
        private const string kBulkPath = "/api/v4.0/labels/bulk";
        private const string kNoLabels = """{"objects":[]}""";
        private const string kAppRoleName = "AR4711-001";
        private const string kIssueExampleBody =
            """[{"key":"AppRole","value":"AR4711-001","criteria":[{"field":"numeric_ip_addresses","op":"SUBNET","argument":"1.2.3.4/32"},""" +
            """{"field":"numeric_ip_addresses","op":"SUBNET","argument":"2.3.4.0/24"},""" +
            """{"field":"numeric_ip_addresses","op":"RANGE","argument":"1.1.1.0-1.1.1.17"}]}]""";

        private static readonly ModellingNamingConvention NamingConvention = new() { AppRolePattern = "AR", AppZone = "AZ", NetworkAreaPattern = "NA" };
        private static readonly List<string> kExcludedAppRoles = [nameof(ModellingTypes.ModObjectType.AppRole)];
        private static readonly List<string> kExcludedAppServers = [nameof(ModellingTypes.ModObjectType.AppServer)];
        private static readonly List<string> kUpdatedCriteria = ["10.0.0.1/32", "10.0.0.3/32"];

        private static string ExistingLabel(params string[] subnets)
        {
            JsonArray criteria = [];
            int index = 1;
            foreach (string subnet in subnets)
            {
                criteria.Add(new JsonObject { ["id"] = $"c{index++}", ["field"] = "numeric_ip_addresses", ["op"] = "SUBNET", ["argument"] = subnet });
            }
            JsonObject label = new() { ["id"] = "lab-1", ["key"] = "AppRole", ["value"] = kAppRoleName, ["dynamic_criteria"] = criteria };
            JsonArray objects = [label];
            return new JsonObject { ["objects"] = objects }.ToJsonString();
        }

        private static FirewallSimulation Simulation(string labels, string bulkAnswer = """{"succeeded":["lab-1"],"failed":[]}""")
        {
            FirewallSimulation simulation = new();
            simulation.Handler = request => request.Resource switch
            {
                kAuthenticatePath => FirewallWriteTestSupport.Ok("""{"access_token":"tok"}"""),
                kLabelsPath => FirewallWriteTestSupport.Ok(labels),
                kBulkPath => FirewallWriteTestSupport.Ok(bulkAnswer),
                _ => FirewallWriteTestSupport.Ok("{}")
            };
            return simulation;
        }

        private static FirewallWriteContext Context(GlobalDeviceSpecificSettings? settings = null, int deviceTypeId = FirewallVendors.kGuardicoreManagement) =>
            FirewallWriteTestSupport.Context(deviceTypeId, settings) with { NamingConvention = NamingConvention };

        private static Task<ModifyFirewallResult> Write(FirewallSimulation simulation, WfImplTask task, FirewallWriteContext? context = null) =>
            new FirewallWriter(new FixedFirewallWriteDataSource(context ?? Context()), new SimulatedFirewallWriteClientFactory(simulation)).ModifyFirewall(task);

        private static WfImplElement Member(string ip, string? ipEnd = null, RequestAction action = RequestAction.create, string groupName = kAppRoleName)
        {
            WfImplElement member = FirewallWriteTestSupport.Network(ip, ip, ipEnd, action: action);
            member.GroupName = groupName;
            return member;
        }

        [Test]
        public async Task GroupCreate_CreatesAMissingLabelWithTheIpCriteriaOfTheIssue()
        {
            FirewallSimulation simulation = Simulation(kNoLabels);
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create,
                Member("1.2.3.4"), Member("2.3.4.0/24"), Member("1.1.1.0", "1.1.1.17")));

            RecordedFirewallRequest lookup = simulation.Requests.Single(request => request.Resource == kLabelsPath);
            RecordedFirewallRequest create = simulation.Requests.Single(request => request.Resource == kBulkPath);
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests[0].Resource, Is.EqualTo(kAuthenticatePath));
                Assert.That(JsonNode.Parse(simulation.Requests[0].Body!)!["username"]!.GetValue<string>(), Is.EqualTo("writer"));
                Assert.That(lookup.QueryParameters["key"], Is.EqualTo("AppRole"));
                Assert.That(lookup.QueryParameters["value"], Is.EqualTo(kAppRoleName));
                Assert.That(lookup.Headers["Authorization"], Is.EqualTo("Bearer tok"));
                Assert.That(create.Method, Is.EqualTo(Method.Post));
                Assert.That(JsonNode.DeepEquals(JsonNode.Parse(create.Body!), JsonNode.Parse(kIssueExampleBody)), Is.True, create.Body);
            });
        }

        [Test]
        public async Task GroupModify_ReplacesTheCriteriaThroughTheBulkUpdateOfApiV4()
        {
            FirewallSimulation simulation = Simulation(ExistingLabel("10.0.0.1/32", "10.0.0.2/32"));
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_modify,
                Member("10.0.0.1", action: RequestAction.unchanged), Member("10.0.0.3"), Member("10.0.0.2", action: RequestAction.delete)));

            RecordedFirewallRequest update = simulation.Requests.Single(request => request.Resource == kBulkPath);
            JsonNode label = JsonNode.Parse(update.Body!)![0]!;
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(update.QueryParameters["override"], Is.EqualTo("true"));
                Assert.That(update.QueryParameters["append"], Is.EqualTo("false"));
                Assert.That(label["key"]!.GetValue<string>(), Is.EqualTo("AppRole"));
                Assert.That(label["value"]!.GetValue<string>(), Is.EqualTo(kAppRoleName));
                Assert.That(label["criteria"]!.AsArray().Select(criterion => criterion!["argument"]!.GetValue<string>()), Is.EqualTo(kUpdatedCriteria));
                Assert.That(simulation.Requests.Where(request => request.Resource != kAuthenticatePath).All(request => request.Resource.StartsWith("/api/v4.0/")), Is.True);
            });
        }

        [Test]
        public async Task RepeatedRequest_WritesNothingTwice()
        {
            FirewallSimulation simulation = Simulation(ExistingLabel("10.0.0.1/32", "10.0.0.3/32"));
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create,
                Member("10.0.0.1"), Member("10.0.0.3")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Steps[0].Message, Does.StartWith(FirewallWriteClient.kAlreadyPresentNote));
                Assert.That(simulation.Requests.Exists(request => request.Method != Method.Get && request.Resource != kAuthenticatePath), Is.False);
            });
        }

        [Test]
        public async Task GroupDelete_DeletesTheLabelValueById()
        {
            FirewallSimulation simulation = Simulation(ExistingLabel("10.0.0.1/32"));
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_delete,
                Member("10.0.0.1", action: RequestAction.delete)));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests.Exists(request => request.Method == Method.Delete && request.Resource == "/api/v4.0/labels/lab-1"), Is.True);
            });
        }

        [Test]
        public async Task GroupDelete_OfAMissingLabelSucceedsWithoutDeleting()
        {
            FirewallSimulation simulation = Simulation(kNoLabels);
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_delete, Member("10.0.0.1")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Steps[0].Message, Does.StartWith(FirewallWriteClient.kAlreadyDeletedNote));
                Assert.That(simulation.Requests.Exists(request => request.Method == Method.Delete), Is.False);
            });
        }

        [Test]
        public async Task GroupDelete_ThatIsGoneMeanwhileCountsAsDeleted()
        {
            FirewallSimulation simulation = Simulation(ExistingLabel("10.0.0.1/32"));
            Func<RecordedFirewallRequest, RestResponse> answer = simulation.Handler;
            simulation.Handler = request => request.Method == Method.Delete ? FirewallWriteTestSupport.Response(HttpStatusCode.NotFound, "") : answer(request);
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_delete, Member("10.0.0.1")));
            Assert.That(result.Success, Is.True, result.Message);
        }

        [Test]
        public async Task GroupDelete_IsSkippedWhileDeletingObjectsIsOff()
        {
            FirewallSimulation simulation = Simulation(ExistingLabel("10.0.0.1/32"));
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_delete, Member("10.0.0.1")),
                Context(new GatewayDeviceSpecificSettings { FwWriteDeleteObjects = false }));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.SkippedDeletions, Has.Count.EqualTo(1));
                Assert.That(simulation.Requests, Is.Empty);
            });
        }

        [Test]
        public async Task FailedBulkItems_FailTheTask()
        {
            FirewallSimulation simulation = Simulation(kNoLabels, """{"succeeded":[],"failed":["AppRole: AR4711-001"]}""");
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Rejected));
            });
        }

        [Test]
        public async Task FailedLogin_IsAnAuthenticationError()
        {
            FirewallSimulation simulation = new() { Handler = _ => FirewallWriteTestSupport.Response(HttpStatusCode.Unauthorized, "{}") };
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1")));
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Authentication));
        }

        [Test]
        public async Task FailedLookup_FailsTheTask()
        {
            FirewallSimulation simulation = Simulation(kNoLabels);
            Func<RecordedFirewallRequest, RestResponse> answer = simulation.Handler;
            simulation.Handler = request => request.Resource == kLabelsPath ? FirewallWriteTestSupport.Response(HttpStatusCode.ServiceUnavailable, "") : answer(request);
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.IsRetryable, Is.True);
            });
        }

        [Test]
        public async Task ObjectTasks_WriteNothing()
        {
            FirewallSimulation simulation = Simulation(kNoLabels);
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.object_create, Member("10.0.0.1")));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Message, Does.StartWith("Nothing to write."));
                Assert.That(simulation.Requests, Is.Empty);
            });
        }

        [Test]
        public async Task RuleTasks_AreNotSupportedYet()
        {
            ModifyFirewallResult result = await Write(Simulation(kNoLabels), FirewallWriteTestSupport.Task(WfTaskType.access, Member("10.0.0.1")));
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
        }

        [Test]
        public async Task GroupsThatStandForNoLabel_AreInvalidTaskData()
        {
            ModifyFirewallResult result = await Write(Simulation(kNoLabels), FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1", groupName: "WebServers")));
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
        }

        [Test]
        public async Task MembersWithoutAddress_AreInvalidTaskData()
        {
            WfImplElement member = Member("10.0.0.1");
            member.IpString = null;
            ModifyFirewallResult result = await Write(Simulation(kNoLabels), FirewallWriteTestSupport.Task(WfTaskType.group_create, member));
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.InvalidTaskData));
        }

        [Test]
        public void GroupsNamedKeyAndValue_WriteThatLabel()
        {
            List<RenderedFirewallRequest> requests = new FirewallWriter(new FixedFirewallWriteDataSource(Context()))
                .RenderAll(FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1", groupName: "Stage: Prod")), Context());
            JsonNode body = JsonNode.Parse(requests.Single().Body!)![0]!;
            Assert.Multiple(() =>
            {
                Assert.That(body["key"]!.GetValue<string>(), Is.EqualTo("Stage"));
                Assert.That(body["value"]!.GetValue<string>(), Is.EqualTo("Prod"));
                Assert.That(requests.Single().Step.ReplacesMembers, Is.True);
            });
        }

        [Test]
        public async Task ExcludedObjectTypes_AreNotWritten()
        {
            FirewallSimulation simulation = Simulation(kNoLabels);
            GatewayDeviceSpecificSettings settings = new() { ProvisioningExcludedObjectTypes = kExcludedAppRoles };
            ModifyFirewallResult result = await Write(simulation, FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1")), Context(settings));

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Message, Does.Contain("exclude their object type from provisioning").And.Contain("LabelWrite 'AppRole: AR4711-001'"));
                Assert.That(simulation.Requests, Is.Empty);
            });
        }

        [Test]
        public void ExcludedAppServers_AreIgnored()
        {
            GatewayDeviceSpecificSettings settings = new() { ProvisioningExcludedObjectTypes = kExcludedAppServers };
            FirewallWriteContext context = Context(settings, FirewallVendors.kCheckPointR8x);
            WfImplTask task = FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1"));

            List<RenderedFirewallRequest> requests = new FirewallWriter(new FixedFirewallWriteDataSource(context)).RenderAll(task, context);
            Assert.Multiple(() =>
            {
                Assert.That(requests.Exists(request => request.Operation == FirewallWriteOperation.HostCreate), Is.True);
                Assert.That(requests.Exists(request => request.Operation == FirewallWriteOperation.GroupCreate), Is.True);
            });
        }

        [Test]
        public void ExcludedGroups_LeaveOutTheirMemberObjectsOnOtherVendors()
        {
            GatewayDeviceSpecificSettings settings = new() { ProvisioningExcludedObjectTypes = kExcludedAppRoles };
            FirewallWriteContext context = Context(settings, FirewallVendors.kCheckPointR8x);
            WfImplTask task = FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1"));

            List<RenderedFirewallRequest> requests = new FirewallWriter(new FixedFirewallWriteDataSource(context)).RenderAll(task, context);
            List<RenderedFirewallRequest> withoutExclusion = new FirewallWriter(new FixedFirewallWriteDataSource(context))
                .RenderAll(task, context with { Settings = new GatewayDeviceSpecificSettings() });
            Assert.Multiple(() =>
            {
                Assert.That(requests, Is.Empty);
                Assert.That(withoutExclusion.Exists(request => request.Operation == FirewallWriteOperation.HostCreate), Is.True);
            });
        }

        [Test]
        public void GroupTypes_OfTasks()
        {
            FirewallWriteContext context = Context();
            Assert.Multiple(() =>
            {
                Assert.That(FirewallWriteExclusions.GroupType(FirewallWriteTestSupport.Task(WfTaskType.group_modify, Member("10.0.0.1", groupName: "AppZone: AZ1")), context),
                    Is.EqualTo(ModellingTypes.ModObjectType.AppZone));
                Assert.That(FirewallWriteExclusions.GroupType(FirewallWriteTestSupport.Task(WfTaskType.group_delete, Member("10.0.0.1", groupName: "NA50")), context),
                    Is.EqualTo(ModellingTypes.ModObjectType.NetworkArea));
                Assert.That(FirewallWriteExclusions.GroupType(FirewallWriteTestSupport.Task(WfTaskType.group_create, Member("10.0.0.1", groupName: "Servers")), context), Is.Null);
                Assert.That(FirewallWriteExclusions.GroupType(FirewallWriteTestSupport.Task(WfTaskType.access, Member("10.0.0.1")), context), Is.Null);
            });
        }
    }
}
