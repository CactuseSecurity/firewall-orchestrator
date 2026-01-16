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
    /// Config/state listener for report generation (no BackgroundService)
    /// </summary>
    public class ReportSchedulerService : IAsyncDisposable
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly ApiConnection apiConnection;
        private readonly ReportSchedulerState state;
        private bool disposed = false;
        private GraphQlApiSubscription<ReportSchedule[]>? scheduleSubscription;
        private GraphQlApiSubscription<List<Ldap>>? ldapSubscription;
        private IScheduler? scheduler;
        private const string JobKeyName = "ReportJob";
        private const string TriggerKeyName = "ReportTrigger";
        private const string SchedulerName = "ReportScheduler";

        /// <summary>
        /// Initializes the report scheduler service.
        /// </summary>
        /// <param name="schedulerFactory">Quartz scheduler factory.</param>
        /// <param name="apiConnection">GraphQL API connection.</param>
        /// <param name="state">Shared scheduler state used by the report job.</param>
        /// <param name="appLifetime"></param>
        public ReportSchedulerService(ISchedulerFactory schedulerFactory, ApiConnection apiConnection, ReportSchedulerState state, IHostApplicationLifetime appLifetime)
        {
            this.schedulerFactory = schedulerFactory;
            this.apiConnection = apiConnection;
            this.state = state;

            // Attach after application started
            appLifetime.ApplicationStarted.Register(OnStarted);
        }

        private async void OnStarted()
        {
            try
            {
                // Initial state population
                state.UpdateLdaps(await apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections));
                ldapSubscription = apiConnection.GetSubscription<List<Ldap>>(ApiExceptionHandler, OnLdapUpdate, AuthQueries.getLdapConnectionsSubscription);
                scheduleSubscription = apiConnection.GetSubscription<ReportSchedule[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);
                scheduler = await schedulerFactory.GetScheduler();

                Log.WriteInfo(SchedulerName, "Listener started");
            }
            catch (Exception ex)
            {
                Log.WriteError(SchedulerName, "Startup failed", ex);
            }
        }

        private async void OnScheduleUpdate(ReportSchedule[] scheduledReports)
        {
            state.UpdateSchedules(scheduledReports);
            await ScheduleJob();
            Log.WriteInfo(SchedulerName, $"Received {scheduledReports.Length} report schedule updates");
        }

        private void OnLdapUpdate(List<Ldap> connectedLdaps)
        {
            state.UpdateLdaps(connectedLdaps);
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

            // Ensure the job exists as a durable job (so it can be manually triggered)
            if (!await scheduler.CheckExists(jobKey))
            {
                IJobDetail durableJob = JobBuilder.Create<ReportJob>()
                    .WithIdentity(jobKey)
                    .StoreDurably()
                    .Build();

                await scheduler.AddJob(durableJob, replace: true);

                Log.WriteInfo(SchedulerName, "Added durable job for manual triggering");
            }

            // Remove existing trigger (if any) but keep the job
            bool unscheduled = await scheduler.UnscheduleJob(triggerKey);
            if (unscheduled)
            {
                Log.WriteInfo(SchedulerName, "Removed existing trigger");
            }

            int interval = 60;

            // Create trigger with recurring schedule for existing job
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(interval)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(trigger);

            Log.WriteInfo(SchedulerName,
                $"Trigger scheduled, Interval: {interval}s");
        }

        private static void ApiExceptionHandler(Exception exception)
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

                    disposed = true;
                }
                catch (Exception ex)
                {
                    Log.WriteError(SchedulerName, "Error during disposal", ex);
                }
            }
        }

        private static DateTimeOffset CalculateStartTime(DateTime configuredStartTime, int intervalSeconds)
        {
            DateTime startTime = configuredStartTime;
            DateTime now = DateTime.Now;

            // Move start time forward until it's in the future
            while (startTime < now)
            {
                startTime = startTime.AddSeconds(intervalSeconds);
            }

            return new DateTimeOffset(startTime);
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
