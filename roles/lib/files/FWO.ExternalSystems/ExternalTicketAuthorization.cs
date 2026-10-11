using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Encryption;
using System.Text;

namespace FWO.ExternalSystems
{
    /// <summary>
    /// Builds the Authorization header string of external ticket systems from their credential. The header is only
    /// held in memory for the calls to the ticket system and never stored.
    /// </summary>
    public static class ExternalTicketAuthorization
    {
        private const string kBasicScheme = "Basic ";

        /// <summary>
        /// Builds the header string for basic authentication.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password in plain text.</param>
        /// <returns>"Basic " followed by the base64 encoded "username:password".</returns>
        public static string BuildBasicHeader(string username, string password)
        {
            return kBasicScheme + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        }

        /// <summary>
        /// Builds the basic authentication header string from a credential.
        /// </summary>
        /// <param name="credential">The credential, its secret encrypted with the main key.</param>
        /// <param name="mainKey">The main key.</param>
        /// <returns>The header string in plain text.</returns>
        /// <exception cref="InvalidOperationException">If the credential is a key pair or its secret cannot be decrypted.</exception>
        public static string BuildHeader(Credential credential, string mainKey)
        {
            if (credential.IsKeyPair)
            {
                throw new InvalidOperationException($"Credential '{credential.Name}' is a key pair and cannot be used for basic authentication.");
            }
            if (!AesEnc.TryDecrypt(credential.Secret, mainKey, out string password))
            {
                throw new InvalidOperationException($"Could not decrypt secret of credential '{credential.Name}'.");
            }
            return BuildBasicHeader(credential.ImportUser ?? "", password);
        }

        /// <summary>
        /// Decrypts the legacy header string stored with an external request created before ticket systems referenced credentials.
        /// </summary>
        /// <param name="encryptedHeader">The header string encrypted with the main key.</param>
        /// <param name="mainKey">The main key.</param>
        /// <returns>The header string in plain text, empty if no header string is given.</returns>
        /// <exception cref="InvalidOperationException">If the header string cannot be decrypted.</exception>
        public static string DecryptHeader(string? encryptedHeader, string mainKey)
        {
            if (string.IsNullOrEmpty(encryptedHeader))
            {
                return "";
            }
            if (!AesEnc.TryDecrypt(encryptedHeader, mainKey, out string header))
            {
                throw new InvalidOperationException("Could not decrypt the authorization of the external ticket system.");
            }
            return header;
        }

        /// <summary>
        /// Sets the Authorization header string of a ticket system right before calls to it: built from the current
        /// credential of the ticket system, so that changed passwords apply to open requests as well, or decrypted from
        /// the legacy header string of an older request without credential.
        /// </summary>
        /// <param name="system">The ticket system, as configured or as stored with an external request.</param>
        /// <param name="apiConnection">Connection allowed to read credential secrets (middleware server).</param>
        /// <exception cref="InvalidOperationException">If the credential is missing or unusable, or the header cannot be decrypted.</exception>
        public static async Task ResolveHeader(ExternalTicketSystem system, ApiConnection apiConnection)
        {
            if (system.CredentialId is > 0)
            {
                Credential credential = await apiConnection.SendQueryAsync<Credential?>(DeviceQueries.getCredentialById, new { id = system.CredentialId })
                    ?? throw new InvalidOperationException($"Credential {system.CredentialId} of external ticket system '{system.Name}' not found.");
                system.AuthorizationHeader = BuildHeader(credential, AesEnc.GetMainKey());
            }
            else
            {
                system.AuthorizationHeader = string.IsNullOrEmpty(system.Authorization) ? "" : DecryptHeader(system.Authorization, AesEnc.GetMainKey());
            }
        }
    }
}
