using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiStateMatrixConfigurationManagementTest
    {
        private static readonly string[] kExpectedVisibilityGroupMemberDns = ["CN=User,DC=example,DC=org"];

        [Test]
        public void RequestQueries_LoadsWorkflowConfigurationManagementOperations()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RequestQueries.getWorkflowVisibilityGroups, Does.Contain("query getWorkflowVisibilityGroups"));
                Assert.That(RequestQueries.getWorkflowVisibilityGroups, Does.Not.Contain("is_active"));
                Assert.That(RequestQueries.getStateMatrixTransitionGroups, Does.Contain("query getStateMatrixTransitionGroups"));
                Assert.That(RequestQueries.getStateMatrixTransitionGroups, Does.Contain("state_matrix_phase_transition_groups"));
                Assert.That(RequestQueries.getStateMatrixTransitionGroups, Does.Contain("exclusive"));
                Assert.That(RequestQueries.getStateMatrixTransitionGroups, Does.Not.Contain("workflow_visibility_group_members"));
                Assert.That(RequestQueries.createWorkflowConfiguration, Does.Contain("mutation createWorkflowConfiguration"));
                Assert.That(RequestQueries.deleteWorkflowVisibilityGroup, Does.Contain("mutation deleteWorkflowVisibilityGroup"));
                Assert.That(RequestQueries.createWorkflowVisibilityGroup, Does.Not.Contain("isActive"));
                Assert.That(RequestQueries.updateWorkflowVisibilityGroup, Does.Not.Contain("isActive"));
                Assert.That(RequestQueries.deleteStateMatrixTransitionGroup, Does.Contain("mutation deleteStateMatrixTransitionGroup"));
                Assert.That(RequestQueries.createStateMatrixTransitionGroup, Does.Contain("exclusive"));
                Assert.That(RequestQueries.updateStateMatrixTransitionGroup, Does.Contain("exclusive"));
                Assert.That(RequestQueries.linkStateMatrixTransitionGroup, Does.Contain("mutation linkStateMatrixTransitionGroup"));
                Assert.That(RequestQueries.unlinkStateMatrixTransitionGroup, Does.Contain("mutation unlinkStateMatrixTransitionGroup"));
                Assert.That(RequestQueries.deleteWorkflowConfiguration, Does.Contain("mutation deleteWorkflowConfiguration"));
            });
        }

        [Test]
        public void BuildClonedPhaseMapping_CopiesPhaseAndReusesTransitionGroup()
        {
            WorkflowConfigurationPhase mapping = new()
            {
                TaskType = "generic",
                Phase = "request",
                PhaseMatrix = new StateMatrixPhase
                {
                    Phase = "request",
                    Active = true,
                    LowestInputState = 1,
                    LowestStartState = 2,
                    LowestEndState = 3,
                    DerivedStates = [new() { FromStateId = 4, DerivedStateId = 5 }],
                    TransitionGroups = [new() { TransitionGroupId = 6, SortOrder = 7 }]
                }
            };
            MethodInfo method = typeof(AddWorkflowConfiguration).GetMethod("BuildClonedPhaseMapping", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(AddWorkflowConfiguration).FullName, "BuildClonedPhaseMapping");

            JObject result = JObject.FromObject(method.Invoke(null, [mapping, "Copy"])!);

            Assert.Multiple(() =>
            {
                Assert.That((string?)result["state_matrix_phase"]?["data"]?["name"], Is.EqualTo("Copy::generic::request"));
                Assert.That((int?)result["state_matrix_phase"]?["data"]?["state_matrix_derived_states"]?["data"]?[0]?["derived_state_id"], Is.EqualTo(5));
                Assert.That((int?)result["state_matrix_phase"]?["data"]?["state_matrix_phase_transition_groups"]?["data"]?[0]?["transition_group_id"], Is.EqualTo(6));
                Assert.That(result["phase_matrix_id"], Is.Null);
            });
        }

        [Test]
        public void AddMemberDn_NormalizesAndDeduplicatesDns()
        {
            EditWorkflowVisibilityGroups component = new();
            MethodInfo method = typeof(EditWorkflowVisibilityGroups).GetMethod("AddMemberDn", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(EditWorkflowVisibilityGroups).FullName, "AddMemberDn");
            FieldInfo field = typeof(EditWorkflowVisibilityGroups).GetField("editGroup", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(EditWorkflowVisibilityGroups).FullName, "editGroup");

            method.Invoke(component, [" CN=User,DC=example,DC=org "]);
            method.Invoke(component, ["cn=user,dc=example,dc=org"]);
            method.Invoke(component, [""]);

            WorkflowVisibilityGroup group = (WorkflowVisibilityGroup)field.GetValue(component)!;
            Assert.That(group.Members.Select(member => member.MemberDn), Is.EqualTo(kExpectedVisibilityGroupMemberDns));
        }

        [Test]
        public void TransitionGroup_NullCollectionsAreNormalized()
        {
            JObject json = JObject.Parse("""{"state_matrix_transitions":null,"state_matrix_phase_transition_groups":null}""");

            StateMatrixTransitionGroup group = json.ToObject<StateMatrixTransitionGroup>()
                ?? throw new InvalidOperationException("Transition group could not be deserialized.");

            Assert.Multiple(() =>
            {
                Assert.That(group.Transitions, Is.Empty);
                Assert.That(group.PhaseMatrixUsages, Is.Empty);
                Assert.That(group.TransitionCount, Is.Zero);
                Assert.That(group.PhaseMatrixUsageCount, Is.Zero);
            });
        }

        [Test]
        public void TransitionGroupEditor_DirectEditModeSuppressesOverview()
        {
            EditStateMatrixTransitionGroups component = new();
            PropertyInfo parameter = typeof(EditStateMatrixTransitionGroups).GetProperty(nameof(EditStateMatrixTransitionGroups.TransitionGroupIdToEdit))
                ?? throw new MissingMemberException(typeof(EditStateMatrixTransitionGroups).FullName, nameof(EditStateMatrixTransitionGroups.TransitionGroupIdToEdit));
            PropertyInfo property = typeof(EditStateMatrixTransitionGroups).GetProperty("DirectEditMode", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(typeof(EditStateMatrixTransitionGroups).FullName, "DirectEditMode");
            parameter.SetValue(component, 42);

            Assert.That(property.GetValue(component), Is.True);
        }

        [Test]
        public void StateMatrix_TransitionGroupDeleteRequiresSingleUsage()
        {
            MethodInfo method = typeof(SettingsStateMatrix).GetMethod("CanDeleteTransitionGroup", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsStateMatrix).FullName, "CanDeleteTransitionGroup");
            StateMatrixTransitionGroup singleUseGroup = new()
            {
                PhaseMatrixUsages = [new StateMatrixPhaseTransitionGroup()]
            };
            StateMatrixTransitionGroup sharedGroup = new()
            {
                PhaseMatrixUsages = [new StateMatrixPhaseTransitionGroup(), new StateMatrixPhaseTransitionGroup()]
            };

            Assert.Multiple(() =>
            {
                Assert.That(method.Invoke(null, [singleUseGroup]), Is.True);
                Assert.That(method.Invoke(null, [sharedGroup]), Is.False);
            });
        }

        [Test]
        public void TransitionGroupEditor_StateDisplayNameIncludesId()
        {
            MethodInfo method = typeof(EditStateMatrixTransitionGroups).GetMethod("StateDisplayName", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(EditStateMatrixTransitionGroups).FullName, "StateDisplayName");

            string? result = method.Invoke(null, [new WfState { Id = 17, Name = "Approved" }]) as string;

            Assert.That(result, Is.EqualTo("Approved (17)"));
        }

        [Test]
        public void StateMatrix_StateDisplayNameIncludesId()
        {
            SettingsStateMatrix component = new();
            FieldInfo field = typeof(SettingsStateMatrix).GetField("stateNames", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(typeof(SettingsStateMatrix).FullName, "stateNames");
            MethodInfo method = typeof(SettingsStateMatrix).GetMethod("StateDisplayName", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsStateMatrix).FullName, "StateDisplayName");
            field.SetValue(component, new Dictionary<int, string> { [23] = "Implemented" });

            string? result = method.Invoke(component, [23]) as string;

            Assert.That(result, Is.EqualTo("Implemented (23)"));
        }

        [Test]
        public async Task StateMatrixConfigurationRepository_Load_FillsMissingWorkflowPhasesWithPlaceholders()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getActiveStateMatrixConfiguration, new List<WorkflowConfiguration>
            {
                new()
                {
                    Id = 17,
                    Name = "Legacy",
                    Phases =
                    [
                        new()
                        {
                            TaskType = WfTaskType.master.ToString(),
                            Phase = WorkflowPhases.request.ToString(),
                            PhaseMatrix = new StateMatrixPhase
                            {
                                Id = 101,
                                Name = "Legacy_request",
                                Phase = WorkflowPhases.request.ToString(),
                                Active = true,
                                LowestInputState = 1,
                                LowestStartState = 2,
                                LowestEndState = 3
                            }
                        }
                    ]
                }
            });

            StateMatrixConfigurationSnapshot snapshot = await StateMatrixConfigurationRepository.Load(api, WfTaskType.master);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Matrices, Has.Count.EqualTo(Enum.GetValues<WorkflowPhases>().Length));
                Assert.That(snapshot.Matrices[WorkflowPhases.request].Active, Is.True);
                Assert.That(snapshot.Matrices[WorkflowPhases.approval].Active, Is.False);
                Assert.That(snapshot.PhaseBindings[WorkflowPhases.approval].PhaseMatrixId, Is.Zero);
            });
        }

        [Test]
        public void StateMatrixConfigurationRepository_Update_RejectsEditingPlaceholderPhases()
        {
            GlobalStateMatrix stateMatrix = GlobalStateMatrix.Create();
            stateMatrix.GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
            {
                [WorkflowPhases.approval] = new StateMatrix
                {
                    Active = true
                }
            };
            SetPrivateProperty(stateMatrix, "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.approval] = new StateMatrixPhaseBinding(0, "Legacy_approval_missing", [], [])
            });
            SetPrivateProperty(stateMatrix, "OriginalGlobalMatrix", new Dictionary<WorkflowPhases, StateMatrix>
            {
                [WorkflowPhases.approval] = new StateMatrix
                {
                    Active = false
                }
            });

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await StateMatrixConfigurationRepository.Update(new RecordingWorkflowApiConnection(), stateMatrix))!;

            Assert.That(exception.Message, Does.Contain("missing a persistence binding"));
        }

        [Test]
        public void StateMatrix_DerivedStateEditorAddsAndRemovesSparseMapping()
        {
            SettingsStateMatrix component = new();
            StateMatrix requestMatrix = new();
            SetField(component, "actStateMatrix", new GlobalStateMatrix
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix> { [WorkflowPhases.request] = requestMatrix }
            });
            SetField(component, "stateIds", new List<int> { 1, 2, 3 });

            Invoke(component, "AddDerivedState", WorkflowPhases.request);
            SetField(component, "selectedDerivedStateId", 1);
            Assert.That(GetProperty<bool>(component, "CanSaveDerivedState"), Is.False);

            SetField(component, "selectedDerivedStateId", 3);
            Invoke(component, "SaveDerivedState");

            Assert.That(requestMatrix.DerivedStates, Is.EqualTo(new Dictionary<int, int> { [1] = 3 }));
            Assert.That(Invoke(component, "HasDerivedStates", WorkflowPhases.request), Is.True);

            Invoke(component, "RemoveDerivedState", WorkflowPhases.request, 1);

            Assert.That(requestMatrix.DerivedStates, Is.Empty);
            Assert.That(Invoke(component, "HasDerivedStates", WorkflowPhases.request), Is.False);
        }

        /// <summary>
        /// Sets a private component field for focused UI state tests.
        /// </summary>
        private static void SetField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
            field.SetValue(instance, value);
        }

        /// <summary>
        /// Gets a private component property for focused UI state tests.
        /// </summary>
        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)(property.GetValue(instance) ?? throw new InvalidOperationException($"Property {propertyName} is null."));
        }

        /// <summary>
        /// Sets a private component property for focused workflow state tests.
        /// </summary>
        private static void SetPrivateProperty(object instance, string propertyName, object value)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            MethodInfo? setter = property.GetSetMethod(true);
            if (setter == null)
            {
                throw new MissingMethodException(instance.GetType().FullName, $"set_{propertyName}");
            }

            setter.Invoke(instance, [value]);
        }

        /// <summary>
        /// Invokes a private component method for focused UI state tests.
        /// </summary>
        private static object? Invoke(object instance, string methodName, params object[] parameters)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            return method.Invoke(instance, parameters);
        }
    }
}
