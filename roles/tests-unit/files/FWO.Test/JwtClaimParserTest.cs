using FWO.Ui.Auth;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    public class JwtClaimParserTest
    {
        private static readonly string[] kExpectedAuditorAndRecertifierRoles = ["auditor", "recertifier"];
        private static readonly int[] kExpectedOwnerIds = [1, 2, 3, 4, 5];
        private static readonly string[] kExpectedAdminAndModellerRoles = ["admin", "modeller"];

        [Test]
        public void ExtractStringClaimValuesParsesJsonArrayClaims()
        {
            List<Claim> claims =
            [
                new("x-hasura-allowed-roles", "[\"admin\",\"modeller\"]")
            ];

            List<string> roles = JwtClaimParser.ExtractStringClaimValues(claims, "x-hasura-allowed-roles");

            Assert.That(roles, Is.EquivalentTo(kExpectedAdminAndModellerRoles));
        }

        [Test]
        public void ExtractIntClaimValuesParsesBraceAndJsonArrayFormats()
        {
            List<Claim> claims =
            [
                new("x-hasura-editable-owners", "{ 1,2, 3 }"),
                new("x-hasura-editable-owners", "[4,5]")
            ];

            List<int> owners = JwtClaimParser.ExtractIntClaimValues(claims, "x-hasura-editable-owners");

            Assert.That(owners, Is.EquivalentTo(kExpectedOwnerIds));
        }

        [Test]
        public void ExtractStringClaimValuesAcceptsNamespacedClaimTypes()
        {
            List<Claim> claims =
            [
                new("https://hasura.io/jwt/claims/x-hasura-allowed-roles", "[\"auditor\",\"recertifier\"]")
            ];

            List<string> roles = JwtClaimParser.ExtractStringClaimValues(claims, "x-hasura-allowed-roles");

            Assert.That(roles, Is.EquivalentTo(kExpectedAuditorAndRecertifierRoles));
        }
    }
}
