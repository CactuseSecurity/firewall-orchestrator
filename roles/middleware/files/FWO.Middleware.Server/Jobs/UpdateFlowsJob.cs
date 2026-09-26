
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
    /// Quartz job for synchronizing public flow mappings with flow schema tables.
    /// </summary>
    [DisallowConcurrentExecution]
    public class UpdateFlowsJob : IJob
    {
        private const string LogMessageTitle = "Update flow sync";

        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private readonly FlowSync flowSync;

        /// <summary>
        /// Creates a new flow sync job.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="flowSync">Flow synchronization service.</param>
        public UpdateFlowsJob(ApiConnection apiConnection, GlobalConfig globalConfig, FlowSync flowSync)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.flowSync = flowSync;
        }

        /// <inheritdoc />
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await flowSync.Run(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.WriteDebug(LogMessageTitle, $"{nameof(UpdateFlowsJob)} stopped.");
            }
            catch (Exception exception)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify, exception);
            }
        }
    }
}
