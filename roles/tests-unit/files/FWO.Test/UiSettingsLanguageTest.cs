using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsLanguageTest
    {
        [SetUp]
        public void SetUp()
        {
            SimulatedUserConfig.DummyTranslate["language_settings"] = "Language settings";
            SimulatedUserConfig.DummyTranslate["U5412"] = "Select the UI language";
            SimulatedUserConfig.DummyTranslate["language"] = "Language";
            SimulatedUserConfig.DummyTranslate["apply_changes"] = "Apply changes";
            SimulatedUserConfig.DummyTranslate["change_language"] = "Change language";
        }

        [Test]
        public async Task SettingsLanguage_RendersCurrentLanguageSelection()
        {
            RecordingLanguageApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            UserConfig userConfig = CreateUserConfig(globalConfig, apiConnection, "English");
            userConfig.User.Language = "English";

            await using BunitContext context = CreateContext(apiConnection, globalConfig, userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Dropdown<Language> dropdown = wrapper.FindComponent<Dropdown<Language>>().Instance;
                Assert.That(dropdown.SelectedElement, Is.Not.Null);
                Assert.That(dropdown.SelectedElement!.Name, Is.EqualTo("English"));
                Assert.That(wrapper.FindAll("button.btn-primary"), Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task SettingsLanguage_ChangeLanguage_UpdatesUserConfigAndPersistsSelection()
        {
            RecordingLanguageApiConnection apiConnection = new();
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            UserConfig userConfig = CreateUserConfig(globalConfig, apiConnection, "English");
            userConfig.User.DbId = 77;
            SettingsLanguage component = CreateComponent(apiConnection, globalConfig, userConfig);

            await InvokePrivateTask(component, "ChangeLanguage", new Language { Name = "German", CultureInfo = "de-DE" });

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.updateUserLanguage));
                Assert.That(apiConnection.Queries, Does.Contain(ConfigQueries.getCustomTextsPerLanguage));
                Assert.That(apiConnection.LastLanguage, Is.EqualTo("German"));
                Assert.That(userConfig.User.Language, Is.EqualTo("German"));
            });
        }

        [Test]
        public async Task SettingsLanguage_ChangeLanguage_ShowsErrorWhenUpdateFails()
        {
            List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
            RecordingLanguageApiConnection apiConnection = new()
            {
                ThrowOnUpdateLanguage = true
            };
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            UserConfig userConfig = CreateUserConfig(globalConfig, apiConnection, "English");
            SettingsLanguage component = CreateComponent(apiConnection, globalConfig, userConfig, messages);

            await InvokePrivateTask(component, "ChangeLanguage", new Language { Name = "German", CultureInfo = "de-DE" });

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].Title, Is.EqualTo("Change language"));
                Assert.That(messages[0].IsError, Is.True);
                Assert.That(userConfig.User.Language, Is.EqualTo("English"));
            });
        }

        private static BunitContext CreateContext(
            RecordingLanguageApiConnection apiConnection,
            SimulatedGlobalConfig globalConfig,
            UserConfig userConfig)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static SettingsLanguage CreateComponent(
            RecordingLanguageApiConnection apiConnection,
            SimulatedGlobalConfig globalConfig,
            UserConfig userConfig,
            List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
        {
            SettingsLanguage component = new();
            SetMember(component, "apiConnection", apiConnection);
            SetMember(component, "globalConfig", globalConfig);
            SetMember(component, "userConfig", userConfig);
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((exception, title, message, isError) =>
            {
                messages?.Add((exception, title, message, isError));
            }));
            return component;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<SettingsLanguage>()));
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            return new SimulatedGlobalConfig
            {
                UiLanguages =
                [
                    new Language { Name = "English", CultureInfo = "en-US" },
                    new Language { Name = "German", CultureInfo = "de-DE" }
                ]
            };
        }

        private static UserConfig CreateUserConfig(SimulatedGlobalConfig globalConfig, RecordingLanguageApiConnection apiConnection, string language)
        {
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, language);
            userConfig.User.DbId = 77;
            return userConfig;
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

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(instance, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private sealed class RecordingLanguageApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = [];
            public string? LastLanguage { get; private set; }
            public bool ThrowOnUpdateLanguage { get; set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == AuthQueries.updateUserLanguage && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    if (ThrowOnUpdateLanguage)
                    {
                        throw new InvalidOperationException("update failed");
                    }
                    LastLanguage = variables?.GetType().GetProperty("language")?.GetValue(variables)?.ToString();
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = 1 });
                }

                if (query == ConfigQueries.getCustomTextsPerLanguage && typeof(QueryResponseType) == typeof(List<UiText>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<UiText>());
                }

                if (query == ConfigQueries.getConfigItemsByUser && typeof(QueryResponseType) == typeof(ConfigItem[]))
                {
                    return Task.FromResult((QueryResponseType)(object)Array.Empty<ConfigItem>());
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
