using FWO.Ai.Harness.Hosting;
using FWO.Ai.Harness.Settings;
using FWO.Data.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the login of the technical user of the harness at a stand-in of the middleware server: the password is read
/// from its file, a refused login is reported without the password, and a refused refresh returns no tokens.
/// </summary>
[TestFixture]
internal class HarnessTechnicalUserLoginTest
{
    private const string kUser = "ai-harness";
    private const string kPassword = "technical-user-test-password";
    private const string kRefreshToken = "refresh-token-1";
    private const string kUnknownRefreshToken = "unknown-refresh-token";
    private const string kLoopbackUrlWithFreePort = "http://127.0.0.1:0";
    private static readonly JsonSerializerOptions kCaseInsensitive = new() { PropertyNameCaseInsensitive = true };
    private static readonly List<string> kSentRefreshTokens = [kRefreshToken, kUnknownRefreshToken];

    private WebApplication middleware = null!;
    private string middlewareUri = "";
    private string passwordFile = "";
    private readonly List<AuthenticationTokenGetParameters> logins = [];
    private readonly List<RefreshTokenRequest> refreshes = [];

    /// <summary>
    /// Starts the stand-in of the middleware server, which accepts the test password and refresh token only.
    /// </summary>
    [OneTimeSetUp]
    public async Task StartMiddleware()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(kLoopbackUrlWithFreePort);
        middleware = builder.Build();
        middleware.MapPost("/api/AuthenticationToken/GetTokenPair", async (HttpRequest request) =>
        {
            AuthenticationTokenGetParameters? parameters = await request.ReadFromJsonAsync<AuthenticationTokenGetParameters>(kCaseInsensitive);
            logins.Add(parameters!);
            return parameters?.Username == kUser && parameters.Password == kPassword ? CreateTokens("login") : Results.Unauthorized();
        });
        middleware.MapPost("/api/AuthenticationToken/Refresh", async (HttpRequest request) =>
        {
            RefreshTokenRequest? parameters = await request.ReadFromJsonAsync<RefreshTokenRequest>(kCaseInsensitive);
            refreshes.Add(parameters!);
            return parameters?.RefreshToken == kRefreshToken ? CreateTokens("refresh") : Results.BadRequest();
        });
        await middleware.StartAsync();
        middlewareUri = McpTestServer.GetBaseAddress(middleware).ToString();
    }

    /// <summary>
    /// Stops the stand-in of the middleware server.
    /// </summary>
    [OneTimeTearDown]
    public async Task StopMiddleware()
    {
        await middleware.StopAsync();
        await middleware.DisposeAsync();
    }

    /// <summary>
    /// Creates the password file of a test.
    /// </summary>
    [SetUp]
    public void CreatePasswordFile()
    {
        passwordFile = Path.Combine(Path.GetTempPath(), $"fwo-ai-harness-test-{Guid.NewGuid():N}");
        File.WriteAllText(passwordFile, kPassword + "\n");
        logins.Clear();
        refreshes.Clear();
    }

    /// <summary>
    /// Deletes the password file of a test.
    /// </summary>
    [TearDown]
    public void DeletePasswordFile()
    {
        File.Delete(passwordFile);
    }

    /// <summary>
    /// Verifies that the technical user logs in with the password from its file, without its line break.
    /// </summary>
    [Test]
    public async Task LoginAsync_WithPasswordFromFile_ReturnsTokens()
    {
        using MiddlewareTechnicalUserLogin login = CreateLogin();

        TokenPair tokens = await login.LoginAsync(CancellationToken.None);

        Assert.That(tokens.AccessToken, Is.EqualTo("login-access"));
        Assert.That(tokens.RefreshToken, Is.EqualTo("login-refresh"));
        Assert.That(logins.Single().Username, Is.EqualTo(kUser));
        Assert.That(logins.Single().Password, Is.EqualTo(kPassword));
    }

    /// <summary>
    /// Verifies that a refused login throws with the user and the status, never with the password.
    /// </summary>
    [Test]
    public void LoginAsync_WithWrongPassword_ThrowsWithoutPassword()
    {
        File.WriteAllText(passwordFile, "wrong-password");
        using MiddlewareTechnicalUserLogin login = CreateLogin();

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => login.LoginAsync(CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain(kUser).And.Contain("401"));
        Assert.That(exception.Message, Does.Not.Contain("wrong-password"));
    }

    /// <summary>
    /// Verifies that a missing password file fails the login before the middleware server is called.
    /// </summary>
    [Test]
    public void LoginAsync_WithoutPasswordFile_Throws()
    {
        File.Delete(passwordFile);
        using MiddlewareTechnicalUserLogin login = CreateLogin();

        Assert.ThrowsAsync<FileNotFoundException>(() => login.LoginAsync(CancellationToken.None));
        Assert.That(logins, Is.Empty);
    }

    /// <summary>
    /// Verifies that a valid refresh token gets new tokens, and that a refused one gets none.
    /// </summary>
    [Test]
    public async Task RefreshAsync_ReturnsTokensOrNull()
    {
        using MiddlewareTechnicalUserLogin login = CreateLogin();

        TokenPair? refreshed = await login.RefreshAsync(kRefreshToken, CancellationToken.None);
        TokenPair? refused = await login.RefreshAsync(kUnknownRefreshToken, CancellationToken.None);

        Assert.That(refreshed?.AccessToken, Is.EqualTo("refresh-access"));
        Assert.That(refused, Is.Null);
        Assert.That(refreshes.Select(refresh => refresh.RefreshToken), Is.EqualTo(kSentRefreshTokens));
    }

    /// <summary>
    /// Verifies that the login needs settings and the address of the middleware server.
    /// </summary>
    [Test]
    public void Ctor_WithoutSettingsOrUri_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new MiddlewareTechnicalUserLogin(null!, middlewareUri));
        Assert.Throws<ArgumentException>(() => _ = new MiddlewareTechnicalUserLogin(new HarnessSettings(), " "));
    }

    /// <summary>
    /// Creates the login of the test user with the password file of the test.
    /// </summary>
    private MiddlewareTechnicalUserLogin CreateLogin()
    {
        HarnessSettings settings = new() { TechnicalUser = kUser, TechnicalUserPasswordFile = passwordFile };
        return new MiddlewareTechnicalUserLogin(settings, middlewareUri);
    }

    /// <summary>
    /// Returns a token pair the way the middleware server does, with the property names of <see cref="TokenPair"/>.
    /// </summary>
    private static IResult CreateTokens(string prefix)
    {
        TokenPair tokens = new() { AccessToken = $"{prefix}-access", RefreshToken = $"{prefix}-refresh" };
        return Results.Text(JsonSerializer.Serialize(tokens), "application/json");
    }
}
