using FWO.Middleware.Server.Services;
using NSubstitute;
using NUnit.Framework;
using Quartz;

// Quartz 4 returns ValueTask from listener callbacks. The tests below await the returned
// ValueTasks directly, so CA2012 does not apply.
#pragma warning disable CA2012

namespace FWO.Test
{
    [TestFixture]
    internal class JobExecutionTrackerTest
    {
        [Test]
        public async Task ListenerCallbacksWithoutResultCompleteSuccessfully()
        {
            JobExecutionTracker tracker = new();
            IJobExecutionContext context = CreateExecutionContext(new JobKey("tracked-job"));

            await tracker.JobToBeExecuted(context);
            await tracker.JobExecutionVetoed(context);

            Assert.That(tracker.Name, Is.EqualTo("JobExecutionTracker"));
            Assert.That(tracker.GetLastResult("tracked-job"), Is.Null);
        }

        [Test]
        public async Task JobWasExecuted_StoresSuccessfulResult()
        {
            JobExecutionTracker tracker = new();
            DateTimeOffset beforeExecution = DateTimeOffset.Now;

            await tracker.JobWasExecuted(CreateExecutionContext(new JobKey("successful-job")), null);

            JobExecutionResult? result = tracker.GetLastResult("successful-job");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.ErrorMessage, Is.Empty);
            Assert.That(result.ExecutedAt, Is.GreaterThanOrEqualTo(beforeExecution));
        }

        [Test]
        public async Task JobWasExecuted_StoresFailureResult()
        {
            JobExecutionTracker tracker = new();
            JobExecutionException exception = new(new InvalidOperationException("boom"));

            await tracker.JobWasExecuted(CreateExecutionContext(new JobKey("failed-job")), exception);

            JobExecutionResult? result = tracker.GetLastResult("failed-job");
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.EqualTo("boom"));
        }

        private static IJobExecutionContext CreateExecutionContext(JobKey jobKey)
        {
            IJobDetail jobDetail = Substitute.For<IJobDetail>();
            jobDetail.Key.Returns(jobKey);

            IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
            context.JobDetail.Returns(jobDetail);
            return context;
        }
    }
}
#pragma warning restore CA2012
