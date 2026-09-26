using Quartz;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Registers the Quartz scheduler of the middleware and its shutdown behaviour.
    /// </summary>
    public static class QuartzHostingConfiguration
    {
        /// <summary>
        /// Time running jobs get on shutdown to reach their next checkpoint and persist their state.
        /// Must stay below TimeoutStopSec of the systemd unit (fworch-middleware.service.j2).
        /// </summary>
        public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Adds Quartz with the job execution tracker, and makes a shutdown cancel running jobs
        /// and wait for them to unwind.
        /// </summary>
        /// <param name="services">Service collection of the middleware.</param>
        /// <param name="timeProvider">Clock shared by Quartz and the jobs.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddMiddlewareQuartz(this IServiceCollection services, TimeProvider timeProvider)
        {
            services.AddSingleton<JobExecutionTracker>();
            services.AddQuartz(q =>
            {
                q.UseTimeProvider(timeProvider);
                // Signal cancellation to running jobs on shutdown, then wait for them to unwind (default is Never)
                q.ConfigureScheduler(s => s.ShutdownJobInterruption = ShutdownJobInterruption.WhenWaitingForJobs);
                q.AddJobListener(serviceProvider => serviceProvider.GetRequiredService<JobExecutionTracker>(), [GroupMatcher<JobKey>.AnyGroup()]);
            });
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownTimeout);
            return services;
        }
    }
}
