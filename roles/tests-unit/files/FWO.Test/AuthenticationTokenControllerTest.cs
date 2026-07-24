using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
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
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AuthenticationTokenControllerTest
    {
        private static readonly string[] kLoginUserCn = { "login-user" };
        private static readonly string[] kLoginUserDn = { "uid=login-user,ou=users,dc=fworch,dc=internal" };
        private static readonly string[] kReporterRoleValues = { Roles.Reporter };
        private static readonly RefreshTokenInfo[] kRefreshTokenUserId7 = { new() { UserId = 7 } };
        private static readonly UiUser[] kTokenUser = { new() { DbId = 7, Name = "token-user" } };
        private static readonly UiUser[] kLoginUserResult =
        {
            new()
            {
                DbId = 7,
                Name = "login-user",
                Dn = "uid=login-user,ou=users,dc=fworch,dc=internal"
            }
        };
        private static readonly string kSearchPassword = LdapTestSupport.CreateEncryptedSecret("searchpwd");

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
        public async Task GetAsync_ReturnsAnonymousJwt_WhenParametersAreNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<string> result = await controller.GetAsync(null!);

            string jwt = ExtractOkString(result);
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

            Assert.That(token.Claims.Single(claim => claim.Type == "x-hasura-default-role").Value, Is.EqualTo(Roles.Anonymous));
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
        public async Task GetTokenPair_ReturnsAnonymousBootstrapPair_WhenParametersAreNull()
        {
            AuthenticationTokenController controller = CreateController();

            ActionResult<TokenPair> result = await controller.GetTokenPair(null!);

            TokenPair tokenPair = ExtractOkValue(result);
            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(tokenPair.AccessToken);

            Assert.That(tokenPair.RefreshToken, Is.Empty);
            Assert.That(token.Claims.Single(claim => claim.Type == "x-hasura-default-role").Value, Is.EqualTo(Roles.Anonymous));
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
        public async Task RefreshToken_ReturnsUnauthorizedWhenRefreshTokenIsUnknown()
        {
            AuthenticationTokenController controller = CreateController(new RecordingApiConnection
            {
                NextResult = Array.Empty<RefreshTokenInfo>()
            });

            ActionResult<TokenPair> result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "refresh-token" });

            Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(((UnauthorizedObjectResult)result.Result!).Value, Is.EqualTo("Invalid or expired refresh token"));
        }

        [Test]
        public async Task RefreshToken_ReturnsUnauthorizedWhenUserCannotBeFound()
        {
            RecordingApiConnection apiConnection = new();
            apiConnection.QueueResult(kRefreshTokenUserId7);
            apiConnection.QueueResult(Array.Empty<UiUser>());
            AuthenticationTokenController controller = CreateController(apiConnection);

            ActionResult<TokenPair> result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "refresh-token" });

            Assert.That(result.Result, Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(((UnauthorizedObjectResult)result.Result!).Value, Is.EqualTo("User not found"));
        }

        [Test]
        public async Task RevokeToken_ReturnsUnauthorizedWhenRefreshTokenIsUnknown()
        {
            AuthenticationTokenController controller = CreateController(new RecordingApiConnection
            {
                NextResult = Array.Empty<RefreshTokenInfo>()
            });

            ActionResult result = await controller.RevokeToken(new RefreshTokenRequest { RefreshToken = "refresh-token" });

            Assert.That(result, Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(((UnauthorizedObjectResult)result).Value, Is.EqualTo("Invalid or expired refresh token"));
        }

        [Test]
        public async Task RevokeToken_ReturnsUnauthorizedWhenRevocationAffectsNoRows()
        {
            RecordingApiConnection apiConnection = new();
            apiConnection.QueueResult(kRefreshTokenUserId7);
            apiConnection.QueueResult(kTokenUser);
            apiConnection.QueueResult(new ReturnId { AffectedRows = 0 });
            AuthenticationTokenController controller = CreateController(apiConnection);

            ActionResult result = await controller.RevokeToken(new RefreshTokenRequest { RefreshToken = "refresh-token" });

            Assert.That(result, Is.TypeOf<UnauthorizedObjectResult>());
            Assert.That(((UnauthorizedObjectResult)result).Value, Is.EqualTo("Invalid or expired refresh token"));
        }

        [Test]
        public async Task GetTokenPair_ReturnsAuthenticatedPair_WhenLdapAndApiSucceed()
        {
            RecordingApiConnection apiConnection = new()
            {
                Responder = (query, variables, resultType) => QueryResponse(query, resultType)
            };
            RecordingLdapClient ldapClient = new()
            {
                SearchResponder = (baseDn, scope, filter, attributes, typesOnly) =>
                {
                    if (baseDn == "ou=users,dc=fworch,dc=internal")
                    {
                        return LdapTestSupport.CreateSearchResults(
                            LdapTestSupport.CreateEntry(
                                "uid=login-user,ou=users,dc=fworch,dc=internal",
                                new LdapAttribute("cn", kLoginUserCn)));
                    }

                    if (baseDn == "ou=roles,dc=fworch,dc=internal")
                    {
                        return LdapTestSupport.CreateSearchResults(
                            LdapTestSupport.CreateEntry(
                                "cn=reporter,ou=roles,dc=fworch,dc=internal",
                                new LdapAttribute("cn", kReporterRoleValues),
                                new LdapAttribute("uniqueMember", kLoginUserDn)));
                    }

                    return LdapTestSupport.CreateSearchResults();
                }
            };
            AuthenticationTokenController controller = CreateController(
                new List<Ldap> { CreateAuthLdap(ldapClient) },
                apiConnection);

            ActionResult<TokenPair> result = await controller.GetTokenPair(new AuthenticationTokenGetParameters
            {
                Username = "login-user",
                Password = "password"
            });

            TokenPair tokenPair = ExtractOkValue(result);

            Assert.Multiple(() =>
            {
                Assert.That(tokenPair.AccessToken, Is.Not.Empty);
                Assert.That(tokenPair.RefreshToken, Is.Not.Empty);
                Assert.That(apiConnection.LastQuery, Is.EqualTo(AuthQueries.storeRefreshToken));
                Assert.That(ldapClient.SearchCalls, Is.Not.Empty);
            });
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
            RefreshTokenInfo[] nextResult = { expectedTokenInfo };
            apiConnection.NextResult = nextResult;

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
            return CreateController(new RecordingApiConnection());
        }

        private static AuthenticationTokenController CreateController(List<Ldap> ldaps, ApiConnection apiConnection)
        {
            RSA rsa = RSA.Create(2048);
            return new AuthenticationTokenController(
                new JwtWriter(new RsaSecurityKey(rsa)),
                ldaps,
                apiConnection,
                new FixedTokenLifetimeProvider());
        }

        private static AuthenticationTokenController CreateController(ApiConnection apiConnection)
        {
            return CreateController([], apiConnection);
        }

        private static TestableLdap CreateAuthLdap(RecordingLdapClient client)
        {
            return new TestableLdap(client)
            {
                Id = 11,
                Address = "ldap.example.test",
                Port = 389,
                SearchUser = "cn=search,dc=fworch,dc=internal",
                SearchUserPwd = kSearchPassword,
                RoleSearchPath = "ou=roles,dc=fworch,dc=internal",
                UserSearchPath = "ou=users,dc=fworch,dc=internal",
                TenantId = 7
            };
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
            public Queue<object> QueuedResults { get; } = new();
            public Exception? ThrowOnQuery { get; set; }
            public Func<string, object?, Type, object?>? Responder { get; set; }

            public void QueueResult(object result)
            {
                QueuedResults.Enqueue(result);
            }

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

                if (Responder != null)
                {
                    object? responderResult = Responder(query, variables, typeof(QueryResponseType));
                    if (responderResult is QueryResponseType responderTypedResult)
                    {
                        return Task.FromResult(responderTypedResult);
                    }
                }

                if (QueuedResults.Count > 0)
                {
                    object queuedResult = QueuedResults.Dequeue();
                    if (queuedResult is QueryResponseType queuedTypedResult)
                    {
                        return Task.FromResult(queuedTypedResult);
                    }
                }

                if (NextResult is QueryResponseType typedResult)
                {
                    return Task.FromResult(typedResult);
                }

                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        private static object? QueryResponse(string query, Type resultType)
        {
            if (resultType == typeof(UiUser[]))
            {
                if (query == AuthQueries.getUserByDbId)
                {
                    return kLoginUserResult;
                }

                if (query == AuthQueries.getUserByDn)
                {
                    return kLoginUserResult;
                }
            }

            if (resultType == typeof(ReturnId))
            {
                if (query == AuthQueries.updateUserLastLogin)
                {
                    return new ReturnId { PasswordMustBeChanged = false };
                }

                if (query == AuthQueries.revokeRefreshToken)
                {
                    return new ReturnId { AffectedRows = 1 };
                }
            }

            if (resultType == typeof(ReturnIdWrapper) && query == AuthQueries.storeRefreshToken)
            {
                return new ReturnIdWrapper();
            }

            if (resultType == typeof(List<FwoOwner>))
            {
                return new List<FwoOwner>();
            }

            if (resultType == typeof(List<WorkflowVisibilityGroup>))
            {
                return new List<WorkflowVisibilityGroup>();
            }

            if (resultType == typeof(Device[]))
            {
                return Array.Empty<Device>();
            }

            if (resultType == typeof(Management[]))
            {
                return Array.Empty<Management>();
            }

            if (resultType == typeof(Tenant[]))
            {
                return Array.Empty<Tenant>();
            }

            return null;
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
