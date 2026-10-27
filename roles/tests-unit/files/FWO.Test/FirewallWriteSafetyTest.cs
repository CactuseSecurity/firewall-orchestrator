using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Workflow;
using FWO.ExternalSystems.FirewallWrite;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json.Nodes;
using static FWO.Test.FirewallWriteTestSupport;

namespace FWO.Test
{
    /// <summary>
    /// Writes that must not happen: rules widened to any, calls with empty target names, and changes that would overwrite
    /// what was changed on the firewall in the meantime.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class FirewallWriteSafetyTest
    {
        private const string kCheckPointLogin = "{\"sid\":\"sid-1\"}";
        private const string kAzureToken = "{\"access_token\":\"token-1\"}";
        private const string kAzureLocation = "{\"location\":\"westeurope\"}";

        private static (FirewallWriter Writer, FirewallSimulation Simulation) Writer(FirewallWriteContext context)
        {
            FirewallSimulation simulation = new();
            return (new FirewallWriter(new FixedFirewallWriteDataSource(context), new SimulatedFirewallWriteClientFactory(simulation)), simulation);
        }

        private static FirewallWriteContext ContextWithoutRulebaseNames(GlobalDeviceSpecificSettings settings, FirewallGatewayRulebases rulebases)
        {
            FirewallWriteContext context = Context(FirewallVendors.kCheckPointR8x, settings);
            context.Gateway!.Package = null;
            context.Gateway.LocalRulebase = null;
            return new FirewallWriteContext { Management = context.Management, Gateway = context.Gateway, Settings = settings, Rulebases = rulebases };
        }

        private static WfImplTask AccessTask(int? ruleAction, params WfImplElement[] elements)
        {
            WfImplTask task = Task(WfTaskType.access, elements);
            task.RuleAction = ruleAction;
            return task;
        }

        /// <summary>An access task without elements or action is refused instead of being written as any-any-accept.</summary>
        [TestCase(FirewallVendors.kCheckPointR8x)]
        [TestCase(FirewallVendors.kFortiManager5ff)]
        [TestCase(FirewallVendors.kFortiOsGateway)]
        [TestCase(FirewallVendors.kOpnSense)]
        [TestCase(FirewallVendors.kAzureFirewall)]
        public async Task RuleWithoutElementsOrActionIsRefused(int deviceTypeId)
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(deviceTypeId, configPath: "rg1"));
            ModifyFirewallResult withoutElements = await writer.ModifyFirewall(AccessTask(1));
            ModifyFirewallResult withoutAction = await writer.ModifyFirewall(AccessTask(null, Network("src", "10.0.0.1"),
                Network("dst", "10.0.1.1", field: ElemFieldType.destination), Service("https", 6, 443)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutElements.Success, Is.False);
                Assert.That(withoutElements.Message, Does.Contain("no source element"));
                Assert.That(withoutAction.Success, Is.False);
                Assert.That(withoutAction.Message, Does.Contain("no rule action"));
                Assert.That(simulation.Requests, Is.Empty);
            }
        }

        /// <summary>Any is only written when the task asks for the vendor's any object explicitly.</summary>
        [Test]
        public async Task RuleWithExplicitAnyObjectsIsWritten()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kFortiOsGateway));
            ModifyFirewallResult result = await writer.ModifyFirewall(AccessTask(1, Network("all", null),
                Network("all", null, field: ElemFieldType.destination), Service("ALL", 0, 0)));
            JsonNode rule = JsonNode.Parse(simulation.Requests.Single().Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(rule["srcaddr"]!.ToJsonString(), Is.EqualTo("[{\"name\":\"all\"}]"));
                Assert.That(rule["service"]!.ToJsonString(), Is.EqualTo("[{\"name\":\"ALL\"}]"));
                Assert.That(rule["action"]!.GetValue<string>(), Is.EqualTo("accept"));
            }
        }

        /// <summary>Rules and installs need layer and package; without gateway names they come from the rulebase links.</summary>
        [Test]
        public async Task CheckPoint_UsesLayerAndPackageOfTheRulebaseLinks()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true };
            FirewallGatewayRulebases rulebases = FirewallGatewayRulebases.FromLinks(FirewallGatewayRulebasesTest.CheckPointLinks());
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(ContextWithoutRulebaseNames(settings, rulebases));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.rule_delete, Rule("uid-1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(JsonNode.Parse(simulation.Requests[1].Body!)!["layer"]!.GetValue<string>(), Is.EqualTo("Network"));
                Assert.That(JsonNode.Parse(simulation.Requests[3].Body!)!["policy-package"]!.GetValue<string>(), Is.EqualTo("Standard"));
            }
        }

        [Test]
        public async Task CheckPoint_WithoutLayerAndPackageSendsNothing()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true };
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(ContextWithoutRulebaseNames(settings, FirewallGatewayRulebases.None));
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.rule_delete, Rule("uid-1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("No value for @@LAYER_NAME@@"));
                Assert.That(simulation.Requests, Is.Empty);
            }
        }

        [Test]
        public async Task InstallWithoutGatewaySendsNothing()
        {
            FirewallWriteContext context = Context(FirewallVendors.kCheckPointR8x, new GatewayDeviceSpecificSettings { FwWriteInstallPolicy = true });
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(new FirewallWriteContext { Management = context.Management, Settings = context.Settings });
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("@@GATEWAY_NAME@@ (the task names no gateway)"));
                Assert.That(simulation.Requests, Is.Empty);
            }
        }

        /// <summary>A rule collection group read with an ETag is only written back if nobody changed it meanwhile.</summary>
        [Test]
        public async Task Azure_RuleWriteIsConditionalOnTheEtagRead()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kAzureFirewall, configPath: "rg1"));
            simulation.Handler = request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok(kAzureToken),
                _ when request.Method == Method.Get => Ok("{\"etag\":\"e1\",\"properties\":{\"priority\":100,\"ruleCollections\":[]}}"),
                _ => Response(HttpStatusCode.PreconditionFailed, "{\"error\":{\"code\":\"PreconditionFailed\"}}")
            };
            ModifyFirewallResult result = await writer.ModifyFirewall(AccessTask(1, Network(null, "10.0.0.1"),
                Network(null, "10.0.1.1", field: ElemFieldType.destination), Service(null, 6, 443)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("412"));
                Assert.That(simulation.Requests[^1].Method, Is.EqualTo(Method.Put));
                Assert.That(simulation.Requests[^1].Headers["If-Match"], Is.EqualTo("e1"));
                Assert.That(simulation.Requests[^1].Headers.ContainsKey("If-None-Match"), Is.False);
            }
        }

        [Test]
        public async Task Azure_WithoutEtagNothingIsMerged()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kAzure, configPath: "rg1"));
            simulation.Handler = request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok(kAzureToken),
                string resource when resource.Contains("/resourcegroups/rg1") => Ok(kAzureLocation),
                _ => Ok("{\"properties\":{\"ipAddresses\":[]}}")
            };
            WfImplElement add = Network(null, "10.0.0.2");
            add.GroupName = "ipg";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("no ETag"));
                Assert.That(simulation.Requests.Any(request => request.Method == Method.Put), Is.False);
            }
        }

        /// <summary>Creating an IP group must not replace an existing one of the same name.</summary>
        [Test]
        public async Task Azure_ObjectCreateDoesNotReplaceAnExistingIpGroup()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kAzure, configPath: "rg1"));
            simulation.Handler = request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok(kAzureToken),
                string resource when resource.Contains("/resourcegroups/rg1") => Ok(kAzureLocation),
                _ => Response(HttpStatusCode.PreconditionFailed, "{}")
            };
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(simulation.Requests[^1].Method, Is.EqualTo(Method.Put));
                Assert.That(simulation.Requests[^1].Headers["If-None-Match"], Is.EqualTo("*"));
            }
        }
    }
}
