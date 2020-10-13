using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FWO.Logging;

namespace FWO.Config
{
    class KeyImporter
    {
        public static RsaSecurityKey ExtractKeyFromPem(string rawKey, bool isPrivateKey)
        {
            bool isRsaKey;
            string keyText = ExtractKeyFromPemAsString(rawKey, isPrivateKey, out isRsaKey);
            RsaSecurityKey rsaKey = null;

            try
            {
                byte[] keyBytes = Convert.FromBase64String(keyText);
                // creating the RSA key 
                RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
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
                else   // public key
                    provider.ImportSubjectPublicKeyInfo(new ReadOnlySpan<byte>(keyBytes), out _);
                rsaKey = new RsaSecurityKey(provider);
            }
            catch (Exception e)
            {
                Log.WriteError("Extract Key", $"unexpected error while trying to extract rsakey from PEM formatted key {rawKey}.", e);
            }
            return rsaKey;
        }

        public static string ExtractKeyFromPemAsString(string rawKey, bool isPrivateKey, out bool isRsaKey)
        {
            string keyText = null;
            isRsaKey = true;
            rawKey = rawKey.Trim(); // remove trailing empty lines
            Log.WriteDebug("Extract Key", $"AuthClient::ExtractKeyFromPemAsString rawKey={rawKey}");
            try
            {
                // removing armor of PEM file (first and last line)
                List<string> lines = new List<string>(rawKey.Split('\n'));
                var firstline = lines[0];
                if (firstline.Contains("RSA"))
                {
                    isRsaKey = true;
                }
                else
                {
                    isRsaKey = false;
                }
                keyText = string.Join("", lines.GetRange(1, lines.Count - 2).ToArray());
            }
            catch (Exception e)
            {
                Log.WriteError("Extract Key", $"unexpected error while trying to extract rsakey from PEM formatted key {rawKey}.", e);
            }
            Log.WriteDebug("Extract Key", $"AuthClient::ExtractKeyFromPemAsString keyText={keyText}");
            return keyText;
        }
    }
}
