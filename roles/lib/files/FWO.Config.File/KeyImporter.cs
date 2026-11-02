using FWO.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace FWO.Config.File
{
    public class KeyImporter
    {
        public static RsaSecurityKey? ExtractKeyFromPem(string rawKey, bool isPrivateKey)
        {
            (string keyText, bool isRsaKey) = ExtractKeyFromPemAsString(rawKey);
            RsaSecurityKey? rsaKey = null;

            try
            {
                byte[] keyBytes = Convert.FromBase64String(keyText);
                // creating the RSA key 
                RSACryptoServiceProvider provider = new(3072);  // keylength just given for satisfying sonarcube checks (as we import the key later)
                if (isPrivateKey)
                {
                    if (isRsaKey)
                    {   // ubuntu 20.04:
                        provider.ImportRSAPrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                    else
                    {   // debian 10:
                        provider.ImportPkcs8PrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                }
                else // public key
                {
                    if (isRsaKey)
                    {
                        provider.ImportRSAPublicKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                    else
                    {
                        provider.ImportSubjectPublicKeyInfo(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                }

                rsaKey = new RsaSecurityKey(provider);
            }
            catch (Exception exception)
            {
                // The key material must never reach the log, so only its kind is named.
                string keyKind = isPrivateKey ? "private" : "public";
                Log.WriteError("Extract Key", $"unexpected error while trying to extract rsa {keyKind} key from PEM formatted key.", exception);
                throw;
            }

            return rsaKey;
        }

        private static (string key, bool isRsa) ExtractKeyFromPemAsString(string rawKey)
        {
            bool isRsaKey = true;
            string keyText = "";

            rawKey = rawKey.Trim(); // remove trailing and leading empty lines

            try
            {
                // removing armor of PEM file (first and last line)
                List<string> lines = new List<string>(rawKey.Split('\n'));
                isRsaKey = lines[0].Contains("RSA");
                keyText = string.Join("", lines.GetRange(1, lines.Count - 2).ToArray());
            }
            catch (Exception exception)
            {
                Log.WriteError("Key extraction", "Error while trying to read key from file.", exception);
                throw;
            }

            Log.WriteDebug("Key extraction", "Key was succesfully extracted.");
            return (keyText, isRsaKey);
        }
    }
}
