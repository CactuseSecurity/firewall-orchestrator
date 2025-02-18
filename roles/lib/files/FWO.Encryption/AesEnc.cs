using System.Security.Cryptography;
using System.Text;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Encryption
{
	public static class AesEnc
	{
		public static string GetMainKey()
		{
			string mainKey = File.ReadAllText(GlobalConst.kMainKeyFile);
			mainKey = mainKey.TrimEnd('\n');    // remove linke break
			return mainKey;
		}

		public static string Encrypt(string plaintext, string key)
		{
			return CustomAesCbcEncryptBase64(plaintext, key);
		}

		public static string Decrypt(string encryptedDataString, string key)
		{
			string decryptedText;
			try
			{
				decryptedText = CustomAesCbcDecryptBase64(encryptedDataString, key);
				return decryptedText;
			}
			catch
			{
				throw new ArgumentException("Could not decrypt.");
            // catch (Exception decryptException)
			// {
			// 	// throw new ArgumentException("Could not decrypt.");
			// 	Log.WriteWarning("AesEnc", $"Could not decrypt.");
			}
			// return encryptedDataString;
		}
		
		public static string CustomAesCbcEncryptBase64(string plaintext, string key)
		{
			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(key);
				aes.GenerateIV();
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;

				using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
				{
					byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
					byte[] encryptedBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

					// Combine IV and encrypted text
					byte[] ivAndEncrypted = new byte[aes.IV.Length + encryptedBytes.Length];
					Array.Copy(aes.IV, ivAndEncrypted, aes.IV.Length);
					Array.Copy(encryptedBytes, 0, ivAndEncrypted, aes.IV.Length, encryptedBytes.Length);

					return Convert.ToBase64String(ivAndEncrypted);
				}
			}
		}

		public static string CustomAesCbcDecryptBase64(string ciphertext, string key)
		{
			byte[] encryptedBytes = Convert.FromBase64String(ciphertext);

			// IV size for AES-CBC is typically 16 bytes
			int ivSize = 16;
			byte[] iv = new byte[ivSize];
			byte[] encryptedText = new byte[encryptedBytes.Length - ivSize];

			// Extract IV from the beginning of the ciphertext
			Array.Copy(encryptedBytes, 0, iv, 0, ivSize);
			Array.Copy(encryptedBytes, ivSize, encryptedText, 0, encryptedText.Length);

			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(key);
				aes.IV = iv;
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;

				using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
				{
					byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedText, 0, encryptedText.Length);
					return Encoding.UTF8.GetString(decryptedBytes);
				}
			}
		}

	}
}
