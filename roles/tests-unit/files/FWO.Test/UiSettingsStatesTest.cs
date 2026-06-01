using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsStatesTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsStates).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsStates).FullName, name);
        }

        private static void SetPrivateField<T>(SettingsStates component, string fieldName, T value)
        {
            FieldInfo? field = typeof(SettingsStates).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsStates).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(SettingsStates component, string fieldName)
        {
            FieldInfo? field = typeof(SettingsStates).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(SettingsStates).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static void SetInjectedApiConnection(SettingsStates component, ApiConnection apiConnection)
        {
            PropertyInfo? prop = typeof(SettingsStates).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(property => property.PropertyType == typeof(ApiConnection));
            if (prop == null)
            {
                throw new MissingMemberException(typeof(SettingsStates).FullName, "apiConnection");
            }
            prop.SetValue(component, apiConnection);
        }

        private static T GetVariable<T>(object variables, string name)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            if (property == null)
            {
                throw new MissingMemberException(variables.GetType().FullName, name);
            }
            return (T)property.GetValue(variables)!;
        }

        private static WfStateActionDataHelper StateAction(int actionId, int sortOrder)
        {
            return new WfStateActionDataHelper
            {
                SortOrder = sortOrder,
                Action = new WfStateAction { Id = actionId, Name = $"Action {actionId}" }
            };
        }

        [Test]
        public void AddState_SelectsFirstFreeStateId_AndEntersAddMode()
        {
            SettingsStates component = new();
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 0, Name = "Open" },
                new() { Id = 2, Name = "Done" }
            });

            GetPrivateMethod("AddState").Invoke(component, null);

            WfState actState = GetPrivateField<WfState>(component, "actState");
            Assert.Multiple(() =>
            {
                Assert.That(actState.Id, Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.True);
            });
        }

        [Test]
        public async Task SaveState_InAddMode_UpsertsStateAddsActionsAndNormalizesOrder()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfState addedState = new()
            {
                Id = 3,
                Name = "Review",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(20, 50),
                    StateAction(10, 40)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            List<WfState> states = GetPrivateField<List<WfState>>(component, "states");
            Assert.Multiple(() =>
            {
                Assert.That(states.Select(state => state.Id).ToList(), Is.EqualTo(new List<int> { 3, 5 }));
                Assert.That(addedState.Actions.Select(action => action.SortOrder).ToList(), Is.EqualTo(new List<int> { 1, 2 }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.upsertState,
                    RequestQueries.addStateAction,
                    RequestQueries.addStateAction
                }));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "id"), Is.EqualTo(3));
                Assert.That(GetVariable<string>(apiConn.Variables[0], "name"), Is.EqualTo("Review"));
                Assert.That(GetVariable<bool>(apiConn.Variables[0], "automaticOnly"), Is.True);
                Assert.That(GetVariable<int>(apiConn.Variables[1], "actionId"), Is.EqualTo(20));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "sortOrder"), Is.EqualTo(1));
                Assert.That(GetVariable<int>(apiConn.Variables[2], "actionId"), Is.EqualTo(10));
                Assert.That(GetVariable<int>(apiConn.Variables[2], "sortOrder"), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task AddActionToState_ForExistingState_SendsMutationAndAppendsWithNextSortOrder()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfState actState = new()
            {
                Id = 4,
                Actions = [StateAction(10, 1)]
            };
            SetInjectedApiConnection(component, apiConn);
            SetPrivateField(component, "actState", actState);
            SetPrivateField(component, "selectedAction", new WfStateAction { Id = 30, Name = "Notify" });
            SetPrivateField(component, "AddStateMode", false);
            SetPrivateField(component, "SelectActionMode", true);

            Task task = (Task)GetPrivateMethod("AddActionToState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 10, 30 }));
                Assert.That(actState.Actions.Select(action => action.SortOrder).ToList(), Is.EqualTo(new List<int> { 1, 2 }));
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.False);
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.addStateAction }));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "stateId"), Is.EqualTo(4));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "actionId"), Is.EqualTo(30));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "sortOrder"), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task RemoveActionFromState_ForExistingState_RemovesActionAndPersistsRemainingOrder()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfStateActionDataHelper first = StateAction(10, 1);
            WfStateActionDataHelper second = StateAction(20, 2);
            WfStateActionDataHelper third = StateAction(30, 3);
            WfState actState = new()
            {
                Id = 6,
                Actions = [first, second, third]
            };
            SetInjectedApiConnection(component, apiConn);
            SetPrivateField(component, "actState", actState);
            SetPrivateField(component, "AddStateMode", false);

            Task task = (Task)GetPrivateMethod("RemoveActionFromState").Invoke(component, [first])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 20, 30 }));
                Assert.That(actState.Actions.Select(action => action.SortOrder).ToList(), Is.EqualTo(new List<int> { 1, 2 }));
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.removeStateAction,
                    RequestQueries.updateStateActionSortOrder,
                    RequestQueries.updateStateActionSortOrder
                }));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "actionId"), Is.EqualTo(10));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "actionId"), Is.EqualTo(20));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "sortOrder"), Is.EqualTo(1));
                Assert.That(GetVariable<int>(apiConn.Variables[2], "actionId"), Is.EqualTo(30));
                Assert.That(GetVariable<int>(apiConn.Variables[2], "sortOrder"), Is.EqualTo(2));
            });
        }

        [Test]
        public async Task MoveActionInState_SwapsActionsAndPersistsChangedRowsOnly()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfStateActionDataHelper first = StateAction(10, 1);
            WfStateActionDataHelper second = StateAction(20, 2);
            WfStateActionDataHelper third = StateAction(30, 3);
            WfState actState = new()
            {
                Id = 7,
                Actions = [first, second, third]
            };
            SetInjectedApiConnection(component, apiConn);
            SetPrivateField(component, "actState", actState);
            SetPrivateField(component, "AddStateMode", false);

            Task task = (Task)GetPrivateMethod("MoveActionInState").Invoke(component, [second, -1])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 20, 10, 30 }));
                Assert.That(actState.Actions.Select(action => action.SortOrder).ToList(), Is.EqualTo(new List<int> { 1, 2, 3 }));
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.updateStateActionSortOrder,
                    RequestQueries.updateStateActionSortOrder
                }));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "actionId"), Is.EqualTo(10));
                Assert.That(GetVariable<int>(apiConn.Variables[0], "sortOrder"), Is.EqualTo(2));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "actionId"), Is.EqualTo(20));
                Assert.That(GetVariable<int>(apiConn.Variables[1], "sortOrder"), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task MoveActionInState_IgnoresOutOfRangeMove()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfStateActionDataHelper first = StateAction(10, 1);
            WfState actState = new()
            {
                Id = 8,
                Actions = [first]
            };
            SetInjectedApiConnection(component, apiConn);
            SetPrivateField(component, "actState", actState);

            Task task = (Task)GetPrivateMethod("MoveActionInState").Invoke(component, [first, -1])!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 10 }));
                Assert.That(apiConn.Queries, Is.Empty);
            });
        }
    }

    internal sealed class SettingsStatesTestApiConn : SimulatedApiConnection
    {
        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            return Task.FromResult(default(QueryResponseType)!);
        }
    }
}
