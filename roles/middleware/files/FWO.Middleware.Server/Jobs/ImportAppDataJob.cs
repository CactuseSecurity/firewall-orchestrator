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
    /// Quartz Job for importing app data and adjusting app server names
    /// </summary>
    public class ImportAppDataJob : IJob
    {
        private const string LogMessageTitleImport = "Import App Data";
        private const string LogMessageTitleAdjust = "Adjust App Server Names";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Creates a new job for importing app data and adjusting app server names.
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        public ImportAppDataJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            await ImportAppData();
            await AdjustAppServerNames();
        }

        private async Task ImportAppData()
        {
            try
            {
                AppDataImport import = new(apiConnection, globalConfig);
                List<string> failedImports = await import.Run();
                if (failedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"{LogMessageTitleImport} failed for {string.Join(", ", failedImports)}.");
                }
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitleImport, GlobalConst.kImportAppData, AlertCode.ImportAppData, exc);
            }
        }

        private async Task AdjustAppServerNames()
        {
            try
            {
                if (globalConfig.DnsLookup)
                {
                    UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
                    userConfig.User.Name = Roles.MiddlewareServer;
                    await AppServerHelper.AdjustAppServerNames(apiConnection, userConfig);
                }
            }
            catch (Exception exc)
            {
                await SchedulerJobHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, LogMessageTitleAdjust, GlobalConst.kAdjustAppServerNames, AlertCode.AdjustAppServerNames, exc);
            }
        }
    }
}
