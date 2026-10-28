using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test;

/// <summary>Persistent OPNsense member updates must preserve concurrent edits and refuse unsupported mutations.</summary>
[TestFixture]
[Parallelizable]
internal sealed class OpnSenseWriteClientTest
{
    private const string kLookupPath = "/api/firewall/alias/getAliasUUID/grp";
    private const string kItemPath = "/api/firewall/alias/getItem/uuid-1";
    private const string kAddPath = "/api/firewall/alias_util/add/grp";
    private const string kDeletePath = "/api/firewall/alias_util/delete/grp";
    private const string kDone = "{\"status\":\"done\"}";
    private static readonly List<string> ExpectedAddresses = ["10.0.0.99", "10.0.0.2", "2001:db8::1/128"];

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

    /// <summary>A group change referencing a literal address or alias name.</summary>
    private static WfImplElement Member(string name, RequestAction action = RequestAction.create)
    {
        WfImplElement member = Network(name, null, action: action);
        member.GroupName = "grp";
        return member;
    }

    /// <summary>Concurrent additions after the last read survive both member additions and removals.</summary>
    [Test]
    public async Task MemberChanges_PreserveUpdatesAfterTheLastRead()
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        HashSet<string> configured = new() { "10.0.0.1" };
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
                configured.Add("10.0.0.99");
                return Ok(snapshot);
            }
            if (request.Resource is kAddPath or kDeletePath)
            {
                string address = JsonNode.Parse(request.Body!)!["address"]!.GetValue<string>();
                if (request.Resource == kAddPath)
                {
                    configured.Add(address);
                }
                else
                {
                    configured.Remove(address);
                }
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

    /// <summary>Unsupported references and malformed CIDRs fail planning before even another valid member is written.</summary>
    [TestCase("nested_alias", FirewallWriteErrorKind.Unsupported)]
    [TestCase("10.0.0.1-10.0.0.9", FirewallWriteErrorKind.Unsupported)]
    [TestCase("fe80::1%eth0", FirewallWriteErrorKind.Unsupported)]
    [TestCase("1234", FirewallWriteErrorKind.Unsupported)]
    [TestCase("10.0.0.1/33", FirewallWriteErrorKind.InvalidTaskData)]
    [TestCase("2001:db8::/129", FirewallWriteErrorKind.InvalidTaskData)]
    [TestCase("10.0.0.1/-1", FirewallWriteErrorKind.InvalidTaskData)]
    [TestCase("10.0.0.1/no-prefix", FirewallWriteErrorKind.InvalidTaskData)]
    public async Task InvalidMembers_SendNothing(string member, FirewallWriteErrorKind kind)
    {
        (FirewallWriter writer, FirewallSimulation simulation) = Writer();
        WfImplTask task = Task(WfTaskType.group_modify, Member("10.0.0.2"), Member(member));
        ModifyFirewallResult result = await writer.ModifyFirewall(task);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error?.Kind, Is.EqualTo(kind));
            Assert.That(result.IsRetryable, Is.False);
            Assert.That(simulation.Requests, Is.Empty);
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
