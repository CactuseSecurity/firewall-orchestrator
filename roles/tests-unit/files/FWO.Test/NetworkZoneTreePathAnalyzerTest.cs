using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.NetworkTopology;
using FWO.Services.PathAnalysis;
using NetTools;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the network zone tree analyzer around the algorithm: which zone matrix it loads, how it reports a
/// matrix it cannot use, and what it puts into the result.
/// </summary>
[TestFixture]
[Parallelizable]
internal class NetworkZoneTreePathAnalyzerTest
{
    private const int kRequestedMatrixId = 3;
    private const int kDesignatedMatrixId = 7;
    private const int kNoDesignatedMatrix = 0;

    /// <summary>Verifies a requested matrix is loaded and reported, not the designated one.</summary>
    [Test]
    public async Task AnalyzeAsync_WithRequestedMatrix_LoadsAndReportsRequestedMatrix()
    {
        PathAnalysisMatrixApiConnection apiConnection = PathAnalysisTestData.CreateApiConnection();

        PathAnalysisResult result = await CreateAnalyzer(apiConnection, kDesignatedMatrixId)
            .AnalyzeAsync(CreateRequest(kRequestedMatrixId));

        Assert.Multiple(() =>
        {
            Assert.That(result.MatrixId, Is.EqualTo(kRequestedMatrixId));
            Assert.That(apiConnection.QueriedVariables, Has.All.Contain(kRequestedMatrixId.ToString()));
            Assert.That(apiConnection.QueriedVariables, Has.None.Contain(kDesignatedMatrixId.ToString()));
        });
    }

    /// <summary>Verifies the designated matrix is loaded and reported when the request names none.</summary>
    [Test]
    public async Task AnalyzeAsync_WithoutRequestedMatrix_LoadsAndReportsDesignatedMatrix()
    {
        PathAnalysisMatrixApiConnection apiConnection = PathAnalysisTestData.CreateApiConnection();

        PathAnalysisResult result = await CreateAnalyzer(apiConnection, kDesignatedMatrixId).AnalyzeAsync(CreateRequest(null));

        Assert.Multiple(() =>
        {
            Assert.That(result.MatrixId, Is.EqualTo(kDesignatedMatrixId));
            Assert.That(apiConnection.QueriedVariables, Has.Count.EqualTo(4));
            Assert.That(apiConnection.QueriedVariables, Has.All.Contain(kDesignatedMatrixId.ToString()));
        });
    }

    /// <summary>Verifies the result reports the algorithm, the segments and the devices of the paths.</summary>
    [Test]
    public async Task AnalyzeAsync_ReportsAlgorithmSegmentsAndDevices()
    {
        PathAnalysisResult result = await CreateAnalyzer(PathAnalysisTestData.CreateApiConnection(), kDesignatedMatrixId)
            .AnalyzeAsync(CreateRequest(null));

        Assert.Multiple(() =>
        {
            Assert.That(result.AlgorithmId, Is.EqualTo(GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree));
            Assert.That(result.Segments, Has.Count.EqualTo(1));
            Assert.That(result.Devices.Select(device => device.Name), Is.EquivalentTo(new List<string>
            {
                PathAnalysisTestData.kAccessAName,
                PathAnalysisTestData.kCoreName,
                PathAnalysisTestData.kAccessBName
            }));
        });
    }

    /// <summary>
    /// Verifies a device on several paths is listed once. Both paths of the request pass fw-access-b, and the
    /// path from zone A also passes fw-core.
    /// </summary>
    [Test]
    public async Task AnalyzeAsync_WithDeviceOnSeveralPaths_ListsDeviceOnce()
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(PathAnalysisTestData.kInZoneA));
        sources.Add(IPAddressRange.Parse(PathAnalysisTestData.kInZoneB));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(PathAnalysisTestData.kInZoneB));
        PathAnalysisRequest request = new() { Sources = sources, Destinations = destinations };

        PathAnalysisResult result = await CreateAnalyzer(PathAnalysisTestData.CreateApiConnection(), kDesignatedMatrixId)
            .AnalyzeAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Segments, Has.Count.EqualTo(2));
            Assert.That(result.Devices, Has.Count.EqualTo(3));
            Assert.That(result.Devices.Count(device => device.Id == PathAnalysisTestData.kAccessBDeviceId), Is.EqualTo(1));
        });
    }

    /// <summary>Verifies a missing matrix id and a missing designated matrix fail before the api is queried.</summary>
    [Test]
    public void AnalyzeAsync_WithoutAnyMatrix_ThrowsConfigExceptionWithoutQuery()
    {
        PathAnalysisMatrixApiConnection apiConnection = PathAnalysisTestData.CreateApiConnection();
        NetworkZoneTreePathAnalyzer analyzer = CreateAnalyzer(apiConnection, kNoDesignatedMatrix);

        Assert.Multiple(() =>
        {
            Assert.That(async () => await analyzer.AnalyzeAsync(CreateRequest(null)), Throws.InstanceOf<ConfigException>());
            Assert.That(apiConnection.QueriedVariables, Is.Empty);
        });
    }

    /// <summary>Verifies a matrix without any zone, as for an id that does not exist, cannot be used.</summary>
    [Test]
    public void AnalyzeAsync_WithMatrixWithoutZones_ThrowsZoneMatrixUnavailable()
    {
        NetworkZoneTreePathAnalyzer analyzer = CreateAnalyzer(PathAnalysisTestData.CreateEmptyApiConnection(), kDesignatedMatrixId);

        ZoneMatrixUnavailableException? exception = Assert.ThrowsAsync<ZoneMatrixUnavailableException>(
            async () => await analyzer.AnalyzeAsync(CreateRequest(kRequestedMatrixId)));

        Assert.That(exception!.MatrixId, Is.EqualTo(kRequestedMatrixId));
    }

    /// <summary>
    /// Verifies a matrix holding only the auto calculated zones cannot be used either. The algorithm would put
    /// every address into those zones and silently report no device.
    /// </summary>
    [Test]
    public void AnalyzeAsync_WithOnlyAutoCalculatedZones_ThrowsZoneMatrixUnavailable()
    {
        NetworkZoneTreePathAnalyzer analyzer = CreateAnalyzer(PathAnalysisTestData.CreateAutoCalculatedOnlyApiConnection(), kDesignatedMatrixId);

        ZoneMatrixUnavailableException? exception = Assert.ThrowsAsync<ZoneMatrixUnavailableException>(
            async () => await analyzer.AnalyzeAsync(CreateRequest(null)));

        Assert.That(exception!.MatrixId, Is.EqualTo(kDesignatedMatrixId));
    }

    private static NetworkZoneTreePathAnalyzer CreateAnalyzer(PathAnalysisMatrixApiConnection apiConnection, int designatedZoneMatrixId)
    {
        SimulatedGlobalConfig globalConfig = new() { DesignatedZoneMatrixId = designatedZoneMatrixId };
        return new NetworkZoneTreePathAnalyzer(apiConnection, globalConfig);
    }

    private static PathAnalysisRequest CreateRequest(int? matrixId)
    {
        List<IPAddressRange> sources = [];
        sources.Add(IPAddressRange.Parse(PathAnalysisTestData.kInZoneA));
        List<IPAddressRange> destinations = [];
        destinations.Add(IPAddressRange.Parse(PathAnalysisTestData.kInZoneB));
        return new PathAnalysisRequest { Sources = sources, Destinations = destinations, MatrixId = matrixId };
    }
}
