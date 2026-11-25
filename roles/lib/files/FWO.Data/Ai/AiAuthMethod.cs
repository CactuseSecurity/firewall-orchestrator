namespace FWO.Data.Ai
{
    /// <summary>
    /// How the AI harness (FWO.Ai.Harness) authenticates at the AI provider. Stored by name in the global config
    /// (<see cref="AiSettings.kAuthMethodKey"/>).
    /// </summary>
    public enum AiAuthMethod
    {
        /// <summary>The secret of the credential is the API key of the provider.</summary>
        ApiKey,

        /// <summary>
        /// The harness gets an access token with the OAuth 2.0 client credentials grant from the token URL
        /// (<see cref="AiSettings.TokenUrl"/>) and sends it as bearer token instead of an API key; the credential holds the
        /// client id as user name and the client secret as secret. For an OpenAI compatible server only.
        /// </summary>
        OAuth2ClientCredentials
    }
}
