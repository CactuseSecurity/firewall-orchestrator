using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Services.PathAnalysis;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the device list path analyzers (for each device, first device (if available)).
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class DeviceListPathAnalyzerTest
    {
        private static readonly List<int> kAllDeviceIds = [3, 5, 7];
        private static readonly List<int> kFirstDeviceId = [3];
        private static readonly List<Device> kNoDevices = [];

        [Test]
        public async Task ForEachDevice_ReturnsEveryGatewayInQueryOrder()
        {
            PathAnalysisResult result = await new DeviceListPathAnalyzer(new DeviceApiConnection(CreateDevices()), false)
                .AnalyzeAsync(new PathAnalysisRequest());

            Assert.Multiple(() =>
            {
                Assert.That(result.Devices.Select(device => device.Id), Is.EqualTo(kAllDeviceIds));
                Assert.That(result.Devices[0].Name, Is.EqualTo("fw-3"));
                Assert.That(result.AlgorithmId, Is.EqualTo(GlobalConst.kPathAnalysisAlgorithmForEachDevice));
            });
        }

        [Test]
        public async Task FirstDevice_ReturnsOnlyTheFirstGateway()
        {
            PathAnalysisResult result = await new DeviceListPathAnalyzer(new DeviceApiConnection(CreateDevices()), true)
                .AnalyzeAsync(new PathAnalysisRequest());

            Assert.Multiple(() =>
            {
                Assert.That(result.Devices.Select(device => device.Id), Is.EqualTo(kFirstDeviceId));
                Assert.That(result.AlgorithmId, Is.EqualTo(GlobalConst.kPathAnalysisAlgorithmFirstDevice));
            });
        }

        [Test]
        public async Task FirstDevice_WithoutGateways_ReturnsNoDevice()
        {
            PathAnalysisResult result = await new DeviceListPathAnalyzer(new DeviceApiConnection(kNoDevices), true)
                .AnalyzeAsync(new PathAnalysisRequest());

            Assert.That(result.Devices, Is.Empty);
        }

        private static List<Device> CreateDevices()
        {
            return [.. kAllDeviceIds.Select(id => new Device { Id = id, Name = $"fw-{id}" })];
        }

        private sealed class DeviceApiConnection(List<Device> devices) : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getDeviceDetails)
                {
                    return Task.FromResult((QueryResponseType)(object)devices);
                }
                throw new NotImplementedException();
            }
        }
    }
}
