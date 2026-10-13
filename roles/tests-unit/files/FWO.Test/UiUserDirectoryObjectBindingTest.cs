using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Novell.Directory.Ldap;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// A dn can be handed to another person. The local user is therefore bound to the immutable object id of its
    /// directory entry, and a different entry under the same dn neither takes over the local user nor continues its
    /// sessions (GHSA-695m-f6mv-9g72).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class UiUserDirectoryObjectBindingTest
    {
        private const int kLdapId = 4;
        private const int kStoredUserId = 17;
        private const string kDn = "uid=x,ou=users,dc=example,dc=com";
        private const string kRenamedDn = "uid=x-renamed,ou=users,dc=example,dc=com";
        private const string kFirstObject = "entryUUID:11111111-1111-1111-1111-111111111111";
        private const string kSecondObject = "entryUUID:22222222-2222-2222-2222-222222222222";
        private const string kSecondEntryUuid = "22222222-2222-2222-2222-222222222222";
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");
        private static readonly string[] kUserCn = ["x"];

        [Test]
        public async Task ResolveLocalUser_ReturnsNullForAnUnknownUser()
        {
            DirectoryObjectApiConnection apiConnection = new();

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, kFirstObject), kLdapId, true);

            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task ResolveLocalUser_BindsAnUnboundLocalUserAtAPasswordLogin()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, null));

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, kFirstObject), kLdapId, true);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(apiConnection.StoredUsers[0].DirectoryObjectId, Is.EqualTo(kFirstObject));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.bindUiUserDirectoryObject));
            });
        }

        [Test]
        public async Task ResolveLocalUser_DoesNotBindWithoutPassword()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, null));

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, kFirstObject), kLdapId, false);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(apiConnection.StoredUsers[0].DirectoryObjectId, Is.Null);
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.bindUiUserDirectoryObject));
            });
        }

        [Test]
        public async Task ResolveLocalUser_AcceptsTheBoundDirectoryObject()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, kFirstObject), kLdapId, false);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.bindUiUserDirectoryObject));
                Assert.That(apiConnection.RevokedUserIds, Is.Empty);
            });
        }

        [Test]
        public async Task ResolveLocalUser_KeepsTheDnBindingForDirectoriesWithoutObjectIds()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, null));

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, null), kLdapId, true);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.getUserByDirectoryObjectId));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.bindUiUserDirectoryObject));
            });
        }

        [TestCase(kSecondObject)]
        [TestCase(null)]
        public void ResolveLocalUser_RefusesAndEndsSessions_WhenTheDnNamesAnotherDirectoryObject(string? currentObject)
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () =>
                await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kDn, currentObject), kLdapId, true))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("A0008"));
                Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<int> { kStoredUserId }));
                Assert.That(apiConnection.StoredUsers[0].DirectoryObjectId, Is.EqualTo(kFirstObject));
            });
        }

        [Test]
        public async Task ResolveLocalUser_FollowsARenamedDirectoryEntry()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));

            UiUser? result = await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kRenamedDn, kFirstObject), kLdapId, false);

            Assert.Multiple(() =>
            {
                Assert.That(result?.DbId, Is.EqualTo(kStoredUserId));
                Assert.That(result?.Dn, Is.EqualTo(kRenamedDn));
                Assert.That(apiConnection.StoredUsers[0].Dn, Is.EqualTo(kRenamedDn));
            });
        }

        [Test]
        public void ResolveLocalUser_RefusesARenameOntoTheDnOfAnotherLocalUser()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId + 1, kRenamedDn, null));

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () =>
                await UiUserDirectoryObjectBinding.ResolveLocalUser(apiConnection, LoginOf(kRenamedDn, kFirstObject), kLdapId, true))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("A0008"));
                Assert.That(apiConnection.StoredUsers[0].Dn, Is.EqualTo(kDn));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.bindUiUserDirectoryObject));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_StoresTheObjectIdOfANewLocalUser()
        {
            DirectoryObjectApiConnection apiConnection = new();

            UiUser result = await UiUserHandler.SynchronizeUiUserContext(apiConnection, LoginOf(kDn, kFirstObject), bindDirectoryObject: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.DbId, Is.EqualTo(DirectoryObjectApiConnection.kNewUserId));
                Assert.That(apiConnection.UpsertedObjectIds, Is.EqualTo(new List<string?> { kFirstObject }));
            });
        }

        [Test]
        public void SynchronizeUiUserContext_PassesTheRefusalOn()
        {
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));

            Assert.ThrowsAsync<AuthenticationException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, LoginOf(kDn, kSecondObject), bindDirectoryObject: true));
            Assert.That(apiConnection.UpsertedObjectIds, Is.Empty, "a refused login must not create another local user");
        }

        [Test]
        public void AuthenticateAndBuildUser_DoesNotHandTheLocalUserOfADeletedAccountToTheNextHolderOfItsDn()
        {
            // A logged in with dn uid=x and object G1; A was deleted and B was created with the same dn and object G2
            DirectoryObjectApiConnection apiConnection = new();
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));
            object authManager = CreateAuthManager(apiConnection, CreateLdap(CreateDirectoryWithSecondHolder()));

            AuthenticationException exception = Assert.ThrowsAsync<AuthenticationException>(async () =>
                await AuthenticateAndBuildUser(authManager, new UiUser { Name = "x", Dn = kDn }))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.StartWith("A0008"));
                Assert.That(apiConnection.UpsertedObjectIds, Is.Empty);
                Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<int> { kStoredUserId }));
            });
        }

        [Test]
        public async Task RefreshToken_EndsTheSessionOfADeletedAccountOnceItsDnIsReused()
        {
            DirectoryObjectApiConnection apiConnection = new() { RefreshTokenUserId = kStoredUserId };
            apiConnection.StoredUsers.Add(StoredUser(kStoredUserId, kDn, kFirstObject));
            List<Ldap> ldaps = [CreateLdap(CreateDirectoryWithSecondHolder())];
            AuthenticationTokenController controller = new(new JwtWriter(new RsaSecurityKey(RSA.Create(2048))),
                ldaps, apiConnection, new TokenLifetimeProvider(), new LoginThrottle(new LoginThrottleSettings()));

            ActionResult<TokenPair> result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "refresh-token-of-a" });

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.InstanceOf<UnauthorizedObjectResult>());
                Assert.That(((UnauthorizedObjectResult)result.Result!).Value, Is.EqualTo(UiUserDirectoryObjectBinding.kDirectoryObjectChanged));
                Assert.That(apiConnection.RevokedUserIds, Is.EqualTo(new List<int> { kStoredUserId }));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.storeRefreshToken), "no new token pair may be issued");
            });
        }

        private static UiUser LoginOf(string dn, string? directoryObjectId)
        {
            return new UiUser { Name = "x", Dn = dn, DirectoryObjectId = directoryObjectId, LdapConnection = new() { Id = kLdapId } };
        }

        private static UiUser StoredUser(int id, string dn, string? directoryObjectId)
        {
            return new UiUser { DbId = id, Name = "x", Dn = dn, DirectoryObjectId = directoryObjectId, LdapConnection = new() { Id = kLdapId } };
        }

        private static RecordingLdapClient CreateDirectoryWithSecondHolder()
        {
            RecordingLdapClient client = new();
            client.ReadResultsByDn[kDn] = LdapTestSupport.CreateEntry(kDn, new LdapAttribute("cn", kUserCn), new LdapAttribute("entryUUID", kSecondEntryUuid));
            return client;
        }

        private static TestableLdap CreateLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = kLdapId,
                Addresses = ["ldap.example.com"],
                Port = 389,
                SearchUser = "cn=search,dc=example,dc=com",
                SearchUserPwd = kSearchPassword,
                UserSearchPath = "ou=users,dc=example,dc=com",
                TenantId = 1
            };
        }

        private static object CreateAuthManager(ApiConnection apiConnection, params Ldap[] ldaps)
        {
            Type authManagerType = typeof(AuthenticationTokenController).Assembly.GetType("FWO.Middleware.Server.Controllers.AuthManager", throwOnError: true)!;
            return Activator.CreateInstance(authManagerType, new JwtWriter(new RsaSecurityKey(RSA.Create(2048))), ldaps.ToList(), apiConnection, null)!;
        }

        private static async Task<UiUser?> AuthenticateAndBuildUser(object authManager, UiUser user)
        {
            MethodInfo method = authManager.GetType().GetMethod("AuthenticateAndBuildUserAsync")
                ?? throw new MissingMethodException(authManager.GetType().FullName, "AuthenticateAndBuildUserAsync");
            object?[] arguments = [user, false, true, CancellationToken.None];
            return await (Task<UiUser?>)method.Invoke(authManager, arguments)!;
        }

        /// <summary>
        /// Keeps local users and refresh tokens in memory and answers the queries of login, refresh and binding.
        /// </summary>
        private sealed class DirectoryObjectApiConnection : SimulatedApiConnection
        {
            public const int kNewUserId = 99;
            public List<UiUser> StoredUsers { get; } = [];
            public List<string> Queries { get; } = [];
            public List<int> RevokedUserIds { get; } = [];
            public List<string?> UpsertedObjectIds { get; } = [];
            public int RefreshTokenUserId { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                return Task.FromResult((QueryResponseType)Answer(query, variables, typeof(QueryResponseType))!);
            }

            private object? Answer(string query, object? variables, Type resultType)
            {
                if (query == AuthQueries.getUserByDirectoryObjectId)
                {
                    return StoredUsers.Where(user => user.LdapConnection.Id == Variable<long>(variables, "ldapConnectionId")
                        && user.DirectoryObjectId == Variable<string>(variables, "directoryObjectId")).Select(Copy).ToArray();
                }
                if (query == AuthQueries.getUserByDn)
                {
                    return StoredUsers.Where(user => user.LdapConnection.Id == Variable<int>(variables, "ldapConnectionId")
                        && DistName.DnEquals(user.Dn, Variable<string>(variables, "dn"))).Select(Copy).ToArray();
                }
                if (query == AuthQueries.getUserByDbId)
                {
                    return StoredUsers.Where(user => user.DbId == Variable<int>(variables, "userId")).Select(Copy).ToArray();
                }
                if (query == AuthQueries.getRefreshToken)
                {
                    RefreshTokenInfo[] tokens = RefreshTokenUserId > 0 ? [new() { UserId = RefreshTokenUserId }] : [];
                    return tokens;
                }
                return AnswerMutation(query, variables) ?? EmptyResult(resultType);
            }

            private object? AnswerMutation(string query, object? variables)
            {
                if (query == AuthQueries.bindUiUserDirectoryObject)
                {
                    UiUser storedUser = StoredUsers.Single(user => user.DbId == Variable<int>(variables, "id"));
                    storedUser.DirectoryObjectId = Variable<string>(variables, "directoryObjectId");
                    storedUser.Dn = Variable<string>(variables, "uuid")!;
                    return new ReturnId { UpdatedId = storedUser.DbId };
                }
                if (query == AuthQueries.revokeRefreshTokensOfUser)
                {
                    RevokedUserIds.Add(Variable<int>(variables, "userId"));
                    return new ReturnId { AffectedRows = 1 };
                }
                if (query == AuthQueries.upsertUiUser)
                {
                    UpsertedObjectIds.Add(Variable<string>(variables, "directoryObjectId"));
                    return new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = kNewUserId }] };
                }
                if (query == AuthQueries.updateUserLastLogin)
                {
                    return new ReturnId();
                }
                return null;
            }

            // the api hands out copies, like a real query result
            private static UiUser Copy(UiUser storedUser)
            {
                return new UiUser(storedUser);
            }

            private static T? Variable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value == null ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
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
                return Activator.CreateInstance(resultType);
            }
        }
    }
}
