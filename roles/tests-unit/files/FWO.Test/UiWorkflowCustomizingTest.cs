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
            SetMember(component, "taskTypesActiveDict", Enum.GetValues(typeof(WfTaskType)).Cast<WfTaskType>().ToDictionary(type => type, _ => false));
            SetMember(component, "prioList", new List<WfPriority>());

            Task saveTask = (Task)GetPrivateMethod(typeof(SettingsCustomizing), "Save").Invoke(component, [])!;
            await saveTask;

            Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            ConfigItem flowDbConfig = apiConnection.LastConfigItems.Single(item => item.Key == "reqUseFlowDb");
            Assert.That(flowDbConfig.Value, Is.EqualTo("True"));
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
                Assert.That(dropdowns[0].Instance.ElementToString(FlowIntegrationObjectSelectionOptions.FromFlowDb), Is.EqualTo("flow_integration_from_flowdb"));
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

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
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
