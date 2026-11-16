using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.PathAnalysis;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests how the routing based path analysis combines the source and destination elements of a request task into
/// paths, merges the devices found on them, and handles unusable addresses and failing queries.
/// </summary>
[TestFixture]
[Parallelizable]
internal class RoutingBasedPathAnalyzerTest
{
    private static readonly Device kCore = new() { Id = 1, Name = "fw-core" };
    private static readonly Device kEdge = new() { Id = 2, Name = "fw-edge" };
    private static readonly Device kBranch = new() { Id = 3, Name = "fw-branch" };

    private static readonly List<(string Source, string Destination)> kAllPairs =
    [
        ("10.1.0.1", "10.2.0.1"),
        ("10.1.0.1", "10.2.0.2"),
        ("10.1.0.2", "10.2.0.1"),
        ("10.1.0.2", "10.2.0.2")
    ];
    private static readonly List<(string Source, string Destination)> kSinglePair = [("10.1.0.1", "10.2.0.1")];
    private static readonly List<int> kMergedDeviceIds = [1, 2, 3];
    private static readonly List<string> kFirstNetworkAddress = ["10.1.0.0"];

    /// <summary>Verifies every source is combined with every destination, one query per pair.</summary>
    [Test]
    public async Task GetAllDevices_TwoSourcesAndTwoDestinations_AnalyzesEveryPair()
    {
        PathApiConn apiConnection = new();
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.1"),
            Element(ElemFieldType.source, "10.1.0.2"),
            Element(ElemFieldType.destination, "10.2.0.1"),
            Element(ElemFieldType.destination, "10.2.0.2")
        ];

        await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.AnalyzedPairs, Is.EquivalentTo(kAllPairs));
            Assert.That(apiConnection.Queries.All(query => query == PathAnalysisQueries.pathAnalysis), Is.True);
        });
    }

    /// <summary>Verifies a device on several paths is returned once, in the order it was first found.</summary>
    [Test]
    public async Task GetAllDevices_DeviceOnSeveralPaths_IsReturnedOnce()
    {
        PathApiConn apiConnection = new();
        apiConnection.DevicesBySource["10.1.0.1"] = [kCore, kEdge];
        apiConnection.DevicesBySource["10.1.0.2"] = [kEdge, kBranch];
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.1"),
            Element(ElemFieldType.source, "10.1.0.2"),
            Element(ElemFieldType.destination, "10.2.0.1")
        ];

        List<Device> devices = await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.That(devices.Select(device => device.Id), Is.EqualTo(kMergedDeviceIds));
    }

    /// <summary>Verifies only the first address of a network is analyzed.</summary>
    [Test]
    public async Task GetAllDevices_SourceNetwork_AnalyzesItsFirstAddress()
    {
        PathApiConn apiConnection = new();
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.0/24"),
            Element(ElemFieldType.destination, "10.2.0.1")
        ];

        await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.That(apiConnection.AnalyzedPairs.Select(pair => pair.Source), Is.EqualTo(kFirstNetworkAddress));
    }

    /// <summary>Verifies service elements and elements without an address are ignored.</summary>
    [Test]
    public async Task GetAllDevices_ServiceAndAddresslessElements_AreIgnored()
    {
        PathApiConn apiConnection = new();
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.1"),
            new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = null },
            new WfReqElement { Field = ElemFieldType.service.ToString(), Cidr = new Cidr("10.9.0.1") },
            Element(ElemFieldType.destination, "10.2.0.1")
        ];

        await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.That(apiConnection.AnalyzedPairs, Is.EqualTo(kSinglePair));
    }

    /// <summary>
    /// Verifies an element with an unparseable address sends no query for its paths while the other paths are
    /// still analyzed, and no exception reaches the caller.
    /// </summary>
    [Test]
    public async Task GetAllDevices_InvalidAddress_SkipsItsPathsAndAnalyzesTheOthers()
    {
        PathApiConn apiConnection = new();
        apiConnection.DevicesBySource["10.1.0.1"] = [kCore];
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.1"),
            new WfReqElement { Field = ElemFieldType.source.ToString(), Cidr = new Cidr() },
            Element(ElemFieldType.destination, "10.2.0.1")
        ];

        List<Device> devices = await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.AnalyzedPairs, Is.EqualTo(kSinglePair));
            Assert.That(devices.Single().Id, Is.EqualTo(kCore.Id));
        });
    }

    /// <summary>Verifies a failing query is logged and the devices of the other paths are still returned.</summary>
    [Test]
    public async Task GetAllDevices_FailingQuery_ReturnsDevicesOfTheOtherPaths()
    {
        PathApiConn apiConnection = new() { FailingSource = "10.1.0.2" };
        apiConnection.DevicesBySource["10.1.0.1"] = [kCore];
        List<WfReqElement> elements =
        [
            Element(ElemFieldType.source, "10.1.0.1"),
            Element(ElemFieldType.source, "10.1.0.2"),
            Element(ElemFieldType.destination, "10.2.0.1")
        ];

        List<Device> devices = await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.That(devices.Single().Id, Is.EqualTo(kCore.Id));
    }

    /// <summary>Verifies no query is sent when there is no destination to pair a source with.</summary>
    [Test]
    public async Task GetAllDevices_NoDestination_SendsNoQuery()
    {
        PathApiConn apiConnection = new();
        List<WfReqElement> elements = [Element(ElemFieldType.source, "10.1.0.1")];

        List<Device> devices = await RoutingBasedPathAnalyzer.GetAllDevices(elements, apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(devices, Is.Empty);
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    /// <summary>Verifies the device names of one path are joined in the order returned.</summary>
    [Test]
    public async Task GetDeviceNamesForSinglePath_JoinsDeviceNames()
    {
        PathApiConn apiConnection = new();
        apiConnection.DevicesBySource["10.1.0.1"] = [kCore, new Device { Id = 4, Name = null }, kEdge];

        string deviceNames = await RoutingBasedPathAnalyzer.GetDeviceNamesForSinglePath("10.1.0.1", "10.2.0.1", apiConnection);

        Assert.That(deviceNames, Is.EqualTo("fw-core, , fw-edge"));
    }

    /// <summary>Verifies an unparseable address yields an empty result without a query.</summary>
    [Test]
    public async Task GetDeviceNamesForSinglePath_InvalidAddress_ReturnsEmpty()
    {
        PathApiConn apiConnection = new();

        string deviceNames = await RoutingBasedPathAnalyzer.GetDeviceNamesForSinglePath("banana", "10.2.0.1", apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(deviceNames, Is.Empty);
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    private static WfReqElement Element(ElemFieldType field, string address)
    {
        return new WfReqElement { Field = field.ToString(), Cidr = new Cidr(address) };
    }

    /// <summary>
    /// Records every analyzed path and answers with the devices configured for its source address.
    /// </summary>
    private sealed class PathApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];

        public List<(string Source, string Destination)> AnalyzedPairs { get; } = [];

        public Dictionary<string, List<Device>> DevicesBySource { get; } = [];

        public string? FailingSource { get; init; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            string source = variables?.GetType().GetProperty("src")?.GetValue(variables)?.ToString() ?? string.Empty;
            string destination = variables?.GetType().GetProperty("dst")?.GetValue(variables)?.ToString() ?? string.Empty;
            AnalyzedPairs.Add((source, destination));
            if (source == FailingSource)
            {
                throw new InvalidOperationException("simulated query failure");
            }

            List<Device> devices = DevicesBySource.GetValueOrDefault(source) ?? [];
            return Task.FromResult((QueryResponseType)(object)devices);
        }
    }
}
