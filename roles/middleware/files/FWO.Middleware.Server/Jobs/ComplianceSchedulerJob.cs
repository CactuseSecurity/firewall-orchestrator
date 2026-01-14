using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.DeviceAutoDiscovery;
using FWO.Logging;
using Quartz;
using System.Linq;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for autodiscovery
    /// </summary>
    public class ComplianceSchedulerJob : IJob
    {
        private const string LogMessageTitle = "Compliance";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new autodiscovery job.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public ComplianceSchedulerJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                UserConfig userConfig = new(globalConfig);
                ComplianceCheck complianceCheck = new(userConfig, apiConnection);

                await complianceCheck.CheckAll();
                await complianceCheck.PersistDataAsync();
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitle, GlobalConst.kComplianceCheck, AlertCode.ComplianceCheck, exc);
            }
        }
    }
}
