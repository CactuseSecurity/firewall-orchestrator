using FWO.Encryption;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Text;
using Assert = NUnit.Framework.Assert;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class AesDecryptTest
    {

        private static readonly Random random = new Random();
        private const string printableChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        public static string GenerateRandomString(int minLength, int maxLength)
        {
            int length = random.Next(minLength, maxLength + 1);
            StringBuilder stringBuilder = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                char randomChar = printableChars[random.Next(printableChars.Length)];
                stringBuilder.Append(randomChar);
            }

            return stringBuilder.ToString();
        }
        [Test]
        public void TestEncryptDecryptRandomData()
        {
            string tempKey = GenerateRandomString(32,32);
            string randomPlaintext = GenerateRandomString(15, 100);
            string encryptedString = AesEnc.Encrypt(randomPlaintext, tempKey);
            string decryptedString = AesEnc.Decrypt(encryptedString, tempKey);
            ClassicAssert.AreEqual(randomPlaintext, decryptedString);
        }

    }
}
