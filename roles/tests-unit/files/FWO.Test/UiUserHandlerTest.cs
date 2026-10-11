using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class UiUserHandlerTest
    {
        private static readonly int[] kExpectedOwnerships = [1, 2];
        private static readonly int[] kExpectedRecertOwnerships = [3];
        private static readonly int[] kExpectedVisibilityGroupIds = [7];
        private static readonly string[] kGetWorkflowVisibilityGroupsQuery = [RequestQueries.getWorkflowVisibilityGroups];
        private static readonly string[] kUpsertUiUserQuery = [AuthQueries.upsertUiUser];
        private static readonly string[] kUpdatePasswordChangeQuery = [AuthQueries.updateUserPasswordChange];
        private const int kLdapConnectionId = 1;
        private const int kOtherLdapConnectionId = 2;
        private const string kSharedDn = "uid=shared,ou=users,dc=example,dc=com";

        [Test]
        public async Task GetExpirationTime_WhenConfigExistsInDatabase_ReturnsDatabaseValue()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime> { new() { ExpirationValue = 11 } });

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetime));

            Assert.That(expirationTime, Is.EqualTo(11));
        }

        [Test]
        public async Task GetExpirationTime_WhenConfigMissingInDatabase_ReturnsConfiguredDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfExpirationTime>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfExpirationTime>());

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.RefreshTokenLifetime));

            Assert.That(expirationTime, Is.EqualTo(1));
        }

        [Test]
        public async Task GetExpirationTime_WhenLifetimeKeyIsUnknown_ReturnsHardcodedDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, "UnknownLifetimeKey");

            Assert.That(expirationTime, Is.EqualTo(720));
        }

        [Test]
        public async Task GetOwnershipsFromOwnerLdap_UsesOwnerResponsibleQueries()
        {
            OwnershipApiConnection apiConnection = new();
            UiUser user = new()
            {
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                Groups = ["cn=group1,ou=groups,dc=example,dc=com"]
            };

            await UiUserHandler.GetOwnershipsFromOwnerLdap(apiConnection, user);

            Assert.That(user.Ownerships, Is.EquivalentTo(kExpectedOwnerships));
            Assert.That(user.RecertOwnerships, Is.EquivalentTo(kExpectedRecertOwnerships));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForUser));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersFromGroups));
            Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForDnsWithRecertification));
            Assert.That(apiConnection.Queries, Does.Not.Contain(OwnerQueries.getOwners));
            Assert.That(apiConnection.Queries, Does.Not.Contain(ConfigQueries.getConfigItemByKey));
        }

        [Test]
        public void SynchronizeUiUserContext_WhenWorkflowVisibilityGroupsCannotBeResolved_Throws()
        {
            FailingVisibilityApiConnection apiConnection = new();
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, user))!;

            Assert.That(exception.Message, Does.Contain("Workflow visibility groups could not be determined"));
        }

        [Test]
        public void SynchronizeUiUserContext_WhenApiUnreachableForVisibilityGroups_RethrowsOriginalException()
        {
            FailingVisibilityApiConnection apiConnection = new(new HttpRequestException("connection reset"));
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            HttpRequestException exception = Assert.ThrowsAsync<HttpRequestException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: false))!;

            Assert.That(exception.Message, Is.EqualTo("connection reset"));
        }

        [Test]
        public void SynchronizeUiUserContext_WhenApiUnreachableForUserLookup_RethrowsOriginalException()
        {
            UnreachableUserLookupApiConnection apiConnection = new();
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            Assert.ThrowsAsync<HttpRequestException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: false));
        }

        [Test]
        public async Task GetWorkflowVisibilityGroupIds_WhenQueryFailsForOtherReason_ReturnsFalse()
        {
            FailingVisibilityApiConnection apiConnection = new();
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com"
            };

            bool loaded = await UiUserHandler.GetWorkflowVisibilityGroupIds(apiConnection, user);

            Assert.That(loaded, Is.False);
        }

        [Test]
        public async Task GetExpirationTime_WhenConfigQueryThrows_ReturnsHardcodedDefault()
        {
            ThrowingConfigApiConnection apiConnection = new();

            int expirationTime = await UiUserHandler.GetExpirationTime(apiConnection, nameof(ConfigData.AccessTokenLifetime));

            Assert.That(expirationTime, Is.EqualTo(720));
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigContainsNumericIndex_ReturnsParsedEnum()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ConfigItems =
                [
                    new() { Value = ((int)TokenLifetimeUnit.Days).ToString() }
                ]
            };

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.AccessTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Days));
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigContainsInvalidValue_ReturnsConfiguredDefault()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ConfigItems =
                [
                    new() { Value = "not-a-valid-unit" }
                ]
            };

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.RefreshTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Days));
        }

        [Test]
        public async Task GetWorkflowVisibilityGroupIds_DeduplicatesMatchesFromUserAndGroups()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                VisibilityGroups =
                [
                    new()
                    {
                        Id = 7,
                        Name = "Direct",
                        Members = [new() { MemberDn = "uid=user1,ou=users,dc=example,dc=com" }]
                    },
                    new()
                    {
                        Id = 7,
                        Name = "Group",
                        Members = [new() { MemberDn = "cn=group1,ou=groups,dc=example,dc=com" }]
                    },
                    new()
                    {
                        Id = 9,
                        Name = "Other",
                        Members = [new() { MemberDn = "cn=someoneelse,ou=groups,dc=example,dc=com" }]
                    }
                ]
            };
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                Groups = ["cn=group1,ou=groups,dc=example,dc=com"]
            };

            bool loaded = await UiUserHandler.GetWorkflowVisibilityGroupIds(apiConnection, user);

            Assert.Multiple(() =>
            {
                Assert.That(loaded, Is.True);
                Assert.That(apiConnection.Queries, Is.EqualTo(kGetWorkflowVisibilityGroupsQuery));
                Assert.That(user.WorkflowVisibilityGroupIds, Is.EqualTo(kExpectedVisibilityGroupIds));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_WhenExistingUserIsFound_PreservesStateAndSkipsUpsert()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ExistingUsers =
                [
                    new()
                    {
                        DbId = 42,
                        Name = "user1",
                        Dn = "uid=user1,ou=users,dc=example,dc=com",
                        PasswordMustBeChanged = true
                    }
                ],
                DirectOwners =
                [
                    new() { Id = 1 },
                    new() { Id = 1 }
                ],
                GroupOwners =
                [
                    new() { Id = 2 }
                ],
                RecertOwners =
                [
                    new() { Id = 3 },
                    new() { Id = 3 }
                ],
                VisibilityGroups =
                [
                    new()
                    {
                        Id = 7,
                        Name = "Visible",
                        Members = [new() { MemberDn = "uid=user1,ou=users,dc=example,dc=com" }]
                    }
                ]
            };
            UiUser user = new()
            {
                Name = "user1",
                Dn = "uid=user1,ou=users,dc=example,dc=com",
                Groups = ["cn=group1,ou=groups,dc=example,dc=com"],
                Ownerships = [99],
                RecertOwnerships = [88],
                WorkflowVisibilityGroupIds = [55],
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            UiUser result = await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.DbId, Is.EqualTo(42));
                Assert.That(result.PasswordMustBeChanged, Is.True);
                Assert.That(result.Ownerships, Is.EquivalentTo(kExpectedOwnerships));
                Assert.That(result.RecertOwnerships, Is.EquivalentTo(kExpectedRecertOwnerships));
                Assert.That(result.WorkflowVisibilityGroupIds, Is.EquivalentTo(kExpectedVisibilityGroupIds));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getUserByDn));
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForUser));
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersFromGroups));
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnersForDnsWithRecertification));
                Assert.That(apiConnection.Queries, Does.Contain(RequestQueries.getWorkflowVisibilityGroups));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.updateUserLastLogin));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.upsertUiUser));
            });
        }

        [Test]
        public void SynchronizeUiUserContext_WhenUserIsMissingAndCreateIfMissingIsFalse_Throws()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                VisibilityGroups = []
            };
            UiUser user = new()
            {
                Name = "newuser",
                Dn = "uid=newuser,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            KeyNotFoundException exception = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: false))!;

            Assert.That(exception.Message, Does.Contain("could not be found in the local database"));
            Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.upsertUiUser));
        }

        [Test]
        public async Task SynchronizeUiUserContext_WhenUserIsMissingAndCreateIfMissingIsTrue_UpsertsUser()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                VisibilityGroups = [],
                UpsertNewId = 77
            };
            UiUser user = new()
            {
                Name = "newuser",
                Dn = "uid=newuser,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = 4 }
            };

            UiUser result = await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.DbId, Is.EqualTo(77));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.upsertUiUser));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.updateUserLastLogin));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_WhenExistingUserAndUpdateLastLoginIsTrue_UsesUpdateLastLogin()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ExistingUsers =
                [
                    new()
                    {
                        DbId = 12,
                        Name = "existing",
                        Dn = "uid=existing,ou=users,dc=example,dc=com",
                        PasswordMustBeChanged = true
                    }
                ],
                VisibilityGroups = []
            };
            UiUser user = new()
            {
                Name = "existing",
                Dn = "uid=existing,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            UiUser result = await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: true, createIfMissing: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.DbId, Is.EqualTo(12));
                Assert.That(result.PasswordMustBeChanged, Is.False);
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.updateUserLastLogin));
            });
        }

        [Test]
        public async Task HandleUiUserAtLogin_ForwardsToSynchronization()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                VisibilityGroups = []
            };
            UiUser user = new()
            {
                Name = "login",
                Dn = "uid=login,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = kLdapConnectionId }
            };

            UiUser result = await UiUserHandler.HandleUiUserAtLogin(apiConnection, user);

            Assert.That(result, Is.SameAs(user));
        }

        [Test]
        public async Task UpsertUiUser_WhenLoginHasNotHappened_UsesMutationWithoutLoginTime()
        {
            UiUserContextApiConnection apiConnection = new();
            UiUser user = new()
            {
                Name = "newuser",
                Dn = "uid=newuser,ou=users,dc=example,dc=com",
                LdapConnection = new() { Id = 3 }
            };

            await UiUserHandler.UpsertUiUser(apiConnection, user, loginHappened: false);

            Assert.That(apiConnection.Queries, Is.EqualTo(kUpsertUiUserQuery));
        }

        [Test]
        public async Task UpdateUserPasswordChanged_SendsMutation()
        {
            UiUserContextApiConnection apiConnection = new();

            await UiUserHandler.UpdateUserPasswordChanged(apiConnection, 17, true);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Is.EqualTo(kUpdatePasswordChangeQuery));
                Assert.That(ReadVariable(apiConnection.Variables.Single(), "id"), Is.EqualTo(17));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_LooksUpTheLocalUserByDirectoryAndDn()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ExistingUsers = [new() { DbId = 42, Dn = kSharedDn }]
            };
            UiUser user = new()
            {
                Name = "shared",
                Dn = kSharedDn,
                LdapConnection = new() { Id = kOtherLdapConnectionId }
            };

            await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: false);

            object? lookupVariables = apiConnection.Variables[apiConnection.Queries.IndexOf(AuthQueries.getUserByDn)];
            Assert.Multiple(() =>
            {
                Assert.That(ReadVariable(lookupVariables, "dn"), Is.EqualTo(kSharedDn));
                Assert.That(ReadVariable(lookupVariables, "ldapConnectionId"), Is.EqualTo(kOtherLdapConnectionId));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_WhenSameDnExistsInTwoDirectories_ResolvesDistinctLocalUsers()
        {
            DirectoryScopedApiConnection apiConnection = new();
            apiConnection.AddUser(kLdapConnectionId, kSharedDn, 11);
            apiConnection.AddUser(kOtherLdapConnectionId, kSharedDn, 22);
            UiUser firstUser = new() { Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kLdapConnectionId } };
            UiUser secondUser = new() { Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kOtherLdapConnectionId } };

            await UiUserHandler.SynchronizeUiUserContext(apiConnection, firstUser, updateLastLogin: false, createIfMissing: false);
            await UiUserHandler.SynchronizeUiUserContext(apiConnection, secondUser, updateLastLogin: false, createIfMissing: false);

            Assert.Multiple(() =>
            {
                Assert.That(firstUser.DbId, Is.EqualTo(11));
                Assert.That(secondUser.DbId, Is.EqualTo(22));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.upsertUiUser));
            });
        }

        [Test]
        public async Task SynchronizeUiUserContext_WhenDnIsOnlyKnownInAnotherDirectory_CreatesANewLocalUser()
        {
            DirectoryScopedApiConnection apiConnection = new();
            apiConnection.AddUser(kLdapConnectionId, kSharedDn, 11);
            UiUser user = new() { Name = "shared", Dn = kSharedDn, LdapConnection = new() { Id = kOtherLdapConnectionId } };

            UiUser result = await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: true);

            object? upsertVariables = apiConnection.Variables[apiConnection.Queries.IndexOf(AuthQueries.upsertUiUser)];
            Assert.Multiple(() =>
            {
                Assert.That(result.DbId, Is.EqualTo(DirectoryScopedApiConnection.kNewUserId));
                Assert.That(result.DbId, Is.Not.EqualTo(11));
                Assert.That(ReadVariable(upsertVariables, "ldapConnectionId"), Is.EqualTo(kOtherLdapConnectionId));
            });
        }

        [Test]
        public void SynchronizeUiUserContext_WhenUserHasNoLdapConnection_ThrowsWithoutLookup()
        {
            UiUserContextApiConnection apiConnection = new()
            {
                ExistingUsers = [new() { DbId = 42, Dn = kSharedDn }]
            };
            UiUser user = new() { Name = "shared", Dn = kSharedDn, LdapConnection = null! };

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await UiUserHandler.SynchronizeUiUserContext(apiConnection, user, updateLastLogin: false, createIfMissing: true))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("has no LDAP connection"));
                Assert.That(apiConnection.Queries, Is.Empty);
                Assert.That(user.DbId, Is.Zero);
            });
        }

        private static object? ReadVariable(object? variables, string name)
        {
            return variables?.GetType().GetProperty(name)?.GetValue(variables);
        }

        private sealed class OwnershipApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                object result = query switch
                {
                    string value when value == OwnerQueries.getOwnersForUser => new List<FwoOwner> { new() { Id = 1 } },
                    string value when value == OwnerQueries.getOwnersFromGroups => new List<FwoOwner> { new() { Id = 2 } },
                    string value when value == OwnerQueries.getOwnersForDnsWithRecertification => new List<FwoOwner> { new() { Id = 3 } },
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }
        }

        private sealed class UiUserContextApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];
            public UiUser[] ExistingUsers { get; set; } = [];
            public List<FwoOwner> DirectOwners { get; set; } = [];
            public List<FwoOwner> GroupOwners { get; set; } = [];
            public List<FwoOwner> RecertOwners { get; set; } = [];
            public List<WorkflowVisibilityGroup> VisibilityGroups { get; set; } = [];
            public List<ConfigItem> ConfigItems { get; set; } = [];
            public int UpsertNewId { get; set; } = 99;

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);
                object result = query switch
                {
                    string value when value == AuthQueries.getUserByDn => ExistingUsers,
                    string value when value == AuthQueries.updateUserLastLogin => new ReturnId { PasswordMustBeChanged = false },
                    string value when value == AuthQueries.upsertUiUser => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = UpsertNewId }] },
                    string value when value == OwnerQueries.getOwnersForUser => DirectOwners,
                    string value when value == OwnerQueries.getOwnersFromGroups => GroupOwners,
                    string value when value == OwnerQueries.getOwnersForDnsWithRecertification => RecertOwners,
                    string value when value == RequestQueries.getWorkflowVisibilityGroups => VisibilityGroups,
                    string value when value == ConfigQueries.getConfigItemByKey => ConfigItems,
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }
        }

        /// <summary>
        /// Holds local users keyed like the database does, by directory and dn, so that a lookup
        /// that ignored the directory would return the wrong local user.
        /// </summary>
        private sealed class DirectoryScopedApiConnection : SimulatedApiConnection
        {
            public const int kNewUserId = 77;
            private readonly Dictionary<(int LdapConnectionId, string Dn), int> userIds = [];
            public List<string> Queries { get; } = [];
            public List<object?> Variables { get; } = [];

            public void AddUser(int ldapConnectionId, string dn, int dbId) => userIds[(ldapConnectionId, dn)] = dbId;

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
                Variables.Add(variables);
                object result = query switch
                {
                    string value when value == AuthQueries.getUserByDn => FindUsers(variables),
                    string value when value == AuthQueries.upsertUiUser => new ReturnIdWrapper { ReturnIds = [new ReturnId { NewId = kNewUserId }] },
                    string value when value == OwnerQueries.getOwnersForUser => new List<FwoOwner>(),
                    string value when value == OwnerQueries.getOwnersForDnsWithRecertification => new List<FwoOwner>(),
                    string value when value == RequestQueries.getWorkflowVisibilityGroups => new List<WorkflowVisibilityGroup>(),
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }

            private UiUser[] FindUsers(object? variables)
            {
                string dn = (string)ReadVariable(variables, "dn")!;
                int ldapConnectionId = (int)ReadVariable(variables, "ldapConnectionId")!;
                return userIds.TryGetValue((ldapConnectionId, dn), out int dbId) ? [new UiUser { DbId = dbId, Dn = dn }] : [];
            }
        }

        private sealed class ThrowingConfigApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ConfigQueries.getConfigItemByKey)
                {
                    throw new InvalidOperationException("config lookup failed");
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        private sealed class FailingVisibilityApiConnection(Exception? visibilityFailure = null) : SimulatedApiConnection
        {
            private readonly Exception visibilityFailure = visibilityFailure ?? new InvalidOperationException("visibility lookup failed");

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                object result = query switch
                {
                    string value when value == AuthQueries.getUserByDn => new UiUser[]
                    {
                        new()
                        {
                            DbId = 42,
                            Name = "user1",
                            Dn = "uid=user1,ou=users,dc=example,dc=com"
                        }
                    },
                    string value when value == AuthQueries.updateUserLastLogin => new ReturnId { PasswordMustBeChanged = false },
                    string value when value == OwnerQueries.getOwnersForUser => new List<FwoOwner>(),
                    string value when value == OwnerQueries.getOwnersForDnsWithRecertification => new List<FwoOwner>(),
                    string value when value == RequestQueries.getWorkflowVisibilityGroups => throw visibilityFailure,
                    _ => throw new AssertionException($"Unexpected query: {query}")
                };
                return Task.FromResult((QueryResponseType)result);
            }
        }

        private sealed class UnreachableUserLookupApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new HttpRequestException("connection refused");
            }
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigExistsInDatabase_ReturnsParsedUnit()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem> { new() { Value = "Minutes" } });

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.AccessTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Minutes));
        }

        [Test]
        public async Task GetExpirationUnit_WhenConfigMissingInDatabase_ReturnsConfiguredDefault()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<ConfigItem>>(ConfigQueries.getConfigItemByKey, Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<ConfigItem>());

            TokenLifetimeUnit unit = await UiUserHandler.GetExpirationUnit(apiConnection, nameof(ConfigData.RefreshTokenLifetimeUnit));

            Assert.That(unit, Is.EqualTo(TokenLifetimeUnit.Days));
        }
    }
}
