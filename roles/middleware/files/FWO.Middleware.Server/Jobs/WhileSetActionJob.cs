using FWO.Api.Client;
using FWO.Basics;
using FWO.Basics.Exceptions;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz Job performing the state actions with event "While set" for the tickets in their states
    /// </summary>
    [DisallowConcurrentExecution]
    public class WhileSetActionJob : IJob
    {
        private const string LogMessageTitle = "While Set Action Job";
        private readonly ApiConnection apiConnection;
        private readonly GlobalConfig globalConfig;
        private readonly WorkflowTicketLocks ticketLocks;

        /// <summary>
        /// Constructor with DI
        /// </summary>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="globalConfig">Global configuration.</param>
        /// <param name="ticketLocks">Per-ticket locks shared with the workflow action endpoint.</param>
        public WhileSetActionJob(ApiConnection apiConnection, GlobalConfig globalConfig, WorkflowTicketLocks ticketLocks)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
            this.ticketLocks = ticketLocks;
        }

        /// <summary>
        /// Execute the job
        /// </summary>
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            Log.WriteDebug(LogMessageTitle, "Job started");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using WorkflowWhileSetActionExecutor executor = new(apiConnection, globalConfig, ticketLocks);
                WhileSetActionRunner runner = new(apiConnection, executor);
                List<string> failures = await runner.Run(cancellationToken);
                if (failures.Count > 0)
                {
                    throw new ProcessingFailedException($"{failures.Count} while set action(s) failed: {string.Join(". ", failures)}.");
                }
                Log.WriteDebug(LogMessageTitle, "Job completed successfully");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.WriteDebug(LogMessageTitle, $"{nameof(WhileSetActionJob)} stopped.");
            }
            catch (Exception exc)
            {
                Log.WriteError(LogMessageTitle, "Job failed", exc);
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, "While Set Actions", GlobalConst.kModuleWorkflow, AlertCode.WorkflowAlert, exc);

                // Mark job as failed but don't refire immediately
                throw new JobExecutionException(exc);
            }
        }
    }
}
