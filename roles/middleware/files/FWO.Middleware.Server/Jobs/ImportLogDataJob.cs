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
    /// Quartz job for importing log data.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ImportLogDataJob(ApiConnection apiConnection, GlobalConfig globalConfig) : IJob
    {
        private const string LogMessageTitle = "Import Log Data";

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                LogDataImport import = new(apiConnection, globalConfig);
                List<string> failedImports = await import.Run();
                if (failedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"{LogMessageTitle} failed for {string.Join(", ", failedImports)}.");
                }
            }
            catch (Exception exception)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitle, GlobalConst.kImportLogData, AlertCode.ImportLogData, exception);
            }
        }
    }
}
