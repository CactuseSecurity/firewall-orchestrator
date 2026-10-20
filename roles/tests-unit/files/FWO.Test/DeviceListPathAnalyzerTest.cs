using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.PathAnalysis;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the device list path analyzers (for each device, only if device available) and the conversion of request
    /// elements into the address ranges of a path analysis.
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

        [Test]
        public void ToRanges_ConvertsSingleAddressesRangesAndNetworksOfTheField()
        {
            List<WfReqElement> elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.1.1", IpEnd = "10.0.1.9" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.2.0/24" },
                new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "192.168.0.1" }
            ];

            List<IPAddressRange> sources = ReqElementIpRanges.ToRanges(elements, ElemFieldType.source);

            Assert.Multiple(() =>
            {
                Assert.That(sources, Has.Count.EqualTo(3));
                Assert.That(sources[0].ToString(), Is.EqualTo("10.0.0.1"));
                Assert.That(sources[1].End.ToString(), Is.EqualTo("10.0.1.9"));
                Assert.That(sources[2].Begin.ToString(), Is.EqualTo("10.0.2.0"));
                Assert.That(ReqElementIpRanges.ToRanges(elements, ElemFieldType.destination), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void ToRanges_SkipsElementsWithoutOrWithInvalidAddress()
        {
            List<WfReqElement> elements =
            [
                new WfReqElement { Field = ElemFieldType.source.ToString(), NetworkId = 4 },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "no address" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.9", IpEnd = "10.0.0.1" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1", IpEnd = "::1" },
                new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1", IpEnd = "garbage" }
            ];

            Assert.That(ReqElementIpRanges.ToRanges(elements, ElemFieldType.source), Is.Empty);
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
