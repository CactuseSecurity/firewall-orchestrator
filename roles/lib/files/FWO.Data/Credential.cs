using FWO.Basics;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace FWO.Data
{
    public class Credential
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("credential_name"), JsonPropertyName("credential_name")]
        public string Name { get; set; } = "";

        [JsonProperty("is_key_pair"), JsonPropertyName("is_key_pair")]
        public Boolean IsKeyPair { get; set; } = false;

        [JsonProperty("user"), JsonPropertyName("user")]
        public string? ImportUser { get; set; }

        [JsonProperty("secret"), JsonPropertyName("secret")]
        public string Secret { get; set; } = "";

        [JsonProperty("sshPublicKey"), JsonPropertyName("sshPublicKey")]
        public string? PublicKey { get; set; }

        [JsonProperty("additional_credential_id"), JsonPropertyName("additional_credential_id")]
        public int? AdditionalCredentialId { get; set; }

        public Credential()
        { }

        public Credential(Credential cred)
        {
            Id = cred.Id;
            Name = cred.Name;
            IsKeyPair = cred.IsKeyPair;
            ImportUser = cred.ImportUser;
            Secret = cred.Secret;
            PublicKey = cred.PublicKey;
            AdditionalCredentialId = cred.AdditionalCredentialId;
        }
        public Credential(string username, string password)
        {
            ImportUser = username;
            Secret = password;
        }
        public bool Sanitize()
        {
            bool shortened = false;
            Name = Name.SanitizeMand(ref shortened);
            ImportUser = ImportUser.SanitizeOpt(ref shortened);
            PublicKey = PublicKey.SanitizeKeyOpt(ref shortened);
            Secret = Secret.SanitizeKeyMand(ref shortened);

            return shortened;
        }
    }
}
