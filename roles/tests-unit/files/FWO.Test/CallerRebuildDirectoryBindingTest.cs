using System.Security.Claims;
using System.Security.Cryptography;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// The report and normalized config endpoints rebuild the caller from its token. The dn of the
    /// token may exist in several LDAP connections (SEC-11), so the rebuild has to start from the
    /// local user id of the token, which names the directory the caller belongs to.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class CallerRebuildDirectoryBindingTest
    {
        private const int kCallerDbId = 7;
        private const string kCallerDn = "uid=shared,ou=users,dc=fworch,dc=internal";
        private static readonly List<int> kCallerDbIdOnly = [kCallerDbId];

        [Test]
        public async Task ReportGet_RebuildsCallerFromItsLocalUserId()
        {
            StoredUserApiConnection apiConnection = new();
            ReportController controller = new(CreateJwtWriter(), new List<Ldap>(), apiConnection)
            {
                ControllerContext = CreateContext(CreateCaller())
            };

            await controller.Get(new ReportGetParameters());

            Assert.That(apiConnection.LookedUpUserIds, Is.EqualTo(kCallerDbIdOnly));
        }

        [Test]
        public async Task NormalizedConfigGet_RebuildsCallerFromItsLocalUserId()
        {
            StoredUserApiConnection apiConnection = new();
            NormalizedConfigController controller = new(CreateJwtWriter(), new List<Ldap>(), apiConnection)
            {
                ControllerContext = CreateContext(CreateCaller())
            };

            await controller.Get(new NormalizedConfigGetParameters());

            Assert.That(apiConnection.LookedUpUserIds, Is.EqualTo(kCallerDbIdOnly));
        }

        [Test]
        public async Task ReportGet_WithoutLocalUserIdClaim_DoesNotLookUpAStoredUser()
        {
            StoredUserApiConnection apiConnection = new();
            List<Claim> claimsWithoutUserId =
            [
                new("unique_name", "shared"),
                new("x-hasura-uuid", kCallerDn)
            ];
            ReportController controller = new(CreateJwtWriter(), new List<Ldap>(), apiConnection)
            {
                ControllerContext = CreateContext(new ClaimsPrincipal(new ClaimsIdentity(claimsWithoutUserId, "test")))
            };

            await controller.Get(new ReportGetParameters());

            Assert.That(apiConnection.LookedUpUserIds, Is.Empty);
        }

        private static ClaimsPrincipal CreateCaller()
        {
            List<Claim> claims =
            [
                new("unique_name", "shared"),
                new("x-hasura-uuid", kCallerDn),
                new("x-hasura-user-id", kCallerDbId.ToString())
            ];
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private static ControllerContext CreateContext(ClaimsPrincipal caller)
        {
            return new ControllerContext { HttpContext = new DefaultHttpContext { User = caller } };
        }

        private static JwtWriter CreateJwtWriter()
        {
            return new JwtWriter(new RsaSecurityKey(RSA.Create(2048)));
        }

        /// <summary>
        /// Holds the caller's local user in a directory that is not connected, so the rebuild stops
        /// after the lookup, and records which local users were looked up.
        /// </summary>
        private sealed class StoredUserApiConnection : SimulatedApiConnection
        {
            public List<int> LookedUpUserIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getUserByDbId)
                {
                    LookedUpUserIds.Add((int)variables!.GetType().GetProperty("userId")!.GetValue(variables)!);
                    UiUser[] storedUsers = [new() { DbId = kCallerDbId, Dn = kCallerDn, LdapConnection = new() { Id = 12 } }];
                    return Task.FromResult((QueryResponseType)(object)storedUsers);
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
