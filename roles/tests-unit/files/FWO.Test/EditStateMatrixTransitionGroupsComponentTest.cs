using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    internal class EditStateMatrixTransitionGroupsComponentTest
    {
        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase("  request  ", "request")]
        public void NormalizeOptional_TrimsOrReturnsNull(string? input, string? expected)
        {
            Assert.That(InvokeStatic("NormalizeOptional", input), Is.EqualTo(expected));
        }

        [Test]
        public void Clone_DeepCopiesTransitionsAndPreservesMetadata()
        {
            StateMatrixTransitionGroup source = new()
            {
                Id = 5,
                Name = "Approval",
                Description = "Description",
                Phase = "approval",
                VisibilityGroupId = 9,
                Exclusive = true,
                PhaseMatrixUsages = [new() { PhaseMatrixId = 3, SortOrder = 2 }],
                Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 4 }]
            };

            StateMatrixTransitionGroup clone = (StateMatrixTransitionGroup)InvokeStatic("Clone", source)!;
            clone.Transitions[0].ToStateId = 8;
            clone.Transitions.Add(new() { FromStateId = 8, ToStateId = 9 });

            Assert.Multiple(() =>
            {
                Assert.That(clone.Id, Is.EqualTo(5));
                Assert.That(clone.Phase, Is.EqualTo("approval"));
                Assert.That(clone.VisibilityGroupId, Is.EqualTo(9));
                Assert.That(clone.Exclusive, Is.True);
                Assert.That(clone.PhaseMatrixUsages, Is.SameAs(source.PhaseMatrixUsages));
                Assert.That(source.Transitions, Has.Count.EqualTo(1));
                Assert.That(source.Transitions[0].ToStateId, Is.EqualTo(2));
            });
        }

        [Test]
        public void CanSave_ValidatesNameUniquenessAndTransitionUniqueness()
        {
            EditStateMatrixTransitionGroups component = new();
            SetField(component, "transitionGroups", new List<StateMatrixTransitionGroup>
            {
                new() { Id = 1, Name = "Existing" },
                new() { Id = 2, Name = "Current" }
            });
            StateMatrixTransitionGroup edit = new() { Id = 2, Name = " " };
            SetField(component, "editGroup", edit);
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            edit.Name = " existing ";
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            edit.Name = "Current";
            edit.Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2 },
                new() { FromStateId = 1, ToStateId = 2 }
            ];
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.False);

            edit.Transitions.RemoveAt(1);
            Assert.That(GetProperty<bool>(component, "CanSave"), Is.True);
        }

        [Test]
        public void AddTransition_UsesFirstAvailableStateForBothEnds()
        {
            EditStateMatrixTransitionGroups component = new();
            SetProperty(component, nameof(EditStateMatrixTransitionGroups.States), new List<WfState>
            {
                new() { Id = 6, Name = "First" },
                new() { Id = 7, Name = "Second" }
            });

            Invoke(component, "AddTransition");

            StateMatrixTransition transition = GetField<StateMatrixTransitionGroup>(component, "editGroup").Transitions.Single();
            Assert.Multiple(() =>
            {
                Assert.That(transition.FromStateId, Is.EqualTo(6));
                Assert.That(transition.ToStateId, Is.EqualTo(6));
            });
        }

        [Test]
        public void AddAndEditGroup_InitializeIndependentEditorState()
        {
            EditStateMatrixTransitionGroups component = new();
            Invoke(component, "AddGroup");
            Assert.That(GetField<bool>(component, "EditMode"), Is.True);

            StateMatrixTransitionGroup source = new()
            {
                Id = 8,
                Name = "Source",
                Transitions = [new() { FromStateId = 1, ToStateId = 2 }]
            };
            Invoke(component, "EditGroup", source);
            GetField<StateMatrixTransitionGroup>(component, "editGroup").Transitions[0].ToStateId = 3;

            Assert.Multiple(() =>
            {
                Assert.That(source.Transitions[0].ToStateId, Is.EqualTo(2));
                Assert.That(GetField<StateMatrixTransitionGroup>(component, "originalGroup").Transitions[0].ToStateId, Is.EqualTo(2));
            });
        }

        [Test]
        public void StateDisplayName_ContainsNameAndId()
        {
            Assert.That(InvokeStatic("StateDisplayName", new WfState { Id = 42, Name = "Done" }), Is.EqualTo("Done (42)"));
        }

        [Test]
        public void VisibilityGroupName_ResolvesKnownAndUnknownIds()
        {
            EditStateMatrixTransitionGroups component = new();
            SetField(component, "visibilityGroups", new List<WorkflowVisibilityGroup> { new() { Id = 3, Name = "Visible" } });

            Assert.Multiple(() =>
            {
                Assert.That(Invoke(component, "VisibilityGroupName", 3), Is.EqualTo("Visible"));
                Assert.That(Invoke(component, "VisibilityGroupName", 99), Is.EqualTo("-"));
                Assert.That(Invoke(component, "VisibilityGroupName", new object?[] { null }), Is.EqualTo("-"));
            });
        }

        [Test]
        public async Task SaveTransitions_SendsRemovalsInsertionsAndSortOrderUpdates()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.replaceStateMatrixTransitionGroupTransitions, new object());
            EditStateMatrixTransitionGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "originalGroup", new StateMatrixTransitionGroup
            {
                Transitions =
                [
                    new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 },
                    new() { FromStateId = 2, ToStateId = 3, SortOrder = 1 }
                ]
            });
            SetField(component, "editGroup", new StateMatrixTransitionGroup
            {
                Transitions =
                [
                    new() { FromStateId = 3, ToStateId = 4 },
                    new() { FromStateId = 1, ToStateId = 2 }
                ]
            });

            await InvokeAsync(component, "SaveTransitions", 12);

            JObject variables = JObject.FromObject(api.Calls.Single().Variables!);
            Assert.Multiple(() =>
            {
                Assert.That(variables["removedTransitions"], Has.Count.EqualTo(1));
                Assert.That((int?)variables["removedTransitions"]?[0]?["from_state_id"]?["_eq"], Is.EqualTo(2));
                Assert.That(variables["transitions"], Has.Count.EqualTo(2));
                Assert.That((int?)variables["transitions"]?[0]?["from_state_id"], Is.EqualTo(3));
                Assert.That((int?)variables["transitions"]?[0]?["sort_order"], Is.EqualTo(0));
                Assert.That((int?)variables["transitions"]?[1]?["from_state_id"], Is.EqualTo(1));
                Assert.That((int?)variables["transitions"]?[1]?["sort_order"], Is.EqualTo(1));
            });
        }

        [Test]
        public async Task SaveTransitions_DoesNotCallApiWhenNothingChanged()
        {
            RecordingWorkflowApiConnection api = new();
            StateMatrixTransitionGroup group = new()
            {
                Transitions = [new() { FromStateId = 1, ToStateId = 2, SortOrder = 0 }]
            };
            EditStateMatrixTransitionGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "originalGroup", (StateMatrixTransitionGroup)InvokeStatic("Clone", group)!);
            SetField(component, "editGroup", (StateMatrixTransitionGroup)InvokeStatic("Clone", group)!);

            await InvokeAsync(component, "SaveTransitions", 4);

            Assert.That(api.Calls, Is.Empty);
        }

        [TestCase(0, false)]
        [TestCase(15, true)]
        public async Task Save_UsesCorrectCreateOrUpdateVariableShape(int groupId, bool expectsId)
        {
            RecordingWorkflowApiConnection api = new();
            string mutation = groupId == 0 ? RequestQueries.createStateMatrixTransitionGroup : RequestQueries.updateStateMatrixTransitionGroup;
            api.Respond(mutation, new ReturnId { NewId = 15, UpdatedId = groupId });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
            EditStateMatrixTransitionGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "editGroup", new StateMatrixTransitionGroup
            {
                Id = groupId,
                Name = "  Group  ",
                Description = "   ",
                Phase = " request ",
                VisibilityGroupId = 6,
                Exclusive = true
            });
            SetField(component, "originalGroup", new StateMatrixTransitionGroup { Id = groupId });

            await InvokeAsync(component, "Save");

            JObject variables = JObject.FromObject(api.Calls.First(call => call.Query == mutation).Variables!);
            Assert.Multiple(() =>
            {
                Assert.That((string?)variables["name"], Is.EqualTo("Group"));
                Assert.That(variables["description"]?.Type, Is.EqualTo(JTokenType.Null));
                Assert.That((string?)variables["phase"], Is.EqualTo("request"));
                Assert.That((int?)variables["visibilityGroupId"], Is.EqualTo(6));
                Assert.That((bool?)variables["exclusive"], Is.True);
                Assert.That(variables["id"] != null, Is.EqualTo(expectsId));
            });
        }

        [Test]
        public async Task Save_DisablesExclusiveWhenVisibilityGroupMissing()
        {
            RecordingWorkflowApiConnection api = new();
            api.Respond(RequestQueries.createStateMatrixTransitionGroup, new ReturnId { NewId = 15 });
            api.Respond(RequestQueries.getStateMatrixTransitionGroups, new List<StateMatrixTransitionGroup>());
            api.Respond(RequestQueries.getWorkflowVisibilityGroups, new List<WorkflowVisibilityGroup>());
            EditStateMatrixTransitionGroups component = new();
            SetProperty(component, "apiConnection", api);
            SetField(component, "editGroup", new StateMatrixTransitionGroup
            {
                Name = "Group",
                Phase = "request",
                VisibilityGroupId = null,
                Exclusive = true
            });
            SetField(component, "originalGroup", new StateMatrixTransitionGroup());

            await InvokeAsync(component, "Save");

            JObject variables = JObject.FromObject(api.Calls.First(call => call.Query == RequestQueries.createStateMatrixTransitionGroup).Variables!);
            Assert.That((bool?)variables["exclusive"], Is.False);
        }

        [Test]
        public void CloseEditor_ClearsEditMode()
        {
            EditStateMatrixTransitionGroups component = new();
            SetField(component, "EditMode", true);

            Invoke(component, "CloseEditor");

            Assert.That(GetField<bool>(component, "EditMode"), Is.False);
        }

        private static object? InvokeStatic(string methodName, params object?[] parameters)
        {
            MethodInfo method = typeof(EditStateMatrixTransitionGroups).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(EditStateMatrixTransitionGroups).FullName, methodName);
            return method.Invoke(null, parameters);
        }
    }
}
