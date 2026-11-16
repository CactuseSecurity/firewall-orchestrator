using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the ui parts of the conditional auto-forward with rollout check, error state and "While set" interval.
    /// </summary>
    [TestFixture]
    internal class UiConditionalAutoPromoteTest
    {
        private const int kConditionalStateId = -2;
        private static readonly List<WfState> kStates =
        [
            new() { Id = 249, Name = "Implemented" },
            new() { Id = 260, Name = "In Review" },
            new() { Id = 270, Name = "Rollout Error" }
        ];
        private static readonly List<int> kRealTargetStates = [260, 270];
        private static readonly List<int> kCompliantStateOnly = [260];
        private static readonly List<bool> kNoError = [false];
        private static readonly List<bool> kError = [true];
        private static readonly List<int> kConditionalTargetIds = [WfStateAction.RemainStateId, 249, 260, 270];
        private static readonly List<string> kTicketScopeOnly = [nameof(WfObjectScopes.Ticket)];
        private static readonly List<string> kLegacyScopeAndTicket = [nameof(WfObjectScopes.RequestTask), nameof(WfObjectScopes.Ticket)];
        private static readonly TimeSpan kRenderTimeout = TimeSpan.FromSeconds(15);
        private const int kMissingStateId = 77;
        private const string kMiddlewareWithoutServer = "http://127.0.0.1:1/";

        private static string RolloutCheckParams()
        {
            return ConditionalParams(260, WfStateAction.RemainStateId);
        }

        private static string ConditionalParams(int ifCompliantState, int ifNotCompliantState)
        {
            return JsonSerializer.Serialize(new ConditionalAutoPromoteParams
            {
                ToBeCalled = ToBeCalled.RolloutCheck,
                IfCompliantState = ifCompliantState,
                IfNotCompliantState = ifNotCompliantState,
                ErrorState = 270,
                ErrorConditions = new ConditionalErrorConditions { TimeoutMinutes = 240 }
            });
        }

        private static ConditionalAutoPromoteParams ReadConditionalParams(WfStateAction action)
        {
            return JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
        }

        private static async Task<EditActionAutoPromote> LoadAutoPromote(WfStateAction action)
        {
            EditActionAutoPromote component = new();
            SetMember(component, "States", kStates);
            SetMember(component, "ActAction", action);
            await InvokeAsync(component, "OnParametersSet");
            return component;
        }

        [Test]
        public async Task AutoPromote_ConditionalTargets_OfferRemainButNotAutomatic()
        {
            EditActionAutoPromote component = await LoadAutoPromote(new WfStateAction { ExternalParams = RolloutCheckParams() });

            List<int> conditionalTargetIds = GetMember<List<WfState>>(component, "conditionalTargetStates").Select(state => state.Id).ToList();
            List<int> toStateIds = GetMember<List<WfState>>(component, "statesPlus").Select(state => state.Id).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(conditionalTargetIds, Is.EqualTo(kConditionalTargetIds));
                Assert.That(toStateIds, Does.Contain(WfStateAction.AutomaticStateId));
            });
        }

        [TestCase(WfStateAction.AutomaticStateId, WfStateAction.AutomaticStateId, WfStateAction.RemainStateId, WfStateAction.RemainStateId)]
        [TestCase(260, WfStateAction.AutomaticStateId, 260, WfStateAction.RemainStateId)]
        [TestCase(WfStateAction.AutomaticStateId, 270, WfStateAction.RemainStateId, 270)]
        public async Task AutoPromote_OnParametersSet_LegacyAutomaticTarget_LoadsAndSavesAsRemain(int storedOk, int storedNotOk, int expectedOk, int expectedNotOk)
        {
            WfStateAction action = new() { ExternalParams = ConditionalParams(storedOk, storedNotOk) };

            EditActionAutoPromote component = await LoadAutoPromote(action);

            ConditionalAutoPromoteParams saved = ReadConditionalParams(action);
            Assert.Multiple(() =>
            {
                Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(expectedOk));
                Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(expectedNotOk));
                Assert.That(saved.IfCompliantState, Is.EqualTo(expectedOk));
                Assert.That(saved.IfNotCompliantState, Is.EqualTo(expectedNotOk));
                Assert.That(saved.ToBeCalled, Is.EqualTo(ToBeCalled.RolloutCheck));
                Assert.That(saved.ErrorState, Is.EqualTo(270));
                Assert.That(saved.ErrorConditions.TimeoutMinutes, Is.EqualTo(240));
            });
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithoutLegacyTarget_KeepsStoredParams()
        {
            string storedParams = RolloutCheckParams();
            WfStateAction action = new() { ExternalParams = storedParams };

            await LoadAutoPromote(action);

            Assert.That(action.ExternalParams, Is.EqualTo(storedParams));
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_MissingTargetStates_ShowRemain()
        {
            string storedParams = ConditionalParams(kMissingStateId, kMissingStateId);
            WfStateAction action = new() { ExternalParams = storedParams };

            EditActionAutoPromote component = await LoadAutoPromote(action);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(action.ExternalParams, Is.EqualTo(storedParams));
            });
        }

        [Test]
        public async Task AutoPromote_NewConditionalConfiguration_DefaultsTargetsToRemain()
        {
            WfStateAction action = new();
            EditActionAutoPromote component = await LoadAutoPromote(action);

            await InvokeAsync(component, "OnToStateChanged", new WfState { Id = kConditionalStateId });

            ConditionalAutoPromoteParams saved = ReadConditionalParams(action);
            Assert.Multiple(() =>
            {
                Assert.That(GetMember<WfState>(component, "selectedStateOk").Id, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(saved.IfCompliantState, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(saved.IfNotCompliantState, Is.EqualTo(WfStateAction.RemainStateId));
            });
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_SimpleAutomaticTarget_StaysAutomatic()
        {
            WfStateAction action = new() { ExternalParams = WfStateAction.AutomaticStateId.ToString() };

            EditActionAutoPromote component = await LoadAutoPromote(action);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<WfState>(component, "selectedToState").Id, Is.EqualTo(WfStateAction.AutomaticStateId));
                Assert.That(action.ExternalParams, Is.EqualTo(WfStateAction.AutomaticStateId.ToString()));
            });
        }

        [Test]
        public async Task AutoPromote_UpdateExternalParams_SerializesErrorStateAndTimeout()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = kConditionalStateId });
            SetMember(component, "selectedToBeCalled", ToBeCalled.RolloutCheck);
            SetMember(component, "selectedStateOk", kStates[1]);
            SetMember(component, "selectedStateNotOk", new WfState { Id = WfStateAction.RemainStateId });
            SetMember(component, "selectedErrorState", kStates[2]);
            SetMember(component, "timeoutMinutes", 90);

            await InvokeAsync(component, "UpdateExternalParams");

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.Multiple(() =>
            {
                Assert.That(parameters.ToBeCalled, Is.EqualTo(ToBeCalled.RolloutCheck));
                Assert.That(parameters.IfCompliantState, Is.EqualTo(260));
                Assert.That(parameters.IfNotCompliantState, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(parameters.ErrorState, Is.EqualTo(270));
                Assert.That(parameters.ErrorConditions.TimeoutMinutes, Is.EqualTo(90));
            });
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_LoadsErrorStateTimeoutAndRemainTarget()
        {
            EditActionAutoPromote component = new();
            SetMember(component, "States", kStates);
            SetMember(component, "ActAction", new WfStateAction { ExternalParams = RolloutCheckParams() });

            await InvokeAsync(component, "OnParametersSet");

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<ToBeCalled>(component, "selectedToBeCalled"), Is.EqualTo(ToBeCalled.RolloutCheck));
                Assert.That(GetMember<WfState>(component, "selectedStateOk"), Is.SameAs(kStates[1]));
                Assert.That(GetMember<WfState>(component, "selectedStateNotOk").Id, Is.EqualTo(WfStateAction.RemainStateId));
                Assert.That(GetMember<WfState?>(component, "selectedErrorState"), Is.SameAs(kStates[2]));
                Assert.That(GetMember<int?>(component, "timeoutMinutes"), Is.EqualTo(240));
            });
        }

        [Test]
        public async Task AutoPromote_OnParametersSet_WithoutErrorState_ResetsErrorFields()
        {
            EditActionAutoPromote component = new();
            SetMember(component, "States", kStates);
            SetMember(component, "selectedErrorState", kStates[2]);
            SetMember(component, "timeoutMinutes", 5);
            SetMember(component, "ActAction", new WfStateAction { ExternalParams = "260" });

            await InvokeAsync(component, "OnParametersSet");

            Assert.That(GetMember<WfState?>(component, "selectedErrorState"), Is.Null);
            Assert.That(GetMember<int?>(component, "timeoutMinutes"), Is.Null);
        }

        [TestCase("30", 30)]
        [TestCase("", null)]
        [TestCase("-2", null)]
        [TestCase("0", null)]
        [TestCase("abc", null)]
        public async Task AutoPromote_OnTimeoutChanged_AcceptsOnlyPositiveMinutes(string input, int? expectedMinutes)
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = kConditionalStateId });

            await InvokeAsync(component, "OnTimeoutChanged", new ChangeEventArgs { Value = input });

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.That(parameters.ErrorConditions.TimeoutMinutes, Is.EqualTo(expectedMinutes));
        }

        [Test]
        public async Task AutoPromote_OnErrorStateChanged_SetsAndClearsErrorState()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", new WfState { Id = kConditionalStateId });

            await InvokeAsync(component, "OnErrorStateChanged", kStates[2]);
            int? setErrorState = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!.ErrorState;
            WfState? noErrorState = null;
            await InvokeAsync(component, "OnErrorStateChanged", noErrorState);
            int? clearedErrorState = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!.ErrorState;

            Assert.That(setErrorState, Is.EqualTo(270));
            Assert.That(clearedErrorState, Is.Null);
        }

        [TestCase(ToBeCalled.RolloutCheck, "ifImportedState", "ifNotImportedState", "H5540")]
        [TestCase(ToBeCalled.PolicyCheck, "ifCompliantState", "ifNotCompliantState", "H5528")]
        public void AutoPromote_LabelsDependOnCheck(ToBeCalled toBeCalled, string passedKey, string notPassedKey, string helpKey)
        {
            EditActionAutoPromote component = new();
            SetMember(component, "selectedToBeCalled", toBeCalled);

            Assert.Multiple(() =>
            {
                Assert.That(GetMember<string>(component, "PassedStateTextKey"), Is.EqualTo(passedKey));
                Assert.That(GetMember<string>(component, "NotPassedStateTextKey"), Is.EqualTo(notPassedKey));
                Assert.That(GetMember<string>(component, "CheckHelpTextKey"), Is.EqualTo(helpKey));
            });
        }

        [Test]
        public async Task AutoPromote_Render_RolloutCheck_ShowsTimeoutAndErrorStateButNoPolicies()
        {
            await using BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<EditActionAutoPromote> component = context.Render<EditActionAutoPromote>(parameters => parameters
                .Add(p => p.ActAction, new WfStateAction { ExternalParams = RolloutCheckParams() })
                .Add(p => p.States, kStates));

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#conditionalTimeoutMinutes").GetAttribute("value"), Is.EqualTo("240"));
                Assert.That(component.Find("#conditionalTimeoutMinutes").GetAttribute("min"), Is.EqualTo("1"));
                Assert.That(component.Markup, Does.Contain("errorState"));
                Assert.That(component.Markup, Does.Contain("ifImportedState"));
                Assert.That(component.Markup, Does.Not.Contain("policies"));
            });
        }

        private static string ConfirmedRolloutCheckParams()
        {
            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(RolloutCheckParams())!;
            parameters.ConfirmUiMessage = true;
            return JsonSerializer.Serialize(parameters);
        }

        private static IRenderedComponent<EditActionAutoPromote> RenderAutoPromote(BunitContext context, WfStateAction action)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            return context.Render<EditActionAutoPromote>(parameters => parameters
                .Add(p => p.ActAction, action)
                .Add(p => p.States, kStates));
        }

        [Test]
        public async Task AutoPromote_Render_WhileSet_HidesAndDropsConfirmMessage()
        {
            await using BunitContext context = new();
            WfStateAction action = new() { Event = nameof(StateActionEvents.WhileSet), ExternalParams = ConfirmedRolloutCheckParams() };

            IRenderedComponent<EditActionAutoPromote> component = RenderAutoPromote(context, action);

            ConditionalAutoPromoteParams parameters = JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!;
            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("#confirmAutoPromoteMessage"), Is.Empty);
                Assert.That(parameters.ConfirmUiMessage, Is.False);
                Assert.That(parameters.ErrorConditions.TimeoutMinutes, Is.EqualTo(240));
            });
        }

        [Test]
        public async Task AutoPromote_Render_OnSet_ShowsConfirmMessage()
        {
            await using BunitContext context = new();
            WfStateAction action = new() { Event = nameof(StateActionEvents.OnSet), ExternalParams = ConfirmedRolloutCheckParams() };

            IRenderedComponent<EditActionAutoPromote> component = RenderAutoPromote(context, action);

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("#confirmAutoPromoteMessage").HasAttribute("checked"), Is.True);
                Assert.That(JsonSerializer.Deserialize<ConditionalAutoPromoteParams>(action.ExternalParams)!.ConfirmUiMessage, Is.True);
            });
        }

        [Test]
        public async Task AutoPromote_UpdateExternalParams_WhileSet_StoresNoConfirmForSimpleTarget()
        {
            EditActionAutoPromote component = new();
            WfStateAction action = new() { Event = nameof(StateActionEvents.WhileSet) };
            SetMember(component, "ActAction", action);
            SetMember(component, "selectedToState", kStates[1]);
            SetMember(component, "confirmUiMessage", true);

            await InvokeAsync(component, "UpdateExternalParams");

            Assert.That(action.ExternalParams, Is.EqualTo("260"));
        }

        [Test]
        public async Task General_OnEventChanged_SetsEventAndNotifiesParent()
        {
            EditActionGeneral component = new();
            WfStateAction action = new() { Event = nameof(StateActionEvents.OnSet) };
            string? notifiedEvent = null;
            SetMember(component, "ActAction", action);
            SetMember(component, "EventChanged", EventCallback.Factory.Create<string?>(this, changedEvent => notifiedEvent = changedEvent));

            await InvokeAsync(component, "OnEventChanged", nameof(StateActionEvents.WhileSet));

            Assert.Multiple(() =>
            {
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(notifiedEvent, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
            });
        }

        private static WfStateAction GeneralAction(StateActionTypes actionType, WfObjectScopes scope, StateActionEvents actionEvent)
        {
            return new WfStateAction { ActionType = actionType.ToString(), Scope = scope.ToString(), Event = actionEvent.ToString() };
        }

        private static EditActionGeneral CreateGeneral(WfStateAction action, ParentNotifications notifications)
        {
            EditActionGeneral component = new();
            SetMember(component, "ActAction", action);
            SetMember(component, "ActionTypeChanged", EventCallback.Factory.Create<string?>(notifications, actionType => action.ActionType = actionType ?? ""));
            SetMember(component, "ScopeChanged", EventCallback.Factory.Create<string?>(notifications, scope => notifications.Scope = scope));
            SetMember(component, "EventChanged", EventCallback.Factory.Create<string?>(notifications, actionEvent => notifications.Event = actionEvent));
            return component;
        }

        private static List<string> SelectableEvents(WfStateAction action)
        {
            return GetMember<IEnumerable<string>>(CreateGeneral(action, new ParentNotifications()), "SelectableEvents").ToList();
        }

        private static List<string> SelectableScopes(WfStateAction action)
        {
            return GetMember<IEnumerable<string>>(CreateGeneral(action, new ParentNotifications()), "SelectableScopes").ToList();
        }

        [Test]
        public void General_WhileSet_OffersOnlyTicketScope()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.Ticket, StateActionEvents.WhileSet);

            Assert.That(SelectableScopes(action), Is.EqualTo(kTicketScopeOnly));
        }

        [Test]
        public async Task General_OnEventChanged_WhileSet_NormalizesScopeToTicketAndNotifiesParent()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.RequestTask, StateActionEvents.OnSet);
            action.TaskType = WfTaskType.access.ToString();
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);
            SetMember(component, "selectedTaskType", WfTaskType.access);

            await InvokeAsync(component, "OnEventChanged", nameof(StateActionEvents.WhileSet));

            Assert.Multiple(() =>
            {
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(action.TaskType, Is.Empty);
                Assert.That(GetMember<WfTaskType?>(component, "selectedTaskType"), Is.Null);
                Assert.That(notifications.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(notifications.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
            });
        }

        [Test]
        public async Task General_OnEventChanged_OtherEvent_KeepsScope()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.RequestTask, StateActionEvents.OnSet);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnEventChanged", nameof(StateActionEvents.OnLeave));

            Assert.Multiple(() =>
            {
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.RequestTask)));
                Assert.That(notifications.Event, Is.EqualTo(nameof(StateActionEvents.OnLeave)));
                Assert.That(notifications.Scope, Is.Null);
            });
        }

        [TestCase(WfObjectScopes.RequestTask)]
        [TestCase(WfObjectScopes.ImplementationTask)]
        [TestCase(WfObjectScopes.Approval)]
        public void General_NonTicketScope_DoesNotOfferWhileSet(WfObjectScopes scope)
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, scope, StateActionEvents.OnSet);

            List<string> events = SelectableEvents(action);

            Assert.Multiple(() =>
            {
                Assert.That(events, Does.Not.Contain(nameof(StateActionEvents.WhileSet)));
                Assert.That(events, Does.Contain(nameof(StateActionEvents.OnSet)));
            });
        }

        [TestCase(WfObjectScopes.None)]
        [TestCase(WfObjectScopes.Ticket)]
        public void General_AutoPromote_OffersWhileSetWithTicketOrNoScope(WfObjectScopes scope)
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, scope, StateActionEvents.OnSet);

            Assert.That(SelectableEvents(action), Does.Contain(nameof(StateActionEvents.WhileSet)));
        }

        [Test]
        public async Task General_OnEventChanged_WhileSetWithoutScope_SwitchesToTicketAndNotifiesParent()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.None, StateActionEvents.None);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnEventChanged", nameof(StateActionEvents.WhileSet));

            Assert.Multiple(() =>
            {
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(notifications.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(notifications.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(SelectableScopes(action), Is.EqualTo(kTicketScopeOnly));
            });
        }

        [TestCase(WfObjectScopes.RequestTask)]
        [TestCase(WfObjectScopes.Ticket)]
        public void General_AddApproval_DoesNotOfferWhileSet(WfObjectScopes scope)
        {
            WfStateAction action = GeneralAction(StateActionTypes.AddApproval, scope, StateActionEvents.OnSet);

            Assert.That(SelectableEvents(action), Does.Not.Contain(nameof(StateActionEvents.WhileSet)));
        }

        [TestCase(StateActionTypes.DoNothing)]
        [TestCase(StateActionTypes.SetAlert)]
        [TestCase(StateActionTypes.ExternalCall)]
        [TestCase(StateActionTypes.SendEmail)]
        [TestCase(StateActionTypes.UpdateConnectionOwner)]
        [TestCase(StateActionTypes.UpdateConnectionRelease)]
        [TestCase(StateActionTypes.UpdateConnectionReject)]
        [TestCase(StateActionTypes.UpdateModelling)]
        [TestCase(StateActionTypes.CreateFlow)]
        [TestCase(StateActionTypes.BundleTasks)]
        [TestCase(StateActionTypes.CreateImplTasks)]
        public void General_ActionTypeWithoutWhileSet_DoesNotOfferIt(StateActionTypes actionType)
        {
            WfStateAction ticketAction = GeneralAction(actionType, WfObjectScopes.Ticket, StateActionEvents.OnSet);
            WfStateAction actionWithoutScope = GeneralAction(actionType, WfObjectScopes.None, StateActionEvents.OnSet);

            Assert.Multiple(() =>
            {
                Assert.That(SelectableEvents(ticketAction), Does.Not.Contain(nameof(StateActionEvents.WhileSet)));
                Assert.That(SelectableEvents(actionWithoutScope), Does.Not.Contain(nameof(StateActionEvents.WhileSet)));
                Assert.That(SelectableEvents(ticketAction), Does.Contain(nameof(StateActionEvents.OnSet)));
            });
        }

        [Test]
        public void General_ExistingWhileSetWithOtherScope_RemainsVisibleForEditing()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.RequestTask, StateActionEvents.WhileSet);

            Assert.Multiple(() =>
            {
                Assert.That(SelectableEvents(action)[0], Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(SelectableScopes(action), Is.EqualTo(kLegacyScopeAndTicket));
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.RequestTask)));
            });
        }

        [Test]
        public void General_ExistingWhileSetOfOtherActionType_RemainsVisibleWithoutRestrictingScope()
        {
            WfStateAction action = GeneralAction(StateActionTypes.SendEmail, WfObjectScopes.Ticket, StateActionEvents.WhileSet);

            List<string> events = SelectableEvents(action);

            Assert.Multiple(() =>
            {
                Assert.That(events[0], Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(events.Count(actionEvent => actionEvent == nameof(StateActionEvents.WhileSet)), Is.EqualTo(1));
                Assert.That(SelectableScopes(action), Is.EqualTo(Enum.GetNames<WfObjectScopes>()));
            });
        }

        [Test]
        public async Task General_ActionTypeChangeToAddApproval_DropsWhileSetAndNotifiesParent()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.Ticket, StateActionEvents.WhileSet);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnActionTypeChanged", nameof(StateActionTypes.AddApproval));

            Assert.Multiple(() =>
            {
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.RequestTask)));
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.None)));
                Assert.That(notifications.Scope, Is.EqualTo(nameof(WfObjectScopes.RequestTask)));
                Assert.That(notifications.Event, Is.EqualTo(nameof(StateActionEvents.None)));
            });
        }

        [TestCase(StateActionTypes.SendEmail)]
        [TestCase(StateActionTypes.SetAlert)]
        [TestCase(StateActionTypes.CreateFlow)]
        public async Task General_ActionTypeChangeToTypeWithoutWhileSet_DropsWhileSetAndKeepsScope(StateActionTypes actionType)
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.Ticket, StateActionEvents.WhileSet);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnActionTypeChanged", actionType.ToString());

            Assert.Multiple(() =>
            {
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.None)));
                Assert.That(notifications.Scope, Is.Null);
                Assert.That(notifications.Event, Is.EqualTo(nameof(StateActionEvents.None)));
            });
        }

        [Test]
        public async Task General_ActionTypeChangeToAutoPromote_KeepsWhileSetAndMovesLegacyScopeToTicket()
        {
            WfStateAction action = GeneralAction(StateActionTypes.SendEmail, WfObjectScopes.RequestTask, StateActionEvents.WhileSet);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnActionTypeChanged", nameof(StateActionTypes.AutoPromote));

            Assert.Multiple(() =>
            {
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.WhileSet)));
                Assert.That(notifications.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(notifications.Event, Is.Null);
            });
        }

        [Test]
        public async Task General_ActionTypeChange_WithoutNormalization_DoesNotNotifyScopeOrEvent()
        {
            WfStateAction action = GeneralAction(StateActionTypes.AutoPromote, WfObjectScopes.Ticket, StateActionEvents.OnSet);
            ParentNotifications notifications = new();
            EditActionGeneral component = CreateGeneral(action, notifications);

            await InvokeAsync(component, "OnActionTypeChanged", nameof(StateActionTypes.SendEmail));

            Assert.Multiple(() =>
            {
                Assert.That(action.Scope, Is.EqualTo(nameof(WfObjectScopes.Ticket)));
                Assert.That(action.Event, Is.EqualTo(nameof(StateActionEvents.OnSet)));
                Assert.That(notifications.Scope, Is.Null);
                Assert.That(notifications.Event, Is.Null);
            });
        }

        private sealed class ParentNotifications
        {
            public string? Scope { get; set; }

            public string? Event { get; set; }
        }

        [Test]
        public void DisplayState_RemainState_UsesText()
        {
            SimulatedUserConfig userConfig = new();

            string display = DisplayService.DisplayStateWithId(userConfig, new WfState { Id = WfStateAction.RemainStateId, Name = "remain" });

            Assert.That(display, Is.EqualTo(userConfig.GetText("remain_in_state")));
        }

        [Test]
        public async Task SettingsStates_ConditionalTargets_IncludeErrorStateAndSkipPseudoStates()
        {
            SettingsStates component = new();
            SetMember(component, "usedStateIds", new List<int>());
            ConditionalAutoPromoteParams withErrorState = new() { IfCompliantState = 260, IfNotCompliantState = WfStateAction.RemainStateId, ErrorState = 270 };
            ConditionalAutoPromoteParams withoutErrorState = new() { IfCompliantState = 260, IfNotCompliantState = WfStateAction.AutomaticStateId };

            await InvokeAsync(component, "AddConditionalTargetStates", withErrorState);
            List<int> withErrorStateIds = [.. GetMember<List<int>>(component, "usedStateIds")];
            SetMember(component, "usedStateIds", new List<int>());
            await InvokeAsync(component, "AddConditionalTargetStates", withoutErrorState);

            Assert.That(withErrorStateIds, Is.EqualTo(kRealTargetStates));
            Assert.That(GetMember<List<int>>(component, "usedStateIds"), Is.EqualTo(kCompliantStateOnly));
        }

        [Test]
        public async Task SettingsActions_SaveWhileSetActionSleepTime_WritesConfigItem()
        {
            SimulatedGlobalConfig globalConfig = new();
            ConfigData configData = await globalConfig.GetEditableConfig();
            configData.WhileSetActionSleepTime = 30;
            RecordingApiConnection apiConnection = new();
            MessageRecorder messages = new();
            SettingsActions component = CreateSettingsActions(globalConfig, apiConnection, messages);
            SetMember(component, "configData", configData);

            await InvokeAsync(component, "SaveWhileSetActionSleepTime");

            List<ConfigItem> writtenItems = apiConnection.ConfigItems ?? [];
            Assert.Multiple(() =>
            {
                Assert.That(writtenItems.Single(item => item.Key == "whileSetActionSleepTime").Value, Is.EqualTo("30"));
                Assert.That(messages.ErrorFlags, Is.EqualTo(kNoError));
            });
        }

        [Test]
        public async Task SettingsActions_SaveWithoutLoadedConfig_ShowsError()
        {
            RecordingApiConnection apiConnection = new();
            MessageRecorder messages = new();
            SettingsActions component = CreateSettingsActions(new SimulatedGlobalConfig(), apiConnection, messages);

            await InvokeAsync(component, "SaveWhileSetActionSleepTime");

            Assert.That(messages.ErrorFlags, Is.EqualTo(kError));
            Assert.That(apiConnection.Queries, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task SettingsActions_Render_IntervalUsesFormControlAndSaveButtonWithIcon(bool isAdmin)
        {
            SimulatedUserConfig userConfig = new() { ReqAvailableTaskTypes = "[\"access\"]" };
            if (isAdmin)
            {
                userConfig.User.Roles = new List<string> { Roles.Admin };
            }
            await using BunitContext context = CreateSettingsActionsContext(userConfig);

            IRenderedComponent<CascadingAuthenticationState> root = context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<SettingsActions>());
            IElement input = root.WaitForElement("#whileSetActionSleepTime", kRenderTimeout);
            IElement saveButton = root.Find("form button");

            Assert.Multiple(() =>
            {
                Assert.That(input.ClassList, Does.Contain("form-control"));
                Assert.That(input.ClassList, Does.Contain("form-control-sm"));
                Assert.That(saveButton.ClassName, Is.EqualTo("btn btn-sm btn-primary"));
                Assert.That(saveButton.QuerySelector("span")?.ClassName, Is.EqualTo(Icons.Save));
                Assert.That(saveButton.HasAttribute("disabled"), Is.EqualTo(!isAdmin));
            });
        }

        private static BunitContext CreateSettingsActionsContext(UserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AnonymousAuthStateProvider());
            context.Services.AddSingleton<ApiConnection>(new RecordingApiConnection());
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton(userConfig);
            context.Services.AddSingleton(new MiddlewareClient(kMiddlewareWithoutServer));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            return context;
        }

        private sealed class AnonymousAuthStateProvider : AuthenticationStateProvider
        {
            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
            }
        }

        private static SettingsActions CreateSettingsActions(GlobalConfig globalConfig, ApiConnection apiConnection, MessageRecorder messages)
        {
            SettingsActions component = new();
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "DisplayMessageInUi", new Action<Exception?, string, string, bool>(messages.Record));
            return component;
        }

        private sealed class MessageRecorder
        {
            public List<bool> ErrorFlags { get; } = [];

            public void Record(Exception? exception, string title, string message, bool errorFlag)
            {
                ErrorFlags.Add(errorFlag);
            }
        }

        private sealed class RecordingApiConnection : JobTestApiConnectionBase
        {
            public List<ConfigItem>? ConfigItems { get; private set; }

            protected override Task<QueryResponseType> HandleQueryAsync<QueryResponseType>(string query, object? variables,
                string? operationName, QueryChunkingOptions? chunkingOptions)
            {
                if (query == ConfigQueries.upsertConfigItems)
                {
                    ConfigItems = (List<ConfigItem>)variables!.GetType().GetProperty("config_items")!.GetValue(variables)!;
                }
                return ReturnEmptyOrDefault<QueryResponseType>();
            }
        }

        private static async Task InvokeAsync(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            if (method.Invoke(instance, args) is Task task)
            {
                await task;
            }
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
            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(type.FullName, memberName);
            field.SetValue(instance, value);
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }
            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(type.FullName, memberName);
            return (T)field.GetValue(instance)!;
        }
    }
}
