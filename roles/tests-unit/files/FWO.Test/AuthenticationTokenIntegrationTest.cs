using NUnit.Framework;
using System.Net.Http.Json;
using FWO.Data.Middleware;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using FWO.Test.DataGenerators;
using FWO.Test.Helpers;
using FWO.Config.File;

namespace FWO.Test
{
    /// <summary>
    /// Integration tests for JWT authentication and refresh token functionality.
    /// Tests the complete authentication flow including token generation, refresh, and revocation.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [RequiresIntegrationEnvironment]
    internal class AuthenticationTokenIntegrationTest
    {
        // High enough that the requests genuinely overlap on the single-use token.
        private const int kConcurrentRefreshRequests = 18;
        // A burst this size can lose a call to a transport blip, which is a failure to
        // complete rather than a second token being spent. Tolerated, but bounded: without
        // a ceiling the test would also pass when every loser failed to reach the API, and
        // a reachability regression would read as a green run carrying a warning.
        private const int kMaxToleratedUpstreamFailures = 2;
        private const string DefaultCiUsername = "integration_user_jwt_refresh_test";
        private const string DefaultCiPassword = "testpassword";
        private HttpClient? client;
        private JwtSecurityTokenHandler? tokenHandler;
        private TokenTestDataBuilder defaultCredentialsBuilder = null!;
        //private TokenTestDataBuilder adminCredentialsBuilder = null!; // For future admin tests

        #region Setup and Teardown

        [OneTimeSetUp]
        public void GlobalSetup()
        {
            string? configuredUsername = Environment.GetEnvironmentVariable("FWO_TEST_USERNAME");
            string? configuredPassword = Environment.GetEnvironmentVariable("FWO_TEST_PASSWORD");

            string username = !string.IsNullOrWhiteSpace(configuredUsername) ? configuredUsername : DefaultCiUsername;
            string password = !string.IsNullOrWhiteSpace(configuredPassword) ? configuredPassword : DefaultCiPassword;

            bool usingLocalIntegrationMode = string.Equals(Environment.GetEnvironmentVariable("FWO_RUN_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

            bool runningInGitHubActions = string.Equals(Environment.GetEnvironmentVariable("RUNNING_ON_GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

            Log.WriteInfo("JWT Integration Tests", $"Enabled={usingLocalIntegrationMode}, using test user '{username}'.");

            if (usingLocalIntegrationMode && !runningInGitHubActions && (string.IsNullOrWhiteSpace(configuredUsername) || string.IsNullOrWhiteSpace(configuredPassword)))
            {
                Assert.Ignore("JWT integration tests require FWO_TEST_USERNAME and FWO_TEST_PASSWORD in local integration environments.");
            }

            // Initialize test credential
            defaultCredentialsBuilder = new TokenTestDataBuilder()
                .WithUsername(username)
                .WithPassword(password);

            // Exercise the middleware service that the installer deployed. Starting a
            // second host here can wait forever for its startup dependencies and does
            // not verify the installed reverse proxy.
            Uri middlewareUri = new(ConfigFile.MiddlewareServerUri);
            Log.WriteInfo("Test Setup", $"Using installed middleware at '{middlewareUri}'.");
            client = new HttpClient { BaseAddress = middlewareUri };

            tokenHandler = new JwtSecurityTokenHandler();
        }

        [OneTimeTearDown]
        public void GlobalCleanup()
        {
            Log.WriteInfo("Test Cleanup", "Disposing JWT integration test resources");
            client?.Dispose();
        }

        #endregion

        #region Token Generation Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenGeneration")]
        public async Task GetTokenPair_WithValidCredentials_ReturnsValidTokens()
        {
            // Arrange - use the integration-test credentials and assert the login still works in this environment
            AuthenticationTokenGetParameters parameters = defaultCredentialsBuilder.BuildGetParameters();

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPair", parameters);
            Assert.That(response.IsSuccessStatusCode, Is.True,
                $"Expected /api/AuthenticationToken/GetTokenPair to succeed for the configured integration credentials, but got {(int)response.StatusCode} {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
            TokenPair tokenPair = (await response.Content.ReadFromJsonAsync<TokenPair>())!;

            // Asserts
            AuthTestHelpers.AssertValidTokenPair(tokenPair);
            AuthTestHelpers.AssertJwtStructure(tokenPair.AccessToken, tokenHandler!);
            AuthTestHelpers.AssertTokenClaims(tokenPair.AccessToken, parameters.Username!, tokenHandler!);
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenGeneration")]
        public async Task GetTokenPair_WithInvalidCredentials_ReturnsBadRequest()
        {
            // Arrange - create invalid credentials
            AuthenticationTokenGetParameters parameters = AuthTestHelpers.CreateInvalidCredentials();

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPair", parameters);

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.False);
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenGeneration")]
        public async Task GetTokenPair_WithNullCredentials_ReturnsBadRequest()
        {
            // Arrange
            AuthenticationTokenGetParameters? credentials = null;

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPair", credentials);

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.False);
        }

        #endregion

        #region Token Refresh Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenRefresh")]
        public async Task RefreshToken_WithValidToken_ReturnsNewTokenPair()
        {
            // Arrange
            TokenPair initialTokens = await GetValidTokenPair();
            await Task.Delay(1000); // Ensure different timestamps

            // Act
            RefreshTokenRequest refreshRequest = new() { RefreshToken = initialTokens.RefreshToken };
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            // Asserts
            AuthTestHelpers.AssertSuccessResponse(response);
            TokenPair? newTokens = await response.Content.ReadFromJsonAsync<TokenPair>();

            AuthTestHelpers.AssertValidTokenPair(newTokens);
            AuthTestHelpers.AssertTokenRotation(initialTokens, newTokens!);
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenRefresh")]
        public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            RefreshTokenRequest refreshRequest = new() { RefreshToken = "invalid_refresh_token_xyz" };

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenRefresh")]
        public async Task RefreshToken_WithEmptyToken_ReturnsBadRequest()
        {
            // Arrange
            RefreshTokenRequest refreshRequest = new() { RefreshToken = "" };

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenRefresh")]
        [Category("Security")]
        public async Task RefreshToken_UsedTwice_SecondAttemptFails()
        {
            // Arrange
            TokenPair initialTokens = await GetValidTokenPair();
            RefreshTokenRequest refreshRequest = new() { RefreshToken = initialTokens.RefreshToken };

            // Act - First refresh (should succeed)
            HttpResponseMessage firstResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            // Act - Second refresh with same token (should fail due to token rotation)
            HttpResponseMessage secondResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            // Assert
            Assert.That(firstResponse.IsSuccessStatusCode, Is.True);
            Assert.That(secondResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        /// <summary>
        /// The security property under test is that a refresh token can be spent exactly
        /// once, however many requests race for it: the endpoint consumes it with a single
        /// conditional update and only that one caller gets a new pair.
        /// </summary>
        /// <remarks>
        /// The losers are expected to be rejected with 401. A burst this size against the
        /// installed middleware also makes every request authenticate against LDAP and query
        /// the API, so a single call can fail at the transport level; the endpoint answers
        /// 503 for that, and this test tolerates it while warning, because it is a failure to
        /// complete rather than a second token being spent. Any other status is a defect and
        /// fails, with every status and body in the message - a bare count comparison here
        /// cost a CI run that could not be diagnosed from its own output.
        /// </remarks>
        [Test]
        [Category("Authentication")]
        [Category("TokenRefresh")]
        [Category("Security")]
        public async Task RefreshToken_WithConcurrentRequests_OnlyOneSucceeds()
        {
            // Arrange
            TokenPair initialTokens = await GetValidTokenPair();
            RefreshTokenRequest refreshRequest = new() { RefreshToken = initialTokens.RefreshToken };

            // Act
            HttpResponseMessage[] responses = await Task.WhenAll(Enumerable
                .Range(0, kConcurrentRefreshRequests)
                .Select(_ => client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest)));

            string[] outcomes = await Task.WhenAll(responses
                .Select(async response => $"{(int)response.StatusCode} {response.StatusCode}: " +
                    $"{(await response.Content.ReadAsStringAsync()).Trim()}"));
            string report = string.Join(Environment.NewLine, outcomes);

            // Assert
            int successCount = responses.Count(response => response.IsSuccessStatusCode);
            int unauthorizedCount = responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized);
            int upstreamFailureCount = responses.Count(response => response.StatusCode == HttpStatusCode.ServiceUnavailable);

            Assert.Multiple(() =>
            {
                Assert.That(successCount, Is.EqualTo(1),
                    $"Exactly one concurrent refresh request must succeed. Responses:{Environment.NewLine}{report}");
                Assert.That(unauthorizedCount + upstreamFailureCount, Is.EqualTo(kConcurrentRefreshRequests - 1),
                    "Every other concurrent refresh request must be rejected with 401, or report 503 when it " +
                    $"could not reach the API. Responses:{Environment.NewLine}{report}");
                Assert.That(upstreamFailureCount, Is.LessThanOrEqualTo(kMaxToleratedUpstreamFailures),
                    $"At most {kMaxToleratedUpstreamFailures} of {kConcurrentRefreshRequests} requests may fail to " +
                    $"reach the API; more than that is a reachability problem, not a blip. " +
                    $"Responses:{Environment.NewLine}{report}");
            });

            if (upstreamFailureCount > 0)
            {
                Assert.Warn($"{upstreamFailureCount} of {kConcurrentRefreshRequests} concurrent refresh requests " +
                    $"could not reach the API and answered 503. Responses:{Environment.NewLine}{report}");
            }
        }

        #endregion

        #region Token Revocation Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenRevocation")]
        [Category("Security")]
        public async Task RevokeToken_WithValidToken_SucceedsAndPreventsRefresh()
        {
            // Arrange
            TokenPair tokens = await GetValidTokenPair();

            // Act - Revoke
            RefreshTokenRequest revokeRequest = new() { RefreshToken = tokens.RefreshToken };
            HttpResponseMessage revokeResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            // Assert - Revocation succeeded
            AuthTestHelpers.AssertSuccessResponse(revokeResponse);

            // Act - Try to refresh with revoked token
            HttpResponseMessage refreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", revokeRequest);

            // Assert - Refresh failed
            Assert.That(refreshResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenRevocation")]
        public async Task RevokeToken_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            RefreshTokenRequest revokeRequest = new() { RefreshToken = "invalid_token" };

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenRevocation")]
        public async Task RevokeToken_AlreadyRevoked_ReturnsUnauthorized()
        {
            // Arrange
            TokenPair tokens = await GetValidTokenPair();
            RefreshTokenRequest revokeRequest = new() { RefreshToken = tokens.RefreshToken };

            // Act - First revocation
            await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            // Act - Second revocation attempt
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        #endregion

        #region Admin Token Generation Tests

        [Test]
        [Category("Authentication")]
        [Category("AdminOperations")]
        [Category("Security")]
        public async Task GetForUser_WithNonAdminCredentials_ReturnsBadRequest()
        {
            // Arrange - ensure the configured integration user is valid and not admin
            TokenPair tokenPair = await GetValidTokenPair();
            var accessToken = tokenHandler!.ReadJwtToken(tokenPair.AccessToken);
            string? defaultRole = accessToken.Claims.SingleOrDefault(claim => claim.Type == "x-hasura-default-role")?.Value;
            if (string.Equals(defaultRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("Configured JWT integration test user has admin role in this environment; non-admin coverage requires a non-admin user.");
            }

            // Use regular user credentials (not admin)
            AuthenticationTokenGetForUserParameters parameters = defaultCredentialsBuilder
                .WithTargetUser(defaultCredentialsBuilder.Username!)
                .BuildGetForUserParameters();

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPairForUser", parameters);

            string responseText = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
            Assert.That(responseText, Does.Contain("Provided credentials do not belong to a user with role admin"));
        }

        #endregion

        #region Token Expiration Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenExpiration")]
        public async Task TokenPair_ExpirationDates_AreSetCorrectly()
        {
            // Arrange & Act
            TokenPair tokens = await GetValidTokenPair();

            // Assert for expiration hierarchy
            AuthTestHelpers.AssertTokenExpirationHierarchy(tokens);
        }

        #endregion

        #region Helper Methods

        private async Task<TokenPair> GetValidTokenPair()
        {
            // Use default credentials from GlobalSetup
            AuthenticationTokenGetParameters parameters = defaultCredentialsBuilder.BuildGetParameters();
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPair", parameters);

            if (!response.IsSuccessStatusCode)
            {
                Assert.Ignore($"Configured integration credentials are not accepted in this environment. Got {(int)response.StatusCode} {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
            }

            return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        }

        #endregion

        #region Token Workflow Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenWorkflow")]
        public async Task TokenWorkflow_CompleteLifecycle_GetRefreshRevoke_WorksCorrectly()
        {
            // Step 1: Get initial token pair
            TokenPair initialTokens = await GetValidTokenPair();
            await Task.Delay(1000); // Ensure different timestamps

            // Assert initial tokens are valid
            AuthTestHelpers.AssertValidTokenPair(initialTokens);
            AuthTestHelpers.AssertJwtStructure(initialTokens.AccessToken, tokenHandler!);

            // Step 2: Refresh the token
            RefreshTokenRequest refreshRequest = new() { RefreshToken = initialTokens.RefreshToken };
            HttpResponseMessage refreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            AuthTestHelpers.AssertSuccessResponse(refreshResponse);
            TokenPair? refreshedTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenPair>();

            // Assert refreshed tokens are valid and different
            AuthTestHelpers.AssertValidTokenPair(refreshedTokens);
            AuthTestHelpers.AssertTokenRotation(initialTokens, refreshedTokens!);

            // Step 3: Revoke the refreshed token
            RefreshTokenRequest revokeRequest = new() { RefreshToken = refreshedTokens!.RefreshToken };
            HttpResponseMessage revokeResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            // Assert revocation succeeded
            AuthTestHelpers.AssertSuccessResponse(revokeResponse);

            // Step 4: Verify token cannot be used after revocation
            HttpResponseMessage postRevokeRefreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", revokeRequest);
            Assert.That(postRevokeRefreshResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenWorkflow")]
        public async Task TokenWorkflow_MultipleSequentialRefreshes_AllSucceed()
        {
            // Step 1: Get initial token pair
            TokenPair currentTokens = await GetValidTokenPair();
            AuthTestHelpers.AssertValidTokenPair(currentTokens);

            // Step 2: Perform multiple sequential refreshes
            const int refreshCount = 5;
            for (int i = 0; i < refreshCount; i++)
            {
                await Task.Delay(1000); // Ensure different timestamps

                RefreshTokenRequest refreshRequest = new() { RefreshToken = currentTokens.RefreshToken };
                HttpResponseMessage refreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

                // Assert each refresh succeeds
                AuthTestHelpers.AssertSuccessResponse(refreshResponse);
                TokenPair? newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenPair>();

                // Assert new tokens are valid and different
                AuthTestHelpers.AssertValidTokenPair(newTokens);
                AuthTestHelpers.AssertTokenRotation(currentTokens, newTokens!);

                // Update current tokens for next iteration
                currentTokens = newTokens!;
            }

            // Step 3: Final revocation to clean up
            RefreshTokenRequest finalRevokeRequest = new() { RefreshToken = currentTokens.RefreshToken };
            HttpResponseMessage revokeResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", finalRevokeRequest);
            AuthTestHelpers.AssertSuccessResponse(revokeResponse);
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenWorkflow")]
        [Category("Security")]
        public async Task TokenWorkflow_OldRefreshTokenInvalidAfterRefresh_NewTokenWorks()
        {
            // Step 1: Get initial token pair
            TokenPair initialTokens = await GetValidTokenPair();
            await Task.Delay(1000);

            // Step 2: Refresh to get new tokens
            RefreshTokenRequest firstRefreshRequest = new() { RefreshToken = initialTokens.RefreshToken };
            HttpResponseMessage firstRefreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", firstRefreshRequest);

            AuthTestHelpers.AssertSuccessResponse(firstRefreshResponse);
            TokenPair? newTokens = await firstRefreshResponse.Content.ReadFromJsonAsync<TokenPair>();
            AuthTestHelpers.AssertValidTokenPair(newTokens);

            // Step 3: Try to use old refresh token (should fail due to rotation)
            HttpResponseMessage oldTokenRefreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", firstRefreshRequest);
            Assert.That(oldTokenRefreshResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized),
                "Old refresh token should be invalid after rotation");

            // Step 4: Verify new token still works
            await Task.Delay(1000);
            RefreshTokenRequest newRefreshRequest = new() { RefreshToken = newTokens!.RefreshToken };
            HttpResponseMessage newTokenRefreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", newRefreshRequest);

            AuthTestHelpers.AssertSuccessResponse(newTokenRefreshResponse);
            TokenPair? finalTokens = await newTokenRefreshResponse.Content.ReadFromJsonAsync<TokenPair>();
            AuthTestHelpers.AssertValidTokenPair(finalTokens);

            // Cleanup
            RefreshTokenRequest revokeRequest = new() { RefreshToken = finalTokens!.RefreshToken };
            await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);
        }

        [Test]
        [Category("Authentication")]
        [Category("TokenWorkflow")]
        [Category("Security")]
        public async Task TokenWorkflow_GetTokenThenImmediateRevoke_CannotRefresh()
        {
            // Step 1: Get initial token pair
            TokenPair tokens = await GetValidTokenPair();
            AuthTestHelpers.AssertValidTokenPair(tokens);

            // Step 2: Immediately revoke without any refresh
            RefreshTokenRequest revokeRequest = new() { RefreshToken = tokens.RefreshToken };
            HttpResponseMessage revokeResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Revoke", revokeRequest);

            AuthTestHelpers.AssertSuccessResponse(revokeResponse);

            // Step 3: Attempt to refresh revoked token (should fail)
            RefreshTokenRequest refreshRequest = new() { RefreshToken = tokens.RefreshToken };
            HttpResponseMessage refreshResponse = await client!.PostAsJsonAsync("/api/AuthenticationToken/Refresh", refreshRequest);

            Assert.That(refreshResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized),
                "Cannot refresh a token that has been revoked");
        }

        #endregion
    }
}
