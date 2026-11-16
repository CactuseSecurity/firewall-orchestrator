using System.Reflection;
using System.Runtime.Loader;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Services.PathAnalysis;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the path analysis queries: the static initialization surfaces a missing query file, and the routing based
/// path analysis passes the source and destination of a path to the matching arguments of the
/// <c>devices_in_path</c> database function.
/// </summary>
[TestFixture]
[NonParallelizable]
internal class PathAnalysisQueriesTest
{
    private const string kBaseDirectoryEnvironmentVariable = "FWO_BASE_DIR";
    private const string kQueryTypeName =
        "FWO.Api.Client.Queries.PathAnalysisQueries";
    private const string kQueryFieldName =
        "getPathAnalysisAlgorithms";

    /// <summary>
    /// Walks an exception and all its inner exceptions, outermost first.
    /// </summary>
    private static IEnumerable<Exception> UnwrapExceptions(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            yield return current;
        }
    }

    /// <summary>
    /// Verifies that a missing GraphQL file is logged and rethrown
    /// by the static query initialization.
    /// </summary>
#if DEBUG
    [Test]
    public void Initialization_WhenQueryFileIsMissing_RethrowsException()
    {
        string? originalBaseDirectory =
            Environment.GetEnvironmentVariable(
                kBaseDirectoryEnvironmentVariable);

        string temporaryBaseDirectory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{nameof(PathAnalysisQueriesTest)}-{Guid.NewGuid():N}");

        string pathAnalysisDirectory = Path.Combine(
            temporaryBaseDirectory,
            "fwo-api-calls",
            "path_analysis");

        Directory.CreateDirectory(pathAnalysisDirectory);

        AssemblyLoadContext loadContext = new(
            nameof(PathAnalysisQueriesTest),
            isCollectible: true);

        try
        {
            Environment.SetEnvironmentVariable(
                kBaseDirectoryEnvironmentVariable,
                temporaryBaseDirectory);

            Assembly isolatedAssembly =
                loadContext.LoadFromAssemblyPath(
                    typeof(PathAnalysisQueries).Assembly.Location);

            Type queryType = isolatedAssembly.GetType(
                kQueryTypeName,
                throwOnError: true)!;

            FieldInfo queryField = queryType.GetField(
                kQueryFieldName,
                BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingFieldException(
                    kQueryTypeName,
                    kQueryFieldName);

            Exception exception = Assert.Catch(() => queryField.GetValue(null))!;

            Assert.That(
                UnwrapExceptions(exception).Any(inner => inner is FileNotFoundException),
                Is.True,
                "static initialization must surface the missing query file instead of swallowing it");

        }
        finally
        {
            Environment.SetEnvironmentVariable(
                kBaseDirectoryEnvironmentVariable,
                originalBaseDirectory);

            loadContext.Unload();

            if (Directory.Exists(temporaryBaseDirectory))
            {
                Directory.Delete(
                    temporaryBaseDirectory,
                    recursive: true);
            }
        }
    }
#endif

    /// <summary>Verifies the query maps <c>$src</c> to <c>c_source</c> and <c>$dst</c> to <c>c_destination</c>.</summary>
    [Test]
    public void PathAnalysisQuery_MapsSourceAndDestinationToMatchingArguments()
    {
        string query = string.Concat(PathAnalysisQueries.pathAnalysis.Where(character => !char.IsWhiteSpace(character)));

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("c_source:$src"));
            Assert.That(query, Does.Contain("c_destination:$dst"));
        });
    }

    /// <summary>
    /// Verifies the analyzer sends the first address of the source as <c>src</c> and the first address of the
    /// destination as <c>dst</c>, and returns the names of the devices found.
    /// </summary>
    [Test]
    public async Task GetDeviceNamesForSinglePath_SendsSourceAsSrcAndDestinationAsDst()
    {
        PathAnalysisApiConn apiConnection = new();

        string deviceNames = await RoutingBasedPathAnalyzer.GetDeviceNamesForSinglePath(
            "10.1.0.0/24", "10.2.0.5", apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.SentQuery, Is.EqualTo(PathAnalysisQueries.pathAnalysis));
            Assert.That(apiConnection.SentSource, Is.EqualTo("10.1.0.0"));
            Assert.That(apiConnection.SentDestination, Is.EqualTo("10.2.0.5"));
            Assert.That(deviceNames, Is.EqualTo("fw-core, fw-edge"));
        });
    }

    private sealed class PathAnalysisApiConn : SimulatedApiConnection
    {
        public string? SentQuery { get; private set; }

        public string? SentSource { get; private set; }

        public string? SentDestination { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQuery = query;
            SentSource = variables?.GetType().GetProperty("src")?.GetValue(variables)?.ToString();
            SentDestination = variables?.GetType().GetProperty("dst")?.GetValue(variables)?.ToString();
            List<Device> devices =
            [
                new Device { Id = 1, Name = "fw-core" },
                new Device { Id = 2, Name = "fw-edge" }
            ];
            return Task.FromResult((QueryResponseType)(object)devices);
        }
    }
}
