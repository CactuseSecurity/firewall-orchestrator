using FWO.Data;
using FWO.Encryption;
using FWO.ExternalSystems;
using FWO.Middleware.Server;
using NUnit.Framework;
using System.Net;

namespace FWO.Test
{
    /// <summary>
    /// Tests that the external request sender builds the Authorization header from the current credential of the
    /// ticket system right before each call, and that a header which cannot be built counts as a failed attempt
    /// (GHSA-jhj7-f9vj-r9v8).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class ExtRequestSenderCredentialTest
    {
        private const string kMainKey = "0123456789ABCDEF0123456789ABCDEF";
        private const int kCredentialId = 7;
        private const int kOldCredentialId = 5;
        private const int kMaxAttempts = 3;
        private const int kCyclesBetweenAttempts = 5;
        private const string kUsername = "probe_user";
        private const string kRotatedPassword = "rotated:pw";
        private const string kExtTicketId = "4711";
        private const string kTicketContent = "{ \"ticketText\": \"\" }";
        private static readonly Dictionary<string, (HttpStatusCode StatusCode, string Content)> NoResponses = [];
        private static readonly List<string> ExpectedHeaders = [ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)];

        [Test]
        public async Task SendUsesCurrentPasswordOfCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(new()
            {
                ["tickets.json"] = (HttpStatusCode.Created, "{\"ticket\": {\"id\": 11, \"status\": \"In Progress\" } }")
            });
            ExtRequestSenderTestApiConn apiConnection = new()
            {
                Credential = CreateCredential(),
                OpenRequests = [CreateRequest(server.BaseUrl, ExtStates.ExtReqInitialized)]
            };

            await RunSender(apiConnection, server.BaseUrl);

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests.Select(request => request.Path), Is.EqualTo(new List<string> { "tickets.json" }));
                Assert.That(server.Requests.Select(request => request.Authorization), Is.All.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)));
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.UpdateExtRequestCreation, Has.Count.EqualTo(1));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain("extTicketId = 11"));
            });
        }

        [Test]
        public async Task PollUsesCurrentPasswordOfCredential()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(new()
            {
                [$"tickets/{kExtTicketId}"] = (HttpStatusCode.OK, "{\"ticket\": {\"id\": 4711, \"status\": \"In Progress\" } }")
            });
            ExternalRequest request = CreateRequest(server.BaseUrl, ExtStates.ExtReqInProgress);
            request.ExtTicketId = kExtTicketId;
            ExtRequestSenderTestApiConn apiConnection = new() { Credential = CreateCredential(), OpenRequests = [request] };

            await RunSender(apiConnection, server.BaseUrl);

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests.Select(request => request.Path), Is.EqualTo(new List<string> { $"tickets/{kExtTicketId}" }));
                Assert.That(server.Requests.Select(request => request.Authorization), Is.All.EqualTo(ExternalTicketAuthorization.BuildBasicHeader(kUsername, kRotatedPassword)));
            });
        }

        [Test]
        public async Task SendUsesCredentialNowConfiguredForTicketSystem()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(new()
            {
                ["tickets.json"] = (HttpStatusCode.Created, "{\"ticket\": {\"id\": 11, \"status\": \"In Progress\" } }")
            });
            // the request was created while the ticket system still used another credential, which may be deleted by now
            ExtRequestSenderTestApiConn apiConnection = new()
            {
                Credential = CreateCredential(),
                OpenRequests = [CreateRequest(server.BaseUrl, ExtStates.ExtReqInitialized, credentialId: kOldCredentialId)]
            };

            await RunSender(apiConnection, server.BaseUrl);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain($"id = {kCredentialId}"));
                Assert.That(server.Requests.Select(request => request.Authorization), Is.EqualTo(ExpectedHeaders));
            });
        }

        [Test]
        public async Task PollFallsBackToStoredCredentialOfUnconfiguredTicketSystem()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(new()
            {
                [$"tickets/{kExtTicketId}"] = (HttpStatusCode.OK, "{\"ticket\": {\"id\": 4711, \"status\": \"In Progress\" } }")
            });
            ExternalRequest request = CreateRequest(server.BaseUrl, ExtStates.ExtReqInProgress);
            request.ExtTicketId = kExtTicketId;
            ExtRequestSenderTestApiConn apiConnection = new() { Credential = CreateCredential(), OpenRequests = [request] };

            await RunSender(apiConnection, server.BaseUrl, ticketSystemConfigured: false);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.CredentialQueryVars, Has.Count.EqualTo(1));
                Assert.That(apiConnection.CredentialQueryVars[0], Does.Contain($"id = {kCredentialId}"));
                Assert.That(server.Requests.Select(request => request.Authorization), Is.EqualTo(ExpectedHeaders));
            });
        }

        [Test]
        public async Task MissingCredentialCountsAsFailedAttempt()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(NoResponses);
            ExtRequestSenderTestApiConn apiConnection = new() { OpenRequests = [CreateRequest(server.BaseUrl, ExtStates.ExtReqInitialized)] };

            List<string> failedRequests = await RunSender(apiConnection, server.BaseUrl);

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests, Is.Empty);
                Assert.That(failedRequests, Is.Empty);
                Assert.That(apiConnection.UpdateExtRequestCreation, Has.Count.EqualTo(1));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain($"extRequestState = {ExtStates.ExtReqFailed}"));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain("attempts = 1"));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain($"waitCycles = {kCyclesBetweenAttempts}"));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain($"Credential {kCredentialId}"));
            });
        }

        [Test]
        public async Task UndecryptableLegacyHeaderIsRejectedAfterMaxAttempts()
        {
            using TestMainKeyScope mainKeyScope = new(kMainKey);
            using AuthorizationCaptureServer server = new(NoResponses);
            ExternalRequest request = CreateRequest(server.BaseUrl, ExtStates.ExtReqFailed, credentialId: null, legacyAuthorization: "Basic xyz");
            request.Attempts = kMaxAttempts - 1;
            ExtRequestSenderTestApiConn apiConnection = new() { OpenRequests = [request] };

            await RunSender(apiConnection, server.BaseUrl, configuredCredentialId: null);

            Assert.Multiple(() =>
            {
                Assert.That(server.Requests, Is.Empty);
                Assert.That(apiConnection.CredentialQueryVars, Is.Empty);
                Assert.That(apiConnection.UpdateExtRequestCreation, Has.Count.EqualTo(1));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain($"extRequestState = {ExtStates.ExtReqRejected}"));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Contain($"attempts = {kMaxAttempts}"));
                Assert.That(apiConnection.UpdateExtRequestCreation[0], Does.Not.Contain("Basic xyz"));
            });
        }

        private static Credential CreateCredential()
        {
            return new Credential { Id = kCredentialId, Name = "probe", ImportUser = kUsername, Secret = AesEnc.Encrypt(kRotatedPassword, kMainKey) };
        }

        private static ExternalTicketSystem CreateTicketSystem(string url, int? credentialId = kCredentialId, string? legacyAuthorization = null)
        {
            return new ExternalTicketSystem
            {
                Id = 1,
                TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId,
                Name = "Tufin",
                Url = url,
                CredentialId = credentialId,
                Authorization = legacyAuthorization,
                MaxAttempts = kMaxAttempts,
                CyclesBetweenAttempts = kCyclesBetweenAttempts
            };
        }

        private static ExternalRequest CreateRequest(string url, ExtStates state, int? credentialId = kCredentialId, string? legacyAuthorization = null)
        {
            return new ExternalRequest
            {
                Id = 2,
                TicketId = 2,
                ExtTicketSystem = System.Text.Json.JsonSerializer.Serialize(CreateTicketSystem(url, credentialId, legacyAuthorization)),
                ExtRequestState = state.ToString(),
                ExtRequestContent = kTicketContent
            };
        }

        private static async Task<List<string>> RunSender(ExtRequestSenderTestApiConn apiConnection, string url, int? configuredCredentialId = kCredentialId,
            bool ticketSystemConfigured = true)
        {
            List<ExternalTicketSystem> configuredSystems = ticketSystemConfigured ? [CreateTicketSystem(url, configuredCredentialId)] : [];
            SimulatedGlobalConfig globalConfig = new()
            {
                ExternalRequestWaitCycles = 3,
                ExtTicketSystems = System.Text.Json.JsonSerializer.Serialize(configuredSystems)
            };
            ExternalRequestSender sender = new(apiConnection, globalConfig);
            return await sender.Run();
        }
    }
}
