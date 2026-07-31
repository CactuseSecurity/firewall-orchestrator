using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiWorkflowCustomizingTest
    {
        private static MethodInfo GetPrivateMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
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

        [Test]
        public async Task HandleAllowedChangesByApproverChanged_PersistsConfigImmediately()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]"
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            ApproverAllowedChangesConfig newConfig = new();
            newConfig.SetTicketField(WorkflowEditableFieldKeys.Reason, true);

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);

            Task handlerTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "HandleAllowedChangesByApproverChanged")
                .Invoke(component, [newConfig.ToConfigValue()])!;
            await handlerTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("reqAllowedChangesByApprover"));
            Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo(newConfig.ToConfigValue()));
            Assert.That(editableConfig.ReqAllowedChangesByApprover, Is.EqualTo(newConfig.ToConfigValue()));
        }

        [Test]
        public async Task Save_PersistsReqUseFlowDb()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqUseFlowDb = false
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ReqUseFlowDb = true;

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);
            SetMember(component, "taskTypesActiveDict", Enum.GetValues<WfTaskType>().ToDictionary(type => type, _ => false));
            SetMember(component, "prioList", new List<WfPriority>());

            Task saveTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "Save").Invoke(component, [])!;
            await saveTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            ConfigItem flowDbConfig = apiConnection.LastConfigItems.Single(item => item.Key == "reqUseFlowDb");
            Assert.That(flowDbConfig.Value, Is.EqualTo("True"));
        }

        [Test]
        public async Task Save_PersistsApiTicketInitialStateId()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new()
            {
                States = [new WfState { Id = 0, Name = "draft" }, new WfState { Id = 17, Name = "requested" }]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqApiTicketInitialStateId = -1
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ReqApiTicketInitialStateId = 17;

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);
            SetMember(component, "states", apiConnection.States);
            SetMember(component, "stateIds", apiConnection.States.Select(state => state.Id).ToList());
            SetMember(component, "selectedApiTicketInitialStateId", 17);
            SetMember(component, "taskTypesActiveDict", Enum.GetValues<WfTaskType>().ToDictionary(type => type, _ => false));
            SetMember(component, "prioList", new List<WfPriority>());

            Task saveTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "Save").Invoke(component, [])!;
            await saveTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            ConfigItem stateConfig = apiConnection.LastConfigItems.Single(item => item.Key == "reqApiTicketInitialStateId");
            Assert.That(stateConfig.Value, Is.EqualTo("17"));
        }

        [Test]
        public async Task Save_PersistsReqConsiderBundling()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqConsiderBundling = false
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.ReqConsiderBundling = true;

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);
            SetMember(component, "taskTypesActiveDict", Enum.GetValues<WfTaskType>().ToDictionary(type => type, _ => false));
            SetMember(component, "prioList", new List<WfPriority>());

            Task saveTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "Save").Invoke(component, [])!;
            await saveTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            ConfigItem considerBundlingConfig = apiConnection.LastConfigItems.Single(item => item.Key == "reqConsiderBundling");
            Assert.That(considerBundlingConfig.Value, Is.EqualTo("True"));
        }

        [Test]
        public async Task HandleFlowIntegrationChanged_PersistsConfigImmediately()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]"
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            string newValue = new FlowIntegrationConfig
            {
                SelectObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                SelectServices = FlowIntegrationObjectSelectionOptions.Manually,
                SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Minutes
            }.ToConfigValue();

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);

            Task handlerTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "HandleFlowIntegrationChanged")
                .Invoke(component, [newValue])!;
            await handlerTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems[0].Key, Is.EqualTo("reqFlowIntegration"));
            Assert.That(apiConnection.LastConfigItems[0].Value, Is.EqualTo(newValue));
            Assert.That(editableConfig.ReqFlowIntegration, Is.EqualTo(newValue));
        }

        [Test]
        public async Task HandleCreateRequestTaskSortConfigChanged_UpdatesLocalStateOnly()
        {
            SettingsCustomizing component = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]"
            };
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            CreateRequestTaskSortConfig newConfig = new()
            {
                GroupCreatePriority = 6,
                GroupModifyAddPriority = 5,
                AccessPriority = 4,
                RuleModifyPriority = 3,
                RuleDeletePriority = 2,
                GroupModifyRemovePriority = 1,
                GroupDeletePriority = 0,
                AllowTaskSplit = false
            };

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);

            Task handlerTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "HandleCreateRequestTaskSortConfigChanged")
                .Invoke(component, [newConfig.ToConfigValue()])!;
            await handlerTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(0));
            Assert.That(editableConfig.ReqCreateRequestTaskSortConfig, Is.EqualTo(newConfig.ToConfigValue()));
        }

        [Test]
        public async Task SettingsCustomizing_ShowsTaskSortConfigTooltipAndOpensPopup()
        {
            await using BunitContext context = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedUserConfig userConfig = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<AuthenticationStateProvider>(new WorkflowCustomizingAuthStateProvider(Roles.Admin));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<SettingsCustomizing>());

            wrapper.WaitForAssertion(() =>
            {
                IRenderedComponent<SettingsCustomizing> settings = wrapper.FindComponent<SettingsCustomizing>();
                string sortLabelText = userConfig.GetText("reqCreateRequestTaskSortConfig");
                var sortLabel = settings.FindAll("label").Single(label => label.TextContent.Contains(sortLabelText));
                Assert.That(sortLabel.GetAttribute("title"), Is.EqualTo(userConfig.PureLine("C9034")));
                IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = settings.FindComponent<CreateRequestTaskSortConfigPopup>();
                Assert.That(popup.Instance.Display, Is.False);
            });

            IRenderedComponent<SettingsCustomizing> settingsComponent = wrapper.FindComponent<SettingsCustomizing>();
            string expectedLabelText = userConfig.GetText("reqCreateRequestTaskSortConfig");
            var label = settingsComponent.FindAll("label").Single(element => element.TextContent.Contains(expectedLabelText));
            label.ParentElement!.ParentElement!.QuerySelector("button")!.Click();

            wrapper.WaitForAssertion(() =>
            {
                IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = settingsComponent.FindComponent<CreateRequestTaskSortConfigPopup>();
                Assert.That(popup.Instance.Display, Is.True);
                Assert.That(popup.Markup, Does.Contain(userConfig.GetText("allow_task_split")));
                Assert.That(popup.Markup, Does.Contain(userConfig.PureLine("C9033")));
            });
        }

        [Test]
        public async Task CreateRequestTaskSortConfigPopup_RendersRowsAndAllowSplitTooltip()
        {
            await using BunitContext context = new();
            SimulatedUserConfig userConfig = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = context.Render<CreateRequestTaskSortConfigPopup>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, new CreateRequestTaskSortConfig().ToConfigValue()));

            IReadOnlyList<AngleSharp.Dom.IElement> rows = popup.FindAll(".form-group.row.mt-2.align-items-center").ToList();
            AngleSharp.Dom.IElement allowSplitRow = popup.FindAll("div.form-group.row.mt-2")
                .Single(row => row.TextContent.Contains(userConfig.GetText("allow_task_split")));

            Assert.Multiple(() =>
            {
                Assert.That(rows, Has.Count.EqualTo(7));
                Assert.That(rows[0].TextContent, Does.Contain(userConfig.GetText("create_group")));
                Assert.That(rows[1].TextContent, Does.Contain(userConfig.GetText("group_modify") + userConfig.GetText("add_members")));
                Assert.That(rows[6].TextContent, Does.Contain(userConfig.GetText("delete_group")));
                Assert.That(allowSplitRow.GetAttribute("title"), Is.EqualTo(userConfig.PureLine("C9033")));
                Assert.That(allowSplitRow.QuerySelector("input[type=checkbox]")!.HasAttribute("checked"), Is.True);
            });
        }

        [Test]
        public async Task CreateRequestTaskSortConfigPopup_ReordersAndSavesUpdatedPriorities()
        {
            await using BunitContext context = new();
            SimulatedUserConfig userConfig = new();
            WorkflowCustomizingApiConn apiConnection = new()
            {
                States = [new WfState { Id = 0, Name = "draft" }]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };
            bool display = true;
            string? savedValue = null;

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = context.Render<CreateRequestTaskSortConfigPopup>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, EventCallback.Factory.Create<bool>(this, value => display = value))
                .Add(p => p.ConfigValue, new CreateRequestTaskSortConfig().ToConfigValue())
                .Add(p => p.ConfigValueChanged, EventCallback.Factory.Create<string>(this, value => savedValue = value)));

            popup.FindAll(".form-group.row.mt-2.align-items-center")[0].QuerySelectorAll("button")[1].Click();

            popup.WaitForAssertion(() =>
            {
                Assert.That(popup.FindAll(".form-group.row.mt-2.align-items-center")[0].TextContent,
                    Does.Contain(userConfig.GetText("group_modify") + userConfig.GetText("add_members")));
            });

            popup.Find("button.btn.btn-primary").Click();

            CreateRequestTaskSortConfig saved = CreateRequestTaskSortConfig.Parse(savedValue);
            Assert.Multiple(() =>
            {
                Assert.That(display, Is.False);
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastConfigItems.Single(item => item.Key == "reqCreateRequestTaskSortConfig").Value, Is.EqualTo(savedValue));
                Assert.That(saved.GroupModifyAddPriority, Is.EqualTo(0));
                Assert.That(saved.GroupCreatePriority, Is.EqualTo(1));
                Assert.That(saved.AccessPriority, Is.EqualTo(2));
                Assert.That(saved.RuleModifyPriority, Is.EqualTo(3));
                Assert.That(saved.RuleDeletePriority, Is.EqualTo(4));
                Assert.That(saved.GroupModifyRemovePriority, Is.EqualTo(5));
                Assert.That(saved.GroupDeletePriority, Is.EqualTo(6));
                Assert.That(saved.AllowTaskSplit, Is.True);
            });
        }

        [Test]
        public async Task CreateRequestTaskSortConfigPopup_CancelClosesWithoutSaving()
        {
            await using BunitContext context = new();
            SimulatedUserConfig userConfig = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };
            bool display = true;
            bool configChangedCalled = false;

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = context.Render<CreateRequestTaskSortConfigPopup>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.DisplayChanged, EventCallback.Factory.Create<bool>(this, value => display = value))
                .Add(p => p.ConfigValue, new CreateRequestTaskSortConfig().ToConfigValue())
                .Add(p => p.ConfigValueChanged, EventCallback.Factory.Create<string>(this, _ => configChangedCalled = true)));

            popup.FindAll(".btn-group").Last().QuerySelectorAll("button").Last().Click();

            Assert.Multiple(() =>
            {
                Assert.That(display, Is.False);
                Assert.That(configChangedCalled, Is.False);
            });
        }

        [Test]
        public async Task CreateRequestTaskSortConfigPopup_MoveItemUpMovesRowAndKeepsSavedOrder()
        {
            await using BunitContext context = new();
            SimulatedUserConfig userConfig = new();
            WorkflowCustomizingApiConn apiConnection = new()
            {
                States = [new WfState { Id = 0, Name = "draft" }]
            };
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };
            string? savedValue = null;

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = context.Render<CreateRequestTaskSortConfigPopup>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, new CreateRequestTaskSortConfig().ToConfigValue())
                .Add(p => p.ConfigValueChanged, EventCallback.Factory.Create<string>(this, value => savedValue = value)));

            popup.FindAll(".form-group.row.mt-2.align-items-center")[1].QuerySelectorAll("button")[0].Click();

            popup.WaitForAssertion(() =>
            {
                IReadOnlyList<AngleSharp.Dom.IElement> rows = popup.FindAll(".form-group.row.mt-2.align-items-center").ToList();
                Assert.That(rows[0].TextContent, Does.Contain(userConfig.GetText("group_modify") + userConfig.GetText("add_members")));
                Assert.That(rows[1].TextContent, Does.Contain(userConfig.GetText("create_group")));
            });

            popup.Find("button.btn.btn-primary").Click();

            CreateRequestTaskSortConfig saved = CreateRequestTaskSortConfig.Parse(savedValue);
            Assert.That(saved.GroupModifyAddPriority, Is.EqualTo(0));
            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            Assert.That(apiConnection.LastConfigItems.Single(item => item.Key == "reqCreateRequestTaskSortConfig").Value, Is.EqualTo(savedValue));
        }

        [Test]
        public async Task CreateRequestTaskSortConfigPopup_UsesConfiguredPriorityOrder()
        {
            await using BunitContext context = new();
            SimulatedUserConfig userConfig = new();
            WorkflowCustomizingApiConn apiConnection = new();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqCreateRequestTaskSortConfig = new CreateRequestTaskSortConfig().ToConfigValue()
            };
            CreateRequestTaskSortConfig sortConfig = new()
            {
                GroupCreatePriority = 60,
                GroupModifyAddPriority = 10,
                AccessPriority = 40,
                RuleModifyPriority = 20,
                RuleDeletePriority = 30,
                GroupModifyRemovePriority = 50,
                GroupDeletePriority = 0,
                AllowTaskSplit = true
            };

            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<DomEventService>();

            IRenderedComponent<CreateRequestTaskSortConfigPopup> popup = context.Render<CreateRequestTaskSortConfigPopup>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, sortConfig.ToConfigValue()));

            IReadOnlyList<AngleSharp.Dom.IElement> rows = popup.FindAll(".form-group.row.mt-2.align-items-center").ToList();

            Assert.Multiple(() =>
            {
                Assert.That(rows[0].TextContent, Does.Contain(userConfig.GetText("group_delete")));
                Assert.That(rows[1].TextContent, Does.Contain(userConfig.GetText("group_modify") + userConfig.GetText("add_members")));
                Assert.That(rows[2].TextContent, Does.Contain(userConfig.GetText("modify_rule")));
                Assert.That(rows[3].TextContent, Does.Contain(userConfig.GetText("remove_rule")));
                Assert.That(rows[4].TextContent, Does.Contain(userConfig.GetText("access")));
                Assert.That(rows[5].TextContent, Does.Contain(userConfig.GetText("group_modify") + userConfig.GetText("remove_members")));
                Assert.That(rows[6].TextContent, Does.Contain(userConfig.GetText("create_group")));
            });
        }

        [Test]
        public async Task SettingsCustomizing_RendersFlowIntegrationButtonAndPassesConfig()
        {
            await using BunitContext context = new();
            WorkflowCustomizingApiConn apiConnection = new();
            string configValue = new FlowIntegrationConfig
            {
                SelectObjects = FlowIntegrationObjectSelectionOptions.Manually,
                SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                SelectTimeObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Hours
            }.ToConfigValue();
            SimulatedGlobalConfig globalConfig = new()
            {
                ReqAvailableTaskTypes = "[]",
                ReqPriorities = "[]",
                ReqFlowIntegration = configValue
            };
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<AuthenticationStateProvider>(new WorkflowCustomizingAuthStateProvider(Roles.Admin));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<SettingsCustomizing>());

            wrapper.WaitForAssertion(() =>
            {
                IRenderedComponent<SettingsCustomizing> settings = wrapper.FindComponent<SettingsCustomizing>();
                IRenderedComponent<FlowIntegration> flowIntegration = settings.FindComponent<FlowIntegration>();
                Assert.That(settings.Markup, Does.Contain("flow_integration"));
                Assert.That(settings.Markup, Does.Contain("cbx_visibility_based"));
                Assert.That(settings.Markup, Does.Contain("cbx_consider_bundling"));
                Assert.That(flowIntegration.Instance.ConfigValue, Is.EqualTo(configValue));
            });
        }

        [Test]
        public async Task FlowIntegration_LoadsConfigIntoFourDropdowns()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            string configValue = new FlowIntegrationConfig
            {
                SelectObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                SelectServices = FlowIntegrationObjectSelectionOptions.Manually,
                SelectTimeObjects = FlowIntegrationObjectSelectionOptions.Both,
                TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Minutes
            }.ToConfigValue();

            IRenderedComponent<FlowIntegration> component = context.Render<FlowIntegration>(parameters => parameters
                .Add(p => p.Display, true)
                .Add(p => p.ConfigValue, configValue));

            IReadOnlyList<IRenderedComponent<Dropdown<string>>> dropdowns = component.FindComponents<Dropdown<string>>();

            Assert.Multiple(() =>
            {
                Assert.That(dropdowns, Has.Count.EqualTo(4));
                Assert.That(dropdowns[0].Instance.SelectedElement, Is.EqualTo(FlowIntegrationObjectSelectionOptions.FromFlowDb));
                Assert.That(dropdowns[1].Instance.SelectedElement, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Manually));
                Assert.That(dropdowns[2].Instance.SelectedElement, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Both));
                Assert.That(dropdowns[3].Instance.SelectedElement, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Minutes));
                Assert.That(dropdowns[0].Instance.ElementToString(FlowIntegrationObjectSelectionOptions.FromFlowDb), Is.EqualTo("FromFlowDb"));
                Assert.That(dropdowns[3].Instance.ElementToString(FlowIntegrationTimePrecisionOptions.Hours), Is.EqualTo("hours"));
            });
        }

        [Test]
        public async Task FlowIntegration_SaveEmitsSerializedConfigAndCloses()
        {
            await using BunitContext context = new();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            string? savedValue = null;
            bool display = true;
            FlowIntegrationConfig changedConfig = new()
            {
                SelectObjects = FlowIntegrationObjectSelectionOptions.Manually,
                SelectServices = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                SelectTimeObjects = FlowIntegrationObjectSelectionOptions.FromFlowDb,
                TimeObjectPrecision = FlowIntegrationTimePrecisionOptions.Hours
            };

            IRenderedComponent<FlowIntegration> component = context.Render<FlowIntegration>(parameters => parameters
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, EventCallback.Factory.Create<bool>(this, value => display = value))
                .Add(p => p.ConfigValue, new FlowIntegrationConfig().ToConfigValue())
                .Add(p => p.ConfigValueChanged, EventCallback.Factory.Create<string>(this, value => savedValue = value)));

            SetMember(component.Instance, "actConfig", changedConfig);
            component.Find("button.btn-primary").Click();

            FlowIntegrationConfig savedConfig = FlowIntegrationConfig.Parse(savedValue);
            Assert.Multiple(() =>
            {
                Assert.That(display, Is.False);
                Assert.That(savedConfig.SelectObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.Manually));
                Assert.That(savedConfig.SelectServices, Is.EqualTo(FlowIntegrationObjectSelectionOptions.FromFlowDb));
                Assert.That(savedConfig.SelectTimeObjects, Is.EqualTo(FlowIntegrationObjectSelectionOptions.FromFlowDb));
                Assert.That(savedConfig.TimeObjectPrecision, Is.EqualTo(FlowIntegrationTimePrecisionOptions.Hours));
            });
        }

        private sealed class WorkflowCustomizingApiConn : SimulatedApiConnection
        {
            public int UpsertConfigCallCount { get; private set; }
            public List<ConfigItem> LastConfigItems { get; private set; } = [];
            public List<WfState> States { get; set; } = [new WfState { Id = 0, Name = "draft" }];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getStates && typeof(QueryResponseType) == typeof(List<WfState>))
                {
                    return Task.FromResult((QueryResponseType)(object)States);
                }

                if (query == ConfigQueries.upsertConfigItems)
                {
                    UpsertConfigCallCount++;
                    PropertyInfo configItemsProperty = variables?.GetType().GetProperty("config_items")
                        ?? throw new MissingFieldException("config_items");
                    LastConfigItems = ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult((QueryResponseType)(object)new object());
                }

                throw new NotImplementedException();
            }
        }

        private sealed class WorkflowCustomizingAuthStateProvider(params string[] roles) : AuthenticationStateProvider
        {
            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                List<Claim> claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
                ClaimsIdentity identity = new(claims, "Test");
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
            }
        }
    }
}
