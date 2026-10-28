using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test;

/// <summary>
/// Persistent OPNsense member updates must preserve concurrent edits, match the entries of created groups and refuse
/// unsupported mutations.
/// </summary>
[TestFixture]
[Parallelizable]
internal sealed class OpnSenseWriteClientTest
{
    private const string kLookupPath = "/api/firewall/alias/getAliasUUID/grp";
    private const string kItemPath = "/api/firewall/alias/getItem/uuid-1";
    private const string kAddPath = "/api/firewall/alias_util/add/grp";
    private const string kDeletePath = "/api/firewall/alias_util/delete/grp";
    private const string kApplyPath = "/api/firewall/filter/apply";
    private const string kCreatePath = "/api/firewall/alias/addItem";
    private const string kDone = "{\"status\":\"done\"}";
    private const string kHost = "host";
    private const string kNetwork = "network";
    private static readonly List<string> ExpectedAddresses = ["10.0.0.99/32", "10.0.0.2/32", "2001:db8::1/128"];

    /// <summary>Builds a writer with a simulated OPNsense API.</summary>
    private static (FirewallWriter Writer, FirewallSimulation Simulation) Writer(GlobalDeviceSpecificSettings? settings = null)
    {
        FirewallSimulation simulation = new();
        FirewallWriter writer = new(new FixedFirewallWriteDataSource(Context(FirewallVendors.kOpnSense, settings)),
            new SimulatedFirewallWriteClientFactory(simulation));
        simulation.Handler = request => request.Resource switch
        {
            kLookupPath => Ok("{\"uuid\":\"uuid-1\"}"),
            kItemPath => Ok("{\"alias\":{\"type\":\"network\"}}"),
            _ => Ok(kDone)
        };
        return (writer, simulation);
    }

    /// <summary>A group member with a descriptive name and its address.</summary>
    private static WfImplElement Member(string ip, RequestAction action = RequestAction.create, string? ipEnd = null)
    {
        WfImplElement member = Network("app-server", ip, ipEnd, action: action);
        member.GroupName = "grp";
        return member;
    }

    /// <summary>
    /// Changes the alias content like the OPNsense alias utility: add appends the full-length mask to a single address in
    /// network aliases only; delete removes a single address with and without its mask, a masked address only as written.
    /// </summary>
    private static void ApplyAliasUtility(RecordedFirewallRequest request, HashSet<string> configured, string aliasType)
    {
        string address = JsonNode.Parse(request.Body!)!["address"]!.GetValue<string>();
        string masked = address.Contains('/') ? address : address + (address.Contains(':') ? "/128" : "/32");
        if (request.Resource == kAddPath)
        {
            configured.Add(aliasType == kNetwork ? masked : address);
            return;
        }
        configured.Remove(masked);
        configured.Remove(address);
    }

    /// <summary>Simulates an alias of the given type whose content is held in <paramref name="configured"/>.</summary>
    private static Func<RecordedFirewallRequest, RestResponse> AliasHandler(HashSet<string> configured, string aliasType) => request =>
    {
        if (request.Resource == kLookupPath)
        {
            return Ok("{\"uuid\":\"uuid-1\"}");
        }
        if (request.Resource == kItemPath)
        {
            return Ok(new JsonObject { ["alias"] = new JsonObject { ["type"] = aliasType } }.ToJsonString());
        }
        if (request.Resource is kAddPath or kDeletePath)
        {
            ApplyAliasUtility(request, configured, aliasType);
        }
        return Ok(kDone);
    };

    /// <summary>Concurrent additions after the last read survive both member additions and removals.</summary>
    [Test]
    public async Task MemberChanges_PreserveUpdatesAfterTheLastRead()
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        HashSet<string> configured = new() { "10.0.0.1/32" };
        simulation.Handler = request =>
        {
            if (request.Resource == kLookupPath)
            {
                return Ok("{\"uuid\":\"uuid-1\"}");
            }
            if (request.Resource == kItemPath)
            {
                JsonObject content = new();
                foreach (string member in configured)
                {
                    content[member] = new JsonObject { ["selected"] = 1 };
                }
                string snapshot = new JsonObject { ["alias"] = new JsonObject { ["type"] = "network", ["content"] = content } }.ToJsonString();
                configured.Add("10.0.0.99/32");
                return Ok(snapshot);
            }
            if (request.Resource is kAddPath or kDeletePath)
            {
                ApplyAliasUtility(request, configured, kNetwork);
            }
            return Ok(kDone);
        };
        WfImplTask task = Task(WfTaskType.group_modify, Member("10.0.0.2"), Member("2001:DB8::1/128"),
            Member("10.0.0.1", RequestAction.delete));

        ModifyFirewallResult result = await writer.ModifyFirewall(task);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(configured, Is.EquivalentTo(ExpectedAddresses));
            Assert.That(simulation.Requests.Count(request => request.Resource == kAddPath), Is.EqualTo(2));
            Assert.That(simulation.Requests.Count(request => request.Resource == kDeletePath), Is.EqualTo(1));
            Assert.That(simulation.Requests.Any(request => request.Resource.Contains("setItem")), Is.False);
        }
    }

    /// <summary>A member without address or with an invalid one fails planning before even another valid member is written.</summary>
    [TestCase(null, FirewallWriteErrorKind.Unsupported)]
    [TestCase("fe80::1%eth0", FirewallWriteErrorKind.InvalidTaskData)]
    [TestCase("10.0.0.1/33", FirewallWriteErrorKind.InvalidTaskData)]
    [TestCase("nested_alias", FirewallWriteErrorKind.InvalidTaskData)]
    public async Task InvalidMembers_SendNothing(string? ip, FirewallWriteErrorKind kind)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        WfImplTask task = Task(WfTaskType.group_modify, Member("10.0.0.2"), Member(ip!));
        ModifyFirewallResult result = await writer.ModifyFirewall(task);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error?.Kind, Is.EqualTo(kind));
            Assert.That(result.IsRetryable, Is.False);
            Assert.That(simulation.Requests, Is.Empty);
        }
    }

    /// <summary>
    /// A group FWO creates with named members holds their CIDR entries; a later group modify sends the single address, which
    /// OPNsense matches to the entry written with its mask.
    /// </summary>
    [Test]
    public async Task CreatedGroup_CanBeModifiedLater()
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        ModifyFirewallResult created = await writer.ModifyFirewall(Task(WfTaskType.group_create, Member("10.0.0.2"),
            Member("10.0.1.1", ipEnd: "10.0.1.2")));
        ModifyFirewallResult modified = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2", RequestAction.delete)));

        RecordedFirewallRequest create = simulation.Requests.Single(request => request.Resource == kCreatePath);
        RecordedFirewallRequest remove = simulation.Requests.Single(request => request.Resource == kDeletePath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Success, Is.True, created.Message);
            Assert.That(modified.Success, Is.True, modified.Message);
            Assert.That(JsonNode.Parse(create.Body!)!["alias"]!["content"]!.GetValue<string>(), Is.EqualTo("10.0.0.2/32\n10.0.1.1/32\n10.0.1.2/32"));
            Assert.That(JsonNode.Parse(create.Body!)!["alias"]!["name"]!.GetValue<string>(), Is.EqualTo("grp"));
            Assert.That(simulation.Requests.Count(request => request.Resource == kCreatePath), Is.EqualTo(1));
            Assert.That(JsonNode.Parse(remove.Body!)!["address"]!.GetValue<string>(), Is.EqualTo("10.0.0.2"));
        }
    }

    /// <summary>
    /// Existing host and network aliases store single addresses with or without mask; both are removed, and an added
    /// address takes the form of the alias type.
    /// </summary>
    [TestCase(kHost, "10.0.0.2", "10.0.0.3")]
    [TestCase(kNetwork, "10.0.0.2", "10.0.0.3/32")]
    [TestCase(kNetwork, "10.0.0.2/32", "10.0.0.3/32")]
    public async Task SingleAddresses_MatchExistingAliasEntries(string aliasType, string stored, string expectedAdded)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        HashSet<string> configured = new() { stored, "10.0.0.9" };
        simulation.Handler = AliasHandler(configured, aliasType);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.3"),
            Member("10.0.0.2", RequestAction.delete)));
        HashSet<string> expected = new() { "10.0.0.9", expectedAdded };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(configured, Is.EquivalentTo(expected));
        }
    }

    /// <summary>
    /// Host aliases cannot hold networks: OPNsense would reject the addition and report the removal as done without a
    /// change, so the task is refused before any change is sent, even for a valid single address in the same task.
    /// Network aliases accept the same task.
    /// </summary>
    [TestCase(kHost, "10.0.1.0/24", null, RequestAction.create, false)]
    [TestCase(kHost, "10.0.1.0/24", null, RequestAction.delete, false)]
    [TestCase(kHost, "10.0.0.1", "10.0.0.3", RequestAction.create, false)]
    [TestCase(kNetwork, "10.0.1.0/24", null, RequestAction.create, true)]
    [TestCase(kNetwork, "10.0.0.1", "10.0.0.3", RequestAction.delete, true)]
    public async Task Networks_OnlyInNetworkAliases(string aliasType, string ip, string? ipEnd, RequestAction action, bool supported)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        HashSet<string> configured = [];
        simulation.Handler = AliasHandler(configured, aliasType);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.5"),
            Member(ip, action, ipEnd)));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.EqualTo(supported), result.Message);
            Assert.That(simulation.Requests.Any(request => request.Method != Method.Get), Is.EqualTo(supported));
            if (!supported)
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
                Assert.That(result.IsRetryable, Is.False);
            }
        }
    }

    /// <summary>filter/apply answers the configd output, e.g. "OK\n\n"; only an error status fails the install.</summary>
    [TestCase("{\"status\":\"OK\\n\\n\"}", true)]
    [TestCase("{\"status\":\"ok\"}", true)]
    [TestCase("{\"status\":\"Error (1)\\n\\n\"}", false)]
    public async Task InstallPolicy_StatusOfFilterApply(string response, bool success)
    {
        GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true };
        (FirewallWriter writer, FirewallSimulation simulation) = Writer(settings);
        Func<RecordedFirewallRequest, RestResponse> original = simulation.Handler;
        simulation.Handler = request => request.Resource == kApplyPath ? Ok(response) : original(request);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.EqualTo(success), result.Message);
            Assert.That(simulation.Requests.Last().Resource, Is.EqualTo(kApplyPath));
            if (!success)
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Rejected));
            }
        }
    }

    /// <summary>The utility persists only host/network aliases, never external or nested group types.</summary>
    [TestCase("host", true)]
    [TestCase("network", true)]
    [TestCase("external", false)]
    [TestCase("networkgroup", false)]
    [TestCase("port", false)]
    [TestCase(null, false)]
    public async Task AliasType_ControlsPersistentMemberChanges(string? aliasType, bool supported)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        Func<RecordedFirewallRequest, RestResponse> original = simulation.Handler;
        simulation.Handler = request => request.Resource == kItemPath
            ? Ok(new JsonObject { ["alias"] = new JsonObject { ["type"] = aliasType } }.ToJsonString())
            : original(request);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.EqualTo(supported), result.Message);
            Assert.That(simulation.Requests.Any(request => request.Resource == kAddPath), Is.EqualTo(supported));
            if (!supported)
            {
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
            }
        }
    }

    /// <summary>Old merged-list overrides cannot restore the unsafe whole-alias write.</summary>
    [Test]
    public async Task MergedMemberOverride_IsRefused()
    {
        GatewayDeviceSpecificSettings settings = new();
        settings.FwWriteEndpoints[FirewallWriteOperation.GroupAddMembers] = "POST /api/firewall/alias/setItem/@@OBJECT_UUID@@";
        settings.FwWriteTemplates[FirewallWriteOperation.GroupAddMembers] = "{\"alias\":{\"content\":\"@@MERGED_MEMBERS@@\"}}";
        (FirewallWriter writer, FirewallSimulation simulation) = Writer(settings);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
            Assert.That(result.IsRetryable, Is.False);
            Assert.That(simulation.Requests, Is.Empty);
        }
    }

    /// <summary>HTTP 200 does not turn a rejected or incomplete utility operation into success.</summary>
    [TestCase("{\"status\":\"failed\"}", FirewallWriteErrorKind.Rejected)]
    [TestCase("{\"status\":\"not_an_address\"}", FirewallWriteErrorKind.Rejected)]
    [TestCase("{}", FirewallWriteErrorKind.Unknown)]
    public async Task UtilityFailure_IsReportedAndStopsPublishing(string response, FirewallWriteErrorKind kind)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        Func<RecordedFirewallRequest, RestResponse> original = simulation.Handler;
        simulation.Handler = request => request.Resource == kAddPath ? Ok(response) : original(request);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error?.Kind, Is.EqualTo(kind));
            Assert.That(result.Error?.NativeMessage, Is.EqualTo(response));
            Assert.That(simulation.Requests.Last().Resource, Is.EqualTo(kAddPath));
        }
    }

    /// <summary>Lookup and type-read errors prevent any mutation and keep the normalized firewall error.</summary>
    [TestCase(kLookupPath)]
    [TestCase(kItemPath)]
    public async Task FailedRead_SendsNoMutation(string path)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        Func<RecordedFirewallRequest, RestResponse> original = simulation.Handler;
        simulation.Handler = request => request.Resource == path ? Response(HttpStatusCode.Forbidden, "{}") : original(request);
        ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, Member("10.0.0.2")));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Authentication));
            Assert.That(simulation.Requests.All(request => request.Method == Method.Get), Is.True);
        }
    }
}
