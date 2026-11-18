using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Logging;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Requests a rule hit import run for every management the importers import.
    /// The responsible importer picks up the request, refreshes the last hits of the management's rules
    /// and removes the request; managements whose importer module cannot read hits are skipped there.
    /// </summary>
    public class RuleHitImportRequester(ApiConnection apiConnection)
    {
        private const string LogMessageTitle = "Import Rule Hits";

        /// <summary>
        /// Writes one rule hit import request per imported top-level management.
        /// A request still pending from an earlier run is kept unchanged.
        /// </summary>
        /// <param name="cancellationToken">Stops before the requests are written.</param>
        /// <returns>The number of newly written requests.</returns>
        public async Task<int> RequestRuleHitImports(CancellationToken cancellationToken = default)
        {
            List<Management> managements = await apiConnection.SendQueryAsync<List<Management>>(ImportQueries.getManagementsForRuleHitImport);
            cancellationToken.ThrowIfCancellationRequested();
            if (managements.Count == 0)
            {
                Log.WriteDebug(LogMessageTitle, "No managements to request a rule hit import for.");
                return 0;
            }

            List<object> requests = [.. managements.Select(management => new { mgm_id = management.Id })];
            ReturnId result = await apiConnection.SendQueryAsync<ReturnId>(ImportQueries.addRuleHitImportRequests, new { requests });
            Log.WriteInfo(LogMessageTitle, $"Requested rule hit imports for {result.AffectedRows} of {managements.Count} managements.");
            return result.AffectedRows;
        }
    }
}
