namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Provides the AI settings the harness runs its agent with.
/// </summary>
public interface IAiSettingsSource
{
    /// <summary>
    /// Reads the current AI settings.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <returns>The settings; their <see cref="AiRuntimeSettings.Problem"/> tells why they cannot be used.</returns>
    /// <exception cref="Exception">The settings could not be read, e.g. the API is not reachable.</exception>
    Task<AiRuntimeSettings> LoadAsync(CancellationToken cancellationToken);
}
