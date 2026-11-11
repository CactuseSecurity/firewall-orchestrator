using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.RulePositioning;
using FWO.Services.RulePositioning;
using System.Diagnostics;
using DesignatedZoneMatrixReadResult = FWO.Data.RulePositioning.PositioningResult<
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadValue,
    FWO.Compliance.RulePositioning.DesignatedZoneMatrixReadFailureCategory>;

namespace FWO.Compliance.RulePositioning;

/// <summary>Loads the configured designated zone matrix through the normalized-data API.</summary>
public sealed class DesignatedZoneMatrixReader(ApiConnection apiConnection)
    : IDesignatedZoneMatrixReader
{
    /// <inheritdoc />
    public async Task<DesignatedZoneMatrixReadResult> ReadAsync(
        int criterionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (criterionId <= 0)
        {
            RulePositioningDiagnosticLog.WriteFailure("matrix.validation", Facts(criterionId,
                DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid));
            return Failure(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid);
        }

        try
        {
            Stopwatch query = Stopwatch.StartNew();
            List<ComplianceCriterion>? matrices = await apiConnection
                .SendQueryAsync<List<ComplianceCriterion>>(
                    ComplianceQueries.getMatrixById,
                    new { criterionId })
                .WaitAsync(cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo("matrix.query", "success", new Dictionary<string, object?>
            {
                ["query_kind"] = "matrix",
                ["criterion_id"] = criterionId,
                ["duration_ms"] = query.ElapsedMilliseconds,
                ["result_count"] = matrices?.Count ?? 0,
                ["returned_id"] = matrices is { Count: 1 } ? matrices[0]?.Id : null
            });
            if (matrices == null || matrices.Count == 0)
            {
                RulePositioningDiagnosticLog.WriteFailure("matrix.invariant", Facts(criterionId,
                    DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable, matrices?.Count ?? 0));
                return Failure(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable);
            }
            if (matrices.Count != 1 || matrices[0] == null || matrices[0].Id != criterionId)
            {
                RulePositioningDiagnosticLog.WriteFailure("matrix.invariant", Facts(criterionId,
                    DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid, matrices.Count,
                    matrices.Count == 1 ? matrices[0]?.Id : null));
                return Failure(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid);
            }

            cancellationToken.ThrowIfCancellationRequested();
            query.Restart();
            List<NetworkZone>? zones = await apiConnection
                .SendQueryAsync<List<NetworkZone>>(
                    NetworkZoneQueries.getNetworkZonesForMatrix,
                    new { criterionId })
                .WaitAsync(cancellationToken);
            RulePositioningDiagnosticLog.WriteInfo("matrix.query", "success", new Dictionary<string, object?>
            {
                ["query_kind"] = "zones",
                ["criterion_id"] = criterionId,
                ["duration_ms"] = query.ElapsedMilliseconds,
                ["result_count"] = zones?.Count ?? 0
            });
            return DesignatedZoneMatrixReadResult.Success(new DesignatedZoneMatrixReadValue(zones ?? []));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RulePositioningDiagnosticLog.WriteException("matrix.query", exception, Facts(criterionId,
                DesignatedZoneMatrixReadFailureCategory.QueryFailed));
            return Failure(DesignatedZoneMatrixReadFailureCategory.QueryFailed);
        }
    }

    /// <summary>Creates a failed read result.</summary>
    private static DesignatedZoneMatrixReadResult Failure(DesignatedZoneMatrixReadFailureCategory category)
    {
        return DesignatedZoneMatrixReadResult.Failure(category);
    }

    private static Dictionary<string, object?> Facts(
        int criterionId,
        DesignatedZoneMatrixReadFailureCategory category,
        int? resultCount = null,
        int? returnedId = null) => new()
        {
            ["criterion_id"] = criterionId,
            ["result_count"] = resultCount,
            ["returned_id"] = returnedId,
            ["failure_category"] = category.ToString()
        };
}
