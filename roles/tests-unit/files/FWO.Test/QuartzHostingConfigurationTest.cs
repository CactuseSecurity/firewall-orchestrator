using FWO.Middleware.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Quartz;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class QuartzHostingConfigurationTest
    {
        /// <summary>
        /// Generous upper bound for a waited condition. Only costs time when a test is failing
        /// anyway, so a loaded runner delays the suite rather than failing it.
        /// </summary>
        private static readonly TimeSpan kConditionTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Records what the running job observed, shared with the test through DI.
        /// </summary>
        private sealed class JobProbe
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool CancellationObserved { get; set; }
            public bool CleanupDone { get; set; }
        }

        /// <summary>
        /// Works in steps with a checkpoint in between and persists its state after being cancelled,
        /// like the middleware jobs do.
        /// </summary>
        private sealed class CheckpointJob(JobProbe probe) : IJob
        {
            public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
            {
                probe.Started.TrySetResult();
                try
                {
                    DateTime deadline = DateTime.UtcNow + kConditionTimeout;
                    while (DateTime.UtcNow < deadline)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken.None);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    probe.CancellationObserved = true;
                    // cleanup must not use the cancelled token, and the shutdown has to wait for it
                    await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
                    probe.CleanupDone = true;
                }
            }
        }

        [Test]
        public async Task Shutdown_CancelsRunningJobAndWaitsForCleanup()
        {
            JobProbe probe = new();
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton(probe);
            builder.Services.AddMiddlewareQuartz(TimeProvider.System);
            using IHost host = builder.Build();
            await host.StartAsync();

            IScheduler scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
            await scheduler.ScheduleJob(
                JobBuilder.Create<CheckpointJob>().WithIdentity(nameof(CheckpointJob)).Build(),
                TriggerBuilder.Create().StartNow().Build());
            await probe.Started.Task.WaitAsync(kConditionTimeout);

            using CancellationTokenSource stopTimeout = new(kConditionTimeout);
            await host.StopAsync(stopTimeout.Token);

            ClassicAssert.IsTrue(probe.CancellationObserved, "Running job was not cancelled on shutdown.");
            ClassicAssert.IsTrue(probe.CleanupDone, "Shutdown did not wait for the job to persist its state.");
        }

        [Test]
        public void AddMiddlewareQuartz_SetsHostShutdownTimeout()
        {
            ServiceCollection services = new();
            services.AddMiddlewareQuartz(TimeProvider.System);
            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            HostOptions hostOptions = serviceProvider.GetRequiredService<IOptions<HostOptions>>().Value;

            ClassicAssert.AreEqual(QuartzHostingConfiguration.ShutdownTimeout, hostOptions.ShutdownTimeout);
        }
    }
}
