using System.Reflection;
using System.Text.Json;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
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

        private static string RolloutCheckParams()
        {
            return JsonSerializer.Serialize(new ConditionalAutoPromoteParams
            {
                ToBeCalled = ToBeCalled.RolloutCheck,
                IfCompliantState = 260,
                IfNotCompliantState = WfStateAction.RemainStateId,
                ErrorState = 270,
                ErrorConditions = new ConditionalErrorConditions { TimeoutMinutes = 240 }
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
