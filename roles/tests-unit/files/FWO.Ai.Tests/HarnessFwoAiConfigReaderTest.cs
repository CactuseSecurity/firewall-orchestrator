using FWO.Ai.Harness.Settings;
using FWO.Api.Client.Queries;
using FWO.Data.Ai;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests reading the AI settings with the token of the technical user: the user logs in once, the token is kept until
/// shortly before it expires, and is then renewed with the refresh token, or by a new login if that is refused.
/// </summary>
[TestFixture]
internal class HarnessFwoAiConfigReaderTest
{
    private const string kModel = "test-model";
    private const int kCredentialId = 21;
    private static readonly DateTimeOffset kStartTime = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan kTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly List<string> kFirstRefreshToken = ["refresh-1"];
    private static readonly List<string> kFirstTwoRefreshTokens = ["refresh-1", "refresh-2"];

    private ManualTimeProvider clock = null!;
    private FakeTechnicalUserLogin login = null!;
    private RecordingAiApiConnection connection = null!;
    private List<string> connectTokens = null!;

    /// <summary>
    /// Points the query files at the repository, before the queries are first read.
    /// </summary>
    [OneTimeSetUp]
    public void UseRepositoryQueries()
    {
        AiTestTokens.UseRepositoryQueries();
    }

    /// <summary>
    /// Creates the clock, login and connection of a test.
    /// </summary>
    [SetUp]
    public void CreateDoubles()
    {
        clock = new ManualTimeProvider(kStartTime);
        login = new FakeTechnicalUserLogin(clock, kTokenLifetime);
        connection = new RecordingAiApiConnection();
        connectTokens = [];
    }

    /// <summary>
    /// Disposes the connection of a test.
    /// </summary>
    [TearDown]
    public void DisposeDoubles()
    {
        connection.Dispose();
    }

    /// <summary>
    /// Verifies that the settings are read with the AI keys, and that the token of one login is used for several
    /// readings while it is valid.
    /// </summary>
    [Test]
    public async Task ReadSettingsAsync_WithValidToken_LogsInOnce()
    {
        connection.ConfigValues =
        [
            new AiConfigValue { Key = AiSettings.kProviderKey, Value = "OpenAi" },
            new AiConfigValue { Key = AiSettings.kModelKey, Value = kModel }
        ];
        using FwoAiConfigReader reader = CreateReader();

        IReadOnlyDictionary<string, string?> values = await reader.ReadSettingsAsync(CancellationToken.None);
        clock.Now = kStartTime.AddMinutes(5);
        await reader.ReadSettingsAsync(CancellationToken.None);

        Assert.That(values[AiSettings.kProviderKey], Is.EqualTo("OpenAi"));
        Assert.That(values[AiSettings.kModelKey], Is.EqualTo(kModel));
        Assert.That(login.Logins, Is.EqualTo(1));
        Assert.That(login.UsedRefreshTokens, Is.Empty);
        Assert.That(connectTokens, Is.EqualTo(login.IssuedAccessTokens));
        Assert.That(connection.AuthHeaders, Is.Empty);
        Assert.That(connection.Queries, Is.All.EqualTo(AiQueries.getAiSettings));
        Assert.That(GetKeysVariable(connection.Variables[0]), Is.EqualTo(AiSettings.kKeys));
    }

    /// <summary>
    /// Verifies that a token about to expire is renewed with the refresh token before the next reading.
    /// </summary>
    [Test]
    public async Task ReadSettingsAsync_NearExpiry_RefreshesToken()
    {
        using FwoAiConfigReader reader = CreateReader();

        await reader.ReadSettingsAsync(CancellationToken.None);
        clock.Now = kStartTime.Add(kTokenLifetime) - FwoAiConfigReader.kTokenRenewalLeadTime;
        await reader.ReadSettingsAsync(CancellationToken.None);
        clock.Now += kTokenLifetime - FwoAiConfigReader.kTokenRenewalLeadTime;
        await reader.ReadSettingsAsync(CancellationToken.None);

        Assert.That(login.Logins, Is.EqualTo(1));
        Assert.That(login.UsedRefreshTokens, Is.EqualTo(kFirstTwoRefreshTokens));
        Assert.That(connectTokens, Has.Count.EqualTo(1));
        Assert.That(connection.AuthHeaders, Is.EqualTo(login.IssuedAccessTokens.Skip(1)));
    }

    /// <summary>
    /// Verifies that the technical user logs in again when the refresh token is refused, e.g. after a restart of the
    /// middleware server.
    /// </summary>
    [Test]
    public async Task ReadSettingsAsync_WhenRefreshIsRefused_LogsInAgain()
    {
        using FwoAiConfigReader reader = CreateReader();

        await reader.ReadSettingsAsync(CancellationToken.None);
        login.RefuseRefresh = true;
        clock.Now = kStartTime.Add(kTokenLifetime);
        await reader.ReadSettingsAsync(CancellationToken.None);

        Assert.That(login.Logins, Is.EqualTo(2));
        Assert.That(login.UsedRefreshTokens, Is.EqualTo(kFirstRefreshToken));
        Assert.That(connection.AuthHeaders, Is.EqualTo(login.IssuedAccessTokens.Skip(1)));
    }

    /// <summary>
    /// Verifies that the credential is only returned if the view returns exactly one, the one the settings reference.
    /// </summary>
    [Test]
    public async Task ReadCredentialAsync_ReturnsOnlySingleCredential()
    {
        AiCredentialValue credential = new() { Id = kCredentialId, Secret = "encrypted" };
        using FwoAiConfigReader reader = CreateReader();

        connection.Credentials = [credential];
        AiCredentialValue? single = await reader.ReadCredentialAsync(CancellationToken.None);
        connection.Credentials = [];
        AiCredentialValue? none = await reader.ReadCredentialAsync(CancellationToken.None);
        connection.Credentials = [credential, new AiCredentialValue { Id = kCredentialId + 1 }];
        AiCredentialValue? ambiguous = await reader.ReadCredentialAsync(CancellationToken.None);

        Assert.That(single, Is.SameAs(credential));
        Assert.That(none, Is.Null);
        Assert.That(ambiguous, Is.Null);
        Assert.That(connection.Queries, Is.All.EqualTo(AiQueries.getAiProviderCredential));
    }

    /// <summary>
    /// Verifies that the columns of the view v_ai_provider_credential map to the credential, the user name (client id
    /// of OAuth 2.0) included, with both JSON serializers the API clients use.
    /// </summary>
    [Test]
    public void AiCredentialValue_ReadsTheColumnsOfTheView()
    {
        const string row = "{\"id\":21,\"is_key_pair\":false,\"secret\":\"encrypted\",\"username\":\"fwo-ai-client\"}";

        AiCredentialValue? fromNewtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<AiCredentialValue>(row);
        AiCredentialValue? fromSystemText = System.Text.Json.JsonSerializer.Deserialize<AiCredentialValue>(row);

        Assert.Multiple(() =>
        {
            foreach (AiCredentialValue? credential in new List<AiCredentialValue?> { fromNewtonsoft, fromSystemText })
            {
                Assert.That(credential?.Id, Is.EqualTo(kCredentialId));
                Assert.That(credential?.Secret, Is.EqualTo("encrypted"));
                Assert.That(credential?.Username, Is.EqualTo("fwo-ai-client"));
            }
        });
    }

    /// <summary>
    /// Verifies that a failed login is passed on, without creating a connection.
    /// </summary>
    [Test]
    public void ReadSettingsAsync_WhenLoginFails_Throws()
    {
        using FwoAiConfigReader reader = new(new FailingTechnicalUserLogin(), Connect, clock);

        Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadSettingsAsync(CancellationToken.None));
        Assert.That(connectTokens, Is.Empty);
    }

    /// <summary>
    /// Verifies that disposing the reader disposes its connection, and that a reader without connection can be disposed.
    /// </summary>
    [Test]
    public async Task Dispose_DisposesConnection()
    {
        FwoAiConfigReader unusedReader = CreateReader();
        FwoAiConfigReader reader = CreateReader();
        await reader.ReadSettingsAsync(CancellationToken.None);

        unusedReader.Dispose();
        reader.Dispose();

        Assert.That(connection.Disposed, Is.True);
    }

    /// <summary>
    /// Verifies that the reader needs a login and a way to connect.
    /// </summary>
    [Test]
    public void Ctor_WithoutLoginOrConnect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new FwoAiConfigReader(null!, Connect));
        Assert.Throws<ArgumentNullException>(() => _ = new FwoAiConfigReader(login, null!));
    }

    /// <summary>
    /// Creates the reader with the doubles of the test.
    /// </summary>
    private FwoAiConfigReader CreateReader()
    {
        return new FwoAiConfigReader(login, Connect, clock);
    }

    /// <summary>
    /// Returns the connection of the test, recording the token it was created with.
    /// </summary>
    private RecordingAiApiConnection Connect(string accessToken)
    {
        connectTokens.Add(accessToken);
        return connection;
    }

    /// <summary>
    /// Returns the keys variable of the settings query.
    /// </summary>
    private static object? GetKeysVariable(object? variables)
    {
        return variables?.GetType().GetProperty("keys")?.GetValue(variables);
    }

    /// <summary>
    /// Login of a technical user whose password is refused.
    /// </summary>
    private sealed class FailingTechnicalUserLogin : ITechnicalUserLogin
    {
        public Task<FWO.Data.Middleware.TokenPair> LoginAsync(CancellationToken cancellationToken)
        {
            return Task.FromException<FWO.Data.Middleware.TokenPair>(new InvalidOperationException("login refused"));
        }

        public Task<FWO.Data.Middleware.TokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            return Task.FromResult<FWO.Data.Middleware.TokenPair?>(null);
        }
    }
}
