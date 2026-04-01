using FWO.Data;
using FWO.Middleware.Server;
using FWO.Basics;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    public class JwtWriterClaimsTest
    {
        [Test]
        public void SetClaimsAddsVisibleScopeClaimsWhenTenantIsMissing()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = ["reporter"]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-visible-managements")?.Value, Is.EqualTo("{}"));
            Assert.That(claimsIdentity.FindFirst("x-hasura-visible-devices")?.Value, Is.EqualTo("{}"));
        }

        [Test]
        public void SetClaimsAddsVisibleScopeClaimsFromTenant()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = ["reporter"],
                Tenant = new Tenant
                {
                    Id = 7,
                    VisibleManagementIds = [3, 9],
                    VisibleGatewayIds = [5]
                }
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-tenant-id")?.Value, Is.EqualTo("7"));
            Assert.That(claimsIdentity.FindFirst("x-hasura-visible-managements")?.Value, Is.EqualTo("{3,9}"));
            Assert.That(claimsIdentity.FindFirst("x-hasura-visible-devices")?.Value, Is.EqualTo("{5}"));
        }

        [Test]
        public async Task CreateJWT_WhenAnonymousAndLifetimeOverrideProvided_UsesRequestedLifetime()
        {
            using RSA rsa = RSA.Create(2048);
            JwtWriter jwtWriter = new(new RsaSecurityKey(rsa.ExportParameters(true)));
            TimeSpan requestedLifetime = TimeSpan.FromMinutes(15);
            DateTime utcNow = DateTime.UtcNow;

            string jwt = await jwtWriter.CreateJWT(null, requestedLifetime);

            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            TimeSpan actualLifetime = token.ValidTo - utcNow;

            Assert.That(actualLifetime, Is.GreaterThan(requestedLifetime - TimeSpan.FromSeconds(10)));
            Assert.That(actualLifetime, Is.LessThan(requestedLifetime + TimeSpan.FromSeconds(10)));
            Assert.That(token.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value, Is.EqualTo(Roles.Anonymous));
        }

        private static ClaimsIdentity InvokeSetClaims(UiUser user)
        {
            MethodInfo? method = typeof(JwtWriter).GetMethod("SetClaims", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (ClaimsIdentity)(method!.Invoke(null, [user]) ?? throw new AssertionException("SetClaims returned null."));
        }
    }
}
