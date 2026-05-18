using FWO.Data.Middleware;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceCheckStatusTrackerTest
    {
        [Test]
        public void CreateQueuedJob_CreatesQueuedActiveJob()
        {
            ComplianceCheckStatusTracker tracker = new();

            ComplianceCheckJobStatus job = tracker.CreateQueuedJob();

            Assert.That(job.JobId, Is.Not.Empty);
            Assert.That(job.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Queued));
            Assert.That(tracker.Get(job.JobId)?.JobId, Is.EqualTo(job.JobId));
            Assert.That(tracker.GetActiveJob()?.JobId, Is.EqualTo(job.JobId));
        }

        [Test]
        public void SetRunningAndSucceeded_UpdateStatusAndClearActiveJob()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckJobStatus job = tracker.CreateQueuedJob();

            tracker.SetRunning(job.JobId);
            ComplianceCheckJobStatus? runningJob = tracker.Get(job.JobId);

            Assert.That(runningJob, Is.Not.Null);
            Assert.That(runningJob!.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Running));
            Assert.That(runningJob.FinishedAt, Is.Null);

            tracker.SetSucceeded(job.JobId);
            ComplianceCheckJobStatus? finishedJob = tracker.Get(job.JobId);

            Assert.That(finishedJob, Is.Not.Null);
            Assert.That(finishedJob!.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Succeeded));
            Assert.That(finishedJob.FinishedAt, Is.Not.Null);
            Assert.That(tracker.GetActiveJob(), Is.Null);
        }

        [Test]
        public void SetFailed_StoresFailureMessage()
        {
            ComplianceCheckStatusTracker tracker = new();
            ComplianceCheckJobStatus job = tracker.CreateQueuedJob();

            tracker.SetFailed(job.JobId, "failure");
            ComplianceCheckJobStatus? failedJob = tracker.Get(job.JobId);

            Assert.That(failedJob, Is.Not.Null);
            Assert.That(failedJob!.Status, Is.EqualTo(ComplianceCheckExecutionStatus.Failed));
            Assert.That(failedJob.Message, Is.EqualTo("failure"));
            Assert.That(failedJob.FinishedAt, Is.Not.Null);
        }
    }
}
