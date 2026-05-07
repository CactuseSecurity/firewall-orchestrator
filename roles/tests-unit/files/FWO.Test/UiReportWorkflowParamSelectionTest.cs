using FWO.Basics;
using FWO.Data.Report;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Reporting;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiReportWorkflowParamSelectionTest
    {
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
                Assert.That(component.WorkflowFilter.TaskTypes.Count, Is.EqualTo(Enum.GetValues(typeof(WfTaskType)).Length - 1));
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
        public void GetLabelFilterSummary_HandlesEmptyExactAndModeFilters()
        {
            ReportWorkflowParamSelection component = CreateComponent();

            string emptySummary = (string)(InvokePrivateMethod(component, "GetLabelFilterSummary")
                ?? throw new InvalidOperationException("Expected empty label summary."));

            component.WorkflowFilter.LabelFilter = new()
            {
                Name = "policy_check",
                Mode = WorkflowLabelFilterMode.value,
                Value = "passed"
            };
            string valueSummary = (string)(InvokePrivateMethod(component, "GetLabelFilterSummary")
                ?? throw new InvalidOperationException("Expected value label summary."));

            component.WorkflowFilter.LabelFilter = new()
            {
                Name = "policy_check",
                Mode = WorkflowLabelFilterMode.not_existing
            };
            string modeSummary = (string)(InvokePrivateMethod(component, "GetLabelFilterSummary")
                ?? throw new InvalidOperationException("Expected mode label summary."));
            component.WorkflowFilter.LabelFilter = new()
            {
                Name = "policy_check",
                Mode = WorkflowLabelFilterMode.display_only
            };
            string displayOnlySummary = (string)(InvokePrivateMethod(component, "GetLabelFilterSummary")
                ?? throw new InvalidOperationException("Expected display only label summary."));

            Assert.Multiple(() =>
            {
                Assert.That(emptySummary, Is.EqualTo("-"));
                Assert.That(valueSummary, Is.EqualTo("policy_check: passed"));
                Assert.That(modeSummary, Is.EqualTo("policy_check: not existing"));
                Assert.That(displayOnlySummary, Is.EqualTo("policy_check: Display only"));
            });
        }

        [Test]
        public void OnParametersSet_AddsSelectedLabelNameToAvailableNames()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                LabelFilter = new()
                {
                    Name = "custom_policy_label",
                    Mode = WorkflowLabelFilterMode.existing
                }
            });

            InvokePrivateMethod(component, "OnParametersSet");

            List<string> availableLabelNames = GetPrivateMember<List<string>>(component, "availableLabelNames");

            Assert.That(availableLabelNames, Does.Contain("custom_policy_label"));
        }

        [Test]
        public async Task DeleteLabelFilterDialog_ResetsFilterAndClosesDialog()
        {
            ReportWorkflowParamSelection component = CreateComponent(new WorkflowFilter
            {
                LabelFilter = new()
                {
                    Name = "policy_check",
                    Mode = WorkflowLabelFilterMode.value,
                    Value = "passed"
                }
            });
            SetMember(component, "showLabelFilterDialog", true);

            Task task = (Task)(InvokePrivateMethod(component, "DeleteLabelFilterDialog")
                ?? throw new InvalidOperationException("Expected delete task."));
            await task;

            bool showLabelFilterDialog = GetPrivateMember<bool>(component, "showLabelFilterDialog");

            Assert.Multiple(() =>
            {
                Assert.That(component.WorkflowFilter.LabelFilter.Name, Is.EqualTo(string.Empty));
                Assert.That(component.WorkflowFilter.LabelFilter.Mode, Is.EqualTo(WorkflowLabelFilterMode.existing));
                Assert.That(component.WorkflowFilter.LabelFilter.Value, Is.EqualTo(string.Empty));
                Assert.That(showLabelFilterDialog, Is.False);
            });
        }

        [Test]
        public void BuildAvailableLabelNames_IncludesAdditionalInfoAndConditionalAutoPromoteLabels()
        {
            ConditionalAutoPromoteParams conditionalParams = new()
            {
                CheckResultLabel = "policy_check_result"
            };
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = JsonSerializer.Serialize(conditionalParams)
            };

            List<string> labelNames = (List<string>)(typeof(ReportWorkflowParamSelection)
                .GetMethod("BuildAvailableLabelNames", BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, [new List<WfStateAction> { action }])
                ?? throw new MissingMethodException(nameof(ReportWorkflowParamSelection), "BuildAvailableLabelNames"));

            Assert.Multiple(() =>
            {
                Assert.That(labelNames, Does.Contain(AdditionalInfoKeys.ReqOwner));
                Assert.That(labelNames, Does.Contain("policy_check_result"));
            });
        }
    }
}
