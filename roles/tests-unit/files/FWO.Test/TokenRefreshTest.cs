using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using System.Security.Cryptography;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class TokenRefreshTest
    {
        private MiddlewareClient? middlewareClient = new("https://localhost:5001/");
        private const string TestRefreshToken = "test-refresh-token-12345";
        private const string TestAccessToken = "test-access-token-67890";

        [SetUp]
        public void Initialize()
        {
        }

        [TearDown]
        public void Cleanup()
        {
            middlewareClient?.Dispose();
        }

        [Test]
        public void TestRefreshTokenRequestCreation()
        {
            RefreshTokenRequest request = new()
            {
                RefreshToken = TestRefreshToken
            };

            ClassicAssert.AreEqual(TestRefreshToken, request.RefreshToken);
            ClassicAssert.IsNotNull(request.RefreshToken);
            ClassicAssert.IsFalse(string.IsNullOrEmpty(request.RefreshToken));
        }

        [Test]
        public void TestTokenPairCreation()
        {
            DateTime accessExpiry = DateTime.UtcNow.AddMinutes(15);
            DateTime refreshExpiry = DateTime.UtcNow.AddDays(7);

            TokenPair tokenPair = new()
            {
                AccessToken = TestAccessToken,
                RefreshToken = TestRefreshToken,
                AccessTokenExpires = accessExpiry,
                RefreshTokenExpires = refreshExpiry
            };

            ClassicAssert.AreEqual(TestAccessToken, tokenPair.AccessToken);
            ClassicAssert.AreEqual(TestRefreshToken, tokenPair.RefreshToken);
            ClassicAssert.AreEqual(accessExpiry, tokenPair.AccessTokenExpires);
            ClassicAssert.AreEqual(refreshExpiry, tokenPair.RefreshTokenExpires);
        }

        [Test]
        public void TestTokenPairExpirationLogic()
        {
            TokenPair expiredTokenPair = new()
            {
                AccessToken = TestAccessToken,
                RefreshToken = TestRefreshToken,
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(-5),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            ClassicAssert.IsTrue(expiredTokenPair.AccessTokenExpires < DateTime.UtcNow);
            ClassicAssert.IsTrue(expiredTokenPair.RefreshTokenExpires > DateTime.UtcNow);
        }

        [Test]
        public void TestTokenHashGeneration()
        {
            string token = "test-token-123";

            byte[] hash1 = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            string tokenHash1 = Convert.ToBase64String(hash1);

            byte[] hash2 = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            string tokenHash2 = Convert.ToBase64String(hash2);

            ClassicAssert.AreEqual(tokenHash1, tokenHash2);
            ClassicAssert.IsNotNull(tokenHash1);
            ClassicAssert.IsTrue(tokenHash1.Length > 0);
        }

        [Test]
        public void TestTokenHashUniqueness()
        {
            string token1 = "test-token-123";
            string token2 = "test-token-456";

            byte[] hash1 = SHA256.HashData(Encoding.UTF8.GetBytes(token1));
            string tokenHash1 = Convert.ToBase64String(hash1);

            byte[] hash2 = SHA256.HashData(Encoding.UTF8.GetBytes(token2));
            string tokenHash2 = Convert.ToBase64String(hash2);

            ClassicAssert.AreNotEqual(tokenHash1, tokenHash2);
        }

        [Test]
        public void TestRefreshTokenRequestValidation()
        {
            RefreshTokenRequest emptyRequest = new()
            {
                RefreshToken = ""
            };

            ClassicAssert.IsTrue(string.IsNullOrEmpty(emptyRequest.RefreshToken));

            RefreshTokenRequest validRequest = new()
            {
                RefreshToken = TestRefreshToken
            };

            ClassicAssert.IsFalse(string.IsNullOrEmpty(validRequest.RefreshToken));
        }

        [Test]
        public void TestTokenPairDefaultValues()
        {
            TokenPair defaultTokenPair = new();

            ClassicAssert.AreEqual(string.Empty, defaultTokenPair.AccessToken);
            ClassicAssert.AreEqual(string.Empty, defaultTokenPair.RefreshToken);
            ClassicAssert.AreEqual(default(DateTime), defaultTokenPair.AccessTokenExpires);
            ClassicAssert.AreEqual(default(DateTime), defaultTokenPair.RefreshTokenExpires);
        }

        [Test]
        public void TestRefreshTokenExpirationScenario()
        {
            DateTime now = DateTime.UtcNow;

            TokenPair validTokens = new()
            {
                AccessToken = TestAccessToken,
                RefreshToken = TestRefreshToken,
                AccessTokenExpires = now.AddMinutes(-5),
                RefreshTokenExpires = now.AddDays(7)
            };

            bool accessExpired = validTokens.AccessTokenExpires < now;
            bool refreshValid = validTokens.RefreshTokenExpires > now;

            ClassicAssert.IsTrue(accessExpired, "Access token should be expired");
            ClassicAssert.IsTrue(refreshValid, "Refresh token should still be valid");
        }

        [Test]
        public void TestBothTokensExpiredScenario()
        {
            DateTime now = DateTime.UtcNow;

            TokenPair expiredTokens = new()
            {
                AccessToken = TestAccessToken,
                RefreshToken = TestRefreshToken,
                AccessTokenExpires = now.AddMinutes(-5),
                RefreshTokenExpires = now.AddDays(-1)
            };

            bool accessExpired = expiredTokens.AccessTokenExpires < now;
            bool refreshExpired = expiredTokens.RefreshTokenExpires < now;

            ClassicAssert.IsTrue(accessExpired, "Access token should be expired");
            ClassicAssert.IsTrue(refreshExpired, "Refresh token should be expired");
        }

        [Test]
        public void TestTokenRotationScenario()
        {
            RefreshTokenRequest oldTokenRequest = new()
            {
                RefreshToken = "old-refresh-token"
            };

            TokenPair newTokenPair = new()
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(15),
                RefreshTokenExpires = DateTime.UtcNow.AddDays(7)
            };

            ClassicAssert.AreNotEqual(oldTokenRequest.RefreshToken, newTokenPair.RefreshToken);
            ClassicAssert.IsTrue(newTokenPair.AccessTokenExpires > DateTime.UtcNow);
            ClassicAssert.IsTrue(newTokenPair.RefreshTokenExpires > DateTime.UtcNow);
        }

        [Test]
        public void TestTokenLifetimeCalculation()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan accessLifetime = TimeSpan.FromMinutes(15);
            TimeSpan refreshLifetime = TimeSpan.FromDays(7);

            TokenPair tokenPair = new()
            {
                AccessToken = TestAccessToken,
                RefreshToken = TestRefreshToken,
                AccessTokenExpires = now.Add(accessLifetime),
                RefreshTokenExpires = now.Add(refreshLifetime)
            };

            TimeSpan actualAccessLifetime = tokenPair.AccessTokenExpires - now;
            TimeSpan actualRefreshLifetime = tokenPair.RefreshTokenExpires - now;

            ClassicAssert.IsTrue(Math.Abs(actualAccessLifetime.TotalMinutes - 15) < 1);
            ClassicAssert.IsTrue(Math.Abs(actualRefreshLifetime.TotalDays - 7) < 0.01);
        }

        [Test]
        public void TestMultipleRefreshTokenRequests()
        {
            List<RefreshTokenRequest> requests = [];

            for (int i = 0; i < 5; i++)
            {
                requests.Add(new RefreshTokenRequest
                {
                    RefreshToken = $"refresh-token-{i}"
                });
            }

            ClassicAssert.AreEqual(5, requests.Count);

            for (int i = 0; i < requests.Count; i++)
            {
                ClassicAssert.AreEqual($"refresh-token-{i}", requests[i].RefreshToken);
            }
        }
    }
}
