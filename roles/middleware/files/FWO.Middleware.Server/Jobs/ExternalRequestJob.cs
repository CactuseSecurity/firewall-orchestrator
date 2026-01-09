using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job for sending external requests
    /// </summary>
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
                ExternalRequestSender externalRequestSender = new(apiConnection, globalConfig);
                List<string> failedRequests = await externalRequestSender.Run();
                
                if (failedRequests.Count > 0)
                {
                    throw new ProcessingFailedException($"{failedRequests.Count} External Request(s) failed: {string.Join(". ", failedRequests)}.");
                }
                
                Log.WriteDebug(LogMessageTitle, "Job completed successfully");
            }
            catch (Exception exc)
            {
                Log.WriteError(LogMessageTitle, "Job failed", exc);
                await LogErrorsWithAlert(exc);
                
                // Mark job as failed but don't refire immediately
                throw new JobExecutionException(exc, refireImmediately: false);
            }
        }

        private async Task LogErrorsWithAlert(Exception exc)
        {
            try
            {
                Log.WriteError(LogMessageTitle, $"Ran into exception: ", exc);
                string titletext = $"Error encountered while trying External Request";
                
                var Variables = new
                {
                    source = GlobalConst.kExternalRequest,
                    discoverUser = 0,
                    severity = 1,
                    suspectedCause = "External Request",
                    description = globalConfig.GetText("ran_into_exception") + exc.Message,
                    mgmId = (int?)null,
                    devId = (int?)null,
                    importId = (long?)null,
                    objectType = (string?)null,
                    objectName = (string?)null,
                    objectUid = (string?)null,
                    ruleUid = (string?)null,
                    ruleId = (long?)null
                };
                
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addLogEntry, Variables);
                
                var alertVariables = new
                {
                    source = GlobalConst.kExternalRequest,
                    userId = 0,
                    title = "External Request",
                    description = titletext,
                    mgmId = (int?)null,
                    devId = (int?)null,
                    alertCode = (int)AlertCode.ExternalRequest,
                    jsonData = (object?)null,
                    refAlert = (long?)null
                };
                
                await apiConnection.SendQueryAsync<ReturnIdWrapper>(MonitorQueries.addAlert, alertVariables);
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, "Could not write alert", exception);
            }
        }
    }
}
