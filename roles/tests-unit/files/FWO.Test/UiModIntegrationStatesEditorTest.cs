using System.Reflection;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Ui.Pages.Settings;
using Microsoft.AspNetCore.Components;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiModIntegrationStatesEditorTest
    {
        private static readonly List<ModIntegrationState> ExistingStates =
        [
            new() { Name = "Existing", IncludeIntoRequest = true }
        ];

        private static readonly List<ModIntegrationState> OldDisabledState =
        [
            new() { Name = "Old", IncludeIntoRequest = false }
        ];

        private static readonly List<ModIntegrationState> RetryState =
        [
            new() { Name = "Retry", IncludeIntoRequest = true, MonitorStatus = ModIntegrationStateStatus.RequestRunning }
        ];

        [Test]
        public void OnParametersSet_UsesDefaultMarkerWhenMarkerIsBlank()
        {
            ModIntegrationStatesEditor component = CreateComponent();
            SetMember(component, "Display", true);
            SetMember(component, "ConfigValue", ModIntegrationStateConfig.ToConfigValue(ExistingStates));
            SetMember(component, "ConfigMarker", "");

            InvokeOnParametersSet(component);

            Assert.That(GetPrivateField<string>(component, "stateMarker"), Is.EqualTo(ModIntegrationStateConfig.DefaultMarker));
        }

        [Test]
        public void AddState_IgnoresBlankName()
        {
            ModIntegrationStatesEditor component = CreateComponent();
            SetPrivateField(component, "states", new List<ModIntegrationState>());
            SetPrivateField(component, "newState", new ModIntegrationState { Name = "" });

            InvokePrivate(component, "AddState");

            Assert.That(GetPrivateField<List<ModIntegrationState>>(component, "states"), Is.Empty);
        }

        [Test]
        public void AddState_AppendsStateAndResetsEditorRow()
        {
            ModIntegrationStatesEditor component = CreateComponent();
            SetPrivateField(component, "states", new List<ModIntegrationState>());
            SetPrivateField(component, "newState", new ModIntegrationState
            {
                Name = "Retry",
                IncludeIntoRequest = true,
                MonitorStatus = ModIntegrationStateStatus.RequestRunning
            });

            InvokePrivate(component, "AddState");

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<List<ModIntegrationState>>(component, "states"), Has.Count.EqualTo(1));
                Assert.That(GetPrivateField<List<ModIntegrationState>>(component, "states")[0].Name, Is.EqualTo("Retry"));
                Assert.That(GetPrivateField<ModIntegrationState>(component, "newState").Name, Is.Empty);
            });
        }

        [Test]
        public void RemoveState_DeletesSelectedEntry()
        {
            ModIntegrationStatesEditor component = CreateComponent();
            ModIntegrationState first = new() { Name = "First" };
            ModIntegrationState second = new() { Name = "Second" };
            SetPrivateField(component, "states", new List<ModIntegrationState> { first, second });

            InvokePrivate(component, "RemoveState", first);

            Assert.That(GetPrivateField<List<ModIntegrationState>>(component, "states"), Is.EqualTo(new List<ModIntegrationState> { second }));
        }

        [Test]
        public async Task Save_PersistsStatesAndClosesPopup()
        {
            RecordingSettingsApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ModIntegrationStates = ModIntegrationStateConfig.ToConfigValue(OldDisabledState),
                ModIntegrationStateMarker = ""
            };
            ModIntegrationStatesEditor component = CreateComponent(apiConnection, globalConfig);
            string? savedValue = null;
            string? savedMarker = null;
            bool displayClosed = false;
            SetMember(component, "Display", true);
            SetMember(component, "ConfigValue", ModIntegrationStateConfig.ToConfigValue(OldDisabledState));
            SetMember(component, "ConfigMarker", "");
            SetMember(component, "ConfigValueChanged", EventCallback.Factory.Create<string>(new object(), value => savedValue = value));
            SetMember(component, "ConfigMarkerChanged", EventCallback.Factory.Create<string>(new object(), value => savedMarker = value));
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(new object(), value => displayClosed = true));
            InvokeOnParametersSet(component);
            SetPrivateField(component, "states", RetryState.ToList());
            SetPrivateField(component, "stateMarker", " ");

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(savedValue, Is.EqualTo(ModIntegrationStateConfig.ToConfigValue(RetryState)));
                Assert.That(savedMarker, Is.EqualTo(ModIntegrationStateConfig.DefaultMarker));
                Assert.That(displayClosed, Is.True);
                Assert.That(component.Display, Is.False);
            });
        }

        [Test]
        public void DisplayMonitorStatus_UsesTranslatedMonitorStatusText()
        {
            ModIntegrationStatesEditor component = CreateComponent();

            string text = (string)InvokePrivate(component, "DisplayMonitorStatus", ModIntegrationStateStatus.Implemented)!;

            Assert.That(text, Is.EqualTo("Implemented"));
        }

        private static ModIntegrationStatesEditor CreateComponent(RecordingSettingsApiConn? apiConnection = null, SimulatedGlobalConfig? globalConfig = null)
        {
            globalConfig ??= new SimulatedGlobalConfig();
            apiConnection ??= new RecordingSettingsApiConn();

            ModIntegrationStatesEditor component = new()
            ;
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, _, _, _) => { }));
            SetMember(component, "ConfigValue", "");
            SetMember(component, "ConfigMarker", "");
            SetMember(component, "Display", false);
            object callbackContext = new();
            SetMember(component, "ConfigValueChanged", EventCallback.Factory.Create<string>(callbackContext, _ => { }));
            SetMember(component, "ConfigMarkerChanged", EventCallback.Factory.Create<string>(callbackContext, _ => { }));
            SetMember(component, "DisplayChanged", EventCallback.Factory.Create<bool>(callbackContext, _ => { }));
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            return component;
        }

        private static void InvokeOnParametersSet(ModIntegrationStatesEditor component)
        {
            MethodInfo method = typeof(ModIntegrationStatesEditor).GetMethod("OnParametersSet", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(ModIntegrationStatesEditor).FullName, "OnParametersSet");
            method.Invoke(component, null);
        }

        private static object? InvokePrivate(object component, string methodName, params object?[] args)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            return method.Invoke(component, args);
        }

        private static async Task InvokePrivateAsync(object component, string methodName, params object?[] args)
        {
            Task task = (Task)(InvokePrivate(component, methodName, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void SetPrivateField<T>(object component, string fieldName, T value)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            field.SetValue(component, value);
        }

        private static void SetMember(object component, string memberName, object? value)
        {
            Type type = component.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            return (T)field.GetValue(component)!;
        }
    }
}
