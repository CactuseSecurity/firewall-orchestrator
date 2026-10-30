using FWO.Data;
using FWO.Data.Workflow;
using FWO.Encryption;
using FWO.ExternalSystems;
using FWO.Middleware.Server;
using System.Net;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests that external requests only reference the credential of their ticket system and never store an
    /// Authorization header string, which is built right before calls such as the requester lookup (GHSA-jhj7-f9vj-r9v8).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExtTicketHandlerCredentialTest
    {
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";
        private const int kCredentialId = 7;
        private const string kUsername = "probe_user";
        private const string kPassword = "pw:with:colon";
        private const string kLegacyHeader = "Basic cHJvYmVfdXNlcjpwYXNz";
        private const string kTufinUrl = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/";

        [Test]
        public async Task CreatedRequestStoresOnlyCredentialReference()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential() };

            await CreateNextRequest(apiConnection, kCredentialId, legacyAuthorization: kLegacyHeader);

            ExternalTicketSystem storedSystem = System.Text.Json.JsonSerializer.Deserialize<ExternalTicketSystem>(apiConnection.AddedExtTicketSystem ?? "")
                ?? throw new AssertionException("No external request created");
            Assert.Multiple(() =>
            {
                Assert.That(storedSystem.CredentialId, Is.EqualTo(kCredentialId));
                Assert.That(apiConnection.AddedExtTicketSystem, Does.Not.Contain("Authorization"));
                Assert.That(apiConnection.AddedExtTicketSystem, Does.Not.Contain(kLegacyHeader["Basic ".Length..]));
                // without requester lookup the ticket system is not called before sending, so the credential is not read yet
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
            });
        }

        [Test]
        public async Task CreatedRequestWithoutCredentialHasNoAuthorization()
        {
            ExtTicketHandlerTestApiConn apiConnection = new();

            await CreateNextRequest(apiConnection, null, legacyAuthorization: kLegacyHeader);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.AddedExtTicketSystem, Is.Not.Null);
                Assert.That(apiConnection.AddedExtTicketSystem, Does.Not.Contain("Authorization"));
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
            });
        }

        [Test]
        public async Task RequesterLookupSendsHeaderOfCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(new()
            {
                ["users.json"] = (HttpStatusCode.OK, "{\"users\":{\"user\":[{\"id\":42}]}}")
            });
            ExtTicketHandlerTestApiConn apiConnection = new() { Credential = CreateCredential(), TicketRequester = new UiUser { Name = "requester" } };

            await CreateNextRequest(apiConnection, kCredentialId, url: server.BaseUrl, lookupRequesterId: true);

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests.Select(request => request.Path), Does.Contain("users.json"));
                Assert.That(server.Requests.Select(request => request.Authorization), Is.All.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kPassword)));
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.AddedExtTicketSystem, Does.Not.Contain("Authorization"));
                Assert.That(apiConnection.AddedExtTicketSystem, Does.Not.Contain(kPassword));
            });
        }

        [Test]
        public void RequesterLookupFailsForUnknownCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            ExtTicketHandlerTestApiConn apiConnection = new();

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => CreateNextRequest(apiConnection, kCredentialId, lookupRequesterId: true));

            Assert.Multiple(() =>
            {
                Assert.That(exception?.Message, Does.Contain($"Credential {kCredentialId}"));
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.AddedExtTicketSystem, Is.Null);
            });
        }

        private static Credential CreateCredential()
        {
            return new Credential { Id = kCredentialId, Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kPassword, kMainKey) };
        }

        private static async Task CreateNextRequest(ExtTicketHandlerTestApiConn apiConnection, int? credentialId, string? legacyAuthorization = null,
            string url = kTufinUrl, bool lookupRequesterId = false)
        {
            ExternalTicketSystem tufinSystem = new()
            {
                Id = 1,
                TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                CredentialId = credentialId,
                Authorization = legacyAuthorization,
                LookupRequesterId = lookupRequesterId,
                Name = "Tufin",
                Url = url,
                Templates = ExtTicketHandlerTest.ticketSystem.Templates
            };
            SimulatedUserConfig userConfig = new()
            {
                ExternalRequestWaitCycles = 3,
                ExtTicketSystems = System.Text.Json.JsonSerializer.Serialize(new List<ExternalTicketSystem> { tufinSystem }),
                FwConfigChangeMgmSettings = System.Text.Json.JsonSerializer.Serialize(new List<ManagementFwConfigChangeState>
                {
                    new()
                    {
                        Id = 1,
                        Name = "Checkpoint",
                        Enabled = true,
                        SelectedChanges = new()
                        {
                            [ManagementFwConfigChangeCategories.ObjectChanges] = tufinSystem.Id.ToString(),
                            [ManagementFwConfigChangeCategories.RuleChanges] = tufinSystem.Id.ToString()
                        }
                    }
                }),
                ReqPriorities = ExtTicketHandlerTest.reqPrios,
                ModNamingConvention = ExtTicketHandlerTest.namingConvention,
                ModRolloutBundleTasks = true
            };

            using ExternalRequestHandler handler = new(userConfig, apiConnection, null) { SCTemplateSource = new FixedSCTemplateSource(ExtTicketHandlerTest.ticketSystem.Templates) };
            await handler.HandleStateChange(new ExternalRequest
            {
                Id = 2,
                TicketId = 123,
                TaskNumber = 2,
                ExtRequestState = ExtStates.ExtReqDone.ToString(),
                ExtTicketId = "4712",
                ExtQueryVariables = "{\"BundledTasks\":[2,3]}"
            });
        }
    }
}
