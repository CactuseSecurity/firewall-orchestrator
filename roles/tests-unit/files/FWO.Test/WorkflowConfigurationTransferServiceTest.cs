using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class WorkflowConfigurationTransferServiceTest
    {
        private static readonly string[] kExpectedPhaseTransitionGroups = ["Reviewers"];
        private static readonly string[] kExpectedVisibilityGroupMembers = ["cn=operators"];
        private static readonly string[] kExpectedRequestPhase = ["request"];
        private static readonly string[] kExpectedSelectedTransitionGroups = ["Reviewers"];

        [TestCase(true)]
        [TestCase(false)]
        public async Task Export_UsesNamesAndOptionallyIncludesVisibilityGroups(bool includeVisibilityGroups)
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 5, Name = "Shared", Description = "Package" }
            });
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>
            {
                PhaseMapping(10)
            });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>
            {
                TransitionGroup(10, 20)
            });
            api.Respond(RequestQueries.getStates, States());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>
            {
                VisibilityGroup(20)
            });
            WorkflowConfigurationTransferService service = new(api);

            WorkflowConfigurationTransferPackage package = await service.Export(5, includeVisibilityGroups);

            Assert.Multiple(() =>
            {
                Assert.That(package.Format, Is.EqualTo(WorkflowConfigurationTransferPackage.kFormat));
                Assert.That(package.Configuration.Name, Is.EqualTo("Shared"));
                Assert.That(package.Configuration.Phases[0].TransitionGroups, Is.EqualTo(kExpectedPhaseTransitionGroups));
                Assert.That(package.States.Single(state => state.Id == 2).Name, Is.EqualTo("Automatic approval"));
                Assert.That(package.States.Single(state => state.Id == 2).AutomaticOnly, Is.True);
                Assert.That(package.TransitionGroups[0].VisibilityGroup, Is.EqualTo(includeVisibilityGroups ? "Operators" : null));
                Assert.That(package.TransitionGroups[0].Exclusive, Is.EqualTo(includeVisibilityGroups));
                Assert.That(package.VisibilityGroups?.Single().Members, Is.EqualTo(includeVisibilityGroups ? kExpectedVisibilityGroupMembers : null));
            });
        }

        [Test]
        public async Task Export_IncludesOnlySelectedPhasesAndTheirTransitionGroups()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 5, Name = "Shared" }
            });
            WorkflowConfigurationPhase selected = PhaseMapping(10);
            WorkflowConfigurationPhase omitted = PhaseMapping(11);
            omitted.Phase = WorkflowPhases.approval.ToString();
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { selected, omitted });
            StateMatrixTransitionGroup omittedGroup = TransitionGroup(11, 21);
            omittedGroup.Name = "Approvers";
            omittedGroup.VisibilityGroup!.Name = "Managers";
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>
            {
                TransitionGroup(10, 20), omittedGroup
            });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationTransferService service = new(api);

            WorkflowConfigurationTransferPackage package = await service.Export(5, false,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "master:request" });

            Assert.Multiple(() =>
            {
                Assert.That(package.Configuration.Phases.Select(phase => phase.Phase), Is.EqualTo(kExpectedRequestPhase));
                Assert.That(package.TransitionGroups.Select(group => group.Name), Is.EqualTo(kExpectedSelectedTransitionGroups));
            });
        }

        [Test]
        public void Export_RejectsAnEmptyPhaseSelection()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { PhaseMapping(10) });
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Export(5, false, new HashSet<string>()));

            Assert.That(exception?.Message, Does.Contain("At least one"));
        }

        [Test]
        public void Package_RoundTripsWithVersionedPortablePropertyNames()
        {
            string json = JsonSerializer.Serialize(Package());
            WorkflowConfigurationTransferPackage result = JsonSerializer.Deserialize<WorkflowConfigurationTransferPackage>(json)
                ?? throw new InvalidDataException("Package did not deserialize.");

            WorkflowConfigurationTransferService.ValidateStructure(result);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"transition_groups\""));
                Assert.That(json, Does.Contain("\"states\""));
                Assert.That(json, Does.Contain("\"visibility_groups\""));
                Assert.That(result.Configuration.Phases[0].DerivedStates[0].DerivedStateId, Is.EqualTo(3));
                Assert.That(result.States.Single(state => state.Id == 2).AutomaticOnly, Is.True);
            });
        }

        [Test]
        public async Task Import_CreatesDependenciesAndRemapsNamesToIds()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
            WorkflowConfigurationTransferService service = new(api);

            int result = await service.Import(Package(), "Imported");

            JObject configurationVariables = JObject.FromObject(api.Calls.Single(call => call.Query == RequestQueries.createWorkflowConfiguration).Variables!);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(40));
                Assert.That((int?)configurationVariables["phaseMappings"]?[0]?["state_matrix_phase"]?["data"]?
                    ["state_matrix_phase_transition_groups"]?["data"]?[0]?["transition_group_id"], Is.EqualTo(30));
                Assert.That((string?)configurationVariables["name"], Is.EqualTo("Imported"));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.replaceWorkflowVisibilityGroupMembers), Is.True);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.replaceStateMatrixTransitionGroupTransitions), Is.True);
                Assert.That(service.LastImportStatistics.CreatedStates, Is.EqualTo(0));
                Assert.That(service.LastImportStatistics.CreatedTransitionGroups, Is.EqualTo(1));
                Assert.That(service.LastImportStatistics.CreatedVisibilityGroups, Is.EqualTo(1));
                Assert.That(service.LastImportStatistics.AddedPhases, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Import_CreatesMissingStatesFromPackageDefinitions()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createState, new object());
            api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases[0].LowestEndState = 999;
            package.States.Add(new WorkflowStateTransferData { Id = 999, Name = "Automatic approval", AutomaticOnly = true });
            WorkflowConfigurationTransferService service = new(api);

            await service.Import(package, "Imported");

            JObject stateVariables = JObject.FromObject(api.Calls.Single(call => call.Query == RequestQueries.createState).Variables!);
            Assert.Multiple(() =>
            {
                Assert.That((int?)stateVariables["id"], Is.EqualTo(999));
                Assert.That((string?)stateVariables["name"], Is.EqualTo("Automatic approval"));
                Assert.That((bool?)stateVariables["automaticOnly"], Is.True);
            });
        }

        [Test]
        public async Task Import_DoesNotOverwriteExistingStateDefinitions()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
            WorkflowConfigurationTransferPackage package = Package();
            package.States.Single(state => state.Id == 1).Name = "Source name";
            WorkflowConfigurationTransferService service = new(api);

            await service.Import(package, "Imported");

            Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createState), Is.False);
        }

        [Test]
        public void Import_RollsBackMissingStatesWhenExistingGroupWouldBeOverridden()
        {
            RecordingWorkflowApiConnection api = new();
            StateMatrixTransitionGroup conflictingGroup = TransitionGroup(30, 20);
            conflictingGroup.Description = "Local definition";
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { conflictingGroup });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            api.Respond(RequestQueries.createState, new object());
            api.Respond(RequestQueries.deleteState, new object());
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases[0].LowestEndState = 999;
            package.States.Add(new WorkflowStateTransferData { Id = 999, Name = "Imported end state" });
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Import(package, "Imported"));

            Assert.Multiple(() =>
            {
                Assert.That(exception?.Message, Does.Contain("different definition"));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createState), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.deleteState), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.False);
            });
        }

        [Test]
        public async Task Import_ReusesEquivalentNamedGroups()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { TransitionGroup(30, 20) });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
            WorkflowConfigurationTransferService service = new(api);

            await service.Import(Package(), "Imported");

            Assert.Multiple(() =>
            {
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowVisibilityGroup), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createStateMatrixTransitionGroup), Is.False);
                Assert.That(api.Calls.Count(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task Import_MergesIntoSelectedExistingConfiguration()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>
            {
                new() { Id = 5, Name = "Existing" }
            });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { TransitionGroup(30, 20) });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationPhase targetPhase = PhaseMapping(30);
            targetPhase.PhaseMatrixId = 101;
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { targetPhase });
            WorkflowConfigurationTransferService service = new(api);

            int result = await service.Import(Package(), "Ignored", 5);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(5));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.addWorkflowConfigurationPhase), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.replaceStateMatrixConfiguration), Is.False);
            });
        }

        [Test]
        public async Task Import_MergesNewDerivedStateAndTransitionGroupLink()
        {
            RecordingWorkflowApiConnection api = new();
            WorkflowTransitionGroupTransferData importedExtraGroup = new()
            {
                Name = "Additional",
                Transitions = []
            };
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases[0].DerivedStates.Add(new() { FromStateId = 2, DerivedStateId = 3 });
            package.Configuration.Phases[0].TransitionGroups.Add(importedExtraGroup.Name);
            package.TransitionGroups.Add(importedExtraGroup);
            StateMatrixTransitionGroup existingExtraGroup = new() { Id = 31, Name = "Additional", Transitions = [] };
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Existing" } });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>
            {
                TransitionGroup(30, 20), existingExtraGroup
            });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationPhase targetPhase = PhaseMapping(30);
            targetPhase.PhaseMatrixId = 101;
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { targetPhase });
            api.Respond(RequestQueries.replaceStateMatrixConfiguration, new object());
            api.Respond(RequestQueries.linkStateMatrixTransitionGroup, new object());
            WorkflowConfigurationTransferService service = new(api);

            int result = await service.Import(package, "Ignored", 5);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(5));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.replaceStateMatrixConfiguration), Is.True);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.linkStateMatrixTransitionGroup), Is.True);
                Assert.That(service.LastImportStatistics.AddedDerivedStates, Is.EqualTo(1));
                Assert.That(service.LastImportStatistics.AddedTransitionGroupLinks, Is.EqualTo(1));
            });
        }

        [Test]
        public void Import_RollsBackMergedDerivedStateAndEarlierLinksWhenLaterLinkFails()
        {
            RecordingWorkflowApiConnection api = new();
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases[0].DerivedStates.Add(new() { FromStateId = 2, DerivedStateId = 3 });
            package.Configuration.Phases[0].TransitionGroups.Add("Additional one");
            package.Configuration.Phases[0].TransitionGroups.Add("Additional two");
            package.TransitionGroups.Add(new() { Name = "Additional one", Transitions = [] });
            package.TransitionGroups.Add(new() { Name = "Additional two", Transitions = [] });
            StateMatrixTransitionGroup additionalOne = new() { Id = 31, Name = "Additional one", Transitions = [] };
            StateMatrixTransitionGroup additionalTwo = new() { Id = 32, Name = "Additional two", Transitions = [] };
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Existing" } });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>
            {
                TransitionGroup(30, 20), additionalOne, additionalTwo
            });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationPhase targetPhase = PhaseMapping(30);
            targetPhase.PhaseMatrixId = 101;
            targetPhase.PhaseMatrix.TransitionGroups = [];
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { targetPhase });
            api.Respond(RequestQueries.replaceStateMatrixConfiguration, new object());
            api.Respond(RequestQueries.unlinkStateMatrixTransitionGroup, new object());
            api.FailOnCall(RequestQueries.linkStateMatrixTransitionGroup, 2);
            WorkflowConfigurationTransferService service = new(api);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.Import(package, "Ignored", 5));

            Assert.Multiple(() =>
            {
                Assert.That(api.Calls.Count(call => call.Query == RequestQueries.linkStateMatrixTransitionGroup), Is.EqualTo(2));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.unlinkStateMatrixTransitionGroup), Is.True);
                Assert.That(api.Calls.Count(call => call.Query == RequestQueries.replaceStateMatrixConfiguration), Is.EqualTo(2));
            });
        }

        [Test]
        public void Import_RollsBackPreviouslyAddedPhaseWhenLaterPhaseFails()
        {
            RecordingWorkflowApiConnection api = new();
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases.Add(new()
            {
                TaskType = WfTaskType.master.ToString(),
                Phase = WorkflowPhases.approval.ToString(),
                Active = true,
                LowestInputState = 1,
                LowestStartState = 2,
                LowestEndState = 999,
                DerivedStates = [],
                TransitionGroups = []
            });
            package.States.Add(new WorkflowStateTransferData { Id = 999, Name = "Imported end state" });
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Existing" } });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { TransitionGroup(30, 20) });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>());
            api.Respond(RequestQueries.createState, new object());
            api.Respond(RequestQueries.deleteState, new object());
            api.Respond(RequestQueries.deleteWorkflowConfigurationPhase, new object());
            api.FailOnCall(RequestQueries.addWorkflowConfigurationPhase, 2);
            WorkflowConfigurationTransferService service = new(api);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.Import(package, "Ignored", 5));

            Assert.Multiple(() =>
            {
                Assert.That(api.Calls.Count(call => call.Query == RequestQueries.addWorkflowConfigurationPhase), Is.EqualTo(2));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.deleteWorkflowConfigurationPhase), Is.True);
                int deletedPhaseIndex = api.Calls.FindIndex(call => call.Query == RequestQueries.deleteWorkflowConfigurationPhase);
                int deletedStateIndex = api.Calls.FindIndex(call => call.Query == RequestQueries.deleteState);
                Assert.That(deletedPhaseIndex, Is.LessThan(deletedStateIndex));
            });
        }

        [Test]
        public async Task Import_AddsPhaseToSelectedExistingConfiguration()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Existing" } });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { TransitionGroup(30, 20) });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase>());
            api.Respond(RequestQueries.addWorkflowConfigurationPhase, new object());
            WorkflowConfigurationTransferService service = new(api);

            int result = await service.Import(Package(), "Ignored", 5);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(5));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.addWorkflowConfigurationPhase), Is.True);
                Assert.That(service.LastImportStatistics.AddedPhases, Is.EqualTo(1));
            });
        }

        [Test]
        public void Import_RejectsUnknownSelectedTarget()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Import(Package(), "Ignored", 99));

            Assert.That(exception?.Message, Does.Contain("does not exist"));
        }

        [Test]
        public void Import_RejectsDifferingExistingGroupWithoutOverwritingIt()
        {
            RecordingWorkflowApiConnection api = new();
            StateMatrixTransitionGroup conflictingGroup = TransitionGroup(30, 20);
            conflictingGroup.Description = "Local definition";
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { conflictingGroup });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Import(Package(), "Imported"));

            Assert.Multiple(() =>
            {
                Assert.That(exception?.Message, Does.Contain("different definition"));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.updateStateMatrixTransitionGroup), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowConfiguration), Is.False);
            });
        }

        [Test]
        public void Import_RollsBackNewGroupsWhenConfigurationCreationFails()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            api.Respond(RequestQueries.deleteStateMatrixTransitionGroup, new ReturnId { DeletedId = 30 });
            api.Respond(RequestQueries.deleteWorkflowVisibilityGroup, new ReturnId { DeletedId = 20 });
            WorkflowConfigurationTransferService service = new(api);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.Import(Package(), "Imported"));

            Assert.Multiple(() =>
            {
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.deleteStateMatrixTransitionGroup), Is.True);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.deleteWorkflowVisibilityGroup), Is.True);
            });
        }

        [Test]
        public void Export_ThrowsWhenReferencedTransitionGroupIsNotLoaded()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { PhaseMapping(99) });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            WorkflowConfigurationTransferService service = new(api);

            InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => service.Export(5, false));

            Assert.That(exception?.Message, Does.Contain("99"));
        }

        [Test]
        public async Task Export_WithoutVisibilityGroupReferences_ExportsEmptyVisibilityGroupList()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration> { new() { Id = 5, Name = "Shared" } });
            api.Respond(RequestQueries.getWorkflowConfigurationPhaseMappings, new List<WorkflowConfigurationPhase> { PhaseMapping(10) });
            StateMatrixTransitionGroup group = TransitionGroup(10, 20);
            group.VisibilityGroupId = null;
            group.VisibilityGroup = null;
            group.Exclusive = false;
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { group });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationTransferService service = new(api);

            WorkflowConfigurationTransferPackage package = await service.Export(5, true);

            Assert.Multiple(() =>
            {
                Assert.That(package.VisibilityGroups, Is.Empty);
                Assert.That(package.TransitionGroups[0].VisibilityGroup, Is.Null);
                Assert.That(package.TransitionGroups[0].Exclusive, Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.getWorkflowVisibilityGroups), Is.False);
            });
        }

        [Test]
        public void ValidateStructure_RejectsIncompleteOrUnsupportedPackages()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StructureError(package => package.Configuration = null!)?.Message, Does.Contain("incomplete"));
                Assert.That(StructureError(package => package.Configuration.Phases = null!)?.Message, Does.Contain("incomplete"));
                Assert.That(StructureError(package => package.Configuration.Phases[0].DerivedStates = null!)?.Message, Does.Contain("incomplete"));
                Assert.That(StructureError(package => package.TransitionGroups[0].Transitions = null!)?.Message, Does.Contain("incomplete"));
                Assert.That(StructureError(package => package.VisibilityGroups![0].Members = null!)?.Message, Does.Contain("incomplete"));
                Assert.That(StructureError(package => package.Format = "other")?.Message, Does.Contain("Unsupported"));
                Assert.That(StructureError(package => package.Version = 99)?.Message, Does.Contain("99"));
            });
        }

        [Test]
        public void ValidateStructure_RejectsInvalidGroupDefinitions()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StructureError(package => package.TransitionGroups[0].Name = " ")?.Message, Does.Contain("transition group name"));
                Assert.That(StructureError(package => package.VisibilityGroups![0].Name = "operators ")?.Message, Does.Contain("visibility group name"));
                Assert.That(StructureError(package => package.VisibilityGroups!.Add(new() { Name = "Extra", Members = [] }))?.Message,
                    Does.Contain("transition-group references"));
                Assert.That(StructureError(package => package.TransitionGroups[0].Transitions.Add(new() { FromStateId = 1, ToStateId = 2, SortOrder = 9 }))?.Message,
                    Does.Contain("duplicate transitions"));
                Assert.That(StructureError(package =>
                {
                    package.TransitionGroups[0].VisibilityGroup = null;
                    package.VisibilityGroups = null;
                })?.Message, Does.Contain("exclusive"));
                Assert.That(StructureError(package => package.TransitionGroups[0].Phase = " request")?.Message, Does.Contain("invalid phase"));
                Assert.That(StructureError(package => package.TransitionGroups[0].Phase = "bogus")?.Message, Does.Contain("invalid phase"));
                Assert.That(StructureError(package => package.VisibilityGroups![0].Members = ["cn=operators", "CN=Operators"])?.Message,
                    Does.Contain("member DN"));
                Assert.That(StructureError(package => package.VisibilityGroups![0].Members = [" cn=operators"])?.Message, Does.Contain("member DN"));
            });
        }

        [Test]
        public void ValidateStructure_RejectsInvalidPhases()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StructureError(package =>
                {
                    package.Configuration.Phases = [];
                    package.TransitionGroups = [];
                    package.VisibilityGroups = null;
                })?.Message, Does.Contain("no phases"));
                Assert.That(StructureError(package => package.Configuration.Phases[0].TaskType = "bogus")?.Message,
                    Does.Contain("unknown task type"));
                Assert.That(StructureError(package => package.Configuration.Phases[0].Phase = "request ")?.Message,
                    Does.Contain("unknown task type or phase"));
                Assert.That(StructureError(package => package.Configuration.Phases.Add(package.Configuration.Phases[0]))?.Message,
                    Does.Contain("duplicate task type"));
                Assert.That(StructureError(package => package.Configuration.Phases[0].TransitionGroups = ["Reviewers", "reviewers"])?.Message,
                    Does.Contain("duplicate transition-group"));
                Assert.That(StructureError(package => package.Configuration.Phases[0].DerivedStates.Add(new() { FromStateId = 1, DerivedStateId = 2 }))?.Message,
                    Does.Contain("derived-state"));
            });
        }

        [Test]
        public void ValidateStructure_RejectsInvalidStateDefinitions()
        {
            Assert.Multiple(() =>
            {
                Assert.That(StructureError(package => package.States[0].Id = 0)?.Message,
                    Does.Contain("invalid or duplicate workflow state"));
                Assert.That(StructureError(package => package.States.RemoveAt(0))?.Message,
                    Does.Contain("missing workflow state definitions"));
                Assert.That(StructureError(package => package.States.Add(new WorkflowStateTransferData
                {
                    Id = package.States[0].Id, Name = "Duplicate"
                }))?.Message, Does.Contain("invalid or duplicate workflow state"));
            });
        }

        [Test]
        public void Validate_RejectsEmptyOrExistingConfigurationName()
        {
            List<WorkflowConfiguration> existingConfigurations = [new() { Id = 1, Name = "Imported" }];

            Assert.Multiple(() =>
            {
                Assert.That(Assert.Throws<InvalidDataException>(() =>
                        WorkflowConfigurationTransferService.Validate(Package(), "   ", [], States()))?.Message,
                    Does.Contain("empty or already exists"));
                Assert.That(Assert.Throws<InvalidDataException>(() =>
                        WorkflowConfigurationTransferService.Validate(Package(), " imported ", existingConfigurations, States()))?.Message,
                    Does.Contain("empty or already exists"));
            });
        }

        [Test]
        public async Task Import_CreatesTransitionGroupWithoutVisibilityDataOrTransitions()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.createWorkflowConfiguration, new ReturnId { NewId = 40 });
            WorkflowConfigurationTransferPackage package = Package();
            package.VisibilityGroups = null;
            package.TransitionGroups[0].VisibilityGroup = null;
            package.TransitionGroups[0].Exclusive = false;
            package.TransitionGroups[0].Phase = null;
            package.TransitionGroups[0].Transitions = [];
            WorkflowConfigurationTransferService service = new(api);

            int result = await service.Import(package, "Imported");

            JObject groupVariables = JObject.FromObject(api.Calls.Single(call => call.Query == RequestQueries.createStateMatrixTransitionGroup).Variables!);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(40));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowVisibilityGroup), Is.False);
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.replaceStateMatrixTransitionGroupTransitions), Is.False);
                Assert.That(groupVariables["visibilityGroupId"]?.Type, Is.EqualTo(JTokenType.Null));
                Assert.That((bool?)groupVariables["exclusive"], Is.False);
            });
        }

        [Test]
        public void Import_RejectsExistingVisibilityGroupWithDifferentMembers()
        {
            RecordingWorkflowApiConnection api = new();
            WorkflowVisibilityGroup existing = VisibilityGroup(20);
            existing.Members = [new() { VisibilityGroupId = 20, MemberDn = "cn=admins" }];
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { existing });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Import(Package(), "Imported"));

            Assert.Multiple(() =>
            {
                Assert.That(exception?.Message, Does.Contain("Operators"));
                Assert.That(api.Calls.Any(call => call.Query == RequestQueries.createWorkflowVisibilityGroup), Is.False);
            });
        }

        [Test]
        public void Import_RejectsExistingTransitionGroupWithoutVisibilityGroup()
        {
            RecordingWorkflowApiConnection api = new();
            StateMatrixTransitionGroup existing = TransitionGroup(30, 20);
            existing.VisibilityGroupId = null;
            existing.VisibilityGroup = null;
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup> { existing });
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup> { VisibilityGroup(20) });
            api.Respond(RequestQueries.getStates, States());
            WorkflowConfigurationTransferService service = new(api);

            InvalidDataException? exception = Assert.ThrowsAsync<InvalidDataException>(() => service.Import(Package(), "Imported"));

            Assert.That(exception?.Message, Does.Contain("Reviewers"));
        }

        [Test]
        public void Import_WrapsRollbackFailuresInAggregateException()
        {
            RecordingWorkflowApiConnection api = EmptyImportApi();
            api.Respond(RequestQueries.createWorkflowVisibilityGroup, new ReturnId { NewId = 20 });
            api.Respond(RequestQueries.replaceWorkflowVisibilityGroupMembers, new object());
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 30 });
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            WorkflowConfigurationTransferService service = new(api);

            AggregateException? exception = Assert.ThrowsAsync<AggregateException>(() => service.Import(Package(), "Imported"));

            Assert.That(exception?.Message, Does.Contain("rollback failed"));
        }

        [Test]
        public void Validate_RejectsMissingTargetStatesAndUnknownReferences()
        {
            WorkflowConfigurationTransferPackage package = Package();
            package.Configuration.Phases[0].LowestEndState = 999;
            InvalidDataException? missingState = Assert.Throws<InvalidDataException>(() =>
                WorkflowConfigurationTransferService.Validate(package, "Imported", [], States()));

            package.Configuration.Phases[0].LowestEndState = 3;
            package.Configuration.Phases[0].TransitionGroups[0] = "Missing";
            InvalidDataException? missingGroup = Assert.Throws<InvalidDataException>(() =>
                WorkflowConfigurationTransferService.Validate(package, "Imported", [], States()));

            Assert.Multiple(() =>
            {
                Assert.That(missingState?.Message, Does.Contain("999"));
                Assert.That(missingGroup?.Message, Does.Contain("do not match"));
            });
        }

        private static InvalidDataException? StructureError(Action<WorkflowConfigurationTransferPackage> mutate)
        {
            WorkflowConfigurationTransferPackage package = Package();
            mutate(package);
            return Assert.Throws<InvalidDataException>(() => WorkflowConfigurationTransferService.ValidateStructure(package));
        }

        private static RecordingWorkflowApiConnection EmptyImportApi()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.getWorkflowConfigurations, new List<WorkflowConfiguration>());
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
            api.Respond(RequestQueries.getStates, States());
            return api;
        }

        private static WorkflowConfigurationTransferPackage Package() => new()
        {
            Configuration = new()
            {
                Name = "Shared",
                Description = "Package",
                Phases =
                [
                    new()
                    {
                        TaskType = WfTaskType.master.ToString(),
                        Phase = WorkflowPhases.request.ToString(),
                        Active = true,
                        LowestInputState = 1,
                        LowestStartState = 2,
                        LowestEndState = 3,
                        DerivedStates = [new() { FromStateId = 1, DerivedStateId = 3 }],
                        TransitionGroups = ["Reviewers"]
                    }
                ]
            },
            States =
            [
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Automatic approval", AutomaticOnly = true },
                new() { Id = 3, Name = "Closed" }
            ],
            TransitionGroups =
            [
                new()
                {
                    Name = "Reviewers",
                    Description = "Review workflow",
                    Phase = WorkflowPhases.request.ToString(),
                    Exclusive = true,
                    VisibilityGroup = "Operators",
                    Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 }]
                }
            ],
            VisibilityGroups =
            [
                new() { Name = "Operators", Description = "LDAP operators", Members = ["cn=operators"] }
            ]
        };

        private static WorkflowConfigurationPhase PhaseMapping(int transitionGroupId) => new()
        {
            TaskType = WfTaskType.master.ToString(),
            Phase = WorkflowPhases.request.ToString(),
            PhaseMatrix = new()
            {
                Active = true,
                LowestInputState = 1,
                LowestStartState = 2,
                LowestEndState = 3,
                DerivedStates = [new() { FromStateId = 1, DerivedStateId = 3 }],
                TransitionGroups = [new() { TransitionGroupId = transitionGroupId, SortOrder = 0 }]
            }
        };

        private static StateMatrixTransitionGroup TransitionGroup(int id, int visibilityGroupId) => new()
        {
            Id = id,
            Name = "Reviewers",
            Description = "Review workflow",
            Phase = WorkflowPhases.request.ToString(),
            VisibilityGroupId = visibilityGroupId,
            VisibilityGroup = new() { Id = visibilityGroupId, Name = "Operators" },
            Exclusive = true,
            Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 }]
        };

        private static WorkflowVisibilityGroup VisibilityGroup(int id) => new()
        {
            Id = id,
            Name = "Operators",
            Description = "LDAP operators",
            Members = [new() { VisibilityGroupId = id, MemberDn = "cn=operators" }]
        };

        private static List<WfState> States() =>
        [
            new() { Id = 1, Name = "Open" },
            new() { Id = 2, Name = "Automatic approval", AutomaticOnly = true },
            new() { Id = 3, Name = "Closed" }
        ];

    }
}
