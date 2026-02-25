using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for update Rule Owner Mappings
    /// </summary>
    [DisallowConcurrentExecution]
    public class UpdateRuleOwnerMappingJob : IJob
    {
        private const string LogMessageTitle = "Import Change Notify";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new job for update Rule Owner Mappings.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public UpdateRuleOwnerMappingJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                UpdateRuleOwnerMapping updateRuleOwnerMapping = new(apiConnection, globalConfig);
                await updateRuleOwnerMapping.Run();
            }
            catch (Exception exc)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kImportChangeNotify, AlertCode.ImportChangeNotify, exc);
            }
        }
    }
}
