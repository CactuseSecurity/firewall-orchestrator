using FWO.Data.Ai;

namespace FWO.Config.Api.Data
{
    /// <summary>
    /// Copies the AI settings between the global config and an <see cref="AiSettings"/> object, which the AI settings
    /// page edits and validates.
    /// </summary>
    public static class AiSettingsConfigExtensions
    {
        /// <summary>
        /// Returns a copy of the AI settings of the config.
        /// </summary>
        /// <param name="config">Config holding the AI settings</param>
        /// <returns>The AI settings.</returns>
        public static AiSettings GetAiSettings(this ConfigData config)
        {
            ArgumentNullException.ThrowIfNull(config);
            return new AiSettings
            {
                Provider = config.AiProvider,
                Model = config.AiModel,
                Endpoint = config.AiEndpoint,
                CredentialId = config.AiCredentialId,
                AuthMethod = config.AiAuthMethod,
                TokenUrl = config.AiTokenUrl,
                ContextWindowTokens = config.AiContextWindowTokens,
                MaxOutputTokens = config.AiMaxOutputTokens,
                MaxIterationsPerRequest = config.AiMaxIterationsPerRequest,
                MaxConsecutiveErrorsPerRequest = config.AiMaxConsecutiveErrorsPerRequest,
                CompactionReserveTokens = config.AiCompactionReserveTokens,
                ChatEnabled = config.AiChatEnabled,
                ReasonCheckEnabled = config.AiReasonCheckEnabled
            };
        }

        /// <summary>
        /// Sets the AI settings of the config; texts are stored trimmed.
        /// </summary>
        /// <param name="config">Config to change</param>
        /// <param name="settings">AI settings to store</param>
        public static void SetAiSettings(this ConfigData config, AiSettings settings)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(settings);
            config.AiProvider = settings.Provider;
            config.AiModel = settings.Model.Trim();
            config.AiEndpoint = settings.Endpoint.Trim();
            config.AiCredentialId = settings.CredentialId;
            config.AiAuthMethod = settings.AuthMethod;
            config.AiTokenUrl = settings.TokenUrl.Trim();
            config.AiContextWindowTokens = settings.ContextWindowTokens;
            config.AiMaxOutputTokens = settings.MaxOutputTokens;
            config.AiMaxIterationsPerRequest = settings.MaxIterationsPerRequest;
            config.AiMaxConsecutiveErrorsPerRequest = settings.MaxConsecutiveErrorsPerRequest;
            config.AiCompactionReserveTokens = settings.CompactionReserveTokens;
            config.AiChatEnabled = settings.ChatEnabled;
            config.AiReasonCheckEnabled = settings.ReasonCheckEnabled;
        }

        /// <summary>
        /// Tells whether the AI settings of the config reference the given credential.
        /// </summary>
        /// <param name="config">Config holding the AI settings</param>
        /// <param name="credentialId">Id of the credential</param>
        /// <returns>True if the credential holds the API key of the AI provider.</returns>
        public static bool AiSettingsUseCredential(this ConfigData config, int credentialId)
        {
            ArgumentNullException.ThrowIfNull(config);
            return credentialId > 0 && config.AiCredentialId == credentialId;
        }
    }
}
