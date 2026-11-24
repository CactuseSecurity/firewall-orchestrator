using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Test.Mocks;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Text;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the member resolution of a recipient group through the middleware: resolved members are used
    /// as they come, and an empty or failed answer falls back to the owner groups known to the email helper.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class EmailHelperMiddlewareResolveTest
    {
        private const string kGroupDn = "cn=network-team,dc=test";
        private const string kAliceDn = "cn=alice,dc=test";
        private const string kBobDn = "cn=bob,dc=test";
        private const string kCarolDn = "cn=carol,dc=test";
        private const string kResolvedMembersJson = "[\"cn=alice,dc=test\", \"\", \"CN=Alice,DC=test\", \"cn=bob,dc=test\"]";
        private static readonly List<string> kAliceAndBob = ["alice@example.test", "bob@example.test"];
        private static readonly List<string> kCarol = ["carol@example.test"];

        private static readonly List<UiUser> kUsers =
        [
            new() { Dn = kAliceDn, Email = "alice@example.test" },
            new() { Dn = kBobDn, Email = "bob@example.test" },
            new() { Dn = kCarolDn, Email = "carol@example.test" }
        ];

        private static readonly List<UserGroup> kOwnerGroups =
        [
            new() { Dn = kGroupDn, Users = [new UiUser { Dn = kCarolDn }] }
        ];

        [Test]
        public async Task GetRecipientsUsesMembersResolvedByMiddleware()
        {
            RecordingResolveHandler handler = new(HttpStatusCode.OK, kResolvedMembersJson);
            ResolveContext context = await CreateContext(handler);

            List<string> recipients = await GetAssignedGroupRecipients(context.Helper);

            Assert.Multiple(() =>
            {
                Assert.That(recipients, Is.EqualTo(kAliceAndBob));
                Assert.That(handler.RequestPaths, Has.All.EndWith("/Group/Resolve"));
                Assert.That(handler.RequestBodies, Has.Some.Contains(kGroupDn));
                Assert.That(context.DisplayedErrors, Is.Empty);
            });
        }

        [Test]
        public async Task GetRecipientsFallsBackToOwnerGroupsWhenMiddlewareResolvesNoMember()
        {
            ResolveContext context = await CreateContext(new RecordingResolveHandler(HttpStatusCode.OK, "[]"));

            List<string> recipients = await GetAssignedGroupRecipients(context.Helper);

            Assert.Multiple(() =>
            {
                Assert.That(recipients, Is.EqualTo(kCarol));
                Assert.That(context.DisplayedErrors, Is.Empty);
            });
        }

        [Test]
        public async Task GetRecipientsReportsErrorAndFallsBackToOwnerGroupsWhenMiddlewareFails()
        {
            ResolveContext context = await CreateContext(new RecordingResolveHandler(HttpStatusCode.InternalServerError, "{}"));

            List<string> recipients = await GetAssignedGroupRecipients(context.Helper);

            Assert.Multiple(() =>
            {
                Assert.That(recipients, Is.EqualTo(kCarol));
                Assert.That(context.DisplayedErrors, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task GetRecipientsReportsErrorAndFallsBackToOwnerGroupsWhenMiddlewareIsUnreachable()
        {
            ResolveContext context = await CreateContext(new ThrowingHttpMessageHandler());

            List<string> recipients = await GetAssignedGroupRecipients(context.Helper);

            Assert.Multiple(() =>
            {
                Assert.That(recipients, Is.EqualTo(kCarol));
                Assert.That(context.DisplayedErrors, Has.Count.EqualTo(1));
            });
        }

        private static async Task<ResolveContext> CreateContext(HttpMessageHandler handler)
        {
            TestMiddlewareClient middlewareClient = new();
            middlewareClient.UseHandler(handler);
            List<string> displayedErrors = [];
            SimulatedUserConfig userConfig = new() { UseDummyEmailAddress = false };
            EmailHelper helper = new(new UserEmailsApiConnection(), middlewareClient, userConfig,
                (_, title, _, _) => displayedErrors.Add(title), kOwnerGroups.ToList(), useInMwServer: true);
            await helper.Init();
            return new ResolveContext(helper, displayedErrors);
        }

        private static Task<List<string>> GetAssignedGroupRecipients(EmailHelper helper)
        {
            WfStatefulObject statefulObject = new() { AssignedGroup = kGroupDn };
            return helper.GetRecipients(EmailRecipientOption.AssignedGroup, statefulObject, null, null, null);
        }

        private sealed record ResolveContext(EmailHelper Helper, List<string> DisplayedErrors);

        /// <summary>
        /// Answers every middleware request with a fixed response and records path and body of the requests.
        /// </summary>
        private sealed class RecordingResolveHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
        {
            public List<string> RequestPaths { get; } = [];
            public List<string> RequestBodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestPaths.Add(request.RequestUri?.AbsolutePath ?? "");
                RequestBodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }
        }

        /// <summary>
        /// Serves the queries of the email helper initialization: no responsible types and the user emails.
        /// </summary>
        private sealed class UserEmailsApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>());
                }
                if (typeof(QueryResponseType) == typeof(List<UiUser>))
                {
                    return Task.FromResult((QueryResponseType)(object)kUsers.Select(user => new UiUser { Dn = user.Dn, Email = user.Email }).ToList());
                }
                throw new NotSupportedException($"Unexpected query type {typeof(QueryResponseType).Name}.");
            }
        }
    }
}
