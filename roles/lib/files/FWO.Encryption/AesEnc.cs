using System.Security.Cryptography;
using System.Text;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Encryption
{
    public static class AesEnc
    {
        public static string TryEncrypt(string secret)
        {
            string mainKey = GetMainKey();

            // only encrypt secret if it was not already encrypted
            if (TryDecrypt(secret, mainKey, out _))
            {
                return secret;
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

        public static bool TryDecrypt(string encryptedDataString, string key, out string decryptedText)
        {
            decryptedText = string.Empty;

            if (string.IsNullOrEmpty(encryptedDataString))
            {
                return false;
            }

            try
            {
                decryptedText = CustomAesCbcDecryptBase64(encryptedDataString, key);
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

        public static string GetMainKey()
        {
            try
            {
                string mainKey = File.ReadAllText(GlobalConst.kMainKeyFile);
                mainKey = mainKey.TrimEnd();    // remove trailing whitespace
                return mainKey;
            }
            catch (Exception e)
            {
                Log.WriteError("Main Key File", "Main key file could not be read.", e);
                throw;
            }
        }

        public static string Encrypt(string plaintext, string key)
        {
            return CustomAesCbcEncryptBase64(plaintext, key);
        }

        public static string Decrypt(string encryptedDataString, string key)
        {
            return TryDecrypt(encryptedDataString, key, out string decryptedText)
                ? decryptedText
                : string.Empty;
        }

        private static string CustomAesCbcEncryptBase64(string plaintext, string key)
        {
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Combine IV and encrypted text
            byte[] ivAndEncrypted = new byte[aes.IV.Length + encryptedBytes.Length];
            Array.Copy(aes.IV, ivAndEncrypted, aes.IV.Length);
            Array.Copy(encryptedBytes, 0, ivAndEncrypted, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(ivAndEncrypted);
        }

        private static string CustomAesCbcDecryptBase64(string ciphertext, string key)
        {
            byte[] encryptedBytes = Convert.FromBase64String(ciphertext);

            // IV size for AES-CBC is typically 16 bytes
            int ivSize = 16;
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

            using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedText, 0, encryptedText.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}
