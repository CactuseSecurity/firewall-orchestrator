using FWO.Api.Client;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;
using System.Security.Cryptography;

namespace FWO.Test
{
    /// <summary>
    /// Tests that admin-issued delegated token pairs cannot be rotated into a full user session (#4654).
    /// </summary>
    [TestFixture]
    public class AuthenticationTokenDelegationTest
    {
        private static readonly string[] kResolvedGroupNames = ["ApproverGroup", "cn=DirectApprover,ou=groups,dc=example,dc=com"];

        [Test]
        public async Task CreateTokenPair_WhenRefreshTokenWithheld_IssuesNonRefreshablePair()
        {
            TokenPair tokenPair = await InvokeCreateTokenPair(issueRefreshToken: false);

            Assert.That(tokenPair.AccessToken, Is.Not.Empty, "A delegated access token should still be issued.");
            Assert.That(tokenPair.RefreshToken, Is.Empty, "A delegated token pair must not contain a refresh token.");
            Assert.That(tokenPair.RefreshTokenExpires, Is.EqualTo(DateTime.MinValue), "A delegated token pair must not carry a refresh-token expiry.");
        }

        [Test]
        public void AddResolvedGroupMemberships_MergesResolvedGroupDnsIntoExistingMemberships()
        {
            Type authManagerType = typeof(AuthenticationTokenController).Assembly.GetType("FWO.Middleware.Server.Controllers.AuthManager", throwOnError: true)!;
            MethodInfo addResolvedGroupMemberships = authManagerType.GetMethod("AddResolvedGroupMemberships", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(authManagerType.FullName, "AddResolvedGroupMemberships");

            HashSet<string> userGroups = new(DistName.DnComparer)
            {
                "cn=DirectApprover,ou=groups,dc=example,dc=com"
            };

            addResolvedGroupMemberships.Invoke(null, [userGroups, kResolvedGroupNames, "ou=groups,dc=example,dc=com"]);

            Assert.That(userGroups, Does.Contain("cn=DirectApprover,ou=groups,dc=example,dc=com"));
            Assert.That(userGroups, Does.Contain("cn=ApproverGroup,ou=groups,dc=example,dc=com"));
            Assert.That(userGroups, Has.Count.EqualTo(2));
        }

        private static async Task<TokenPair> InvokeCreateTokenPair(bool issueRefreshToken)
        {
            JwtWriter jwtWriter = CreateJwtWriter();
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            UiUser targetUser = new()
            {
                Name = "delegated-target",
                DbId = 7,
                Dn = "cn=delegated-target,dc=example,dc=com",
                Roles = ["reporter"]
            };

            Type authManagerType = typeof(AuthenticationTokenController).Assembly.GetType("FWO.Middleware.Server.Controllers.AuthManager", throwOnError: true)!;
            object authManager = Activator.CreateInstance(authManagerType, jwtWriter, new List<Ldap>(), apiConnection, new TokenLifetimeProvider())!;

            MethodInfo createTokenPair = authManagerType.GetMethod("CreateTokenPair")
                ?? throw new MissingMethodException(authManagerType.FullName, "CreateTokenPair");

            object?[] arguments = [targetUser, TimeSpan.FromMinutes(5), issueRefreshToken];
            Task<TokenPair> task = (Task<TokenPair>)createTokenPair.Invoke(authManager, arguments)!;

            return await task;
        }

        private static JwtWriter CreateJwtWriter()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa.ExportParameters(true));
            return new JwtWriter(signingKey);
        }
    }
}
