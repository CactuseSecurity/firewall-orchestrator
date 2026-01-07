using System.Collections.Immutable;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Middleware.Server.Jobs;
using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Quartz scheduler service for report generation.
    /// </summary>
    public class ReportSchedulerService : BackgroundService, IAsyncDisposable
    {
        private const string SchedulerName = "ReportScheduler";
        private const string JobKeyName = "ReportJob";
        private const string TriggerKeyName = "ReportTrigger";
        private static readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly ReportSchedulerState state;
        private bool disposed = false;

        private IScheduler? scheduler;
        private GraphQlApiSubscription<ReportSchedule[]>? scheduleSubscription;
        private GraphQlApiSubscription<List<Ldap>>? ldapSubscription;

        /// <summary>
        /// Initializes the report scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="state">Shared scheduler state used by the report job.</param>
        public ReportSchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, ReportSchedulerState state)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.state = state;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Initial state population
                state.UpdateLdaps(await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections));

                scheduler = await schedulerFactory.GetScheduler(stoppingToken);
                await ScheduleJob();

                ldapSubscription = apiConnection.GetSubscription<List<Ldap>>(ApiExceptionHandler, OnLdapUpdate, AuthQueries.getLdapConnectionsSubscription);
                scheduleSubscription = apiConnection.GetSubscription<ReportSchedule[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);

                Log.WriteInfo(SchedulerName, "Service started");

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.WriteInfo(SchedulerName, "Service stopping");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Service failed", ex);
                throw;
            }
        }

        private async Task ScheduleJob()
        {
            if (scheduler == null)
            {
                Log.WriteWarning(SchedulerName, "Scheduler not initialized");
                return;
            }

            var jobKey = new JobKey(JobKeyName);
            var triggerKey = new TriggerKey(TriggerKeyName);

            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
                Log.WriteInfo(SchedulerName, "Removed existing job");
            }

            IJobDetail job = JobBuilder.Create<ReportJob>()
                .WithIdentity(jobKey)
                .Build();

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(CheckScheduleInterval)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            Log.WriteInfo(SchedulerName, "Job scheduled to run every minute");
        }

        private void OnScheduleUpdate(ReportSchedule[] scheduledReports)
        {
            state.UpdateSchedules(scheduledReports);
            Log.WriteInfo(SchedulerName, $"Received {scheduledReports.Length} report schedule updates");
        }

        private void OnLdapUpdate(List<Ldap> connectedLdaps)
        {
            state.UpdateLdaps(connectedLdaps);
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError(SchedulerName, "Subscription lead to exception. Retry subscription.", exception);
        }

        /// <summary>
        /// Releases resources used by the service.
        /// Disposes active subscriptions and scheduler.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                try
                {
                    ldapSubscription?.Dispose();
                    scheduleSubscription?.Dispose();
                    
                    if (scheduler != null)
                    {
                        await scheduler.Shutdown(waitForJobsToComplete: true);
                    }
                    
                    disposed = true;
                }
                catch (Exception ex)
                {
                    Log.WriteError(SchedulerName, "Error during disposal", ex);
                }
            }
        }
    }

    /// <summary>
    /// Shared state between the scheduler service and report job.
    /// </summary>
    public class ReportSchedulerState
    {
        private ImmutableArray<ReportSchedule> scheduledReports = ImmutableArray<ReportSchedule>.Empty;
        private ImmutableArray<Ldap> connectedLdaps = ImmutableArray<Ldap>.Empty;

        /// <summary>
        /// The current set of scheduled reports known to the scheduler.
        /// </summary>
        public ImmutableArray<ReportSchedule> ScheduledReports => scheduledReports;

        /// <summary>
        /// The LDAP connections currently available for user authorization.
        /// </summary>
        public ImmutableArray<Ldap> ConnectedLdaps => connectedLdaps;

        /// <summary>
        /// Updates the in-memory list of scheduled reports.
        /// </summary>
        public void UpdateSchedules(IEnumerable<ReportSchedule> newSchedules)
        {
            scheduledReports = newSchedules.ToImmutableArray();
        }

        /// <summary>
        /// Updates the in-memory list of connected LDAP instances.
        /// </summary>
        public void UpdateLdaps(IEnumerable<Ldap> newLdaps)
        {
            connectedLdaps = newLdaps.ToImmutableArray();
        }
    }
}
