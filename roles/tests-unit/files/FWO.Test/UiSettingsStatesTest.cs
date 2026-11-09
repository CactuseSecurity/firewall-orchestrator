using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;
using FWO.Ui.Pages.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsStatesTest
    {
        internal static readonly List<WfState> kTestStates =
        [
            new() { Id = 0, Name = "Open" },
            new() { Id = 1, Name = "Done" }
        ];

        internal static readonly List<WfStateAction> kTestActions =
        [
            new() { Id = 10, Name = "Notify" }
        ];

        private static readonly int[] kExpectedDeleteIds = [1];

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

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        private static MethodInfo GetPrivateMethod(Type componentType, string name)
        {
            return componentType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(componentType.FullName, name);
        }

        private static void SetPrivateField(object component, string fieldName, object? value)
        {
            FieldInfo? field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(component.GetType().FullName, fieldName);
            }
            field.SetValue(component, value);
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

            throw new MissingMemberException(type.FullName, memberName);
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

        private static bool HasVariableValue<T>(object variables, string name, T value)
        {
            PropertyInfo? property = variables.GetType().GetProperty(name);
            return property != null && EqualityComparer<T>.Default.Equals((T)property.GetValue(variables)!, value);
        }

        private static WfStateActionDataHelper StateAction(int actionId, int sortOrder)
        {
            return new WfStateActionDataHelper
            {
                SortOrder = sortOrder,
                Action = new WfStateAction { Id = actionId, Name = $"Action {actionId}" }
            };
        }

        private static BunitContext CreateRenderContext(ApiConnection apiConnection, SimulatedUserConfig? userConfig = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            userConfig ??= new SimulatedUserConfig
            {
                ModIconify = true,
                User = { Roles = [Roles.Admin] }
            };
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsStatesAuthStateProvider(userConfig.User.Roles));
            return context;
        }

        private static IRenderedComponent<TComponent> RenderAuthorized<TComponent>(BunitContext context, Action<ComponentParameterCollectionBuilder<TComponent>>? configure = null)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            if (configure == null)
            {
                return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<TComponent>())
                    .FindComponent<TComponent>();
            }

            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<TComponent>(configure))
                .FindComponent<TComponent>();
        }

        private static void AssertIconifiedButton(string markup, string title, string icon)
        {
            Assert.Multiple(() =>
            {
                Assert.That(markup, Does.Contain($"class=\"{icon}\""));
                Assert.That(markup, Does.Contain("data-toggle=\"tooltip\""));
                Assert.That(markup, Does.Contain($"title=\"{title}\""));
            });
        }

        private static void ChangeCheckboxContaining<TComponent>(IRenderedComponent<TComponent> component, string labelText, bool value)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
        {
            component.FindAll("input[type=checkbox]")
                .First(input => input.ParentElement?.TextContent.Contains(labelText) == true)
                .Change(value);
        }

        [Test]
        public async Task AllowedChangesByApprover_RendersIconifiedFooterButtons()
        {
            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn());
            IRenderedComponent<AllowedChangesByApprover> component = RenderAuthorized<AllowedChangesByApprover>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, new ApproverAllowedChangesConfig().ToConfigValue()));

            Assert.Multiple(() =>
            {
                AssertIconifiedButton(component.Markup, "Save", Icons.Save);
                AssertIconifiedButton(component.Markup, "Cancel", Icons.Cancel);
            });
        }

        [Test]
        public async Task AllowedChangesByApprover_UpdatesConfigAndClosesOnConfirm()
        {
            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn());
            bool displayChanged = true;
            string savedConfig = "";
            IRenderedComponent<AllowedChangesByApprover> component = RenderAuthorized<AllowedChangesByApprover>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, value => displayChanged = value)
                .Add(p => p.ConfigValue, new ApproverAllowedChangesConfig().ToConfigValue())
                .Add(p => p.ConfigValueChanged, value => savedConfig = value));

            ChangeCheckboxContaining(component, "Title", true);
            ChangeCheckboxContaining(component, "Services", true);
            component.FindAll("button").First(button => button.InnerHtml.Contains("Save")).Click();

            ApproverAllowedChangesConfig saved = ApproverAllowedChangesConfig.Parse(savedConfig);
            Assert.Multiple(() =>
            {
                Assert.That(saved.IsTicketFieldAllowed(WorkflowEditableFieldKeys.Title), Is.True);
                Assert.That(saved.IsTaskFieldAllowed(WfTaskType.access, WorkflowEditableFieldKeys.Services), Is.True);
                Assert.That(displayChanged, Is.False);
            });
        }

        [Test]
        public async Task AllowedChangesByApprover_ParsesConfigIntoRenderedCheckboxState()
        {
            ApproverAllowedChangesConfig config = new();
            config.SetTicketField(WorkflowEditableFieldKeys.Reason, true);
            config.SetTaskField(WfTaskType.generic, WorkflowEditableFieldKeys.FreeText, true);
            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn());

            IRenderedComponent<AllowedChangesByApprover> component = RenderAuthorized<AllowedChangesByApprover>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, config.ToConfigValue()));

            List<AngleSharp.Dom.IElement> checkedInputs = component.FindAll("input[type=checkbox]")
                .Where(input => input.HasAttribute("checked")).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(checkedInputs.Any(input => input.ParentElement?.TextContent.Contains("Reason") == true), Is.True);
                Assert.That(checkedInputs.Any(input => input.ParentElement?.TextContent.Contains("free_text") == true), Is.True);
                Assert.That(checkedInputs.Any(input => input.ParentElement?.TextContent.Contains("Priority") == true), Is.False);
            });
        }

        [Test]
        public async Task EditExtStates_LoadsConfiguredAndMissingEnumExternalStates()
        {
            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn());
            IRenderedComponent<EditExtStates> component = RenderAuthorized<EditExtStates>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.States, kTestStates));

            component.WaitForAssertion(() =>
            {
                List<object> staticGroups = ((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates"))
                    .Cast<object>()
                    .ToList();
                Assert.That(staticGroups.Select(group => GetVariable<string>(group, "Name")), Does.Contain(ExtStates.Done.ToString()));
                Assert.That(staticGroups.Select(group => GetVariable<string>(group, "Name")), Does.Contain(ExtStates.ExtReqFailed.ToString()));
                object doneGroup = staticGroups.First(group => GetVariable<string>(group, "Name") == ExtStates.Done.ToString());
                List<object> selectedStates = ((System.Collections.IEnumerable)GetVariable<object>(doneGroup, "SelectedStates"))
                    .Cast<object>()
                    .ToList();
                Assert.That(selectedStates.Select(state => GetVariable<int>(state, "Id")), Does.Contain(0));
            });
        }

        [Test]
        public async Task EditExtStates_ChangingAssignmentRemovesOldMappingAddsNewMappingAndRefreshes()
        {
            SettingsStatesRenderApiConn apiConnection = new();
            await using BunitContext context = CreateRenderContext(apiConnection);
            IRenderedComponent<EditExtStates> component = RenderAuthorized<EditExtStates>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.States, kTestStates));
            component.WaitForAssertion(() => Assert.That(((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates")).Cast<object>(), Is.Not.Empty));
            object staticGroup = ((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates"))
                .Cast<object>()
                .First(group => GetVariable<string>(group, "Name") == ExtStates.Done.ToString());
            GetPrivateMethod(typeof(EditExtStates), "EditExtStateGroup").Invoke(component.Instance, new object?[] { staticGroup });

            await component.InvokeAsync(async () =>
            {
                await (Task)GetPrivateMethod(typeof(EditExtStates), "SetSelectedStates").Invoke(component.Instance, new object?[] { new List<WfState> { kTestStates[1] } })!;
            });

            await component.InvokeAsync(async () => await (Task)GetPrivateMethod(typeof(EditExtStates), "ApplySelection").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(RequestQueries.replaceExtStates));
                Assert.That(apiConnection.Queries.Count(query => query == RequestQueries.getExtStates), Is.GreaterThanOrEqualTo(2));
                object addVariables = apiConnection.Variables.First(variables =>
                    variables.GetType().GetProperty("objects") != null);
                List<int> deleteIds = ((System.Collections.IEnumerable)GetVariable<object>(addVariables, "deleteIds"))
                    .Cast<int>()
                    .ToList();
                List<object> objects = ((System.Collections.IEnumerable)GetVariable<object>(addVariables, "objects"))
                    .Cast<object>()
                    .ToList();
                Assert.That(deleteIds, Is.EqualTo(kExpectedDeleteIds));
                Assert.That(objects.Count, Is.EqualTo(1));
                Assert.That(GetVariable<string>(objects[0], "name"), Is.EqualTo(ExtStates.Done.ToString()));
                Assert.That(GetVariable<int>(objects[0], "state_id"), Is.EqualTo(1));
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectStateMode"), Is.False);
            });
        }

        [Test]
        public async Task EditExtStates_NewManualStateCanBeSavedWithoutInternalMapping()
        {
            SettingsStatesRenderApiConn apiConnection = new();
            await using BunitContext context = CreateRenderContext(apiConnection);
            IRenderedComponent<EditExtStates> component = RenderAuthorized<EditExtStates>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.States, kTestStates));
            component.WaitForAssertion(() => Assert.That(((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates")).Cast<object>(), Is.Not.Empty));

            await component.InvokeAsync(() =>
            {
                GetPrivateMethod(typeof(EditExtStates), "AddManualExtState").Invoke(component.Instance, null);
                return Task.CompletedTask;
            });
            object editGroup = GetPrivateField<object>(component.Instance, "editGroup");
            editGroup.GetType().GetProperty("Name")!.SetValue(editGroup, "ManualExternalState");

            await component.InvokeAsync(async () => await (Task)GetPrivateMethod(typeof(EditExtStates), "ApplySelection").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(RequestQueries.replaceExtStates));
                object addVariables = apiConnection.Variables.First(variables => variables.GetType().GetProperty("objects") != null);
                List<int> deleteIds = ((System.Collections.IEnumerable)GetVariable<object>(addVariables, "deleteIds"))
                    .Cast<int>()
                    .ToList();
                List<object> objects = ((System.Collections.IEnumerable)GetVariable<object>(addVariables, "objects"))
                    .Cast<object>()
                    .ToList();
                Assert.That(deleteIds, Is.Empty);
                Assert.That(objects.Count, Is.EqualTo(1));
                Assert.That(GetVariable<object?>(objects[0], "state_id"), Is.Null);
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectStateMode"), Is.False);
            });
        }

        [Test]
        public async Task EditExtStates_SaveWithoutChangesSkipsReplaceMutation()
        {
            SettingsStatesRenderApiConn apiConnection = new();
            await using BunitContext context = CreateRenderContext(apiConnection);
            IRenderedComponent<EditExtStates> component = RenderAuthorized<EditExtStates>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.States, kTestStates));

            component.WaitForAssertion(() => Assert.That(((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates")).Cast<object>(), Is.Not.Empty));
            object staticGroup = ((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates"))
                .Cast<object>()
                .First(group => GetVariable<string>(group, "Name") == ExtStates.Done.ToString());
            GetPrivateMethod(typeof(EditExtStates), "EditExtStateGroup").Invoke(component.Instance, new object?[] { staticGroup });

            await component.InvokeAsync(async () => await (Task)GetPrivateMethod(typeof(EditExtStates), "ApplySelection").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Not.Contain(RequestQueries.replaceExtStates));
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectStateMode"), Is.False);
            });
        }

        [Test]
        public async Task EditExtStates_UnmappedStaticStateDoesNotInsertNullMapping()
        {
            SettingsStatesRenderApiConn apiConnection = new();
            await using BunitContext context = CreateRenderContext(apiConnection);
            IRenderedComponent<EditExtStates> component = RenderAuthorized<EditExtStates>(context, parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.States, kTestStates));

            component.WaitForAssertion(() => Assert.That(((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates")).Cast<object>(), Is.Not.Empty));
            object staticGroup = ((System.Collections.IEnumerable)GetPrivateField<object>(component.Instance, "staticExternalStates"))
                .Cast<object>()
                .First(group => GetVariable<string>(group, "Name") == ExtStates.ExtReqFailed.ToString());
            GetPrivateMethod(typeof(EditExtStates), "EditExtStateGroup").Invoke(component.Instance, new object?[] { staticGroup });

            await component.InvokeAsync(async () => await (Task)GetPrivateMethod(typeof(EditExtStates), "ApplySelection").Invoke(component.Instance, null)!);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Not.Contain(RequestQueries.replaceExtStates));
                Assert.That(GetPrivateField<bool>(component.Instance, "SelectStateMode"), Is.False);
            });
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
        public void SelectAction_SelectsFirstActionWhenAvailableAndOpensPopup()
        {
            SettingsStates component = new();
            WfStateAction action = new() { Id = 99, Name = "Escalate" };
            SetPrivateField(component, "actions", new List<WfStateAction> { action });

            GetPrivateMethod("SelectAction").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<WfStateAction?>(component, "selectedAction"), Is.SameAs(action));
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.True);
            });
        }

        [Test]
        public void SelectAction_DisablesPopupWhenNoSelectableActionsRemain()
        {
            SettingsStates component = new();
            SetPrivateField(component, "actions", new List<WfStateAction>());

            GetPrivateMethod("SelectAction").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<WfStateAction?>(component, "selectedAction"), Is.Null);
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.False);
            });
        }

        [Test]
        public void Cancel_ClosesAllSettingsStatesEditModes()
        {
            SettingsStates component = new();
            SetPrivateField(component, "EditStateMode", true);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "DeleteStateMode", true);
            SetPrivateField(component, "EditExtStatesMode", true);

            GetPrivateMethod("Cancel").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "DeleteStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditExtStatesMode"), Is.False);
            });
        }

        [Test]
        public void Cancel_RevertsEditsToExistingStateWithoutMutatingListItem()
        {
            SettingsStates component = new();
            WfState originalState = new()
            {
                Id = 2,
                Name = "Old",
                AutomaticOnly = false,
                Actions = [StateAction(10, 1)]
            };
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                originalState
            });

            object?[] editStateArgs = [originalState];
            GetPrivateMethod("EditState").Invoke(component, editStateArgs);

            WfState actState = GetPrivateField<WfState>(component, "actState");
            actState.Name = "Edited";
            actState.AutomaticOnly = true;

            GetPrivateMethod("Cancel").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(originalState.Name, Is.EqualTo("Old"));
                Assert.That(originalState.AutomaticOnly, Is.False);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 2).Name, Is.EqualTo("Old"));
            });
        }

        [Test]
        public async Task SaveState_InEditMode_UpdatesExistingStateWithoutAddingActions()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfState editedState = new()
            {
                Id = 2,
                Name = "Approved",
                AutomaticOnly = false,
                Actions = [StateAction(10, 1)]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Old" }
            });
            SetPrivateField(component, "actState", editedState);
            SetPrivateField(component, "AddStateMode", false);
            SetPrivateField(component, "EditStateMode", true);

            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            List<WfState> states = GetPrivateField<List<WfState>>(component, "states");
            Assert.Multiple(() =>
            {
                Assert.That(states.Select(state => state.Name).ToList(), Is.EqualTo(new List<string> { "Open", "Approved" }));
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.updateState }));
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(GetVariable<int>(apiConn.Variables[0], "id"), Is.EqualTo(2));
                Assert.That(GetVariable<string>(apiConn.Variables[0], "name"), Is.EqualTo("Approved"));
            });
        }

        [Test]
        public async Task EditState_ClearsStaleAddStateModeBeforeSavingExistingState()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfState existingState = new()
            {
                Id = 2,
                Name = "Done"
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                existingState
            });
            SetPrivateField(component, "actState", existingState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", false);

            object?[] editStateArgs = [existingState];
            GetPrivateMethod("EditState").Invoke(component, editStateArgs);
            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.updateState }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
            });
        }

        [Test]
        public async Task SaveState_InEditMode_HandlesDeletedStateWithoutThrowing()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                UpdateStateAffectedRows = 0
            };
            WfState editedState = new()
            {
                Id = 2,
                Name = "Approved",
                AutomaticOnly = false
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Old" }
            });
            SetPrivateField(component, "actState", editedState);
            SetPrivateField(component, "AddStateMode", false);
            SetPrivateField(component, "EditStateMode", true);

            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.updateState }));
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Select(state => state.Name).ToList(), Is.EqualTo(new List<string> { "Open", "Old" }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.True);
            });
        }

        [Test]
        public async Task SaveState_InAddMode_LeavesEditModeEnabledWhenActionInsertFails()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                ThrowOnAddStateAction = true
            };
            WfState addedState = new()
            {
                Id = 3,
                Name = "Review",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(20, 1),
                    StateAction(10, 2)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.createState,
                    RequestQueries.addStateAction
                }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.True);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Select(state => state.Id).ToList(), Is.EqualTo(new List<int> { 3, 5 }));
            });
        }

        [Test]
        public async Task SaveState_InAddMode_RetriesOnlyMissingActionsAfterPartialFailure()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                AddStateActionFailOnCallNumber = 2
            };
            WfState addedState = new()
            {
                Id = 3,
                Name = "Review",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(20, 1),
                    StateAction(10, 2)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task firstAttempt = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await firstAttempt;

            Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.True);
            Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.True);
            Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
            {
                RequestQueries.createState,
                RequestQueries.addStateAction,
                RequestQueries.addStateAction
            }));

            object?[] moveArgs = [addedState.Actions[0], 1];
            Task moveTask = (Task)GetPrivateMethod("MoveActionInState").Invoke(component, moveArgs)!;
            await moveTask;

            Task secondAttempt = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await secondAttempt;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.createState,
                    RequestQueries.addStateAction,
                    RequestQueries.addStateAction,
                    RequestQueries.updateStateActionSortOrder,
                    RequestQueries.updateState,
                    RequestQueries.addStateAction
                }));
                Assert.That(apiConn.Variables.Count(v => HasVariableValue(v, "stateId", 3)), Is.EqualTo(4));
                Assert.That(apiConn.Variables.Count(v => HasVariableValue(v, "actionId", 20)), Is.EqualTo(2));
                Assert.That(apiConn.Variables.Count(v => HasVariableValue(v, "actionId", 10)), Is.EqualTo(2));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Select(state => state.Id).ToList(), Is.EqualTo(new List<int> { 3, 5 }));
                Assert.That(addedState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 10, 20 }));
                Assert.That(addedState.Actions.Select(action => action.SortOrder).ToList(), Is.EqualTo(new List<int> { 1, 2 }));
            });
        }

        [Test]
        public async Task SaveState_InAddMode_PersistsEditedStateFieldsOnRetryAfterPartialFailure()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                AddStateActionFailOnCallNumber = 2
            };
            WfState addedState = new()
            {
                Id = 7,
                Name = "Foo",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(20, 1),
                    StateAction(10, 2)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task firstAttempt = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await firstAttempt;

            addedState.Name = "Bar";
            addedState.AutomaticOnly = false;

            Task secondAttempt = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await secondAttempt;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.createState,
                    RequestQueries.addStateAction,
                    RequestQueries.addStateAction,
                    RequestQueries.updateState,
                    RequestQueries.addStateAction
                }));
                int updateStateQueryIndex = apiConn.Queries.IndexOf(RequestQueries.updateState);
                Assert.That(updateStateQueryIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(GetVariable<string>(apiConn.Variables[updateStateQueryIndex], "name"), Is.EqualTo("Bar"));
                Assert.That(GetVariable<bool>(apiConn.Variables[updateStateQueryIndex], "automaticOnly"), Is.False);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 7).Name, Is.EqualTo("Bar"));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
            });
        }

        [Test]
        public async Task SaveState_InAddMode_DropsUnsavedActionsWhenDialogIsClosedAfterPartialFailure()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                AddStateActionFailOnCallNumber = 2
            };
            WfState addedState = new()
            {
                Id = 3,
                Name = "Review",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(20, 1),
                    StateAction(10, 2)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task firstAttempt = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await firstAttempt;

            addedState.Name = "Renamed";
            addedState.AutomaticOnly = false;

            GetPrivateMethod("CloseEditState").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.createState,
                    RequestQueries.addStateAction,
                    RequestQueries.addStateAction
                }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 3).Actions.Select(action => action.Action.Id).ToList(),
                    Is.EqualTo(new List<int> { 20 }));
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 3).Actions.Select(action => action.SortOrder).ToList(),
                    Is.EqualTo(new List<int> { 1 }));
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 3).Name, Is.EqualTo("Review"));
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Single(state => state.Id == 3).AutomaticOnly, Is.True);
            });
        }

        [Test]
        public async Task SaveState_InAddMode_IgnoresConcurrentReentrantCalls()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                CreateStateGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            WfState addedState = new()
            {
                Id = 3,
                Name = "Review",
                AutomaticOnly = true,
                Actions =
                [
                    StateAction(10, 1)
                ]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState> { new() { Id = 5, Name = "Later" } });
            SetPrivateField(component, "actState", addedState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);

            Task firstCall = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await Task.Yield();
            Task secondCall = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;

            apiConn.CreateStateGate.SetResult(true);
            await Task.WhenAll(firstCall, secondCall);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string>
                {
                    RequestQueries.createState,
                    RequestQueries.addStateAction
                }));
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.False);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
            });
        }

        [Test]
        public async Task DeleteState_RemovesStateAndClosesDeleteMode()
        {
            SettingsStatesRenderApiConn apiConn = new();
            await using BunitContext context = CreateRenderContext(apiConn);
            IRenderedComponent<SettingsStates> component = RenderAuthorized<SettingsStates>(context);
            component.WaitForAssertion(() => Assert.That(component.Markup, Does.Contain("available_states")));
            WfState deletedState = new() { Id = 4, Name = "Rejected" };
            SetPrivateField(component.Instance, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                deletedState
            });
            SetPrivateField(component.Instance, "actState", deletedState);
            SetPrivateField(component.Instance, "DeleteStateMode", true);

            await component.InvokeAsync(async () => await (Task)GetPrivateMethod("DeleteState").Invoke(component.Instance, null)!);

            List<WfState> states = GetPrivateField<List<WfState>>(component.Instance, "states");
            object deleteVariables = apiConn.Variables.First(variables => HasVariableValue(variables, "id", 4));
            Assert.Multiple(() =>
            {
                Assert.That(states.Select(state => state.Id).ToList(), Is.EqualTo(new List<int> { 1 }));
                Assert.That(apiConn.Queries, Does.Contain(RequestQueries.deleteState));
                Assert.That(GetVariable<int>(deleteVariables, "id"), Is.EqualTo(4));
                Assert.That(GetPrivateField<bool>(component.Instance, "DeleteStateMode"), Is.False);
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
                    RequestQueries.createState,
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
        public async Task SaveState_InAddMode_RejectsDuplicateStateId()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            List<(string title, string message, bool isError)> messages = new();
            WfState duplicateState = new()
            {
                Id = 2,
                Name = "Review",
                AutomaticOnly = true
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "states", new List<WfState>
            {
                new() { Id = 1, Name = "Open" },
                new() { Id = 2, Name = "Done" }
            });
            SetPrivateField(component, "actState", duplicateState);
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "EditStateMode", true);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, title, message, isError) =>
                messages.Add((title, message, isError))));

            Task task = (Task)GetPrivateMethod("SaveState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.Empty);
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].title, Is.EqualTo("Edit state"));
                Assert.That(messages[0].message, Is.EqualTo("A workflow state with this id already exists."));
                Assert.That(messages[0].isError, Is.True);
                Assert.That(GetPrivateField<bool>(component, "AddStateMode"), Is.True);
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.True);
                Assert.That(GetPrivateField<List<WfState>>(component, "states").Select(state => state.Name).ToList(), Is.EqualTo(new List<string> { "Open", "Done" }));
            });
        }

        [Test]
        public async Task SettingsStates_LoadsUsedStateIdsFromActionExternalParams()
        {
            List<WfStateAction> actions = new()
            {
                new()
                {
                    Id = 41000,
                    Name = "AutoPromote",
                    ActionType = StateActionTypes.AutoPromote.ToString(),
                    ExternalParams = "41001"
                },
                new()
                {
                    Id = 41001,
                    Name = "ConditionalAutoPromote",
                    ActionType = StateActionTypes.AutoPromote.ToString(),
                    ExternalParams = System.Text.Json.JsonSerializer.Serialize(new ConditionalAutoPromoteParams
                    {
                        IfCompliantState = 41002,
                        IfNotCompliantState = 41003
                    })
                },
                new()
                {
                    Id = 41002,
                    Name = "AddApproval",
                    ActionType = StateActionTypes.AddApproval.ToString(),
                    ExternalParams = System.Text.Json.JsonSerializer.Serialize(new ApprovalParams
                    {
                        StateId = 41004,
                        ApproverGroup = "ops",
                        Deadline = 7
                    })
                }
            };
            List<WfState> states = new()
            {
                new() { Id = 41001, Name = "Promoted" },
                new() { Id = 41002, Name = "Compliant" },
                new() { Id = 41003, Name = "NonCompliant" },
                new() { Id = 41004, Name = "Approval" },
                new() { Id = 41010, Name = "Unrelated" }
            };

            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn(actions, states));
            IRenderedComponent<SettingsStates> component = RenderAuthorized<SettingsStates>(context);

            component.WaitForAssertion(() =>
            {
                List<int> usedStateIds = GetPrivateField<List<int>>(component.Instance, "usedStateIds");
                Assert.Multiple(() =>
                {
                    Assert.That(usedStateIds, Does.Contain(41001));
                    Assert.That(usedStateIds, Does.Contain(41002));
                    Assert.That(usedStateIds, Does.Contain(41003));
                    Assert.That(usedStateIds, Does.Contain(41004));
                    Assert.That(usedStateIds, Does.Not.Contain(41010));
                });
            });
        }

        [Test]
        public async Task SettingsStates_IgnoresInvalidActionExternalParams()
        {
            List<WfStateAction> actions = new()
            {
                new()
                {
                    Id = 42000,
                    Name = "BrokenAutoPromote",
                    ActionType = StateActionTypes.AutoPromote.ToString(),
                    ExternalParams = "{"
                },
                new()
                {
                    Id = 42001,
                    Name = "BrokenAddApproval",
                    ActionType = StateActionTypes.AddApproval.ToString(),
                    ExternalParams = "{"
                }
            };
            List<WfState> states = new()
            {
                new() { Id = 42000, Name = "BrokenAutoPromoteState" },
                new() { Id = 42001, Name = "BrokenAddApprovalState" }
            };

            await using BunitContext context = CreateRenderContext(new SettingsStatesRenderApiConn(actions, states));
            IRenderedComponent<SettingsStates> component = RenderAuthorized<SettingsStates>(context);

            component.WaitForAssertion(() =>
            {
                List<int> usedStateIds = GetPrivateField<List<int>>(component.Instance, "usedStateIds");
                Assert.Multiple(() =>
                {
                    Assert.That(usedStateIds, Does.Not.Contain(42000));
                    Assert.That(usedStateIds, Does.Not.Contain(42001));
                });
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
        public void SelectAction_SkipsActionsAlreadyPresentInState()
        {
            SettingsStates component = new();
            SetPrivateField(component, "actions", new List<WfStateAction>
            {
                new() { Id = 10, Name = "Approve" },
                new() { Id = 20, Name = "Notify" }
            });
            SetPrivateField(component, "actState", new WfState
            {
                Actions = [StateAction(10, 1)]
            });

            GetPrivateMethod("SelectAction").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<WfStateAction?>(component, "selectedAction")?.Id, Is.EqualTo(20));
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.True);
            });
        }

        [Test]
        public void SelectAction_DoesNotOpenPopupWhenNoSelectableActionsRemain()
        {
            SettingsStates component = new();
            SetPrivateField(component, "actions", new List<WfStateAction>
            {
                new() { Id = 10, Name = "Approve" }
            });
            SetPrivateField(component, "actState", new WfState
            {
                Actions = [StateAction(10, 1)]
            });

            GetPrivateMethod("SelectAction").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<WfStateAction?>(component, "selectedAction"), Is.Null);
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.False);
            });
        }

        [Test]
        public async Task AddActionToState_InAddMode_RejectsDuplicateAction()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new();
            WfStateActionDataHelper existingAction = StateAction(10, 1);
            WfState actState = new()
            {
                Id = 4,
                Actions = [existingAction]
            };
            SetInjectedApiConnection(component, apiConn);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetPrivateField(component, "actState", actState);
            SetPrivateField(component, "selectedAction", new WfStateAction { Id = 10, Name = "Notify" });
            SetPrivateField(component, "AddStateMode", true);
            SetPrivateField(component, "SelectActionMode", true);

            Task task = (Task)GetPrivateMethod("AddActionToState").Invoke(component, null)!;
            await task;

            Assert.Multiple(() =>
            {
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 10 }));
                Assert.That(apiConn.Queries, Is.Empty);
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.True);
            });
        }

        [Test]
        public async Task AddActionToState_IgnoresConcurrentReentrantCalls()
        {
            SettingsStates component = new();
            SettingsStatesTestApiConn apiConn = new()
            {
                AddStateActionGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
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

            Task firstCall = (Task)GetPrivateMethod("AddActionToState").Invoke(component, null)!;
            await Task.Yield();
            Task secondCall = (Task)GetPrivateMethod("AddActionToState").Invoke(component, null)!;

            apiConn.AddStateActionGate.SetResult(true);
            await Task.WhenAll(firstCall, secondCall);

            Assert.Multiple(() =>
            {
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.addStateAction }));
                Assert.That(actState.Actions.Select(action => action.Action.Id).ToList(), Is.EqualTo(new List<int> { 10, 30 }));
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

            object?[] removeArgs = [first];
            Task task = (Task)GetPrivateMethod("RemoveActionFromState").Invoke(component, removeArgs)!;
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

            object?[] moveArgs = [second, -1];
            Task task = (Task)GetPrivateMethod("MoveActionInState").Invoke(component, moveArgs)!;
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

            object?[] moveArgs = [first, -1];
            Task task = (Task)GetPrivateMethod("MoveActionInState").Invoke(component, moveArgs)!;
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
        public int CreateStateAffectedRows { get; set; } = 1;
        public int UpdateStateAffectedRows { get; set; } = 1;
        public bool ThrowOnAddStateAction { get; set; }
        public int AddStateActionFailOnCallNumber { get; set; }
        public int AddStateActionCallCount { get; private set; }
        public TaskCompletionSource<bool>? CreateStateGate { get; set; }
        public TaskCompletionSource<bool>? AddStateActionGate { get; set; }

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            if (query == RequestQueries.createState && CreateStateGate != null)
            {
                await CreateStateGate.Task;
            }
            if (query == RequestQueries.addStateAction && AddStateActionGate != null)
            {
                await AddStateActionGate.Task;
            }

            object result = query switch
            {
                string q when q == RequestQueries.createState => new ReturnId { AffectedRows = CreateStateAffectedRows },
                string q when q == RequestQueries.updateState => new ReturnId { AffectedRows = UpdateStateAffectedRows },
                string q when q == RequestQueries.addStateAction && ThrowOnAddStateAction => throw new InvalidOperationException("Simulated add-state-action failure"),
                string q when q == RequestQueries.addStateAction && AddStateActionFailOnCallNumber > 0 && ++AddStateActionCallCount == AddStateActionFailOnCallNumber => throw new InvalidOperationException("Simulated add-state-action failure"),
                string q when q == RequestQueries.addStateAction => new ReturnId { AffectedRows = 1 },
                string q when q == RequestQueries.deleteState => new object(),
                _ => default(QueryResponseType)!
            };

            return (QueryResponseType)result;
        }
    }

    internal sealed class SettingsStatesRenderApiConn : SimulatedApiConnection
    {
        private const string kEmptyStateMatrixConfig = """{"config_value":{}}""";
        private readonly List<WfStateAction> actionCatalog;
        private readonly List<WfState> workflowStates;

        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];
        public int CreateStateAffectedRows { get; set; } = 1;
        public int UpdateStateAffectedRows { get; set; } = 1;
        public bool ThrowOnAddStateAction { get; set; }

        public SettingsStatesRenderApiConn(List<WfStateAction>? actions = null, List<WfState>? states = null)
        {
            actionCatalog = (actions ?? UiSettingsStatesTest.kTestActions)
                .Select(action => new WfStateAction(action))
                .ToList();
            workflowStates = (states ?? UiSettingsStatesTest.kTestStates)
                .Select(CopyState)
                .ToList();
        }

        private static WfState CopyState(WfState state)
        {
            return new WfState
            {
                Id = state.Id,
                Name = state.Name,
                AutomaticOnly = state.AutomaticOnly,
                Actions = state.Actions.Select(action => new WfStateActionDataHelper
                {
                    SortOrder = action.SortOrder,
                    Action = new WfStateAction(action.Action)
                }).ToList()
            };
        }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            object result = query switch
            {
                string q when q == RequestQueries.getActions => actionCatalog.Select(action => new WfStateAction(action)).ToList(),
                string q when q == RequestQueries.getStates => workflowStates.Select(CopyState).ToList(),
                string q when q == RequestQueries.getExtStates => new List<WfExtState>
                {
                    new() { Id = 1, Name = ExtStates.Done.ToString(), StateId = 0 }
                },
                string q when q == RequestQueries.getActiveStateMatrixConfiguration => StateMatrixConfigurationTestHelper.FromLegacyJson(kEmptyStateMatrixConfig),
                string q when q == RequestQueries.createState => new ReturnId { AffectedRows = CreateStateAffectedRows },
                string q when q == RequestQueries.updateState => new ReturnId { AffectedRows = UpdateStateAffectedRows },
                string q when q == RequestQueries.addStateAction && ThrowOnAddStateAction => throw new InvalidOperationException("Simulated add-state-action failure"),
                string q when q == RequestQueries.addStateAction => new ReturnId { AffectedRows = 1 },
                string q when q == RequestQueries.replaceExtStates => new ReturnId
                {
                    AffectedRows = 1
                },
                string q when q == RequestQueries.deleteState => new object(),
                _ => throw new InvalidOperationException($"Unexpected query: {query}")
            };

            return Task.FromResult((QueryResponseType)result);
        }
    }

    internal sealed class SettingsStatesAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(principal));
        }
    }
}
