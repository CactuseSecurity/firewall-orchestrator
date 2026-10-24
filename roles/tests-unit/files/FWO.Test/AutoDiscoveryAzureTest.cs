using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class AutoDiscoveryAzureTest
    {
        private const string kSubscriptionId = "00000000-0000-0000-0000-000000000000";
        private const string kResourceGroup = "FWORCH-TEST1";
        private const string kPolicyName = "FirewallPolicy_AzureFirewall1";
        private const string kNewestApiVersion = "2026-01-01";
        private const string kFallbackApiVersion = "2024-05-01";
        private const string kFirewallId = "/subscriptions/" + kSubscriptionId + "/resourceGroups/FWORCH-TEST1/providers/Microsoft.Network/azureFirewalls/AzureFirewall1";
        private const string kPolicyId = "/subscriptions/" + kSubscriptionId + "/resourcegroups/fworch-test1/providers/Microsoft.Network/firewallPolicies/" + kPolicyName;
        private const string kOtherPolicyId = "/subscriptions/" + kSubscriptionId + "/resourceGroups/OTHER-RG/providers/Microsoft.Network/firewallPolicies/OtherPolicy";
        private const string kUnsupportedApiVersionError = "{\"error\":{\"code\":\"NoRegisteredProviderFound\",\"message\":\"no provider\"}}";
        private const string kFirewallsUrlStart = AzureClient.kManagementUrl + "/subscriptions/" + kSubscriptionId + "/providers/Microsoft.Network/azureFirewalls?api-version=";

        private static readonly List<string> ExpectedDeviceNames = ["AzureFirewall1"];
        private static readonly List<string> ExpectedPagedFirewallNames = ["fw1", "fw2"];
        private static readonly List<string> ExpectedImportCredentialLogin = ["tenant-1|client-id|client-secret"];
        private static readonly List<string> ExpectedAdditionalCredentialLogin = ["tenant-1|additional-client-id|additional-secret"];
        private static readonly List<string> ExpectedFirstFirewallName = ["fw1"];
        private static readonly List<string> ExpectedFallbackRequestUrls =
        [
            kFirewallsUrlStart + kNewestApiVersion,
            kFirewallsUrlStart + kFallbackApiVersion
        ];
        private static readonly List<string> ExpectedFallbackUrls =
        [
            kFirewallsUrlStart + kNewestApiVersion,
            kFirewallsUrlStart + kFallbackApiVersion,
            kFirewallsUrlStart + kFallbackApiVersion
        ];

        private static AzureFirewall CreateFirewall(string name, string id, string? policyId)
        {
            return new AzureFirewall
            {
                Name = name,
                Id = id,
                Properties = new AzureFirewallProperties { FirewallPolicy = policyId == null ? null : new AzureSubResource { Id = policyId } }
            };
        }

        private static string FirewallPage(string? nextLink, params AzureFirewall[] firewalls)
        {
            List<object> value = [.. firewalls.Select(fw => new { id = fw.Id, name = fw.Name, properties = new { firewallPolicy = new { id = fw.Properties.FirewallPolicy?.Id } } })];
            return JsonSerializer.Serialize(new { value, nextLink });
        }

        private static Management CreateAzureManagement(int? additionalCredentialId = null)
        {
            return new Management
            {
                Id = 5,
                Name = "azure",
                Hostname = "management.azure.com",
                Port = 443,
                ConfigPath = kResourceGroup,
                CloudSubscriptionId = kSubscriptionId,
                CloudTenantId = "tenant-1",
                DeviceType = new DeviceType { Id = DeviceType.kAzureManagementTypeId, Name = "Azure" },
                ImportCredential = new Credential { Id = 3, Name = "azure-sp", ImportUser = "client-id", Secret = "client-secret", AdditionalCredentialId = additionalCredentialId }
            };
        }

        [Test]
        public void DeviceType_AzureManagementCanBeAutodiscovered()
        {
            DeviceType azureType = new() { Id = DeviceType.kAzureManagementTypeId };
            Assert.Multiple(() =>
            {
                Assert.That(azureType.CanBeAutodiscovered(new Management { Hostname = "management.azure.com" }), Is.True);
                Assert.That(azureType.CanBeAutodiscovered(new Management { Hostname = "file:///tmp/azure.json" }), Is.False);
                Assert.That(azureType.CanBeSupermanager(), Is.False);
                Assert.That(azureType.GetGatewayTypeId(), Is.EqualTo(DeviceType.kAzureFirewallTypeId));
            });
        }

        [Test]
        public void ConvertFirewallsToDevices_KeepsFirewallsWithPolicyInResourceGroup()
        {
            List<AzureFirewall> firewalls =
            [
                CreateFirewall("AzureFirewall1", kFirewallId, kPolicyId),
                CreateFirewall("OtherFirewall", kFirewallId + "-other", kOtherPolicyId),
                CreateFirewall("ClassicFirewall", kFirewallId + "-classic", null)
            ];

            List<Device> devices = AutoDiscoveryAzure.ConvertFirewallsToDevices(firewalls, kResourceGroup);

            Assert.That(devices.Select(device => device.Name), Is.EqualTo(ExpectedDeviceNames));
            Assert.Multiple(() =>
            {
                Assert.That(devices[0].Uid, Is.EqualTo(kFirewallId.ToLowerInvariant()));
                Assert.That(devices[0].LocalRulebase, Is.EqualTo(kPolicyName));
                Assert.That(devices[0].DeviceType.Id, Is.EqualTo(DeviceType.kAzureFirewallTypeId));
            });
        }

        [TestCase(kPolicyId, "fworch-test1")]
        [TestCase("/subscriptions/x/resourceGroups/RG/providers/p/name", "RG")]
        [TestCase("/subscriptions/x/providers/p/name", "")]
        [TestCase("/subscriptions/x/resourceGroups", "")]
        public void GetResourceGroup_ReturnsSegmentAfterResourceGroups(string resourceId, string expected)
        {
            Assert.That(AutoDiscoveryAzure.GetResourceGroup(resourceId), Is.EqualTo(expected));
        }

        [TestCase(kPolicyId, kPolicyName)]
        [TestCase("/a/b/name/", "name")]
        public void GetResourceName_ReturnsLastSegment(string resourceId, string expected)
        {
            Assert.That(AutoDiscoveryAzure.GetResourceName(resourceId), Is.EqualTo(expected));
        }

        [TestCase(kUnsupportedApiVersionError, true)]
        [TestCase("{\"error\":{\"code\":\"InvalidApiVersionParameter\"}}", true)]
        [TestCase("{\"error\":{\"code\":\"AuthorizationFailed\"}}", false)]
        [TestCase("no json", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsUnsupportedApiVersionError_ChecksErrorCode(string? content, bool expected)
        {
            Assert.That(AzureClient.IsUnsupportedApiVersionError(content), Is.EqualTo(expected));
        }

        [Test]
        public async Task GetAzureFirewalls_UsesNewestApiVersionAndFollowsNextLink()
        {
            FakeAzureClient client = new();
            string secondPageUrl = kFirewallsUrlStart + kNewestApiVersion + "&$skiptoken=2";
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage(secondPageUrl, CreateFirewall("fw1", kFirewallId, kPolicyId));
            client.Pages[secondPageUrl] = FirewallPage(null, CreateFirewall("fw2", kFirewallId + "2", kPolicyId));

            List<AzureFirewall> firewalls = await client.GetAzureFirewalls(kSubscriptionId, "token");

            Assert.Multiple(() =>
            {
                Assert.That(firewalls.Select(fw => fw.Name), Is.EqualTo(ExpectedPagedFirewallNames));
                Assert.That(client.ApiVersion, Is.EqualTo(kNewestApiVersion));
                Assert.That(client.RequestedTokens, Is.All.EqualTo("token"));
            });
        }

        [Test]
        public async Task GetAzureFirewalls_FallsBackToOlderApiVersionAndKeepsIt()
        {
            FakeAzureClient client = new();
            client.UnsupportedApiVersionUrls.Add(kFirewallsUrlStart + kNewestApiVersion);
            client.Pages[kFirewallsUrlStart + kFallbackApiVersion] = FirewallPage(null, CreateFirewall("fw1", kFirewallId, kPolicyId));

            await client.GetAzureFirewalls(kSubscriptionId, "token");
            await client.GetAzureFirewalls(kSubscriptionId, "token");

            Assert.Multiple(() =>
            {
                Assert.That(client.ApiVersion, Is.EqualTo(kFallbackApiVersion));
                Assert.That(client.RequestedUrls, Is.EqualTo(ExpectedFallbackUrls));
            });
        }

        [Test]
        public void GetAzureFirewalls_FailsWithoutSupportedApiVersion()
        {
            FakeAzureClient client = new();
            client.UnsupportedApiVersionUrls.Add(kFirewallsUrlStart + kNewestApiVersion);
            client.UnsupportedApiVersionUrls.Add(kFirewallsUrlStart + kFallbackApiVersion);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAzureFirewalls(kSubscriptionId, "token"));
            Assert.That(exception!.Message, Does.Contain("not supported"));
        }

        [Test]
        public void GetAzureFirewalls_RefusesNextLinkToForeignHost()
        {
            FakeAzureClient client = new();
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage("https://evil.example/steal", CreateFirewall("fw1", kFirewallId, kPolicyId));

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAzureFirewalls(kSubscriptionId, "token"));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("unexpected url"));
                Assert.That(client.RequestedUrls, Has.None.Contains("evil"));
            });
        }

        [Test]
        public void GetAzureFirewalls_StopsAtPageLimit()
        {
            FakeAzureClient client = new();
            string firstUrl = kFirewallsUrlStart + kNewestApiVersion;
            client.Pages[firstUrl] = FirewallPage(firstUrl);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAzureFirewalls(kSubscriptionId, "token"));
            Assert.That(exception!.Message, Does.Contain("maximum"));
        }

        [Test]
        public async Task DiscoverGateways_UsesImportCredentialWithoutAdditionalCredential()
        {
            FakeAzureClient client = new();
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage(null, CreateFirewall("AzureFirewall1", kFirewallId, kPolicyId));
            AutoDiscoveryAzure discovery = new(CreateAzureManagement(), new AzureDiscoveryTestApiConn(), true, client);

            List<Device> devices = await discovery.DiscoverGateways();

            Assert.Multiple(() =>
            {
                Assert.That(devices.Select(device => device.Name), Is.EqualTo(ExpectedDeviceNames));
                Assert.That(client.Logins, Is.EqualTo(ExpectedImportCredentialLogin));
            });
        }

        [Test]
        public async Task DiscoverGateways_PrefersAdditionalCredential()
        {
            FakeAzureClient client = new();
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage(null);
            AzureDiscoveryTestApiConn apiConnection = new()
            {
                AdditionalCredential = new Credential { Id = 9, Name = "sp", ImportUser = "additional-client-id", Secret = "additional-secret" }
            };
            AutoDiscoveryAzure discovery = new(CreateAzureManagement(additionalCredentialId: 9), apiConnection, true, client);

            await discovery.DiscoverGateways();

            Assert.Multiple(() =>
            {
                Assert.That(client.Logins, Is.EqualTo(ExpectedAdditionalCredentialLogin));
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain("9"));
            });
        }

        [TestCase("tenant")]
        [TestCase("subscription")]
        [TestCase("resource group")]
        public void DiscoverGateways_FailsWithoutCloudSettings(string missingSetting)
        {
            Management management = CreateAzureManagement();
            switch (missingSetting)
            {
                case "tenant": management.CloudTenantId = ""; break;
                case "subscription": management.CloudSubscriptionId = null; break;
                default: management.ConfigPath = " "; break;
            }
            AutoDiscoveryAzure discovery = new(management, new AzureDiscoveryTestApiConn(), true, new FakeAzureClient());

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => discovery.DiscoverGateways());
            Assert.That(exception!.Message, Does.Contain(missingSetting));
        }

        [Test]
        public async Task Run_ProposesNewFirewallAndRemovalOfVanishedOne()
        {
            FakeAzureClient client = new();
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage(null, CreateFirewall("AzureFirewall1", kFirewallId, kPolicyId));
            Management storedManagement = CreateAzureManagement();
            storedManagement.Devices = [new Device { Id = 11, Name = "RemovedFirewall", Uid = "removed-uid" }];
            AzureDiscoveryTestApiConn apiConnection = new() { ExistingManagements = [storedManagement] };
            AutoDiscoveryAzure discovery = new(CreateAzureManagement(), apiConnection, true, client);

            List<Management> deltas = await discovery.Run();
            List<ActionItem> actions = discovery.ConvertToActions(deltas);

            Assert.That(actions, Has.Count.EqualTo(2));
            ActionItem addAction = actions.First(action => action.ActionType == ActionCode.AddGatewayToExistingManagement.ToString());
            Device addedDevice = JsonSerializer.Deserialize<Device>(addAction.JsonData?.ToString() ?? "")!;
            Assert.Multiple(() =>
            {
                Assert.That(addAction.ManagementId, Is.EqualTo(5));
                Assert.That(addedDevice.Name, Is.EqualTo("AzureFirewall1"));
                Assert.That(addedDevice.LocalRulebase, Is.EqualTo(kPolicyName));
                Assert.That(addedDevice.DeviceType.Id, Is.EqualTo(DeviceType.kAzureFirewallTypeId));
                Assert.That(actions.Any(action => action.ActionType == ActionCode.DeleteGateway.ToString() && action.DeviceId == 11), Is.True);
            });
        }

        [Test]
        public async Task Run_FindsNoChangesForKnownFirewall()
        {
            FakeAzureClient client = new();
            client.Pages[kFirewallsUrlStart + kNewestApiVersion] = FirewallPage(null, CreateFirewall("AzureFirewall1", kFirewallId, kPolicyId));
            Management storedManagement = CreateAzureManagement();
            storedManagement.Devices = [new Device { Id = 11, Name = "AzureFirewall1", Uid = kFirewallId.ToLowerInvariant(), LocalRulebase = kPolicyName }];
            AzureDiscoveryTestApiConn apiConnection = new() { ExistingManagements = [storedManagement] };
            AutoDiscoveryAzure discovery = new(CreateAzureManagement(), apiConnection, true, client);

            List<Management> deltas = await discovery.Run();

            Assert.That(deltas, Is.Empty);
        }

        [Test]
        public void AutoDiscoveryBase_DispatchesAzureManagements()
        {
            Management management = CreateAzureManagement();
            management.CloudTenantId = "";
            AutoDiscoveryBase discovery = new(management, new AzureDiscoveryTestApiConn(), true);

            // fails at the settings check, i.e. before any call to Azure
            Assert.ThrowsAsync<InvalidOperationException>(() => discovery.Run());
        }

        [Test]
        public async Task GetAccessToken_PostsClientCredentialsToTenantTokenEndpoint()
        {
            StubHttpAzureClient client = new();
            client.Handler.Responses.Enqueue((HttpStatusCode.OK, "{\"token_type\":\"Bearer\",\"access_token\":\"tok\"}"));

            string accessToken = await client.GetAccessToken("tenant/../x", "client-id", "client-secret");

            StubHttpMessageHandler.RecordedRequest request = client.Handler.Requests[0];
            Assert.Multiple(() =>
            {
                Assert.That(accessToken, Is.EqualTo("tok"));
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.Url, Is.EqualTo("https://login.microsoftonline.com/tenant%2F..%2Fx/oauth2/v2.0/token"));
                Assert.That(request.Body, Does.Contain("grant_type=client_credentials"));
                Assert.That(request.Body, Does.Contain("client_id=client-id"));
                Assert.That(request.Body, Does.Contain("scope=https%3A%2F%2Fmanagement.azure.com%2F.default"));
                Assert.That(request.Authorization, Is.Null);
            });
        }

        [TestCase(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}")]
        [TestCase(HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}")]
        public void GetAccessToken_FailsWithoutTokenAndDoesNotLeakSecret(HttpStatusCode statusCode, string content)
        {
            StubHttpAzureClient client = new();
            client.Handler.Responses.Enqueue((statusCode, content));

            MailKit.Security.AuthenticationException? exception =
                Assert.ThrowsAsync<MailKit.Security.AuthenticationException>(() => client.GetAccessToken("tenant-1", "client-id", "client-secret"));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain("client-id"));
                Assert.That(exception.Message, Does.Not.Contain("client-secret"));
            });
        }

        [Test]
        public async Task GetPage_SendsBearerTokenAndFallsBackOnUnsupportedApiVersion()
        {
            StubHttpAzureClient client = new();
            client.Handler.Responses.Enqueue((HttpStatusCode.BadRequest, kUnsupportedApiVersionError));
            client.Handler.Responses.Enqueue((HttpStatusCode.OK, FirewallPage(null, CreateFirewall("fw1", kFirewallId, kPolicyId))));

            List<AzureFirewall> firewalls = await client.GetAzureFirewalls(kSubscriptionId, "tok");

            Assert.Multiple(() =>
            {
                Assert.That(firewalls.Select(fw => fw.Name), Is.EqualTo(ExpectedFirstFirewallName));
                Assert.That(firewalls[0].Properties.FirewallPolicy?.Id, Is.EqualTo(kPolicyId));
                Assert.That(client.Handler.Requests.Select(request => request.Url), Is.EqualTo(ExpectedFallbackRequestUrls));
                Assert.That(client.Handler.Requests.Select(request => request.Authorization), Is.All.EqualTo("Bearer tok"));
                Assert.That(client.ApiVersion, Is.EqualTo(kFallbackApiVersion));
            });
        }

        [TestCase(HttpStatusCode.Forbidden, "{\"error\":{\"code\":\"AuthorizationFailed\"}}")]
        [TestCase(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"InvalidResourceType\"}}")]
        public void GetPage_FailsOnOtherErrors(HttpStatusCode statusCode, string content)
        {
            StubHttpAzureClient client = new();
            client.Handler.Responses.Enqueue((statusCode, content));

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAzureFirewalls(kSubscriptionId, "tok"));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Message, Does.Contain(((int)statusCode).ToString()));
                Assert.That(client.Handler.Requests, Has.Count.EqualTo(1), "only an unsupported api version is retried");
            });
        }

        /// <summary>
        /// Runs the real Azure client against a stub HTTP handler instead of the network.
        /// </summary>
        private sealed class StubHttpAzureClient : AzureClient
        {
            public StubHttpMessageHandler Handler { get; } = new();

            public StubHttpAzureClient()
            {
                RestClientOptions options = new(kManagementUrl + "/") { ConfigureMessageHandler = _ => Handler };
                restClient = new RestClient(options);
            }
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            public sealed record RecordedRequest(HttpMethod Method, string Url, string? Authorization, string Body);

            public Queue<(HttpStatusCode, string)> Responses { get; } = new();
            public List<RecordedRequest> Requests { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(new RecordedRequest(request.Method, request.RequestUri?.AbsoluteUri ?? "", request.Headers.Authorization?.ToString(), body));
                (HttpStatusCode statusCode, string content) = Responses.Dequeue();
                return new HttpResponseMessage(statusCode) { Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json") };
            }
        }

        /// <summary>
        /// Serves prepared pages instead of calling Azure.
        /// </summary>
        private sealed class FakeAzureClient : AzureClient
        {
            public Dictionary<string, string> Pages { get; } = [];
            public List<string> UnsupportedApiVersionUrls { get; } = [];
            public List<string> RequestedUrls { get; } = [];
            public List<string> RequestedTokens { get; } = [];
            public List<string> Logins { get; } = [];

            public override Task<string> GetAccessToken(string tenantId, string clientId, string clientSecret)
            {
                Logins.Add($"{tenantId}|{clientId}|{clientSecret}");
                return Task.FromResult("token");
            }

            protected override Task<string> GetPage(string url, string accessToken)
            {
                RequestedUrls.Add(url);
                RequestedTokens.Add(accessToken);
                if (UnsupportedApiVersionUrls.Contains(url))
                {
                    throw new AzureApiVersionNotSupportedException(kUnsupportedApiVersionError);
                }
                return Task.FromResult(Pages[url]);
            }
        }

        private sealed class AzureDiscoveryTestApiConn : SimulatedApiConnection
        {
            public List<Management> ExistingManagements { get; set; } = [];
            public Credential? AdditionalCredential { get; set; }
            public List<string> CredentialQueryVars { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getManagementsDetails)
                {
                    return Task.FromResult((QueryResponseType)(object)ExistingManagements);
                }
                if (query == DeviceQueries.getCredentialById)
                {
                    CredentialQueryVars.Add(JsonSerializer.Serialize(variables));
                    return Task.FromResult((QueryResponseType)(object)AdditionalCredential!);
                }
                throw new NotImplementedException(query);
            }
        }
    }
}
