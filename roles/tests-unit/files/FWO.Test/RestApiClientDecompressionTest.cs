using FWO.Api.Client;
using FWO.Data;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Client;
using NUnit.Framework;
using RestSharp;
using System.Net;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Pins the response decompression of the REST clients. RestSharp decodes every method by
    /// default, Brotli included, so a client created without the explicit setting would hand
    /// responses of remote systems to the Brotli decoder again (CVE-2026-111497).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RestApiClientDecompressionTest
    {
        private sealed class DefaultOptionsClient : RestApiClient
        {
            public DefaultOptionsClient(string baseUrl) : base(baseUrl)
            { }
        }

        [Test]
        public void AllowedDecompression_IsGzipAndDeflateOnly()
        {
            Assert.That(RestApiClient.kAllowedDecompression, Is.EqualTo(DecompressionMethods.GZip | DecompressionMethods.Deflate));
        }

        [Test]
        public void Constructor_DoesNotDecodeBrotli()
        {
            DefaultOptionsClient client = new("https://internal.example/api/");

            AssertBrotliNotDecoded(client);
        }

        [Test]
        public void SetAuthenticationToken_KeepsBrotliDecodingOff()
        {
            DefaultOptionsClient client = new("https://internal.example/api/");

            client.SetAuthenticationToken("header.payload.signature");

            AssertBrotliNotDecoded(client);
        }

        [Test]
        public void MiddlewareClient_DoesNotDecodeBrotli()
        {
            using MiddlewareClient client = new("https://middleware.example/");

            AssertBrotliNotDecoded(client);
        }

        [Test]
        public void ExternalSystemClients_DoNotDecodeBrotli()
        {
            SCClient secureChange = new(new ExternalTicketSystem { Url = "https://securechange.example/", ResponseTimeout = 5 }, true);
            FWO.DeviceAutoDiscovery.FortiManagerClient fortiManager = new(CreateManagement("fortimanager.example", 443), false);

            Assert.Multiple(() =>
            {
                AssertBrotliNotDecoded(secureChange);
                AssertBrotliNotDecoded(fortiManager);
            });
        }

        /// <summary>
        /// Asserts that the RestSharp client of a REST client decodes gzip and deflate, but not Brotli.
        /// </summary>
        /// <param name="client">The client to inspect.</param>
        private static void AssertBrotliNotDecoded(RestApiClient client)
        {
            DecompressionMethods decompression = ReadRestClient(client).Options.AutomaticDecompression;

            Assert.That(decompression, Is.EqualTo(RestApiClient.kAllowedDecompression));
            Assert.That(decompression.HasFlag(DecompressionMethods.Brotli), Is.False);
        }

        /// <summary>
        /// Reads the protected RestSharp client of a REST client.
        /// </summary>
        /// <param name="client">The client to inspect.</param>
        /// <returns>The RestSharp client currently in use.</returns>
        private static RestClient ReadRestClient(RestApiClient client)
        {
            FieldInfo field = typeof(RestApiClient).GetField("restClient", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("RestApiClient no longer has a restClient field.");
            return field.GetValue(client) as RestClient
                ?? throw new InvalidOperationException("restClient did not hold a RestClient.");
        }

        private static Management CreateManagement(string hostname, int port)
        {
            return new Management
            {
                Hostname = hostname,
                Port = port,
                ExportCredential = new Credential("api-user", "unencrypted-secret")
            };
        }
    }
}
