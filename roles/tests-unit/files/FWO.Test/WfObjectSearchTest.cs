using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class WfObjectSearchTest
    {
        private const int kManagementId = 7;

        private sealed class RecordingApiConnection : SimulatedApiConnection
        {
            public string? LastQuery { get; private set; }
            public JsonElement LastVariables { get; private set; }
            public List<string>? LastRoles { get; private set; }
            public int QueryCount { get; private set; }
            public object Result { get; set; } = new List<NetworkObject>();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                QueryCount++;
                LastQuery = query;
                LastVariables = JsonSerializer.SerializeToElement(variables);
                return Task.FromResult((QueryResponseType)Result);
            }

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                LastRoles = targetRoleList;
            }
        }

        [Test]
        public async Task SearchNetworkObjects_SkipsTextsShorterThanTheMinimum()
        {
            RecordingApiConnection apiConnection = new();

            List<NetworkObject> hits = await new WfObjectSearch(apiConnection, new ClaimsPrincipal()).SearchNetworkObjects(kManagementId, " ab ");

            Assert.Multiple(() =>
            {
                Assert.That(hits, Is.Empty);
                Assert.That(apiConnection.QueryCount, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task SearchNetworkObjects_SearchesByNamePreferringTheRequesterRole()
        {
            List<NetworkObject> expected = [new() { Id = 1, Name = "srv_web01" }];
            RecordingApiConnection apiConnection = new() { Result = expected };

            List<NetworkObject> hits = await new WfObjectSearch(apiConnection, new ClaimsPrincipal()).SearchNetworkObjects(kManagementId, "web_0");

            Assert.Multiple(() =>
            {
                Assert.That(hits, Is.SameAs(expected));
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ObjectQueries.searchNetworkObjectsForRequestByName));
                Assert.That(apiConnection.LastRoles?.FirstOrDefault(), Is.EqualTo(Roles.Requester));
                Assert.That(apiConnection.LastVariables.GetProperty("mgmId").GetInt32(), Is.EqualTo(kManagementId));
                Assert.That(apiConnection.LastVariables.GetProperty("pattern").GetString(), Is.EqualTo("%web\\_0%"));
                Assert.That(apiConnection.LastVariables.GetProperty("limit").GetInt32(), Is.EqualTo(WfObjectTaskHelper.kSearchLimit));
                Assert.That(apiConnection.LastVariables.GetProperty("objTypeIds").GetArrayLength(), Is.EqualTo(WfObjectTaskHelper.NetworkObjectTypeIds.Count));
            });
        }

        [TestCase("10.1.1.5")]
        [TestCase("10.1.2.0/24")]
        [TestCase("2001:db8::1")]
        public async Task SearchNetworkObjects_SearchesAnIpAddressExactly(string ip)
        {
            RecordingApiConnection apiConnection = new();

            await new WfObjectSearch(apiConnection, new ClaimsPrincipal()).SearchNetworkObjects(kManagementId, ip);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ObjectQueries.searchNetworkObjectsForRequestByIp));
                Assert.That(apiConnection.LastVariables.GetProperty("ip").GetString(), Is.EqualTo(ip));
            });
        }

        [Test]
        public async Task SearchServices_SearchesAPortEvenWhenShort()
        {
            RecordingApiConnection apiConnection = new() { Result = new List<NetworkService>() };

            await new WfObjectSearch(apiConnection, new ClaimsPrincipal()).SearchServices(kManagementId, "22");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ObjectQueries.searchNetworkServicesForRequestByPort));
                Assert.That(apiConnection.LastVariables.GetProperty("port").GetInt32(), Is.EqualTo(22));
                Assert.That(apiConnection.LastRoles?.FirstOrDefault(), Is.EqualTo(Roles.Requester));
            });
        }

        [Test]
        public async Task SearchServices_SearchesByNameAndSkipsShortTexts()
        {
            RecordingApiConnection apiConnection = new() { Result = new List<NetworkService>() };
            WfObjectSearch search = new(apiConnection, new ClaimsPrincipal());

            List<NetworkService> shortHits = await search.SearchServices(kManagementId, "ht");
            int queriesAfterShortText = apiConnection.QueryCount;
            await search.SearchServices(kManagementId, "https");

            Assert.Multiple(() =>
            {
                Assert.That(shortHits, Is.Empty);
                Assert.That(queriesAfterShortText, Is.EqualTo(0));
                Assert.That(apiConnection.LastQuery, Is.EqualTo(ObjectQueries.searchNetworkServicesForRequestByName));
                Assert.That(apiConnection.LastVariables.GetProperty("pattern").GetString(), Is.EqualTo("%https%"));
            });
        }

        [Test]
        public async Task SearchServices_SearchesAnOutOfRangeNumberAsName()
        {
            RecordingApiConnection apiConnection = new() { Result = new List<NetworkService>() };

            await new WfObjectSearch(apiConnection, new ClaimsPrincipal()).SearchServices(kManagementId, "70000");

            Assert.That(apiConnection.LastQuery, Is.EqualTo(ObjectQueries.searchNetworkServicesForRequestByName));
        }

        [TestCase("web", "%web%")]
        [TestCase("100%", "%100\\%%")]
        [TestCase("a\\b", "%a\\\\b%")]
        public void ToContainsPattern_EscapesWildcards(string text, string expected)
        {
            Assert.That(WfObjectSearch.ToContainsPattern(text), Is.EqualTo(expected));
        }
    }
}
