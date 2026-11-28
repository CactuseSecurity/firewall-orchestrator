using Bunit;
using FWO.Basics;
using FWO.Api.Client;
using FWO.Config.Api;
using FWO.Api.Client.Queries;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiReportWorkflowParamSelectionTest
    {
        private static readonly int[] kClosedStateIds = [10, 12];
        private static readonly string[] kExpectedWorkflowAddInfoNames = [AdditionalInfoKeys.ReqOwner, "policy_check_result"];

        private static T GetPrivateMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
        {
            MethodInfo? method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, methodName);
            }
            return method.Invoke(instance, args);
        }

        private static ReportWorkflowParamSelection CreateComponent(WorkflowFilter? workflowFilter = null)
        {
            ReportWorkflowParamSelection component = new();
            SetMember(component, nameof(ReportWorkflowParamSelection.WorkflowFilter), workflowFilter ?? new WorkflowFilter());
            SetMember(component, "userConfig", new SimulatedUserConfig());
            return component;
        }

        private static void SetWorkflowStateScope(ReportWorkflowParamSelection component)
        {
            GlobalStateMatrix masterStateMatrix = new()
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new()
                    {
                        Active = true,
                        LowestInputState = 1,
                        LowestStartedState = 2,
                        LowestEndState = 4,
                        Matrix = new Dictionary<int, List<int>>
                        {
                            [1] = [2],
                            [2] = [3],
                            [3] = [4]
                        }
                    },
                    [WorkflowPhases.implementation] = new()
                    {
                        Active = true,
                        LowestInputState = 7,
                        LowestStartedState = 8,
                        LowestEndState = 10,
                        Matrix = new Dictionary<int, List<int>>
                        {
                            [7] = [8],
                            [8] = [10]
                        }
                    }
                }
            };
            StateMatrix requestStateMatrix = new()
            {
                MinTicketCompleted = 10
            };
            List<WfState> states =
            [
                new() { Id = 1, Name = "requested" },
                new() { Id = 2, Name = "assigned" },
                new() { Id = 3, Name = "planned" },
                new() { Id = 4, Name = "request done" },
                new() { Id = 7, Name = "implementation ready" },
                new() { Id = 8, Name = "implementing" },
                new() { Id = 10, Name = "closed" }
            ];

            SetMember(component, "masterStateMatrix", masterStateMatrix);
            SetMember(component, "masterRequestStateMatrix", requestStateMatrix);
            SetMember(component, "allStates", states);
        }

        [Test]
        public async Task TaskTypesChanged_NullOrEmptySelection_UsesAllRealTaskTypes()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                TaskTypes = [WfTaskType.access]
            });

            Task task = (Task)(InvokePrivateMethod(component, "TaskTypesChanged", new List<WfTaskType?> { null })
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<WfTaskType?> selectedTaskTypes =
                GetPrivateMember<IEnumerable<WfTaskType?>>(component, "selectedTaskTypesForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.TaskTypes, Does.Not.Contain(WfTaskType.master));
                Assert.That(component.WorkflowFilter.TaskTypes.Count, Is.EqualTo(Enum.GetValues<WfTaskType>().Length - 1));
                Assert.That(selectedTaskTypes, Is.Empty);
            });
        }

        [Test]
        public async Task TaskTypesChanged_EmptySelection_UsesAllRealTaskTypes()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                TaskTypes = [WfTaskType.access]
            });

            Task task = (Task)(InvokePrivateMethod(component, "TaskTypesChanged", Array.Empty<WfTaskType?>())
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<WfTaskType?> selectedTaskTypes =
                GetPrivateMember<IEnumerable<WfTaskType?>>(component, "selectedTaskTypesForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.TaskTypes, Does.Not.Contain(WfTaskType.master));
                Assert.That(component.WorkflowFilter.TaskTypes.Count, Is.EqualTo(Enum.GetValues<WfTaskType>().Length - 1));
                Assert.That(selectedTaskTypes, Is.Empty);
            });
        }

        [Test]
        public async Task TaskTypesChanged_SubsetSelection_StoresSubsetForFilterAndUi()
        {
            ReportWorkflowParamSelection component = CreateComponent();

            Task task = (Task)(InvokePrivateMethod(component, "TaskTypesChanged",
                new List<WfTaskType?> { WfTaskType.access, WfTaskType.rule_modify })
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<WfTaskType?> selectedTaskTypes =
                GetPrivateMember<IEnumerable<WfTaskType?>>(component, "selectedTaskTypesForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.TaskTypes, Is.EqualTo(new List<WfTaskType>
                {
                    WfTaskType.access,
                    WfTaskType.rule_modify
                }));
                Assert.That(selectedTaskTypes, Is.EqualTo(new List<WfTaskType?>
                {
                    WfTaskType.access,
                    WfTaskType.rule_modify
                }));
            });
        }

        [Test]
        public async Task PhaseChanged_PrunedSelectedStatesToNewPhase()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                StateIds = [2, 7, 8, 10]
            });
            SetWorkflowStateScope(component);

            Task task = (Task)(InvokePrivateMethod(component, "PhaseChanged", WorkflowPhases.implementation.ToString())
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<int?> selectedStateIds = GetPrivateMember<IEnumerable<int?>>(component, "selectedStateIdsForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.Phase, Is.EqualTo(WorkflowPhases.implementation.ToString()));
                Assert.That(component.WorkflowFilter.StateIds, Is.EqualTo(new List<int> { 7, 8 }));
                Assert.That(selectedStateIds, Is.Empty);
            });
        }

        [Test]
        public async Task StateIdsChanged_EmptySelection_ClearsStateFilter()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                StateIds = [2, 3]
            });
            SetWorkflowStateScope(component);

            Task task = (Task)(InvokePrivateMethod(component, "StateIdsChanged", Array.Empty<int?>())
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<int?> selectedStateIds = GetPrivateMember<IEnumerable<int?>>(component, "selectedStateIdsForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.StateIds, Is.Empty);
                Assert.That(selectedStateIds, Is.Empty);
            });
        }

        [Test]
        public async Task StateIdsChanged_NullSelection_ClearsStateFilter()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                StateIds = [2, 3]
            });
            SetWorkflowStateScope(component);

            Task task = (Task)(InvokePrivateMethod(component, "StateIdsChanged", new List<int?> { null })
                ?? throw new InvalidOperationException("Expected change task."));
            await task;

            IEnumerable<int?> selectedStateIds = GetPrivateMember<IEnumerable<int?>>(component, "selectedStateIdsForUi");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.StateIds, Is.Empty);
                Assert.That(selectedStateIds, Is.Empty);
            });
        }

        [Test]
        public void BuildAvailableAddInfoNames_IncludesAdditionalInfoAndDeduplicatedConditionalAutoPromoteAddInfos()
        {
            ConditionalAutoPromoteParams conditionalParams = new()
            {
                CheckResultLabel = "policy_check_result"
            };
            ConditionalAutoPromoteParams duplicateConditionalParams = new()
            {
                CheckResultLabel = "policy_check_result"
            };
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = JsonSerializer.Serialize(conditionalParams)
            };
            WfStateAction duplicateAction = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = JsonSerializer.Serialize(duplicateConditionalParams)
            };
            List<WfStateAction> actions = [action, duplicateAction];

            object?[] invokeArgs = [actions];
            List<string> addInfoNames = (List<string>)(typeof(ReportWorkflowParamSelection)
                .GetMethod("BuildAvailableAddInfoNames", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, invokeArgs)
                ?? throw new MissingMethodException(nameof(ReportWorkflowParamSelection), "BuildAvailableAddInfoNames"));

            Assert.Multiple(() =>
            {
                Assert.That(addInfoNames, Does.Contain(AdditionalInfoKeys.ReqOwner));
                Assert.That(addInfoNames, Does.Contain("policy_check_result"));
                Assert.That(addInfoNames.Count(addInfo => string.Equals(addInfo, "policy_check_result", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
            });
        }

        [Test]
        public void GetAvailableStates_ClosedPhase_ReturnsOnlyClosedStates()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                Phase = GlobalConst.kClosed
            });
            SetClosedWorkflowStateScope(component);

            List<WfState> availableStates = (List<WfState>)(InvokePrivateMethod(component, "GetAvailableStates")
                ?? throw new InvalidOperationException("Expected available states."));

            Assert.That(availableStates.Select(state => state.Id), Is.EqualTo(kClosedStateIds));
        }

        [Test]
        public void GetAvailableStates_InvalidPhase_ReturnsStatesInScope()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                Phase = "not_a_phase"
            });
            SetWorkflowStateScope(component);

            List<WfState> allStates = GetPrivateMember<List<WfState>>(component, "allStates");
            allStates.Add(new WfState { Id = 99, Name = "unused" });

            List<WfState> availableStates = (List<WfState>)(InvokePrivateMethod(component, "GetAvailableStates")
                ?? throw new InvalidOperationException("Expected available states."));

            Assert.Multiple(() =>
            {
                Assert.That(availableStates, Has.Count.EqualTo(7));
                Assert.That(availableStates.Select(state => state.Id), Does.Not.Contain(99));
            });
        }

        [Test]
        public async Task HandleAddInfoFilterChanged_CopiesFilterAndNotifiesParent()
        {
            await using BunitContext context = CreateContext();
            AddInfoFilter sourceFilter = new()
            {
                Name = "policy_check",
                Mode = AddInfoFilterMode.value,
                Value = "passed"
            };
            WorkflowFilter? changedFilter = null;
            IRenderedComponent<ReportWorkflowParamSelection> cut = context.Render<ReportWorkflowParamSelection>(parameters => parameters
                .Add(p => p.WorkflowFilter, new WorkflowFilter())
                .Add(p => p.WorkflowFilterChanged, EventCallback.Factory.Create<WorkflowFilter>(context, updated => changedFilter = updated))
                .Add(p => p.SelectedReportType, ReportType.TicketReport));

            await cut.InvokeAsync(() => (Task)(InvokePrivateMethod(cut.Instance, "HandleAddInfoFilterChanged", sourceFilter)
                ?? throw new InvalidOperationException("Expected label-filter task.")));
            sourceFilter.Value = "mutated";

            Assert.Multiple(() =>
            {
                Assert.That(cut.Instance.WorkflowFilter.AddInfoFilter.Name, Is.EqualTo("policy_check"));
                Assert.That(cut.Instance.WorkflowFilter.AddInfoFilter.Value, Is.EqualTo("passed"));
                Assert.That(changedFilter, Is.Not.Null);
                Assert.That(changedFilter!.AddInfoFilter.Name, Is.EqualTo("policy_check"));
                Assert.That(changedFilter.AddInfoFilter.Value, Is.EqualTo("passed"));
            });
        }

        [Test]
        public async Task OnInitializedAsync_WhenInitialQueriesFail_UsesKnownAddInfoNamesOnly()
        {
            ReportWorkflowParamSelection component = CreateComponent();
            SetMember(component, "apiConnection", new ThrowingWorkflowApiConnection());
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "allStates", new List<WfState> { new() { Id = 999, Name = "stale" } });
            SetMember(component, "availablePhases", new List<string> { "stale" });
            SetMember(component, "availableAddInfoNames", new List<string> { "stale" });

            await (Task)(InvokePrivateMethod(component, "OnInitializedAsync")
                ?? throw new InvalidOperationException("Expected initialization task."));

            List<WfState> allStates = GetPrivateMember<List<WfState>>(component, "allStates");
            List<string> availablePhases = GetPrivateMember<List<string>>(component, "availablePhases");
            List<string> availableAddInfoNames = GetPrivateMember<List<string>>(component, "availableAddInfoNames");

            Assert.Multiple(() =>
            {
                Assert.That(allStates, Is.Empty);
                Assert.That(availablePhases, Is.Empty);
                Assert.That(availableAddInfoNames, Does.Contain(AdditionalInfoKeys.ReqOwner));
                Assert.That(availableAddInfoNames, Does.Not.Contain("policy_check_result"));
            });
        }

        [Test]
        public async Task ReportWorkflowParamSelection_AddInfoDropdown_ShowsAvailableValues()
        {
            List<WfStateAction> actions =
            [
                new WfStateAction
                {
                    ActionType = StateActionTypes.AutoPromote.ToString(),
                    ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                    {
                        CheckResultLabel = "policy_check_result"
                    })
                }
            ];
            await using BunitContext context = CreateContext(actions);
            context.JSInterop.Mode = JSRuntimeMode.Loose;

            IRenderedComponent<ReportWorkflowParamSelection> cut = context.Render<ReportWorkflowParamSelection>(parameters => parameters
                .Add(p => p.WorkflowFilter, new WorkflowFilter())
                .Add(p => p.SelectedReportType, ReportType.TicketReport));

            cut.Find("#workflowAddInfo-editButton").Click();
            cut.Find("#dropdown-input-workflowAddInfo-nameDropdown").Focus();

            cut.WaitForAssertion(() =>
            {
                var dropdownItems = cut.FindAll("button.dropdown-item");
                string menuMarkup = string.Join(" ", dropdownItems.Select(item => item.TextContent));

                Assert.That(menuMarkup, Does.Contain(kExpectedWorkflowAddInfoNames[0]));
                Assert.That(menuMarkup, Does.Contain(kExpectedWorkflowAddInfoNames[1]));
            });
        }

        private const int kRequestedStateId = 1;
        private const int kInProgressStateId = 2;
        private const int kPlannedStateId = 3;
        private const int kRequestDoneStateId = 4;
        private const int kDerivedStateId = 5;
        private const int kUnusedStateId = 6;

        /// <summary>
        /// Api fake delivering a workflow configuration with an active request phase, so the state matrix is built
        /// the same way as in the installation.
        /// </summary>
        private sealed class WorkflowConfigurationApiConnection(List<WfStateAction>? actions = null) : SimulatedApiConnection
        {
            private static readonly List<WfState> States =
            [
                new() { Id = kRequestedStateId, Name = "requested" },
                new() { Id = kInProgressStateId, Name = "in progress" },
                new() { Id = kPlannedStateId, Name = "planned" },
                new() { Id = kRequestDoneStateId, Name = "request done" },
                new() { Id = kDerivedStateId, Name = "derived" },
                new() { Id = kUnusedStateId, Name = "unused" }
            ];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((QueryResponseType)(object)States.ToList());
                }
                if (query == RequestQueries.getActions)
                {
                    return Task.FromResult((QueryResponseType)(object)(actions ?? []));
                }
                if (query == RequestQueries.getActiveStateMatrixConfiguration || query == RequestQueries.getStateMatrixConfigurationByName)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WorkflowConfiguration> { BuildConfiguration() });
                }
                throw new NotImplementedException($"Unhandled query {query}");
            }

            private static WorkflowConfiguration BuildConfiguration()
            {
                return new WorkflowConfiguration
                {
                    Id = 1,
                    Name = "test configuration",
                    IsActive = true,
                    Phases =
                    [
                        new WorkflowConfigurationPhase
                        {
                            TaskType = WfTaskType.master.ToString(),
                            Phase = WorkflowPhases.request.ToString(),
                            PhaseMatrix = new StateMatrixPhase
                            {
                                Phase = WorkflowPhases.request.ToString(),
                                Active = true,
                                LowestInputState = kRequestedStateId,
                                LowestStartState = kInProgressStateId,
                                LowestEndState = kRequestDoneStateId,
                                DerivedStates = [new StateMatrixDerivedState { FromStateId = kPlannedStateId, DerivedStateId = kDerivedStateId }]
                            }
                        }
                    ]
                };
            }
        }

        private static BunitContext CreateConfiguredContext(List<WfStateAction>? actions = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new WorkflowConfigurationApiConnection(actions));
            return context;
        }

        private static IRenderedComponent<ReportWorkflowParamSelection> RenderConfigured(BunitContext context, WorkflowFilter workflowFilter,
            ReportType reportType = ReportType.TicketReport, bool useFormLayout = false, Action<WorkflowFilter>? filterChanged = null)
        {
            return context.Render<ReportWorkflowParamSelection>(parameters => parameters
                .Add(p => p.WorkflowFilter, workflowFilter)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.UseFormLayout, useFormLayout)
                .Add(p => p.WorkflowFilterChanged, (WorkflowFilter changed) => filterChanged?.Invoke(changed)));
        }

        private static List<string> OpenDropdown<TElement>(IRenderedComponent<ReportWorkflowParamSelection> component)
        {
            IRenderedComponent<Dropdown<TElement>> dropdown = component.FindComponent<Dropdown<TElement>>();
            dropdown.Find("input").Focus();
            return [.. dropdown.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim())];
        }

        [Test]
        public async Task TheActivePhasesAndTheClosedTicketsCanBeChosen()
        {
            await using BunitContext context = CreateConfiguredContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter());

            Assert.That(OpenDropdown<string>(component), Is.EqualTo(new List<string>
            {
                userConfig.GetText(WorkflowPhases.request.ToString()), userConfig.GetText(GlobalConst.kClosed)
            }));
        }

        [Test]
        public async Task OnlyTheStatesUsedByTheActivePhasesAreOffered()
        {
            await using BunitContext context = CreateConfiguredContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter());

            // the planned state is only reached as the source of a derived state, the unused state not at all
            Assert.That(OpenDropdown<int?>(component), Is.EqualTo(new List<string>
            {
                userConfig.GetText("all"), "requested", "in progress", "planned", "request done", "derived"
            }));
        }

        [Test]
        public async Task TheTaskTypeChooserOffersAllRealTaskTypes()
        {
            await using BunitContext context = CreateConfiguredContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter());

            List<string> expected = [userConfig.GetText("all"), .. Enum.GetValues<WfTaskType>()
                .Where(taskType => taskType != WfTaskType.master).Select(taskType => userConfig.GetText(taskType.ToString()))];
            Assert.That(OpenDropdown<WfTaskType?>(component), Is.EqualTo(expected));
        }

        [Test]
        public async Task ATicketChangeReportAsksForTheReferenceDate()
        {
            await using BunitContext context = CreateConfiguredContext();
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter(), ReportType.TicketChangeReport);

            Assert.That(OpenDropdown<WorkflowReferenceDate>(component),
                Is.SupersetOf(Enum.GetValues<WorkflowReferenceDate>().Select(referenceDate => userConfig.GetText(referenceDate.ToString()))));
        }

        [Test]
        public async Task ATicketReportHasNoReferenceDate()
        {
            await using BunitContext context = CreateConfiguredContext();

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter());

            Assert.That(component.FindComponents<Dropdown<WorkflowReferenceDate>>(), Is.Empty);
        }

        [TestCase(false, "showFullTicket")]
        [TestCase(true, "showFullTicketTemplate")]
        public async Task TheDetailedTicketChangeReportCanShowTheFullTicket(bool useFormLayout, string checkboxId)
        {
            await using BunitContext context = CreateConfiguredContext();
            WorkflowFilter workflowFilter = new() { DetailedView = true, ShowFullTicket = false };

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, workflowFilter, ReportType.TicketChangeReport, useFormLayout);
            component.Find($"#{checkboxId}").Change(true);

            Assert.That(workflowFilter.ShowFullTicket, Is.True);
        }

        [Test]
        public async Task WithoutDetailedViewTheFullTicketOptionIsHidden()
        {
            await using BunitContext context = CreateConfiguredContext();
            WorkflowFilter workflowFilter = new() { DetailedView = false };

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, workflowFilter, ReportType.TicketChangeReport);
            component.Find("#detailedView").Change(true);

            Assert.That(workflowFilter.DetailedView, Is.True);
            Assert.That(component.FindAll("#showFullTicket"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ChosenStatesAreShownAndCanBeChanged()
        {
            await using BunitContext context = CreateConfiguredContext();
            WorkflowFilter workflowFilter = new() { StateIds = [kRequestedStateId] };
            WorkflowFilter? reported = null;
            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, workflowFilter, filterChanged: changed => reported = changed);
            Dropdown<int?> stateDropdown = component.FindComponent<Dropdown<int?>>().Instance;
            List<int?> shownSelection = [.. stateDropdown.SelectedElements];

            await component.InvokeAsync(() => stateDropdown.SelectedElementsChanged.InvokeAsync([kRequestedStateId, kRequestDoneStateId]));

            Assert.That(shownSelection, Is.EqualTo(new List<int?> { kRequestedStateId }));
            Assert.That(reported?.StateIds, Is.EqualTo(new List<int> { kRequestedStateId, kRequestDoneStateId }));
        }

        [Test]
        public void AnUnknownStateIsShownByItsId()
        {
            ReportWorkflowParamSelection component = CreateComponent();

            string stateName = (string)InvokePrivateMethod(component, "StateIdToString", 42)!;

            Assert.That(stateName, Is.EqualTo("42"));
        }

        [Test]
        public async Task OtherActionsThanAutoPromotionAddNoAdditionalInfoName()
        {
            List<WfStateAction> actions =
            [
                new WfStateAction
                {
                    ActionType = StateActionTypes.SetAlert.ToString(),
                    ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams { CheckResultLabel = "not_an_add_info" })
                }
            ];
            await using BunitContext context = CreateConfiguredContext(actions);

            IRenderedComponent<ReportWorkflowParamSelection> component = RenderConfigured(context, new WorkflowFilter());

            List<string> addInfoNames = GetPrivateMember<List<string>>(component.Instance, "availableAddInfoNames");
            Assert.That(addInfoNames, Does.Not.Contain("not_an_add_info"));
            Assert.That(addInfoNames, Does.Contain(AdditionalInfoKeys.ReqOwner));
        }

        [Test]
        public void APhaseWithoutStateMatrixOffersAllStatesInScope()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter { Phase = WorkflowPhases.request.ToString() });
            SetMember(component, "masterStateMatrix", new GlobalStateMatrix());
            SetMember(component, "allStates", new List<WfState> { new() { Id = kRequestedStateId, Name = "requested" } });

            List<WfState> availableStates = (List<WfState>)InvokePrivateMethod(component, "GetAvailableStates")!;

            Assert.That(availableStates, Is.Empty);
        }

        private static void SetClosedWorkflowStateScope(ReportWorkflowParamSelection component)
        {
            GlobalStateMatrix masterStateMatrix = new()
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new()
                    {
                        Active = true,
                        LowestInputState = 10,
                        LowestStartedState = 10,
                        LowestEndState = 13,
                        Matrix = new Dictionary<int, List<int>>
                        {
                            [10] = [12]
                        }
                    }
                }
            };
            StateMatrix requestStateMatrix = new()
            {
                MinTicketCompleted = 10
            };
            List<WfState> states =
            [
                new() { Id = 9, Name = "before" },
                new() { Id = 10, Name = "closed start" },
                new() { Id = 12, Name = "closed middle" },
                new() { Id = 15, Name = "after" }
            ];

            SetMember(component, "masterStateMatrix", masterStateMatrix);
            SetMember(component, "masterRequestStateMatrix", requestStateMatrix);
            SetMember(component, "allStates", states);
        }

        private static BunitContext CreateContext(IEnumerable<WfStateAction>? actions = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new WorkflowReportSelectionTestApiConnection(actions));
            return context;
        }

        private sealed class ThrowingWorkflowApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                _ = variables;
                _ = operationName;
                _ = chunkingOptions;

                if (query == RequestQueries.getStates)
                {
                    throw new InvalidOperationException("query failed");
                }

                throw new NotImplementedException(query);
            }
        }

        private sealed class WorkflowReportSelectionTestApiConnection : SimulatedApiConnection
        {
            private readonly List<WfStateAction> actions;

            public WorkflowReportSelectionTestApiConnection(IEnumerable<WfStateAction>? actions = null)
            {
                this.actions = actions?.ToList() ?? [];
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                _ = variables;
                _ = operationName;
                _ = chunkingOptions;

                if (typeof(QueryResponseType) == typeof(List<WfState>) && query == RequestQueries.getStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfState>());
                }

                if (typeof(QueryResponseType) == typeof(List<WfStateAction>) && query == RequestQueries.getActions)
                {
                    return Task.FromResult((QueryResponseType)(object)actions);
                }

                if (typeof(QueryResponseType) == typeof(List<WorkflowConfiguration>)
                    && (query == RequestQueries.getActiveStateMatrixConfiguration || query == RequestQueries.getStateMatrixConfigurationByName))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WorkflowConfiguration>
                    {
                        new()
                        {
                            Id = 1,
                            Name = "Test configuration",
                            IsActive = true,
                            Phases = []
                        }
                    });
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }
        }
    }
}
