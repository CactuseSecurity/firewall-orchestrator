using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using FWO.Basics;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AuthenticationTokenSessionCheckTest
    {
        private const int kCallerUserId = 7;
        private const string kCallerDn = "uid=caller,ou=users,dc=fworch,dc=internal";
        private const string kRefreshToken = "refresh-token";

        [Test]
        public void CheckSession_RequiresAuthenticationAndKeepsTheRateLimit()
        {
            MethodInfo method = typeof(AuthenticationTokenController).GetMethod(nameof(AuthenticationTokenController.CheckSession))!;

            Assert.Multiple(() =>
            {
                Assert.That(method.GetCustomAttribute<AuthorizeAttribute>(), Is.Not.Null);
                Assert.That(method.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null);
                Assert.That(method.GetCustomAttribute<DisableRateLimitingAttribute>(), Is.Null);
            });
        }

        [Test]
        public async Task CheckSession_ReportsALiveSessionOfTheCaller()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenUserId = kCallerUserId };

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(CreateParameters());

            Assert.That(ExtractOk(result), Is.True);
        }

        [Test]
        public async Task CheckSession_ReportsAnEndedSessionWhenTheTokenIsNotLive()
        {
            SessionRevocationApiConnection apiConnection = new();

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(CreateParameters());

            Assert.That(ExtractOk(result), Is.False);
        }

        [Test]
        public async Task CheckSession_ReportsAnEndedSessionWhenTheTokenBelongsToAnotherUser()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenUserId = kCallerUserId + 1 };

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(CreateParameters());

            Assert.That(ExtractOk(result), Is.False);
        }

        [Test]
        public async Task CheckSession_RefusesCallersWithoutUserIdentity()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenUserId = kCallerUserId };
            ClaimsPrincipal serviceCaller = new(new ClaimsIdentity(new List<Claim> { new(ClaimTypes.Role, Roles.MiddlewareServer) }, "test"));

            ActionResult<bool> result = await CreateController(apiConnection, serviceCaller).CheckSession(CreateParameters());

            Assert.That(result.Result, Is.TypeOf<ForbidResult>());
        }

        [TestCase("")]
        [TestCase("   ")]
        public async Task CheckSession_RejectsAMissingRefreshToken(string refreshToken)
        {
            ActionResult<bool> result = await CreateController(new SessionRevocationApiConnection(), CreateCaller())
                .CheckSession(new SessionCheckParameters { RefreshToken = refreshToken });

            AssertRefreshTokenError(result);
        }

        [Test]
        public async Task CheckSession_RejectsATooLongRefreshToken()
        {
            string refreshToken = new('a', SessionCheckParameters.kMaxRefreshTokenLength + 1);

            ActionResult<bool> result = await CreateController(new SessionRevocationApiConnection(), CreateCaller())
                .CheckSession(new SessionCheckParameters { RefreshToken = refreshToken });

            AssertRefreshTokenError(result);
        }

        [Test]
        public async Task CheckSession_AcceptsARefreshTokenOfMaximumLengthWithoutOptions()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenUserId = kCallerUserId };
            SessionCheckParameters parameters = new() { RefreshToken = new('a', SessionCheckParameters.kMaxRefreshTokenLength), Options = null };

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(parameters);

            Assert.That(ExtractOk(result), Is.True);
        }

        [Test]
        public void ValidateSessionCheck_RejectsAMissingRequest()
        {
            Dictionary<string, string[]> errors = AuthenticationTokenController.ValidateSessionCheck(null);

            Assert.That(errors.Keys, Is.EquivalentTo(new List<string> { "refreshToken" }));
        }

        [Test]
        public void SessionCheckParameters_DefaultToEmptyOptions()
        {
            SessionCheckParameters parameters = new();

            Assert.Multiple(() =>
            {
                Assert.That(parameters.RefreshToken, Is.Empty);
                Assert.That(parameters.Options, Is.Not.Null);
            });
        }

        [Test]
        public async Task CheckSession_ReportsServiceUnavailableWhenTheApiCannotBeReached()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenQueryException = new HttpRequestException("connection refused") };

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(CreateParameters());

            AssertServiceUnavailable(result);
        }

        [Test]
        public async Task CheckSession_ReportsServiceUnavailableWhenTheCheckFails()
        {
            SessionRevocationApiConnection apiConnection = new() { RefreshTokenQueryException = new InvalidOperationException("field refresh_token does not exist") };

            ActionResult<bool> result = await CreateController(apiConnection, CreateCaller()).CheckSession(CreateParameters());

            AssertServiceUnavailable(result);
        }

        private static SessionCheckParameters CreateParameters()
        {
            return new SessionCheckParameters { RefreshToken = kRefreshToken };
        }

        private static ClaimsPrincipal CreateCaller()
        {
            List<Claim> claims = new()
            {
                new("x-hasura-user-id", kCallerUserId.ToString()),
                new("x-hasura-uuid", kCallerDn),
                new(ClaimTypes.Role, Roles.Reporter)
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private static AuthenticationTokenController CreateController(SessionRevocationApiConnection apiConnection, ClaimsPrincipal caller)
        {
            AuthenticationTokenController controller = new(
                new JwtWriter(new RsaSecurityKey(RSA.Create(2048))),
                new List<Ldap>(),
                apiConnection,
                new TokenLifetimeProvider(),
                new LoginThrottle(new LoginThrottleSettings()));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = caller } };
            return controller;
        }

        private static bool ExtractOk(ActionResult<bool> result)
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            return (bool)((OkObjectResult)result.Result!).Value!;
        }

        private static void AssertRefreshTokenError(ActionResult<bool> result)
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            ValidationProblemDetails details = (ValidationProblemDetails)((BadRequestObjectResult)result.Result!).Value!;
            Assert.That(details.Errors["refreshToken"], Is.EqualTo(new List<string> { SessionCheckParameters.kRefreshTokenDescription }));
        }

        private static void AssertServiceUnavailable(ActionResult<bool> result)
        {
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
        }
    }
}
