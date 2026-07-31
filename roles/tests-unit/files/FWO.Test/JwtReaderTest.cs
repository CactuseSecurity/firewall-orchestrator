using FWO.Config.File;
using FWO.Middleware.Client;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
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

        [Test]
        public async Task ValidateToken_WhenJwtIsValid_ShouldExposeClaimsAndRoles()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(rsa.ExportParameters(true));
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, publicKey);

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                claims:
                [
                    new Claim("role", "reporter"),
                    new Claim("x-hasura-allowed-roles", "reporter"),
                    new Claim("x-hasura-allowed-roles", "admin")
                ],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256));

            string jwtString = new JwtSecurityTokenHandler().WriteToken(token);

            JwtReader jwtReader = new(jwtString);
            JwtValidationResult result = await jwtReader.ValidateToken();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(JwtValidationStatus.Success));
                Assert.That(jwtReader.ContainsRole("reporter"), Is.True);
                Assert.That(jwtReader.ContainsAllowedRole("admin"), Is.True);
                Assert.That(jwtReader.GetRole(), Is.EqualTo("reporter"));
                Assert.That(jwtReader.GetClaims().Select(claim => claim.Type), Does.Contain("role"));
            });
        }

        [Test]
        public void JwtHelpers_ThrowBeforeValidation()
        {
            using RSA rsa = RSA.Create(2048);
            RsaSecurityKey publicKey = new(rsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, publicKey);
            JwtReader jwtReader = new("not-a-jwt");

            Assert.Multiple(() =>
            {
                Assert.Throws<ArgumentException>(() => jwtReader.ContainsRole("admin"));
                Assert.Throws<ArgumentException>(() => jwtReader.ContainsAllowedRole("admin"));
                Assert.Throws<ArgumentException>(() => jwtReader.GetClaims());
                Assert.Throws<ArgumentException>(() => jwtReader.GetRole());
            });
        }

        [Test]
        public async Task ValidateToken_WhenSignatureIsInvalid_ShouldReturnInvalidStatus()
        {
            using RSA signingRsa = RSA.Create(2048);
            using RSA validationRsa = RSA.Create(2048);
            RsaSecurityKey privateKey = new(signingRsa.ExportParameters(true));
            RsaSecurityKey validationPublicKey = new(validationRsa.ExportParameters(false));
            JwtPublicKeyField.SetValue(null, validationPublicKey);

            JwtSecurityToken token = new(
                issuer: FWO.Basics.JwtConstants.Issuer,
                audience: FWO.Basics.JwtConstants.Audience,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256));

            string jwtString = new JwtSecurityTokenHandler().WriteToken(token);

            JwtReader jwtReader = new(jwtString);
            JwtValidationResult result = await jwtReader.ValidateToken();

            Assert.That(result.Status, Is.EqualTo(JwtValidationStatus.Invalid));
        }
    }
}
