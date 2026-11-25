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
        /// Returns a copy of the AI settings of the config. A function with an unreadable role filter is returned switched
        /// off, as the AI harness treats it, see <see cref="AiFunctionRoles.TryParse"/>. Unreadable additional headers are
        /// returned as none, so that storing the settings replaces them; the harness refuses them until then.
        /// </summary>
        /// <param name="config">Config holding the AI settings</param>
        /// <returns>The AI settings.</returns>
        public static AiSettings GetAiSettings(this ConfigData config)
        {
            ArgumentNullException.ThrowIfNull(config);
            bool chatRolesReadable = AiFunctionRoles.TryParse(config.AiChatRoles, out List<string> chatRoles);
            bool reasonCheckRolesReadable = AiFunctionRoles.TryParse(config.AiReasonCheckRoles, out List<string> reasonCheckRoles);
            _ = AiCustomHeaderList.TryParse(config.AiCustomHeaders, out List<AiCustomHeader> customHeaders);
            return new AiSettings
            {
                Provider = config.AiProvider,
                Model = config.AiModel,
                Endpoint = config.AiEndpoint,
                CredentialId = config.AiCredentialId,
                AuthMethod = config.AiAuthMethod,
                TokenUrl = config.AiTokenUrl,
                CustomHeaders = customHeaders,
                ContextWindowTokens = config.AiContextWindowTokens,
                MaxOutputTokens = config.AiMaxOutputTokens,
                RequestTimeoutSeconds = config.AiRequestTimeoutSeconds,
                MaxIterationsPerRequest = config.AiMaxIterationsPerRequest,
                MaxConsecutiveErrorsPerRequest = config.AiMaxConsecutiveErrorsPerRequest,
                CompactionReserveTokens = config.AiCompactionReserveTokens,
                ChatEnabled = config.AiChatEnabled && chatRolesReadable,
                ReasonCheckEnabled = config.AiReasonCheckEnabled && reasonCheckRolesReadable,
                ChatRoles = chatRoles,
                ReasonCheckRoles = reasonCheckRoles
            };
        }

        /// <summary>
        /// Sets the AI settings of the config; texts are stored trimmed, additional headers without blank rows.
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
            config.AiCustomHeaders = AiCustomHeaderList.Serialize(settings.CustomHeaders);
            config.AiContextWindowTokens = settings.ContextWindowTokens;
            config.AiMaxOutputTokens = settings.MaxOutputTokens;
            config.AiRequestTimeoutSeconds = settings.RequestTimeoutSeconds;
            config.AiMaxIterationsPerRequest = settings.MaxIterationsPerRequest;
            config.AiMaxConsecutiveErrorsPerRequest = settings.MaxConsecutiveErrorsPerRequest;
            config.AiCompactionReserveTokens = settings.CompactionReserveTokens;
            config.AiChatEnabled = settings.ChatEnabled;
            config.AiReasonCheckEnabled = settings.ReasonCheckEnabled;
            config.AiChatRoles = AiFunctionRoles.Serialize(settings.ChatRoles);
            config.AiReasonCheckRoles = AiFunctionRoles.Serialize(settings.ReasonCheckRoles);
        }

        /// <summary>
        /// Tells whether a user holding the given roles may chat with the AI assistant: the chat is switched on and its
        /// role filter allows one of the roles. An unreadable role filter allows nobody.
        /// </summary>
        /// <param name="config">Config holding the AI settings</param>
        /// <param name="userRoles">Roles of the user</param>
        public static bool MayUseAiChat(this ConfigData config, IEnumerable<string> userRoles)
        {
            ArgumentNullException.ThrowIfNull(config);
            return config.AiChatEnabled && IsAllowedByRoleFilter(config.AiChatRoles, userRoles);
        }

        /// <summary>
        /// Tells whether a user holding the given roles may let the AI assistant check the functional reason: the reason
        /// check is switched on and its role filter allows one of the roles. An unreadable role filter allows nobody.
        /// </summary>
        /// <param name="config">Config holding the AI settings</param>
        /// <param name="userRoles">Roles of the user</param>
        public static bool MayUseAiReasonCheck(this ConfigData config, IEnumerable<string> userRoles)
        {
            ArgumentNullException.ThrowIfNull(config);
            return config.AiReasonCheckEnabled && IsAllowedByRoleFilter(config.AiReasonCheckRoles, userRoles);
        }

        /// <summary>
        /// Tells whether the stored role filter allows one of the roles; false if it cannot be read.
        /// </summary>
        private static bool IsAllowedByRoleFilter(string storedRoles, IEnumerable<string> userRoles)
        {
            bool readable = AiFunctionRoles.TryParse(storedRoles, out List<string> allowedRoles);
            return readable && AiFunctionRoles.IsAllowed(allowedRoles, userRoles);
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
