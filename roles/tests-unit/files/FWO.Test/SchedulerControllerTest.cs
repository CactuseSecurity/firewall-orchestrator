using FWO.Basics;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Quartz;
using Quartz.Impl.Matchers;

namespace FWO.Test
{
    [TestFixture]
    internal class SchedulerControllerTest
    {
        [Test]
        public async Task GetJobs_ReturnsSortedJobsWithExecutionDetails()
        {
            JobKey alphaJob = new("alpha-job", "group-a");
            JobKey betaJob = new("beta-job", "group-b");
            DateTimeOffset alphaNextFire = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset alphaPreviousFire = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);
            DateTimeOffset betaNextFire = new(2026, 7, 21, 13, 0, 0, TimeSpan.Zero);
            DateTimeOffset betaPreviousFire = new(2026, 7, 21, 9, 0, 0, TimeSpan.Zero);

            JobExecutionTracker tracker = new();
            await tracker.JobWasExecuted(CreateExecutionContext(alphaJob), null);
            await tracker.JobWasExecuted(CreateExecutionContext(betaJob), new JobExecutionException(new InvalidOperationException("boom"), refireImmediately: false));

            IScheduler scheduler = Substitute.For<IScheduler>();
            scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), CancellationToken.None)
                .Returns(Task.FromResult<IReadOnlyCollection<JobKey>>(new List<JobKey> { betaJob, alphaJob }));
            scheduler.GetTriggersOfJob(alphaJob, CancellationToken.None)
                .Returns(Task.FromResult<IReadOnlyCollection<ITrigger>>(new List<ITrigger> { CreateSimpleTrigger() }));
            scheduler.GetTriggersOfJob(betaJob, CancellationToken.None)
                .Returns(Task.FromResult<IReadOnlyCollection<ITrigger>>(new List<ITrigger> { CreateCronTrigger() }));

            SchedulerController controller = CreateController(scheduler, tracker);

            List<SchedulerJobInfo> jobs = (await controller.GetJobs()).ToList();

            Assert.That(jobs, Has.Count.EqualTo(2));
            Assert.That(jobs[0].JobName, Is.EqualTo("alpha-job"));
            Assert.That(jobs[0].Group, Is.EqualTo("group-a"));
            Assert.That(jobs[0].IntervalDescription, Is.EqualTo("2h"));
            Assert.That(jobs[0].LastFireTimeUtc, Is.EqualTo(tracker.GetLastResult("alpha-job")!.ExecutedAt));
            Assert.That(jobs[0].LastExecutionStatus, Is.EqualTo(SchedulerJobExecutionStatus.Success));
            Assert.That(jobs[0].LastExecutionError, Is.Empty);
            Assert.That(jobs[1].JobName, Is.EqualTo("beta-job"));
            Assert.That(jobs[1].Group, Is.EqualTo("group-b"));
            Assert.That(jobs[1].IntervalDescription, Is.EqualTo("0 0 * * * ?"));
            Assert.That(jobs[1].LastFireTimeUtc, Is.EqualTo(tracker.GetLastResult("beta-job")!.ExecutedAt));
            Assert.That(jobs[1].LastExecutionStatus, Is.EqualTo(SchedulerJobExecutionStatus.Failed));
            Assert.That(jobs[1].LastExecutionError, Is.EqualTo("boom"));
        }

        [Test]
        public async Task Run_ReturnsBadRequestWhenJobNameIsMissing()
        {
            SchedulerController controller = CreateController(Substitute.For<IScheduler>(), new JobExecutionTracker());

            ActionResult<bool> result = await controller.Run(new SchedulerJobTriggerParameters());

            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Is.EqualTo("Job name missing."));
        }

        [Test]
        public async Task Run_ReturnsNotFoundWhenJobDoesNotExist()
        {
            IScheduler scheduler = Substitute.For<IScheduler>();
            scheduler.CheckExists(new JobKey("missing-job"), CancellationToken.None)
                .Returns(Task.FromResult(false));

            SchedulerController controller = CreateController(scheduler, new JobExecutionTracker());

            ActionResult<bool> result = await controller.Run(new SchedulerJobTriggerParameters { JobName = "missing-job" });

            Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
            Assert.That(((NotFoundObjectResult)result.Result!).Value, Is.EqualTo("Job not found."));
        }

        [Test]
        public async Task Run_TriggersExistingJob()
        {
            IScheduler scheduler = Substitute.For<IScheduler>();
            scheduler.CheckExists(new JobKey("trigger-job"), CancellationToken.None)
                .Returns(Task.FromResult(true));
            scheduler.TriggerJob(new JobKey("trigger-job"), CancellationToken.None)
                .Returns(Task.CompletedTask);

            SchedulerController controller = CreateController(scheduler, new JobExecutionTracker());

            ActionResult<bool> result = await controller.Run(new SchedulerJobTriggerParameters { JobName = "trigger-job" });

            Assert.That(result.Value, Is.True);
            await scheduler.Received(1).TriggerJob(new JobKey("trigger-job"), CancellationToken.None);
        }

        private static SchedulerController CreateController(IScheduler scheduler, JobExecutionTracker tracker)
        {
            ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
            schedulerFactory.GetScheduler().Returns(Task.FromResult(scheduler));
            return new SchedulerController(schedulerFactory, tracker);
        }

        private static IJobExecutionContext CreateExecutionContext(JobKey jobKey)
        {
            IJobDetail jobDetail = Substitute.For<IJobDetail>();
            jobDetail.Key.Returns(jobKey);

            IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
            context.JobDetail.Returns(jobDetail);
            return context;
        }

        private static ITrigger CreateSimpleTrigger()
        {
            return TriggerBuilder.Create()
                .WithIdentity("alpha-trigger")
                .StartNow()
                .WithSimpleSchedule(schedule => schedule.WithInterval(TimeSpan.FromHours(2)).RepeatForever())
                .Build();
        }

        private static ITrigger CreateCronTrigger()
        {
            return TriggerBuilder.Create()
                .WithIdentity("beta-trigger")
                .StartNow()
                .WithCronSchedule("0 0 * * * ?")
                .Build();
        }
    }
}
