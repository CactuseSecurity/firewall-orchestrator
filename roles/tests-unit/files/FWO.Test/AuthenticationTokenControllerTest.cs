using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AuthenticationTokenControllerTest
    {
        [Test]
        public async Task GetAsync_ReturnsAnonymousJwt_WhenCredentialsAreMissing()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<string> result = await controller.GetAsync(new AuthenticationTokenGetParameters());

            string jwt = ExtractOkString(result);
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

            Assert.Multiple(() =>
            {
                Assert.That(token.Claims.Single(claim => claim.Type == "x-hasura-default-role").Value, Is.EqualTo(Roles.Anonymous));
                Assert.That(token.Claims.Single(claim => claim.Type == "x-hasura-allowed-roles").Value, Does.Contain(Roles.Anonymous));
            });
        }

        [Test]
        public async Task GetAsync_ReturnsBadRequest_WhenCredentialsAreEmpty()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<string> result = await controller.GetAsync(new AuthenticationTokenGetParameters { Username = "", Password = "" });

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Does.Contain("Invalid credentials"));
        }

        [Test]
        public async Task GetTokenPair_ReturnsAnonymousBootstrapPair_WhenCredentialsAreMissing()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.GetTokenPair(new AuthenticationTokenGetParameters());

            TokenPair tokenPair = ExtractOkValue(result);
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(tokenPair.AccessToken);

            Assert.Multiple(() =>
            {
                Assert.That(tokenPair.RefreshToken, Is.Empty);
                Assert.That(token.Claims.Single(claim => claim.Type == "x-hasura-default-role").Value, Is.EqualTo(Roles.Anonymous));
            });
        }

        [Test]
        public async Task GetTokenPair_ReturnsBadRequest_WhenCredentialsAreEmpty()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.GetTokenPair(new AuthenticationTokenGetParameters { Username = "", Password = "" });

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Does.Contain("Invalid credentials"));
        }

        [Test]
        public async Task GetAsyncForUser_ReturnsBadRequest_WhenAdminCredentialsAreEmpty()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<string> result = await controller.GetAsyncForUser(new AuthenticationTokenGetForUserParameters());

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Does.Contain("Invalid credentials"));
        }

        [Test]
        public async Task GetAsyncForUser_ReturnsBadRequest_WhenParametersAreNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<string> result = await controller.GetAsyncForUser(null!);

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetTokenPairForUser_ReturnsBadRequest_WhenAdminCredentialsAreEmpty()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.GetTokenPairForUser(new AuthenticationTokenGetForUserParameters());

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Does.Contain("Invalid credentials"));
        }

        [Test]
        public async Task GetTokenPairForUser_ReturnsBadRequest_WhenParametersAreNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.GetTokenPairForUser(null!);

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task RefreshToken_ReturnsBadRequest_WhenTokenIsMissing()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.RefreshToken(new RefreshTokenRequest());

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Is.EqualTo("Refresh token is required"));
        }

        [Test]
        public async Task RefreshToken_ReturnsBadRequest_WhenRequestIsNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.RefreshToken(null!);

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task RevokeToken_ReturnsBadRequest_WhenTokenIsMissing()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult result = await controller.RevokeToken(new RefreshTokenRequest());

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task RevokeToken_ReturnsBadRequest_WhenRequestIsNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult result = await controller.RevokeToken(null!);

            Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task AuthManagerValidateRefreshToken_ReturnsRefreshTokenInfo()
        {
            RecordingApiConnection apiConnection = new();
            RefreshTokenInfo expectedTokenInfo = new()
            {
                UserId = 7,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
            apiConnection.NextResult = new[] { expectedTokenInfo };

            object authManager = CreateAuthManager(apiConnection);

            RefreshTokenInfo? tokenInfo = await InvokeAuthManagerAsync<RefreshTokenInfo?>(authManager, "ValidateRefreshToken", "refresh-token");

            Assert.That(tokenInfo, Is.Not.Null);
            Assert.That(tokenInfo!.UserId, Is.EqualTo(expectedTokenInfo.UserId));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.getRefreshToken));
            Assert.That(apiConnection.LastVariables!.GetType().GetProperty("tokenHash"), Is.Not.Null);
        }

        [Test]
        public async Task AuthManagerValidateRefreshToken_ReturnsNullWhenQueryFails()
        {
            RecordingApiConnection apiConnection = new()
            {
                ThrowOnQuery = new InvalidOperationException("boom")
            };

            object authManager = CreateAuthManager(apiConnection);

            RefreshTokenInfo? tokenInfo = await InvokeAuthManagerAsync<RefreshTokenInfo?>(authManager, "ValidateRefreshToken", "refresh-token");

            Assert.That(tokenInfo, Is.Null);
        }

        [Test]
        public async Task AuthManagerStoreRefreshToken_SendsStoreMutation()
        {
            RecordingApiConnection apiConnection = new()
            {
                NextResult = new object()
            };

            object authManager = CreateAuthManager(apiConnection);
            DateTime expiresAt = DateTime.UtcNow.AddHours(12);

            await InvokeAuthManagerAsync(authManager, "StoreRefreshToken", 42, "refresh-token", expiresAt);

            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.storeRefreshToken));
            Assert.That(apiConnection.CallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastVariables!.GetType().GetProperty("userId")!.GetValue(apiConnection.LastVariables), Is.EqualTo(42));
            Assert.That(apiConnection.LastVariables.GetType().GetProperty("expiresAt")!.GetValue(apiConnection.LastVariables), Is.EqualTo(expiresAt));
        }

        [Test]
        public async Task AuthManagerRevokeRefreshToken_ReturnsAffectedRows()
        {
            RecordingApiConnection apiConnection = new()
            {
                NextResult = new ReturnId { AffectedRows = 1 }
            };

            object authManager = CreateAuthManager(apiConnection);

            int revokedRows = await InvokeAuthManagerAsync<int>(authManager, "RevokeRefreshToken", "refresh-token");

            Assert.That(revokedRows, Is.EqualTo(1));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.revokeRefreshToken));
            Assert.That(apiConnection.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AuthManagerCreateTokenPair_StoresRefreshTokenForAuthenticatedUser()
        {
            RecordingApiConnection apiConnection = new()
            {
                NextResult = new object()
            };
            object authManager = CreateAuthManager(apiConnection, new FixedTokenLifetimeProvider());
            UiUser user = new()
            {
                Name = "token-user",
                DbId = 99,
                Dn = "cn=token-user,dc=example,dc=com",
                Roles = [Roles.Reporter]
            };

            TokenPair tokenPair = await InvokeAuthManagerAsync<TokenPair>(authManager, "CreateTokenPair", user, TimeSpan.FromMinutes(5), true);

            Assert.That(tokenPair.AccessToken, Is.Not.Empty);
            Assert.That(tokenPair.RefreshToken, Is.Not.Empty);
            Assert.That(tokenPair.RefreshTokenExpires, Is.Not.EqualTo(DateTime.MinValue));
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.storeRefreshToken));
            Assert.That(apiConnection.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AuthManagerCreateTokenPair_DoesNotStoreRefreshTokenForAnonymousUser()
        {
            RecordingApiConnection apiConnection = new();
            object authManager = CreateAuthManager(apiConnection, new FixedTokenLifetimeProvider());

            TokenPair tokenPair = await InvokeAuthManagerAsync<TokenPair>(authManager, "CreateTokenPair", null, null, true);

            Assert.That(tokenPair.AccessToken, Is.Not.Empty);
            Assert.That(tokenPair.RefreshToken, Is.Empty);
            Assert.That(tokenPair.RefreshTokenExpires, Is.EqualTo(DateTime.MinValue));
            Assert.That(apiConnection.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ControllerBuildJwtAuditText_IncludesExpirationInformation()
        {
            object authManager = CreateAuthManager(new RecordingApiConnection(), new FixedTokenLifetimeProvider());
            UiUser user = new()
            {
                Name = "audit-user",
                DbId = 17,
                Roles = [Roles.Reporter]
            };
            TokenPair tokenPair = await InvokeAuthManagerAsync<TokenPair>(authManager, "CreateTokenPair", user, TimeSpan.FromMinutes(5), false);

            string auditText = InvokeControllerPrivateStatic<string>("BuildJwtAuditText", tokenPair.AccessToken, "Issued access token.");

            Assert.Multiple(() =>
            {
                Assert.That(auditText, Does.StartWith("Issued access token."));
                Assert.That(auditText, Does.Contain("access_jti="));
                Assert.That(auditText, Does.Contain("access_expires="));
            });
        }

        [Test]
        public async Task ControllerBuildTokenPairAuditText_IncludesRefreshExpirationWhenPresent()
        {
            object authManager = CreateAuthManager(new RecordingApiConnection(), new FixedTokenLifetimeProvider());
            UiUser user = new()
            {
                Name = "audit-user",
                DbId = 17,
                Roles = [Roles.Reporter]
            };
            TokenPair tokenPair = await InvokeAuthManagerAsync<TokenPair>(authManager, "CreateTokenPair", user, TimeSpan.FromMinutes(5), true);

            string auditText = InvokeControllerPrivateStatic<string>("BuildTokenPairAuditText", tokenPair, "Issued token pair.");

            Assert.Multiple(() =>
            {
                Assert.That(auditText, Does.StartWith("Issued token pair."));
                Assert.That(auditText, Does.Contain("access_jti="));
                Assert.That(auditText, Does.Contain("refresh_expires="));
            });
        }

        private static AuthenticationTokenController CreateController()
        {
            RSA rsa = RSA.Create(2048);
            return new AuthenticationTokenController(
                new JwtWriter(new RsaSecurityKey(rsa)),
                [],
                new SimulatedApiConnection(),
                new FixedTokenLifetimeProvider());
        }

        private static object CreateAuthManager(ApiConnection apiConnection, TokenLifetimeProvider? tokenLifetimeProvider = null)
        {
            Type authManagerType = typeof(AuthenticationTokenController).Assembly.GetType("FWO.Middleware.Server.Controllers.AuthManager", throwOnError: true)!;
            RSA rsa = RSA.Create(2048);
            return Activator.CreateInstance(
                authManagerType,
                new JwtWriter(new RsaSecurityKey(rsa)),
                new List<Ldap>(),
                apiConnection,
                tokenLifetimeProvider ?? new FixedTokenLifetimeProvider())!;
        }

        private static async Task<ReturnType> InvokeAuthManagerAsync<ReturnType>(object authManager, string methodName, params object?[] arguments)
        {
            MethodInfo method = authManager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(authManager.GetType().FullName, methodName);
            object? result = method.Invoke(authManager, arguments);
            Task<ReturnType> task = (Task<ReturnType>)result!;
            return await task;
        }

        private static async Task InvokeAuthManagerAsync(object authManager, string methodName, params object?[] arguments)
        {
            MethodInfo method = authManager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(authManager.GetType().FullName, methodName);
            Task task = (Task)method.Invoke(authManager, arguments)!;
            await task;
        }

        private static T InvokeControllerPrivateStatic<T>(string methodName, params object?[] arguments)
        {
            MethodInfo method = typeof(AuthenticationTokenController).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(AuthenticationTokenController).FullName, methodName);
            return (T)method.Invoke(null, arguments)!;
        }

        private static string ExtractOkString(ActionResult<string> result)
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            return (string)((OkObjectResult)result.Result!).Value!;
        }

        private static TokenPair ExtractOkValue(ActionResult<TokenPair> result)
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            return (TokenPair)((OkObjectResult)result.Result!).Value!;
        }

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public string? LastQuery { get; private set; }
            public object? LastVariables { get; private set; }
            public string? LastOperationName { get; private set; }
            public int CallCount { get; private set; }
            public object? NextResult { get; set; }
            public Exception? ThrowOnQuery { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                CallCount++;
                LastQuery = query;
                LastVariables = variables;
                LastOperationName = operationName;

                if (ThrowOnQuery != null)
                {
                    throw ThrowOnQuery;
                }

                if (NextResult is QueryResponseType typedResult)
                {
                    return Task.FromResult(typedResult);
                }

                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        private sealed class FixedTokenLifetimeProvider : TokenLifetimeProvider
        {
            public override Task<TimeSpan> GetUserAccessTokenLifetimeAsync(ApiConnection apiConnection)
            {
                return Task.FromResult(TimeSpan.FromMinutes(5));
            }

            public override Task<TimeSpan> GetRefreshTokenLifetimeAsync(ApiConnection apiConnection)
            {
                return Task.FromResult(TimeSpan.FromHours(12));
            }

            public override TimeSpan GetAnonymousTokenLifetime()
            {
                return TimeSpan.FromMinutes(15);
            }
        }
    }
}
