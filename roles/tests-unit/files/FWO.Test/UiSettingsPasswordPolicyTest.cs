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
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsPasswordPolicyTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["password_policy"] = "Password Policy";
            SimulatedUserConfig.DummyTranslate["U5312"] = "Set the policy for all user passwords";
            SimulatedUserConfig.DummyTranslate["pwMinLength"] = "Min Length";
            SimulatedUserConfig.DummyTranslate["pwUpperCaseRequired"] = "Upper Case Required";
            SimulatedUserConfig.DummyTranslate["pwLowerCaseRequired"] = "Lower Case Required";
            SimulatedUserConfig.DummyTranslate["pwNumberRequired"] = "Number Required";
            SimulatedUserConfig.DummyTranslate["pwSpecialCharactersRequired"] = "Special Characters Required";
            SimulatedUserConfig.DummyTranslate["save"] = "Save";
            SimulatedUserConfig.DummyTranslate["read_config"] = "Read Config";
            SimulatedUserConfig.DummyTranslate["change_policy"] = "Change Password Policy";
            SimulatedUserConfig.DummyTranslate["U5302"] = "Policy changed.";
        }

        [Test]
        public async Task SettingsPasswordPolicy_RendersInputsAndEnabledSaveButtonForAdmin()
        {
            await using BunitContext context = CreateContext(Roles.Admin, out _, out _, out _);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#minLength"), Is.Not.Null);
                Assert.That(wrapper.Find("#upperCaseRequired"), Is.Not.Null);
                Assert.That(wrapper.Find("#lowerCaseRequired"), Is.Not.Null);
                Assert.That(wrapper.Find("#numberRequired"), Is.Not.Null);
                Assert.That(wrapper.Find("#specialCharactersRequired"), Is.Not.Null);
                Assert.That(wrapper.Find("button.btn-primary").HasAttribute("disabled"), Is.False);
            });
        }

        [Test]
        public async Task SettingsPasswordPolicy_RendersDisabledSaveButtonForAuditor()
        {
            await using BunitContext context = CreateContext(Roles.Auditor, out _, out _, out _);

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#minLength"), Is.Not.Null);
                Assert.That(wrapper.Find("button.btn-primary").HasAttribute("disabled"), Is.True);
            });
        }

        [Test]
        public async Task SettingsPasswordPolicy_Save_PersistsChangedValues()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            await using BunitContext context = CreateContext(Roles.Admin, out RecordingConfigApiConnection apiConnection, out _, out _);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context, (exception, title, message, isError) =>
            {
                messages.Add((exception, title, message, isError));
            });
            SettingsPasswordPolicy component = wrapper.FindComponent<SettingsPasswordPolicy>().Instance;

            wrapper.WaitForAssertion(() => Assert.That(GetMember<ConfigData?>(component, "configData"), Is.Not.Null));

            ConfigData editableConfig = GetMember<ConfigData>(component, "configData");
            editableConfig.PwMinLength = 14;
            editableConfig.PwUpperCaseRequired = true;
            editableConfig.PwLowerCaseRequired = true;
            editableConfig.PwNumberRequired = true;
            editableConfig.PwSpecialCharactersRequired = true;

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "pwMinLength"), Is.EqualTo("14"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "pwUpperCaseRequired"), Is.EqualTo("True"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "pwLowerCaseRequired"), Is.EqualTo("True"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "pwNumberRequired"), Is.EqualTo("True"));
                Assert.That(GetConfigValue(apiConnection.LastUpsertConfigItems, "pwSpecialCharactersRequired"), Is.EqualTo("True"));
                Assert.That(messages.Any(entry => entry.Title == "Change Password Policy" && entry.Message == "Policy changed." && entry.IsError == false), Is.True);
            });
        }

        [Test]
        public async Task SettingsPasswordPolicy_ShowsErrorWhenLoadingConfigFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.Dispose();
            SettingsPasswordPolicy component = CreateComponent(Roles.Admin, out _, globalConfig, messages);

            await InvokePrivateTask(component, "OnInitializedAsync");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Read Config"));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(GetMember<ConfigData?>(component, "configData"), Is.Null);
            });
        }

        [Test]
        public async Task SettingsPasswordPolicy_Save_ShowsErrorWhenConfigIsMissing()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            SettingsPasswordPolicy component = CreateComponent(Roles.Admin, out _, new SimulatedGlobalConfig(), messages);
            SetMember(component, "configData", (ConfigData?)null);

            await InvokePrivateTask(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change Password Policy"));
                Assert.That(messages[0].Exception, Is.TypeOf<ArgumentException>());
                Assert.That(messages[0].IsError, Is.True);
            });
        }

        private static BunitContext CreateContext(
            string role,
            out RecordingConfigApiConnection apiConnection,
            out SimulatedUserConfig userConfig,
            out SimulatedGlobalConfig globalConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
            context.Services.AddSingleton<DomEventService>();

            apiConnection = new RecordingConfigApiConnection();
            userConfig = new SimulatedUserConfig();
            userConfig.User.Roles = new List<string> { role };
            globalConfig = new SimulatedGlobalConfig();

            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            return context;
        }

        private static SettingsPasswordPolicy CreateComponent(
            string role,
            out RecordingConfigApiConnection apiConnection,
            SimulatedGlobalConfig globalConfig,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            apiConnection = new RecordingConfigApiConnection();
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { role };
            SettingsPasswordPolicy component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(
            BunitContext context,
            Action<Exception?, string, string, bool>? displayMessageInUi = null)
        {
            Action<Exception?, string, string, bool> callback = displayMessageInUi ?? ((_, _, _, _) => { });
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, callback)
                    .AddChildContent<SettingsPasswordPolicy>()));
        }

        private static void SetMember<T>(object instance, string memberName, T value)
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

            throw new MissingMemberException(type.FullName, memberName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static string GetConfigValue(List<ConfigItem> items, string key)
        {
            ConfigItem item = items.FirstOrDefault(configItem => configItem.Key == key)
                ?? throw new MissingMemberException(typeof(ConfigItem).FullName, key);
            return item.Value ?? "";
        }

        private sealed class RecordingConfigApiConnection : SimulatedApiConnection
        {
            public List<ConfigItem> LastUpsertConfigItems { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ConfigQueries.upsertConfigItems)
                {
                    PropertyInfo? configItemsProperty = variables?.GetType().GetProperty("config_items");
                    LastUpsertConfigItems = configItemsProperty == null
                        ? []
                        : ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
