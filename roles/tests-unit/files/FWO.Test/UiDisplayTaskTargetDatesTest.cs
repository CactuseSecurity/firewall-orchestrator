using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Flow;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.EventMediator;
using FWO.Ui.Pages.Request;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static FWO.Test.UiRequestWorkflowTest;

namespace FWO.Test
{
    [TestFixture]
    internal class UiDisplayTaskTargetDatesTest
    {
        [Test]
        public async Task DisplayTaskTargetDates_EditableDatesUpdateParentValues()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            DateTime? targetEndDate = new DateTime(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> dateInputs = component.FindAll("input[type=date]");
            IReadOnlyList<IElement> timeInputs = component.FindAll("input[type=time]");
            await dateInputs[0].InputAsync(new ChangeEventArgs { Value = "2026-08-15" });
            await timeInputs[0].InputAsync(new ChangeEventArgs { Value = "12:34:56" });
            await dateInputs[1].InputAsync(new ChangeEventArgs { Value = "2026-08-31" });
            await timeInputs[1].InputAsync(new ChangeEventArgs { Value = "23:59:58" });

            Assert.Multiple(() =>
            {
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 8, 15, 12, 34, 56)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 8, 31, 23, 59, 58)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_IncompleteDateTimeMarksInputInvalid()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            bool targetDatesValid = true;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true)
                .Add(p => p.TargetDatesValidChanged, EventCallback.Factory.Create<bool>(this, value => targetDatesValid = value)));

            await component.FindAll("input[type=time]")[0].InputAsync(new ChangeEventArgs { Value = "" });

            Assert.Multiple(() =>
            {
                Assert.That(targetDatesValid, Is.False);
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 8, 15, 30)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_DateSelectionDefaultsMissingTimes()
        {
            DateTime? targetBeginDate = null;
            DateTime? targetEndDate = null;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> dateInputs = component.FindAll("input[type=date]");
            await dateInputs[0].InputAsync(new ChangeEventArgs { Value = "2026-08-15" });
            await dateInputs[1].InputAsync(new ChangeEventArgs { Value = "2026-08-31" });

            Assert.Multiple(() =>
            {
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 8, 15, 0, 0, 0)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 8, 31, 23, 59, 59)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_TimeWithoutSecondsIsAcceptedAsZeroSeconds()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            bool targetDatesValid = true;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true)
                .Add(p => p.TargetDatesValidChanged, EventCallback.Factory.Create<bool>(this, value => targetDatesValid = value)));

            await component.FindAll("input[type=time]")[0].InputAsync(new ChangeEventArgs { Value = "12:34" });

            Assert.Multiple(() =>
            {
                Assert.That(targetDatesValid, Is.True);
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 12, 34, 0)));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_HoursPrecisionUsesHourNumberInputs()
        {
            DateTime? targetBeginDate = new DateTime(2026, 7, 1, 8, 15, 30);
            DateTime? targetEndDate = new DateTime(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Hours
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetBeginDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetBeginDate = value))
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.TargetEndDateChanged, EventCallback.Factory.Create<DateTime?>(this, value => targetEndDate = value))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IReadOnlyList<IElement> hourInputs = component.FindAll("input[type=number]");
            await hourInputs[0].InputAsync(new ChangeEventArgs { Value = "13" });
            await hourInputs[1].InputAsync(new ChangeEventArgs { Value = "1" });

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input[type=time]"), Is.Empty);
                Assert.That(hourInputs, Has.Count.EqualTo(2));
                Assert.That(hourInputs[0].GetAttribute("min"), Is.EqualTo("0"));
                Assert.That(hourInputs[0].GetAttribute("max"), Is.EqualTo("23"));
                Assert.That(targetBeginDate, Is.EqualTo(new DateTime(2026, 7, 1, 13, 0, 0)));
                Assert.That(targetEndDate, Is.EqualTo(new DateTime(2026, 7, 31, 1, 0, 0)));
            });
        }

        [TestCase(FlowIntegrationTimePrecisionOptions.Date, "2026-07-01", "2026-07-31", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "2026-07-01 8 h", "2026-07-31 17 h", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "2026-07-01 08:15", "2026-07-31 17:45", "08:15:30", "17:45:15")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "2026-07-01 08:15:30", "2026-07-31 17:45:15", null, null)]
        public async Task DisplayTaskTargetDates_ReadOnlyDatesDisplayLabelsUseConfiguredPrecision(string precision, string expectedBegin, string expectedEnd, string? hiddenBeginPart, string? hiddenEndPart)
        {
            DateTime targetBeginDate = new(2026, 7, 1, 8, 15, 30);
            DateTime targetEndDate = new(2026, 7, 31, 17, 45, 15);

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = precision
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, targetBeginDate)
                .Add(p => p.TargetEndDate, targetEndDate)
                .Add(p => p.CanEditTargetBeginDate, false)
                .Add(p => p.CanEditTargetEndDate, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input[type=date]"), Is.Empty);
                Assert.That(component.FindAll("input[type=time]"), Is.Empty);
                Assert.That(component.Markup, Does.Contain(expectedBegin));
                Assert.That(component.Markup, Does.Contain(expectedEnd));
                if (hiddenBeginPart != null)
                {
                    Assert.That(component.Markup, Does.Not.Contain(hiddenBeginPart));
                }
                if (hiddenEndPart != null)
                {
                    Assert.That(component.Markup, Does.Not.Contain(hiddenEndPart));
                }
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_FlowTimeObjectDropdownDisplaysNameWithoutRange()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowTimeObjects =
                [
                    new FlowTimeObject
                    {
                        Id = 301,
                        Name = "Business Hours",
                        StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                        EndTime = new DateTime(2026, 8, 1, 17, 0, 0),
                        ShowInRequestModule = true
                    }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            IRenderedComponent<Dropdown<FlowTimeObject>> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>();
            string displayText = dropdown.Instance.ElementToString(apiConn.FlowTimeObjects.Single());

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(FlowQueries.getFlowRequestTimeObjectCatalog));
                Assert.That(displayText, Is.EqualTo("Business Hours"));
                Assert.That(displayText, Does.Not.Contain("2026-08-01"));
                Assert.That(displayText, Does.Not.Contain("08:00"));
                Assert.That(displayText, Does.Not.Contain("17:00"));
            });
        }


    }
}
