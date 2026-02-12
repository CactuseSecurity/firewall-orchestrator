using FWO.Ui.Auth;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    public class JwtClaimParserTest
    {
        [Test]
        public void ExtractStringClaimValuesParsesJsonArrayClaims()
        {
            List<Claim> claims =
            [
                new("x-hasura-allowed-roles", "[\"admin\",\"modeller\"]")
            ];

            List<string> roles = JwtClaimParser.ExtractStringClaimValues(claims, "x-hasura-allowed-roles");

            Assert.That(roles, Is.EquivalentTo(new[] { "admin", "modeller" }));
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

            Assert.That(owners, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5 }));
        }
    }
}
