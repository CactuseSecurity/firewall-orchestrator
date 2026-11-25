namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings and the credential of the AI provider from the API.
/// </summary>
public interface IAiConfigReader
{
    /// <summary>
    /// Reads the global config items of the AI settings.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <returns>The config values by config key; a key that is not stored is missing.</returns>
    Task<IReadOnlyDictionary<string, string?>> ReadSettingsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the credential the AI settings reference.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <returns>The credential with its encrypted secret; null if the settings reference none or no existing one.</returns>
    Task<AiCredentialValue?> ReadCredentialAsync(CancellationToken cancellationToken);
}
