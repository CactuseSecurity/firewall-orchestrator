using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;

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
            List<ComplianceNetworkZone>? zones = await apiConnection
                .SendQueryAsync<List<ComplianceNetworkZone>>(
                    NetworkZoneQueries.getNetworkZonesForMatrix,
                    new { criterionId })
                .WaitAsync(cancellationToken);
            return new DesignatedZoneMatrixReadSuccess(zones ?? []);
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
    private static DesignatedZoneMatrixReadFailure Failure(DesignatedZoneMatrixReadFailureCategory category)
    {
        return new DesignatedZoneMatrixReadFailure(category);
    }
}
