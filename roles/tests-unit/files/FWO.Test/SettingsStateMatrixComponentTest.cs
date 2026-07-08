using FWO.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class SettingsStateMatrixComponentTest
    {
        private static readonly int[] kStateIds1122 = [11, 22];
        private static readonly int[] kConfigurationIds12 = [1, 2];
        private static readonly WfTaskType[] kAvailableTaskTypes = [WfTaskType.master, WfTaskType.access];
        private static readonly int[] kConfigurationIds21 = [2, 1];
        private static readonly int[] kPhaseMatrixIds57 = [5, 7];
        private static readonly int[] kConfigurationIds123 = [1, 2, 3];
        private static readonly int[] kGroupIds21 = [2, 1];
        private static readonly int[] kLinkableTransitionGroupIds23 = [2, 3];
        private static readonly int[] kDerivedStateKeys23 = [2, 3];
        private static readonly int[] kTransitionGroupIds21 = [2, 1];

        [Test]
        public void HasCompleteMatrix_RequiresEveryWorkflowPhase()
        {
            SettingsStateMatrix component = new();
            GlobalStateMatrix matrix = new()
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new()
                }
            };
            SetField(component, "actStateMatrix", matrix);
            Assert.That(GetProperty<bool>(component, "HasCompleteMatrix"), Is.False);

            matrix.GlobalMatrix = Enum.GetValues<WorkflowPhases>().ToDictionary(phase => phase, _ => new StateMatrix());
            Assert.That(GetProperty<bool>(component, "HasCompleteMatrix"), Is.True);
        }

        [Test]
        public async Task OnInitializedAsync_LoadsInitialDataAndTaskTypes()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            RecordingGlobalStateMatrix matrix = new();
            SimulatedUserConfig userConfig = new()
            {
                ReqAvailableTaskTypes = "[0,2]"
            };
            apiConnection.Respond(RequestQueries.getStates, new List<WfState>
            {
                new() { Id = 11, Name = "Open" },
                new() { Id = 22, Name = "Done", AutomaticOnly = true }
            });
            apiConnection.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 2, Name = "Beta" },
                new() { Id = 1, Name = "Alpha", IsActive = true }
            });
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            SetProperty(component, "apiConnection", apiConnection);
            SetProperty(component, "userConfig", userConfig);
            SetField(component, "actStateMatrix", matrix);

            await InvokeAsync(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "InitComplete"), Is.True);
                Assert.That(GetField<List<int>>(component, "stateIds"), Is.EqualTo(kStateIds1122));
                Assert.That(GetField<List<int>>(component, "configurationIds"), Is.EqualTo(kConfigurationIds12));
                Assert.That(GetField<int>(component, "selectedConfigurationId"), Is.EqualTo(1));
                Assert.That(GetProperty<List<WfTaskType>>(component, "availableTaskTypes"), Is.EqualTo(kAvailableTaskTypes));
                Assert.That(matrix.InitCalls.Single(), Is.EqualTo((WfTaskType.master, "Alpha")));
            });
        }

        [Test]
        public async Task ChangeConfiguration_ReloadsSelectedConfigurationWhenClean()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            RecordingGlobalStateMatrix matrix = new();
            SetField(component, "actStateMatrix", matrix);
            SetProperty(component, "apiConnection", apiConnection);
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true },
                new() { Id = 2, Name = "Beta" }
            });
            SetField(component, "selectedConfigurationId", 1);
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());

            await InvokeAsync(component, "ChangeConfiguration", 2);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<int>(component, "selectedConfigurationId"), Is.EqualTo(2));
                Assert.That(matrix.InitCalls.Single(), Is.EqualTo((WfTaskType.master, "Beta")));
            });
        }

        [Test]
        public async Task ChangeTaskType_ReloadsMatrixWhenClean()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            RecordingGlobalStateMatrix matrix = new();
            SetField(component, "actStateMatrix", matrix);
            SetProperty(component, "apiConnection", apiConnection);
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true }
            });
            SetField(component, "selectedConfigurationId", 1);
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());

            await InvokeAsync(component, "ChangeTaskType", WfTaskType.access);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<WfTaskType>(component, "actTaskType"), Is.EqualTo(WfTaskType.access));
                Assert.That(matrix.InitCalls.Single(), Is.EqualTo((WfTaskType.access, "Alpha")));
            });
        }

        [Test]
        public async Task SetSelectedConfigurationActive_ActivatesSelectedConfigurationAndReordersList()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            SetProperty(component, "apiConnection", apiConnection);
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true },
                new() { Id = 2, Name = "Beta" }
            });
            SetField(component, "selectedConfigurationId", 2);
            apiConnection.Respond(RequestQueries.setActiveWorkflowConfiguration, new object());

            await InvokeAsync(component, "SetSelectedConfigurationActive");

            Assert.Multiple(() =>
            {
                object configurationVariables = GetRecordedCallVariables(apiConnection, RequestQueries.setActiveWorkflowConfiguration)
                    ?? throw new InvalidOperationException("Expected configuration mutation variables.");
                Assert.That(GetField<List<int>>(component, "configurationIds"), Is.EqualTo(kConfigurationIds21));
                Assert.That(GetAnonymousProperty<int>(configurationVariables, "configurationId"), Is.EqualTo(2));
                Assert.That(GetField<List<WorkflowConfiguration>>(component, "workflowConfigurations").Single(configuration => configuration.Id == 2).IsActive, Is.True);
                Assert.That(GetField<List<WorkflowConfiguration>>(component, "workflowConfigurations").Single(configuration => configuration.Id == 1).IsActive, Is.False);
            });
        }

        [Test]
        public async Task DeleteSelectedConfiguration_DeletesInactiveConfigurationAndRefreshesData()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            RecordingGlobalStateMatrix matrix = new();
            SetProperty(component, "apiConnection", apiConnection);
            SetField(component, "actStateMatrix", matrix);
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true },
                new() { Id = 2, Name = "Beta" }
            });
            SetField(component, "selectedConfigurationId", 2);
            apiConnection.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>
            {
                new() { PhaseMatrixId = 5 },
                new() { PhaseMatrixId = 5 },
                new() { PhaseMatrixId = 7 }
            });
            apiConnection.Respond(RequestQueries.deleteWorkflowConfiguration, new ReturnId { AffectedRows = 1 });
            apiConnection.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true }
            });
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());

            await InvokeAsync(component, "DeleteSelectedConfiguration");

            Assert.Multiple(() =>
            {
                object deleteVariables = GetRecordedCallVariables(apiConnection, RequestQueries.deleteWorkflowConfiguration)
                    ?? throw new InvalidOperationException("Expected delete mutation variables.");
                Assert.That(GetField<int>(component, "selectedConfigurationId"), Is.EqualTo(1));
                Assert.That(GetAnonymousProperty<List<int>>(deleteVariables, "phaseMatrixIds"),
                    Is.EqualTo(kPhaseMatrixIds57));
                Assert.That(matrix.InitCalls.Single(), Is.EqualTo((WfTaskType.master, "Alpha")));
            });
        }

        [Test]
        public async Task LinkTransitionGroup_UsesNextSortOrderAndClosesPopup()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            SetProperty(component, "apiConnection", apiConnection);
            SetField(component, "actStateMatrix", new GlobalStateMatrix
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new()
                }
            });
            SetProperty(GetField<GlobalStateMatrix>(component, "actStateMatrix"), "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.request] = new(10, "Request", [], [])
            });
            SetField(component, "linkTransitionGroupPhase", WorkflowPhases.request);
            SetField(component, "selectedTransitionGroupId", 3);
            SetField(component, "transitionGroups", new List<StateMatrixTransitionGroup>
            {
                GroupWithUsage(1, 10, 1),
                GroupWithUsage(2, 10, 4),
                new() { Id = 3, Name = "Target" }
            });
            apiConnection.Respond(RequestQueries.linkStateMatrixTransitionGroup, new ReturnId());
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());

            await InvokeAsync(component, "LinkTransitionGroup");

            Assert.Multiple(() =>
            {
                object linkVariables = GetRecordedCallVariables(apiConnection, RequestQueries.linkStateMatrixTransitionGroup)
                    ?? throw new InvalidOperationException("Expected link mutation variables.");
                Assert.That(GetField<bool>(component, "LinkTransitionGroupMode"), Is.False);
                Assert.That(GetAnonymousProperty<int>(linkVariables, "sortOrder"), Is.EqualTo(5));
                Assert.That(GetAnonymousProperty<int>(linkVariables, "phaseMatrixId"), Is.EqualTo(10));
                Assert.That(GetAnonymousProperty<int>(linkVariables, "transitionGroupId"), Is.EqualTo(3));
            });
        }

        [Test]
        public async Task TransitionGroupMutations_UnlinkAndDeleteUseExpectedMutations()
        {
            SettingsStateMatrix component = new();
            RecordingWorkflowApiConnection apiConnection = new();
            SetProperty(component, "apiConnection", apiConnection);
            SetField(component, "actStateMatrix", new GlobalStateMatrix
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new()
                }
            });
            SetProperty(GetField<GlobalStateMatrix>(component, "actStateMatrix"), "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.request] = new(10, "Request", [], [])
            });
            StateMatrixTransitionGroup group = new() { Id = 7, Name = "Shared" };
            SetField(component, "unlinkTransitionGroupPhase", WorkflowPhases.request);
            SetField(component, "transitionGroupToUnlink", group);
            SetField(component, "transitionGroupToDelete", group);
            apiConnection.Respond(RequestQueries.unlinkStateMatrixTransitionGroup, new ReturnId());
            apiConnection.Respond(RequestQueries.deleteStateMatrixTransitionGroup, new ReturnId());
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());

            await InvokeAsync(component, "UnlinkSelectedTransitionGroup");
            await InvokeAsync(component, "DeleteSelectedTransitionGroup");

            Assert.Multiple(() =>
            {
                object unlinkVariables = GetRecordedCallVariables(apiConnection, RequestQueries.unlinkStateMatrixTransitionGroup)
                    ?? throw new InvalidOperationException("Expected unlink mutation variables.");
                object deleteVariables = GetRecordedCallVariables(apiConnection, RequestQueries.deleteStateMatrixTransitionGroup)
                    ?? throw new InvalidOperationException("Expected delete mutation variables.");
                Assert.That(GetAnonymousProperty<int>(unlinkVariables, "phaseMatrixId"), Is.EqualTo(10));
                Assert.That(GetAnonymousProperty<int>(unlinkVariables, "transitionGroupId"), Is.EqualTo(7));
                Assert.That(GetAnonymousProperty<int>(deleteVariables, "id"), Is.EqualTo(7));
            });
        }

        [Test]
        public void ConfigurationDisplayName_UsesActiveSuffixAndReturnsEmptyForUnknownConfiguration()
        {
            SettingsStateMatrix component = new();
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 1, Name = "Alpha", IsActive = true },
                new() { Id = 2, Name = "Beta" }
            });

            Assert.Multiple(() =>
            {
                Assert.That(Invoke(component, "ConfigurationDisplayName", 1), Is.EqualTo("Alpha (Active)"));
                Assert.That(Invoke(component, "ConfigurationDisplayName", 2), Is.EqualTo("Beta"));
                Assert.That(Invoke(component, "ConfigurationDisplayName", 99), Is.EqualTo(""));
                Assert.That(Invoke(component, "TransitionGroupName", 77), Is.EqualTo(""));
            });
        }

        [Test]
        public void DerivedStateEditorTitle_ReflectsAddAndEditModes()
        {
            SettingsStateMatrix component = new();
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetField(component, "derivedStatePhase", WorkflowPhases.request);

            Assert.That(GetProperty<string>(component, "DerivedStateEditorTitle"), Is.EqualTo("add_derived_state: request"));

            SetField(component, "originalDerivedFromStateId", 1);
            Assert.That(GetProperty<string>(component, "DerivedStateEditorTitle"), Is.EqualTo("edit_derived_state: request"));
        }

        [Test]
        public void CloseDerivedStateEditor_ClearsEditState()
        {
            SettingsStateMatrix component = new();
            SetField(component, "EditDerivedStateMode", true);
            SetField(component, "originalDerivedFromStateId", 1);

            Invoke(component, "CloseDerivedStateEditor");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "EditDerivedStateMode"), Is.False);
                Assert.That(GetFieldValue(component, "originalDerivedFromStateId"), Is.Null);
            });
        }

        [Test]
        public async Task SaveMatrix_DelegatesToMatrixSaveAndClosesDialog()
        {
            SettingsStateMatrix component = new();
            RecordingGlobalStateMatrix matrix = new();
            SetField(component, "actStateMatrix", matrix);
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetField(component, "SaveMatrixMode", true);

            await InvokeAsync(component, "SaveMatrix");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "SaveMatrixMode"), Is.False);
                Assert.That(matrix.SaveCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public void RefreshConfigurationIds_OrdersActiveFirstThenByName()
        {
            SettingsStateMatrix component = new();
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration>
            {
                new() { Id = 3, Name = "Zulu" },
                new() { Id = 1, Name = "Beta", IsActive = true },
                new() { Id = 2, Name = "Alpha" }
            });

            Invoke(component, "RefreshConfigurationIds");

            Assert.That(GetField<List<int>>(component, "configurationIds"), Is.EqualTo(kConfigurationIds123));
        }

        [Test]
        public void SelectedConfiguration_TracksSelectedIdAndReturnsNullForUnknownId()
        {
            SettingsStateMatrix component = new();
            WorkflowConfiguration selected = new() { Id = 4, Name = "Selected" };
            SetField(component, "workflowConfigurations", new List<WorkflowConfiguration> { selected });
            SetField(component, "selectedConfigurationId", 4);
            Assert.That(GetProperty<WorkflowConfiguration>(component, "SelectedConfiguration"), Is.SameAs(selected));

            SetField(component, "selectedConfigurationId", 99);
            Assert.That(GetNullableProperty<WorkflowConfiguration>(component, "SelectedConfiguration"), Is.Null);
        }

        [Test]
        public void GetPhaseTransitionGroups_UsesBindingOrderAndSkipsMissingGroups()
        {
            SettingsStateMatrix component = new();
            GlobalStateMatrix matrix = MatrixWithPhase(WorkflowPhases.approval);
            SetProperty(matrix, "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.approval] = new(6, "Approval", [2, 99, 1], [])
            });
            SetField(component, "actStateMatrix", matrix);
            SetField(component, "transitionGroups", new List<StateMatrixTransitionGroup>
            {
                new() { Id = 1, Name = "First" },
                new() { Id = 2, Name = "Second" }
            });

            List<StateMatrixTransitionGroup> result = (List<StateMatrixTransitionGroup>)Invoke(component, "GetPhaseTransitionGroups", WorkflowPhases.approval)!;

            Assert.That(result.Select(group => group.Id), Is.EqualTo(kGroupIds21));
            Assert.That((List<StateMatrixTransitionGroup>)Invoke(component, "GetPhaseTransitionGroups", WorkflowPhases.request)!, Is.Empty);
        }

        [Test]
        public void TransitionGroupManagerActions_SelectOverviewOrSpecificGroup()
        {
            SettingsStateMatrix component = new();
            SetField(component, "transitionGroupIdToEdit", 5);
            Invoke(component, "OpenTransitionGroupManager");
            Assert.Multiple(() =>
            {
                Assert.That(GetFieldValue(component, "transitionGroupIdToEdit"), Is.Null);
                Assert.That(GetField<bool>(component, "EditTransitionGroupsMode"), Is.True);
            });

            SetField(component, "EditTransitionGroupsMode", false);
            Invoke(component, "EditTransitionGroup", new StateMatrixTransitionGroup { Id = 17 });
            Assert.Multiple(() =>
            {
                Assert.That(GetField<int?>(component, "transitionGroupIdToEdit"), Is.EqualTo(17));
                Assert.That(GetField<bool>(component, "EditTransitionGroupsMode"), Is.True);
            });
        }

        [Test]
        public void OpenLinkTransitionGroup_FiltersLinkedAndWrongPhaseGroups()
        {
            SettingsStateMatrix component = new();
            GlobalStateMatrix matrix = MatrixWithPhase(WorkflowPhases.request);
            SetProperty(matrix, "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.request] = new(10, "Request", [1], [])
            });
            SetField(component, "actStateMatrix", matrix);
            SetField(component, "transitionGroups", new List<StateMatrixTransitionGroup>
            {
                new() { Id = 1, Name = "Already linked", Phase = "request" },
                new() { Id = 2, Name = "Generic" },
                new() { Id = 3, Name = "Request", Phase = "REQUEST" },
                new() { Id = 4, Name = "Approval", Phase = "approval" }
            });

            Invoke(component, "OpenLinkTransitionGroup", WorkflowPhases.request);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<List<int>>(component, "linkableTransitionGroupIds"), Is.EqualTo(kLinkableTransitionGroupIds23));
                Assert.That(GetField<int>(component, "selectedTransitionGroupId"), Is.EqualTo(2));
                Assert.That(GetField<bool>(component, "LinkTransitionGroupMode"), Is.True);
            });
        }

        [Test]
        public void DerivedStateOverview_IsSparseAndSorted()
        {
            SettingsStateMatrix component = ComponentWithStates([1, 2, 3, 4], new Dictionary<int, int>
            {
                [3] = 4,
                [1] = 1,
                [2] = 4
            });

            List<KeyValuePair<int, int>> result = (List<KeyValuePair<int, int>>)Invoke(component, "GetDerivedStates", WorkflowPhases.request)!;

            Assert.Multiple(() =>
            {
                Assert.That(result.Select(mapping => mapping.Key), Is.EqualTo(kDerivedStateKeys23));
                Assert.That(Invoke(component, "HasDerivedStates", WorkflowPhases.request), Is.True);
            });
        }

        [Test]
        public void AddDerivedState_SupportsZeroStateIdAndSelectsDifferentTarget()
        {
            SettingsStateMatrix component = ComponentWithStates([0, 1], []);

            Invoke(component, "AddDerivedState", WorkflowPhases.request);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<int>(component, "selectedDerivedFromStateId"), Is.Zero);
                Assert.That(GetField<int>(component, "selectedDerivedStateId"), Is.EqualTo(1));
                Assert.That(GetField<bool>(component, "EditDerivedStateMode"), Is.True);
            });
        }

        [Test]
        public void CanAddDerivedState_RequiresTwoStatesAndAnUnusedInputState()
        {
            SettingsStateMatrix oneState = ComponentWithStates([1], []);
            Assert.That(Invoke(oneState, "CanAddDerivedState", WorkflowPhases.request), Is.False);

            SettingsStateMatrix fullyMapped = ComponentWithStates([1, 2], new Dictionary<int, int> { [1] = 2, [2] = 1 });
            Assert.That(Invoke(fullyMapped, "CanAddDerivedState", WorkflowPhases.request), Is.False);

            fullyMapped = ComponentWithStates([1, 2], new Dictionary<int, int> { [1] = 1, [2] = 1 });
            Assert.That(Invoke(fullyMapped, "CanAddDerivedState", WorkflowPhases.request), Is.True);
        }

        [Test]
        public void CanSaveDerivedState_RejectsIdentityAndDuplicateInputMappings()
        {
            SettingsStateMatrix component = ComponentWithStates([1, 2, 3], new Dictionary<int, int> { [2] = 3 });
            SetField(component, "derivedStatePhase", WorkflowPhases.request);
            SetField(component, "selectedDerivedFromStateId", 1);
            SetField(component, "selectedDerivedStateId", 1);
            Assert.That(GetProperty<bool>(component, "CanSaveDerivedState"), Is.False);

            SetField(component, "selectedDerivedFromStateId", 2);
            SetField(component, "selectedDerivedStateId", 1);
            Assert.That(GetProperty<bool>(component, "CanSaveDerivedState"), Is.False);

            SetField(component, "selectedDerivedFromStateId", 1);
            SetField(component, "selectedDerivedStateId", 3);
            Assert.That(GetProperty<bool>(component, "CanSaveDerivedState"), Is.True);
        }

        [Test]
        public void SaveDerivedState_CanMoveMappingToAnotherInputState()
        {
            SettingsStateMatrix component = ComponentWithStates([1, 2, 3], new Dictionary<int, int> { [1] = 3 });
            Invoke(component, "EditDerivedState", WorkflowPhases.request, new KeyValuePair<int, int>(1, 3));
            SetField(component, "selectedDerivedFromStateId", 2);
            SetField(component, "selectedDerivedStateId", 3);

            Invoke(component, "SaveDerivedState");

            Dictionary<int, int> mappings = GetField<GlobalStateMatrix>(component, "actStateMatrix").GlobalMatrix[WorkflowPhases.request].DerivedStates;
            Assert.Multiple(() =>
            {
                Assert.That(mappings, Is.EqualTo(new Dictionary<int, int> { [2] = 3 }));
                Assert.That(GetField<bool>(component, "EditDerivedStateMode"), Is.False);
                Assert.That(GetFieldValue(component, "originalDerivedFromStateId"), Is.Null);
            });
        }

        [Test]
        public void RequestDeleteTransitionGroup_OpensOnlyForSingleUseGroup()
        {
            SettingsStateMatrix component = new();
            StateMatrixTransitionGroup shared = GroupWithUsages(1, 2);
            Invoke(component, "RequestDeleteTransitionGroup", shared);
            Assert.That(GetField<bool>(component, "DeleteTransitionGroupMode"), Is.False);

            StateMatrixTransitionGroup single = GroupWithUsages(3);
            Invoke(component, "RequestDeleteTransitionGroup", single);
            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "DeleteTransitionGroupMode"), Is.True);
                Assert.That(GetField<StateMatrixTransitionGroup>(component, "transitionGroupToDelete"), Is.SameAs(single));
            });
        }

        [Test]
        public void RequestUnlinkTransitionGroup_StoresPhaseAndGroup()
        {
            SettingsStateMatrix component = new();
            StateMatrixTransitionGroup group = new() { Id = 8 };

            Invoke(component, "RequestUnlinkTransitionGroup", WorkflowPhases.implementation, group);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<WorkflowPhases>(component, "unlinkTransitionGroupPhase"), Is.EqualTo(WorkflowPhases.implementation));
                Assert.That(GetField<StateMatrixTransitionGroup>(component, "transitionGroupToUnlink"), Is.SameAs(group));
                Assert.That(GetField<bool>(component, "UnlinkTransitionGroupMode"), Is.True);
            });
        }

        [Test]
        public void InitSaveMatrix_OpensConfirmation()
        {
            SettingsStateMatrix component = new();
            Invoke(component, "InitSaveMatrix");
            Assert.That(GetField<bool>(component, "SaveMatrixMode"), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task OpenConfigurationTransfer_SelectsModeAndOpensPopup(bool importMode)
        {
            SettingsStateMatrix component = new();

            await InvokeAsync(component, "OpenConfigurationTransfer", importMode);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<bool>(component, "transferImportMode"), Is.EqualTo(importMode));
                Assert.That(GetField<bool>(component, "TransferConfigurationMode"), Is.True);
            });
        }

        [Test]
        public void HasUnsavedChanges_ReflectsDeferredMatrixEditsAfterInitialization()
        {
            SettingsStateMatrix component = ComponentWithSnapshot();

            Assert.That(GetProperty<bool>(component, "HasUnsavedChanges"), Is.False);
            GetField<GlobalStateMatrix>(component, "actStateMatrix").GlobalMatrix[WorkflowPhases.request].Active = true;
            Assert.That(GetProperty<bool>(component, "HasUnsavedChanges"), Is.True);
        }

        [Test]
        public async Task ExecuteOrConfirmDiscard_DefersDirtyActionUntilConfirmed()
        {
            SettingsStateMatrix component = ComponentWithSnapshot();
            GetField<GlobalStateMatrix>(component, "actStateMatrix").GlobalMatrix[WorkflowPhases.request].LowestEndState = 9;
            bool executed = false;

            await InvokeAsync(component, "ExecuteOrConfirmDiscard", (Func<Task>)(() =>
            {
                executed = true;
                return Task.CompletedTask;
            }), null);

            Assert.Multiple(() =>
            {
                Assert.That(executed, Is.False);
                Assert.That(GetField<bool>(component, "DiscardChangesMode"), Is.True);
                Assert.That(GetFieldValue(component, "pendingDiscardAction"), Is.Not.Null);
            });

            await InvokeAsync(component, "ConfirmDiscardChanges");

            Assert.Multiple(() =>
            {
                Assert.That(executed, Is.True);
                Assert.That(GetField<bool>(component, "DiscardChangesMode"), Is.False);
                Assert.That(GetFieldValue(component, "pendingDiscardAction"), Is.Null);
            });
        }

        [Test]
        public async Task DiscardChangesDisplayChanged_CancelsPendingSelectorChange()
        {
            SettingsStateMatrix component = ComponentWithSnapshot();
            GetField<GlobalStateMatrix>(component, "actStateMatrix").GlobalMatrix[WorkflowPhases.request].Active = true;
            SetField(component, "selectedConfigurationId", 3);

            await InvokeAsync(component, "ChangeConfiguration", 8);
            await InvokeAsync(component, "DiscardChangesDisplayChanged", false);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<int>(component, "selectedConfigurationId"), Is.EqualTo(3));
                Assert.That(GetFieldValue(component, "pendingDiscardAction"), Is.Null);
                Assert.That(GetFieldValue(component, "pendingDiscardCancellation"), Is.Null);
            });
        }

        [Test]
        public async Task RefreshTransitionGroups_UpdatesBindingsWithoutReplacingDeferredValues()
        {
            SettingsStateMatrix component = ComponentWithSnapshot();
            GlobalStateMatrix matrix = GetField<GlobalStateMatrix>(component, "actStateMatrix");
            matrix.GlobalMatrix[WorkflowPhases.request].LowestEndState = 42;
            SetProperty(matrix, "PhaseBindings", new Dictionary<WorkflowPhases, StateMatrixPhaseBinding>
            {
                [WorkflowPhases.request] = new(7, "Request", [99], [])
            });
            RecordingWorkflowApiConnection apiConnection = new();
            apiConnection.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>
            {
                GroupWithUsage(1, 7, 2),
                GroupWithUsage(2, 7, 1),
                GroupWithUsage(3, 8, 0)
            });
            SetProperty(component, "apiConnection", apiConnection);

            await InvokeAsync(component, "RefreshTransitionGroups");

            Assert.Multiple(() =>
            {
                Assert.That(matrix.PhaseBindings[WorkflowPhases.request].TransitionGroupIds, Is.EqualTo(kTransitionGroupIds21));
                Assert.That(matrix.GlobalMatrix[WorkflowPhases.request].LowestEndState, Is.EqualTo(42));
                Assert.That(GetProperty<bool>(component, "HasUnsavedChanges"), Is.True);
            });
        }

        private static SettingsStateMatrix ComponentWithStates(List<int> stateIds, Dictionary<int, int> derivedStates)
        {
            SettingsStateMatrix component = new();
            SetField(component, "stateIds", stateIds);
            SetField(component, "actStateMatrix", new GlobalStateMatrix
            {
                GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix>
                {
                    [WorkflowPhases.request] = new() { DerivedStates = derivedStates }
                }
            });
            return component;
        }

        private static GlobalStateMatrix MatrixWithPhase(WorkflowPhases phase) => new()
        {
            GlobalMatrix = new Dictionary<WorkflowPhases, StateMatrix> { [phase] = new() }
        };

        private static SettingsStateMatrix ComponentWithSnapshot()
        {
            SettingsStateMatrix component = new();
            GlobalStateMatrix matrix = MatrixWithPhase(WorkflowPhases.request);
            SetProperty(matrix, "OriginalGlobalMatrix", new Dictionary<WorkflowPhases, StateMatrix>
            {
                [WorkflowPhases.request] = new()
            });
            SetField(component, "actStateMatrix", matrix);
            SetField(component, "InitComplete", true);
            return component;
        }

        private static StateMatrixTransitionGroup GroupWithUsages(params int[] phaseMatrixIds) => new()
        {
            PhaseMatrixUsages = phaseMatrixIds.Select(id => new StateMatrixPhaseTransitionGroup { PhaseMatrixId = id }).ToList()
        };

        private static StateMatrixTransitionGroup GroupWithUsage(int groupId, int phaseMatrixId, int sortOrder) => new()
        {
            Id = groupId,
            PhaseMatrixUsages = [new() { PhaseMatrixId = phaseMatrixId, SortOrder = sortOrder }]
        };

        private static object? GetRecordedCallVariables(RecordingWorkflowApiConnection apiConnection, string query) =>
            apiConnection.Calls.First(call => call.Query == query).Variables;

        private static T GetAnonymousProperty<T>(object? instance, string propertyName)
        {
            if (instance == null)
            {
                throw new InvalidOperationException($"Expected anonymous payload for {propertyName}.");
            }
            System.Reflection.PropertyInfo property = instance.GetType().GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)(property.GetValue(instance) ?? throw new InvalidOperationException($"Property {propertyName} is null."));
        }

        private static T? GetNullableProperty<T>(object instance, string propertyName) where T : class
        {
            System.Reflection.PropertyInfo property = instance.GetType().GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return property.GetValue(instance) as T;
        }

        private sealed class RecordingGlobalStateMatrix : GlobalStateMatrix
        {
            public List<(WfTaskType TaskType, string? ConfigurationName)> InitCalls { get; } = [];
            public int SaveCalls { get; private set; }

            public override Task Init(ApiConnection apiConnection, WfTaskType taskType = WfTaskType.master)
            {
                InitCalls.Add((taskType, null));
                return Task.CompletedTask;
            }

            public override Task Init(ApiConnection apiConnection, WfTaskType taskType, string configurationName)
            {
                InitCalls.Add((taskType, configurationName));
                return Task.CompletedTask;
            }

            public override Task Save(ApiConnection apiConnection)
            {
                SaveCalls++;
                return Task.CompletedTask;
            }
        }
    }
}
