using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    [TestFixture]
    public class AuthenticationTokenControllerAuditTest
    {
        private static readonly MethodInfo BuildJwtAuditTextMethod = typeof(AuthenticationTokenController).GetMethod("BuildJwtAuditText", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AuthenticationTokenController).FullName, "BuildJwtAuditText");

        private static readonly MethodInfo BuildTokenPairAuditTextMethod = typeof(AuthenticationTokenController).GetMethod("BuildTokenPairAuditText", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AuthenticationTokenController).FullName, "BuildTokenPairAuditText");

        [Test]
        public void BuildJwtAuditText_IncludesJwtIdAndExpiry()
        {
            JwtWriter jwtWriter = CreateJwtWriter();
            UiUser user = CreateUser();
            string jwt = jwtWriter.CreateJWT(user, TimeSpan.FromMinutes(5));

            string auditText = (string)(BuildJwtAuditTextMethod.Invoke(null, [jwt, "Issued access token."])
                ?? throw new AssertionException("Expected audit text."));

            Assert.That(auditText, Does.StartWith("Issued access token."));
            Assert.That(TryGetAuditValue(auditText, "access_jti", out string? jti), Is.True);
            Assert.That(Guid.TryParse(jti, out _), Is.True);
            Assert.That(TryGetAuditValue(auditText, "access_expires", out string? accessExpires), Is.True);
            Assert.That(DateTimeOffset.TryParse(accessExpires, out _), Is.True);
        }

        [Test]
        public void BuildTokenPairAuditText_IncludesJwtIdAccessExpiryAndRefreshExpiry()
        {
            JwtWriter jwtWriter = CreateJwtWriter();
            UiUser user = CreateUser();
            TokenPair tokenPair = new()
            {
                AccessToken = jwtWriter.CreateJWT(user, TimeSpan.FromMinutes(5)),
                RefreshTokenExpires = DateTime.UtcNow.AddHours(12)
            };

            string auditText = (string)(BuildTokenPairAuditTextMethod.Invoke(null, [tokenPair, "Issued token pair."])
                ?? throw new AssertionException("Expected audit text."));

            Assert.That(auditText, Does.StartWith("Issued token pair."));
            Assert.That(TryGetAuditValue(auditText, "access_jti", out string? jti), Is.True);
            Assert.That(Guid.TryParse(jti, out _), Is.True);
            Assert.That(TryGetAuditValue(auditText, "access_expires", out string? accessExpires), Is.True);
            Assert.That(DateTimeOffset.TryParse(accessExpires, out _), Is.True);
            Assert.That(TryGetAuditValue(auditText, "refresh_expires", out string? refreshExpires), Is.True);
            Assert.That(DateTimeOffset.TryParse(refreshExpires, out _), Is.True);
        }

        private static JwtWriter CreateJwtWriter()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey signingKey = new(rsa.ExportParameters(true));
            return new JwtWriter(signingKey);
        }

        private static UiUser CreateUser()
        {
            return new UiUser
            {
                Name = "audit-user",
                DbId = 42,
                Dn = "cn=audit-user,dc=example,dc=com",
                Roles = ["reporter"]
            };
        }

        private static bool TryGetAuditValue(string auditText, string key, out string? value)
        {
            Match match = Regex.Match(auditText, $@"\b{Regex.Escape(key)}=(?<value>[^,]+)");
            if (match.Success)
            {
                value = match.Groups["value"].Value.Trim();
                return true;
            }

            value = null;
            return false;
        }
    }
}
