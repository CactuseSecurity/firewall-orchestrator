using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.RulePositioning;
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
            return Failure(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid);
        }

        try
        {
            List<ComplianceCriterion>? matrices = await apiConnection
                .SendQueryAsync<List<ComplianceCriterion>>(
                    ComplianceQueries.getMatrixById,
                    new { criterionId })
                .WaitAsync(cancellationToken);
            if (matrices == null || matrices.Count == 0)
            {
                return Failure(DesignatedZoneMatrixReadFailureCategory.MatrixUnavailable);
            }
            if (matrices.Count != 1 || matrices[0] == null || matrices[0].Id != criterionId)
            {
                return Failure(DesignatedZoneMatrixReadFailureCategory.ConfigurationInvalid);
            }

            cancellationToken.ThrowIfCancellationRequested();
            List<NetworkZone>? zones = await apiConnection
                .SendQueryAsync<List<NetworkZone>>(
                    NetworkZoneQueries.getNetworkZonesForMatrix,
                    new { criterionId })
                .WaitAsync(cancellationToken);
            return DesignatedZoneMatrixReadResult.Success(new DesignatedZoneMatrixReadValue(zones ?? []));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure(DesignatedZoneMatrixReadFailureCategory.QueryFailed);
        }
    }

    /// <summary>Creates a failed read result.</summary>
    private static DesignatedZoneMatrixReadResult Failure(DesignatedZoneMatrixReadFailureCategory category)
    {
        return DesignatedZoneMatrixReadResult.Failure(category);
    }
}
