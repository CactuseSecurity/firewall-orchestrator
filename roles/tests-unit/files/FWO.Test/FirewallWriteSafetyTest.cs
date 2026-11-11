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
        private static readonly List<string> kModifyOnlyCalls = ["login", "set-host", "publish", "logout"];
        private static readonly List<string> kSkippedObjectDeletions = ["HostDelete 'h2'", "ServiceDelete 'svc'", "Ipv6HostDelete 'h6'"];

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

        /// <summary>
        /// The package of the current import wins over device.package_name, which no import maintains any more and may still
        /// hold a value from an older version.
        /// </summary>
        [Test]
        public async Task CheckPoint_InstallsThePackageOfTheImportNotAStalePackageName()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true };
            FirewallGatewayRulebases rulebases = FirewallGatewayRulebases.FromLinks(FirewallGatewayRulebasesTest.CheckPointLinks());
            FirewallWriteContext context = Context(FirewallVendors.kCheckPointR8x, settings);
            context.Gateway!.Package = "Stale Package";
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(new FirewallWriteContext
            {
                Management = context.Management,
                Gateway = context.Gateway,
                Settings = settings,
                Rulebases = rulebases
            });
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.rule_delete, Rule("uid-1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(JsonNode.Parse(simulation.Requests[3].Body!)!["policy-package"]!.GetValue<string>(), Is.EqualTo("Standard"));
            }
        }

        /// <summary>IPv6 objects use their own operations, which Azure (IPv4 only IP groups) does not ship.</summary>
        [Test]
        public async Task Ipv6ObjectsUseTheirOwnOperations()
        {
            (FirewallWriter checkPoint, FirewallSimulation checkPointSimulation) = Writer(Context(FirewallVendors.kCheckPointR8x));
            checkPointSimulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult checkPointResult = await checkPoint.ModifyFirewall(Task(WfTaskType.object_create, Network("h6", "2001:db8::1")));
            (FirewallWriter azure, FirewallSimulation azureSimulation) = Writer(Context(FirewallVendors.kAzure, configPath: "rg1"));
            ModifyFirewallResult azureResult = await azure.ModifyFirewall(Task(WfTaskType.object_create, Network("h6", "2001:db8::1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(checkPointResult.Success, Is.True, checkPointResult.Message);
                Assert.That(checkPointResult.Steps[0].Operation, Is.EqualTo(FirewallWriteOperation.Ipv6HostCreate));
                Assert.That(JsonNode.Parse(checkPointSimulation.Requests[1].Body!)!["ip-address"]!.GetValue<string>(), Is.EqualTo("2001:db8::1"));
                Assert.That(azureResult.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.Unsupported));
                Assert.That(azureResult.Message, Does.Contain("Ipv6HostCreate is not supported"));
                Assert.That(azureSimulation.Requests, Is.Empty);
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
            FirewallWriteContext context = Context(FirewallVendors.kCheckPointR8x,
                new GatewayDeviceSpecificSettings { FwWriteInstallPolicy = true, FwWriteDeleteObjects = true });
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(new FirewallWriteContext { Management = context.Management, Settings = context.Settings });
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("@@GATEWAY_NAME@@ (the task names no gateway)"));
                Assert.That(simulation.Requests, Is.Empty);
            }
        }

        /// <summary>
        /// By default objects, services and groups are not deleted on the firewall, because they may be used elsewhere: the
        /// deletions are left out and reported, the rest of the task is written.
        /// </summary>
        [Test]
        public async Task ObjectDeletionsAreSkippedByDefault()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kCheckPointR8x, new GatewayDeviceSpecificSettings()));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult objects = await writer.ModifyFirewall(Task(WfTaskType.object_modify, Network("h1", "10.0.0.1", action: RequestAction.modify),
                Network("h2", "10.0.0.2", action: RequestAction.delete), Service("svc", 6, 80, action: RequestAction.delete),
                Network("h6", "2001:db8::6", action: RequestAction.delete)));
            int callsForObjects = simulation.Requests.Count;
            ModifyFirewallResult group = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(new GlobalDeviceSpecificSettings().FwWriteDeleteObjects, Is.False);
                Assert.That(objects.Success, Is.True, objects.Message);
                Assert.That(simulation.Requests.Take(callsForObjects).Select(request => request.Resource), Is.EqualTo(kModifyOnlyCalls));
                Assert.That(objects.SkippedDeletions, Is.EqualTo(kSkippedObjectDeletions));
                Assert.That(objects.Message, Does.Contain("Not deleted on the firewall"));
                Assert.That(group.Success, Is.True, group.Message);
                Assert.That(group.Message, Does.StartWith("Nothing to write."));
                Assert.That(group.SkippedDeletions.Single(), Is.EqualTo("GroupDelete 'rule-1'"));
                Assert.That(simulation.Requests, Has.Count.EqualTo(callsForObjects));
            }
        }

        [Test]
        public async Task ObjectDeletionsAreSentWhenSwitchedOn()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kCheckPointR8x,
                new GatewayDeviceSpecificSettings { FwWriteDeleteObjects = true }));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_modify, Network("h2", "10.0.0.2", action: RequestAction.delete)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.SkippedDeletions, Is.Empty);
                Assert.That(simulation.Requests.Select(request => request.Resource), Does.Contain("delete-host"));
            }
        }

        /// <summary>Deleting rules does not depend on the object deletion setting.</summary>
        [Test]
        public async Task RuleDeletionsAreSentByDefault()
        {
            (FirewallWriter writer, FirewallSimulation simulation) = Writer(Context(FirewallVendors.kCheckPointR8x, new GatewayDeviceSpecificSettings()));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.rule_delete, Rule("uid-1")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.SkippedDeletions, Is.Empty);
                Assert.That(simulation.Requests.Select(request => request.Resource), Does.Contain("delete-access-rule"));
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
