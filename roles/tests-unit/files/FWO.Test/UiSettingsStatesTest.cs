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
                Assert.That(deleteIds, Is.EqualTo(new[] { 1 }));
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
        public void SelectAction_AllowsEmptyActionCatalogAndStillOpensPopup()
        {
            SettingsStates component = new();
            SetPrivateField(component, "actions", new List<WfStateAction>());

            GetPrivateMethod("SelectAction").Invoke(component, null);

            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<WfStateAction?>(component, "selectedAction"), Is.Null);
                Assert.That(GetPrivateField<bool>(component, "SelectActionMode"), Is.True);
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
                Assert.That(apiConn.Queries, Is.EqualTo(new List<string> { RequestQueries.upsertState }));
                Assert.That(GetPrivateField<bool>(component, "EditStateMode"), Is.False);
                Assert.That(GetVariable<int>(apiConn.Variables[0], "id"), Is.EqualTo(2));
                Assert.That(GetVariable<string>(apiConn.Variables[0], "name"), Is.EqualTo("Approved"));
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

    internal sealed class SettingsStatesRenderApiConn : SimulatedApiConnection
    {
        private const string kEmptyStateMatrixConfig = """{"config_value":{}}""";

        public List<string> Queries { get; } = [];
        public List<object> Variables { get; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            if (variables != null)
            {
                Variables.Add(variables);
            }

            object result = query switch
            {
                string q when q == RequestQueries.getActions => UiSettingsStatesTest.kTestActions,
                string q when q == RequestQueries.getStates => UiSettingsStatesTest.kTestStates.Select(state => new WfState
                {
                    Id = state.Id,
                    Name = state.Name,
                    AutomaticOnly = state.AutomaticOnly,
                    Actions = [.. state.Actions]
                }).ToList(),
                string q when q == RequestQueries.getExtStates => new List<WfExtState>
                {
                    new() { Id = 1, Name = ExtStates.Done.ToString(), StateId = 0 }
                },
                string q when q == ConfigQueries.getConfigItemByKey => new List<GlobalStateMatrixHelper>
                {
                    new() { ConfData = kEmptyStateMatrixConfig }
                },
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
