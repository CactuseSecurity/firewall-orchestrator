using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using Bunit.TestDoubles;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Basics;
using FWO.Report.Filter;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Microsoft.JSInterop;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportingParameterComponentsTest
    {
        private static readonly byte[] kSamplePdfBytes = new byte[] { 1, 2, 3 };

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate["match_source"] = "match_source";
            SimulatedUserConfig.DummyTranslate["match_destination"] = "match_destination";
            SimulatedUserConfig.DummyTranslate["match_either"] = "match_either";
            SimulatedUserConfig.DummyTranslate["match_any"] = "match_any";
            SimulatedUserConfig.DummyTranslate["match_drop_rules"] = "match_drop_rules";
            SimulatedUserConfig.DummyTranslate["show_full_rules"] = "show_full_rules";
            SimulatedUserConfig.DummyTranslate["rules_for_deleted_conns"] = "rules_for_deleted_conns";
            SimulatedUserConfig.DummyTranslate["analyse_remaining_rules"] = "analyse_remaining_rules";
            SimulatedUserConfig.DummyTranslate["report_time"] = "report_time";
            SimulatedUserConfig.DummyTranslate["shortcut"] = "shortcut";
            SimulatedUserConfig.DummyTranslate["last"] = "last";
            SimulatedUserConfig.DummyTranslate["start_time"] = "start_time";
            SimulatedUserConfig.DummyTranslate["end_time"] = "end_time";
            SimulatedUserConfig.DummyTranslate["time"] = "time";
            SimulatedUserConfig.DummyTranslate["open"] = "open";
            SimulatedUserConfig.DummyTranslate["ok"] = "ok";
            SimulatedUserConfig.DummyTranslate["close"] = "close";
            SimulatedUserConfig.DummyTranslate["export_report_download"] = "export_report_download";
            SimulatedUserConfig.DummyTranslate["download_csv"] = "download_csv";
            SimulatedUserConfig.DummyTranslate["download_pdf"] = "download_pdf";
            SimulatedUserConfig.DummyTranslate["download_html"] = "download_html";
            SimulatedUserConfig.DummyTranslate["download_json"] = "download_json";
        }

        [Test]
        public void ReportAppRuleParamSelection_FormLayout_TogglesAllFlags()
        {
            using BunitContext context = CreateContext();
            ModellingFilter modellingFilter = new()
            {
                ShowSourceMatch = true,
                ShowDestinationMatch = false,
                ShowAnyMatch = false,
                ShowDropRules = false,
                ShowFullRules = false
            };
            ModellingFilter? changedFilter = null;

            IRenderedComponent<ReportAppRuleParamSelection> component = context.Render<ReportAppRuleParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, modellingFilter)
                .Add(p => p.UseFormLayout, true)
                .Add(p => p.UseLightText, false)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            Assert.That(component.Markup, Does.Contain("form-group row"));
            Assert.That(component.Markup, Does.Contain("form-check-label"));
            Assert.That(component.Markup, Does.Not.Contain("text-white"));

            component.Find("#appRuleDstMatch").Change(true);
            Assert.That(modellingFilter.ShowSourceMatch, Is.False);
            Assert.That(modellingFilter.ShowDestinationMatch, Is.True);
            Assert.That(changedFilter, Is.SameAs(modellingFilter));

            component.Find("#appRuleEitherMatch").Change(true);
            Assert.That(modellingFilter.ShowSourceMatch, Is.True);
            Assert.That(modellingFilter.ShowDestinationMatch, Is.True);

            component.Find("#appRuleAnyMatch").Change(true);
            component.Find("#appRuleDropMatch").Change(true);
            component.Find("#appRuleShowFull").Change(true);

            Assert.Multiple(() =>
            {
                Assert.That(modellingFilter.ShowAnyMatch, Is.True);
                Assert.That(modellingFilter.ShowDropRules, Is.True);
                Assert.That(modellingFilter.ShowFullRules, Is.True);
            });
        }

        [Test]
        public void ReportAppRuleParamSelection_NonFormLayout_UsesPaddedWrapperAndWhiteLabels()
        {
            using BunitContext context = CreateContext();
            IRenderedComponent<ReportAppRuleParamSelection> component = context.Render<ReportAppRuleParamSelection>(parameters => parameters
                .Add(p => p.UseFormLayout, false)
                .Add(p => p.UseLightText, true));

            Assert.That(component.Markup, Does.Contain("p-3"));
            Assert.That(component.Markup, Does.Contain("text-white"));
        }

        [Test]
        public void ReportVarianceParamSelection_FormLayout_TogglesBothFlags()
        {
            using BunitContext context = CreateContext();
            ModellingFilter modellingFilter = new()
            {
                RulesForDeletedConns = false,
                AnalyseRemainingRules = false
            };
            ModellingFilter? changedFilter = null;

            IRenderedComponent<ReportVarianceParamSelection> component = context.Render<ReportVarianceParamSelection>(parameters => parameters
                .Add(p => p.ModellingFilter, modellingFilter)
                .Add(p => p.UseFormLayout, true)
                .Add(p => p.UseLightText, false)
                .Add(p => p.ModellingFilterChanged, updated => changedFilter = updated));

            Assert.That(component.Markup, Does.Contain("form-group row"));

            component.Find("#varianceDeletedConns").Change(true);
            component.Find("#varianceAnalyseRemaining").Change(true);

            Assert.Multiple(() =>
            {
                Assert.That(modellingFilter.RulesForDeletedConns, Is.True);
                Assert.That(modellingFilter.AnalyseRemainingRules, Is.True);
                Assert.That(changedFilter, Is.SameAs(modellingFilter));
            });
        }

        [Test]
        public void ReportVarianceParamSelection_NonFormLayout_UsesPaddedWrapper()
        {
            using BunitContext context = CreateContext();
            IRenderedComponent<ReportVarianceParamSelection> component = context.Render<ReportVarianceParamSelection>(parameters => parameters
                .Add(p => p.UseFormLayout, false));

            Assert.That(component.Markup, Does.Contain("p-3"));
        }

        [Test]
        public async Task ReportSelectTime_ChangeReport_RendersRangeInputsAndSavesSelection()
        {
            await using BunitContext context = CreateContext(addDomEventService: true);
            TimeFilter actTimeFilter = new()
            {
                TimeRangeType = TimeRangeType.Fixeddates,
                StartTime = new DateTime(2026, 1, 2, 10, 15, 0),
                EndTime = new DateTime(2026, 1, 2, 11, 30, 0),
                OpenStart = true,
                OpenEnd = false
            };
            TimeFilter savedTimeFilter = new();
            bool displayChanged = true;
            int displayTimeCalls = 0;

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> component = RenderSelectTime(context, ReportType.Changes, actTimeFilter, savedTimeFilter, () =>
            {
                displayTimeCalls++;
                return true;
            }, shown => displayChanged = shown);

            Assert.That(component.Markup, Does.Contain("start_time"));
            Assert.That(component.Markup, Does.Contain("end_time"));
            Assert.That(component.Find("#startTimeDate"), Is.Not.Null);
            Assert.That(component.Find("#endTimeDate"), Is.Not.Null);

            component.Find("button.btn.btn-sm.btn-primary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(displayTimeCalls, Is.EqualTo(1));
                Assert.That(savedTimeFilter.TimeRangeType, Is.EqualTo(TimeRangeType.Fixeddates));
                Assert.That(savedTimeFilter.StartTime, Is.EqualTo(actTimeFilter.StartTime));
                Assert.That(savedTimeFilter.EndTime, Is.EqualTo(actTimeFilter.EndTime));
                Assert.That(savedTimeFilter.OpenStart, Is.True);
                Assert.That(savedTimeFilter.OpenEnd, Is.False);
            });
        }

        [Test]
        public async Task ReportSelectTime_ChangeReport_InvalidRange_ReportsValidationError()
        {
            await using BunitContext context = CreateContext(addDomEventService: true);
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = new();
            TimeFilter actTimeFilter = new()
            {
                TimeRangeType = TimeRangeType.Fixeddates,
                StartTime = new DateTime(2026, 1, 3, 10, 0, 0),
                EndTime = new DateTime(2026, 1, 2, 10, 0, 0)
            };
            TimeFilter savedTimeFilter = new();
            bool displayChanged = true;
            int displayTimeCalls = 0;

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> component = RenderSelectTime(context, ReportType.Changes, actTimeFilter, savedTimeFilter, () =>
            {
                displayTimeCalls++;
                return true;
            }, shown => displayChanged = shown, messages);

            component.Find("button.btn.btn-sm.btn-primary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayTimeCalls, Is.EqualTo(0));
                Assert.That(displayChanged, Is.True);
                Assert.That(savedTimeFilter.StartTime, Is.Not.EqualTo(actTimeFilter.StartTime));
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Message, Is.EqualTo("E1011"));
            });
        }

        [Test]
        public async Task ReportSelectTime_NonChangeReport_RendersReportTimeAndSavesSelection()
        {
            await using BunitContext context = CreateContext();
            TimeFilter actTimeFilter = new()
            {
                IsShortcut = false,
                ReportTime = new DateTime(2026, 7, 27, 13, 45, 0)
            };
            TimeFilter savedTimeFilter = new();
            bool displayChanged = true;
            int displayTimeCalls = 0;

            IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> component = RenderSelectTime(context, ReportType.OwnerRecertification, actTimeFilter, savedTimeFilter, () =>
            {
                displayTimeCalls++;
                return true;
            }, shown => displayChanged = shown);

            Assert.That(component.Markup, Does.Contain("reportTimeDate"));
            Assert.That(component.Markup, Does.Not.Contain("startTimeDate"));

            component.Find("button.btn.btn-sm.btn-primary").Click();

            Assert.Multiple(() =>
            {
                Assert.That(displayChanged, Is.False);
                Assert.That(displayTimeCalls, Is.EqualTo(1));
                Assert.That(savedTimeFilter.IsShortcut, Is.False);
                Assert.That(savedTimeFilter.ReportTime, Is.EqualTo(actTimeFilter.ReportTime));
            });
        }

        [Test]
        public async Task ReportDownloadPopUp_RendersDownloads_InvokesJsAndClose()
        {
            using BunitContext context = CreateContext();
            JSRuntimeInvocationHandler downloadInvocation = context.JSInterop.SetupVoid("DownloadFile", _ => true).SetVoidResult();
            bool closed = false;
            ReportFile reportFile = new()
            {
                Name = "report",
                Csv = "csv-content",
                Pdf = Convert.ToBase64String(kSamplePdfBytes),
                Html = "<html>report</html>",
                Json = "{ \"value\": 1 }"
            };

            IRenderedComponent<ReportDownloadPopUp> component = context.Render<ReportDownloadPopUp>(parameters => parameters
                .Add(p => p.Show, true)
                .Add(p => p.ShowJson, true)
                .Add(p => p.ReportFile, reportFile)
                .Add(p => p.OnClose, () => closed = true));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("download_csv"));
                Assert.That(component.Markup, Does.Contain("download_pdf"));
                Assert.That(component.Markup, Does.Contain("download_html"));
                Assert.That(component.Markup, Does.Contain("download_json"));
            });

            component.Find("btn.btn.btn-sm.btn-info.m-1").Click();

            component.WaitForAssertion(() =>
            {
                Assert.That(downloadInvocation.Invocations, Is.Not.Empty);
                JSRuntimeInvocation invocation = downloadInvocation.Invocations.First();
                Assert.That(invocation.Arguments[0], Is.EqualTo("report.csv"));
                Assert.That(invocation.Arguments[1], Is.EqualTo("text/csv"));
            });

            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("btn-danger")));
            component.Find("button.btn.btn-sm.btn-danger").Click();
            Assert.That(closed, Is.True);
        }

        [Test]
        public void ReportDownloadPopUp_HidesJsonWhenDisabled()
        {
            using BunitContext context = CreateContext();
            ReportFile reportFile = new()
            {
                Name = "report",
                Json = "{ \"value\": 1 }"
            };

            IRenderedComponent<ReportDownloadPopUp> component = context.Render<ReportDownloadPopUp>(parameters => parameters
                .Add(p => p.Show, true)
                .Add(p => p.ShowJson, false)
                .Add(p => p.ReportFile, reportFile));

            Assert.That(component.Markup, Does.Not.Contain("download_json"));
        }

        private static BunitContext CreateContext(bool addDomEventService = false)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            if (addDomEventService)
            {
                context.Services.AddScoped<DomEventService>();
            }

            return context;
        }

        private static IRenderedComponent<CascadingValue<Action<Exception?, string, string, bool>>> RenderSelectTime(
            BunitContext context,
            ReportType reportType,
            TimeFilter actTimeFilter,
            TimeFilter savedTimeFilter,
            Func<bool> displayTime,
            Action<bool> onDisplayChanged,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            Action<Exception?, string, string, bool> displayMessage = (exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            };

            return context.Render<CascadingValue<Action<Exception?, string, string, bool>>>(parameters => parameters
                .Add(p => p.Value, displayMessage)
                .AddChildContent<ReportSelectTime>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.DisplayChanged, shown => onDisplayChanged(shown))
                    .Add(p => p.SelectedReportType, reportType)
                    .Add(p => p.ActTimeFilter, actTimeFilter)
                    .Add(p => p.SavedTimeFilter, savedTimeFilter)
                    .Add(p => p.DisplayTime, displayTime)));
        }

    }
}
