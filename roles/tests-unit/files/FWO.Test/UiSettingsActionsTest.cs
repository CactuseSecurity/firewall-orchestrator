using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsActionsTest
    {
        private static readonly int[] ExpectedAvailableStateLinkIds = [14];

        private sealed class SettingsActionsApiConn : SimulatedApiConnection
        {
            public string LastQuery { get; private set; } = "";
            public object? LastVariables { get; private set; }
            public int NextNewActionId { get; set; } = 42;
            public int NextNewNotificationId { get; set; } = 101;
            public int? ForcedUpdatedId { get; set; }
            public List<WfStateAction> InitialActions { get; set; } = [];
            public List<WfState> InitialStates { get; set; } = [];
            public List<CompliancePolicy> InitialPolicies { get; set; } = [];
            public List<string> Queries { get; } = [];
            public List<int> DeletedNotificationIds { get; } = [];
            public bool ReturnNullNewActionIds { get; set; }

            public override Task<T> SendQueryAsync<T>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                Queries.Add(query);
                if (query == RequestQueries.getActions)
                {
                    return Task.FromResult((T)(object)InitialActions);
                }
                if (query == RequestQueries.getStates)
                {
                    return Task.FromResult((T)(object)InitialStates);
                }
                if (query == ComplianceQueries.getPolicies)
                {
                    return Task.FromResult((T)(object)InitialPolicies);
                }
                if (query == RequestQueries.updateAction)
                {
                    return Task.FromResult((T)(object)new ReturnId { UpdatedId = ForcedUpdatedId ?? GetVariable<int>(variables, "id") });
                }
                if (query == RequestQueries.newAction)
                {
                    if (ReturnNullNewActionIds)
                    {
                        return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = null! });
                    }
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new() { NewId = NextNewActionId }] });
                }
                if (query == RequestQueries.addStateAction || query == RequestQueries.removeStateAction || query == RequestQueries.updateStateActionSortOrder)
                {
                    return Task.FromResult((T)(object)new object());
                }
                if (query == NotificationQueries.addNotification)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new() { NewId = NextNewNotificationId }] });
                }
                if (query == NotificationQueries.updateNotification)
                {
                    return Task.FromResult((T)(object)new ReturnIdWrapper { ReturnIds = [new() { NewId = GetVariable<int>(variables, "id") }] });
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
        public async Task BundleTasks_UpdateExternalParams_SerializesSelectedValues()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedBundleTaskType", BundleTaskType.TwoOutOfThree);
            SetMember(component, "cleanBundleZones", true);
            SetMember(component, "selectedBundlePolicy", new CompliancePolicy { Id = 12, Name = "Zone policy" });

            await InvokeAsync(component, "UpdateExternalParams");

            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(parameters.CleanZones, Is.True);
            Assert.That(parameters.PolicyId, Is.EqualTo(12));
        }

        [Test]
        public async Task BundleTasks_OnParametersSet_LoadsExternalParams()
        {
            EditActionBundleTasks component = new();
            List<CompliancePolicy> policies = [new() { Id = 12, Name = "Zone policy" }];
            WfStateAction action = new()
            {
                ExternalParams = new BundleTasksActionParams { BundleType = BundleTaskType.TwoOutOfThree, CleanZones = true, PolicyId = 12 }.ToExternalParams()
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "Policies", policies);

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(GetMember<bool>(component, "cleanBundleZones"), Is.True);
            Assert.That(GetMember<CompliancePolicy?>(component, "selectedBundlePolicy"), Is.SameAs(policies[0]));
        }

        [Test]
        public async Task BundleTasks_OnParametersSet_ResetsLocalStateWhenNoExternalParams()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new()
            {
                ExternalParams = ""
            };
            CompliancePolicy existingPolicy = new() { Id = 9, Name = "Old policy" };
            SetMember(component, "ActAction", action);
            SetMember(component, "Policies", new List<CompliancePolicy> { existingPolicy });
            SetMember(component, "selectedBundleTaskType", BundleTaskType.TwoOutOfThree);
            SetMember(component, "cleanBundleZones", true);
            SetMember(component, "selectedBundlePolicy", existingPolicy);

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(GetMember<bool>(component, "cleanBundleZones"), Is.False);
            Assert.That(GetMember<CompliancePolicy?>(component, "selectedBundlePolicy"), Is.Null);
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(parameters.CleanZones, Is.False);
            Assert.That(parameters.PolicyId, Is.Null);
        }

        [Test]
        public async Task BundleTasks_OnParametersSet_WhenPolicyIsMissing_KeepsSelectionNull()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new()
            {
                ExternalParams = new BundleTasksActionParams
                {
                    BundleType = BundleTaskType.TwoOutOfThree,
                    CleanZones = true,
                    PolicyId = 77
                }.ToExternalParams()
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "Policies", new List<CompliancePolicy> { new() { Id = 12, Name = "Zone policy" } });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
            Assert.That(GetMember<bool>(component, "cleanBundleZones"), Is.True);
            Assert.That(GetMember<CompliancePolicy?>(component, "selectedBundlePolicy"), Is.Null);
        }

        [Test]
        public async Task BundleTasks_OnBundleTypeChanged_UpdatesExternalParams()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnBundleTypeChanged", BundleTaskType.TwoOutOfThree);

            Assert.That(GetMember<BundleTaskType>(component, "selectedBundleTaskType"), Is.EqualTo(BundleTaskType.TwoOutOfThree));
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.BundleType, Is.EqualTo(BundleTaskType.TwoOutOfThree));
        }

        [Test]
        public async Task BundleTasks_OnCleanZonesChanged_UpdatesExternalParams()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnCleanZonesChanged", new ChangeEventArgs { Value = "true" });

            Assert.That(GetMember<bool>(component, "cleanBundleZones"), Is.True);
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.CleanZones, Is.True);
        }

        [Test]
        public async Task BundleTasks_OnPolicyChanged_NullClearsPolicyAndUpdatesParams()
        {
            EditActionBundleTasks component = new();
            WfStateAction action = new();
            CompliancePolicy selectedPolicy = new() { Id = 12, Name = "Zone policy" };
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedBundlePolicy", selectedPolicy);

            await InvokeAsync(component, "OnPolicyChanged", new object?[] { null });

            Assert.That(GetMember<CompliancePolicy?>(component, "selectedBundlePolicy"), Is.Null);
            BundleTasksActionParams parameters = BundleTasksActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.PolicyId, Is.Null);
        }

        [Test]
        public async Task AutoPromote_UpdateExternalParams_SerializesFixedState()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = 9, Name = "Done" });

            await InvokeAsync(component, "UpdateExternalParams");

            Assert.That(action.ExternalParams, Is.EqualTo("9"));
        }

        [Test]
        public async Task AutoPromote_UpdateExternalParams_SerializesConditionalParams()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = -2, Name = "Conditional" });
            SetMember(component, "selectedPolicies", new List<CompliancePolicy> { new() { Id = 4 }, new() { Id = 7 } });
            SetMember(component, "selectedStateOk", new WfState { Id = 11 });
            SetMember(component, "selectedStateNotOk", new WfState { Id = 12 });
            SetMember(component, "selectedToBeCalled", ToBeCalled.PolicyCheck);
            SetMember(component, "actConditionalAutoPromoteParams", new ConditionalAutoPromoteParams { CheckResultLabel = " policy_ok " });

            await InvokeAsync(component, "UpdateExternalParams");

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.That(parameters.PolicyIds, Is.EqualTo(new List<int> { 4, 7 }));
            Assert.That(parameters.CheckResultLabel, Is.EqualTo("policy_ok"));
            Assert.That(parameters.IfCompliantState, Is.EqualTo(11));
            Assert.That(parameters.IfNotCompliantState, Is.EqualTo(12));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_LoadsExternalParams()
        {
            EditActionAutoPromote component = new();
            List<CompliancePolicy> policies = [new() { Id = 4 }, new() { Id = 8 }];
            List<WfState> states = [new() { Id = 11, Name = "Compliant" }, new() { Id = 12, Name = "Not compliant" }];
            SetMember(component, "Policies", policies);
            SetMember(component, "States", states);
            SetMember(component, "ActAction", new WfStateAction
            {
                ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                {
                    ToBeCalled = ToBeCalled.PolicyCheck,
                    PolicyIds = [8],
                    IfCompliantState = 11,
                    IfNotCompliantState = 12
                })
            });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState>(component, "selectedToState").Id, Is.EqualTo(-2));
            Assert.That(GetMember<IEnumerable<CompliancePolicy>>(component, "selectedPolicies").Single(), Is.SameAs(policies[1]));
            Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(11));
            Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(12));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithInvalidExternalParams_ResetsToAutomatic()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new()
            {
                ExternalParams = "not-a-valid-auto-promote-value"
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = 13, Name = "Previous" });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState>(component, "selectedToState").Id, Is.EqualTo(-1));
            Assert.That(action.ExternalParams, Is.EqualTo("not-a-valid-auto-promote-value"));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithSimpleState_PreservesStateSelection()
        {
            EditActionAutoPromote component = new();
            List<WfState> states = [new() { Id = 11, Name = "Compliant" }, new() { Id = 12, Name = "Not compliant" }];
            WfStateAction action = new()
            {
                ExternalParams = "12"
            };
            SetMember(component, "States", states);
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState>(component, "selectedToState"), Is.SameAs(states[1]));
            Assert.That(action.ExternalParams, Is.EqualTo("12"));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithMissingConditionalReferences_FallsBackToAutomatic()
        {
            EditActionAutoPromote component = new();
            List<CompliancePolicy> policies = [new() { Id = 4 }];
            List<WfState> states = [new() { Id = 11, Name = "Compliant" }, new() { Id = 12, Name = "Not compliant" }];
            SetMember(component, "Policies", policies);
            SetMember(component, "States", states);
            SetMember(component, "ActAction", new WfStateAction
            {
                ExternalParams = JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                {
                    ToBeCalled = ToBeCalled.PolicyCheck,
                    PolicyIds = [99],
                    IfCompliantState = 77,
                    IfNotCompliantState = 88
                })
            });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<IEnumerable<CompliancePolicy>>(component, "selectedPolicies"), Is.Empty);
            Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(-1));
            Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(-1));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithEmptyParams_ResetsToAutomaticDefaults()
        {
            EditActionAutoPromote component = new();
            SetMember(component, "selectedToState", new WfState { Id = 19, Name = "Previous" });
            SetMember(component, "selectedStateOk", new WfState { Id = 20, Name = "Ok" });
            SetMember(component, "selectedStateNotOk", new WfState { Id = 21, Name = "Not Ok" });
            SetMember(component, "selectedPolicies", new List<CompliancePolicy> { new() { Id = 4 } });
            SetMember(component, "selectedToBeCalled", ToBeCalled.PolicyCheck);
            SetMember(component, "ActAction", new WfStateAction());

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState>(component, "selectedToState").Id, Is.EqualTo(-1));
            Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(-1));
            Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(-1));
            Assert.That(GetMember<IEnumerable<CompliancePolicy>>(component, "selectedPolicies"), Is.Empty);
            Assert.That(GetMember<ToBeCalled>(component, "selectedToBeCalled"), Is.EqualTo(ToBeCalled.PolicyCheck));
        }

        [Test]
        public async Task AutoPromote_OnStateSelectionHandlers_KeepParametersInSync()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            WfState state = new() { Id = 19, Name = "Escalated" };
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = -2 });
            SetMember(component, "selectedStateOk", new WfState { Id = 5 });
            SetMember(component, "selectedStateNotOk", new WfState { Id = 6 });

            await InvokeAsync(component, "OnToStateChanged", state);
            Assert.That(action.ExternalParams, Is.EqualTo("19"));

            await InvokeAsync(component, "OnToStateChanged", new WfState { Id = -2 });
            await InvokeAsync(component, "OnToBeCalledChanged", ToBeCalled.PolicyCheck);
            await InvokeAsync(component, "OnPoliciesChanged", new List<CompliancePolicy> { new() { Id = 1 }, new() { Id = 3 } });
            await InvokeAsync(component, "OnCheckResultLabelChanged", new ChangeEventArgs { Value = " ok " });
            await InvokeAsync(component, "OnStateOkChanged", new WfState { Id = 5 });
            await InvokeAsync(component, "OnStateNotOkChanged", new WfState { Id = 6 });

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.That(parameters.PolicyIds, Is.EqualTo(new List<int> { 1, 3 }));
            Assert.That(parameters.CheckResultLabel, Is.EqualTo("ok"));
            Assert.That(parameters.IfCompliantState, Is.EqualTo(5));
            Assert.That(parameters.IfNotCompliantState, Is.EqualTo(6));
        }

        [Test]
        public async Task AutoPromote_OnCheckResultLabelChanged_TrimsLabel()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = -2 });

            await InvokeAsync(component, "OnCheckResultLabelChanged", new ChangeEventArgs { Value = " passed " });

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.That(parameters.CheckResultLabel, Is.EqualTo("passed"));
        }

        [Test]
        public async Task AddApproval_UpdateExternalParams_SerializesSelectedValues()
        {
            EditActionAddApproval component = new();
            WfStateAction action = new();
            WfState state = new() { Id = 17, Name = "State" };
            UiUser approver = new() { Dn = "cn=approvers,dc=fwo" };
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedState", state);
            SetMember(component, "selectedUserGroup", approver);
            SetMember(component, "deadline", 5);

            await InvokeAsync(component, "UpdateExternalParams");

            ApprovalParams parameters = JsonSerializer.Deserialize<ApprovalParams>(action.ExternalParams)!;
            Assert.That(parameters.StateId, Is.EqualTo(17));
            Assert.That(parameters.ApproverGroup, Is.EqualTo("cn=approvers,dc=fwo"));
            Assert.That(parameters.Deadline, Is.EqualTo(5));
        }

        [Test]
        public async Task AddApproval_OnParametersSet_LoadsExternalParams()
        {
            EditActionAddApproval component = new();
            WfState approvalState = new() { Id = 31, Name = "Approve" };
            UiUser approver = new() { Dn = "cn=approver,dc=fwo" };
            WfStateAction action = new()
            {
                ExternalParams = JsonSerializer.Serialize(new ApprovalParams
                {
                    StateId = approvalState.Id,
                    ApproverGroup = approver.Dn,
                    Deadline = 3
                })
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { approvalState });
            SetMember(component, "UserAndGroupList", new List<UiUser> { approver });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState>(component, "selectedState"), Is.SameAs(approvalState));
            Assert.That(GetMember<UiUser?>(component, "selectedUserGroup"), Is.SameAs(approver));
            Assert.That(GetMember<int>(component, "deadline"), Is.EqualTo(3));
        }

        [Test]
        public async Task AddApproval_ChangeHandlers_KeepParametersInSync()
        {
            EditActionAddApproval component = new();
            WfStateAction action = new();
            WfState state = new() { Id = 44, Name = "State" };
            UiUser group = new() { Dn = "cn=group,dc=fwo" };
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnStateChanged", state);
            await InvokeAsync(component, "OnUserGroupChanged", group);
            await InvokeAsync(component, "OnDeadlineChanged", new ChangeEventArgs { Value = "9" });

            ApprovalParams parameters = JsonSerializer.Deserialize<ApprovalParams>(action.ExternalParams)!;
            Assert.That(parameters.StateId, Is.EqualTo(44));
            Assert.That(parameters.ApproverGroup, Is.EqualTo("cn=group,dc=fwo"));
            Assert.That(parameters.Deadline, Is.EqualTo(9));
        }

        [Test]
        public async Task CreateFlow_UpdateExternalParams_SerializesSelectedValues()
        {
            EditActionCreateFlow component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedSuccessState", new WfState { Id = 21 });
            SetMember(component, "selectedErrorState", new WfState { Id = 22 });
            SetMember(component, "confirmUiMessage", true);

            await InvokeAsync(component, "UpdateExternalParams");

            ActionResultStateParams parameters = JsonSerializer.Deserialize<ActionResultStateParams>(action.ExternalParams)!;
            Assert.That(parameters.SuccessState, Is.EqualTo(21));
            Assert.That(parameters.ErrorState, Is.EqualTo(22));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task CreateFlow_OnParametersSet_LoadsExternalParams()
        {
            EditActionCreateFlow component = new();
            WfStateAction action = new()
            {
                ExternalParams = JsonSerializer.Serialize(new ActionResultStateParams
                {
                    SuccessState = 21,
                    ErrorState = 22,
                    ConfirmUiMessage = true
                })
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { new() { Id = 21 }, new() { Id = 22 } });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState?>(component, "selectedSuccessState")?.Id, Is.EqualTo(21));
            Assert.That(GetMember<WfState?>(component, "selectedErrorState")?.Id, Is.EqualTo(22));
            Assert.That(GetMember<bool>(component, "confirmUiMessage"), Is.True);
        }

        [Test]
        public async Task CreateFlow_ChangeHandlers_KeepParametersInSync()
        {
            EditActionCreateFlow component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnSuccessStateChanged", new WfState { Id = 61 });
            await InvokeAsync(component, "OnErrorStateChanged", new WfState { Id = 62 });
            await InvokeAsync(component, "OnConfirmChanged", new ChangeEventArgs { Value = "true" });

            ActionResultStateParams parameters = JsonSerializer.Deserialize<ActionResultStateParams>(action.ExternalParams)!;
            Assert.That(parameters.SuccessState, Is.EqualTo(61));
            Assert.That(parameters.ErrorState, Is.EqualTo(62));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task UpdateModelling_UpdateExternalParams_SerializesSelectedValues()
        {
            EditActionUpdateModelling component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedModIntegrationState", "Implemented");
            SetMember(component, "actConfirmUpdateModelling", true);

            await InvokeAsync(component, "UpdateExternalParams");

            UpdateModellingActionParams parameters = UpdateModellingActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.ModellingState, Is.EqualTo("Implemented"));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task UpdateModelling_OnParametersSet_LoadsExternalParams()
        {
            EditActionUpdateModelling component = new();
            SetMember(component, "AvailableModIntegrationStateNames", new List<string> { "Implemented" });
            SetMember(component, "ActAction", new WfStateAction
            {
                ExternalParams = JsonSerializer.Serialize(new UpdateModellingActionParams { ModellingState = "Implemented", ConfirmUiMessage = true })
            });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<string?>(component, "selectedModIntegrationState"), Is.EqualTo("Implemented"));
            Assert.That(GetMember<bool>(component, "actConfirmUpdateModelling"), Is.True);
        }

        [Test]
        public async Task UpdateModelling_ChangeHandlers_KeepParametersInSync()
        {
            EditActionUpdateModelling component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnIntegrationStateChanged", "Implemented");
            await InvokeAsync(component, "OnConfirmChanged", new ChangeEventArgs { Value = "true" });

            UpdateModellingActionParams parameters = UpdateModellingActionParams.FromExternalParams(action.ExternalParams);
            Assert.That(parameters.ModellingState, Is.EqualTo("Implemented"));
            Assert.That(parameters.ConfirmUiMessage, Is.True);
        }

        [Test]
        public async Task EditActionGeneral_OnParametersSet_LoadsSelections()
        {
            EditActionGeneral component = new();
            SetMember(component, "ActAction", new WfStateAction
            {
                Phase = WorkflowPhases.implementation.ToString(),
                TaskType = WfTaskType.access.ToString()
            });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<string?>(component, "selectedPhase"), Is.EqualTo(WorkflowPhases.implementation.ToString()));
            Assert.That(GetMember<WfTaskType?>(component, "selectedTaskType"), Is.EqualTo(WfTaskType.access));
        }

        [Test]
        public async Task EditActionGeneral_OnActionTypeChanged_InvokesCallbackWithoutMutatingAction()
        {
            EditActionGeneral component = new();
            int callbackCount = 0;
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString()
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "ActionTypeChanged", EventCallback.Factory.Create<string?>(new object(), _ => callbackCount++));

            await InvokeAsync(component, "OnActionTypeChanged", StateActionTypes.SendEmail.ToString());

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.SetAlert.ToString()));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task EditActionGeneral_OnActionTypeChanged_NullClearsActionType()
        {
            EditActionGeneral component = new();
            int callbackCount = 0;
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString()
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "ActionTypeChanged", EventCallback.Factory.Create<string?>(new object(), _ => callbackCount++));

            await InvokeAsync(component, "OnActionTypeChanged", (string?)null);

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.SetAlert.ToString()));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public async Task EditActionGeneral_OnPhaseChanged_UpdatesAction()
        {
            EditActionGeneral component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnPhaseChanged", WorkflowPhases.review.ToString());

            Assert.That(action.Phase, Is.EqualTo(WorkflowPhases.review.ToString()));
            Assert.That(GetMember<string?>(component, "selectedPhase"), Is.EqualTo(WorkflowPhases.review.ToString()));
        }

        [Test]
        public async Task EditActionGeneral_OnPhaseChanged_NullClearsPhase()
        {
            EditActionGeneral component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnPhaseChanged", (string?)null);

            Assert.That(action.Phase, Is.EqualTo(""));
            Assert.That(GetMember<string?>(component, "selectedPhase"), Is.Null);
        }

        [Test]
        public async Task EditActionGeneral_OnTaskTypeChanged_UpdatesAction()
        {
            EditActionGeneral component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnTaskTypeChanged", WfTaskType.access);

            Assert.That(action.TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(GetMember<WfTaskType?>(component, "selectedTaskType"), Is.EqualTo(WfTaskType.access));
        }

        [Test]
        public async Task EditActionGeneral_OnTaskTypeChanged_NullClearsTaskType()
        {
            EditActionGeneral component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnTaskTypeChanged", (WfTaskType?)null);

            Assert.That(action.TaskType, Is.EqualTo(""));
            Assert.That(GetMember<WfTaskType?>(component, "selectedTaskType"), Is.Null);
        }

        [Test]
        public async Task EditActionGeneral_RenderingShowsButtonTextAndTaskType()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<EditActionGeneral> component = context.Render<EditActionGeneral>(parameters => parameters
                .Add(p => p.ActAction, new WfStateAction
                {
                    Event = StateActionEvents.OfferButton.ToString(),
                    Scope = WfObjectScopes.RequestTask.ToString()
                })
                .Add(p => p.AvailableTaskTypes, new List<WfTaskType> { WfTaskType.master, WfTaskType.access }));

            Assert.Multiple(() =>
            {
                Assert.That(component.Markup, Does.Contain("button_text"));
                Assert.That(component.Markup, Does.Contain("Task type:"));
            });
        }

        [Test]
        public async Task EditActionGeneral_RenderingHidesTaskTypeForNonTaskScope()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<EditActionGeneral> component = context.Render<EditActionGeneral>(parameters => parameters
                .Add(p => p.ActAction, new WfStateAction
                {
                    Event = StateActionEvents.None.ToString(),
                    Scope = WfObjectScopes.None.ToString()
                })
                .Add(p => p.AvailableTaskTypes, new List<WfTaskType> { WfTaskType.master, WfTaskType.access }));

            Assert.That(component.Markup, Does.Not.Contain("task_type"));
        }

        [Test]
        public void SettingsActions_ActionNotificationIds_ReturnsDistinctPositiveIds()
        {
            MethodInfo method = typeof(SettingsActions).GetMethod("ActionNotificationIds", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(SettingsActions).FullName, "ActionNotificationIds");
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [0, 5, 5, 7, -2]
                })
            };

            List<int> notificationIds = (List<int>)method.Invoke(null, [action])!;

            Assert.That(notificationIds, Is.EqualTo(new List<int> { 5, 7 }));
        }

        [Test]
        public async Task SettingsActions_DeleteActionNotifications_DeletesDistinctPositiveIds()
        {
            SettingsActions component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);

            await InvokeAsync(component, "DeleteActionNotifications", new List<int> { 3, 3, 0, 7, -1 });

            Assert.That(apiConn.DeletedNotificationIds, Is.EqualTo(new List<int> { 3, 7 }));
        }

        [Test]
        public async Task SettingsActions_RequestDeleteAction_SetsDeleteState()
        {
            SettingsActions component = new();
            WfStateAction action = new() { Id = 77, Name = "Delete me" };

            await InvokeAsync(component, "RequestDeleteAction", action);

            Assert.That(GetMember<WfStateAction?>(component, "deleteActionTarget"), Is.SameAs(action));
            Assert.That(GetMember<bool>(component, "DeleteActionMode"), Is.True);
        }

        [Test]
        public async Task SettingsActions_RefreshActions_LoadsData()
        {
            SettingsActionsApiConn apiConn = new()
            {
                InitialActions =
                [
                    new WfStateAction
                    {
                        Id = 10,
                        Name = "Notify",
                        ActionType = StateActionTypes.SendEmail.ToString(),
                        StateActions = [new WfStateActionStateHelper { State = new WfState { Id = 11, Name = "Open" } }]
                    }
                ],
                InitialStates =
                [
                    new WfState { Id = 11, Name = "Open" }
                ],
                InitialPolicies =
                [
                    new CompliancePolicy { Id = 7, Name = "Policy" }
                ]
            };

            await using OneShotJsonServer middlewareServer = await OneShotJsonServer.StartAsync("[]");
            await using BunitContext context = CreateSettingsActionsContext(apiConn, middlewareServer.BaseUrl);

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SettingsActions>());
            SettingsActions component = root.FindComponent<SettingsActions>().Instance;

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<bool>(component, "InitComplete"), Is.True);
                Assert.That(GetMember<List<WfStateAction>>(component, "actions"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<WfState>>(component, "states"), Has.Count.EqualTo(1));
                Assert.That(GetMember<List<CompliancePolicy>>(component, "policies"), Has.Count.EqualTo(1));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getActions));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.getStates));
                Assert.That(apiConn.Queries, Does.Contain(ComplianceQueries.getPolicies));
            });

            await InvokeAsync(component, "RefreshActionsUi");
        }

        [Test]
        public async Task SettingsActions_RefreshActions_WhenMiddlewareLookupFails_ReportsError()
        {
            SettingsActionsApiConn apiConn = new();
            string? reportedTitle = null;
            string? reportedMessage = null;
            SetMember(apiConn, "InitialActions", new List<WfStateAction>());
            SetMember(apiConn, "InitialStates", new List<WfState>());
            SetMember(apiConn, "InitialPolicies", new List<CompliancePolicy>());

            SettingsActions component = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "middlewareClient", new MiddlewareClient("http://127.0.0.1:1/"));
            SetMember(component, "globalConfig", new SimulatedGlobalConfig());
            SetMember(component, "userConfig", new SimulatedUserConfig
            {
                ReqAvailableTaskTypes = "[\"access\"]"
            });
            SetMember(component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((_, title, message, _) =>
            {
                reportedTitle = title;
                reportedMessage = message;
            }));

            await InvokeAsync(component, "RefreshActions");

            Assert.Multiple(() =>
            {
                Assert.That(reportedTitle, Is.EqualTo(new SimulatedUserConfig().GetText("fetch_data")));
                Assert.That(reportedMessage, Is.EqualTo(""));
            });
        }

        [Test]
        public async Task SettingsActions_AddAndEditAction_DelegateToPopup()
        {
            SettingsActionsApiConn apiConn = new();
            await using OneShotJsonServer middlewareServer = await OneShotJsonServer.StartAsync("[]");
            await using BunitContext context = CreateSettingsActionsContext(apiConn, middlewareServer.BaseUrl);

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SettingsActions>());
            SettingsActions component = root.FindComponent<SettingsActions>().Instance;
            EditActionPopup? popup = GetMember<EditActionPopup?>(component, "editActionPopup");
            Assert.That(popup, Is.Not.Null);

            await InvokeAsync(component, "AddAction");
            Assert.That(GetMember<bool>(popup!, "AddActionMode"), Is.True);

            WfStateAction action = new() { Id = 88, Name = "Edit me" };
            await InvokeAsync(component, "EditAction", action);

            Assert.That(GetMember<WfStateAction>(popup!, "actAction"), Is.SameAs(action));
            Assert.That(GetMember<bool>(popup!, "AddActionMode"), Is.False);
        }

        [Test]
        public async Task SettingsActions_DeleteAction_RemovesActionAndNotifications()
        {
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Id = 91,
                Name = "Mail",
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams { NotificationIds = [5, 5, 7] })
            };
            apiConn.InitialActions = [action];
            await using OneShotJsonServer middlewareServer = await OneShotJsonServer.StartAsync("[]");
            await using BunitContext context = CreateSettingsActionsContext(apiConn, middlewareServer.BaseUrl);

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SettingsActions>());
            SettingsActions component = root.FindComponent<SettingsActions>().Instance;
            SetMember(component, "actions", new List<WfStateAction> { action });
            SetMember(component, "deleteActionTarget", action);
            SetMember(component, "DeleteActionMode", true);

            await InvokeAsync(component, "DeleteAction");

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.deleteAction));
                Assert.That(apiConn.DeletedNotificationIds, Is.EqualTo(new List<int> { 5, 7 }));
                Assert.That(GetMember<List<WfStateAction>>(component, "actions"), Is.Empty);
                Assert.That(GetMember<bool>(component, "DeleteActionMode"), Is.False);
            });
        }

        [Test]
        public async Task SelectStatePopup_Confirm_InvokesCallbacksAndCloses()
        {
            SelectStatePopup component = new();
            WfState selected = new() { Id = 13, Name = "Selected" };
            bool display = true;
            WfState? changedState = null;
            int confirmCount = 0;

            SetMember(component, "Display", true);
            SetMember(component, "SelectedState", selected);
            SetMember(component, "States", new List<WfState> { selected });
            SetMember(component, "SelectedStateChanged", EventCallback.Factory.Create<WfState?>(new object(), state => changedState = state));
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(new object(), value => display = value));
            SetMember(component, "OnConfirm", EventCallback.Factory.Create(new object(), () => confirmCount++));

            await InvokeAsync(component, "OnParametersSet");
            await InvokeAsync(component, "Confirm");

            Assert.That(changedState, Is.SameAs(selected));
            Assert.That(confirmCount, Is.EqualTo(1));
            Assert.That(display, Is.False);
        }

        [Test]
        public async Task SelectStatePopup_OnParametersSet_InitializesSelectedState()
        {
            SelectStatePopup component = new();
            WfState selected = new() { Id = 13, Name = "Selected" };

            SetMember(component, "SelectedState", selected);

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState?>(component, "selectedState"), Is.SameAs(selected));
        }

        [Test]
        public async Task SelectStatePopup_OnParametersSet_AllowsNullSelection()
        {
            SelectStatePopup component = new();
            SetMember(component, "SelectedState", null);
            SetMember(component, "selectedState", new WfState { Id = 13, Name = "Previous" });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState?>(component, "selectedState"), Is.Null);
        }

        [Test]
        public async Task SelectStatePopup_Confirm_WithNoSelection_DoesNothing()
        {
            SelectStatePopup component = new();
            bool display = true;
            int confirmCount = 0;

            SetMember(component, "Display", true);
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(new object(), value => display = value));
            SetMember(component, "OnConfirm", EventCallback.Factory.Create(new object(), () => confirmCount++));

            await InvokeAsync(component, "Confirm");

            Assert.That(confirmCount, Is.Zero);
            Assert.That(display, Is.True);
        }

        [Test]
        public async Task SelectStatePopup_ClosePopup_InvokesCancelAndCloses()
        {
            SelectStatePopup component = new();
            bool display = true;
            int cancelCount = 0;

            SetMember(component, "Display", true);
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(new object(), value => display = value));
            SetMember(component, "OnCancel", EventCallback.Factory.Create(new object(), () => cancelCount++));

            await InvokeAsync(component, "ClosePopup");

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(display, Is.False);
        }

        [Test]
        public async Task SelectStatePopup_Cancel_InvokesCancelAndCloses()
        {
            SelectStatePopup component = new();
            bool display = true;
            int cancelCount = 0;

            SetMember(component, "Display", true);
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(new object(), value => display = value));
            SetMember(component, "OnCancel", EventCallback.Factory.Create(new object(), () => cancelCount++));

            await InvokeAsync(component, "CancelAsync");

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(display, Is.False);
        }

        [Test]
        public void SelectStatePopup_DefaultStateToString_UsesStateName()
        {
            SelectStatePopup component = new();
            WfState state = new() { Id = 13, Name = "Selected" };

            string result = GetMember<Func<WfState, string>>(component, "StateToString").Invoke(state);

            Assert.That(result, Is.EqualTo("Selected"));
        }

        [Test]
        public async Task EditActionUsingStates_OnParametersSet_SelectsFirstAvailableState()
        {
            EditActionUsingStates component = new();
            List<WfState> states =
            [
                new() { Id = -1, Name = "Automatic" },
                new() { Id = 8, Name = "Alpha" },
                new() { Id = 11, Name = "Beta" }
            ];
            SetMember(component, "ActAction", new WfStateAction());
            SetMember(component, "States", states);

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState?>(component, "selectedStateLink"), Is.SameAs(states[1]));
        }

        [Test]
        public async Task EditActionUsingStates_AvailableStateLinks_ExcludesNegativeAndLinkedStates()
        {
            EditActionUsingStates component = new();
            WfState linkedState = new() { Id = 11, Name = "Linked" };
            WfStateAction action = new()
            {
                StateActions = [new WfStateActionStateHelper { State = linkedState }]
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState>
            {
                new() { Id = -1, Name = "Automatic" },
                linkedState,
                new() { Id = 14, Name = "Available" }
            });
            await InvokeAsync(component, "OnParametersSet");

            List<WfState> availableStateLinks = (List<WfState>)GetInstanceMethod(component.GetType(), "AvailableStateLinks").Invoke(component, [])!;

            Assert.That(availableStateLinks.Select(state => state.Id), Is.EqualTo(ExpectedAvailableStateLinkIds));
        }

        [Test]
        public async Task EditActionUsingStates_SelectStateLink_UsesFirstAvailableState()
        {
            EditActionUsingStates component = new();
            WfState firstAvailable = new() { Id = 14, Name = "Available" };
            SetMember(component, "ActAction", new WfStateAction());
            SetMember(component, "States", new List<WfState>
            {
                new() { Id = -1, Name = "Automatic" },
                firstAvailable
            });

            await InvokeAsync(component, "SelectStateLink");

            Assert.That(GetMember<bool>(component, "selectStateMode"), Is.True);
            Assert.That(GetMember<WfState?>(component, "selectedStateLink"), Is.SameAs(firstAvailable));
        }

        [Test]
        public async Task EditActionUsingStates_SelectStateLink_UsesNullWhenNoStateIsAvailable()
        {
            EditActionUsingStates component = new();
            SetMember(component, "ActAction", new WfStateAction());
            SetMember(component, "States", new List<WfState>
            {
                new() { Id = -1, Name = "Automatic" }
            });

            await InvokeAsync(component, "SelectStateLink");

            Assert.That(GetMember<bool>(component, "selectStateMode"), Is.True);
            Assert.That(GetMember<WfState?>(component, "selectedStateLink"), Is.Null);
        }

        [Test]
        public async Task EditActionUsingStates_CancelSelectStateLink_ClearsSelection()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 14, Name = "Available" };
            SetMember(component, "ActAction", new WfStateAction());
            SetMember(component, "States", new List<WfState> { state });
            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", state);
            SetMember(component, "selectStateMode", true);

            await InvokeAsync(component, "CancelSelectStateLink");

            Assert.That(GetMember<bool>(component, "selectStateMode"), Is.False);
            Assert.That(GetMember<WfState?>(component, "selectedStateLink"), Is.Null);
        }

        [Test]
        public async Task EditActionUsingStates_AddStateLink_AddModeUpdatesLocalStateOnly()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 11, Name = "Approved" };
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", state);

            await InvokeAsync(component, "AddStateLink");

            Assert.That(action.StateActions, Is.Empty);
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Has.Count.EqualTo(1));
            Assert.That(GetMember<bool>(component, "selectStateMode"), Is.False);
            Assert.That(GetMember<WfState?>(component, "selectedStateLink"), Is.Null);
        }

        [Test]
        public async Task EditActionUsingStates_AddStateLink_NullSelectionDoesNothing()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 11, Name = "Approved" };
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", null);

            await InvokeAsync(component, "AddStateLink");

            Assert.That(action.StateActions, Is.Empty);
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Is.Empty);
        }

        [Test]
        public async Task EditActionUsingStates_AddStateLink_DuplicateSelectionDoesNothing()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 11, Name = "Approved" };
            WfStateAction action = new()
            {
                StateActions = [new WfStateActionStateHelper { State = state, SortOrder = 1 }]
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", state);

            await InvokeAsync(component, "AddStateLink");

            Assert.That(action.StateActions, Has.Count.EqualTo(1));
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task EditActionUsingStates_AddStateLink_EditModePersistsAndNotifies()
        {
            EditActionUsingStates component = new();
            SettingsActionsApiConn apiConn = new();
            WfState state = new() { Id = 11, Name = "Approved" };
            WfStateAction action = new()
            {
                Id = 19,
                Name = "Action",
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            int changedCount = 0;
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            SetMember(component, "selectedStateLink", state);

            await InvokeAsync(component, "AddStateLink");

            Assert.That(apiConn.Queries, Is.Empty);
            Assert.That(action.StateActions, Is.Empty);
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Has.Count.EqualTo(1));

            await component.PersistPendingStateLinksAsync();

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.addStateAction), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.updateStateActionSortOrder), Is.EqualTo(1));
            Assert.That(action.StateActions, Has.Count.EqualTo(1));
            Assert.That(state.Actions, Has.Count.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task EditActionUsingStates_OnParametersSet_DoesNotResetPendingLinksForSameAction()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 11, Name = "Approved" };
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });

            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", state);
            await InvokeAsync(component, "AddStateLink");
            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task EditActionUsingStates_ResetPendingStateLinks_ReinitializesFromActionOnNextParametersSet()
        {
            EditActionUsingStates component = new();
            WfState persistedState = new() { Id = 11, Name = "Approved" };
            WfState stagedState = new() { Id = 14, Name = "Rejected" };
            WfStateAction action = new()
            {
                StateActions = [new WfStateActionStateHelper { State = persistedState, SortOrder = 1 }]
            };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { persistedState, stagedState });

            await InvokeAsync(component, "OnParametersSet");
            SetMember(component, "selectedStateLink", stagedState);
            await InvokeAsync(component, "AddStateLink");
            await InvokeAsync(component, "ResetPendingStateLinks");
            await InvokeAsync(component, "OnParametersSet");

            List<WfStateActionStateHelper> pendingStateLinks = GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks");
            Assert.That(pendingStateLinks.Select(link => link.State.Id), Is.EqualTo(new[] { persistedState.Id }));
        }

        [Test]
        public async Task EditActionUsingStates_RemoveStateLink_WithoutLinkedAction_RemovesLocally()
        {
            EditActionUsingStates component = new();
            WfState state = new() { Id = 17, Name = "State" };
            WfStateAction action = new();
            WfStateActionStateHelper stateLink = new() { State = state, SortOrder = 1 };
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            action.StateActions.Add(stateLink);
            await InvokeAsync(component, "OnParametersSet");

            await InvokeAsync(component, "RemoveStateLink", stateLink);

            Assert.That(action.StateActions, Has.Count.EqualTo(1));
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Is.Empty);
        }

        [Test]
        public async Task EditActionUsingStates_RemoveStateLink_EditModePersistsRemoval()
        {
            EditActionUsingStates component = new();
            SettingsActionsApiConn apiConn = new();
            WfState state = new() { Id = 17, Name = "State" };
            WfStateAction action = new()
            {
                Id = 22,
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            WfStateAction otherAction = new()
            {
                Id = 23,
                ActionType = StateActionTypes.AddApproval.ToString()
            };
            WfStateActionDataHelper linkedAction = new()
            {
                Action = action,
                SortOrder = 1
            };
            WfStateActionDataHelper remainingAction = new()
            {
                Action = otherAction,
                SortOrder = 2
            };
            WfStateActionStateHelper stateLink = new() { State = state, SortOrder = 1 };
            int changedCount = 0;
            state.Actions.Add(linkedAction);
            state.Actions.Add(remainingAction);
            action.StateActions.Add(stateLink);
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { state });
            await InvokeAsync(component, "OnParametersSet");

            await InvokeAsync(component, "RemoveStateLink", stateLink);

            Assert.That(apiConn.Queries, Is.Empty);
            Assert.That(action.StateActions, Has.Count.EqualTo(1));
            Assert.That(GetMember<List<WfStateActionStateHelper>>(component, "pendingStateLinks"), Is.Empty);

            await component.PersistPendingStateLinksAsync();

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.removeStateAction), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.updateStateActionSortOrder), Is.EqualTo(1));
            Assert.That(action.StateActions, Is.Empty);
            Assert.That(state.Actions, Has.Count.EqualTo(1));
            Assert.That(state.Actions[0].Action, Is.SameAs(otherAction));
            Assert.That(state.Actions[0].SortOrder, Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task EditActionUsingStates_PersistPendingStateLinksAsync_PersistsAllLinkedStates()
        {
            EditActionUsingStates component = new();
            SettingsActionsApiConn apiConn = new();
            WfState firstState = new() { Id = 21, Name = "First" };
            WfState secondState = new() { Id = 22, Name = "Second" };
            WfStateAction action = new()
            {
                Id = 23,
                ActionType = StateActionTypes.SendEmail.ToString(),
                StateActions =
                [
                    new WfStateActionStateHelper { State = firstState, SortOrder = 0 },
                    new WfStateActionStateHelper { State = secondState, SortOrder = 0 }
                ]
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", action);
            SetMember(component, "States", new List<WfState> { firstState, secondState });
            await InvokeAsync(component, "OnParametersSet");

            await component.PersistPendingStateLinksAsync();

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.addStateAction), Is.EqualTo(2));
            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.updateStateActionSortOrder), Is.EqualTo(2));
            Assert.That(firstState.Actions, Has.Count.EqualTo(1));
            Assert.That(secondState.Actions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task SendEmail_OnAttachedContentChanged_UpdatesExternalParams()
        {
            EditActionSendEmail component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnAttachedContentChanged", EmailAttachedContent.RequestedConnections);

            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
        }

        [Test]
        public async Task SendEmail_OnConfirmSentMailChanged_UpdatesExternalParams()
        {
            EditActionSendEmail component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnConfirmSentMailChanged", new ChangeEventArgs { Value = "true" });

            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.ConfirmSentMail, Is.True);
        }

        [Test]
        public async Task SendEmail_PrepareForSaveAsync_LegacyNotificationCreatesNotificationAndPersistsIds()
        {
            EditActionSendEmail component = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Name = "Workflow mail",
                ActionType = StateActionTypes.SendEmail.ToString(),
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    Subject = "Legacy subject",
                    Body = "Legacy body"
                })
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "ActAction", action);

            await InvokeAsync(component, "OnParametersSet");
            bool result = await component.PrepareForSaveAsync();

            Assert.That(result, Is.True);
            Assert.That(apiConn.Queries.Count(q => q == NotificationQueries.addNotification), Is.EqualTo(1));
            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { apiConn.NextNewNotificationId }));
            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.NotificationIds, Is.EqualTo(new List<int> { apiConn.NextNewNotificationId }));
        }

        [Test]
        public async Task SendEmail_CleanupOnCancelAsync_ExistingActionDoesNotDelete()
        {
            EditActionSendEmail component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", new WfStateAction { Id = 99, ActionType = StateActionTypes.SendEmail.ToString() });
            SetMember(component, "actActionNotificationIds", new List<int> { 5, 7 });

            await component.CleanupOnCancelAsync();

            Assert.That(apiConn.DeletedNotificationIds, Is.Empty);
        }

        [Test]
        public async Task EditActionPopup_OpenAddAsync_InitializesAddMode()
        {
            SettingsActionsApiConn apiConn = new();
            await using BunitContext context = CreateSettingsActionsContext(apiConn, "http://127.0.0.1:1/");
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditActionPopup>());
            EditActionPopup component = root.FindComponent<EditActionPopup>().Instance;

            await component.OpenAddAsync();

            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.True);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.True);
            Assert.That(GetMember<WfStateAction>(component, "actAction").Id, Is.EqualTo(0));
        }

        [Test]
        public async Task EditActionPopup_OpenEditAsync_LoadsSetAlertMessage()
        {
            SettingsActionsApiConn apiConn = new();
            await using BunitContext context = CreateSettingsActionsContext(apiConn, "http://127.0.0.1:1/");
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditActionPopup>());
            EditActionPopup component = root.FindComponent<EditActionPopup>().Instance;
            WfStateAction action = new()
            {
                Id = 7,
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "alert message"
            };

            await component.OpenEditAsync(action);

            Assert.That(GetMember<WfStateAction>(component, "actAction"), Is.SameAs(action));
            Assert.That(GetMember<string>(component, "message"), Is.EqualTo("alert message"));
            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.False);
        }

        [Test]
        public async Task EditActionPopup_LoadActionExternalParams_IgnoresEmptyParams()
        {
            SettingsActionsApiConn apiConn = new();
            await using BunitContext context = CreateSettingsActionsContext(apiConn, "http://127.0.0.1:1/");
            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<EditActionPopup>());
            EditActionPopup component = root.FindComponent<EditActionPopup>().Instance;
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.TrafficPathAnalysis.ToString(),
                ExternalParams = ""
            };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "stale");
            SetMember(component, "actPathAnalysisParams", new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList });

            await component.OpenEditAsync(action);

            Assert.That(GetMember<string>(component, "message"), Is.EqualTo(""));
            Assert.That(GetMember<PathAnalysisActionParams>(component, "actPathAnalysisParams").Option, Is.EqualTo(PathAnalysisOptions.DisplayFoundDevices));
        }

        [Test]
        public async Task EditActionPopup_ActionTypeChanged_ResetsSpecificState()
        {
            EditActionPopup component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "alert message"
            };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "alert message");

            await InvokeAsync(component, "ActionTypeChanged", StateActionTypes.TrafficPathAnalysis.ToString());

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.TrafficPathAnalysis.ToString()));
            Assert.That(action.ExternalParams, Is.EqualTo(""));
            Assert.That(GetMember<string>(component, "message"), Is.EqualTo(""));
        }

        [Test]
        public async Task EditActionPopup_ActionTypeChanged_SameTypeDoesNothing()
        {
            EditActionPopup component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "alert message"
            };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "alert message");

            await InvokeAsync(component, "ActionTypeChanged", StateActionTypes.SetAlert.ToString());

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.SetAlert.ToString()));
            Assert.That(action.ExternalParams, Is.EqualTo("alert message"));
            Assert.That(GetMember<string>(component, "message"), Is.EqualTo("alert message"));
        }

        [Test]
        public async Task EditActionPopup_ActionTypeChanged_FromGeneralCallback_ClearsExternalParams()
        {
            EditActionPopup component = new();
            EditActionGeneral general = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "alert message"
            };
            SetMember(component, "actAction", action);
            SetMember(component, "message", "alert message");
            SetMember(general, "ActAction", action);
            SetMember(general, "ActionTypeChanged", EventCallback.Factory.Create<string?>(new object(), async actionType => await InvokeAsync(component, "ActionTypeChanged", actionType)));

            await InvokeAsync(general, "OnActionTypeChanged", StateActionTypes.TrafficPathAnalysis.ToString());

            Assert.That(action.ActionType, Is.EqualTo(StateActionTypes.TrafficPathAnalysis.ToString()));
            Assert.That(action.ExternalParams, Is.EqualTo(""));
            Assert.That(GetMember<string>(component, "message"), Is.EqualTo(""));
        }

        [Test]
        public async Task EditActionPopup_LoadActionExternalParams_DeserializesTrafficPathAnalysis()
        {
            EditActionPopup component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.TrafficPathAnalysis.ToString(),
                ExternalParams = JsonSerializer.Serialize(new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList })
            };
            SetMember(component, "actAction", action);

            await InvokeAsync(component, "EditAction", action);

            Assert.That(GetMember<PathAnalysisActionParams>(component, "actPathAnalysisParams").Option, Is.EqualTo(PathAnalysisOptions.WriteToDeviceList));
        }

        [Test]
        public async Task EditActionPopup_TryUpdateExternalParams_SerializesTrafficPathAnalysis()
        {
            EditActionPopup component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.TrafficPathAnalysis.ToString()
            };
            SetMember(component, "actAction", action);
            SetMember(component, "actPathAnalysisParams", new PathAnalysisActionParams { Option = PathAnalysisOptions.WriteToDeviceList });

            bool result = await InvokeAsync<bool>(component, "TryUpdateExternalParams");

            Assert.That(result, Is.True);
            PathAnalysisActionParams parameters = JsonSerializer.Deserialize<PathAnalysisActionParams>(action.ExternalParams)!;
            Assert.That(parameters.Option, Is.EqualTo(PathAnalysisOptions.WriteToDeviceList));
        }

        [Test]
        public async Task EditActionPopup_TryUpdateExternalParams_DelegatesSendEmailEditor()
        {
            EditActionPopup component = new();
            EditActionSendEmail sendEmailEditor = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            SetMember(component, "actAction", action);
            SetMember(component, "persistedAction", new WfStateAction(action));
            SetMember(component, "sendEmailEditor", sendEmailEditor);
            SetMember(sendEmailEditor, "ActAction", action);
            SetMember(sendEmailEditor, "PersistedAction", new WfStateAction(action));
            SetMember(sendEmailEditor, "actActionNotificationIds", new List<int> { 11 });

            bool result = await InvokeAsync<bool>(component, "TryUpdateExternalParams");

            Assert.That(result, Is.True);
            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.NotificationIds, Is.EqualTo(new List<int> { 11 }));
        }

        [Test]
        public async Task EditActionPopup_NotificationChange_UsesPersistedSnapshotWhenSavingAndCancelKeepsPopupClosed()
        {
            EditActionPopup component = new();
            EditActionSendEmail sendEmailEditor = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Id = 77,
                Name = "Original action",
                ActionType = StateActionTypes.SendEmail.ToString(),
                Scope = WfObjectScopes.Ticket.ToString(),
                TaskType = "access",
                Phase = "review",
                Event = StateActionEvents.OnSet.ToString(),
                ButtonText = "Original button",
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [3]
                })
            };

            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "actAction", action);
            SetMember(component, "persistedAction", new WfStateAction(action));
            SetMember(component, "EditActionMode", true);
            SetMember(component, "AddActionMode", false);
            SetMember(component, "sendEmailEditor", sendEmailEditor);
            SetMember(sendEmailEditor, "apiConnection", apiConn);
            SetMember(sendEmailEditor, "userConfig", new SimulatedUserConfig());
            SetMember(sendEmailEditor, "ActAction", action);
            SetMember(sendEmailEditor, "PersistedAction", new WfStateAction(action));
            SetMember(sendEmailEditor, "actActionNotificationIds", new List<int> { 3 });
            SetMember(sendEmailEditor, "actAttachedContent", EmailAttachedContent.RequestedConnections);
            SetMember(sendEmailEditor, "actConfirmSentMail", true);

            action.Name = "Changed action";
            action.Scope = WfObjectScopes.RequestTask.ToString();
            action.TaskType = "rule_modify";
            action.ButtonText = "Changed button";

            await InvokeAsync(sendEmailEditor, "SetActionNotificationIds", new List<int> { 3, 7 });
            await InvokeAsync(component, "Cancel");

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.LastQuery, Is.EqualTo(RequestQueries.updateAction));
                Assert.That(GetVariable<string>(apiConn.LastVariables, "name"), Is.EqualTo("Original action"));
                Assert.That(GetVariable<string>(apiConn.LastVariables, "scope"), Is.EqualTo(WfObjectScopes.Ticket.ToString()));
                Assert.That(GetVariable<string>(apiConn.LastVariables, "taskType"), Is.EqualTo("access"));
                Assert.That(GetVariable<string>(apiConn.LastVariables, "buttonText"), Is.EqualTo("Original button"));
                Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
            });
        }

        [Test]
        public async Task EditActionPopup_TryUpdateExternalParams_ReturnsFalseWithoutSendEmailEditor()
        {
            EditActionPopup component = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            SetMember(component, "actAction", action);

            bool result = await InvokeAsync<bool>(component, "TryUpdateExternalParams");

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task EditActionPopup_SaveNewAction_PersistsActionAndPendingStateLinks()
        {
            EditActionPopup component = new();
            SettingsActionsApiConn apiConn = new();
            WfState state = new() { Id = 41, Name = "Approved" };
            WfStateAction action = new()
            {
                Name = "New action",
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "hello",
                StateActions = [new WfStateActionStateHelper { State = state, SortOrder = 0 }]
            };
            EditActionUsingStates usingStatesEditor = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "Actions", new List<WfStateAction>());
            SetMember(component, "States", new List<WfState> { state });
            SetMember(component, "actAction", action);
            SetMember(component, "message", "hello");
            SetMember(component, "usingStatesEditor", usingStatesEditor);
            SetMember(usingStatesEditor, "apiConnection", apiConn);
            SetMember(usingStatesEditor, "ActAction", action);
            SetMember(usingStatesEditor, "States", new List<WfState> { state });
            await InvokeAsync(usingStatesEditor, "OnParametersSet");
            SetMember(component, "AddActionMode", true);

            await InvokeAsync(component, "SaveAction");

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.newAction), Is.EqualTo(1));
            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.addStateAction), Is.EqualTo(1));
            Assert.That(GetMember<List<WfStateAction>>(component, "Actions"), Has.Count.EqualTo(1));
            Assert.That(action.Id, Is.EqualTo(apiConn.NextNewActionId));
        }

        [Test]
        public async Task EditActionPopup_SaveNewAction_ReturnIdsNull_DoesNotAddAction()
        {
            EditActionPopup component = new();
            SettingsActionsApiConn apiConn = new()
            {
                ReturnNullNewActionIds = true
            };
            WfStateAction action = new()
            {
                Name = "New action",
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "hello"
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "Actions", new List<WfStateAction>());
            SetMember(component, "actAction", action);
            SetMember(component, "message", "hello");
            SetMember(component, "EditActionMode", true);
            SetMember(component, "AddActionMode", true);

            await InvokeAsync(component, "SaveAction");

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.newAction), Is.EqualTo(1));
            Assert.That(GetMember<List<WfStateAction>>(component, "Actions"), Is.Empty);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.True);
        }

        [Test]
        public async Task EditActionPopup_SaveExistingAction_UpdatesActionAndInvokesChanged()
        {
            EditActionPopup component = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                Id = 55,
                Name = "Existing",
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "updated message"
            };
            int changedCount = 0;
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "Actions", new List<WfStateAction> { action });
            SetMember(component, "actAction", action);
            SetMember(component, "message", "updated message");
            SetMember(component, "EditActionMode", true);
            SetMember(component, "AddActionMode", false);
            SetMember(component, "OnChanged", EventCallback.Factory.Create(new object(), () => changedCount++));

            await InvokeAsync(component, "SaveAction");

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.updateAction), Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
        }

        [Test]
        public async Task EditActionPopup_SaveExistingAction_UpdatedIdMismatch_DoesNotClose()
        {
            EditActionPopup component = new();
            SettingsActionsApiConn apiConn = new()
            {
                ForcedUpdatedId = 999
            };
            WfStateAction action = new()
            {
                Id = 55,
                Name = "Existing",
                ActionType = StateActionTypes.SetAlert.ToString(),
                ExternalParams = "updated message"
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "Actions", new List<WfStateAction> { action });
            SetMember(component, "actAction", action);
            SetMember(component, "message", "updated message");
            SetMember(component, "AddActionMode", false);

            await InvokeAsync(component, "SaveAction");

            Assert.That(apiConn.Queries.Count(q => q == RequestQueries.updateAction), Is.EqualTo(1));
            Assert.That(GetMember<List<WfStateAction>>(component, "Actions")[0], Is.SameAs(action));
        }

        [Test]
        public async Task EditActionPopup_Cancel_AddModeCleansEmailDraftNotifications()
        {
            EditActionPopup component = new();
            EditActionSendEmail sendEmailEditor = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "actAction", action);
            SetMember(component, "AddActionMode", true);
            SetMember(component, "sendEmailEditor", sendEmailEditor);
            SetMember(sendEmailEditor, "apiConnection", apiConn);
            SetMember(sendEmailEditor, "ActAction", action);
            SetMember(sendEmailEditor, "actActionNotificationIds", new List<int> { 5, 5, 7 });

            await InvokeAsync(component, "Cancel");

            Assert.That(apiConn.DeletedNotificationIds, Is.EqualTo(new List<int> { 5, 7 }));
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
        }

        [Test]
        public async Task EditActionPopup_Cancel_EditModeResetsUsingStatesEditor()
        {
            EditActionPopup component = new();
            EditActionUsingStates usingStatesEditor = new();
            WfStateAction action = new();
            SetMember(component, "actAction", action);
            SetMember(component, "EditActionMode", true);
            SetMember(component, "AddActionMode", false);
            SetMember(component, "usingStatesEditor", usingStatesEditor);
            SetMember(usingStatesEditor, "ActAction", action);
            SetMember(usingStatesEditor, "States", new List<WfState> { new() { Id = 11, Name = "Approved" } });
            await InvokeAsync(usingStatesEditor, "OnParametersSet");
            SetMember(usingStatesEditor, "pendingStateLinks", new List<WfStateActionStateHelper>
            {
                new() { State = new WfState { Id = 11, Name = "Approved" }, SortOrder = 1 }
            });

            await InvokeAsync(component, "Cancel");

            Assert.That(GetMember<List<WfStateActionStateHelper>>(usingStatesEditor, "pendingStateLinks"), Is.Empty);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.False);
        }

        [Test]
        public async Task EditActionPopup_Cancel_AddModeWithoutSendEmailEditor_StillCloses()
        {
            EditActionPopup component = new();
            SetMember(component, "AddActionMode", true);
            SetMember(component, "EditActionMode", true);

            await InvokeAsync(component, "Cancel");

            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
            Assert.That(GetMember<bool>(component, "AddActionMode"), Is.False);
        }

        [Test]
        public async Task EditActionPopup_Cancel_EditModeDoesNotCleanupNotifications()
        {
            EditActionPopup component = new();
            EditActionSendEmail sendEmailEditor = new();
            SettingsActionsApiConn apiConn = new();
            WfStateAction action = new()
            {
                ActionType = StateActionTypes.SendEmail.ToString()
            };
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "actAction", action);
            SetMember(component, "AddActionMode", false);
            SetMember(component, "sendEmailEditor", sendEmailEditor);
            SetMember(sendEmailEditor, "apiConnection", apiConn);
            SetMember(sendEmailEditor, "ActAction", action);
            SetMember(sendEmailEditor, "actActionNotificationIds", new List<int> { 5, 5, 7 });

            await InvokeAsync(component, "Cancel");

            Assert.That(apiConn.DeletedNotificationIds, Is.Empty);
            Assert.That(GetMember<bool>(component, "EditActionMode"), Is.False);
        }

        [Test]
        public async Task SendEmail_PrepareForSaveAsync_RejectsWithoutNotification()
        {
            EditActionSendEmail component = new();
            string? errorMessage = null;
            SetMember(component, "ActAction", new WfStateAction { ActionType = StateActionTypes.SendEmail.ToString() });
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>((_, _, message, _) => errorMessage = message));

            bool result = await component.PrepareForSaveAsync();

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.EqualTo(new SimulatedUserConfig().GetText("E4011")));
        }

        [Test]
        public async Task SendEmail_OnParametersSet_LoadsExternalParams()
        {
            EditActionSendEmail component = new();
            SetMember(component, "ActAction", new WfStateAction
            {
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams
                {
                    NotificationIds = [7, 8],
                    AttachedContent = EmailAttachedContent.RequestedConnections,
                    ConfirmSentMail = true
                })
            });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 7, 8 }));
            Assert.That(GetMember<EmailAttachedContent>(component, "actAttachedContent"), Is.EqualTo(EmailAttachedContent.RequestedConnections));
            Assert.That(GetMember<bool>(component, "actConfirmSentMail"), Is.True);
        }

        [Test]
        public async Task SendEmail_SetActionNotificationIds_PersistsForExistingEmailAction()
        {
            EditActionSendEmail component = new();
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
            SetMember(component, "ActAction", action);
            SetMember(component, "PersistedAction", new WfStateAction(action));
            SetMember(component, "actAttachedContent", EmailAttachedContent.RequestedConnections);

            await InvokeAsync(component, "SetActionNotificationIds", new List<int> { 3, 3, 8 });

            Assert.That(apiConn.LastQuery, Is.EqualTo(RequestQueries.updateAction));
            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 3, 8 }));
            EmailActionParams parameters = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams)!;
            Assert.That(parameters.NotificationIds, Is.EqualTo(new List<int> { 3, 8 }));
            Assert.That(parameters.AttachedContent, Is.EqualTo(EmailAttachedContent.RequestedConnections));
        }

        [Test]
        public async Task SendEmail_SetActionNotificationIds_DoesNotPersistForNewAction()
        {
            EditActionSendEmail component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", new WfStateAction { ActionType = StateActionTypes.SendEmail.ToString() });

            await InvokeAsync(component, "SetActionNotificationIds", new List<int> { 3 });

            Assert.That(apiConn.Queries, Is.Empty);
            Assert.That(GetMember<List<int>>(component, "actActionNotificationIds"), Is.EqualTo(new List<int> { 3 }));
        }

        [Test]
        public async Task SendEmail_CleanupOnCancelAsync_DeletesTemporaryNotificationsOnlyForNewAction()
        {
            EditActionSendEmail component = new();
            SettingsActionsApiConn apiConn = new();
            SetMember(component, "apiConnection", apiConn);
            SetMember(component, "ActAction", new WfStateAction { ActionType = StateActionTypes.SendEmail.ToString() });
            SetMember(component, "actActionNotificationIds", new List<int> { 5, 5, 0, 7 });

            await component.CleanupOnCancelAsync();

            Assert.That(apiConn.DeletedNotificationIds, Is.EqualTo(new List<int> { 5, 7 }));
        }

        private static async Task InvokeAsync(object instance, string methodName, params object?[] args)
        {
            object? result = GetInstanceMethod(instance.GetType(), methodName).Invoke(instance, args);
            if (result is Task task)
            {
                await task;
            }
        }

        private static async Task<T> InvokeAsync<T>(object instance, string methodName, params object?[] args)
        {
            object? result = GetInstanceMethod(instance.GetType(), methodName).Invoke(instance, args);
            if (result is Task<T> taskOfT)
            {
                return await taskOfT;
            }
            if (result is Task task)
            {
                await task;
                return default!;
            }
            return (T)result!;
        }

        private static MethodInfo GetInstanceMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException(type.FullName, name);
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

        private static BunitContext CreateSettingsActionsContext(SettingsActionsApiConn apiConn, string middlewareBaseUrl)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                ReqAvailableTaskTypes = "[\"access\",\"rule_delete\",\"rule_modify\"]"
            });
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient(middlewareBaseUrl));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            context.Services.AddSingleton<AuthenticationStateProvider>(new StaticAuthStateProvider(Roles.Admin));
            return context;
        }

        private sealed class StaticAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authState;

            public StaticAuthStateProvider(params string[] roles)
            {
                ClaimsIdentity identity = new(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role);
                authState = new AuthenticationState(new ClaimsPrincipal(identity));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(authState);
            }
        }

        private sealed class OneShotJsonServer : IAsyncDisposable
        {
            private readonly TcpListener listener;
            private readonly Task worker;
            private readonly CancellationTokenSource cancellationTokenSource = new();

            public string BaseUrl { get; }

            private OneShotJsonServer(string responseBody)
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                BaseUrl = $"http://127.0.0.1:{port}/";
                worker = Task.Run(() => ServeOnceAsync(responseBody));
            }

            public static Task<OneShotJsonServer> StartAsync(string responseBody)
            {
                return Task.FromResult(new OneShotJsonServer(responseBody));
            }

            private async Task ServeOnceAsync(string responseBody)
            {
                try
                {
                    using TcpClient client = await listener.AcceptTcpClientAsync();
                    using NetworkStream stream = client.GetStream();
                    byte[] requestBuffer = new byte[2048];
                    _ = await stream.ReadAsync(requestBuffer.AsMemory(), cancellationTokenSource.Token);
                    byte[] body = Encoding.UTF8.GetBytes(responseBody);
                    string header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes.AsMemory(), cancellationTokenSource.Token);
                    await stream.WriteAsync(body.AsMemory(), cancellationTokenSource.Token);
                }
                catch
                {
                    // Ignore shutdown races in tests.
                }
            }

            public async ValueTask DisposeAsync()
            {
                cancellationTokenSource.Cancel();
                listener.Stop();
                try
                {
                    await worker;
                }
                catch
                {
                    // Ignore shutdown races in tests.
                }
                cancellationTokenSource.Dispose();
            }
        }

    }
}
