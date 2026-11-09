using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class AddWorkflowConfigurationComponentTest
    {
        private static readonly int[] kExpectedSourceConfigurationIds = [0, 3, 9];

        [Test]
        public void CanSave_RequiresNonBlankUniqueNameAndValidCreationSource()
        {
            AddWorkflowConfiguration component = new();
            SetField(component, "name", "Candidate");
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            SetProperty(component, nameof(AddWorkflowConfiguration.StateIds), new List<int> { 1 });
            SetField(component, "name", "   ");
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            SetProperty(component, nameof(AddWorkflowConfiguration.ExistingConfigurations), new List<WorkflowConfiguration>
            {
                new() { Id = 2, Name = "Candidate" }
            });
            SetField(component, "name", " candidate ");
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            SetField(component, "name", "Independent");
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.True);

            SetProperty(component, nameof(AddWorkflowConfiguration.StateIds), new List<int>());
            SetProperty(component, nameof(AddWorkflowConfiguration.ExistingConfigurations), new List<WorkflowConfiguration>
            {
                new() { Id = 4, Name = "Source" }
            });
            SetField(component, "selectedSourceConfigurationId", 4);
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.True);
        }

        [Test]
        public void OnParametersSet_ResetsInputsOnlyWhenPopupOpens()
        {
            AddWorkflowConfiguration component = new();
            SetField(component, "name", "Old");
            SetField(component, "description", "Old description");
            SetField(component, "selectedSourceConfigurationId", 9);
            SetProperty(component, nameof(AddWorkflowConfiguration.ExistingConfigurations), new List<WorkflowConfiguration>
            {
                new() { Id = 9, Name = "Zulu" },
                new() { Id = 3, Name = "Alpha" }
            });
            SetProperty(component, nameof(AddWorkflowConfiguration.Display), true);

            Invoke(component, "OnParametersSet");

            Assert.Multiple(() =>
            {
                Assert.That(GetField<string>(component, "name"), Is.Empty);
                Assert.That(GetFieldValue(component, "description"), Is.Null);
                Assert.That(GetField<int>(component, "selectedSourceConfigurationId"), Is.Zero);
                Assert.That(GetField<List<int>>(component, "sourceConfigurationIds"), Is.EqualTo(kExpectedSourceConfigurationIds));
            });

            SetField(component, "name", "In progress");
            Invoke(component, "OnParametersSet");
            Assert.That(GetField<string>(component, "name"), Is.EqualTo("In progress"));
        }

        [Test]
        public void BuildClonedPhaseMapping_CopiesAllPhaseValuesAndNestedMappings()
        {
            WorkflowConfigurationPhase source = new()
            {
                TaskType = "generic",
                Phase = "approval",
                PhaseMatrix = new StateMatrixPhase
                {
                    Phase = "approval",
                    Active = true,
                    LowestInputState = 10,
                    LowestStartState = 20,
                    LowestEndState = 30,
                    DerivedStates = [new() { FromStateId = 11, DerivedStateId = 21 }],
                    TransitionGroups = [new() { TransitionGroupId = 7, SortOrder = 4 }]
                }
            };

            JObject result = JObject.FromObject(InvokeStatic("BuildClonedPhaseMapping", source, "Clone")!);
            JToken data = result["state_matrix_phase"]!["data"]!;

            Assert.Multiple(() =>
            {
                Assert.That((string?)result["task_type"], Is.EqualTo("generic"));
                Assert.That((string?)result["phase"], Is.EqualTo("approval"));
                Assert.That((string?)data["name"], Is.EqualTo("Clone::generic::approval"));
                Assert.That((bool?)data["active"], Is.True);
                Assert.That((int?)data["lowest_input_state"], Is.EqualTo(10));
                Assert.That((int?)data["lowest_start_state"], Is.EqualTo(20));
                Assert.That((int?)data["lowest_end_state"], Is.EqualTo(30));
                Assert.That((int?)data["state_matrix_derived_states"]?["data"]?[0]?["derived_state_id"], Is.EqualTo(21));
                Assert.That((int?)data["state_matrix_phase_transition_groups"]?["data"]?[0]?["transition_group_id"], Is.EqualTo(7));
                Assert.That((int?)data["state_matrix_phase_transition_groups"]?["data"]?[0]?["sort_order"], Is.EqualTo(4));
                Assert.That(result["phase_matrix_id"], Is.Null);
            });
        }

        [Test]
        public void BuildClonedPhaseMapping_PreservesEmptyNestedCollections()
        {
            WorkflowConfigurationPhase source = new()
            {
                TaskType = "master",
                Phase = "request",
                PhaseMatrix = new StateMatrixPhase { Phase = "request" }
            };

            JObject result = JObject.FromObject(InvokeStatic("BuildClonedPhaseMapping", source, "Empty")!);
            JToken data = result["state_matrix_phase"]!["data"]!;

            Assert.Multiple(() =>
            {
                Assert.That(data["state_matrix_derived_states"]?["data"], Is.Empty);
                Assert.That(data["state_matrix_phase_transition_groups"]?["data"], Is.Empty);
            });
        }

        [Test]
        public void BuildEmptyPhaseMappings_CreatesEveryTaskTypeAndPhase()
        {
            List<object> mappings = (List<object>)InvokeStatic("BuildEmptyPhaseMappings", "Blank", 6)!;
            List<JObject> serialized = mappings.Select(JObject.FromObject).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(mappings, Has.Count.EqualTo(Enum.GetValues<WfTaskType>().Length * Enum.GetValues<WorkflowPhases>().Length));
                Assert.That(serialized.Select(mapping => (string?)mapping["task_type"]).Distinct(),
                    Is.EquivalentTo(Enum.GetNames<WfTaskType>()));
                Assert.That(serialized.Select(mapping => (string?)mapping["phase"]).Distinct(),
                    Is.EquivalentTo(Enum.GetNames<WorkflowPhases>()));
                Assert.That(serialized.All(mapping => (bool?)mapping["state_matrix_phase"]?["data"]?["active"] == false), Is.True);
                Assert.That(serialized.All(mapping => (int?)mapping["state_matrix_phase"]?["data"]?["lowest_input_state"] == 6), Is.True);
                Assert.That(serialized.All(mapping => !((JArray)mapping["state_matrix_phase"]!["data"]!["state_matrix_derived_states"]!["data"]!).Any()), Is.True);
            });
        }

        [Test]
        public async Task Save_TrimsPayloadClonesPhasesAndClosesPopup()
        {
            WorkflowConfiguration sourceConfiguration = new() { Id = 5, Name = "Source" };
            WorkflowConfigurationPhase sourcePhase = new()
            {
                TaskType = "master",
                Phase = "request",
                PhaseMatrix = new StateMatrixPhase { Phase = "request", Active = true }
            };
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { sourcePhase });
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 91 });
            AddWorkflowConfiguration component = new();
            SetProperty(component, "apiConnection", api);
            SetProperty(component, nameof(AddWorkflowConfiguration.ExistingConfigurations), new List<WorkflowConfiguration> { sourceConfiguration });
            SetProperty(component, nameof(AddWorkflowConfiguration.Display), true);
            SetField(component, "selectedSourceConfigurationId", sourceConfiguration.Id);
            SetField(component, "name", "  New config  ");
            SetField(component, "description", "  Description  ");

            await InvokeAsync(component, "Save");

            JObject lookupVariables = JObject.FromObject(api.Calls[0].Variables!);
            JObject createVariables = JObject.FromObject(api.Calls[1].Variables!);
            Assert.Multiple(() =>
            {
                Assert.That((int?)lookupVariables["configurationId"], Is.EqualTo(5));
                Assert.That((string?)createVariables["name"], Is.EqualTo("New config"));
                Assert.That((string?)createVariables["description"], Is.EqualTo("Description"));
                Assert.That((string?)createVariables["phaseMappings"]?[0]?["state_matrix_phase"]?["data"]?["name"], Is.EqualTo("New config::master::request"));
                Assert.That(GetProperty<bool>(component, nameof(AddWorkflowConfiguration.Display)), Is.False);
            });
        }

        [Test]
        public async Task Save_EmptySelectionCreatesCompleteEmptyConfigurationWithoutLookup()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 92 });
            AddWorkflowConfiguration component = new();
            SetProperty(component, "apiConnection", api);
            SetProperty(component, nameof(AddWorkflowConfiguration.StateIds), new List<int> { 8, 4 });
            SetProperty(component, nameof(AddWorkflowConfiguration.Display), true);
            SetField(component, "name", "Blank");

            await InvokeAsync(component, "Save");

            Assert.That(api.Calls, Has.Count.EqualTo(1));
            JObject variables = JObject.FromObject(api.Calls.Single().Variables!);
            JArray mappings = (JArray)variables["phaseMappings"]!;
            Assert.Multiple(() =>
            {
                Assert.That(api.Calls[0].Query, Is.EqualTo(RequestQueries.createWorkflowConfiguration));
                Assert.That(mappings, Has.Count.EqualTo(Enum.GetValues<WfTaskType>().Length * Enum.GetValues<WorkflowPhases>().Length));
                Assert.That(mappings.All(mapping => (int?)mapping["state_matrix_phase"]?["data"]?["lowest_input_state"] == 4), Is.True);
                Assert.That(mappings.All(mapping => (bool?)mapping["state_matrix_phase"]?["data"]?["active"] == false), Is.True);
            });
        }

        private static object? InvokeStatic(string methodName, params object?[] parameters)
        {
            System.Reflection.MethodInfo method = typeof(AddWorkflowConfiguration).GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(AddWorkflowConfiguration).FullName, methodName);
            return method.Invoke(null, parameters);
        }
    }
}
