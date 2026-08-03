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
        public void BuildAvailableAddInfoNames_IncludesAdditionalInfoAndDeduplicatedConditionalAutoPromoteLabels()
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

            List<string> addInfoNames = (List<string>)(typeof(ReportWorkflowParamSelection)
                .GetMethod("BuildAvailableAddInfoNames", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, [new List<WfStateAction> { action, duplicateAction }])
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
        public async Task OnInitializedAsync_WhenInitialQueriesFail_UsesKnownLabelNamesOnly()
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
        public async Task ReportWorkflowParamSelection_LabelDropdown_ShowsAvailableValues()
        {
            await using BunitContext context = CreateContext(
                [
                    new WfStateAction
                    {
                        ActionType = StateActionTypes.AutoPromote.ToString(),
                        ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                        {
                            CheckResultLabel = "policy_check_result"
                        })
                    }
                ]);
            context.JSInterop.Mode = JSRuntimeMode.Loose;

            IRenderedComponent<ReportWorkflowParamSelection> cut = context.Render<ReportWorkflowParamSelection>(parameters => parameters
                .Add(p => p.WorkflowFilter, new WorkflowFilter())
                .Add(p => p.SelectedReportType, ReportType.TicketReport));

            cut.Find("#workflowLabel-editButton").Click();
            cut.Find("#dropdown-input-workflowLabel-nameDropdown").Focus();

            cut.WaitForAssertion(() =>
            {
                var dropdownItems = cut.FindAll("button.dropdown-item");
                string menuMarkup = string.Join(" ", dropdownItems.Select(item => item.TextContent));

                Assert.That(menuMarkup, Does.Contain(kExpectedWorkflowAddInfoNames[0]));
                Assert.That(menuMarkup, Does.Contain(kExpectedWorkflowAddInfoNames[1]));
            });
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
