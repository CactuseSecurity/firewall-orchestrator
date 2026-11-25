using FWO.Ai.Harness.Agent;
using FWO.Ai.Harness.Settings;
using FWO.Api.Client;
using FWO.Config.File;

namespace FWO.Ai.Harness.Hosting;

/// <summary>
/// Service setup of the AI settings refresh: the harness logs in with its technical user at the middleware server on
/// the same host, reads the AI settings of the UI and the credential of the API key from the API server (both named in
/// the FWO config file), and sets the agent factory of the settings in the <see cref="AgentFactoryHolder"/>.
/// </summary>
internal static class HarnessSettingsRefreshSetup
{
    /// <summary>
    /// Adds the services reading the AI settings at startup and every <see cref="HarnessSettings.SettingsRefreshSeconds"/>.
    /// Requires the <see cref="AgentFactoryHolder"/> of <see cref="HarnessServerSetup.AddFwoAgentServer"/>.
    /// </summary>
    /// <param name="services">Services of the harness server</param>
    /// <param name="settings">Settings of the harness server, naming the technical user and the refresh interval</param>
    public static IServiceCollection AddFwoAiSettingsRefresh(this IServiceCollection services, HarnessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        services.AddSingleton<ITechnicalUserLogin>(_ => new MiddlewareTechnicalUserLogin(settings, ConfigFile.MiddlewareServerNativeUri));
        services.AddSingleton<IAiConfigReader>(serviceProvider => new FwoAiConfigReader(serviceProvider.GetRequiredService<ITechnicalUserLogin>(),
            accessToken => new GraphQlApiConnection(ConfigFile.ApiServerUri, accessToken)));
        services.AddSingleton<IAiSettingsSource>(serviceProvider => new FwoAiSettingsSource(serviceProvider.GetRequiredService<IAiConfigReader>()));
        services.AddSingleton(serviceProvider => new AiSettingsRefresher(
            serviceProvider.GetRequiredService<IAiSettingsSource>(),
            serviceProvider.GetRequiredService<AgentFactoryHolder>(),
            runtimeSettings => CreateAgentFactory(runtimeSettings, serviceProvider.GetService<ILoggerFactory>()),
            serviceProvider.GetService<ILogger<AiSettingsRefresher>>()));
        services.AddHostedService(serviceProvider => new AiSettingsRefreshService(serviceProvider.GetRequiredService<AiSettingsRefresher>(),
            TimeSpan.FromSeconds(settings.SettingsRefreshSeconds)));
        return services;
    }

    /// <summary>
    /// Creates the agent factory of usable AI settings.
    /// </summary>
    /// <exception cref="ArgumentException">The settings are incomplete or invalid; the message never contains the API key.</exception>
    internal static FwoAgentFactory CreateAgentFactory(AiRuntimeSettings runtimeSettings, ILoggerFactory? loggerFactory)
    {
        return new FwoAgentFactory(runtimeSettings.ToProviderSettings(), runtimeSettings.ToAgentSettings(), loggerFactory);
    }
}
