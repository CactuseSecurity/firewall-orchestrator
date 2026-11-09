using FWO.Api.Client;
using FWO.Api.Client.ExceptionHandling;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Keeps the REST API settings of the shared config snapshot current, so the list endpoints read the
/// configured page size at request time instead of the value loaded at startup.
/// </summary>
public sealed class RestApiConfigService : IDisposable
{
    private readonly GlobalConfig globalConfig;
    private readonly ApiSubscription? configSubscription;

    /// <summary>
    /// Initializes a new instance of the type and subscribes to changes of the REST API settings.
    /// </summary>
    public RestApiConfigService(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        this.globalConfig = globalConfig;
        try
        {
            configSubscription = apiConnection.GetSubscription<ConfigItem[]>(
                GraphqlExceptionHandler.Handle,
                OnGlobalConfigChange,
                ConfigQueries.subscribeRestApiConfigChanges);
        }
        catch (Exception exception)
        {
            Log.WriteError("REST API config", "Could not start REST API config subscription.", exception);
        }
    }

    /// <summary>
    /// Applies refreshed REST API config values to the shared config snapshot.
    /// </summary>
    private void OnGlobalConfigChange(ConfigItem[] configItems)
    {
        globalConfig.MergeSubscriptionUpdateHandler(configItems);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        configSubscription?.Dispose();
    }
}
