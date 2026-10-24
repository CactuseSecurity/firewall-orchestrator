using System.Security.Cryptography;
using System.Text;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Encryption
{
    /// <summary>
    /// Encrypts the stored secrets (credentials, LDAP and email passwords) with the installation's main key.
    /// </summary>
    /// <remarks>
    /// Secrets are written in the authenticated format "fwo-v2:" + base64(IV | AES-256-CBC ciphertext | HMAC-SHA256 tag)
    /// (encrypt-then-MAC). The encryption and the authentication key are both derived from the main key with HMAC-SHA256.
    /// The tag lets decryption with a wrong main key fail reliably, whereas the legacy format (base64 of IV | AES-CBC
    /// ciphertext with the main key itself) only detected it through an invalid padding and accepted about 1 in 250 wrong
    /// keys (#5476). The legacy format can still be read; such secrets are rewritten in the authenticated format when saved
    /// again and by the database installation (encryptPasswords). The format is shared with fwo_encrypt.py (importer) and
    /// fworch-encryption.sql (database).
    /// </remarks>
    public static class AesEnc
    {
        private const string mainKeyFileEnvVar = "FWO_MAIN_KEY_FILE";
        private const string kAuthenticatedPrefix = "fwo-v2:";
        private const int kIvSize = 16;
        private const int kBlockSize = 16;
        private const int kTagSize = 32;
        private static readonly byte[] kEncryptionKeyLabel = Encoding.UTF8.GetBytes("fwo-secret-encryption-v2");
        private static readonly byte[] kAuthenticationKeyLabel = Encoding.UTF8.GetBytes("fwo-secret-authentication-v2");
        private static readonly List<int> kValidMainKeySizes = [16, 24, 32];

        /// <summary>
        /// Encrypts a secret with the main key, unless it is already encrypted with it.
        /// A secret encrypted in the legacy format is re-encrypted in the authenticated format.
        /// </summary>
        /// <param name="secret">The secret in plain text or encrypted with the main key.</param>
        /// <returns>The secret encrypted in the authenticated format.</returns>
        public static string TryEncrypt(string secret)
        {
            string mainKey = GetMainKey();

            // only encrypt secret if it was not already encrypted
            if (TryDecrypt(secret, mainKey, out string decryptedText))
            {
                return IsAuthenticatedFormat(secret) ? secret : Encrypt(decryptedText, mainKey);
            }

            return Encrypt(secret, mainKey);
        }

        public static string TryDecrypt(string secret, bool returnOrigin = false, string logMessageTitle = "", string logText = "", bool onlyWarning = false)
        {
            string mainKey;
            try
            {
                mainKey = GetMainKey();
            }
            catch (Exception exception)
            {
                HandleDecryptLog(logMessageTitle, logText, onlyWarning, exception);
                return returnOrigin ? secret : "";
            }

            if (TryDecrypt(secret, mainKey, out string decryptedText))
            {
                return decryptedText;
            }

            HandleDecryptLog(logMessageTitle, logText, onlyWarning);
            return returnOrigin ? secret : "";
        }

        /// <summary>
        /// Decrypts a secret in the authenticated or the legacy format.
        /// </summary>
        /// <param name="encryptedDataString">The encrypted secret.</param>
        /// <param name="key">The main key.</param>
        /// <param name="decryptedText">The secret in plain text, empty if decryption failed.</param>
        /// <returns>True if the secret could be decrypted with the key.</returns>
        public static bool TryDecrypt(string encryptedDataString, string key, out string decryptedText)
        {
            decryptedText = string.Empty;

            if (string.IsNullOrEmpty(encryptedDataString))
            {
                return false;
            }

            try
            {
                string? decrypted = IsAuthenticatedFormat(encryptedDataString)
                    ? DecryptAuthenticated(encryptedDataString[kAuthenticatedPrefix.Length..], key)
                    : DecryptLegacy(encryptedDataString, key);
                if (decrypted == null)
                {
                    return false;
                }

                decryptedText = decrypted;
                return true;
            }
            catch (Exception)
            {
                decryptedText = string.Empty;
                return false;
            }
        }

        private static void HandleDecryptLog(string logMessageTitle, string logText, bool onlyWarning, Exception? exception = null)
        {
            if (string.IsNullOrEmpty(logMessageTitle))
            {
                return;
            }

            string message = string.IsNullOrEmpty(logText) ? "Could not decrypt secret." : logText;

            if (onlyWarning)
            {
                Log.WriteWarning(logMessageTitle, message);
            }
            else
            {
                if (exception != null)
                {
                    Log.WriteError(logMessageTitle, message, exception);
                }
                else
                {
                    Log.WriteError(logMessageTitle, message);
                }
            }
        }

        /// <summary>
        /// Reads the installation's main key, the key every stored secret is encrypted with.
        /// </summary>
        /// <remarks>
        /// The path may be overridden through the FWO_MAIN_KEY_FILE environment variable, the
        /// same mechanism ConfigFile and Log already use for their host paths. This exists so
        /// tests can exercise real encryption without an installed key at the fixed location,
        /// which is otherwise readable by the FWO service account alone. A deployed service
        /// never sets the variable and keeps using the installed key.
        /// </remarks>
        /// <returns>The main key, without trailing whitespace.</returns>
        public static string GetMainKey()
        {
            try
            {
                string mainKeyFile = Environment.GetEnvironmentVariable(mainKeyFileEnvVar) is string configuredFile
                    && !string.IsNullOrEmpty(configuredFile)
                        ? configuredFile
                        : GlobalConst.kMainKeyFile;
                string mainKey = File.ReadAllText(mainKeyFile);
                mainKey = mainKey.TrimEnd();    // remove trailing whitespace
                return mainKey;
            }
            catch (Exception e)
            {
                Log.WriteError("Main Key File", "Main key file could not be read.", e);
                throw;
            }
        }

        /// <summary>
        /// Encrypts a secret in the authenticated format with a random IV.
        /// </summary>
        /// <param name="plaintext">The secret in plain text.</param>
        /// <param name="key">The main key, 16, 24 or 32 bytes long.</param>
        /// <returns>The encrypted secret.</returns>
        /// <exception cref="CryptographicException">If the main key has an invalid length.</exception>
        public static string Encrypt(string plaintext, string key)
        {
            byte[] mainKey = GetMainKeyBytes(key);
            byte[] iv = RandomNumberGenerator.GetBytes(kIvSize);

            using Aes aes = Aes.Create();
            aes.Key = DeriveKey(mainKey, kEncryptionKeyLabel);
            byte[] ciphertext = aes.EncryptCbc(Encoding.UTF8.GetBytes(plaintext), iv, PaddingMode.PKCS7);

            // IV | ciphertext | HMAC-SHA256 tag over IV and ciphertext
            int authenticatedLength = kIvSize + ciphertext.Length;
            byte[] payload = new byte[authenticatedLength + kTagSize];
            iv.CopyTo(payload, 0);
            ciphertext.CopyTo(payload, kIvSize);
            HMACSHA256.HashData(DeriveKey(mainKey, kAuthenticationKeyLabel), payload.AsSpan(0, authenticatedLength), payload.AsSpan(authenticatedLength));

            return kAuthenticatedPrefix + Convert.ToBase64String(payload);
        }

        /// <summary>
        /// Decrypts a secret in the authenticated or the legacy format.
        /// </summary>
        /// <param name="encryptedDataString">The encrypted secret.</param>
        /// <param name="key">The main key.</param>
        /// <returns>The secret in plain text, empty if it could not be decrypted with the key.</returns>
        public static string Decrypt(string encryptedDataString, string key)
        {
            return TryDecrypt(encryptedDataString, key, out string decryptedText)
                ? decryptedText
                : string.Empty;
        }

        private static bool IsAuthenticatedFormat(string encryptedDataString)
        {
            return encryptedDataString.StartsWith(kAuthenticatedPrefix, StringComparison.Ordinal);
        }

        private static byte[] GetMainKeyBytes(string key)
        {
            byte[] mainKey = Encoding.UTF8.GetBytes(key);
            if (!kValidMainKeySizes.Contains(mainKey.Length))
            {
                throw new CryptographicException("The main key must be 16, 24 or 32 bytes long.");
            }
            return mainKey;
        }

        private static byte[] DeriveKey(byte[] mainKey, byte[] label)
        {
            return HMACSHA256.HashData(mainKey, label);
        }

        private static string? DecryptAuthenticated(string base64Payload, string key)
        {
            byte[] mainKey = GetMainKeyBytes(key);
            if (!TryDecodeBase64String(base64Payload, out byte[] payload))
            {
                return null;
            }

            int ciphertextLength = payload.Length - kIvSize - kTagSize;
            if (ciphertextLength < kBlockSize || ciphertextLength % kBlockSize != 0)
            {
                return null;
            }

            // verify the tag before decrypting anything: a wrong main key or modified data fails here
            int authenticatedLength = kIvSize + ciphertextLength;
            byte[] expectedTag = HMACSHA256.HashData(DeriveKey(mainKey, kAuthenticationKeyLabel), payload.AsSpan(0, authenticatedLength));
            if (!CryptographicOperations.FixedTimeEquals(expectedTag, payload.AsSpan(authenticatedLength)))
            {
                return null;
            }

            using Aes aes = Aes.Create();
            aes.Key = DeriveKey(mainKey, kEncryptionKeyLabel);
            byte[] plaintext = aes.DecryptCbc(payload.AsSpan(kIvSize, ciphertextLength), payload.AsSpan(0, kIvSize), PaddingMode.PKCS7);
            return Encoding.UTF8.GetString(plaintext);
        }

        private static string? DecryptLegacy(string encryptedDataString, string key)
        {
            return TryDecodeBase64String(encryptedDataString, out byte[] encryptedBytes)
                ? CustomAesCbcDecrypt(encryptedBytes, key)
                : null;
        }

        private static bool TryDecodeBase64String(string ciphertext, out byte[] decodedBytes)
        {
            decodedBytes = Array.Empty<byte>();

            if (string.IsNullOrWhiteSpace(ciphertext))
            {
                return false;
            }

            byte[] buffer = new byte[ciphertext.Length];
            if (!Convert.TryFromBase64String(ciphertext, buffer, out int bytesWritten))
            {
                return false;
            }

            decodedBytes = new byte[bytesWritten];
            Array.Copy(buffer, decodedBytes, bytesWritten);
            return true;
        }

        // legacy format: AES-CBC with the main key itself, a wrong key is only detected by an invalid padding
        private static string? CustomAesCbcDecrypt(byte[] encryptedBytes, string key)
        {
            // IV size for AES-CBC is typically 16 bytes
            int ivSize = 16;
            if (encryptedBytes.Length < ivSize)
            {
                return null;
            }
            byte[] iv = new byte[ivSize];
            byte[] encryptedText = new byte[encryptedBytes.Length - ivSize];

            // Extract IV from the beginning of the ciphertext
            Array.Copy(encryptedBytes, 0, iv, 0, ivSize);
            Array.Copy(encryptedBytes, ivSize, encryptedText, 0, encryptedText.Length);

            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            int blockSizeBytes = aes.BlockSize / 8;
            if (encryptedText.Length % blockSizeBytes != 0)
            {
                return null;
            }

            try
            {
                using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedText, 0, encryptedText.Length);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
