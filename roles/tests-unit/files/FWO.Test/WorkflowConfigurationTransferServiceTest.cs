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
                Assert.That(package.TransitionGroups[0].VisibilityGroup, Is.EqualTo(includeVisibilityGroups ? "Operators" : null));
                Assert.That(package.TransitionGroups[0].Exclusive, Is.EqualTo(includeVisibilityGroups));
                Assert.That(package.VisibilityGroups?.Single().Members, Is.EqualTo(includeVisibilityGroups ? kExpectedVisibilityGroupMembers : null));
            });
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
                Assert.That(json, Does.Contain("\"visibility_groups\""));
                Assert.That(result.Configuration.Phases[0].DerivedStates[0].DerivedStateId, Is.EqualTo(3));
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

        private static List<WfState> States() => [new() { Id = 1 }, new() { Id = 2 }, new() { Id = 3 }];
    }
}
