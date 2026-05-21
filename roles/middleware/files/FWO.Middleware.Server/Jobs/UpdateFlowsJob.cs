
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using Quartz;

namespace FWO.Services
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
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await flowSync.Run();
            }
            catch (Exception exception)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify, exception);
            }
        }
    }
}
