using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for importing area IP data
    /// </summary>
    public class ImportIpDataJob : IJob
    {
        private const string LogMessageTitle = "Import Area IP Data";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new job for importing area IP data.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public ImportIpDataJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                AreaIpDataImport import = new(apiConnection, globalConfig);
                List<string> failedImports = await import.Run();
                if (failedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"{LogMessageTitle} failed for {string.Join(", ", failedImports)}.");
                }
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitle, GlobalConst.kImportAreaSubnetData, AlertCode.ImportAreaSubnetData, exc);
            }
        }
    }
}
