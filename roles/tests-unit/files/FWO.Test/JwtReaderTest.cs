using FWO.Config.File;
using FWO.Middleware.Client;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    public class JwtReaderTest
    {
        private static readonly FieldInfo JwtPublicKeyField = typeof(ConfigFile).GetField("jwtPublicKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(ConfigFile).FullName, "jwtPublicKey");

        private RsaSecurityKey? originalJwtPublicKey;

        [SetUp]
        public void Setup()
        {
            originalJwtPublicKey = (RsaSecurityKey?)JwtPublicKeyField.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            JwtPublicKeyField.SetValue(null, originalJwtPublicKey);
        }

        [Test]
        public async Task ValidateToken_WhenJwtIsExpired_ShouldReturnExpiredStatus()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, publicKey);

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                expires: DateTime.UtcNow.AddMinutes(-5),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256));

            string jwtString = new JwtSecurityTokenHandler().WriteToken(token);

            JwtReader jwtReader = new(jwtString);
            JwtValidationResult result = await jwtReader.ValidateToken();

            Assert.That(result.Status, Is.EqualTo(JwtValidationStatus.Expired));
        }
    }
}
