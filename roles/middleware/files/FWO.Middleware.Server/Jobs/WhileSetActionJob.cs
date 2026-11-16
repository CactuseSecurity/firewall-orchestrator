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
        /// <param name="globalConfig">Global configuration loaded at the middleware start; each run reads the current
        /// config items with it and uses the injected one only for its alert texts.</param>
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
                List<string> failures = await RunWithCurrentConfig(cancellationToken);
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
                // the injected config only provides the alert texts, so the alert is raised even if the current config could not be read
                await AlertHelper.LogErrorsWithAlert(apiConnection, globalConfig, 1, "While Set Actions", GlobalConst.kModuleWorkflow, AlertCode.WorkflowAlert, exc);

                // Mark job as failed but don't refire immediately
                throw new JobExecutionException(exc);
            }
        }

        /// <summary>
        /// Performs the "While set" actions of one run with the global config read at its start. The injected config is
        /// loaded once at the middleware start and does not follow later changes of the workflow settings, but the actions
        /// have to, like the ones of the workflow action endpoint. The same config is used for all tickets of the run,
        /// including the state actions of the states the tickets are moved into.
        /// </summary>
        /// <param name="cancellationToken">Stops before the next ticket is processed.</param>
        /// <returns>Descriptions of the actions that failed.</returns>
        private async Task<List<string>> RunWithCurrentConfig(CancellationToken cancellationToken)
        {
            using GlobalConfig currentGlobalConfig = await globalConfig.WithCurrentConfigItemsAsync(apiConnection);
            using WorkflowWhileSetActionExecutor executor = new(apiConnection, currentGlobalConfig, ticketLocks);
            WhileSetActionRunner runner = new(apiConnection, executor);
            return await runner.Run(cancellationToken);
        }
    }
}
