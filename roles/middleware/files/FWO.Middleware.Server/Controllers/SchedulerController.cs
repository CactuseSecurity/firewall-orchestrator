using FWO.Basics;
using FWO.Data.Middleware;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>Controller exposing Quartz jobs for manual triggering.</summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulerController : ControllerBase
    {
        private readonly ISchedulerFactory schedulerFactory;
        private readonly JobExecutionTracker executionTracker;

        public SchedulerController(ISchedulerFactory schedulerFactory, JobExecutionTracker executionTracker)
        {
            this.schedulerFactory = schedulerFactory;
            this.executionTracker = executionTracker;
        }

        [HttpGet]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<IEnumerable<SchedulerJobInfo>> GetJobs()
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler();
            IReadOnlyCollection<JobKey> jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            List<SchedulerJobInfo> jobs = [];
            foreach (JobKey jobKey in jobKeys.OrderBy(jk => jk.Name))
            {
                IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey);
                DateTimeOffset? nextFire = triggers
                    .Select(trigger => trigger.GetNextFireTimeUtc())
                    .Where(fireTime => fireTime.HasValue)
                    .Select(fireTime => (DateTimeOffset?)fireTime!.Value)
                    .OrderBy(fireTime => fireTime)
                    .FirstOrDefault();

                DateTimeOffset? lastFire = triggers
                    .Select(trigger => trigger.GetPreviousFireTimeUtc())
                    .Where(fireTime => fireTime.HasValue)
                    .Select(fireTime => (DateTimeOffset?)fireTime!.Value)
                    .OrderByDescending(fireTime => fireTime)
                    .FirstOrDefault();

                string intervalDescription = DescribeTriggers(triggers);

                JobExecutionResult? lastResult = executionTracker.GetLastResult(jobKey.Name);
                string executionStatus = lastResult != null ? (lastResult.Success ? "success" : "error") : "unknown";
                string executionError = lastResult?.ErrorMessage ?? "";

                DateTimeOffset? actualLastFire = lastResult?.ExecutedAt ?? lastFire;

                jobs.Add(new SchedulerJobInfo
                {
                    JobName = jobKey.Name,
                    Group = jobKey.Group,
                    NextFireTimeUtc = nextFire,
                    LastFireTimeUtc = actualLastFire,
                    IntervalDescription = intervalDescription,
                    LastExecutionStatus = executionStatus,
                    LastExecutionError = executionError
                });
            }

            return jobs;
        }

        [HttpPost("Run")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<ActionResult<bool>> Run([FromBody] SchedulerJobTriggerParameters parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.JobName))
            {
                return BadRequest("Job name missing.");
            }

            IScheduler scheduler = await schedulerFactory.GetScheduler();
            JobKey jobKey = new(parameters.JobName);

            if (!await scheduler.CheckExists(jobKey))
            {
                return NotFound("Job not found.");
            }

            await scheduler.TriggerJob(jobKey);

            Log.WriteAudit("Scheduler", $"Manual trigger for {jobKey.Name}.");
            return true;
        }

        private static string DescribeTriggers(IEnumerable<ITrigger> triggers)
        {
            foreach (ITrigger trigger in triggers)
            {
                if (trigger is ISimpleTrigger simple && simple.RepeatInterval > TimeSpan.Zero)
                {
                    return $"every {FormatInterval(simple.RepeatInterval)}";
                }

                if (trigger is ICronTrigger cron)
                {
                    return $"cron: {cron.CronExpressionString}";
                }
            }

            return "manual";
        }

        private static string FormatInterval(TimeSpan interval)
        {
            if (interval.TotalDays >= 1)
            {
                return $"{interval.TotalDays:g}d";
            }
            if (interval.TotalHours >= 1)
            {
                return $"{interval.TotalHours:g}h";
            }
            if (interval.TotalMinutes >= 1)
            {
                return $"{interval.TotalMinutes:g}m";
            }
            return $"{interval.TotalSeconds:g}s";
        }
    }
}
