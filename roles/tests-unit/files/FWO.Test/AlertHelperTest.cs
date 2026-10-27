using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Services;
using FWO.Test.Helpers;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Alert data is stored for every role that may read alerts and written to the log, so secrets in it are masked.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    internal class AlertHelperTest
    {
        private const string kSecretMarker = "secret-marker";
        private const string kAlertTitle = "Supermanagement: mds";
        private const string kAlertDescription = "AddManagement";

        [Test]
        public async Task SetAlert_MasksSecretInStoredAndLoggedJsonText()
        {
            AlertApiFake apiConnection = new();
            string jsonData = $"{{\"name\":\"domain-a\",\"import_credential\":{{\"id\":7,\"secret\":\"{kSecretMarker}\"}}}}";

            string writtenLog = await ConsoleOutput.CaptureAsync(() => SetAlert(apiConnection, jsonData));

            string storedJson = apiConnection.StoredJsonData as string ?? "";
            Assert.Multiple(() =>
            {
                Assert.That(storedJson, Does.Not.Contain(kSecretMarker));
                Assert.That(storedJson, Does.Contain("\"id\":7"));
                Assert.That(storedJson, Does.Contain(JsonSecretRedactor.kRedactedValue));
                Assert.That(writtenLog, Does.Contain(kAlertDescription), "the alert is still logged");
                Assert.That(writtenLog, Does.Not.Contain(kSecretMarker));
            });
        }

        [Test]
        public async Task SetAlert_MasksSecretInLoggedObjectData()
        {
            AlertApiFake apiConnection = new();
            object jsonData = new { ticketId = 5, password = kSecretMarker };

            string writtenLog = await ConsoleOutput.CaptureAsync(() => SetAlert(apiConnection, jsonData));

            Assert.Multiple(() =>
            {
                Assert.That(writtenLog, Does.Contain("ticketId"));
                Assert.That(writtenLog, Does.Not.Contain(kSecretMarker));
            });
        }

        [Test]
        public async Task SetAlert_MasksSecretInLog_WhenWritingTheAlertFails()
        {
            AlertApiFake apiConnection = new() { ThrowOnAddAlert = true };
            string jsonData = $"{{\"import_credential\":{{\"secret\":\"{kSecretMarker}\"}}}}";

            string writtenLog = await ConsoleOutput.CaptureAsync(() => SetAlert(apiConnection, jsonData));

            Assert.Multiple(() =>
            {
                Assert.That(writtenLog, Does.Contain(kAlertDescription), "the alert is logged although it could not be stored");
                Assert.That(writtenLog, Does.Not.Contain(kSecretMarker));
            });
        }

        private static Task<long?> SetAlert(ApiConnection apiConnection, object jsonData)
        {
            return AlertHelper.SetAlert(apiConnection, kAlertTitle, kAlertDescription, GlobalConst.kAutodiscovery, AlertCode.Autodiscovery,
                new AlertHelper.AdditionalAlertData { JsonData = jsonData });
        }

        private sealed class AlertApiFake : SimulatedApiConnection
        {
            private static readonly ReturnId[] kAddedAlertIds = [new ReturnId { NewIdLong = 1 }];

            public bool ThrowOnAddAlert { get; init; }

            public object? StoredJsonData { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == MonitorQueries.getOpenAlerts)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Alert>());
                }
                if (query == MonitorQueries.addAlert)
                {
                    if (ThrowOnAddAlert)
                    {
                        throw new InvalidOperationException("alert failed");
                    }
                    StoredJsonData = variables?.GetType().GetProperty("jsonData")?.GetValue(variables);
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = kAddedAlertIds });
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
