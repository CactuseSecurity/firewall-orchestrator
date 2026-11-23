using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Hosting;
using FWO.Data.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings and applies them to the agent factory holder when they have changed: a new factory for
/// usable settings, none for missing or invalid ones, so that runs fail with the reason. If the settings cannot be read
/// at all, e.g. while the API is not reachable, the current factory is kept. Each change and each new failure is logged
/// once. Not thread-safe: called by one background service only.
/// </summary>
public sealed class AiSettingsRefresher
{
    private readonly IAiSettingsSource settingsSource;
    private readonly AgentFactoryHolder agentFactories;
    private readonly Func<AiRuntimeSettings, FwoAgentFactory> createAgentFactory;
    private readonly ILogger logger;
    private AiRuntimeSettings? appliedSettings;
    private string? lastReadFailure;

    /// <summary>
    /// Creates the refresher.
    /// </summary>
    /// <param name="settingsSource">Reads the AI settings</param>
    /// <param name="agentFactories">Holder the factory of the settings is set in</param>
    /// <param name="createAgentFactory">Creates the agent factory of usable settings; throws for settings it refuses,
    /// with a message that holds no secret</param>
    /// <param name="logger">Logger of the changes and failures; none if null</param>
    public AiSettingsRefresher(IAiSettingsSource settingsSource, AgentFactoryHolder agentFactories,
        Func<AiRuntimeSettings, FwoAgentFactory> createAgentFactory, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsSource);
        ArgumentNullException.ThrowIfNull(agentFactories);
        ArgumentNullException.ThrowIfNull(createAgentFactory);
        this.settingsSource = settingsSource;
        this.agentFactories = agentFactories;
        this.createAgentFactory = createAgentFactory;
        this.logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Reads the AI settings and applies them if they have changed since they were last applied.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <returns>True if the settings were read, false if reading them failed.</returns>
    /// <exception cref="OperationCanceledException">The reading was cancelled.</exception>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        AiRuntimeSettings settings;
        try
        {
            settings = await settingsSource.LoadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogReadFailure(exception);
            return false;
        }
        if (lastReadFailure != null)
        {
            logger.LogInformation("The AI settings can be read again.");
            lastReadFailure = null;
        }
        if (!settings.HasSameValues(appliedSettings))
        {
            Apply(settings);
            appliedSettings = settings;
        }
        return true;
    }

    /// <summary>
    /// Sets the factory of the settings in the holder, or removes the factory with the reason the settings cannot be used.
    /// </summary>
    private void Apply(AiRuntimeSettings settings)
    {
        if (!settings.IsConfigured)
        {
            agentFactories.SetUnavailable(AgentFactoryHolder.kNotConfiguredReason);
            logger.LogInformation("No AI provider is configured, the agent is not available.");
            return;
        }
        if (settings.Problem != null)
        {
            agentFactories.SetUnavailable(settings.Problem);
            logger.LogError("The AI settings cannot be used, the agent is not available: {Problem}", settings.Problem);
            return;
        }
        try
        {
            agentFactories.SetFactory(createAgentFactory(settings));
            logger.LogInformation("Applied the AI settings: provider {Provider}, model {Model}, chat {ChatEnabled} (roles {ChatRoles}), "
                + "reason check {ReasonCheckEnabled} (roles {ReasonCheckRoles}).", settings.Values.Provider, settings.Values.Model,
                settings.Values.ChatEnabled, AiFunctionRoles.Serialize(settings.Values.ChatRoles),
                settings.Values.ReasonCheckEnabled, AiFunctionRoles.Serialize(settings.Values.ReasonCheckRoles));
        }
        catch (Exception exception)
        {
            // the factory and the chat client factory never put the API key into their messages
            string reason = $"The AI settings are invalid: {exception.Message}";
            agentFactories.SetUnavailable(reason);
            logger.LogError("The AI settings cannot be used, the agent is not available: {Problem}", reason);
        }
    }

    /// <summary>
    /// Logs a failure to read the settings, as a warning when it differs from the last one and as debug output otherwise,
    /// so that an API that stays unreachable does not fill the log.
    /// </summary>
    private void LogReadFailure(Exception exception)
    {
        string failure = $"{exception.GetType().Name}: {exception.Message}";
        if (failure == lastReadFailure)
        {
            logger.LogDebug("The AI settings still cannot be read: {Failure}", failure);
            return;
        }
        lastReadFailure = failure;
        string keptState = agentFactories.HasFactory ? "the current AI settings stay in effect" : "the agent stays unavailable";
        logger.LogWarning(exception, "The AI settings could not be read, {KeptState}.", keptState);
    }
}
