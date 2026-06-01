using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsActionsTest
    {
        private sealed class SettingsActionsApiConn : SimulatedApiConnection
        {
            public string LastQuery { get; private set; } = "";
            public object? LastVariables { get; private set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                if (query == RequestQueries.updateAction)
                {
                    return Task.FromResult((T)(object)new ReturnId { UpdatedId = GetVariable<int>(variables, "id") });
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesBundleTaskType()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.BundleTasks.ToString()
            };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedBundleTaskType", BundleTaskType.TwoOutOfThree);

            bool result = await (Task<bool>)GetPrivateMethod("TryUpdateExternalParams").Invoke(component, [])!;

            Assert.That(result, Is.True);
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(action.ExternalParams, Does.Contain("bundle_type"));
            Assert.That(action.ExternalParams, Does.Contain(nameof(BundleTaskType.TwoOutOfThree)));
        }

        [Test]
        public void BundleTasksActionParams_FallsBackToDefaultForInvalidJson()
        {
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams("{invalid");

            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
        }

        [Test]
        public async Task SaveAction_UpdatesExistingActionWithBundleTaskParams()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Id = 4,
                Name = "Bundle",
                ActionType = StateActionTypes.BundleTasks.ToString(),
                Scope = WfObjectScopes.Ticket.ToString(),
                Event = StateActionEvents.OnSet.ToString()
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "actions", new List<WfStateAction> { action });
            SetMember(component, "actAction", action);
            SetMember(component, "selectedBundleTaskType", BundleTaskType.TwoOutOfThree);

            await (Task)GetPrivateMethod("SaveAction").Invoke(component, [])!;

            Assert.That(apiConn.LastQuery, Is.EqualTo(RequestQueries.updateAction));
            string externalParams = GetVariable<string>(apiConn.LastVariables, "externalParameters");
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(externalParams);
            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesFixedAutoPromoteState()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.AutoPromote.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedToState", new WfState { Id = 9, Name = "Done" });

            bool result = await InvokeTryUpdateExternalParams(component);

            Assert.That(result, Is.True);
            Assert.That(action.ExternalParams, Is.EqualTo("9"));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesConditionalAutoPromoteParams()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.AutoPromote.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedToState", new WfState { Id = -2, Name = "Conditional" });
            SetMember(component, "selectedPolicies", new List<CompliancePolicy> { new() { Id = 4 }, new() { Id = 7 } });
            SetMember(component, "selectedStateOk", new WfState { Id = 11 });
            SetMember(component, "selectedStateNotOk", new WfState { Id = 12 });
            SetMember(component, "selectedToBeCalled", ToBeCalled.PolicyCheck);
            SetMember(component, "actConditionalAutoPromoteParams", new ConditionalAutoPromoteParams { CheckResultLabel = " policy_ok " });

            bool result = await InvokeTryUpdateExternalParams(component);

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.PolicyIds, Is.EqualTo(new List<int> { 4, 7 }));
            Assert.That(parameters.CheckResultLabel, Is.EqualTo("policy_ok"));
            Assert.That(parameters.IfCompliantState, Is.EqualTo(11));
            Assert.That(parameters.IfNotCompliantState, Is.EqualTo(12));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesEmailNotificationParams()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.SendEmail.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "actActionNotificationIds", new List<int> { 5, 3, 5 });
            SetMember(component, "actAttachedContent", EmailAttachedContent.RequestedConnections);
            SetMember(component, "actConfirmSentMail", true);

            bool result = await InvokeTryUpdateExternalParams(component);

            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.NotificationIds, Is.EqualTo(new List<int> { 5, 3, 5 }));
            Assert.That(parameters.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
            Assert.That(parameters.ConfirmSentMail, Is.True);
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesUpdateModellingParams()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.UpdateModelling.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedModIntegrationState", "Implemented");
            SetMember(component, "actConfirmUpdateModelling", true);

            bool result = await InvokeTryUpdateExternalParams(component);

            UpdateModellingActionParams parameters = UpdateModellingActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(result, Is.True);
            Assert.That(parameters.ModellingState, Is.EqualTo("Implemented"));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesCreateFlowStates()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.CreateFlow.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedSuccessState", new WfState { Id = 21 });
            SetMember(component, "selectedErrorState", new WfState { Id = 22 });

            bool result = await InvokeTryUpdateExternalParams(component);

            ActionResultStateParams parameters = JsonSerializer.Deserialize<ActionResultStateParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.SuccessState, Is.EqualTo(21));
            Assert.That(parameters.ErrorState, Is.EqualTo(22));
            Assert.That(parameters.ConfirmUiMessage, Is.False);
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesCreateFlowConfirmationWithoutStates()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.CreateFlow.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "actActionResultStateParams", new ActionResultStateParams { ConfirmUiMessage = true });

            bool result = await InvokeTryUpdateExternalParams(component);

            ActionResultStateParams parameters = JsonSerializer.Deserialize<ActionResultStateParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.SuccessState, Is.Null);
            Assert.That(parameters.ErrorState, Is.Null);
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task TryUpdateExternalParams_ClearsCreateFlowParamsWhenNoStatesSelected()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.CreateFlow.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedSuccessState", null);
            SetMember(component, "selectedErrorState", null);

            bool result = await InvokeTryUpdateExternalParams(component);

            Assert.That(result, Is.True);
            Assert.That(action.ExternalParams, Is.Empty);
        }

        [Test]
        public void LoadActionExternalParams_LoadsBundleTaskType()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.BundleTasks.ToString(),
                ExternalParams = new BundleTasksActionParams { BundleType = BundleTaskType.TwoOutOfThree }.ToExternalParams()
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
        }

        [Test]
        public void LoadActionExternalParams_LoadsEmailParams()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [7, 8],
                    AttachedContent = EmailAttachedContent.RequestedConnections,
                    ConfirmSentMail = true
                })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 7, 8 }));
            Assert.That(GetMember<EmailAttachedContent>(component, "actAttachedContent"), Is.EqualTo(EmailAttachedContent.RequestedConnections));
            Assert.That(GetMember<bool>(component, "actConfirmSentMail"), Is.True);
        }

        [Test]
        public void LoadActionExternalParams_LoadsOnlyKnownModellingState()
        {
            SettingsActions component = new();
            SetMember(component, "availableModIntegrationStateNames", new List<string> { "Implemented" });
            WfStateAction knownAction = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Implemented", ConfirmUiMessage = true })
            };
            WfStateAction unknownAction = new()
            {
                ActionType = StateActionTypes.UpdateModelling.ToString(),
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Unknown", ConfirmUiMessage = true })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [knownAction]);
            string? knownState = GetMember<string?>(component, "selectedModIntegrationState");
            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [unknownAction]);

            Assert.That(knownState, Is.EqualTo("Implemented"));
            Assert.That(GetMember<string?>(component, "selectedModIntegrationState"), Is.Null);
            Assert.That(GetMember<bool>(component, "actConfirmUpdateModelling"), Is.True);
        }

        [Test]
        public void LoadActionExternalParams_LoadsEmptyCreateFlowParamsAsDefaults()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.CreateFlow.ToString(),
                ExternalParams = ""
            };
            SetMember(component, "actActionResultStateParams", new ActionResultStateParams { SuccessState = 21, ErrorState = 22 });

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            ActionResultStateParams parameters = GetMember<ActionResultStateParams>(component, "actActionResultStateParams");
            Assert.That(parameters.SuccessState, Is.Null);
            Assert.That(parameters.ErrorState, Is.Null);
            Assert.That(parameters.ConfirmUiMessage, Is.False);
            Assert.That(GetMember<WfState?>(component, "selectedSuccessState"), Is.Null);
            Assert.That(GetMember<WfState?>(component, "selectedErrorState"), Is.Null);
        }

        [Test]
        public void ActionTypeChanged_ClearsExternalParamsAndResetsSpecificState()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.BundleTasks.ToString(),
                ExternalParams = new BundleTasksActionParams { BundleType = BundleTaskType.TwoOutOfThree }.ToExternalParams()
            };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "old alert");

            GetPrivateMethod("ActionTypeChanged").Invoke(component, [StateActionTypes.SetAlert.ToString()]);

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.SetAlert.ToString()));
            Assert.That(action.ExternalParams, Is.Empty);
            Assert.That(GetMember<string>(component, "message"), Is.Empty);
            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
        }

        private static Task<bool> InvokeTryUpdateExternalParams(SettingsActions component)
        {
            return (Task<bool>)GetPrivateMethod("TryUpdateExternalParams").Invoke(component, [])!;
        }

        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(SettingsActions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(SettingsActions).FullName, name);
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

            throw new MissingFieldException(type.FullName, memberName);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingFieldException(type.FullName, memberName);
        }

        private static TValue GetVariable<TValue>(object? variables, string propertyName)
        {
            PropertyInfo? property = variables?.GetType().GetProperty(propertyName);
            return property != null ? (TValue)property.GetValue(variables)! : default!;
        }
    }
}
