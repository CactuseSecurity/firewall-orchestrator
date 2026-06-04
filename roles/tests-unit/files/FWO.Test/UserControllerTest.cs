using FWO.Api.Client;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UserControllerTest
    {
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
                Users = [new() { DbId = 42, Dn = "uid=user,dc=fworch,dc=internal" }]
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
        public async Task ResetPasswordReturnsOkWithoutMatchingWritableLdap()
        {
            UserControllerTestApiConnection apiConnection = new()
            {
                Users = [new() { DbId = 42, Dn = "uid=user,dc=fworch,dc=internal" }]
            };
            UserController controller = CreateController(
                [new Ldap { Id = 3 }],
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
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }
    }

    internal sealed class UserControllerTestApiConnection : SimulatedApiConnection
    {
        public int UserQueryCount { get; private set; }
        public int ReturnIdQueryCount { get; private set; }
        public int ReturnIdWrapperQueryCount { get; private set; }
        public string LastVariablesText { get; private set; } = "";
        public UiUser[] Users { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
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
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper());
            }

            throw new NotImplementedException();
        }
    }
}
