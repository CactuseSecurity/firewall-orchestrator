using FWO.Data;
using FWO.NetworkTopology;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Services.PathAnalysis;
using NetTools;
using FWO.Basics.Exceptions;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Runs path analyses for the path analysis REST endpoints and maps their results to the response contract.
/// </summary>
public sealed class PathAnalysisService
{
    private readonly PathAnalyzerFactory pathAnalyzerFactory;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    /// <param name="pathAnalyzerFactory">Selects the analyzer for the requested or the configured algorithm.</param>
    public PathAnalysisService(PathAnalyzerFactory pathAnalyzerFactory)
    {
        this.pathAnalyzerFactory = pathAnalyzerFactory;
    }

    /// <summary>
    /// Runs the path analysis for every combination of a source and a destination of the request and returns
    /// the devices on those paths, restricted by the optional filter.
    /// </summary>
    /// <param name="request">The validated request; the controller rejects invalid requests before calling this.</param>
    /// <returns>
    /// The devices found, each listed once and in no particular order, together with the algorithm and zone
    /// matrix the analysis actually used, whether requested or taken from the configuration.
    /// </returns>
    /// <exception cref="ZoneMatrixUnavailableException">The requested or designated zone matrix does not exist or holds no configured zones.</exception>
    /// <exception cref="ConfigException">No zone matrix was requested and none is designated; the validator normally rejects this beforehand.</exception>
    public async Task<ResolvePathDevicesResponse?> ResolvePathDevicesAsync(ResolvePathDevicesRequest request)
    {
        PathAnalysisRequest pathAnalysisRequest = new()
        {
            Sources = ToRanges(request.Sources),
            Destinations = ToRanges(request.Destinations),
            MatrixId = request.Options.MatrixId
        };

        long? algorithmId = request.Options.Algorithm != null
            && PathAnalysisAlgorithmNames.TryGetId(request.Options.Algorithm, out long requestedId)
                ? requestedId
                : null;

        IPathAnalyzer analyzer = pathAnalyzerFactory.Create(algorithmId);
        PathAnalysisResult result = await analyzer.AnalyzeAsync(pathAnalysisRequest);
        List<PathDeviceResponse> resultDevices = ToDeviceResponses(result.Devices, request.Options.Filter);

        return new ResolvePathDevicesResponse
        {
            Devices = resultDevices,
            Algorithm = PathAnalysisAlgorithmNames.GetName(result.AlgorithmId) ?? string.Empty,
            MatrixId = result.MatrixId
        };
    }

    /// <summary>
    /// Converts validated IP address blocks of the request into address ranges.
    /// </summary>
    /// <remarks>
    /// The request validator has already rejected missing lists, null elements and every invalid block, so a
    /// failure here is a programming error and not the caller's fault; it surfaces as a server error.
    /// </remarks>
    private static List<IPAddressRange> ToRanges(List<IpRangeOrNetworkRequest?>? ipBlocks)
    {
        List<IPAddressRange> ranges = [];
        ipBlocks ??= [];

        for (int index = 0; index < ipBlocks.Count; index++)
        {
            IpRangeOrNetworkRequest? ipBlock = ipBlocks[index]
                ?? throw new InvalidOperationException($"IP block at index {index} passed validation but is null.");
            if (!IpRangeOrNetworkParser.TryParse(ipBlock, out IPAddressRange? range, out string? error))
            {
                throw new InvalidOperationException($"IP block at index {index} passed validation but could not be converted: {error}");
            }

            ranges.Add(range!);
        }
        return ranges;
    }

    /// <summary>
    /// Maps the analysed devices to the response type and applies the optional filter.
    /// </summary>
    private static List<PathDeviceResponse> ToDeviceResponses(IReadOnlyList<PathDevice> devices, ResolvePathDevicesFilter? filter)
    {
        List<PathDeviceResponse> responses = [];
        foreach (PathDevice device in devices)
        {
            if (filter?.Id != null && device.Id != filter.Id)
            {
                continue;
            }

            if (filter?.Name != null && !string.Equals(device.Name, filter.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            responses.Add(new PathDeviceResponse { Id = device.Id, Name = device.Name });
        }

        return responses;
    }
}
