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
            public int NextNewActionId { get; set; } = 42;
            public int? ForcedUpdatedId { get; set; }
            public List<string> Queries { get; } = [];
            public List<int> DeletedNotificationIds { get; } = [];

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                Queries.Add(query);
                if (query == RequestQueries.updateAction)
                {
                    return Task.FromResult((T)(object)new ReturnId { UpdatedId = ForcedUpdatedId ?? GetVariable<int>(variables, "id") });
                }
                if (query == RequestQueries.newAction)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new() { NewId = NextNewActionId }] });
                }
                if (query == RequestQueries.deleteAction)
                {
                    return Task.FromResult((T)(object)new object());
                }
                if (query == NotificationQueries.deleteNotification)
                {
                    DeletedNotificationIds.Add(GetVariable<int>(variables, "id"));
                    return Task.FromResult((T)(object)new object());
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
            Assert.That(parameters.CleanZones, Is.False);
            Assert.That(parameters.PolicyId, Is.Null);
            Assert.That(action.ExternalParams, Does.Contain("bundle_type"));
            Assert.That(action.ExternalParams, Does.Contain(nameof(BundleTaskType.TwoOutOfThree)));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesBundleCleanZoneParams()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.BundleTasks.ToString()
            };
            SetMember(component, "actAction", action);
            SetMember(component, "selectedBundleTaskType", BundleTaskType.TwoOutOfThree);
            SetMember(component, "cleanBundleZones", true);
            SetMember(component, "selectedBundlePolicy", new CompliancePolicy { Id = 12, Name = "Zone policy" });

            bool result = await (Task<bool>)GetPrivateMethod("TryUpdateExternalParams").Invoke(component, [])!;

            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(result, Is.True);
            Assert.That(parameters.CleanZones, Is.True);
            Assert.That(parameters.PolicyId, Is.EqualTo(12));
        }

        [Test]
        public void BundleTasksActionParams_FallsBackToDefaultForInvalidJson()
        {
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams("{invalid");

            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(parameters.CleanZones, Is.False);
            Assert.That(parameters.PolicyId, Is.Null);
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
        public async Task TryUpdateExternalParams_SerializesApprovalParams()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.AddApproval.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "actApprovalParams", new ApprovalParams
            {
                StateId = 17,
                ApproverGroup = "cn=approvers,dc=fwo",
                Deadline = 5
            });

            bool result = await InvokeTryUpdateExternalParams(component);

            ApprovalParams parameters = JsonSerializer.Deserialize<ApprovalParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.StateId, Is.EqualTo(17));
            Assert.That(parameters.ApproverGroup, Is.EqualTo("cn=approvers,dc=fwo"));
            Assert.That(parameters.Deadline, Is.EqualTo(5));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesAlertMessage()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.SetAlert.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "Manual review required");

            bool result = await InvokeTryUpdateExternalParams(component);

            Assert.That(result, Is.True);
            Assert.That(action.ExternalParams, Is.EqualTo("Manual review required"));
        }

        [Test]
        public async Task TryUpdateExternalParams_SerializesPathAnalysisParams()
        {
            SettingsActions component = new();
            WfStateAction action = new() { ActionType = StateActionTypes.TrafficPathAnalysis.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "actPathAnalysisParams", new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList });

            bool result = await InvokeTryUpdateExternalParams(component);

            PathAnalysisActionParams parameters = JsonSerializer.Deserialize<PathAnalysisActionParams>(action.ExternalParams)!;
            Assert.That(result, Is.True);
            Assert.That(parameters.Option, Is.EqualTo(PathAnalysisOptions.WriteToDeviceList));
        }

        [Test]
        public async Task TryUpdateExternalParams_LeavesUnknownActionParamsUnchanged()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = "UnknownAction",
                ExternalParams = "keep"
            };
            SetMember(component, "actAction", action);

            bool result = await InvokeTryUpdateExternalParams(component);

            Assert.That(result, Is.True);
            Assert.That(action.ExternalParams, Is.EqualTo("keep"));
        }

        [Test]
        public async Task TryUpdateExternalParams_RejectsEmailWithoutNotification()
        {
            SettingsActions component = new();
            string? errorMessage = null;
            WfStateAction action = new() { ActionType = StateActionTypes.SendEmail.ToString() };
            SetMember(component, "actAction", action);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((_, _, message, _) => errorMessage = message));

            bool result = await InvokeTryUpdateExternalParams(component);

            Assert.That(result, Is.False);
            Assert.That(action.ExternalParams, Is.Empty);
            Assert.That(errorMessage, Is.EqualTo(new SimulatedUserConfig().GetText("E4011")));
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
            List<CompliancePolicy> policies = [new() { Id = 12, Name = "Zone policy" }];
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.BundleTasks.ToString(),
                ExternalParams = new BundleTasksActionParams { BundleType = BundleTaskType.TwoOutOfThree, CleanZones = true, PolicyId = 12 }.ToExternalParams()
            };
            SetMember(component, "policies", policies);

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(GetMember<bool>(component, "cleanBundleZones"), Is.True);
            Assert.That(GetMember<CompliancePolicy?>(component, "selectedBundlePolicy"), Is.SameAs(policies[0]));
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
        public void LoadActionExternalParams_LoadsApprovalParams()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.AddApproval.ToString(),
                ExternalParams = JsonSerializer.Serialize(new ApprovalParams { StateId = 9, ApproverGroup = "cn=group", Deadline = 3 })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            ApprovalParams parameters = GetMember<ApprovalParams>(component, "actApprovalParams");
            Assert.That(parameters.StateId, Is.EqualTo(9));
            Assert.That(parameters.ApproverGroup, Is.EqualTo("cn=group"));
            Assert.That(parameters.Deadline, Is.EqualTo(3));
        }

        [Test]
        public void LoadActionExternalParams_LoadsAlertMessage()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "watch this"
            };
            SetMember(component, "actAction", action);

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            Assert.That(GetMember<string>(component, "message"), Is.EqualTo("watch this"));
        }

        [Test]
        public void LoadActionExternalParams_LoadsPathAnalysisParams()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.TrafficPathAnalysis.ToString(),
                ExternalParams = JsonSerializer.Serialize(new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            PathAnalysisActionParams parameters = GetMember<PathAnalysisActionParams>(component, "actPathAnalysisParams");
            Assert.That(parameters.Option, Is.EqualTo(PathAnalysisOptions.WriteToDeviceList));
        }

        [Test]
        public void LoadActionExternalParams_LoadsAutoPromoteParams()
        {
            SettingsActions component = new();
            List<CompliancePolicy> policies = [new() { Id = 4 }, new() { Id = 8 }];
            SetMember(component, "policies", policies);
            WfStateAction fixedStateAction = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = "23"
            };
            WfStateAction conditionalAction = new()
            {
                ActionType = StateActionTypes.AutoPromote.ToString(),
                ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                {
                    ToBeCalled = ToBeCalled.PolicyCheck,
                    PolicyIds = [8],
                    IfCompliantState = 11,
                    IfNotCompliantState = 12
                })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [fixedStateAction]);
            int parsedToState = GetMember<int>(component, "toState");
            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [conditionalAction]);

            Assert.That(parsedToState, Is.EqualTo(23));
            Assert.That(GetMember<WfState>(component, "selectedToState").Id, Is.EqualTo(-2));
            Assert.That(GetMember<IEnumerable<CompliancePolicy>>(component, "selectedPolicies").Single(), Is.SameAs(policies[1]));
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
        public void LoadActionExternalParams_LoadsCreateFlowParams()
        {
            SettingsActions component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.CreateFlow.ToString(),
                ExternalParams = JsonSerializer.Serialize(new ActionResultStateParams
                {
                    SuccessState = 21,
                    ErrorState = 22,
                    ConfirmUiMessage = true
                })
            };

            GetPrivateMethod("LoadActionExternalParams").Invoke(component, [action]);

            ActionResultStateParams parameters = GetMember<ActionResultStateParams>(component, "actActionResultStateParams");
            Assert.That(parameters.SuccessState, Is.EqualTo(21));
            Assert.That(parameters.ErrorState, Is.EqualTo(22));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public void EditAction_ResolvesSelectionsAndSnapshotsPersistedAction()
        {
            SettingsActions component = new();
            WfState approvalState = new() { Id = 31, Name = "Approve" };
            UiUser approver = new() { Dn = "cn=approver,dc=fwo" };
            WfStateAction action = new()
            {
                Id = 14,
                Name = "Approval",
                ActionType = StateActionTypes.AddApproval.ToString(),
                Phase = WorkflowPhases.request.ToString(),
                TaskType = WfTaskType.access.ToString(),
                ExternalParams = JsonSerializer.Serialize(new ApprovalParams
                {
                    StateId = approvalState.Id,
                    ApproverGroup = "CN=APPROVER,DC=FWO"
                })
            };
            SetMember(component, "states", new List<WfState> { approvalState });
            SetMember(component, "statesPlus", new List<WfState> { new() { Id = -1 }, new() { Id = -2 }, approvalState });
            SetMember(component, "userAndGroupList", new List<UiUser> { approver });

            GetPrivateMethod("EditAction").Invoke(component, [action]);

            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.True);
            Assert.That(GetMember<string?>(component, "selectedPhase"), Is.EqualTo(WorkflowPhases.request.ToString()));
            Assert.That(GetMember<WfTaskType?>(component, "selectedTaskType"), Is.EqualTo(WfTaskType.access));
            Assert.That(GetMember<WfState>(component, "selectedState"), Is.SameAs(approvalState));
            Assert.That(GetMember<UiUser?>(component, "selectedUserGroup"), Is.SameAs(approver));
            Assert.That(GetMember<WfStateAction>(component, "persistedActionSnapshot"), Is.Not.SameAs(action));
            Assert.That(GetMember<WfStateAction>(component, "persistedActionSnapshot").Id, Is.EqualTo(action.Id));
        }

        [Test]
        public void AddAction_EntersAddAndEditModeWithNewAction()
        {
            SettingsActions component = new();

            GetPrivateMethod("AddAction").Invoke(component, []);

            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.True);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.True);
            Assert.That(GetMember<WfStateAction>(component, "actAction").Id, Is.EqualTo(0));
        }

        [Test]
        public async Task SaveAction_CreatesNewActionAndStoresReturnedId()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new() { NextNewActionId = 77 };
            WfStateAction action = new()
            {
                Name = "Alert",
                ActionType = StateActionTypes.SetAlert.ToString(),
                Scope = WfObjectScopes.Ticket.ToString(),
                Event = StateActionEvents.OnSet.ToString()
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "actions", new List<WfStateAction>());
            SetMember(component, "actAction", action);
            SetMember(component, "message", "new alert");
            SetMember(component, "AddActionMode", true);
            SetMember(component, "EditActionMode", true);

            await (Task)GetPrivateMethod("SaveAction").Invoke(component, [])!;

            Assert.That(apiConn.LastQuery, Is.EqualTo(RequestQueries.newAction));
            Assert.That(action.Id, Is.EqualTo(77));
            Assert.That(GetMember<List<WfStateAction>>(component, "actions"), Has.Count.EqualTo(1));
            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.False);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
            Assert.That(GetVariable<string>(apiConn.LastVariables, "externalParameters"), Is.EqualTo("new alert"));
        }

        [Test]
        public async Task SetActionNotificationIds_PersistsForExistingEmailAction()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Id = 19,
                Name = "Mail",
                ActionType = StateActionTypes.SendEmail.ToString(),
                Scope = WfObjectScopes.Ticket.ToString(),
                Event = StateActionEvents.OnSet.ToString()
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "actAction", action);
            SetMember(component, "persistedActionSnapshot", new WfStateAction(action));
            SetMember(component, "actAttachedContent", EmailAttachedContent.RequestedConnections);

            await (Task)GetPrivateMethod("SetActionNotificationIds").Invoke(component, [new List<int> { 3, 3, 8 }])!;

            Assert.That(apiConn.LastQuery, Is.EqualTo(RequestQueries.updateAction));
            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 3, 8 }));
            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.NotificationIds, Is.EqualTo(new List<int> { 3, 8 }));
            Assert.That(parameters.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
        }

        [Test]
        public async Task SetActionNotificationIds_DoesNotPersistForNewAction()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "actAction", new WfStateAction { Id = 19, ActionType = StateActionTypes.SendEmail.ToString() });
            SetMember(component, "AddActionMode", true);

            await (Task)GetPrivateMethod("SetActionNotificationIds").Invoke(component, [new List<int> { 3 }])!;

            Assert.That(apiConn.Queries, Is.Empty);
            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 3 }));
        }

        [Test]
        public async Task Cancel_DeletesTemporaryNotificationsOnlyInAddMode()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "actActionNotificationIds", new List<int> { 5, 5, 0, 7 });
            SetMember(component, "AddActionMode", true);
            SetMember(component, "EditActionMode", true);
            SetMember(component, "DeleteActionMode", true);

            await (Task)GetPrivateMethod("Cancel").Invoke(component, [])!;

            Assert.That(apiConn.DeletedNotificationIds, Is.EqualTo(new List<int> { 5, 7 }));
            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.False);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
            Assert.That(GetMember<bool>(component, "DeleteActionMode"), Is.False);
        }

        [Test]
        public void ActionNotificationIds_ReturnsDistinctPositiveIdsForEmailActions()
        {
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams { NotificationIds = [3, 0, 3, 5] })
            };

            List<int> ids = (List<int>)GetPrivateMethod("ActionNotificationIds").Invoke(null, [action])!;

            Assert.That(ids, Is.EqualTo(new List<int> { 3, 5 }));
        }

        [Test]
        public void ActionNotificationIds_IgnoresNonEmailAndInvalidJson()
        {
            WfStateAction nonEmailAction = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams { NotificationIds = [3] })
            };
            WfStateAction invalidEmailAction = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = "{invalid"
            };

            List<int> nonEmailIds = (List<int>)GetPrivateMethod("ActionNotificationIds").Invoke(null, [nonEmailAction])!;
            List<int> invalidEmailIds = (List<int>)GetPrivateMethod("ActionNotificationIds").Invoke(null, [invalidEmailAction])!;

            Assert.That(nonEmailIds, Is.Empty);
            Assert.That(invalidEmailIds, Is.Empty);
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
            return typeof(SettingsActions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
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
