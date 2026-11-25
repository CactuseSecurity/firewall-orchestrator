using FWO.Data.Ai;
using FWO.Encryption;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Reads the AI settings of the UI and decrypts the secret of the credential they reference (the API key, or the client
/// secret of OAuth 2.0), with the main key of the host. Settings that cannot be used are returned with their problem
/// rather than thrown, so that they reach the
/// log and the failing runs; only a failure to read them at all is thrown.
/// </summary>
public sealed class FwoAiSettingsSource : IAiSettingsSource
{
    private readonly IAiConfigReader configReader;
    private readonly Func<string> mainKeyProvider;

    /// <summary>
    /// Creates the source.
    /// </summary>
    /// <param name="configReader">Reads the settings and the credential from the API</param>
    /// <param name="mainKeyProvider">Returns the main key the secrets are encrypted with; the main key file of the host if null</param>
    public FwoAiSettingsSource(IAiConfigReader configReader, Func<string>? mainKeyProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configReader);
        this.configReader = configReader;
        this.mainKeyProvider = mainKeyProvider ?? AesEnc.GetMainKey;
    }

    /// <summary>
    /// Reads the AI settings; for a provider requiring a credential also the credential and decrypts its secret.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reading</param>
    /// <exception cref="Exception">The settings or the credential could not be read, or the main key is missing.</exception>
    public async Task<AiRuntimeSettings> LoadAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> configValues = await configReader.ReadSettingsAsync(cancellationToken);
        AiSettings settings = AiSettings.FromConfigValues(configValues);
        if (!settings.IsConfigured)
        {
            return new AiRuntimeSettings(settings);
        }
        List<AiSettingsProblem> problems = settings.Validate();
        if (problems.Count > 0)
        {
            return new AiRuntimeSettings(settings, problem: $"The AI settings are invalid: {string.Join(", ", problems)}.");
        }
        if (!settings.RequiresCredential)
        {
            return new AiRuntimeSettings(settings);
        }
        AiCredentialValue? credential = await configReader.ReadCredentialAsync(cancellationToken);
        return ResolveCredential(settings, credential);
    }

    /// <summary>
    /// Decrypts the secret of the credential of the settings: the API key, or for OAuth 2.0 the client secret, whose
    /// client id is the user name of the credential.
    /// </summary>
    /// <exception cref="Exception">The main key is missing.</exception>
    private AiRuntimeSettings ResolveCredential(AiSettings settings, AiCredentialValue? credential)
    {
        string secretName = settings.UsesOAuth ? "the OAuth client secret" : "the API key";
        if (credential == null || credential.Id != settings.CredentialId)
        {
            return new AiRuntimeSettings(settings, problem: $"The credential {settings.CredentialId} of the AI provider was not found.");
        }
        if (credential.IsKeyPair)
        {
            return new AiRuntimeSettings(settings, problem: $"The credential {credential.Id} of the AI provider is a key pair, it needs {secretName} as secret.");
        }
        string clientId = credential.Username.Trim();
        if (settings.UsesOAuth && clientId.Length == 0)
        {
            return new AiRuntimeSettings(settings, problem: $"The credential {credential.Id} of the AI provider has no user name, it needs the OAuth client id as user name.");
        }
        bool decrypted = AesEnc.TryDecrypt(credential.Secret, mainKeyProvider(), out string secret);
        if (!decrypted || string.IsNullOrWhiteSpace(secret))
        {
            return new AiRuntimeSettings(settings, problem: $"The secret of the credential {credential.Id} of the AI provider could not be decrypted.");
        }
        return new AiRuntimeSettings(settings, secret.Trim(), clientId: settings.UsesOAuth ? clientId : null);
    }
}
