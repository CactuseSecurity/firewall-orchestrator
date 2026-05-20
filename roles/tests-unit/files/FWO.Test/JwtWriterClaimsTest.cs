using FWO.Data;
using FWO.Middleware.Server;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

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
        public void SetClaimsAddsJwtIdClaim()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = ["reporter"]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);
            Claim? jtiClaim = claimsIdentity.FindFirst("jti");

            Assert.That(jtiClaim, Is.Not.Null);
            Assert.That(Guid.TryParse(jtiClaim!.Value, out _), Is.True);
        }

        private static ClaimsIdentity InvokeSetClaims(UiUser user)
        {
            MethodInfo? method = typeof(JwtWriter).GetMethod("SetClaims", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (ClaimsIdentity)(method!.Invoke(null, [user]) ?? throw new AssertionException("SetClaims returned null."));
        }
    }
}
