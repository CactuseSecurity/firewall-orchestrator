using FWO.Data.Middleware;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FWO.Test.DataGenerators
{
    /// <summary>
    /// Helper utilities for authentication integration tests
    /// </summary>
    public static class AuthTestHelpers
    {
        /// <summary>
        /// Creates invalid test credentials
        /// </summary>
        public static AuthenticationTokenGetParameters CreateInvalidCredentials()
        {
            return new AuthenticationTokenGetParameters
            {
                Username = "invaliduser",
                Password = "wrongpassword"
            };
        }

        /// <summary>
        /// Asserts that HTTP response is successful
        /// </summary>
        public static void AssertSuccessResponse(HttpResponseMessage response)
        {
            Assert.That(response.IsSuccessStatusCode, Is.True,
                $"Expected success status code, got {response.StatusCode}. Content: {response.Content.ReadAsStringAsync().Result}");
        }

        /// <summary>
        /// Validates that a TokenPair has all required fields populated
        /// </summary>
        public static void AssertValidTokenPair(TokenPair? tokenPair)
        {
            Assert.That(tokenPair, Is.Not.Null, "TokenPair should not be null");
            Assert.That(tokenPair!.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken should not be null or empty");
            Assert.That(tokenPair.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken should not be null or empty");
            Assert.That(tokenPair.AccessTokenExpires, Is.Not.EqualTo(default(DateTime)), "AccessTokenExpires should be set");
            Assert.That(tokenPair.RefreshTokenExpires, Is.Not.EqualTo(default(DateTime)), "RefreshTokenExpires should be set");
        }

        /// <summary>
        /// Validates JWT token structure (3 parts: header.payload.signature)
        /// </summary>
        public static void AssertJwtStructure(string jwt, JwtSecurityTokenHandler tokenHandler)
        {
            var parts = jwt.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3), "JWT should have 3 parts (header.payload.signature)");

            // Validate it's a parseable JWT
            JwtSecurityToken token = tokenHandler.ReadJwtToken(jwt);
            Assert.That(token, Is.Not.Null, "JWT should be parseable");
            Assert.That(token.Claims, Is.Not.Empty, "JWT should contain claims");
        }

        /// <summary>
        /// Validates that JWT contains expected claims for the user
        /// </summary>
        public static void AssertTokenClaims(string jwt, string expectedUsername, JwtSecurityTokenHandler tokenHandler)
        {
            JwtSecurityToken token = tokenHandler.ReadJwtToken(jwt);

            // Check for required claims
            Claim? nameClaim = token.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name");
            Assert.That(nameClaim, Is.Not.Null, "JWT should contain name claim");

            if (!string.IsNullOrEmpty(expectedUsername))
            {
                Assert.That(nameClaim!.Value, Is.EqualTo(expectedUsername), "JWT should contain correct username");
            }

            // Check for Hasura claims (specific to your implementation)
            Claim? hasuraRolesClaim = token.Claims.FirstOrDefault(c => c.Type == "x-hasura-allowed-roles");
            Assert.That(hasuraRolesClaim, Is.Not.Null, "JWT should contain hasura roles claim");

            Claim? defaultRoleClaim = token.Claims.FirstOrDefault(c => c.Type == "x-hasura-default-role");
            Assert.That(defaultRoleClaim, Is.Not.Null, "JWT should contain default role claim");
        }

        /// <summary>
        /// Validates that token rotation occurred (new tokens are different from old)
        /// </summary>
        public static void AssertTokenRotation(TokenPair oldTokens, TokenPair newTokens)
        {
            Assert.That(newTokens.AccessToken, Is.Not.EqualTo(oldTokens.AccessToken),
                "New access token should be different from old token");
            Assert.That(newTokens.RefreshToken, Is.Not.EqualTo(oldTokens.RefreshToken),
                "New refresh token should be different from old token (token rotation)");
        }

        /// <summary>
        /// Validates token expiration hierarchy (refresh token lives longer than access token)
        /// </summary>
        public static void AssertTokenExpirationHierarchy(TokenPair tokens)
        {
            Assert.That(tokens.AccessTokenExpires, Is.GreaterThan(DateTime.UtcNow),
                "Access token should not be expired");
            Assert.That(tokens.RefreshTokenExpires, Is.GreaterThan(tokens.AccessTokenExpires),
                "Refresh token should expire after access token");

            // Refresh token should have longer lifetime than access token
            TimeSpan accessLifetime = tokens.AccessTokenExpires - DateTime.UtcNow;
            TimeSpan refreshLifetime = tokens.RefreshTokenExpires - DateTime.UtcNow;

            Assert.That(refreshLifetime, Is.GreaterThan(accessLifetime),
                "Refresh token should have longer lifetime than access token");
        }
    }
}
