using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsRecertificationGenTest
    {
        [Test]
        public async Task SettingsRecertificationGen_RendersRuleByRuleControls()
        {
            await using BunitContext context = CreateContext(new SettingsRecertificationTestApiConnection(), CreateGlobalConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#cbx_rec_check_active"), Is.Not.Null);
                Assert.That(wrapper.Find("#cbx_rec_refresh_startup"), Is.Not.Null);
                Assert.That(wrapper.Find("#cbx_rec_refresh_daily"), Is.Not.Null);
                Assert.That(wrapper.Find("#cbx_rec_auto_create_delete_ticket"), Is.Not.Null);
                Assert.That(wrapper.FindAll("button.btn-info"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void SettingsRecertificationGen_PrepareRecCheckParams_NormalizesDayOfMonth()
        {
            SettingsRecertificationGen component = new();
            ConfigData configData = new();
            SetMember(component, "configData", configData);
            SetMember(component, "recCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Weeks,
                RecertCheckOffset = 2,
                RecertCheckWeekday = (int)DayOfWeek.Friday,
                RecertCheckDayOfMonth = 0
            });
            SetMember(component, "selectedDayOfWeek", DayOfWeek.Friday);

            InvokePrivate(component, "PrepareRecCheckParams");

            RecertCheckParams? parsed = JsonSerializer.Deserialize<RecertCheckParams>(configData.RecCheckParams);
            Assert.That(parsed, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsed!.RecertCheckInterval, Is.EqualTo(SchedulerInterval.Weeks));
                Assert.That(parsed.RecertCheckWeekday, Is.EqualTo((int)DayOfWeek.Friday));
                Assert.That(parsed.RecertCheckDayOfMonth, Is.Null);
            });
        }

        [Test]
        public async Task SettingsRecertificationGen_Save_PersistsChangedValues()
        {
            SettingsRecertificationTestApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            SettingsRecertificationGen component = new();
            ConfigData editableConfig = await globalConfig.GetEditableConfig();
            editableConfig.RecertificationPeriod = 99;
            editableConfig.RecertificationNoticePeriod = 11;
            editableConfig.RecertificationDisplayPeriod = 22;
            editableConfig.RuleRemovalGracePeriod = 33;
            editableConfig.CommentRequired = true;
            editableConfig.RecertificationMode = RecertificationMode.RuleByRule;

            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "userConfig", new SimulatedUserConfig());
            SetMember(component, "configData", editableConfig);
            SetMember(component, "recCheckParams", new RecertCheckParams
            {
                RecertCheckInterval = SchedulerInterval.Months,
                RecertCheckOffset = 3,
                RecertCheckDayOfMonth = 0
            });
            SetMember(component, "selectedDayOfWeek", DayOfWeek.Monday);
            SetMember(component, "selectedPriority", new WfPriority { NumPrio = 5, Name = "High" });
            SetMember(component, "selectedState", new WfState { Id = 44, Name = "Done" });

            await InvokeAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastUpsertConfigItems.Count, Is.GreaterThan(0));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recertificationPeriod"), Is.EqualTo("99"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recertificationNoticePeriod"), Is.EqualTo("11"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recertificationDisplayPeriod"), Is.EqualTo("22"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "ruleRemovalGracePeriod"), Is.EqualTo("33"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "commentRequired"), Is.EqualTo("True"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recDeleteRuleTicketPriority"), Is.EqualTo("5"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recDeleteRuleInitState"), Is.EqualTo("44"));
                Assert.That(JsonSerializer.Deserialize<RecertCheckParams>(GetConfigValue(apiConnection.LastUpsertConfigItems, "recCheckParams"))!.RecertCheckDayOfMonth, Is.Null);
            });
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                RecertificationMode = RecertificationMode.RuleByRule,
                RecertificationPeriod = 365,
                InitialRecertificationPeriod = 30,
                RecertificationNoticePeriod = 7,
                RecertificationDisplayPeriod = 14,
                RuleRemovalGracePeriod = 21,
                CommentRequired = false,
                RecCheckActive = true,
                RecRefreshDaily = true,
                RecCheckParams = JsonSerializer.Serialize(new RecertCheckParams()),
                ReqPriorities = "[]"
            };
        }

        private static BunitContext CreateContext(SettingsRecertificationTestApiConnection apiConnection, SimulatedGlobalConfig globalConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsRecertificationAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsRecertificationGen>());
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

        private static void InvokePrivate(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            method.Invoke(instance, null);
        }

        private static async Task InvokeAsync(object instance, string methodName)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            object? result = method.Invoke(instance, null);
            if (result is Task task)
            {
                await task;
            }
        }

        private static string GetConfigValue(List<ConfigItem> items, string key)
        {
            ConfigItem item = items.FirstOrDefault(configItem => configItem.Key == key)
                ?? throw new MissingMemberException(typeof(ConfigItem).FullName, key);
            return item.Value ?? "";
        }

        private sealed class SettingsRecertificationAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public SettingsRecertificationAuthStateProvider(params string[] roles)
            {
                ClaimsIdentity identity = new(
                    roles.Select(role => new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role)),
                    authenticationType: "Test",
                    nameType: System.Security.Claims.ClaimTypes.Name,
                    roleType: System.Security.Claims.ClaimTypes.Role);
                authenticationState = new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(identity));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(authenticationState);
            }
        }

        private sealed class SettingsRecertificationTestApiConnection : SimulatedApiConnection
        {
            public List<ConfigItem> LastUpsertConfigItems { get; private set; } = new();
            public List<WfState> States { get; set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == RequestQueries.getStates && typeof(QueryResponseType) == typeof(List<WfState>))
                {
                    return Task.FromResult((QueryResponseType)(object)States);
                }

                if (query == ConfigQueries.upsertConfigItems)
                {
                    PropertyInfo? configItemsProperty = variables?.GetType().GetProperty("config_items");
                    LastUpsertConfigItems = configItemsProperty == null
                        ? new List<ConfigItem>()
                        : ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
