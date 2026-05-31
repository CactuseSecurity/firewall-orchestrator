using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UserControllerTest
    {
        private sealed class UserApiConnection : SimulatedApiConnection
        {
            public UiUser[] Users { get; set; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null)
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
            UserController controller = new([], apiConnection)
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
            return new UiUser { DbId = id, Dn = $"uid=user{id},ou=user,dc=fworch,dc=internal" };
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
    }
}
