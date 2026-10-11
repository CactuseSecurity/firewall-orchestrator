using FWO.Basics.Exceptions;
using FWO.Data;
using FWO.Encryption;
using System.Text;

namespace FWO.ExternalSystems
{
    /// <summary>
    /// Builds the Authorization header string of external ticket systems from their credential.
    /// The header is only stored encrypted with the main key and decrypted right before sending.
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
        /// Builds the basic authentication header string from a credential and encrypts it.
        /// </summary>
        /// <param name="credential">The credential, its secret encrypted with the main key.</param>
        /// <param name="mainKey">The main key.</param>
        /// <returns>The header string encrypted with the main key.</returns>
        /// <exception cref="ProcessingFailedException">If the secret of the credential cannot be decrypted.</exception>
        public static string BuildEncryptedHeader(Credential credential, string mainKey)
        {
            if (!AesEnc.TryDecrypt(credential.Secret, mainKey, out string password))
            {
                throw new ProcessingFailedException($"Could not decrypt secret of credential '{credential.Name}'.");
            }
            return AesEnc.Encrypt(BuildBasicHeader(credential.ImportUser ?? "", password), mainKey);
        }

        /// <summary>
        /// Decrypts the header string stored with an external request.
        /// </summary>
        /// <param name="encryptedHeader">The header string encrypted with the main key.</param>
        /// <param name="mainKey">The main key.</param>
        /// <returns>The header string in plain text, empty if no header string is given.</returns>
        /// <exception cref="ProcessingFailedException">If the header string cannot be decrypted.</exception>
        public static string DecryptHeader(string? encryptedHeader, string mainKey)
        {
            if (string.IsNullOrEmpty(encryptedHeader))
            {
                return "";
            }
            if (!AesEnc.TryDecrypt(encryptedHeader, mainKey, out string header))
            {
                throw new ProcessingFailedException("Could not decrypt the authorization of the external ticket system.");
            }
            return header;
        }

        /// <summary>
        /// Decrypts the header string stored with an external request with the main key of the installation.
        /// </summary>
        /// <param name="encryptedHeader">The header string encrypted with the main key.</param>
        /// <returns>The header string in plain text, empty if no header string is given.</returns>
        public static string DecryptHeader(string? encryptedHeader)
        {
            return string.IsNullOrEmpty(encryptedHeader) ? "" : DecryptHeader(encryptedHeader, AesEnc.GetMainKey());
        }
    }
}
