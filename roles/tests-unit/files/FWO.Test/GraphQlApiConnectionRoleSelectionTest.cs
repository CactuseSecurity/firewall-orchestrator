using FWO.Api.Client;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    public class GraphQlApiConnectionRoleSelectionTest
    {
        [Test]
        public void SetBestRoleUsesRoleClaims()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Role, "recertifier")
            ], "test", ClaimTypes.Name, ClaimTypes.Role));

            connection.SetBestRole(user, ["modeller", "recertifier", "admin"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("recertifier"));
        }

        [Test]
        public void SetBestRoleParsesHasuraAllowedRolesJsonArrayClaim()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("x-hasura-allowed-roles", "[\"auditor\",\"admin\"]")
            ], "test"));

            connection.SetBestRole(user, ["modeller", "admin"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("admin"));
        }

        [Test]
        public void SetBestRoleParsesNamespacedHasuraAllowedRolesClaim()
        {
            using GraphQlApiConnection connection = new("http://localhost");
            ClaimsPrincipal user = new(new ClaimsIdentity(
            [
                new Claim("https://hasura.io/jwt/claims/x-hasura-allowed-roles", "[\"auditor\",\"modeller\"]")
            ], "test"));

            connection.SetBestRole(user, ["recertifier", "modeller"]);

            Assert.That(connection.GetActRole(), Is.EqualTo("modeller"));
        }
    }
}
