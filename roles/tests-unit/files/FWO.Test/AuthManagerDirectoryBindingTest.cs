using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.IdentityModel.Tokens;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A dn is unique only inside its directory: the same dn in two LDAP connections names two
    /// different subjects (SEC-11). A user that is already known locally - a token refresh, a
    /// scheduled report, a request rebuilt from the caller's token - therefore has to be
    /// authenticated in the directory it belongs to, and must come back as the same local user.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class AuthManagerDirectoryBindingTest
    {
        private const string kSharedDn = "uid=shared,ou=users,dc=fworch,dc=internal";
        private const string kRoleSearchPath = "ou=roles,dc=fworch,dc=internal";
        private const int kFirstLdapId = 11;
        private const int kSecondLdapId = 12;
        private const int kStoredUserId = 7;
        private static readonly string[] kUserCn = ["shared"];
        private static readonly string[] kReporterRoleValues = [Roles.Reporter];
        private static readonly string[] kSharedDnValues = [kSharedDn];
        private static readonly List<long> kSecondLdapIdOnly = [kSecondLdapId];
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");

        [Test]
        public async Task AuthenticateAndBuildUser_WithStoredDirectory_SearchesOnlyThatDirectory()
        {
            RecordingLdapClient firstDirectory = CreateDirectoryHoldingSharedDn();
            RecordingLdapClient secondDirectory = CreateDirectoryHoldingSharedDn();
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = kStoredUserId };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, firstDirectory), CreateLdap(kSecondLdapId, secondDirectory));
            UiUser storedUser = new() { DbId = kStoredUserId, Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kSecondLdapId } };

            UiUser? result = await AuthenticateAndBuildUser(authManager, storedUser);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(result?.LdapConnection.Id, Is.EqualTo(kSecondLdapId));
                Assert.That(firstDirectory.ReadCalls, Is.Empty, "the other directory holds the same dn for a different subject");
                Assert.That(secondDirectory.ReadCalls, Does.Contain(kSharedDn));
                Assert.That(apiConnection.LookedUpLdapIds, Is.EqualTo(kSecondLdapIdOnly));
            });
        }

        [Test]
        public async Task AuthenticateAndBuildUser_WithLocalUserIdOnly_BindsToTheDirectoryStoredWithIt()
        {
            RecordingLdapClient firstDirectory = CreateDirectoryHoldingSharedDn();
            RecordingLdapClient secondDirectory = CreateDirectoryHoldingSharedDn();
            DirectoryApiConnection apiConnection = new()
            {
                ResolvedUserId = kStoredUserId,
                StoredUser = new() { DbId = kStoredUserId, Dn = kSharedDn, LdapConnection = new() { Id = kSecondLdapId } }
            };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, firstDirectory), CreateLdap(kSecondLdapId, secondDirectory));
            UiUser callerFromToken = new() { DbId = kStoredUserId, Name = "shared", Dn = kSharedDn };

            UiUser? result = await AuthenticateAndBuildUser(authManager, callerFromToken);

            Assert.Multiple(() =>
            {
                Assert.That(result?.LdapConnection.Id, Is.EqualTo(kSecondLdapId));
                Assert.That(firstDirectory.ReadCalls, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getUserByDbId));
            });
        }

        [Test]
        public void AuthenticateAndBuildUser_WhenDirectoryResolvesToAnotherLocalUser_Rejects()
        {
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = 99 };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, CreateDirectoryHoldingSharedDn()));
            UiUser storedUser = new() { DbId = kStoredUserId, Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kFirstLdapId } };

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () => await AuthenticateAndBuildUser(authManager, storedUser))!;

            Assert.That(exception.Message, Does.StartWith("A0003"));
        }

        [Test]
        public void AuthenticateAndBuildUser_WhenStoredDirectoryIsNotConnected_DoesNotFallBackToAnotherDirectory()
        {
            RecordingLdapClient otherDirectory = CreateDirectoryHoldingSharedDn();
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = kStoredUserId };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, otherDirectory));
            UiUser storedUser = new() { DbId = kStoredUserId, Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kSecondLdapId } };

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () => await AuthenticateAndBuildUser(authManager, storedUser))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("A0002"));
                Assert.That(otherDirectory.ReadCalls, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.getUserByDn));
            });
        }

        [Test]
        public void AuthenticateAndBuildUser_WhenLocalUserIdIsUnknown_Rejects()
        {
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = kStoredUserId };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, CreateDirectoryHoldingSharedDn()));
            UiUser callerFromToken = new() { DbId = kStoredUserId, Name = "shared", Dn = kSharedDn };

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () => await AuthenticateAndBuildUser(authManager, callerFromToken))!;

            Assert.That(exception.Message, Does.StartWith("A0004"));
        }

        [Test]
        public async Task AuthenticateAndBuildUser_WithoutLocalUser_SelectsDirectoryAndLooksUpLocalUserThere()
        {
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = 5 };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kSecondLdapId, CreateDirectoryHoldingSharedDn()));
            UiUser newLogin = new() { Name = "shared", Dn = kSharedDn };

            UiUser? result = await AuthenticateAndBuildUser(authManager, newLogin);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(5));
                Assert.That(apiConnection.LookedUpLdapIds, Is.EqualTo(kSecondLdapIdOnly));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.getUserByDbId));
            });
        }

        [Test]
        public async Task DelegatedTarget_WithSelectedDirectory_UsesSecondAccount()
        {
            RecordingLdapClient firstDirectory = CreateDirectoryHoldingSharedDn();
            RecordingLdapClient secondDirectory = CreateDirectoryHoldingSharedDn();
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = kStoredUserId };
            object authManager = CreateAuthManager(apiConnection, CreateLdap(kFirstLdapId, firstDirectory), CreateLdap(kSecondLdapId, secondDirectory));
            AuthenticationTokenGetForUserParameters parameters = new()
            {
                TargetUserDn = kSharedDn,
                Options = new() { TargetLdapId = kSecondLdapId }
            };

            UiUser? result = await AuthenticateAndBuildUser(authManager, AuthDirectoryBinding.BuildDelegatedTargetUser(parameters));

            Assert.Multiple(() =>
            {
                Assert.That(result?.LdapConnection.Id, Is.EqualTo(kSecondLdapId));
                Assert.That(firstDirectory.ReadCalls, Is.Empty);
                Assert.That(secondDirectory.ReadCalls, Does.Contain(kSharedDn));
                Assert.That(apiConnection.LookedUpLdapIds, Is.EqualTo(kSecondLdapIdOnly));
            });
        }

        [Test]
        public void DelegatedTarget_WithoutDirectory_RejectsAmbiguousAccount()
        {
            DirectoryApiConnection apiConnection = new() { ResolvedUserId = kStoredUserId };
            object authManager = CreateAuthManager(apiConnection,
                CreateLdap(kFirstLdapId, CreateDirectoryHoldingSharedDn()),
                CreateLdap(kSecondLdapId, CreateDirectoryHoldingSharedDn()));
            AuthenticationTokenGetForUserParameters parameters = new() { TargetUserDn = kSharedDn };

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () =>
                await AuthenticateAndBuildUser(authManager, AuthDirectoryBinding.BuildDelegatedTargetUser(parameters)))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("A0005"));
                Assert.That(apiConnection.Queries, Is.Empty, "an ambiguous target must not update either local user");
            });
        }

        [Test]
        public void DelegatedTarget_WithInvalidDirectoryId_RejectsBeforeAuthentication()
        {
            AuthenticationTokenGetForUserParameters parameters = new()
            {
                TargetUserDn = kSharedDn,
                Options = new() { TargetLdapId = 0 }
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => AuthDirectoryBinding.BuildDelegatedTargetUser(parameters))!;

            Assert.That(exception.Message, Does.Contain("options.targetLdapId"));
        }

        [Test]
        public void DelegatedTarget_WithNullOptions_RejectsBeforeAuthentication()
        {
            AuthenticationTokenGetForUserParameters parameters = new()
            {
                TargetUserDn = kSharedDn,
                Options = null
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() => AuthDirectoryBinding.BuildDelegatedTargetUser(parameters))!;

            Assert.That(exception.Message, Does.Contain("options must be an object"));
        }

        private static async Task<UiUser?> AuthenticateAndBuildUser(object authManager, UiUser user)
        {
            MethodInfo method = authManager.GetType().GetMethod("AuthenticateAndBuildUserAsync")
                ?? throw new MissingMethodException(authManager.GetType().FullName, "AuthenticateAndBuildUserAsync");
            object?[] arguments = [user, false, false, CancellationToken.None];
            return await (Task<UiUser?>)method.Invoke(authManager, arguments)!;
        }

        private static object CreateAuthManager(ApiConnection apiConnection, params Ldap[] ldaps)
        {
            Type authManagerType = typeof(AuthenticationTokenController).Assembly.GetType("FWO.Middleware.Server.Controllers.AuthManager", throwOnError: true)!;
            return Activator.CreateInstance(
                authManagerType,
                new JwtWriter(new RsaSecurityKey(RSA.Create(2048))),
                ldaps.ToList(),
                apiConnection,
                null)!;
        }

        private static TestableLdap CreateLdap(int id, RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = id,
                Address = $"ldap{id}.example.test",
                Port = 389,
                SearchUser = "cn=search,dc=fworch,dc=internal",
                SearchUserPwd = kSearchPassword,
                RoleSearchPath = kRoleSearchPath,
                UserSearchPath = "ou=users,dc=fworch,dc=internal",
                TenantId = 1
            };
        }

        /// <summary>
        /// A directory that holds the shared dn, as every directory in these tests does.
        /// </summary>
        private static RecordingLdapClient CreateDirectoryHoldingSharedDn()
        {
            RecordingLdapClient client = new()
            {
                SearchResponder = (baseDn, scope, filter, attributes, typesOnly) => baseDn == kRoleSearchPath
                    ? LdapTestSupport.CreateSearchResults(
                        LdapTestSupport.CreateEntry(
                            "cn=reporter,ou=roles,dc=fworch,dc=internal",
                            new LdapAttribute("cn", kReporterRoleValues),
                            new LdapAttribute("uniqueMember", kSharedDnValues)))
                    : LdapTestSupport.CreateSearchResults()
            };
            client.ReadResultsByDn[kSharedDn] = LdapTestSupport.CreateEntry(kSharedDn, new LdapAttribute("cn", kUserCn));
            return client;
        }

        /// <summary>
        /// Answers the local user lookups of the authentication and records which directory the
        /// local user was looked up in.
        /// </summary>
        private sealed class DirectoryApiConnection : SimulatedApiConnection
        {
            public int ResolvedUserId { get; set; }
            public UiUser? StoredUser { get; set; }
            public List<string> Queries { get; } = [];
            public List<long> LookedUpLdapIds { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                if (query == AuthQueries.getUserByDn)
                {
                    LookedUpLdapIds.Add(Convert.ToInt64(variables?.GetType().GetProperty("ldapConnectionId")?.GetValue(variables)));
                    UiUser[] resolvedUsers = [new() { DbId = ResolvedUserId, Dn = kSharedDn }];
                    return Answer<QueryResponseType>(resolvedUsers);
                }
                if (query == AuthQueries.getUserByDbId)
                {
                    UiUser[] storedUsers = StoredUser == null ? [] : [StoredUser];
                    return Answer<QueryResponseType>(storedUsers);
                }
                return Answer<QueryResponseType>(EmptyResult(typeof(QueryResponseType)));
            }

            private static Task<QueryResponseType> Answer<QueryResponseType>(object? result)
            {
                return Task.FromResult((QueryResponseType)result!);
            }

            private static object? EmptyResult(Type resultType)
            {
                if (resultType.IsArray)
                {
                    return Array.CreateInstance(resultType.GetElementType()!, 0);
                }
                if (resultType == typeof(List<FwoOwner>))
                {
                    return new List<FwoOwner>();
                }
                if (resultType == typeof(List<WorkflowVisibilityGroup>))
                {
                    return new List<WorkflowVisibilityGroup>();
                }
                return null;
            }
        }
    }
}
