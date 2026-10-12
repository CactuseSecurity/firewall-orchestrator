using FWO.Api.Client;
using FWO.Basics;
using FWO.NetworkTopology;
using FWO.Services.PathAnalysis;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the selection of a path analyzer from the configured algorithm id, and the behaviour of
    /// the analyzer used when no algorithm is selected.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class PathAnalyzerFactoryTest
    {
        private const long kUnknownAlgorithmId = 99;
        private const long kUnsetAlgorithmId = 0;

        /// <summary>Verifies the none algorithm selects the analyzer that does not search.</summary>
        [Test]
        public void Create_WithNoneAlgorithm_ReturnsNoPathAnalyzer()
        {
            IPathAnalyzer analyzer = CreateFactory(GlobalConst.kPathAnalysisAlgorithmNone).Create();

            Assert.That(analyzer, Is.InstanceOf<NoPathAnalyzer>());
        }

        /// <summary>Verifies the network zone tree algorithm selects its own analyzer.</summary>
        [Test]
        public void Create_WithNetworkZoneTreeAlgorithm_ReturnsNetworkZoneTreePathAnalyzer()
        {
            IPathAnalyzer analyzer = CreateFactory(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree).Create();

            Assert.That(analyzer, Is.InstanceOf<NetworkZoneTreePathAnalyzer>());
        }

        /// <summary>
        /// Verifies an id that no analyzer implements falls back to the none analyzer instead of
        /// throwing. An id can reach the config through a seeded row that the factory does not map yet.
        /// </summary>
        [TestCase(kUnknownAlgorithmId)]
        [TestCase(kUnsetAlgorithmId)]
        public void Create_WithUnmappedAlgorithmId_FallsBackToNoPathAnalyzer(long algorithmId)
        {
            IPathAnalyzer analyzer = CreateFactory(algorithmId).Create();

            Assert.That(analyzer, Is.InstanceOf<NoPathAnalyzer>());
        }

        /// <summary>
        /// Verifies each call builds a new analyzer. The network zone tree analyzer loads and indexes
        /// matrix data per run, so a shared instance would carry state between unrelated requests.
        /// </summary>
        [Test]
        public void Create_CalledTwice_ReturnsSeparateInstances()
        {
            PathAnalyzerFactory factory = CreateFactory(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree);

            IPathAnalyzer first = factory.Create();
            IPathAnalyzer second = factory.Create();

            Assert.That(first, Is.Not.SameAs(second));
        }

        /// <summary>
        /// Verifies selecting an analyzer does not query the api. The stub connection throws on every
        /// call, so a request during selection would fail this test.
        /// </summary>
        [Test]
        public void Create_DoesNotUseApiConnection()
        {
            PathAnalyzerFactory factory = CreateFactory(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree);

            Assert.That(() => factory.Create(), Throws.Nothing);
        }

        /// <summary>Verifies the none analyzer reports the none algorithm id.</summary>
        [Test]
        public async Task NoPathAnalyzer_ReportsNoneAlgorithmId()
        {
            PathAnalysisResult result = await new NoPathAnalyzer().AnalyzeAsync(new PathAnalysisRequest());

            Assert.That(result.AlgorithmId, Is.EqualTo(GlobalConst.kPathAnalysisAlgorithmNone));
        }

        /// <summary>
        /// Verifies the none analyzer reports neither devices nor segments, so a caller can tell a
        /// switched off analysis from an analysis that found nothing.
        /// </summary>
        [Test]
        public async Task NoPathAnalyzer_ReportsNeitherDevicesNorSegments()
        {
            PathAnalysisResult result = await new NoPathAnalyzer().AnalyzeAsync(new PathAnalysisRequest());

            Assert.Multiple(() =>
            {
                Assert.That(result.Devices, Is.Empty);
                Assert.That(result.Segments, Is.Empty);
            });
        }

        /// <summary>Verifies the none analyzer ignores the ranges of the request instead of resolving them.</summary>
        [Test]
        public async Task NoPathAnalyzer_WithPopulatedRequest_StillReportsNothing()
        {
            List<NetTools.IPAddressRange> sources = [];
            sources.Add(NetTools.IPAddressRange.Parse("10.1.0.1"));
            List<NetTools.IPAddressRange> destinations = [];
            destinations.Add(NetTools.IPAddressRange.Parse("10.2.0.1"));
            PathAnalysisRequest request = new() { Sources = sources, Destinations = destinations, MatrixId = 1 };

            PathAnalysisResult result = await new NoPathAnalyzer().AnalyzeAsync(request);

            Assert.Multiple(() =>
            {
                Assert.That(result.Devices, Is.Empty);
                Assert.That(result.Segments, Is.Empty);
            });
        }

        private static PathAnalyzerFactory CreateFactory(long algorithmId)
        {
            SimulatedGlobalConfig globalConfig = new() { PathAnalysisAlgorithm = algorithmId };
            return new PathAnalyzerFactory(new UnusedApiConnection(), globalConfig);
        }

        /// <summary>
        /// Api connection that fails on every call. Selecting an analyzer must not talk to the api,
        /// so any use during Create would surface here.
        /// </summary>
        private sealed class UnusedApiConnection : ApiConnection
        {
            public override void SetAuthHeader(string jwt) => throw new NotSupportedException();

            public override void SetRole(string role) => throw new NotSupportedException();

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) =>
                throw new NotSupportedException();

            public override void SwitchBack() => throw new NotSupportedException();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null) =>
                throw new NotSupportedException();

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(
                string query, object? variables = null, string? operationName = null) =>
                throw new NotSupportedException();

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription,
                object? variables = null,
                string? operationName = null) =>
                throw new NotSupportedException();

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) =>
                throw new NotSupportedException();

            public override void DisposeSubscriptions<T>() => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
            }
        }
    }
}
