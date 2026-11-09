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
using System.Reflection;
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
        public async Task DisplayTaskTargetDates_HidesConfiguredDateFields()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.ShowTargetBeginDate, false)
                .Add(p => p.ShowTargetEndDate, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("valid_from"));
                Assert.That(component.Markup, Does.Not.Contain("valid_to"));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_KeepsEndDateWhenBeginDateIsHidden()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.ShowTargetBeginDate, false)
                .Add(p => p.ShowTargetEndDate, true));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Not.Contain("valid_from"));
                Assert.That(component.Markup, Does.Contain("valid_to"));
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
                .Add(p => p.DefaultStartTimeInput, "")
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

        [TestCase(FlowIntegrationTimePrecisionOptions.Date, "01.07.2026", "31.07.2026", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "01.07.2026 8 h", "31.07.2026 17 h", "08:15", "17:45")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "01.07.2026 08:15", "31.07.2026 17:45", "08:15:30", "17:45:15")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "01.07.2026 08:15:30", "31.07.2026 17:45:15", null, null)]
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
        public async Task DisplayTaskTargetDates_DatePrecisionShowsInclusiveDateForExclusiveMidnightEnd()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Date
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetEndDate, new DateTime(2026, 9, 1, 0, 0, 0)));

            Assert.That(component.Markup, Does.Contain("31.08.2026"));
            Assert.That(component.Markup, Does.Not.Contain("01.09.2026"));
        }

        [Test]
        public async Task DisplayTaskTargetDates_DatePrecisionKeepsMidnightStartAndAdjustsExclusiveMidnightEnd()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Date
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, new DateTime(2026, 9, 1, 0, 0, 0))
                .Add(p => p.TargetEndDate, new DateTime(2026, 9, 1, 0, 0, 0)));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("01.09.2026"));
                Assert.That(component.Markup, Does.Contain("31.08.2026"));
            });
        }

        [TestCase(FlowIntegrationTimePrecisionOptions.Hours, "2026-09-01")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Minutes, "2026-09-01")]
        [TestCase(FlowIntegrationTimePrecisionOptions.Seconds, "2026-09-01")]
        public async Task DisplayTaskTargetDates_NonDatePrecisionKeepsMidnightEndDate(string precision, string expectedDate)
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = false,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectObjects = FlowIntegrationObjectSelectionOptions.Both,
                    SelectServices = FlowIntegrationObjectSelectionOptions.Both,
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                    TimeObjectPrecision = precision
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetEndDate, new DateTime(2026, 9, 1, 0, 0, 0))
                .Add(p => p.CanEditTargetEndDate, true));

            Assert.That(component.FindAll("input[type=date]")[0].GetAttribute("value"), Is.EqualTo(expectedDate));
        }

        [Test]
        public async Task TimeObjectSelector_FlowTimeObjectDropdownDisplaysNameWithoutRange()
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

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>();

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

        [Test]
        public async Task TimeObjectSelector_TicketTimeObjectsAreAvailableWithoutFlowDb()
        {
            FlowTimeObject ticketTimeObject = new()
            {
                Id = -17,
                Name = "Ticket maintenance",
                StartTime = new DateTime(2026, 8, 1, 8, 0, 0),
                EndTime = new DateTime(2026, 8, 1, 17, 0, 0)
            };

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = false
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject> { ticketTimeObject }));

            IRenderedComponent<Dropdown<FlowTimeObject>> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>();

            Assert.That(dropdown.Instance.ElementToString(ticketTimeObject), Is.EqualTo("Ticket maintenance"));
        }

        [Test]
        public async Task TimeObjectSelector_RestoresSelectedTicketTimeObjectById()
        {
            FlowTimeObject ticketTimeObject = new() { Id = -17, Name = "Ticket maintenance" };

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedId, -17L)
                .Add(p => p.SelectedName, ticketTimeObject.Name)
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject> { ticketTimeObject }));

            Dropdown<FlowTimeObject> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>().Instance;

            Assert.That(dropdown.SelectedElement?.Id, Is.EqualTo(-17));
        }

        [Test]
        public async Task TimeObjectSelector_PreservesStoredSuffix()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = false
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedId, -17L)
                .Add(p => p.SelectedName, "maintenance_user")
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject>
                {
                    new() { Id = -17, Name = "maintenance_user" }
                }));

            Dropdown<FlowTimeObject> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>().Instance;

            Assert.That(dropdown.ElementToString(dropdown.SelectedElement!), Is.EqualTo("maintenance_user"));
        }

        [Test]
        public async Task TimeObjectSelector_SelectsTimeObjectAndUpdatesBothDates()
        {
            DateTime? targetBeginDate = null;
            DateTime? targetEndDate = null;
            FlowTimeObject timeObject = new()
            {
                Id = 302,
                Name = "Nightly window",
                StartTime = new DateTime(2026, 8, 31, 22, 0, 0),
                EndTime = new DateTime(2026, 9, 1, 2, 0, 0),
                ShowInRequestModule = true
            };

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.TimeObjectSelected, EventCallback.Factory.Create<FlowTimeObject>(this, value =>
                {
                    targetBeginDate = value.StartTime;
                    targetEndDate = value.EndTime;
                })));

            await UiRequestWorkflowTest.InvokePrivateTask(component.Instance, "SelectTimeObject", timeObject);

            Assert.Multiple(() =>
            {
                Assert.That(targetBeginDate, Is.EqualTo(timeObject.StartTime));
                Assert.That(targetEndDate, Is.EqualTo(timeObject.EndTime));
            });
        }

        [Test]
        public async Task TimeObjectSelector_ClearingSelectionClearsIdAndName()
        {
            long? selectedId = 302;
            string? selectedName = "Nightly window";

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());
            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedIdChanged, EventCallback.Factory.Create<long?>(this, value => selectedId = value))
                .Add(p => p.SelectedNameChanged, EventCallback.Factory.Create<string?>(this, value => selectedName = value)));

            await UiRequestWorkflowTest.InvokePrivateTask(component.Instance, "ClearTimeObjectSelection");

            Assert.Multiple(() =>
            {
                Assert.That(selectedId, Is.Null);
                Assert.That(selectedName, Is.Null);
            });
        }

        [Test]
        public async Task TimeObjectSelector_HideSelectionDoesNotRenderOrQuery()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.HideSelection, true)
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject> { new() { Id = -1, Name = "ticket" } }));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindComponents<Dropdown<FlowTimeObject>>(), Is.Empty);
                Assert.That(apiConn.Queries, Does.Not.Contain(FlowQueries.getFlowRequestTimeObjectCatalog));
            });
        }

        [Test]
        public async Task TimeObjectSelector_DisablingSelectionDoesNotRender()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = true });

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.AllowSelection, false)
                .Add(p => p.SelectedId, 12L)
                .Add(p => p.SelectedName, "existing"));

            Assert.That(component.FindComponents<Dropdown<FlowTimeObject>>(), Is.Empty);
        }

        [Test]
        public async Task TimeObjectSelector_UsesFullWidthAndHidesLabel()
        {
            await using BunitContext context = new();
            UiRequestWorkflowTest.RequestWorkflowApiConn apiConn = new()
            {
                FlowTimeObjects = [new FlowTimeObject
                {
                    Id = 31,
                    Name = "Visible",
                    State = FlowState.Requested,
                    ShowInRequestModule = true
                }]
            };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqUseFlowDb = true,
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    SelectTimeObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb
                }.ToConfigValue()
            });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.FullWidthLayout, true)
                .Add(p => p.ShowLabel, false));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("col-12"));
                Assert.That(component.Markup, Does.Not.Contain("time_object"));
                Assert.That(component.FindComponent<Dropdown<FlowTimeObject>>().Instance.Elements, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task TimeObjectSelector_RestoresSelectedObjectByNameAndRaisesChangedCallbacks()
        {
            long? selectedId = null;
            string? selectedName = "ticket window";
            FlowTimeObject ticketTimeObject = new() { Id = -21, Name = "Ticket Window" };

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedName, selectedName)
                .Add(p => p.SelectedIdChanged, EventCallback.Factory.Create<long?>(this, value => selectedId = value))
                .Add(p => p.SelectedNameChanged, EventCallback.Factory.Create<string?>(this, value => selectedName = value))
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject> { ticketTimeObject }));

            Dropdown<FlowTimeObject> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>().Instance;

            Assert.Multiple(() =>
            {
                Assert.That(dropdown.SelectedElement?.Id, Is.EqualTo(-21));
                Assert.That(selectedId, Is.EqualTo(-21));
                Assert.That(selectedName, Is.EqualTo("Ticket Window"));
            });
        }

        [Test]
        public async Task TimeObjectSelector_AddsFallbackForUnknownPersistedSelection()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedId, -99L)
                .Add(p => p.SelectedName, "Legacy window"));

            Dropdown<FlowTimeObject> dropdown = component.FindComponent<Dropdown<FlowTimeObject>>().Instance;

            Assert.Multiple(() =>
            {
                Assert.That(dropdown.SelectedElement?.Id, Is.EqualTo(-99));
                Assert.That(dropdown.SelectedElement?.Name, Is.EqualTo("Legacy window"));
                Assert.That(dropdown.Elements, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task TimeObjectSelector_SelectTimeObjectRaisesAllSelectionCallbacks()
        {
            FlowTimeObject selectedObject = new() { Id = 44, Name = "Selected" };
            FlowTimeObject? callbackObject = null;
            long? selectedId = null;
            string? selectedName = null;

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig());

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.TimeObjectSelected, EventCallback.Factory.Create<FlowTimeObject>(this, value => callbackObject = value))
                .Add(p => p.SelectedIdChanged, EventCallback.Factory.Create<long?>(this, value => selectedId = value))
                .Add(p => p.SelectedNameChanged, EventCallback.Factory.Create<string?>(this, value => selectedName = value)));

            await UiRequestWorkflowTest.InvokePrivateTask(component.Instance, "SelectTimeObject", selectedObject);

            Assert.Multiple(() =>
            {
                Assert.That(callbackObject, Is.SameAs(selectedObject));
                Assert.That(selectedId, Is.EqualTo(44));
                Assert.That(selectedName, Is.EqualTo("Selected"));
            });
        }

        [Test]
        public async Task TimeObjectSelector_ParameterChangesRestoreAndClearSelection()
        {
            FlowTimeObject ticketTimeObject = new() { Id = -7, Name = "Ticket" };

            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.SelectedId, -7L)
                .Add(p => p.SelectedName, "Ticket")
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject> { ticketTimeObject }));

            Assert.That(component.FindComponent<Dropdown<FlowTimeObject>>().Instance.SelectedElement?.Id, Is.EqualTo(-7));

            UiRequestWorkflowTest.SetMember(component.Instance, nameof(TimeObjectSelector.SelectedId), (long?)null);
            UiRequestWorkflowTest.SetMember(component.Instance, nameof(TimeObjectSelector.SelectedName), (string?)null);
            UiRequestWorkflowTest.GetPrivateMethod(typeof(TimeObjectSelector), "OnParametersSet")
                .Invoke(component.Instance, []);

            Assert.That(UiRequestWorkflowTest.GetMember<FlowTimeObject?>(component.Instance, "selectedTimeObjectInternal"), Is.Null);

            UiRequestWorkflowTest.SetMember(component.Instance, nameof(TimeObjectSelector.SelectedId), -7L);
            UiRequestWorkflowTest.GetPrivateMethod(typeof(TimeObjectSelector), "OnParametersSet")
                .Invoke(component.Instance, []);

            Assert.That(UiRequestWorkflowTest.GetMember<FlowTimeObject?>(component.Instance, "selectedTimeObjectInternal")?.Id, Is.EqualTo(-7));
        }

        [Test]
        public async Task TimeObjectSelector_RefreshesTicketObjectsWhenParametersChange()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>(parameters => parameters
                .Add(p => p.TicketTimeObjects, new List<FlowTimeObject>
                {
                    new() { Id = -7, Name = "Old" }
                }));

            IReadOnlyList<FlowTimeObject> updatedObjects =
            [
                new FlowTimeObject { Id = -7, Name = "Old" },
                new FlowTimeObject { Id = -8, Name = "Created" }
            ];
            UiRequestWorkflowTest.SetMember(component.Instance, nameof(TimeObjectSelector.TicketTimeObjects), updatedObjects);
            UiRequestWorkflowTest.GetPrivateMethod(typeof(TimeObjectSelector), "OnParametersSet")
                .Invoke(component.Instance, []);

            List<FlowTimeObject> displayedObjects = UiRequestWorkflowTest.GetMember<List<FlowTimeObject>>(
                component.Instance, "flowTimeObjects");
            Assert.That(displayedObjects.Select(timeObject => timeObject.Id), Does.Contain(-8));
        }

        [Test]
        public async Task TimeObjectSelector_PrivateHelpersClassifySourcesAndDisplayIds()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig { ReqUseFlowDb = false });
            IRenderedComponent<TimeObjectSelector> component = context.Render<TimeObjectSelector>();

            MethodInfo displayMethod = typeof(TimeObjectSelector).GetMethod("DisplayTimeObject", BindingFlags.NonPublic | BindingFlags.Instance)!;
            MethodInfo sourceMethod = typeof(TimeObjectSelector).GetMethod("ShowsFlowDbSelection", BindingFlags.NonPublic | BindingFlags.Static)!;

            string display = (string)displayMethod.Invoke(component.Instance, [new FlowTimeObject { Id = 55 }])!;

            Assert.Multiple(() =>
            {
                Assert.That(display, Is.EqualTo("55"));
                Assert.That(sourceMethod.Invoke(null, [FlowIntegrationObjectSelectionOptions.FromFlowDb]), Is.EqualTo(true));
                Assert.That(sourceMethod.Invoke(null, [FlowIntegrationObjectSelectionOptions.Both]), Is.EqualTo(true));
                Assert.That(sourceMethod.Invoke(null, [FlowIntegrationObjectSelectionOptions.Manually]), Is.EqualTo(false));
                Assert.That(sourceMethod.Invoke(null, ["unknown"]), Is.EqualTo(false));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_DateAndTimeInputsCanWrap()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true));

            Assert.That(component.Markup, Does.Contain("d-flex flex-wrap gap-1"));
        }

        [Test]
        public async Task DisplayTaskTargetDates_FullWidthLayoutPlacesDatesInSeparateRows()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Seconds
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, new DateTime(2026, 11, 1, 8, 0, 0))
                .Add(p => p.TargetEndDate, new DateTime(2026, 11, 30, 18, 0, 0))
                .Add(p => p.FullWidthLayout, true));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("div.d-flex.flex-column.w-100 > div.w-100.flex-shrink-0"), Has.Count.EqualTo(2));
                Assert.That(component.FindAll("div.w-100.flex-shrink-0 > .form-group.row"), Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task DisplayTaskTargetDates_DatePrecisionDoesNotRenderTimeInputs()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<ApiConnection>(new UiRequestWorkflowTest.RequestWorkflowApiConn());
            context.Services.AddSingleton<UserConfig>(new UiRequestWorkflowTest.RequestWorkflowUserConfig
            {
                ReqFlowIntegration = new FlowIntegrationConfig
                {
                    TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Date
                }.ToConfigValue()
            });

            IRenderedComponent<DisplayTaskTargetDates> component = context.Render<DisplayTaskTargetDates>(parameters => parameters
                .Add(p => p.TargetBeginDate, new DateTime(2026, 11, 1, 8, 0, 0))
                .Add(p => p.TargetEndDate, new DateTime(2026, 11, 30, 18, 0, 0))
                .Add(p => p.CanEditTargetBeginDate, true)
                .Add(p => p.CanEditTargetEndDate, true)
                .Add(p => p.ForceManualTimeObjectSelection, true));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("input[type=date]"), Has.Count.EqualTo(2));
                Assert.That(component.FindAll("input[type=time], input[type=number]"), Is.Empty);
            });
        }

    }
}
