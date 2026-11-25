using FWO.Ai.Harness.Hosting;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings at startup and again every <see cref="HarnessSettings.SettingsRefreshSeconds"/>, so that a
/// change on the AI settings page of the UI, or of the credential of the API key, applies without a restart.
/// </summary>
public sealed class AiSettingsRefreshService : BackgroundService
{
    private readonly AiSettingsRefresher refresher;
    private readonly TimeSpan refreshInterval;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="refresher">Reads and applies the settings</param>
    /// <param name="refreshInterval">Time between two readings; must be positive</param>
    /// <param name="timeProvider">Clock of the interval; the system clock if null</param>
    public AiSettingsRefreshService(AiSettingsRefresher refresher, TimeSpan refreshInterval, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(refresher);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(refreshInterval, TimeSpan.Zero);
        this.refresher = refresher;
        this.refreshInterval = refreshInterval;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Reads the settings until the service stops; a failed reading is logged by the refresher and retried after the
    /// interval.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await refresher.RefreshAsync(stoppingToken);
                await Task.Delay(refreshInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
