using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Ai.Harness.Settings;

/// <summary>
/// Global config item returned by the query getAiSettings.
/// </summary>
public sealed class AiConfigValue
{
    /// <summary>Config key, see FWO.Data.Ai.AiSettings.kKeys.</summary>
    [JsonProperty("config_key"), JsonPropertyName("config_key")]
    public string Key { get; set; } = "";

    /// <summary>Config value as stored.</summary>
    [JsonProperty("config_value"), JsonPropertyName("config_value")]
    public string? Value { get; set; }
}

/// <summary>
/// Credential of the AI provider returned by the query getAiProviderCredential (view v_ai_provider_credential): the API
/// key, or the client id and secret of OAuth 2.0.
/// A class rather than a record on purpose: the generated ToString of a record would print the secret.
/// </summary>
public sealed class AiCredentialValue
{
    /// <summary>Id of the credential.</summary>
    [JsonProperty("id"), JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Whether the credential is a key pair, which has no API key.</summary>
    [JsonProperty("is_key_pair"), JsonPropertyName("is_key_pair")]
    public bool IsKeyPair { get; set; }

    /// <summary>Secret of the credential, encrypted with the main key: the API key, or the OAuth client secret. Never logged.</summary>
    [JsonProperty("secret"), JsonPropertyName("secret")]
    public string Secret { get; set; } = "";

    /// <summary>User name of the credential: the OAuth client id; not used for an API key.</summary>
    [JsonProperty("username"), JsonPropertyName("username")]
    public string Username { get; set; } = "";
}
