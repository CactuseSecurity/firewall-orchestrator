using Bunit;
using AngleSharp.Dom;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Services.SystemUsage;
using FWO.Ui.Pages.Monitoring;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiMonitorSystemUsageTest
    {
        private const long kMegaByte = 1024 * 1024;
        private static readonly TimeSpan kAsyncOperationTimeout = TimeSpan.FromSeconds(5);

        private static SystemUsageSnapshot CreateSnapshot(double cpuPercent = 25)
        {
            return new SystemUsageSnapshot
            {
                CollectedAt = new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc),
                SourceAvailable = true,
                MemoryTotalBytes = 16000 * kMegaByte,
                MemoryFreeBytes = 2000 * kMegaByte,
                MemoryAvailableBytes = 8000 * kMegaByte,
                SwapTotalBytes = 4000 * kMegaByte,
                SwapFreeBytes = 3000 * kMegaByte,
                CpuUsedPercent = cpuPercent,
                LoadAverage1 = 0.5,
                LoadAverage5 = 1.5,
                LoadAverage15 = 2.5,
                ProcessorCount = 8,
                ProcessCpuPercent = 3,
                ProcessWorkingSetBytes = 400 * kMegaByte,
                ProcessManagedHeapBytes = 120 * kMegaByte,
                ProcessThreadCount = 42,
                ProcessStartTime = new DateTime(2026, 7, 29, 8, 0, 0, DateTimeKind.Utc),
                Services = CreateServices()
            };
        }

        private static List<ServiceUsage> CreateServices()
        {
            return
            [
                new ServiceUsage
                {
                    NameKey = "middleware",
                    ProcessCount = 1,
                    CpuPercent = 12.5,
                    MemoryBytes = 200 * kMegaByte,
                    MemoryPercent = 1.25,
                    ThreadCount = 25,
                    UpTime = TimeSpan.FromMinutes(90)
                },
                new ServiceUsage
                {
                    NameKey = "database",
                    ProcessCount = 7,
                    CpuPercent = 3,
                    MemoryBytes = 800 * kMegaByte,
                    MemoryPercent = 5,
                    ThreadCount = 7,
                    UpTime = TimeSpan.FromHours(26)
                }
            ];
        }

        private static IRenderedComponent<MonitorSystemUsage> Render(BunitContext context,
            FakeSystemUsageCollector collector, UiSessionTracker tracker, params string[] roles)
        {
            return Render(context, collector, tracker, new FakePeriodicTaskRunnerFactory(), roles);
        }

        private static IRenderedComponent<MonitorSystemUsage> Render(BunitContext context,
            FakeSystemUsageCollector collector, UiSessionTracker tracker,
            FakePeriodicTaskRunnerFactory runnerFactory, params string[] roles)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(roles));
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [.. roles];
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ISystemUsageCollector>(collector);
            context.Services.AddSingleton(tracker);
            context.Services.AddSingleton<IPeriodicTaskRunnerFactory>(runnerFactory);

            IRenderedComponent<CascadingAuthenticationState> rendered = context.Render<CascadingAuthenticationState>(
                parameters => parameters.AddChildContent<MonitorSystemUsage>());
            return rendered.FindComponent<MonitorSystemUsage>();
        }

        private static T GetPrivateProperty<T>(MonitorSystemUsage page, string propertyName)
        {
            PropertyInfo property = typeof(MonitorSystemUsage).GetProperty(propertyName,
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(MonitorSystemUsage).FullName, propertyName);
            return (T)property.GetValue(page)!;
        }

        private static T GetPrivateField<T>(MonitorSystemUsage page, string fieldName)
        {
            FieldInfo field = typeof(MonitorSystemUsage).GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(MonitorSystemUsage).FullName, fieldName);
            return (T)field.GetValue(page)!;
        }

        private static void SetPrivateProperty<T>(MonitorSystemUsage page, string propertyName, T value)
        {
            PropertyInfo property = typeof(MonitorSystemUsage).GetProperty(propertyName,
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(MonitorSystemUsage).FullName, propertyName);
            property.SetValue(page, value);
        }

        private static void InvokePrivateMethod(MonitorSystemUsage page, string methodName)
        {
            MethodInfo method = typeof(MonitorSystemUsage).GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitorSystemUsage).FullName, methodName);
            method.Invoke(page, null);
        }

        private static async Task WaitForExecutionModeUpdate(IRenderedComponent<MonitorSystemUsage> page)
        {
            try
            {
                await page.InvokeAsync(async () =>
                {
                    SemaphoreSlim semaphore = GetPrivateField<SemaphoreSlim>(page.Instance, "monitoringStateSemaphore");
                    await semaphore.WaitAsync();
                    semaphore.Release();
                }).WaitAsync(kAsyncOperationTimeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("The execution-mode update did not complete within the expected time.");
            }
        }

        [Test]
        public void Page_ShowsSessionAndUsageValues()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();
            tracker.Register("session1");
            tracker.Register("session2");
            tracker.SetUser("session1", "tim");
            tracker.SetUser("session2", "tim");
            tracker.SetConnected("session1", true);

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);
            List<string> tileValues = [.. page.FindAll(".usage-tile-value").Select(tile => tile.TextContent.Trim())];

            Assert.Multiple(() =>
            {
                // one user holding two sessions, one of them connected
                Assert.That(tileValues[0], Is.EqualTo("1"));
                Assert.That(tileValues[1], Is.EqualTo("2"));
                Assert.That(tileValues[2], Is.EqualTo("8"));
                // 8000 MB available of 16000 MB total
                Assert.That(tileValues[3], Is.EqualTo("7.8 GB"));
                Assert.That(page.Markup, Does.Contain("15.6 GB"));
                Assert.That(page.Markup, Does.Contain("0.50 / 1.50 / 2.50"));
                Assert.That(page.Markup, Does.Not.Contain("alert-warning"));
            });
        }

        [Test]
        public void Page_ShowsAvailableMemoryOnlyWithoutFreeMemory()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);

            Assert.Multiple(() =>
            {
                // 8000 MB available, the free memory of 2000 MB would only be confusing next to it
                Assert.That(page.Markup, Does.Contain("memory_available: 7.8 GB"));
                Assert.That(page.Markup, Does.Not.Contain("memory_free"));
            });
        }

        [Test]
        public void Page_ShowsUsageOfTheOtherServicesOnThisHost()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);
            List<string> serviceCells = [.. page.FindAll("tbody tr td").Select(cell => cell.TextContent.Trim())];

            Assert.Multiple(() =>
            {
                // one row per service: name, cpu, memory, memory share, threads, processes, uptime
                Assert.That(serviceCells, Has.Count.EqualTo(14));
                Assert.That(serviceCells.GetRange(0, 7),
                    Is.EqualTo(new List<string> { "middleware", "12.5 %", "200 MB", "1.3 %", "25", "1", "01:30" }));
                Assert.That(serviceCells.GetRange(7, 7),
                    Is.EqualTo(new List<string> { "database", "3 %", "800 MB", "5 %", "7", "7", "1d 02:00" }));
                Assert.That(page.Markup, Does.Not.Contain("no_services_found"));
            });
        }

        [Test]
        public void Page_WithoutOtherServicesShowsHintInsteadOfTable()
        {
            using BunitContext context = new();
            SystemUsageSnapshot snapshot = CreateSnapshot();
            snapshot.Services = [];
            FakeSystemUsageCollector collector = new(snapshot);
            UiSessionTracker tracker = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);

            Assert.Multiple(() =>
            {
                Assert.That(page.Markup, Does.Contain("no_services_found"));
                Assert.That(page.FindAll("tbody tr"), Is.Empty);
            });
        }

        [Test]
        public void Page_WithHiddenProcessesDoesNotClaimThatNoServicesRun()
        {
            using BunitContext context = new();
            SystemUsageSnapshot snapshot = CreateSnapshot();
            snapshot.Services = [];
            snapshot.ServicesVisible = false;
            FakeSystemUsageCollector collector = new(snapshot);

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);

            Assert.Multiple(() =>
            {
                Assert.That(page.Markup, Does.Contain("services_not_visible"));
                // stating that nothing runs here would be wrong, the services are only invisible
                Assert.That(page.Markup, Does.Not.Contain("no_services_found"));
            });
        }

        [Test]
        public void Page_WithHiddenProcessesStillShowsTheServicesItFound()
        {
            using BunitContext context = new();
            SystemUsageSnapshot snapshot = CreateSnapshot();
            snapshot.ServicesVisible = false;
            FakeSystemUsageCollector collector = new(snapshot);

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);

            Assert.Multiple(() =>
            {
                // the list is incomplete, but what could be read stays visible next to the warning
                Assert.That(page.Markup, Does.Contain("services_not_visible"));
                Assert.That(page.FindAll("tbody tr"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public void Page_CollectsInitialSampleOnOpen()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);

            Assert.Multiple(() =>
            {
                Assert.That(collector.CollectCount, Is.EqualTo(1));
                Assert.That(GetPrivateProperty<List<double>>(page.Instance, "CpuHistory"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateProperty<List<double>>(page.Instance, "MemoryHistory"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void Refresh_AppendsToHistory()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);

            collector.Snapshot = CreateSnapshot(70);
            InvokePrivateMethod(page.Instance, "Refresh");

            List<double> cpuHistory = GetPrivateProperty<List<double>>(page.Instance, "CpuHistory");
            Assert.Multiple(() =>
            {
                Assert.That(cpuHistory, Has.Count.EqualTo(2));
                Assert.That(cpuHistory[1], Is.EqualTo(70));
            });
        }

        [Test]
        public void Refresh_KeepsHistoryBounded()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            UiSessionTracker tracker = new();
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, tracker, Roles.Admin);

            for (int sample = 0; sample < 100; sample++)
            {
                InvokePrivateMethod(page.Instance, "Refresh");
            }

            Assert.That(GetPrivateProperty<List<double>>(page.Instance, "CpuHistory"), Has.Count.EqualTo(60));
        }

        [Test]
        public async Task RefreshCallback_OfTheTimerUpdatesTheHistory()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            FakePeriodicTaskRunnerFactory factory = new();
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), factory, Roles.Admin);
            SynchronizationContext? rendererSynchronizationContext = collector.LastSynchronizationContext;

            collector.Snapshot = CreateSnapshot(80);
            await Task.Run(() => factory.LastRunner!.Callback());

            List<double> cpuHistory = GetPrivateProperty<List<double>>(page.Instance, "CpuHistory");
            Assert.Multiple(() =>
            {
                Assert.That(rendererSynchronizationContext, Is.Not.Null);
                Assert.That(cpuHistory, Has.Count.EqualTo(2));
                Assert.That(cpuHistory[1], Is.EqualTo(80));
                Assert.That(collector.LastSynchronizationContext, Is.SameAs(rendererSynchronizationContext));
            });
        }

        [Test]
        public void Refresh_WithFailingCollectorReportsTheError()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot()) { ThrowOnCollect = true };

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);

            Assert.Multiple(() =>
            {
                // the page must stay usable even when the counters cannot be read
                Assert.That(GetPrivateProperty<List<double>>(page.Instance, "CpuHistory"), Is.Empty);
                Assert.That(page.Markup, Does.Contain("alert-warning"));
            });
        }

        [Test]
        public void Refresh_ReportsAFailureStreakOnlyOnce()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);
            List<string> messages = [];
            SetPrivateProperty(page.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, _, _) => messages.Add(title)));

            collector.ThrowOnCollect = true;
            // the timer samples every few seconds, a lasting failure must not repeat the message every tick
            InvokePrivateMethod(page.Instance, "Refresh");
            InvokePrivateMethod(page.Instance, "Refresh");
            InvokePrivateMethod(page.Instance, "Refresh");

            Assert.That(messages, Has.Count.EqualTo(1));
        }

        [Test]
        public void Refresh_ReportsAgainAfterTheCountersRecovered()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);
            List<string> messages = [];
            SetPrivateProperty(page.Instance, "DisplayMessageInUi",
                new Action<Exception?, string, string, bool>((_, title, _, _) => messages.Add(title)));

            collector.ThrowOnCollect = true;
            InvokePrivateMethod(page.Instance, "Refresh");
            collector.ThrowOnCollect = false;
            InvokePrivateMethod(page.Instance, "Refresh");
            collector.ThrowOnCollect = true;
            InvokePrivateMethod(page.Instance, "Refresh");

            // a new failure after a successful sample is a new incident and is reported again
            Assert.That(messages, Has.Count.EqualTo(2));
        }

        [Test]
        public void Sparkline_DrawsOneScaledPointPerSample()
        {
            using BunitContext context = new();
            List<double> values = [0, 50, 100];

            IRenderedComponent<UsageSparkline> sparkline = context.Render<UsageSparkline>(parameters =>
                parameters.Add(component => component.Values, values)
                    .Add(component => component.Caption, "cpu")
                    .Add(component => component.StartTime, "10:00:00")
                    .Add(component => component.EndTime, "10:05:00"));

            Assert.Multiple(() =>
            {
                // 3 samples over a viewbox of 100 x 30: x = 0/50/100, y = 30/15/0
                Assert.That(sparkline.Markup, Does.Contain(@"points=""0,30 50,15 100,0"""));
                Assert.That(sparkline.Markup, Does.Contain(@"points=""0,30 0,30 50,15 100,0 100,30"""));
                Assert.That(sparkline.Find(".usage-sparkline-time").TextContent, Does.Contain("10:00:00"));
                Assert.That(sparkline.Find(".usage-sparkline-time").TextContent, Does.Contain("10:05:00"));
            });
        }

        [Test]
        public void Page_ShowsActualTimeRangeBelowUsageHistories()
        {
            using BunitContext context = new();
            SystemUsageSnapshot firstSnapshot = CreateSnapshot();
            FakeSystemUsageCollector collector = new(firstSnapshot);
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);
            SystemUsageSnapshot secondSnapshot = CreateSnapshot(30);
            secondSnapshot.CollectedAt = firstSnapshot.CollectedAt.AddSeconds(5);

            collector.Snapshot = secondSnapshot;
            InvokePrivateMethod(page.Instance, "Refresh");
            page.Render();

            string expectedStart = firstSnapshot.CollectedAt.ToLocalTime().ToString("T");
            string expectedEnd = secondSnapshot.CollectedAt.ToLocalTime().ToString("T");
            IReadOnlyList<IElement> timeRanges = page.FindAll(".usage-sparkline-time");
            Assert.Multiple(() =>
            {
                Assert.That(timeRanges, Has.Count.EqualTo(2));
                Assert.That(timeRanges[0].TextContent, Does.Contain(expectedStart));
                Assert.That(timeRanges[0].TextContent, Does.Contain(expectedEnd));
            });
        }

        [Test]
        public void Sparkline_ScalesAgainstTheGivenMaximum()
        {
            using BunitContext context = new();
            List<double> values = [0, 4, 8];

            IRenderedComponent<UsageSparkline> sparkline = context.Render<UsageSparkline>(parameters =>
                parameters.Add(component => component.Values, values).Add(component => component.MaxValue, 8));

            Assert.That(sparkline.Markup, Does.Contain(@"points=""0,30 50,15 100,0"""));
        }

        [Test]
        public void Sparkline_WithoutEnoughSamplesShowsPlaceholder()
        {
            using BunitContext context = new();
            List<double> values = [42];

            IRenderedComponent<UsageSparkline> sparkline = context.Render<UsageSparkline>(parameters =>
                parameters.Add(component => component.Values, values).Add(component => component.EmptyText, "collecting"));

            Assert.Multiple(() =>
            {
                Assert.That(sparkline.Markup, Does.Contain("collecting"));
                Assert.That(sparkline.Markup, Does.Not.Contain("<svg"));
            });
        }

        [Test]
        public void Sparkline_WithInvalidMaximumDoesNotDivideByZero()
        {
            using BunitContext context = new();
            List<double> values = [0, 1];

            IRenderedComponent<UsageSparkline> sparkline = context.Render<UsageSparkline>(parameters =>
                parameters.Add(component => component.Values, values).Add(component => component.MaxValue, 0));

            Assert.That(sparkline.Markup, Does.Contain(@"points=""0,30 100,0"""));
        }

        [Test]
        public void Page_WithUnavailableSourceShowsWarning()
        {
            using BunitContext context = new();
            SystemUsageSnapshot snapshot = CreateSnapshot();
            snapshot.SourceAvailable = false;
            FakeSystemUsageCollector collector = new(snapshot);

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Admin);

            Assert.That(page.Markup, Does.Contain("alert-warning"));
        }

        [Test]
        public void Page_WithoutAdminRolesDoesNotCollect()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), Roles.Reporter);

            Assert.Multiple(() =>
            {
                Assert.That(collector.CollectCount, Is.EqualTo(0));
                Assert.That(page.Markup, Does.Not.Contain("usage-sparkline"));
            });
        }

        [Test]
        public async Task Page_ExecutionModeChangeStartsAndStopsMonitoring()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            FakePeriodicTaskRunnerFactory factory = new();
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector,
                new UiSessionTracker(), factory, Roles.Admin, Roles.Reporter);
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            Assert.Multiple(() =>
            {
                Assert.That(collector.CollectCount, Is.EqualTo(0));
                Assert.That(factory.CreateCount, Is.EqualTo(0));
            });

            userConfig.SetExecutionMode(Roles.Admin);
            await WaitForExecutionModeUpdate(page);
            Assert.Multiple(() =>
            {
                Assert.That(collector.CollectCount, Is.EqualTo(1));
                Assert.That(factory.CreateCount, Is.EqualTo(1));
                Assert.That(factory.LastRunner!.Started, Is.True);
                Assert.That(page.Markup, Does.Contain("usage-sparkline"));
            });
            FakePeriodicTaskRunner activeRunner = factory.LastRunner!;

            userConfig.SetExecutionMode(GlobalConst.kUserRolesSelection);
            await WaitForExecutionModeUpdate(page);

            Assert.Multiple(() =>
            {
                Assert.That(activeRunner.Disposed, Is.True);
                Assert.That(factory.CreateCount, Is.EqualTo(1));
                Assert.That(factory.LastRunner, Is.SameAs(activeRunner));
                Assert.That(page.Markup, Does.Not.Contain("usage-sparkline"));
            });
        }

        [Test]
        public async Task DisposeAsync_StopsTheRefreshRunner()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            FakePeriodicTaskRunnerFactory factory = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), factory, Roles.Admin);
            await page.Instance.DisposeAsync();
            // disposing twice must stay harmless
            await page.Instance.DisposeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(factory.LastRunner, Is.Not.Null);
                Assert.That(factory.LastRunner!.Started, Is.True);
                Assert.That(factory.LastRunner!.Disposed, Is.True);
            });
        }

        [Test]
        public async Task DisposeAsync_ShutsTheRunnerDownWithoutBlockingTheCaller()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            FakePeriodicTaskRunnerFactory factory = new();
            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), factory, Roles.Admin);

            // the real runner waits for its loop to end, and that loop needs the render dispatcher:
            // the shutdown therefore has to be awaited instead of blocking the caller
            using ManualResetEventSlim runnerShutdown = new(false);
            factory.LastRunner!.DisposeGate = runnerShutdown;

            ValueTask disposal = page.Instance.DisposeAsync();
            bool completedWhileRunnerWasBlocked = disposal.IsCompleted;
            runnerShutdown.Set();
            await disposal;

            Assert.Multiple(() =>
            {
                Assert.That(completedWhileRunnerWasBlocked, Is.False);
                Assert.That(factory.LastRunner.Disposed, Is.True);
            });
        }

        [Test]
        public void Page_StartsTheRefreshTimerOnlyAfterTheFirstRender()
        {
            using BunitContext context = new();
            FakeSystemUsageCollector collector = new(CreateSnapshot());
            FakePeriodicTaskRunnerFactory factory = new();

            IRenderedComponent<MonitorSystemUsage> page = Render(context, collector, new UiSessionTracker(), factory, Roles.Admin);
            page.Render();

            Assert.Multiple(() =>
            {
                // the timer must never be created more than once, no matter how often the page re-renders
                Assert.That(factory.CreateCount, Is.EqualTo(1));
                Assert.That(factory.LastRunner!.Started, Is.True);
            });
        }

        internal sealed class FakeSystemUsageCollector(SystemUsageSnapshot snapshot) : ISystemUsageCollector
        {
            public SystemUsageSnapshot Snapshot { get; set; } = snapshot;

            public bool ThrowOnCollect { get; set; }

            public int CollectCount { get; private set; }

            public SynchronizationContext? LastSynchronizationContext { get; private set; }

            public SystemUsageSnapshot Collect()
            {
                CollectCount++;
                LastSynchronizationContext = SynchronizationContext.Current;
                return ThrowOnCollect ? throw new InvalidOperationException("counters unavailable") : Snapshot;
            }
        }

        internal sealed class FakePeriodicTaskRunner(Func<Task> callback) : IPeriodicTaskRunner
        {
            public Func<Task> Callback { get; } = callback;
            public bool Started { get; private set; }
            public bool Disposed { get; private set; }

            /// <summary>
            /// Optional gate letting a test hold up the shutdown, imitating the blocking dispose of the
            /// real <see cref="PeriodicTaskRunner"/>.
            /// </summary>
            public ManualResetEventSlim? DisposeGate { get; set; }

            public void Start()
            {
                Started = true;
            }

            public void Dispose()
            {
                DisposeGate?.Wait();
                Disposed = true;
            }

            /// <inheritdoc />
            public async ValueTask DisposeAsync()
            {
                await Task.Run(Dispose);
            }
        }

        internal sealed class FakePeriodicTaskRunnerFactory : IPeriodicTaskRunnerFactory
        {
            public FakePeriodicTaskRunner? LastRunner { get; private set; }

            public int CreateCount { get; private set; }

            public IPeriodicTaskRunner Create(Func<Task> callback, TimeSpan interval, string taskName = "")
            {
                CreateCount++;
                LastRunner = new FakePeriodicTaskRunner(callback);
                return LastRunner;
            }
        }
    }
}
