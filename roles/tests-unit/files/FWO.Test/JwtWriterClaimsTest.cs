using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    public class JwtWriterClaimsTest
    {
        // apache rejects a single request header field larger than this by default (LimitRequestFieldSize)
        private const int kApacheHeaderFieldLimit = 8190;

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
        public void SetClaimsAddsWorkflowVisibilityGroupClaims()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = ["reporter"],
                WorkflowVisibilityGroupIds = [2, 4, 9]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-workflow-visibility-groups")?.Value, Is.EqualTo("{2,4,9}"));
        }

        [Test]
        public void SetClaimsOmitsGroupDnsToKeepTokenSmall()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = ["reporter"],
                Groups = ["cn=approver,ou=groups,dc=example,dc=com", "cn=auditor,ou=groups,dc=example,dc=com"]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.Multiple(() =>
            {
                Assert.That(claimsIdentity.FindFirst("x-hasura-groups"), Is.Null);
                Assert.That(claimsIdentity.Claims.Any(claim => claim.Value.Contains("ou=groups,dc=example,dc=com")), Is.False);
            });
        }

        [Test]
        public void CreateJwtStaysBelowWebServerHeaderLimitForManyGroups()
        {
            // apache rejects a single request header field above 8190 bytes by default. The jwt travels in
            // "Authorization: Bearer ...", so a user with many group memberships must not blow that budget.
            UiUser user = new()
            {
                Name = "test-user",
                Dn = "cn=test-user,ou=users,ou=operator,dc=fworch,dc=internal",
                Roles = ["reporter", "modeller", "requester"],
                Groups = [.. Enumerable.Range(0, 500).Select(index => $"cn=app-owner-group-{index:D4},ou=groups,ou=operator,dc=fworch,dc=internal")]
            };

            string jwt = new JwtWriter(new RsaSecurityKey(RSA.Create(2048))).CreateJWT(user, TimeSpan.FromHours(1));

            Assert.That("Authorization: Bearer ".Length + jwt.Length, Is.LessThan(kApacheHeaderFieldLimit));
        }

        [TestCase(Roles.Admin, Roles.Admin)]
        [TestCase(Roles.Auditor, Roles.Auditor)]
        [TestCase(Roles.FwAdmin, Roles.FwAdmin)]
        [TestCase(Roles.ReporterViewAll, Roles.ReporterViewAll)]
        [TestCase(Roles.Reporter, Roles.Reporter)]
        [TestCase(Roles.Recertifier, Roles.Recertifier)]
        [TestCase(Roles.Modeller, Roles.Modeller)]
        public void SetClaimsPicksTheHighestRankedRoleAsDefaultRole(string role, string expectedDefaultRole)
        {
            // every user also holds the lowest ranked role, so the ranking decides
            UiUser user = new()
            {
                Name = "test-user",
                Roles = [Roles.Requester, role]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-default-role")?.Value, Is.EqualTo(expectedDefaultRole));
        }

        [Test]
        public void SetClaimsFallsBackToTheFirstRoleWhenNoneIsRanked()
        {
            UiUser user = new()
            {
                Name = "test-user",
                Roles = [Roles.Requester, Roles.Approver]
            };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-default-role")?.Value, Is.EqualTo(Roles.Requester));
        }

        [Test]
        public void SetClaimsLeavesDefaultRoleEmptyForUserWithoutRoles()
        {
            UiUser user = new() { Name = "test-user", Roles = [] };

            ClaimsIdentity claimsIdentity = InvokeSetClaims(user);

            Assert.That(claimsIdentity.FindFirst("x-hasura-default-role")?.Value, Is.Empty);
        }

        [Test]
        public void CreateJwtForInternalRolesIssuesSingleRoleTokens()
        {
            JwtWriter writer = new(new RsaSecurityKey(RSA.Create(2048)));

            string middlewareToken = writer.CreateJWTMiddlewareServer(TimeSpan.FromMinutes(5));
            string reporterToken = writer.CreateJWTReporterViewall(TimeSpan.FromMinutes(5));

            Assert.Multiple(() =>
            {
                Assert.That(ReadDefaultRole(middlewareToken), Is.EqualTo(Roles.MiddlewareServer));
                Assert.That(ReadDefaultRole(reporterToken), Is.EqualTo(Roles.ReporterViewAll));
            });
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

        private static string? ReadDefaultRole(string jwt)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims
                .FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value;
        }

        private static ClaimsIdentity InvokeSetClaims(UiUser user)
        {
            MethodInfo? method = typeof(JwtWriter).GetMethod("SetClaims", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (ClaimsIdentity)(method!.Invoke(null, [user]) ?? throw new AssertionException("SetClaims returned null."));
        }
    }
}
