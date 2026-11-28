using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportSelectTimeTest
    {
        private const string kRangeRadios = "input[name=shortcutRange]";
        private const string kShortcutRadios = "input[name=shortcut]";

        private readonly SimulatedUserConfig userConfig = new();
        private int displayTimeCalls;
        private bool? shown;

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private IRenderedComponent<ReportSelectTime> Render(BunitContext context, ReportType reportType, TimeFilter actTimeFilter, TimeFilter savedTimeFilter)
        {
            return context.Render<ReportSelectTime>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, (bool display) => shown = display)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.ActTimeFilter, actTimeFilter)
                .Add(p => p.SavedTimeFilter, savedTimeFilter)
                .Add(p => p.DisplayTime, () => { displayTimeCalls++; return true; }));
        }

        private static void ClickOk(IRenderedComponent<ReportSelectTime> component)
        {
            component.Find("button.btn-primary").Click();
        }

        [Test]
        public async Task TheRadiosOfAChangeReportSwitchTheTimeRangeType()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { TimeRangeType = TimeRangeType.Shortcut };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Changes, actTimeFilter, new TimeFilter());

            component.FindAll(kRangeRadios)[1].Change(true);
            TimeRangeType afterSecond = actTimeFilter.TimeRangeType;
            component.FindAll(kRangeRadios)[2].Change(true);
            TimeRangeType afterThird = actTimeFilter.TimeRangeType;
            component.FindAll(kRangeRadios)[0].Change(true);

            Assert.That(afterSecond, Is.EqualTo(TimeRangeType.Interval));
            Assert.That(afterThird, Is.EqualTo(TimeRangeType.Fixeddates));
            Assert.That(actTimeFilter.TimeRangeType, Is.EqualTo(TimeRangeType.Shortcut));
        }

        [Test]
        public async Task AShortcutRangeIsSaved()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { TimeRangeType = TimeRangeType.Shortcut, TimeRangeShortcut = "last month" };
            TimeFilter savedTimeFilter = new() { TimeRangeType = TimeRangeType.Interval };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Changes, actTimeFilter, savedTimeFilter);

            ClickOk(component);

            Assert.That(savedTimeFilter.TimeRangeType, Is.EqualTo(TimeRangeType.Shortcut));
            Assert.That(savedTimeFilter.TimeRangeShortcut, Is.EqualTo("last month"));
            Assert.That(displayTimeCalls, Is.EqualTo(1));
            Assert.That(shown, Is.False);
        }

        [Test]
        public async Task AnIntervalIsSavedWithItsOffset()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { TimeRangeType = TimeRangeType.Interval, Offset = 3, Interval = SchedulerInterval.Weeks };
            TimeFilter savedTimeFilter = new();
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.TicketChangeReport, actTimeFilter, savedTimeFilter);

            ClickOk(component);

            Assert.That((savedTimeFilter.TimeRangeType, savedTimeFilter.Offset, savedTimeFilter.Interval),
                Is.EqualTo((TimeRangeType.Interval, 3, SchedulerInterval.Weeks)));
        }

        [Test]
        public async Task AnUnknownTimeRangeTypeIsRejected()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { TimeRangeType = (TimeRangeType)99 };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Changes, actTimeFilter, new TimeFilter());

            Assert.Throws<NotSupportedException>(() => ClickOk(component));
        }

        [Test]
        public async Task TheShortcutAndIntervalChoosersShowTheirLocalizedEntries()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Changes, new TimeFilter(), new TimeFilter());

            IRenderedComponent<Dropdown<string>> shortcuts = component.FindComponent<Dropdown<string>>();
            shortcuts.Find("input").Focus();
            IRenderedComponent<Dropdown<SchedulerInterval>> intervals = component.FindComponent<Dropdown<SchedulerInterval>>();
            intervals.Find("input").Focus();

            Assert.That(shortcuts.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()),
                Is.EquivalentTo(TimeRangeShortcuts.Ranges.Select(userConfig.GetText)));
            Assert.That(intervals.Instance.Elements, Is.EqualTo(Enum.GetValues<SchedulerInterval>().Where(interval => interval.OfferedForReport()).ToList()));
            Assert.That(intervals.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()),
                Does.Contain(userConfig.GetText(SchedulerInterval.Days.ToString())));
        }

        [Test]
        public async Task TheRadiosOfAReportAtOneTimeSwitchBetweenShortcutAndTime()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { IsShortcut = true };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Rules, actTimeFilter, new TimeFilter());

            component.FindAll(kShortcutRadios)[1].Change(true);
            bool afterTime = actTimeFilter.IsShortcut;
            component.FindAll(kShortcutRadios)[0].Change(true);

            Assert.That(afterTime, Is.False);
            Assert.That(actTimeFilter.IsShortcut, Is.True);
        }

        [Test]
        public async Task TheNowShortcutIsSaved()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new() { IsShortcut = true, TimeShortcut = "now" };
            TimeFilter savedTimeFilter = new() { IsShortcut = false, TimeShortcut = "" };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Rules, actTimeFilter, savedTimeFilter);

            IElement shortcutLabel = component.FindAll("label").First(label => label.TextContent.Trim() == userConfig.GetText("now"));
            ClickOk(component);

            Assert.That(shortcutLabel, Is.Not.Null);
            Assert.That((savedTimeFilter.IsShortcut, savedTimeFilter.TimeShortcut), Is.EqualTo((true, "now")));
        }

        [Test]
        public async Task CancellingKeepsTheSavedTime()
        {
            await using BunitContext context = CreateContext();
            TimeFilter savedTimeFilter = new() { TimeShortcut = "kept" };
            IRenderedComponent<ReportSelectTime> component = Render(context, ReportType.Rules, new TimeFilter { TimeShortcut = "changed" }, savedTimeFilter);

            component.Find("button.btn-secondary").Click();

            Assert.That(savedTimeFilter.TimeShortcut, Is.EqualTo("kept"));
            Assert.That(displayTimeCalls, Is.Zero);
            Assert.That(shown, Is.False);
        }
    }
}
