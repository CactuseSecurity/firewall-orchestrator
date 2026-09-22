using FWO.Api.Client;
using FWO.Data;
using FWO.ExternalSystems.Tufin.SecureChange;
using FWO.Middleware.Client;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Pins which REST clients validate the server certificate. The flag decides whether a
    /// password or a JWT may be handed to whatever answers on the port, so a silent change
    /// of the default has to fail here rather than in production.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RestApiClientCertificateCheckTest
    {
        private sealed class DefaultOptionsClient : RestApiClient
        {
            public DefaultOptionsClient(string baseUrl) : base(baseUrl)
            { }
        }

        private sealed class OptedOutClient : RestApiClient
        {
            public OptedOutClient(string baseUrl) : base(baseUrl, checkCertificates: false)
            { }
        }

        [Test]
        public void Constructor_WithoutExplicitChoice_ChecksCertificates()
        {
            DefaultOptionsClient client = new("https://internal.example/api/");

            Assert.That(ReadCheckCertificates(client), Is.True);
        }

        [Test]
        public void Constructor_WithExplicitOptOut_DoesNotCheckCertificates()
        {
            OptedOutClient client = new("https://appliance.example/api/");

            Assert.That(ReadCheckCertificates(client), Is.False);
        }

        [Test]
        public void MiddlewareClient_ChecksCertificates()
        {
            using MiddlewareClient client = new("https://middleware.example/");

            Assert.That(ReadCheckCertificates(client), Is.True);
        }

        [Test]
        public void ExternalSystemClients_DoNotCheckCertificates()
        {
            SCClient secureChange = new(new ExternalTicketSystem { Url = "https://securechange.example/", ResponseTimeout = 5 });
            FWO.DeviceAutoDiscovery.CheckPointClient checkPoint = new(CreateManagement("checkpoint.example", 443));
            FWO.DeviceAutoDiscovery.FortiManagerClient fortiManager = new(CreateManagement("fortimanager.example", 443));
            FWO.ExternalSystems.CheckPoint.CheckPointClient checkPointTickets =
                new(new ExternalTicketSystem { Url = "https://checkpoint.example/web_api/", ResponseTimeout = 5 },
                    CreateManagement("checkpoint.example", 443));

            Assert.Multiple(() =>
            {
                Assert.That(ReadCheckCertificates(secureChange), Is.False);
                Assert.That(ReadCheckCertificates(checkPoint), Is.False);
                Assert.That(ReadCheckCertificates(fortiManager), Is.False);
                Assert.That(ReadCheckCertificates(checkPointTickets), Is.False);
            });
        }

        /// <summary>
        /// Reads the private certificate checking flag of a REST client.
        /// </summary>
        /// <param name="client">The client to inspect.</param>
        /// <returns>True when the client validates the server certificate.</returns>
        private static bool ReadCheckCertificates(RestApiClient client)
        {
            FieldInfo field = typeof(RestApiClient).GetField("CheckCertificates", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("RestApiClient no longer has a CheckCertificates field.");
            return field.GetValue(client) as bool?
                ?? throw new InvalidOperationException("CheckCertificates did not hold a boolean value.");
        }

        private static Management CreateManagement(string hostname, int port)
        {
            return new Management
            {
                Hostname = hostname,
                Port = port,
                ExportCredential = new ImportCredential("api-user", "unencrypted-secret")
            };
        }
    }
}
