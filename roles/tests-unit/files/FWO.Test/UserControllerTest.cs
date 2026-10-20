using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Novell.Directory.Ldap;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UserControllerTest
    {
        private const int kUserLdapId = 2;
        private const int kOtherLdapId = 3;
        private const string kUserSearchPath = "ou=users,dc=fworch,dc=internal";
        private const string kOtherUserSearchPath = "ou=users,dc=example,dc=com";
        private const string kBoundObjectUuid = "6f1b6c2e-0c5b-4a55-9e5a-5d3e2c1b0a99";
        private const string kReplacementObjectUuid = "0d4c2b1a-9e8f-4a7b-8c6d-5e4f3a2b1c0d";

        private sealed class UserApiConnection : SimulatedApiConnection
        {
            public UiUser[] Users { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getUsers)
                {
                    return Task.FromResult((T)(object)Users);
                }

                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        [Test]
        public void ChangePasswordRequiresAuthorization()
        {
            MethodInfo method = typeof(UserController).GetMethod(nameof(UserController.ChangePassword))!;

            AuthorizeAttribute? authorizeAttribute = method.GetCustomAttribute<AuthorizeAttribute>();

            Assert.That(authorizeAttribute, Is.Not.Null);
        }

        [Test]
        public async Task ChangePasswordRejectsAnonymousCaller()
        {
            UserController controller = CreateController(CreateUser(1), new ClaimsPrincipal(new ClaimsIdentity()));

            ActionResult<string> result = await controller.ChangePassword(CreateParameters(1));

            Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
        }

        [Test]
        public async Task ChangePasswordRejectsDifferentNonAdminUser()
        {
            UserController controller = CreateController(CreateUser(2), CreatePrincipal(1));

            ActionResult<string> result = await controller.ChangePassword(CreateParameters(2));

            Assert.That(result.Result, Is.TypeOf<ForbidResult>());
        }

        [Test]
        public async Task ChangePasswordAllowsCallerForOwnUser()
        {
            UserController controller = CreateController(CreateUser(1), CreatePrincipal(1));

            ActionResult<string> result = await controller.ChangePassword(CreateParameters(1));

            Assert.That(result.Value, Is.Empty);
        }

        [Test]
        public async Task ChangePasswordAllowsAdminForDifferentUser()
        {
            UserController controller = CreateController(CreateUser(2), CreatePrincipal(1, Roles.Admin));

            ActionResult<string> result = await controller.ChangePassword(CreateParameters(2));

            Assert.That(result.Value, Is.Empty);
        }

        private static UserController CreateController(UiUser user, ClaimsPrincipal principal)
        {
            UserApiConnection apiConnection = new() { Users = [user] };
            List<Ldap> readOnlyLdapOfUser = [new Ldap { Id = kUserLdapId }];
            UserController controller = new(readOnlyLdapOfUser, apiConnection)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = principal }
                }
            };
            return controller;
        }

        private static UiUser CreateUser(int id)
        {
            return new UiUser { DbId = id, Dn = $"uid=user{id},ou=user,dc=fworch,dc=internal", LdapConnection = new() { Id = kUserLdapId } };
        }

        private static ClaimsPrincipal CreatePrincipal(int userId, params string[] roles)
        {
            List<Claim> claims =
            [
                new("x-hasura-user-id", userId.ToString())
            ];
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test", ClaimTypes.Name, ClaimTypes.Role));
        }

        private static UserChangePasswordParameters CreateParameters(int userId)
        {
            return new UserChangePasswordParameters
            {
                UserId = userId,
                OldPassword = "old-password",
                NewPassword = "new-password"
            };
        }
        [Test]
        public async Task GetReturnsOnlyPersistedUsers()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users =
                [
                    new() { DbId = 0, Name = "transient" },
                    new() { DbId = 7, Name = "persisted", Dn = "uid=persisted,dc=fworch,dc=internal", Email = "persisted@test" }
                ]
            };
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            List<UserGetReturnParameters> result = await controller.Get();

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].UserId, Is.EqualTo(7));
            Assert.That(result[0].Name, Is.EqualTo("persisted"));
        }

        [Test]
        public async Task GetLdapUsersReturnsEmptyListForUnknownLdap()
        {
            UserController controller = CreateController(new UserControllerTestApiConnection(), PrincipalWithRoles(Roles.Admin));

            List<LdapUserGetReturnParameters> result = await controller.Get(new LdapUserGetParameters
            {
                LdapId = 99,
                SearchPattern = "user"
            });

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task AddReturnsZeroWithoutWritableLdap()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(
                [new Ldap { Id = 2 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                LdapId = 2,
                UserDn = "uid=newuser,dc=fworch,dc=internal",
                Password = "password",
                TenantId = 1
            });

            Assert.That(result, Is.Zero);
            Assert.That(apiConnection.UserQueryCount, Is.Zero);
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
            Assert.That(apiConnection.ReturnIdWrapperQueryCount, Is.Zero);
        }

        [Test]
        public async Task AddReturnsZeroWhenLdapIdDoesNotMatchConfiguredLdap()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(
                [new Ldap { Id = 3 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                LdapId = 2,
                UserDn = "uid=newuser,dc=fworch,dc=internal",
                Password = "password",
                TenantId = 1
            });

            Assert.That(result, Is.Zero);
            Assert.That(apiConnection.UserQueryCount, Is.Zero);
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
            Assert.That(apiConnection.ReturnIdWrapperQueryCount, Is.Zero);
        }

        [Test]
        public void ChangeThrowsForUnknownUser()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await controller.Change(new UserEditParameters
                {
                    UserId = 42,
                    Email = "new@test"
                });
            });

            Assert.That(exception?.Message, Is.EqualTo("Wrong UserId"));
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public async Task ChangeReturnsFalseWhenLdapIdDoesNotMatchConfiguredLdap()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [CreateUser(42)]
            };
            UserController controller = CreateController(
                [new Ldap { Id = 3 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Change(new UserEditParameters
            {
                UserId = 42,
                LdapId = 2,
                Email = "new@test"
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public async Task ChangeDoesNotUpdateLocalDbWithoutWritableLdapUpdate()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [new() { DbId = 42, Dn = "uid=user,dc=fworch,dc=internal" }]
            };
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Change(new UserEditParameters
            {
                UserId = 42,
                Email = "new@test"
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public async Task DeleteRemovesLocalUserWhenMatchedLdapIsReadOnly()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [CreateUser(42)]
            };
            UserController controller = CreateController(
                [new Ldap { Id = 2 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters
            {
                LdapId = 2,
                UserId = 42
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastVariablesText, Does.Contain("42"));
        }

        [Test]
        public async Task DeleteReturnsFalseWhenLdapIdDoesNotMatchConfiguredLdap()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [new() { DbId = 42, Dn = "uid=user,dc=fworch,dc=internal" }]
            };
            UserController controller = CreateController(
                [new Ldap { Id = 3 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters
            {
                LdapId = 2,
                UserId = 42
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public void DeleteThrowsForUnknownUserBeforeLocalDelete()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await controller.Delete(new UserDeleteParameters
                {
                    UserId = 42
                });
            });

            Assert.That(exception?.Message, Is.EqualTo("Wrong UserId"));
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public async Task DeleteAllGroupsAndRolesReturnsFalseWithoutWritableRoleOrGroupLdap()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [new() { DbId = 42, Dn = "uid=user,dc=fworch,dc=internal" }]
            };
            UserController controller = CreateController(
                [new Ldap { Id = 2 }],
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.DeleteAllGroupsAndRoles(new UserDeleteAllEntriesParameters
            {
                UserId = 42
            });

            Assert.That(result, Is.False);
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteAllGroupsAndRolesEndsTheSessionsOfTheUser()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [new() { DbId = 42, Dn = "uid=user,ou=users,dc=fworch,dc=internal" }]
            };
            TestableLdap roleLdap = new(new RecordingLdapClient())
            {
                Id = kUserLdapId,
                Addresses = ["ldap.example.test"],
                Port = 389,
                SearchUser = "cn=search,dc=fworch,dc=internal",
                SearchUserPwd = LdapTestSupport.CreateEncryptedSecret("searchpwd"),
                WriteUser = "cn=write,dc=fworch,dc=internal",
                WriteUserPwd = "writepwd",
                UserSearchPath = kUserSearchPath,
                RoleSearchPath = "ou=roles,dc=fworch,dc=internal"
            };
            UserController controller = CreateController(new List<Ldap> { roleLdap }, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.DeleteAllGroupsAndRoles(new UserDeleteAllEntriesParameters
            {
                UserId = 42
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.revokeRefreshTokensOfUsers));
        }

        [Test]
        public async Task ChangePasswordRejectsAuditorModeBeforeUserLookup()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor, Roles.Approver));

            ActionResult<string> result = await controller.ChangePassword(new UserChangePasswordParameters
            {
                UserId = 1,
                ExecutionMode = Roles.Auditor
            });

            Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That(apiConnection.UserQueryCount, Is.Zero);
        }

        [Test]
        public void ChangePasswordInUserRolesModeDoesNotRejectAuditorWithWorkflowRole()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor, Roles.Approver));

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await controller.ChangePassword(new UserChangePasswordParameters
                {
                    UserId = 1,
                    ExecutionMode = GlobalConst.kUserRolesSelection
                });
            });

            Assert.That(exception?.Message, Is.EqualTo("Wrong UserId"));
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ChangePasswordWithoutExecutionModeStillRejectsSingleAuditor()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Auditor));

            ActionResult<string> result = await controller.ChangePassword(new UserChangePasswordParameters
            {
                UserId = 1
            });

            Assert.That(result.Result, Is.TypeOf<UnauthorizedResult>());
            Assert.That(apiConnection.UserQueryCount, Is.Zero);
        }

        [Test]
        public void ResetPasswordThrowsForUnknownUser()
        {
            UserControllerTestApiConnection apiConnection = new();
            UserController controller = CreateController(apiConnection, PrincipalWithRoles(Roles.Admin));

            ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await controller.ResetPassword(new UserResetPasswordParameters
                {
                    UserId = 42,
                    NewPassword = "new-password"
                });
            });

            Assert.That(exception?.Message, Is.EqualTo("Wrong UserId"));
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ResetPasswordReturnsOkWithoutWritableLdapOfTheUser()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [CreateUser(42)]
            };
            List<Ldap> ldaps = [new Ldap { Id = kUserLdapId }];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            ActionResult<string> result = await controller.ResetPassword(new UserResetPasswordParameters
            {
                UserId = 42,
                NewPassword = "new-password"
            });

            Assert.That(result.Result, Is.TypeOf<OkResult>());
            Assert.That(apiConnection.UserQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.ReturnIdQueryCount, Is.Zero);
        }

        [Test]
        public async Task AddReturnsNewIdWhenWritableLdapAndDatabaseInsertSucceed()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                AddUserResult = new ReturnIdWrapper
                {
                    ReturnIds = new ReturnId[] { new() { NewId = 77 } }
                }
            };
            UserController controller = CreateController(
                new List<Ldap> { CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                LdapId = 2,
                UserDn = "uid=newuser,ou=users,dc=fworch,dc=internal",
                Password = "password",
                Email = "new@test",
                TenantId = 1
            });

            Assert.That(result, Is.EqualTo(77));
            Assert.That(apiConnection.ReturnIdWrapperQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.upsertUiUser));
            Assert.That(ldapClient.AddedEntries, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Without a given LDAP the user is added to the writable one, and the local user has to be
        /// bound to that directory: a local user without directory is matched by no login, as the
        /// same dn may exist in another LDAP connection (SEC-11).
        /// </summary>
        [Test]
        public async Task AddWithoutLdapIdBindsLocalUserToTheLdapThatAddedIt()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                AddUserResult = new ReturnIdWrapper
                {
                    ReturnIds = new ReturnId[] { new() { NewId = 78 } }
                }
            };
            UserController controller = CreateController(
                new List<Ldap> { new() { Id = 1 }, CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                LdapId = 0,
                UserDn = "uid=newuser,ou=users,dc=fworch,dc=internal",
                Password = "password",
                TenantId = 1
            });

            Assert.That(result, Is.EqualTo(78));
            Assert.That(apiConnection.LastVariablesText, Does.Contain("ldapConnectionId = 2 "));
        }

        [Test]
        public async Task ChangeReturnsTrueWhenWritableLdapAndDatabaseUpdateSucceed()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = new UiUser[] { CreateUser(42) }
            };
            UserController controller = CreateController(
                new List<Ldap> { CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Change(new UserEditParameters
            {
                UserId = 42,
                LdapId = 2,
                Email = "new@test"
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.ReturnIdQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.updateUserEmail));
            Assert.That(ldapClient.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ChangePasswordReturnsEmptyStringAndUpdatesPasswordChanged()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = new UiUser[] { CreateUser(42) }
            };
            UserController controller = CreateController(
                new List<Ldap> { CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            ActionResult<string> result = await controller.ChangePassword(new UserChangePasswordParameters
            {
                UserId = 42,
                LdapId = 2,
                OldPassword = "old-password",
                NewPassword = "new-password"
            });

            Assert.That(result.Value, Is.Empty);
            Assert.That(apiConnection.ReturnIdQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.updateUserPasswordChange));
            Assert.That(ldapClient.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ChangePasswordRejectsTheLdapOfAnotherDirectory()
        {
            RecordingLdapClient ownClient = new();
            RecordingLdapClient otherClient = new();
            UserControllerTestApiConnection apiConnection = new() { Users = [CreateUser(42)] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ownClient), CreateWritableLdap(kOtherLdapId, otherClient, kOtherUserSearchPath)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                CreatePrincipal(42));

            ActionResult<string> result = await controller.ChangePassword(new UserChangePasswordParameters
            {
                UserId = 42,
                LdapId = kOtherLdapId,
                OldPassword = "old-password",
                NewPassword = "new-password"
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(ownClient.ModifyCalls, Is.Empty);
                Assert.That(otherClient.ModifyCalls, Is.Empty);
                Assert.That(otherClient.BindCalls, Is.Empty);
            });
        }

        [Test]
        public async Task ResetPasswordWithoutLdapIdChangesOnlyTheDirectoryOfTheUser()
        {
            RecordingLdapClient ownClient = new();
            RecordingLdapClient otherClient = new();
            UserControllerTestApiConnection apiConnection = new() { Users = [CreateUser(42)] };
            List<Ldap> ldaps = [CreateWritableLdap(kOtherLdapId, otherClient, kOtherUserSearchPath), CreateWritableLdap(kUserLdapId, ownClient)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            ActionResult<string> result = await controller.ResetPassword(new UserResetPasswordParameters
            {
                UserId = 42,
                NewPassword = "new-password"
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.TypeOf<OkResult>());
                Assert.That(ownClient.ModifyCalls, Has.Count.EqualTo(1));
                Assert.That(otherClient.ModifyCalls, Is.Empty);
            });
        }

        [Test]
        public async Task ResetPasswordRejectsTheLdapOfAnotherDirectory()
        {
            RecordingLdapClient otherClient = new();
            UserControllerTestApiConnection apiConnection = new() { Users = [CreateUser(42)] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, new RecordingLdapClient()), CreateWritableLdap(kOtherLdapId, otherClient, kOtherUserSearchPath)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            ActionResult<string> result = await controller.ResetPassword(new UserResetPasswordParameters
            {
                UserId = 42,
                LdapId = kOtherLdapId,
                NewPassword = "new-password"
            });

            Assert.Multiple(() =>
            {
                Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
                Assert.That(otherClient.ModifyCalls, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.updateUserPasswordChange));
            });
        }

        [Test]
        public async Task DeleteWithoutLdapIdDeletesOnlyInTheDirectoryOfTheUser()
        {
            RecordingLdapClient ownClient = new();
            RecordingLdapClient otherClient = new();
            UserControllerTestApiConnection apiConnection = new() { Users = [CreateUser(42)] };
            List<Ldap> ldaps = [CreateWritableLdap(kOtherLdapId, otherClient, kOtherUserSearchPath), CreateWritableLdap(kUserLdapId, ownClient)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(ownClient.DeletedDns, Has.Count.EqualTo(1));
                Assert.That(otherClient.DeletedDns, Is.Empty);
            });
        }

        [Test]
        public async Task AddWithoutLdapIdRejectsSeveralWritableLdaps()
        {
            RecordingLdapClient firstClient = new();
            RecordingLdapClient secondClient = new();
            UserControllerTestApiConnection apiConnection = new();
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, firstClient), CreateWritableLdap(kOtherLdapId, secondClient, kOtherUserSearchPath)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                UserDn = "uid=newuser,ou=users,dc=fworch,dc=internal",
                Password = "password",
                TenantId = 1
            });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Zero);
                Assert.That(firstClient.AddedEntries, Is.Empty);
                Assert.That(secondClient.AddedEntries, Is.Empty);
                Assert.That(apiConnection.ReturnIdWrapperQueryCount, Is.Zero);
            });
        }

        [Test]
        public async Task AddRejectsADnOutsideThePathsOfTheLdap()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new();
            List<Ldap> ldaps = [CreateWritableLdap(kOtherLdapId, ldapClient, kOtherUserSearchPath)];
            UserController controller = CreateController(
                ldaps,
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            int result = await controller.Add(new UserAddParameters
            {
                LdapId = kOtherLdapId,
                UserDn = "uid=newuser,ou=users,dc=fworch,dc=internal",
                Password = "password",
                TenantId = 1
            });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Zero);
                Assert.That(ldapClient.AddedEntries, Is.Empty);
            });
        }

        [Test]
        public async Task ResetPasswordReturnsOkAndUpdatesPasswordChanged()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = new UiUser[] { CreateUser(42) }
            };
            UserController controller = CreateController(
                new List<Ldap> { CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            ActionResult<string> result = await controller.ResetPassword(new UserResetPasswordParameters
            {
                UserId = 42,
                LdapId = 2,
                NewPassword = "new-password"
            });

            Assert.That(result.Result, Is.TypeOf<OkResult>());
            Assert.That(apiConnection.ReturnIdQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.updateUserPasswordChange));
            Assert.That(ldapClient.ModifyCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DeleteReturnsTrueWhenWritableLdapAndDatabaseDeleteSucceed()
        {
            RecordingLdapClient ldapClient = new();
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = new UiUser[] { CreateUser(42) }
            };
            UserController controller = CreateController(
                new List<Ldap> { CreateWritableLdap(2, ldapClient) },
                apiConnection,
                PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters
            {
                LdapId = 2,
                UserId = 42
            });

            Assert.That(result, Is.True);
            Assert.That(apiConnection.ReturnIdQueryCount, Is.EqualTo(1));
            Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.deleteUser));
            Assert.That(ldapClient.DeletedDns, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task DeleteKeepsTheEntryOfAnotherAccountThatTookTheDn()
        {
            UiUser boundUser = CreateBoundUser(42);
            RecordingLdapClient ldapClient = new() { ReadResult = CreateDirectoryEntry(boundUser.Dn, kReplacementObjectUuid) };
            UserControllerTestApiConnection apiConnection = new() { Users = [boundUser] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ldapClient)];
            UserController controller = CreateController(ldaps, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { LdapId = kUserLdapId, UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(ldapClient.ReadCalls, Has.Count.EqualTo(1).And.Contains(boundUser.Dn));
                Assert.That(ldapClient.DeletedDns, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getUserByDbId));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.deleteUser));
            });
        }

        [Test]
        public async Task DeleteRemovesOnlyTheLocalUserWhenTheBoundEntryIsGone()
        {
            UiUser boundUser = CreateBoundUser(42);
            RecordingLdapClient ldapClient = new() { ReadException = new LdapException("no such object", LdapException.NoSuchObject, null) };
            UserControllerTestApiConnection apiConnection = new() { Users = [boundUser] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ldapClient)];
            UserController controller = CreateController(ldaps, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { LdapId = kUserLdapId, UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(ldapClient.DeletedDns, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.deleteUser));
            });
        }

        [Test]
        public async Task DeleteRemovesTheEntryThatStillIsTheBoundObject()
        {
            UiUser boundUser = CreateBoundUser(42);
            RecordingLdapClient ldapClient = new() { ReadResult = CreateDirectoryEntry(boundUser.Dn, kBoundObjectUuid.ToUpperInvariant()) };
            UserControllerTestApiConnection apiConnection = new() { Users = [boundUser] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ldapClient)];
            UserController controller = CreateController(ldaps, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { LdapId = kUserLdapId, UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(ldapClient.DeletedDns, Has.Count.EqualTo(1).And.Contains(boundUser.Dn));
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.deleteUser));
            });
        }

        [Test]
        public async Task DeleteKeepsTheLocalUserWhenTheDirectoryCannotBeRead()
        {
            UiUser boundUser = CreateBoundUser(42);
            RecordingLdapClient ldapClient = new() { ReadException = new LdapException("busy", LdapException.Busy, null) };
            UserControllerTestApiConnection apiConnection = new() { Users = [boundUser] };
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ldapClient)];
            UserController controller = CreateController(ldaps, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { LdapId = kUserLdapId, UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(ldapClient.DeletedDns, Is.Empty);
                Assert.That(apiConnection.Queries, Does.Not.Contain(AuthQueries.deleteUser));
            });
        }

        [Test]
        public async Task DeleteKeepsTheLocalUserWhenItsBindingCannotBeRead()
        {
            RecordingLdapClient ldapClient = new();
            List<Ldap> ldaps = [CreateWritableLdap(kUserLdapId, ldapClient)];
            BindingQueryFailingApiConnection apiConnection = new() { Users = [CreateBoundUser(42)] };
            UserController controller = CreateController(ldaps, apiConnection, PrincipalWithRoles(Roles.Admin));

            bool result = await controller.Delete(new UserDeleteParameters { LdapId = kUserLdapId, UserId = 42 });

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(ldapClient.ReadCalls, Is.Empty);
                Assert.That(ldapClient.DeletedDns, Is.Empty);
            });
        }

        private static UiUser CreateBoundUser(int id)
        {
            UiUser user = CreateUser(id);
            user.DirectoryObjectId = $"{LdapDirectoryObjectId.kEntryUuid}:{kBoundObjectUuid}";
            return user;
        }

        private static LdapEntry CreateDirectoryEntry(string dn, string entryUuid)
        {
            return LdapTestSupport.CreateEntry(dn, new LdapAttribute(LdapDirectoryObjectId.kEntryUuid, entryUuid));
        }

        private sealed class BindingQueryFailingApiConnection : SimulatedApiConnection
        {
            public UiUser[] Users { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getUsers)
                {
                    return Task.FromResult((T)(object)Users);
                }
                throw new InvalidOperationException("query failed");
            }
        }

        private static UserController CreateController(ApiConnection apiConnection, ClaimsPrincipal user)
        {
            return CreateController([], apiConnection, user);
        }

        private static UserController CreateController(List<Ldap> ldaps, ApiConnection apiConnection, ClaimsPrincipal user)
        {
            return new UserController(ldaps, apiConnection)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = user
                    }
                }
            };
        }

        private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
        {
            List<Claim> claims =
            [
                new("x-hasura-user-id", "1")
            ];
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            return new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                "test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private static TestableLdap CreateWritableLdap(int id, RecordingLdapClient client, string userSearchPath = kUserSearchPath)
        {
            return new TestableLdap(client)
            {
                Id = id,
                Addresses = ["ldap.example.test"],
                Port = 389,
                UserSearchPath = userSearchPath,
                WriteUser = "cn=write,dc=fworch,dc=internal",
                WriteUserPwd = "writepwd"
            };
        }
    }

    internal sealed class UserControllerTestApiConnection : SimulatedApiConnection
    {
        public int UserQueryCount { get; private set; }
        public int ReturnIdQueryCount { get; private set; }
        public int ReturnIdWrapperQueryCount { get; private set; }
        public List<string> Queries { get; } = new();
        public string LastVariablesText { get; private set; } = "";
        public UiUser[] Users { get; set; } = [];
        public ReturnIdWrapper AddUserResult { get; set; } = new();

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (typeof(QueryResponseType) == typeof(UiUser[]))
            {
                UserQueryCount++;
                return Task.FromResult((QueryResponseType)(object)Users);
            }
            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                ReturnIdQueryCount++;
                LastVariablesText = variables?.ToString() ?? "";
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }
            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                ReturnIdWrapperQueryCount++;
                LastVariablesText = variables?.ToString() ?? "";
                return Task.FromResult((QueryResponseType)(object)AddUserResult);
            }

            throw new NotImplementedException();
        }
    }
}
