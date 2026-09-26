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
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogDataImport import = new(apiConnection, globalConfig);
                List<string> failedImports = await import.Run(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (failedImports.Count > 0)
                {
                    throw new ProcessingFailedException($"{LogMessageTitle} failed for {string.Join(", ", failedImports)}.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.WriteDebug(LogMessageTitle, $"{nameof(ImportLogDataJob)} stopped.");
            }
            catch (Exception exception)
            {
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 2, LogMessageTitle, GlobalConst.kImportLogData, AlertCode.ImportLogData, exception);
            }
        }
    }
}
