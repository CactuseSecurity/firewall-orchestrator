using FWO.Basics;
using FWO.Middleware.Server;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    public class InternalServiceJwtProviderTest
    {
        [Test]
        public void CreateJWTMiddlewareServer_WhenLifetimeOverrideProvided_UsesRequestedLifetime()
        {
            using RSA rsa = RSA.Create(2048);
            JwtWriter jwtWriter = new(new RsaSecurityKey(rsa.ExportParameters(true)));
            TimeSpan requestedLifetime = TimeSpan.FromMinutes(10);
            DateTime utcNow = DateTime.UtcNow;

            string jwt = jwtWriter.CreateJWTMiddlewareServer(requestedLifetime);

            JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
            TimeSpan actualLifetime = token.ValidTo - utcNow;

            Assert.That(actualLifetime, Is.GreaterThan(requestedLifetime - TimeSpan.FromSeconds(10)));
            Assert.That(actualLifetime, Is.LessThan(requestedLifetime + TimeSpan.FromSeconds(10)));
            Assert.That(token.Claims.FirstOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value, Is.EqualTo(Roles.MiddlewareServer));
        }

        [Test]
        public void GetBearerToken_UsesCachedTokenUntilInvalidated()
        {
            int mintedTokenCount = 0;
            InternalServiceJwtProvider provider = new(
                _ => CreateTokenWithRole(Roles.MiddlewareServer, ref mintedTokenCount),
                TimeSpan.FromHours(1),
                TimeSpan.FromMinutes(5));

            string firstToken = provider.GetBearerToken();
            string secondToken = provider.GetBearerToken();

            Assert.That(secondToken, Is.EqualTo(firstToken));
            Assert.That(mintedTokenCount, Is.EqualTo(1));

            provider.Invalidate();
            string thirdToken = provider.GetBearerToken();

            Assert.That(thirdToken, Is.Not.EqualTo(firstToken));
            Assert.That(mintedTokenCount, Is.EqualTo(2));
        }

        private static string CreateTokenWithRole(string role, ref int mintedTokenCount)
        {
            mintedTokenCount++;
            using RSA rsa = RSA.Create(2048);
            SigningCredentials credentials = new(new RsaSecurityKey(rsa.ExportParameters(true)), SecurityAlgorithms.RsaSha256);

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                claims:
                [
                    new System.Security.Claims.Claim("x-hasura-default-role", role)
                ],
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
