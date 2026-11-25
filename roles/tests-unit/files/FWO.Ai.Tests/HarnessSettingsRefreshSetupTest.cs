using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Ai.Harness.Settings;
using FWO.Data.Ai;
using FWO.Data.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests the background service reading the AI settings periodically, and the service setup wiring it to the agent
/// factory holder.
/// </summary>
[TestFixture]
internal class HarnessSettingsRefreshSetupTest
{
    private const int kExpectedLoads = 3;
    private static readonly TimeSpan kShortInterval = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan kPollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Verifies that the service reads the settings at start and again after each interval, also after a failed
    /// reading, and stops when asked to.
    /// </summary>
    [Test]
    public async Task RefreshService_ReadsSettingsUntilStopped()
    {
        FakeAiSettingsSource source = new() { Failure = new HttpRequestException("API not reachable") };
        using AgentFactoryHolder holder = new();
        AiSettingsRefresher refresher = new(source, holder, _ => throw new InvalidOperationException("no factory expected"));
        using AiSettingsRefreshService service = new(refresher, kShortInterval);

        await service.StartAsync(CancellationToken.None);
        bool readRepeatedly = await WaitUntil(() => source.Loads >= kExpectedLoads);
        await service.StopAsync(CancellationToken.None);
        int loadsAfterStop = source.Loads;
        await Task.Delay(kShortInterval * 5);

        Assert.That(readRepeatedly, Is.True);
        Assert.That(source.Loads, Is.EqualTo(loadsAfterStop));
        Assert.That(service.ExecuteTask?.IsCompletedSuccessfully, Is.True);
    }

    /// <summary>
    /// Verifies that the service needs a refresher and a positive interval.
    /// </summary>
    [Test]
    public void RefreshService_WithoutRefresherOrInterval_Throws()
    {
        using AgentFactoryHolder holder = new();
        AiSettingsRefresher refresher = new(new FakeAiSettingsSource(), holder, _ => throw new InvalidOperationException());

        Assert.Throws<ArgumentNullException>(() => _ = new AiSettingsRefreshService(null!, kShortInterval));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new AiSettingsRefreshService(refresher, TimeSpan.Zero));
    }

    /// <summary>
    /// Verifies that the setup registers the services reading the settings into the holder of the agent server, and
    /// that they apply the settings read with the technical user.
    /// </summary>
    [Test]
    public async Task AddFwoAiSettingsRefresh_WiresSettingsIntoHolder()
    {
        using AgentFactoryHolder holder = new();
        ServiceCollection services = new();
        services.AddSingleton(holder);
        services.AddFwoAiSettingsRefresh(new HarnessSettings());
        services.AddSingleton<IAiSettingsSource>(new FakeAiSettingsSource { Settings = new AiRuntimeSettings(CreateOllamaSettings()) });
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        AiSettingsRefresher refresher = serviceProvider.GetRequiredService<AiSettingsRefresher>();
        await refresher.RefreshAsync(CancellationToken.None);

        Assert.That(services.Any(service => service.ServiceType == typeof(ITechnicalUserLogin)), Is.True);
        Assert.That(services.Any(service => service.ServiceType == typeof(IAiConfigReader)), Is.True);
        Assert.That(serviceProvider.GetServices<IHostedService>().OfType<AiSettingsRefreshService>().Count(), Is.EqualTo(1));
        Assert.That(holder.HasFactory, Is.True);
    }

    /// <summary>
    /// Verifies that the setup reads the settings with the technical user: the reader it registers gets its token from
    /// the registered login.
    /// </summary>
    [Test]
    public void AddFwoAiSettingsRefresh_ReaderUsesTechnicalUserLogin()
    {
        ServiceCollection services = new();
        services.AddFwoAiSettingsRefresh(new HarnessSettings());
        services.AddSingleton<ITechnicalUserLogin>(new RefusingTechnicalUserLogin());
        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        IAiConfigReader reader = serviceProvider.GetRequiredService<IAiConfigReader>();
        IAiSettingsSource source = serviceProvider.GetRequiredService<IAiSettingsSource>();

        Assert.That(reader, Is.TypeOf<FwoAiConfigReader>());
        Assert.That(source, Is.TypeOf<FwoAiSettingsSource>());
        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadSettingsAsync(CancellationToken.None));
        Assert.That(exception!.Message, Is.EqualTo(RefusingTechnicalUserLogin.kRefusal));
    }

    /// <summary>
    /// Verifies that the agent factory is created from usable settings, and that settings the chat client factory
    /// refuses are refused without naming the API key.
    /// </summary>
    [Test]
    public void CreateAgentFactory_CreatesFactoryOrRefusesSettings()
    {
        AiSettings openAiWithoutKey = CreateOllamaSettings();
        openAiWithoutKey.Provider = AiProvider.OpenAi;

        using FwoAgentFactory factory = HarnessSettingsRefreshSetup.CreateAgentFactory(new AiRuntimeSettings(CreateOllamaSettings()), null);

        Assert.That(factory, Is.Not.Null);
        Assert.Throws<ArgumentException>(() => HarnessSettingsRefreshSetup.CreateAgentFactory(new AiRuntimeSettings(openAiWithoutKey), null));
    }

    /// <summary>
    /// Waits until the condition holds, at most <see cref="kWaitTimeout"/>.
    /// </summary>
    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + kWaitTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }
            await Task.Delay(kPollInterval);
        }
        return true;
    }

    /// <summary>
    /// Creates usable settings of a local Ollama, which needs no API key.
    /// </summary>
    private static AiSettings CreateOllamaSettings()
    {
        return new AiSettings
        {
            Provider = AiProvider.Ollama,
            Model = "llama3",
            Endpoint = "http://127.0.0.1:11434",
            ContextWindowTokens = HarnessTestSetup.kContextWindowTokens
        };
    }

    /// <summary>
    /// Login of a technical user that is always refused.
    /// </summary>
    private sealed class RefusingTechnicalUserLogin : ITechnicalUserLogin
    {
        internal const string kRefusal = "technical user refused";

        public Task<TokenPair> LoginAsync(CancellationToken cancellationToken)
        {
            return Task.FromException<TokenPair>(new InvalidOperationException(kRefusal));
        }

        public Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            return Task.FromResult<TokenPair?>(null);
        }
    }
}
