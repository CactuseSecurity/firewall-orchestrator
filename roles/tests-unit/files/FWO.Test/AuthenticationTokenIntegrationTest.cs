using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using FWO.Data.Middleware;
using FWO.Logging;
using System.IdentityModel.Tokens.Jwt;
using FWO.Test.DataGenerators;
using Microsoft.Extensions.Configuration;
using FWO.Config.File;
using Microsoft.AspNetCore.Hosting;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using FWO.Basics;

namespace FWO.Test
{
    /// <summary>
    /// Integration tests for JWT authentication and refresh token functionality.
    /// Tests the complete authentication flow including token generation, refresh, and revocation.
    /// </summary>
    [TestFixture]
    [Order(2)]
    internal class AuthenticationTokenIntegrationTest
    {
        private WebApplicationFactory<Program>? factory;
        private HttpClient? client;
        private JwtSecurityTokenHandler? tokenHandler;

        // Test credentials - configured once in GlobalSetup
        private TokenTestDataBuilder defaultCredentialsBuilder = null!;
        //private TokenTestDataBuilder adminCredentialsBuilder = null!; // For future admin tests

        #region Setup and Teardown

        [OneTimeSetUp]
        public async Task GlobalSetup()
        {
            bool isLocalTest = IsLocalTestEnvironment();
            bool isGitHubActions = IsRunningInGitHubActions();
            
            Log.WriteInfo("Test Setup", $"Initializing JWT integration test environment (Local: {isLocalTest}, GitHub Actions: {isGitHubActions})");

            // Initialize test credential
            defaultCredentialsBuilder = new TokenTestDataBuilder()
                .WithUsername("integration_user_jwt_refresh_test")
                .WithPassword("testpassword");

            //For tests with admin credentials needed (maybe in the future)
            //adminCredentialsBuilder = new TokenTestDataBuilder()
            //    .WithTargetUser("admin")
            //    .WithUsername("admin")
            //    .WithPassword("adminpassword");

            if (isLocalTest)
            {
                // Spin up local test server using WebApplicationFactory
                Log.WriteInfo("Test Setup", "Creating WebApplicationFactory for local testing");
                factory = new WebApplicationFactory<Program>()
                    .WithWebHostBuilder(builder =>
                    {
                        builder.ConfigureAppConfiguration((context, config) =>
                        {
                            var testConfig = new Dictionary<string, string?>
                            {
                                { "Environment", GlobalConst.ASPNETCORE_ENVIRONMENT_LOCALTEST },
                                { "Logging:LogLevel:Default", "Debug" }
                            };
                            config.AddInMemoryCollection(testConfig);
                        });
                    });

                client = factory.CreateClient();
            }
            else
            {
                string baseUrl = ConfigFile.MiddlewareServerNativeUri;
                Log.WriteInfo("Test Setup", $"Connecting to external middleware server at: {baseUrl}");
                
                client = new HttpClient
                {
                    BaseAddress = new Uri(baseUrl)
                };
            }

            tokenHandler = new JwtSecurityTokenHandler();
        }

        [OneTimeTearDown]
        public void GlobalCleanup()
        {
            Log.WriteInfo("Test Cleanup", "Disposing JWT integration test resources");
            client?.Dispose();
            factory?.Dispose();
        }

        #endregion

        #region Environment Detection

        private static bool IsLocalTestEnvironment()
        {
            string? aspnetcoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return aspnetcoreEnv?.Equals(GlobalConst.ASPNETCORE_ENVIRONMENT_LOCALTEST, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsRunningInGitHubActions()
        {
            string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            string? ci = Environment.GetEnvironmentVariable("CI");

            if (string.IsNullOrEmpty(sudoUser) || string.IsNullOrEmpty(ci))
            {
                return false;
            }

            return sudoUser.Equals("runner", StringComparison.OrdinalIgnoreCase) &&
                ci.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Token Generation Tests

        [Test]
        [Category("Authentication")]
        [Category("TokenGeneration")]
        public async Task GetTokenPair_WithValidCredentials_ReturnsValidTokens()
        {
            // Arrange - use default credentials
            AuthenticationTokenGetParameters parameters = defaultCredentialsBuilder.BuildGetParameters();

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetTokenPair", parameters);

            // Asserts
            AuthTestHelpers.AssertSuccessResponse(response);
            TokenPair? tokenPair = await response.Content.ReadFromJsonAsync<TokenPair>();

            AuthTestHelpers.AssertValidTokenPair(tokenPair);
            AuthTestHelpers.AssertJwtStructure(tokenPair!.AccessToken, tokenHandler!);
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
            // Arrange - use regular user credentials (not admin)
            AuthenticationTokenGetForUserParameters parameters = defaultCredentialsBuilder
                .WithTargetUser(defaultCredentialsBuilder.Username!)
                .BuildGetForUserParameters();

            // Act
            HttpResponseMessage response = await client!.PostAsJsonAsync("/api/AuthenticationToken/GetForUser", parameters);

            string responseText = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
            Assert.That(responseText, Is.EqualTo("Error while validating admin credentials: Provided credentials do not belong to a user with role admin.").IgnoreCase);
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
                throw new Exception($"Failed to get valid token pair for test setup. Status: {response.StatusCode}");
            }

            return (await response.Content.ReadFromJsonAsync<TokenPair>())!;
        }

        #endregion
    }
}
