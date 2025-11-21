using FWO.Config.File;
using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Assert = NUnit.Framework.Assert;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ConfigFileTest
    {
        private const string configFileTestPath = "config_file.test";
        private const string privateKeyTestPath = "private_key.test";
        private const string publicKeyTestPath = "public_key.test";

        #region configFiles
        private const string correctConfigFile = @"{
          ""api_hasura_jwt_alg"": ""RS256"",
          ""api_uri"": ""https://127.0.0.1:9443/api/v1/graphqlo/"",
          ""dotnet_mode"": ""Release"",
          ""fworch_home"": ""/usr/local/fworch"",
          ""middleware_native_uri"": ""http://127.0.0.3:8880/"",
          ""middleware_uri"": ""http://127.0.0.1:8880/"",
          ""product_version"": ""500""
        }";

        private const string incorrectSyntaxConfigFile = @"{
          ""api_hasura_jwt_alg"" = RS256,
          api_uri ! ""https://127.0.0.1:9443/api/v1/graphqlo/"",
          ""dotnet_mode"": ""Release""
          123 : ""/usr/local/fworch""
          ""middleware_native_uri"": ""http://127.0.0.3:8880/"",
          ""middleware_uri"": ""http://127.0.0.1:8880/"",
          ""product_version"": ""5.1""
        }";

        private const string missingValueConfigFile = @"{
          ""api_hasura_jwt_alg"": ""RS256"",
          ""dotnet_mode"": ""Release"",
          ""fworch_home"": ""/usr/local/fworch"",
          ""middleware_native_uri"": ""http://127.0.0.3:8880/"",
          ""product_version"": ""500""
        }";
        #endregion

        #region keys
        private const string correctPrivateKey = @"-----BEGIN RSA PRIVATE KEY-----
MIIEoQIBAAKCAQEAh/VQpJIMz1kWAT5KGcZGEwXB6IXs5s820R+9ZJp2ouL6dDmN
WMuGTjlLbDeCj1Y7EQHuWEbKSL8idjfHay7UevZZcVqecwd8TZodz0JaOuBBfxc1
PZWC73YOUjKMLquBE64P9kbkbbEkSwFmDlFEncbhGunuW3EF7G++aMYhfZZP8YaY
hwhjEkH345kAp5+YVthfBuPEeksCUplfMYuZhdByolHm7WzVLXFeyC4oOfBfWUUe
nF7nHr4TW86k9pT1LZTcukPI00BpZzsiz7+1JZ0YoOIIA1u4672Bz2cAR6HkNFB6
3sh2qZwtC0utP3i3yXlDSxD8lQ7A7NYlifRszwIBJQKCAQEAgJvyVnxRToSz81aZ
H0zagLJrUZNxZLYs71VgIOUkHYqZ45BjHKTY/eMrq450lW0+yuYmpognIjhDMY61
uGqRpL+FClyjuOtnvwdoTm8ywhJneDiMTwMNJ7TdHVJo7eCBBMc/iBmL+Q9Zrrwi
ROUXZNDixm6VXWrp7X512LtrydH2EuTtLakpS7z8DHRY20CPR4STRclWoLyRjX6X
V6K7uLHnxvdo8RC8R1yZ5ymqid4Dr5JcRxwZSpHapAyjFiRXRJ7SBWIBAnzpFN1U
/pSat5i6FzfGNOITVmpH6D4Z9gz1wSd+URwkPy54w8/ROnORSxMneEZbWuQL7c9i
p+JMdQKBgQDquwZjlYJqoX8GMzgQ09ATU7Wt2G4fRe2GYh8jwlqsbSK5HqZgdQmt
biwo2jE1lu/eexavMfu2uMFqQq+pe0Av1EqVvd6s6oLv4RJhPxOC0MD4/Il9Bfxv
gXakIXpAOhb2E0duuVyDWRD7l+4Tdy4/eKK6JW0VdQMxehIkySLDzQKBgQCURxkr
lV4dPfPYm+Xw+qiHJT7FCUG3Vz5eaYaxYwE8l0IcQ56p0VF2pwfjsHkhGJIlMzSg
JodQLRlUP8/Lazz7YTSqytGef7XE88JvBzOsB8z0hHyq1BiFbgJlxwovlGdJasla
ubvqQPOqeHqYBtec5DdQyxBh+nmRd13LqPEPCwKBgHiJjajCZZCzy5tmaOYL52Pe
4MEORmMWEjBAOYEQxsc+9ie1yw7v/gzzYsjev1LezjP6BLrUelbpQLoGkY5h4rDG
9d4xEXtjqwWPQMonTzVWcO6PhN9WGdhlE2kKRbJHV+Ylk2JfL4G4HXpHGWPxF7+6
fRN7AKonVKq0ToGXt+gBAoGAEAevsa9Icd0vHlYLBUuH10JL+aAix7Znm4EHpHln
6t/reK1dQsqFWO92eXrcqaHKnDzjGDuEQATgJMig17iQ+JTjGWII0t1fwkPdqyNf
iDgx1T7BXpN4Rca0Jq276XfT0JXsQSjWC3ykuHu6OfLm4Idf7Q8IsKV07SGpRnMg
+rUCgYARFC0N0BWTtB0ZmhhWXL4zWeCmlUV/96EkKD3Qrkj2AFK8Sj3O96U5TXR2
5+uY7nOytVkLEz5cz9+blzXEQxf+MtZ0wC7g5zhAC2Yvfm9YSYh9pNlQgFOEJfKU
QdJmDaQOJ2fLoK7HjP60Lq4qcUdW79BdPJS4njwLp/FmsebraQ==
-----END RSA PRIVATE KEY-----";

        private const string incorrectPrivateKey = @"
lV4dPfPYm+Xw+qiHJT7FCUG3Vz5eaYaxYwE8l0IcQ56p0VF2pwfjsHkhGJIlMzSg
JodQLRlUP8/Lazz7YTSqytGef7XE88JvBzOsB8z0hHyq1BiFbgJlxwovlGdJasla
ubvqQPOqeHqYBtec5DdQyxBh+nmRd13LqPEPCwKBgHiJjajCZZCzy5tmaOYL52Pe
4MEORmMWEjBAOYEQxsc+9ie1yw7v/gzzYsjev1LezjP6BLrUelbpQLoGkY5h4rDG
9d4xEXtjqwWPQMonTzVWcO6PhN9WGdhlE2kKRbJHV+Ylk2JfL4G4HXpHT0JXsQSj
+rUCgYARFC0N0BWTtB0ZmhhWXL4zWeCmlUV/96EkKD3Qrkj2AFK8Sj3O96U5TXR2
5+uY7nOytVkLEz5cz9+blzXEQxf+MtZ0wC7g5zhAC2Yvfm9YSYh9pNlQgFOEJfKU
QdJmDaQOJ2fLoK7HjP60Lq4qcUdW79BdPJS4njwLp/FmsebraQ==ÖQWRSO
-----END KEY-----";

        private const string correctPublicKey = @"-----BEGIN PUBLIC KEY-----
MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCySAocG6CzIphhr9QeIKhloFb3
baVhd0+Ml6+VcrLukwJazkBZ3JDQewkmGIO4Dnw8S9Qn89R8NKUqTOqj81ggjiHx
7zILfqanV75fyGbPtrzcCkWh/JTqu4dLFpuMsRV9GAGVcBU65tDrnxCJUpAUesvN
g5czPipA0It5aJHuRwIDAQAB
-----END PUBLIC KEY-----";

        private const string incorrectPublicKey = @"---- BEGIN
HALOOOOOOOOOOOOÖÖÖÖÖÖÖÖÖÖÄ
aMYhfZZP8YaYhwhjEkH345kAp5+YVthfBuPEeksCUplfMYuZhdByolHm7WzVLXFe
yC4oOfBfWUUenF7nHr4TW86k9pT1LZTcukPI00BpZzsiz7+1JZ0YoOIIA1u4672B
z2cAR6HkNFB63sh2qZwtC0utP3i3yXlDSxD8lQ7A7NYlifRszw==
2 PUBLIC KEY ----";
        #endregion

        [Test]
        public void CorrectConfigFile()
        {
            CreateAndReadConfigFile(0, correctConfigFile);
            ClassicAssert.AreEqual("http://127.0.0.3:8880/", ConfigFile.MiddlewareServerNativeUri);
            ClassicAssert.AreEqual("http://127.0.0.1:8880/", ConfigFile.MiddlewareServerUri);
            ClassicAssert.AreEqual("https://127.0.0.1:9443/api/v1/graphqlo/", ConfigFile.ApiServerUri);
            ClassicAssert.AreEqual("500", ConfigFile.ProductVersion);
        }

        [Test]
        public void IncorrectSyntaxConfigFile()
        {
            Assert.Catch(typeof(TargetInvocationException), () => CreateAndReadConfigFile(1, incorrectSyntaxConfigFile));
        }

        [Test]
        public void MissingValueConfigFile()
        {
            CreateAndReadConfigFile(2, missingValueConfigFile);
            ClassicAssert.AreEqual("http://127.0.0.3:8880/", ConfigFile.MiddlewareServerNativeUri);
            Assert.Catch(typeof(ApplicationException), () => { var _ = ConfigFile.MiddlewareServerUri; });
            Assert.Catch(typeof(ApplicationException), () => { var _ = ConfigFile.ApiServerUri; });
            ClassicAssert.AreEqual("500", ConfigFile.ProductVersion);
        }

        [Test]
        public void CorrectPublicKey()
        {
            CreateAndReadConfigFile(3, correctConfigFile, "", correctPublicKey);
            ClassicAssert.AreEqual(KeyImporter.ExtractKeyFromPem(correctPublicKey, isPrivateKey: false)!.KeyId, ConfigFile.JwtPublicKey.KeyId);
        }

        [Test]
        public void CorrectPrivateKey()
        {
            CreateAndReadConfigFile(4, correctConfigFile, correctPrivateKey, "");
            ClassicAssert.AreEqual(KeyImporter.ExtractKeyFromPem(correctPrivateKey, isPrivateKey: true)!.KeyId, ConfigFile.JwtPrivateKey.KeyId);
        }

        [Test]
        public void IncorrectPublicKey()
        {
            CreateAndReadConfigFile(5, correctConfigFile, "", incorrectPublicKey);
            Assert.Catch(typeof(ApplicationException), () => { var _ = ConfigFile.JwtPublicKey; });
        }

        [Test]
        public void IncorrectPrivateKey()
        {
            CreateAndReadConfigFile(6, correctConfigFile, incorrectPrivateKey, "");
            Assert.Catch(typeof(ApplicationException), () => { var _ = ConfigFile.JwtPrivateKey; });
        }

        [OneTimeTearDown]
        public void OnFinish()
        {
            for (int uniqueId = 0; uniqueId < 7; uniqueId++)
            {
                File.Delete(configFileTestPath + uniqueId);
                File.Delete(privateKeyTestPath + uniqueId);
                File.Delete(publicKeyTestPath + uniqueId);
            }
        }

        private static void CreateAndReadConfigFile(int uniqueId, string fileContent, string privateKey = "", string publicKey = "")
        {
            string uniqueConfigFilePath = configFileTestPath + uniqueId;
            string uniquePrivateKeyTestPath = privateKeyTestPath + uniqueId;
            string uniquepublicKeyTestPath = publicKeyTestPath + uniqueId;
            File.WriteAllText(uniqueConfigFilePath, fileContent);
            File.WriteAllText(uniquePrivateKeyTestPath, privateKey);
            File.WriteAllText(uniquepublicKeyTestPath, publicKey);
            TestHelper.InvokeMethod<ConfigFile, object?>("Read", new object[] { uniqueConfigFilePath, uniquePrivateKeyTestPath, uniquepublicKeyTestPath });
        }
    }
}
