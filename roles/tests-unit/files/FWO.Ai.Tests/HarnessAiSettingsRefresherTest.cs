using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Ai.Harness.Settings;
using FWO.Data.Ai;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace FWO.Ai.Tests;

/// <summary>
/// Tests applying the AI settings to the agent factory holder: a new factory only for changed settings, none for missing
/// or unusable ones, and the current one kept while the settings cannot be read.
/// </summary>
[TestFixture]
internal class HarnessAiSettingsRefresherTest
{
    private const string kApiKey = "sk-refresher-test-key-4711";
    private const string kModel = "test-model";
    private const string kOtherModel = "other-model";
    private const string kProblem = "The credential 5 of the AI provider was not found.";
    private const string kRefusal = "refused by the factory";

    private FakeAiSettingsSource source = null!;
    private AgentFactoryHolder holder = null!;
    private RecordingLogger logger = null!;
    private List<ScriptedChatClient> createdClients = null!;

    /// <summary>
    /// Creates the source, holder and logger of a test.
    /// </summary>
    [SetUp]
    public void CreateDoubles()
    {
        source = new FakeAiSettingsSource();
        holder = new AgentFactoryHolder();
        logger = new RecordingLogger();
        createdClients = [];
    }

    /// <summary>
    /// Disposes the holder of a test.
    /// </summary>
    [TearDown]
    public void DisposeHolder()
    {
        holder.Dispose();
    }

    /// <summary>
    /// Verifies that usable settings set a factory created from them.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WithUsableSettings_SetsFactory()
    {
        source.Settings = CreateSettings(kModel);

        bool read = await CreateRefresher().RefreshAsync(CancellationToken.None);

        Assert.That(read, Is.True);
        Assert.That(holder.HasFactory, Is.True);
        Assert.That(createdClients, Has.Count.EqualTo(1));
        Assert.That(logger.Count(LogLevel.Information), Is.EqualTo(1));
        Assert.That(logger.Entries.Select(entry => entry.Message), Has.None.Contains(kApiKey));
    }

    /// <summary>
    /// Verifies that settings with the same values keep the factory, and that changed settings replace it.
    /// </summary>
    [Test]
    public async Task RefreshAsync_OnlyChangedSettings_ReplaceFactory()
    {
        AiSettingsRefresher refresher = CreateRefresher();

        source.Settings = CreateSettings(kModel);
        await refresher.RefreshAsync(CancellationToken.None);
        source.Settings = CreateSettings(kModel);
        await refresher.RefreshAsync(CancellationToken.None);
        int factoriesOfSameSettings = createdClients.Count;
        source.Settings = CreateSettings(kOtherModel);
        await refresher.RefreshAsync(CancellationToken.None);

        Assert.That(factoriesOfSameSettings, Is.EqualTo(1));
        Assert.That(createdClients, Has.Count.EqualTo(2));
        Assert.That(createdClients[0].DisposeCount, Is.EqualTo(1));
        Assert.That(createdClients[1].DisposeCount, Is.Zero);
    }

    /// <summary>
    /// Verifies that removing the provider on the settings page removes the factory.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WithoutProvider_RemovesFactory()
    {
        AiSettingsRefresher refresher = CreateRefresher();
        source.Settings = CreateSettings(kModel);
        await refresher.RefreshAsync(CancellationToken.None);

        source.Settings = new AiRuntimeSettings(new AiSettings());
        await refresher.RefreshAsync(CancellationToken.None);

        Assert.That(holder.HasFactory, Is.False);
        Assert.That(holder.UnavailableReason, Is.EqualTo(AgentFactoryHolder.kNotConfiguredReason));
        Assert.That(createdClients[0].DisposeCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that unusable settings make the runs fail with their problem, which is logged as error, without
    /// creating a factory.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WithProblem_MakesAgentUnavailable()
    {
        source.Settings = new AiRuntimeSettings(CreateSettings(kModel).Values, problem: kProblem);

        await CreateRefresher().RefreshAsync(CancellationToken.None);

        Assert.That(holder.HasFactory, Is.False);
        Assert.That(holder.UnavailableReason, Is.EqualTo(kProblem));
        Assert.That(createdClients, Is.Empty);
        Assert.That(logger.Count(LogLevel.Error), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that settings the factory refuses make the runs fail with the reason of the refusal.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WhenFactoryRefusesSettings_MakesAgentUnavailable()
    {
        source.Settings = CreateSettings(kModel);
        AiSettingsRefresher refresher = new(source, holder, _ => throw new ArgumentException(kRefusal), logger);

        await refresher.RefreshAsync(CancellationToken.None);

        Assert.That(holder.HasFactory, Is.False);
        Assert.That(holder.UnavailableReason, Is.EqualTo($"The AI settings are invalid: {kRefusal}"));
        Assert.That(logger.Count(LogLevel.Error), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that the current factory stays while the settings cannot be read, that the same failure is warned about
    /// only once, and that reading them again is logged.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WhenReadingFails_KeepsFactoryAndWarnsOnce()
    {
        AiSettingsRefresher refresher = CreateRefresher();
        source.Settings = CreateSettings(kModel);
        await refresher.RefreshAsync(CancellationToken.None);

        source.Failure = new HttpRequestException("API not reachable");
        bool firstRead = await refresher.RefreshAsync(CancellationToken.None);
        bool secondRead = await refresher.RefreshAsync(CancellationToken.None);
        bool hadFactoryWhileFailing = holder.HasFactory;
        source.Failure = null;
        bool readAgain = await refresher.RefreshAsync(CancellationToken.None);

        Assert.That(firstRead, Is.False);
        Assert.That(secondRead, Is.False);
        Assert.That(readAgain, Is.True);
        Assert.That(hadFactoryWhileFailing, Is.True);
        Assert.That(createdClients, Has.Count.EqualTo(1));
        Assert.That(createdClients[0].DisposeCount, Is.Zero);
        Assert.That(logger.Count(LogLevel.Warning), Is.EqualTo(1));
        Assert.That(logger.Count(LogLevel.Debug), Is.EqualTo(1));
        Assert.That(logger.Entries[^1].Message, Is.EqualTo("The AI settings can be read again."));
    }

    /// <summary>
    /// Verifies that a failure before any settings were applied leaves the agent unavailable.
    /// </summary>
    [Test]
    public async Task RefreshAsync_WhenFirstReadingFails_LeavesAgentUnavailable()
    {
        source.Failure = new HttpRequestException("API not reachable");

        bool read = await CreateRefresher().RefreshAsync(CancellationToken.None);

        Assert.That(read, Is.False);
        Assert.That(holder.HasFactory, Is.False);
        Assert.That(logger.Entries.Single().Message, Does.Contain("the agent stays unavailable"));
    }

    /// <summary>
    /// Verifies that a cancelled reading is passed on rather than logged as failure.
    /// </summary>
    [Test]
    public void RefreshAsync_WhenCancelled_Throws()
    {
        source.Settings = CreateSettings(kModel);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => CreateRefresher().RefreshAsync(cancellation.Token));
        Assert.That(logger.Entries, Is.Empty);
    }

    /// <summary>
    /// Verifies that the refresher needs a source, a holder and a way to create the factory, but no logger.
    /// </summary>
    [Test]
    public void Ctor_WithoutArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new AiSettingsRefresher(null!, holder, CreateFactory));
        Assert.Throws<ArgumentNullException>(() => _ = new AiSettingsRefresher(source, null!, CreateFactory));
        Assert.Throws<ArgumentNullException>(() => _ = new AiSettingsRefresher(source, holder, null!));
        Assert.DoesNotThrow(() => _ = new AiSettingsRefresher(source, holder, CreateFactory));
    }

    /// <summary>
    /// Creates the refresher with the doubles of the test.
    /// </summary>
    private AiSettingsRefresher CreateRefresher()
    {
        return new AiSettingsRefresher(source, holder, CreateFactory, logger);
    }

    /// <summary>
    /// Creates a factory with a scripted chat client, recording the client.
    /// </summary>
    private FwoAgentFactory CreateFactory(AiRuntimeSettings settings)
    {
        ScriptedChatClient chatClient = new();
        createdClients.Add(chatClient);
        return new FwoAgentFactory(chatClient, settings.Values.ContextWindowTokens, settings.ToAgentSettings());
    }

    /// <summary>
    /// Creates usable settings of OpenAI with the given model.
    /// </summary>
    private static AiRuntimeSettings CreateSettings(string model)
    {
        AiSettings values = new()
        {
            Provider = AiProvider.OpenAi,
            Model = model,
            CredentialId = 5,
            ContextWindowTokens = HarnessTestSetup.kContextWindowTokens
        };
        return new AiRuntimeSettings(values, kApiKey);
    }
}
