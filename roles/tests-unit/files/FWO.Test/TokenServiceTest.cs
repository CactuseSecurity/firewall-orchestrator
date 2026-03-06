using NUnit.Framework;
using FWO.Ui.Services;
using FWO.Data.Middleware;
using FWO.Test.Mocks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using FWO.Middleware.Client;

namespace FWO.Test
{
    /// <summary>
    /// Unit tests for TokenService using custom mock implementations.
    /// </summary>
    [TestFixture]
    public class TokenServiceTest
    {
        private MockMiddlewareClient? mockMiddlewareClient;
        private MockProtectedSessionStorage? mockSessionStorage;
        private TokenService? tokenService;

        private const string TEST_ACCESS_TOKEN = "test.access.token";
        private const string TEST_REFRESH_TOKEN = "test_refresh_token_12345";

        [SetUp]
        public void Setup()
        {
            mockMiddlewareClient = new MockMiddlewareClient();
            mockSessionStorage = new MockProtectedSessionStorage();
            tokenService = new TokenService(mockMiddlewareClient, mockSessionStorage);
        }

        [TearDown]
        public void TearDown()
        {
            mockSessionStorage?.Clear();
            mockMiddlewareClient?.Reset();
        }

        #region SetTokenPair Tests

        [Test]
        public async Task SetTokenPair_ShouldStoreTokenPairInSessionStorage()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();

            // Act
            await tokenService!.SetTokenPair(tokenPair);

            // Assert
            Assert.That(mockSessionStorage!.ContainsKey("token_pair"), Is.True);
        }

        [Test]
        public async Task SetTokenPair_ShouldUpdateExistingTokenPair()
        {
            // Arrange
            TokenPair oldTokenPair = CreateTestTokenPair();
            TokenPair newTokenPair = new()
            {
                AccessToken = "new.access.token",
                RefreshToken = "new_refresh_token",
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(14)
            };

            // Act
            await tokenService!.SetTokenPair(oldTokenPair);
            await tokenService.SetTokenPair(newTokenPair);

            // Assert
            string? storedToken = await tokenService.GetAccessTokenAsync();
            Assert.That(storedToken, Is.EqualTo("new.access.token"));
        }

        #endregion

        #region GetAccessTokenAsync Tests

        [Test]
        public async Task GetAccessTokenAsync_WhenNoTokenExists_ShouldReturnNull()
        {
            // Act
            string? result = await tokenService!.GetAccessTokenAsync();

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetAccessTokenAsync_WhenTokenExists_ShouldReturnAccessToken()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            string? result = await tokenService.GetAccessTokenAsync();

            // Assert
            Assert.That(result, Is.EqualTo(TEST_ACCESS_TOKEN));
        }

        [Test]
        public async Task GetAccessTokenAsync_WhenCalledMultipleTimes_ShouldReturnSameToken()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            string? result1 = await tokenService.GetAccessTokenAsync();
            string? result2 = await tokenService.GetAccessTokenAsync();
            string? result3 = await tokenService.GetAccessTokenAsync();

            // Assert
            Assert.That(result1, Is.EqualTo(result2));
            Assert.That(result2, Is.EqualTo(result3));
            Assert.That(result1, Is.EqualTo(TEST_ACCESS_TOKEN));
        }

        #endregion

        #region IsAccessTokenExpired Tests

        [Test]
        public async Task IsAccessTokenExpired_WhenNoTokenExists_ShouldReturnTrue()
        {
            // Act
            bool result = await tokenService!.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsAccessTokenExpired_WhenTokenIsNull_ShouldReturnTrue()
        {
            // Arrange
            TokenPair tokenPair = new()
            {
                AccessToken = "",
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsAccessTokenExpired_WhenTokenIsValidAndNotExpired_ShouldReturnFalse()
        {
            // Arrange
            string validToken = GenerateJwtToken(DateTime.UtcNow.AddHours(2));
            TokenPair tokenPair = new()
            {
                AccessToken = validToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsAccessTokenExpired_WhenTokenExpiresInLessThanOneMinute_ShouldReturnTrue()
        {
            // Arrange
            string expiringToken = GenerateJwtToken(DateTime.UtcNow.AddSeconds(30));
            TokenPair tokenPair = new()
            {
                AccessToken = expiringToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddSeconds(30),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsAccessTokenExpired_WhenTokenIsAlreadyExpired_ShouldReturnTrue()
        {
            // Arrange
            string expiredToken = GenerateJwtToken(DateTime.UtcNow.AddHours(-1));
            TokenPair tokenPair = new()
            {
                AccessToken = expiredToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsAccessTokenExpired_WhenTokenIsInvalid_ShouldReturnTrue()
        {
            // Arrange
            TokenPair tokenPair = new()
            {
                AccessToken = "invalid.jwt.token",
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.IsAccessTokenExpired();

            // Assert
            Assert.That(result, Is.True);
        }

        #endregion

        #region RefreshAccessTokenAsync Tests

        [Test]
        public async Task RefreshAccessTokenAsync_WhenNoTokenPairExists_ShouldReturnFalse()
        {
            // Act
            bool result = await tokenService!.RefreshAccessTokenAsync();

            // Assert
            Assert.That(result, Is.False);
            Assert.That(mockMiddlewareClient!.RefreshTokenCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RefreshAccessTokenAsync_WhenNoRefreshTokenExists_ShouldReturnFalse()
        {
            // Arrange
            TokenPair tokenPair = new()
            {
                AccessToken = TEST_ACCESS_TOKEN,
                RefreshToken = "",
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.RefreshAccessTokenAsync();

            // Assert
            Assert.That(result, Is.False);
            Assert.That(mockMiddlewareClient!.RefreshTokenCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RefreshAccessTokenAsync_WhenTokenIsNotExpired_ShouldReturnTrueWithoutRefreshing()
        {
            // Arrange
            string validToken = GenerateJwtToken(DateTime.UtcNow.AddHours(2));
            TokenPair tokenPair = new()
            {
                AccessToken = validToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            bool result = await tokenService.RefreshAccessTokenAsync();

            // Assert
            Assert.That(result, Is.True);
            Assert.That(mockMiddlewareClient!.RefreshTokenCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RefreshAccessTokenAsync_WhenTokenIsExpiredAndRefreshSucceeds_ShouldReturnTrueAndUpdateToken()
        {
            // Arrange
            string expiredToken = GenerateJwtToken(DateTime.UtcNow.AddHours(-1));
            TokenPair oldTokenPair = new()
            {
                AccessToken = expiredToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            string newAccessToken = GenerateJwtToken(DateTime.UtcNow.AddHours(2));
            TokenPair newTokenPair = new()
            {
                AccessToken = newAccessToken,
                RefreshToken = "new_refresh_token",
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(14)
            };

            await tokenService!.SetTokenPair(oldTokenPair);
            mockMiddlewareClient!.NextRefreshTokenResponse = newTokenPair;
            mockMiddlewareClient.ShouldRefreshSucceed = true;

            // Act
            bool result = await tokenService.RefreshAccessTokenAsync();

            // Assert
            Assert.That(result, Is.True);
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
            Assert.That(mockMiddlewareClient.LastRefreshRequest, Is.Not.Null);
            Assert.That(mockMiddlewareClient.LastRefreshRequest!.RefreshToken, Is.EqualTo(TEST_REFRESH_TOKEN));

            string? storedToken = await tokenService.GetAccessTokenAsync();
            Assert.That(storedToken, Is.EqualTo(newAccessToken));
        }

        [Test]
        public async Task RefreshAccessTokenAsync_WhenTokenIsExpiredAndRefreshFails_ShouldReturnFalse()
        {
            // Arrange
            string expiredToken = GenerateJwtToken(DateTime.UtcNow.AddHours(-1));
            TokenPair tokenPair = new()
            {
                AccessToken = expiredToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            await tokenService!.SetTokenPair(tokenPair);
            mockMiddlewareClient!.ShouldRefreshSucceed = false;

            // Act
            bool result = await tokenService.RefreshAccessTokenAsync();

            // Assert
            Assert.That(result, Is.False);
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RefreshAccessTokenAsync_WhenCalledConcurrently_ShouldOnlyRefreshOnce()
        {
            // Arrange
            string expiredToken = GenerateJwtToken(DateTime.UtcNow.AddHours(-1));
            TokenPair tokenPair = new()
            {
                AccessToken = expiredToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            string newAccessToken = GenerateJwtToken(DateTime.UtcNow.AddHours(2));
            TokenPair newTokenPair = new()
            {
                AccessToken = newAccessToken,
                RefreshToken = "new_refresh_token",
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(14)
            };

            await tokenService!.SetTokenPair(tokenPair);
            mockMiddlewareClient!.NextRefreshTokenResponse = newTokenPair;
            mockMiddlewareClient.ShouldRefreshSucceed = true;

            // Act - call refresh token concurrently
            Task<bool> task1 = tokenService.RefreshAccessTokenAsync();
            Task<bool> task2 = tokenService.RefreshAccessTokenAsync();
            Task<bool> task3 = tokenService.RefreshAccessTokenAsync();

            await Task.WhenAll(task1, task2, task3);

            // Assert - only one refresh should have occurred due to semaphore
            Assert.That(mockMiddlewareClient.RefreshTokenCallCount, Is.LessThanOrEqualTo(1));
        }

        #endregion

        #region RevokeTokens Tests

        [Test]
        public async Task RevokeTokens_WhenNoTokenPairExists_ShouldNotCallMiddleware()
        {
            // Act
            await tokenService!.RevokeTokens();

            // Assert
            Assert.That(mockMiddlewareClient!.RevokeRefreshTokenCallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RevokeTokens_WhenTokenPairExists_ShouldRevokeAndClearStorage()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);
            mockMiddlewareClient!.ShouldRevokeSucceed = true;

            // Act
            await tokenService.RevokeTokens();

            // Assert
            Assert.That(mockMiddlewareClient.RevokeRefreshTokenCallCount, Is.EqualTo(1));
            Assert.That(mockMiddlewareClient.LastRevokeRequest, Is.Not.Null);
            Assert.That(mockMiddlewareClient.LastRevokeRequest!.RefreshToken, Is.EqualTo(TEST_REFRESH_TOKEN));
            Assert.That(mockSessionStorage!.ContainsKey("token_pair"), Is.False);
        }

        [Test]
        public async Task RevokeTokens_AfterRevoke_ShouldClearTokenPair()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            await tokenService.RevokeTokens();

            // Assert
            string? accessToken = await tokenService.GetAccessTokenAsync();
            Assert.That(accessToken, Is.Null);
        }

        [Test]
        public async Task RevokeTokens_WhenCalledMultipleTimes_ShouldOnlyRevokeOnce()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);

            // Act
            await tokenService.RevokeTokens();
            await tokenService.RevokeTokens();
            await tokenService.RevokeTokens();

            // Assert
            Assert.That(mockMiddlewareClient!.RevokeRefreshTokenCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RevokeTokens_ShouldResetInitializationState()
        {
            // Arrange
            TokenPair tokenPair = CreateTestTokenPair();
            await tokenService!.SetTokenPair(tokenPair);

            // Verify token is accessible before revoke
            string? tokenBefore = await tokenService.GetAccessTokenAsync();
            Assert.That(tokenBefore, Is.Not.Null);

            // Act
            await tokenService.RevokeTokens();

            // Set a new token pair
            TokenPair newTokenPair = new()
            {
                AccessToken = "new.access.token",
                RefreshToken = "new_refresh_token",
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService.SetTokenPair(newTokenPair);

            // Assert - should be able to get the new token
            string? tokenAfter = await tokenService.GetAccessTokenAsync();
            Assert.That(tokenAfter, Is.EqualTo("new.access.token"));
        }

        #endregion

        #region Integration Tests

        [Test]
        public async Task FullTokenLifecycle_SetRefreshRevoke_ShouldWorkCorrectly()
        {
            // Arrange
            string initialToken = GenerateJwtToken(DateTime.UtcNow.AddHours(1));
            TokenPair initialTokenPair = new()
            {
                AccessToken = initialToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            // Act & Assert - Set
            await tokenService!.SetTokenPair(initialTokenPair);
            string? token1 = await tokenService.GetAccessTokenAsync();
            Assert.That(token1, Is.EqualTo(initialToken));

            // Act & Assert - Refresh
            string expiredToken = GenerateJwtToken(DateTime.UtcNow.AddHours(-1));
            TokenPair expiredTokenPair = new()
            {
                AccessToken = expiredToken,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(-1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
            await tokenService.SetTokenPair(expiredTokenPair);

            string refreshedToken = GenerateJwtToken(DateTime.UtcNow.AddHours(2));
            TokenPair refreshedTokenPair = new()
            {
                AccessToken = refreshedToken,
                RefreshToken = "new_refresh",
                AccessTokenExpires = DateTime.UtcNow.AddHours(2),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(14)
            };
            mockMiddlewareClient!.NextRefreshTokenResponse = refreshedTokenPair;

            bool refreshResult = await tokenService.RefreshAccessTokenAsync();
            Assert.That(refreshResult, Is.True);

            string? token2 = await tokenService.GetAccessTokenAsync();
            Assert.That(token2, Is.EqualTo(refreshedToken));

            // Act & Assert - Revoke
            await tokenService.RevokeTokens();
            string? token3 = await tokenService.GetAccessTokenAsync();
            Assert.That(token3, Is.Null);
            Assert.That(mockMiddlewareClient.RevokeRefreshTokenCallCount, Is.EqualTo(1));
        }

        #endregion

        #region Helper Methods

        private static TokenPair CreateTestTokenPair()
        {
            return new TokenPair
            {
                AccessToken = TEST_ACCESS_TOKEN,
                RefreshToken = TEST_REFRESH_TOKEN,
                AccessTokenExpires = DateTime.UtcNow.AddHours(1),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };
        }

        private static string GenerateJwtToken(DateTime expiresAt)
        {
            SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes("ThisIsATestSecretKeyForJwtTokenGeneration123456"));
            SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

            Claim[] claims =
            [
                new Claim(JwtRegisteredClaimNames.Sub, "test_user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ];

            JwtSecurityToken token = new(
                issuer: "test_issuer",
                audience: "test_audience",
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        #endregion
    }
}
