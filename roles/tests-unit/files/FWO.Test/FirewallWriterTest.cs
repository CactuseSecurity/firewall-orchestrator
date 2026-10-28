using FWO.Basics.Exceptions;
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
    /// <summary>End-to-end request building and session handling of ModifyFirewall per vendor, against a simulated firewall.</summary>
    [TestFixture]
    [Parallelizable]
    internal class FirewallWriterTest
    {
        private const string kCheckPointLogin = "{\"sid\":\"sid-1\"}";
        private const string kFortiManagerOk = "{\"result\":[{\"status\":{\"code\":0}}],\"session\":\"fmg-session\"}";
        private static readonly List<string> kCheckPointHostCalls = ["login", "add-host", "publish", "logout"];
        private static readonly List<string> kCheckPointInstallCalls = ["login", "delete-group", "publish", "install-policy", "logout"];
        private static readonly List<string> kCheckPointFailedCalls = ["login", "add-host", "discard", "logout"];
        private static readonly List<string> kFortiOsMemberCalls =
        [
            "Post /api/v2/cmdb/firewall/addrgrp/grp%201/member", "Post /api/v2/cmdb/firewall/addrgrp/grp%201/member",
            "Delete /api/v2/cmdb/firewall/addrgrp/grp%201/member/old"
        ];

        private static (FirewallWriter Writer, FirewallSimulation Simulation, SimulatedFirewallWriteClientFactory Factory) Writer(FirewallWriteContext context)
        {
            FirewallSimulation simulation = new();
            SimulatedFirewallWriteClientFactory factory = new(simulation);
            return (new FirewallWriter(new FixedFirewallWriteDataSource(context), factory), simulation, factory);
        }

        private static List<string> Resources(FirewallSimulation simulation) => [.. simulation.Requests.Select(request => request.Resource)];

        [Test]
        public async Task CheckPoint_LogsInWritesPublishesAndLogsOut()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointMds, configPath: "Domain1"));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));

            JsonNode login = JsonNode.Parse(simulation.Requests[0].Body!)!;
            JsonNode host = JsonNode.Parse(simulation.Requests[1].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.Steps, Has.Count.EqualTo(2));
                Assert.That(Resources(simulation), Is.EqualTo(kCheckPointHostCalls));
                Assert.That(login["domain"]!.GetValue<string>(), Is.EqualTo("Domain1"));
                Assert.That(login["password"]!.GetValue<string>(), Is.EqualTo("secret"));
                Assert.That(simulation.Requests[1].Headers["X-chkp-sid"], Is.EqualTo("sid-1"));
                Assert.That(host["ip-address"]!.GetValue<string>(), Is.EqualTo("10.0.0.1"));
            }
        }

        [Test]
        public async Task CheckPoint_InstallsPolicyWhenConfigured()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true, FwWriteDeleteObjects = true };
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointR8x, settings));
            simulation.Handler = request => Ok(request.Resource == "login" ? kCheckPointLogin : "{}");

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));

            JsonNode install = JsonNode.Parse(simulation.Requests[3].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(Resources(simulation), Is.EqualTo(kCheckPointInstallCalls));
                Assert.That(install["policy-package"]!.GetValue<string>(), Is.EqualTo("Standard"));
                Assert.That(install["targets"]![0]!.GetValue<string>(), Is.EqualTo("gw1_vd1"));
                Assert.That(simulation.Requests[1].Body, Does.Contain("\"rule-1\""));
            }
        }

        [Test]
        public async Task CheckPoint_DiscardsOnErrorAndStops()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointR8x));
            simulation.Handler = request => request.Resource switch
            {
                "login" => Ok(kCheckPointLogin),
                "add-host" => Response(HttpStatusCode.BadRequest, "{\"message\":\"invalid\"}"),
                _ => Ok("{}")
            };

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1"), Network("h2", "10.0.0.2")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("HostCreate"));
                Assert.That(result.Steps.Single().StatusCode, Is.EqualTo(400));
                Assert.That(Resources(simulation), Is.EqualTo(kCheckPointFailedCalls));
            }
        }

        [Test]
        public async Task CheckPoint_RetriesDuplicateAddressWarningWithIgnoreWarnings()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointR8x));
            int hostCalls = 0;
            simulation.Handler = request => request.Resource switch
            {
                "login" => Ok(kCheckPointLogin),
                "add-host" when hostCalls++ == 0 => Response(HttpStatusCode.BadRequest, "{\"warnings\":[{\"message\":\"More than one object has the same IP\"}]}"),
                _ => Ok("{}")
            };

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(JsonNode.Parse(simulation.Requests[2].Body!)!["ignore-warnings"]!.GetValue<bool>(), Is.True);
            }
        }

        [Test]
        public async Task CheckPoint_LoginFailureIsReportedAndNothingIsWritten()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointR8x));
            simulation.Handler = _ => Response(HttpStatusCode.Unauthorized, "{}");

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("login"));
                Assert.That(simulation.Requests, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public async Task TemplateErrors_SendNothing()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteDeleteObjects = true };
            settings.FwWriteTemplates[FirewallWriteOperation.GroupDelete] = "{\"name\":\"@@NOT_A_PLACEHOLDER@@\"}";
            (FirewallWriter writer, FirewallSimulation simulation, SimulatedFirewallWriteClientFactory factory) = Writer(Context(FirewallVendors.kCheckPointR8x, settings));

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("Unhandled placeholder found"));
                Assert.That(simulation.Requests, Is.Empty);
                Assert.That(factory.CreatedClients, Is.Zero);
            }
        }

        [Test]
        public async Task UnsupportedOperationAndDeviceType_AreReported()
        {
            (FirewallWriter azureWriter, FirewallSimulation azureSimulation, _) = Writer(Context(FirewallVendors.kAzure));
            (FirewallWriter otherWriter, _, _) = Writer(Context(31));

            ModifyFirewallResult azureResult = await azureWriter.ModifyFirewall(Task(WfTaskType.object_create, Service("svc", 6, 80)));
            ModifyFirewallResult otherResult = await otherWriter.ModifyFirewall(Task(WfTaskType.group_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(azureResult.Success, Is.False);
                Assert.That(azureResult.Message, Does.Contain("ServiceCreate is not supported"));
                Assert.That(azureSimulation.Requests, Is.Empty);
                Assert.That(otherResult.Success, Is.False);
                Assert.That(otherResult.Message, Does.Contain("not supported"));
            }
        }

        [Test]
        public async Task EmptyTask_WritesNothing()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kCheckPointR8x));
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_modify, Network("x", "10.0.0.1", action: RequestAction.unchanged)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(simulation.Requests, Is.Empty);
            }
        }

        [Test]
        public async Task DataSourceErrors_AreReported()
        {
            FirewallWriter writer = new(new ThrowingDataSource());
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            Assert.That(result.Message, Is.EqualTo("no management"));
        }

        [Test]
        public async Task FortiManager_AddsSessionAndChangesMembersWithoutReplacingTheGroup()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiAdom5ff, configPath: "adom1"));
            simulation.Handler = _ => Ok(kFortiManagerOk);
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add, Network("old", null, action: RequestAction.delete)));
            JsonNode addMembers = JsonNode.Parse(simulation.Requests[1].Body!)!;
            JsonNode removeMembers = JsonNode.Parse(simulation.Requests[2].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests.All(request => request.Resource == "/jsonrpc"), Is.True);
                Assert.That(simulation.Requests.Any(request => request.Body!.Contains("\"get\"")), Is.False);
                Assert.That(addMembers["session"]!.GetValue<string>(), Is.EqualTo("fmg-session"));
                Assert.That(addMembers["method"]!.GetValue<string>(), Is.EqualTo("add"));
                Assert.That(addMembers["params"]![0]!["url"]!.GetValue<string>(), Is.EqualTo("/pm/config/adom/adom1/obj/firewall/addrgrp/grp/member"));
                Assert.That(addMembers["params"]![0]!["data"]!.ToJsonString(), Is.EqualTo("[\"m2\"]"));
                Assert.That(removeMembers["method"]!.GetValue<string>(), Is.EqualTo("delete"));
                Assert.That(removeMembers["params"]![0]!["data"]!.ToJsonString(), Is.EqualTo("[\"old\"]"));
                Assert.That(simulation.Requests[^1].Body, Does.Contain("/sys/logout"));
            }
        }

        /// <summary>A '/' in a name must not change the JSON-RPC url path; FortiManager expects it escaped as "\/".</summary>
        [Test]
        public async Task FortiManager_EscapesSlashInNamesOfUrls()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiManager5ff, configPath: "root"));
            simulation.Handler = _ => Ok(kFortiManagerOk);
            WfImplTask task = Task(WfTaskType.object_modify, Network("net_10.0.0.0/24", "10.0.0.0/24", action: RequestAction.delete));
            ModifyFirewallResult result = await writer.ModifyFirewall(task);
            JsonNode delete = JsonNode.Parse(simulation.Requests[1].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(delete["params"]![0]!["url"]!.GetValue<string>(), Is.EqualTo("/pm/config/adom/root/obj/firewall/address/net_10.0.0.0\\/24"));
            }
        }

        /// <summary>A custom template can still replace the whole member list, merged into the members read from the FortiManager.</summary>
        [Test]
        public async Task FortiManager_CustomTemplateMergesMembers()
        {
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteTemplates[FirewallWriteOperation.GroupAddMembers] =
                """{"method":"update","params":[{"url":"/pm/config/adom/@@ADOM_NAME@@/obj/firewall/addrgrp/@@GROUP_NAME_IN_URL@@","data":{"member":@@MERGED_MEMBERS@@}}]}""";
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiAdom5ff, settings, "adom1"));
            simulation.Handler = request => request.Body!.Contains("\"get\"")
                ? Ok("{\"result\":[{\"status\":{\"code\":0},\"data\":{\"member\":[\"m1\",\"old\"]}}]}")
                : Ok(kFortiManagerOk);
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));
            JsonNode update = JsonNode.Parse(simulation.Requests[2].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(JsonNode.Parse(simulation.Requests[1].Body!)!["params"]![0]!["url"]!.GetValue<string>(),
                    Is.EqualTo("/pm/config/adom/adom1/obj/firewall/addrgrp/grp"));
                Assert.That(update["params"]![0]!["data"]!["member"]!.ToJsonString(), Is.EqualTo("[\"m1\",\"old\",\"m2\"]"));
            }
        }

        [Test]
        public async Task FortiManager_FailingMemberReadStopsTheWrite()
        {
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteTemplates[FirewallWriteOperation.GroupAddMembers] = """{"method":"update","params":[{"data":{"member":@@MERGED_MEMBERS@@}}]}""";
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiAdom5ff, settings, "adom1"));
            simulation.Handler = _ => Ok(kFortiManagerOk);
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("has no url"));
                Assert.That(simulation.Requests.Any(request => request.Body!.Contains("\"update\"")), Is.False);
            }
        }

        /// <summary>A custom template can still replace the whole member list, merged into the members read from FortiOS.</summary>
        [Test]
        public async Task FortiOs_CustomTemplateMergesMembers()
        {
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteEndpoints[FirewallWriteOperation.GroupAddMembers] = "PUT /api/v2/cmdb/firewall/addrgrp/@@GROUP_NAME@@";
            settings.FwWriteTemplates[FirewallWriteOperation.GroupAddMembers] = """{"member":@@MERGED_MEMBERS@@}""";
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiOsGateway, settings));
            simulation.Handler = request => request.Method == Method.Get
                ? Ok("{\"results\":[{\"member\":[{\"name\":\"m1\"}]}]}")
                : Ok("{\"status\":\"success\"}");
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests[0].Method, Is.EqualTo(Method.Get));
                Assert.That(simulation.Requests[1].Body, Is.EqualTo("{\"member\":[{\"name\":\"m1\"},{\"name\":\"m2\"}]}"));
            }
        }

        [Test]
        public async Task FortiOs_FailingMemberReadStopsTheWrite()
        {
            GatewayDeviceSpecificSettings settings = new();
            settings.FwWriteEndpoints[FirewallWriteOperation.GroupAddMembers] = "PUT /api/v2/cmdb/firewall/addrgrp/@@GROUP_NAME@@";
            settings.FwWriteTemplates[FirewallWriteOperation.GroupAddMembers] = """{"member":@@MERGED_MEMBERS@@}""";
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiOsGateway, settings));
            simulation.Handler = _ => Response(HttpStatusCode.NotFound, "{}");
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Does.Contain("Could not read the members of the group"));
                Assert.That(result.Error?.Kind, Is.EqualTo(FirewallWriteErrorKind.NotFound));
                Assert.That(result.IsRetryable, Is.False);
                Assert.That(simulation.Requests.Single().Method, Is.EqualTo(Method.Get));
            }
        }

        [Test]
        public async Task FortiManager_FailedStatusCodeFailsTheStep()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiManager5ff, configPath: "root"));
            simulation.Handler = request => request.Body!.Contains("/sys/login/user") ? Ok(kFortiManagerOk) : Ok("{\"result\":[{\"status\":{\"code\":-2}}]}");
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task FortiOs_UsesBearerTokenAndChangesTheMemberTableEntryByEntry()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiOsGateway));
            simulation.Handler = _ => Ok("{\"status\":\"success\"}");
            WfImplElement add = Network("m2", null);
            add.GroupName = "grp 1";
            WfImplElement addToo = Network("m3", null);
            addToo.GroupName = "grp 1";
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add, addToo,
                Network("old", null, action: RequestAction.delete)));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests.Any(request => request.Method == Method.Get), Is.False);
                Assert.That(simulation.Requests.Select(request => $"{request.Method} {request.Resource}"), Is.EqualTo(kFortiOsMemberCalls));
                Assert.That(simulation.Requests[0].Headers["Authorization"], Is.EqualTo("Bearer secret"));
                Assert.That(simulation.Requests[0].Body, Is.EqualTo("{\"name\":\"m2\"}"));
                Assert.That(simulation.Requests[2].Body, Is.Null);
            }
        }

        [Test]
        public async Task FortiOs_Ipv6ObjectsUseTheAddress6Table()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiOsGateway));
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.object_create,
                Network("net6", "2001:db8::/64"), Network("host4", "10.0.0.1")));
            JsonNode network6 = JsonNode.Parse(simulation.Requests[0].Body!)!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests[0].Resource, Is.EqualTo("/api/v2/cmdb/firewall/address6"));
                Assert.That(network6["ip6"]!.GetValue<string>(), Is.EqualTo("2001:db8::/64"));
                Assert.That(network6["type"]!.GetValue<string>(), Is.EqualTo("ipprefix"));
                Assert.That(simulation.Requests[1].Resource, Is.EqualTo("/api/v2/cmdb/firewall/address"));
                Assert.That(JsonNode.Parse(simulation.Requests[1].Body!)!["subnet"]!.GetValue<string>(), Is.EqualTo("10.0.0.1 255.255.255.255"));
            }
        }

        [Test]
        public async Task FortiOs_DeleteHasNoBody()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kFortiOsManagement));
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.rule_delete, Rule("42")));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests.Single().Method, Is.EqualTo(Method.Delete));
                Assert.That(simulation.Requests.Single().Resource, Is.EqualTo("/api/v2/cmdb/firewall/policy/42"));
                Assert.That(simulation.Requests.Single().Body, Is.Null);
            }
        }

        [Test]
        public async Task OpnSense_ChangesOneAddressAndApplies()
        {
            GatewayDeviceSpecificSettings settings = new() { FwWriteInstallPolicy = true };
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kOpnSense, settings));
            simulation.Handler = request => request.Resource switch
            {
                "/api/firewall/alias/getAliasUUID/grp" => Ok("{\"uuid\":\"uuid-1\"}"),
                "/api/firewall/alias/getItem/uuid-1" => Ok("{\"alias\":{\"type\":{\"network\":{\"selected\":1},\"external\":{\"selected\":0}}}}"),
                "/api/firewall/alias_util/add/grp" => Ok("{\"status\":\"done\"}"),
                "/api/firewall/alias/reconfigure" => Ok("{\"status\":\"ok\"}"),
                "/api/firewall/filter/apply" => Ok("{\"status\":\"OK\\n\\n\"}"),
                _ => Ok("{\"result\":\"saved\"}")
            };
            WfImplElement add = Network("app-server", "10.0.0.2");
            add.GroupName = "grp";

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));

            RecordedFirewallRequest update = simulation.Requests[2];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(update.Resource, Is.EqualTo("/api/firewall/alias_util/add/grp"));
                Assert.That(JsonNode.Parse(update.Body!)!["address"]!.GetValue<string>(), Is.EqualTo("10.0.0.2"));
                Assert.That(update.Headers["Authorization"], Is.EqualTo("Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("writer:secret"))));
                Assert.That(simulation.Requests[1].Resource, Is.EqualTo("/api/firewall/alias/getItem/uuid-1"));
                Assert.That(simulation.Requests[3].Resource, Is.EqualTo("/api/firewall/alias/reconfigure"));
                Assert.That(simulation.Requests[4].Resource, Is.EqualTo("/api/firewall/filter/apply"));
            }
        }

        [Test]
        public async Task OpnSense_ValidationErrorsAndMissingAliasFail()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kOpnSense));
            simulation.Handler = _ => Ok("{\"result\":\"failed\",\"validations\":{\"alias.name\":\"exists\"}}");
            ModifyFirewallResult createResult = await writer.ModifyFirewall(Task(WfTaskType.object_create, Network("h1", "10.0.0.1")));

            simulation.Handler = _ => Ok("[]");
            ModifyFirewallResult deleteResult = await writer.ModifyFirewall(Task(WfTaskType.group_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(createResult.Success, Is.False);
                Assert.That(deleteResult.Success, Is.False);
                Assert.That(deleteResult.Message, Does.Contain("not found"));
            }
        }

        [Test]
        public async Task Azure_GetsTokenResolvesLocationAndMergesIpGroups()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kAzure, configPath: "rg1"));
            simulation.Handler = request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok("{\"access_token\":\"token-1\"}"),
                string resource when resource.Contains("/resourcegroups/rg1") => Ok("{\"location\":\"westeurope\"}"),
                _ when request.Method == Method.Get => Ok("{\"etag\":\"W/\\\"7\\\"\",\"properties\":{\"ipAddresses\":[\"10.0.0.1/32\"]}}"),
                _ => Ok("{}")
            };
            WfImplElement add = Network(null, "10.0.0.2");
            add.GroupName = "ipg";

            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_modify, add));

            RecordedFirewallRequest login = simulation.Requests[0];
            RecordedFirewallRequest update = simulation.Requests[^1];
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(login.Resource, Is.EqualTo("https://login.microsoftonline.com/tenant-1/oauth2/v2.0/token"));
                Assert.That(login.FormParameters["client_id"], Is.EqualTo("writer"));
                Assert.That(login.FormParameters["client_secret"], Is.EqualTo("secret"));
                Assert.That(update.Method, Is.EqualTo(Method.Put));
                Assert.That(update.Resource, Is.EqualTo("/subscriptions/sub-1/resourceGroups/rg1/providers/Microsoft.Network/ipGroups/ipg"));
                Assert.That(update.QueryParameters["api-version"], Is.EqualTo(AzureWriteDefaults.kNetworkApiVersion));
                Assert.That(update.Headers["Authorization"], Is.EqualTo("Bearer token-1"));
                Assert.That(update.Body, Is.EqualTo("{\"location\":\"westeurope\",\"properties\":{\"ipAddresses\":[\"10.0.0.1/32\",\"10.0.0.2/32\"]}}"));
                Assert.That(update.Headers["If-Match"], Is.EqualTo("W/\"7\""));
            }
        }

        [Test]
        public async Task Azure_CreatesRuleInNewRuleCollectionGroup()
        {
            (FirewallWriter writer, FirewallSimulation simulation, _) = Writer(Context(FirewallVendors.kAzureFirewall, configPath: "rg1"));
            simulation.Handler = request => request.Resource switch
            {
                string resource when resource.Contains("oauth2") => Ok("{\"access_token\":\"token-1\"}"),
                _ when request.Method == Method.Get => Response(HttpStatusCode.NotFound, "{}"),
                _ => Response(HttpStatusCode.Created, "{}")
            };

            WfImplTask task = Task(WfTaskType.access, Network(null, "10.0.0.1"),
                Network(null, "10.0.1.0/24", field: ElemFieldType.destination), Service(null, 6, 443));
            task.RuleAction = 1;
            ModifyFirewallResult result = await writer.ModifyFirewall(task);

            JsonNode group = JsonNode.Parse(simulation.Requests[^1].Body!)!;
            JsonNode collection = group["properties"]!["ruleCollections"]![0]!;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(simulation.Requests[^1].Resource, Does.EndWith("/firewallPolicies/Standard/ruleCollectionGroups/FWO"));
                Assert.That(simulation.Requests[^1].Headers["If-None-Match"], Is.EqualTo("*"));
                Assert.That(simulation.Requests[^1].QueryParameters["api-version"], Is.EqualTo(AzureWriteDefaults.kNetworkApiVersion));
                Assert.That(collection["name"]!.GetValue<string>(), Is.EqualTo("FWO-Allow"));
                Assert.That(collection["action"]!["type"]!.GetValue<string>(), Is.EqualTo("Allow"));
                Assert.That(collection["rules"]![0]!["name"]!.GetValue<string>(), Is.EqualTo("rule-1"));
                Assert.That(collection["rules"]![0]!["destinationPorts"]!.ToJsonString(), Is.EqualTo("[\"443\"]"));
            }
        }

        [Test]
        public async Task Azure_FailsWithoutTenant()
        {
            FirewallWriteContext context = Context(FirewallVendors.kAzure, configPath: "rg1");
            context.Management.CloudTenantId = "";
            (FirewallWriter writer, _, _) = Writer(context);
            ModifyFirewallResult result = await writer.ModifyFirewall(Task(WfTaskType.group_delete));
            Assert.That(result.Message, Does.Contain("tenant"));
        }

        [Test]
        public void ClientFactory_CreatesTheVendorClient()
        {
            FirewallWriteClientFactory factory = new();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(factory.Create(Context(FirewallVendors.kCheckPointR8x)), Is.TypeOf<CheckPointWriteClient>());
                Assert.That(factory.Create(Context(FirewallVendors.kFortiGate5ff)), Is.TypeOf<FortiManagerWriteClient>());
                Assert.That(factory.Create(Context(FirewallVendors.kFortiOsGateway)), Is.TypeOf<FortiOsWriteClient>());
                Assert.That(factory.Create(Context(FirewallVendors.kOpnSense)), Is.TypeOf<OpnSenseWriteClient>());
                Assert.That(factory.Create(Context(FirewallVendors.kAzureFirewall)), Is.TypeOf<AzureWriteClient>());
                Assert.Throws<NotSupportedException>(() => factory.Create(Context(31)));
            }
        }

        [Test]
        public async Task MissingCredentialsOrHost_AreReported()
        {
            FirewallWriteContext noSecret = Context(FirewallVendors.kFortiOsGateway);
            noSecret.Management.ExportCredential.Secret = "";
            FirewallWriteContext noHost = Context(FirewallVendors.kCheckPointR8x);
            noHost.Management.Hostname = "";

            ModifyFirewallResult noSecretResult = await Writer(noSecret).Writer.ModifyFirewall(Task(WfTaskType.group_delete));
            ModifyFirewallResult noHostResult = await new FirewallWriter(new FixedFirewallWriteDataSource(noHost)).ModifyFirewall(Task(WfTaskType.group_delete));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(noSecretResult.Message, Does.Contain("Export credentials missing"));
                Assert.That(noHostResult.Message, Does.Contain("has no host name"));
            }
        }

        private sealed class ThrowingDataSource : IFirewallWriteDataSource
        {
            public Task<FirewallWriteContext> LoadContext(WfImplTask implTask) => throw new ConfigException("no management");
        }
    }
}
