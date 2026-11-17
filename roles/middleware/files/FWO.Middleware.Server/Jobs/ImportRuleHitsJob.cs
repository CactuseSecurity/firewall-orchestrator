using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz job requesting rule hit import runs from the importers.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ImportRuleHitsJob(ApiConnection apiConnection, GlobalConfig globalConfig) : IJob
    {
        private const string LogMessageTitle = "Import Rule Hits";

        /// <inheritdoc />
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await new RuleHitImportRequester(apiConnection).RequestRuleHitImports(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.WriteDebug(LogMessageTitle, $"{nameof(ImportRuleHitsJob)} stopped.");
            }
            catch (Exception exception)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitle, GlobalConst.kImportRuleHits, AlertCode.ImportRuleHits, exception);
            }
        }
    }
}
