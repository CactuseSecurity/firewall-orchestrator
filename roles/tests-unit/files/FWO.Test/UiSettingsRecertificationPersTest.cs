using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
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
    internal class UiSettingsRecertificationPersTest
    {
        [Test]
        public async Task SettingsRecertificationPers_RendersDisplayPeriodInput()
        {
            await using BunitContext context = CreateContext(new SettingsRecertificationPersTestApiConnection(), new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#recertificationDisplayPeriod"), Is.Not.Null);
                Assert.That(wrapper.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SettingsRecertificationPers_Save_PersistsDisplayPeriod()
        {
            SettingsRecertificationPers component = new();
            SettingsRecertificationPersTestApiConnection apiConnection = new();
            SimulatedUserConfig userConfig = new();
            ConfigData editableConfig = await userConfig.GetEditableConfig();
            editableConfig.RecertificationDisplayPeriod = 42;

            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", new SimulatedGlobalConfig());
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "configData", editableConfig);

            await InvokeAsync(component, "SaveRecertificationDisplayPeriod");

            Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "recertificationDisplayPeriod"), Is.EqualTo("42"));
        }

        private static BunitContext CreateContext(SettingsRecertificationPersTestApiConnection apiConnection, SimulatedUserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsRecertificationAuthStateProvider(Roles.Admin));
            context.Services.AddLocalization();
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsRecertificationPers>());
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

        private sealed class SettingsRecertificationPersTestApiConnection : SimulatedApiConnection
        {
            public List<ConfigItem> LastUpsertConfigItems { get; private set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
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
