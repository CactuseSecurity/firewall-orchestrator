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
    /// Quartz Job for sending external requests
    /// </summary>
    [DisallowConcurrentExecution]
    public class ExternalRequestJob : IJob
    {
        private const string LogMessageTitle = "External Request Job";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;

        /// <summary>
        /// Constructor with DI
        /// </summary>
        public ExternalRequestJob(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Execute the job
        /// </summary>
        public async Task Execute(IJobExecutionContext context)
        {
            Log.WriteDebug(LogMessageTitle, "Job started");

            try
            {
                using (ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig))
                {
                    List<string> failedRequests = await externalRequestSender.Run();

                    if (failedRequests.Count > 0)
                    {
                        throw new ProcessingFailedException($"{failedRequests.Count} External Request(s) failed: {string.Join(". ", failedRequests)}.");
                    }

                    Log.WriteDebug(LogMessageTitle, "Job completed successfully");
                }
            }
            catch (Exception exc)
            {
                Log.WriteError(LogMessageTitle, "Job failed", exc);
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, "External Request", GlobalConst.kExternalRequest, AlertCode.ExternalRequest, exc);

                // Mark job as failed but don't refire immediately
                throw new JobExecutionException(exc, refireImmediately: false);
            }
        }

    }
}
