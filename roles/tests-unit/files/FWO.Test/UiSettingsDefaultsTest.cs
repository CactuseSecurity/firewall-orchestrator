using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Enums;
using FWO.Middleware.Client;
using FWO.Services.EventMediator.Interfaces;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsDefaultsTest
    {
        private const long kNetworkZoneTreeId = 2;
        private const string kNoneName = "None";
        private const string kNetworkZoneTreeName = "Network Zone Tree";

        /// <summary>
        /// Builds the lookup rows as the API would return them, ordered by name like the real query.
        /// </summary>
        private static List<PathAnalysisAlgorithm> BuildPathAnalysisAlgorithms()
        {
            return
            [
                new PathAnalysisAlgorithm { Id = GlobalConst.kPathAnalysisAlgorithmNone, Name = kNoneName },
                new PathAnalysisAlgorithm { Id = kNetworkZoneTreeId, Name = kNetworkZoneTreeName }
            ];
        }

        [Test]
        public async Task SettingsDefaults_RendersGlobalIconifyToggle()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#cbx_default_iconify"), Is.Not.Null);
            });
        }

        [Test]
        public async Task SettingsDefaults_SavePersistsSelectedLanguageAndIconifySetting()
        {
            await using BunitContext context = CreateSavingContext(out RecordingSettingsApiConn apiConnection);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.Find("#cbx_default_iconify"), Is.Not.Null);
                Assert.That(wrapper.FindComponent<CustomLogoUpload>(), Is.Not.Null);
            });

            SettingsDefaults component = wrapper.FindComponent<SettingsDefaults>().Instance;
            SetPrivateField(component, "selectedLanguage", new Language { Name = "German", CultureInfo = "de-DE" });
            ConfigData configData = GetPrivateField<ConfigData>(component, "configData");
            configData.ModIconify = !configData.ModIconify;

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastUpsertConfigItems.Any(item => item.Key == "DefaultLanguage" && item.Value == "German"), Is.True);
                Assert.That(apiConnection.LastUpsertConfigItems.Any(item => item.Key == "modIconify"), Is.True);
            });
        }

        [Test]
        public async Task SettingsDefaults_SaveRejectsInvalidTokenLifetimes()
        {
            await using BunitContext context = CreateSavingContext(out RecordingSettingsApiConn apiConnection);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() => Assert.That(wrapper.Find("#cbx_default_iconify"), Is.Not.Null));

            SettingsDefaults component = wrapper.FindComponent<SettingsDefaults>().Instance;
            RecordingMessageSink sink = new();
            SetPrivateField(component, "DisplayMessageInUi", sink.Handler);
            SetPrivateField(component, "accessTokenLifetimeValue", 0);
            SetPrivateField(component, "accessTokenLifetimeUnit", TokenLifetimeUnit.Minutes);
            SetPrivateField(component, "refreshTokenLifetimeValue", 1);
            SetPrivateField(component, "refreshTokenLifetimeUnit", TokenLifetimeUnit.Minutes);

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(sink.Messages[0].IsError, Is.True);
                Assert.That(apiConnection.UpsertConfigCallCount, Is.Zero);
            });
        }

        [Test]
        public async Task SettingsDefaults_RendersPathAnalysisAlgorithmOptions()
        {
            await using BunitContext context =
                CreateSavingContext(out RecordingSettingsApiConn apiConnection);

            apiConnection.PathAnalysisAlgorithms = BuildPathAnalysisAlgorithms();

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                IReadOnlyList<IElement> options =
                    wrapper.FindAll("#pathAnalysisAlgorithm option");


                Assert.Multiple(() =>
                {
                    Assert.That(options, Has.Count.EqualTo(2));
                    Assert.That(options[0].GetAttribute("value"), Is.EqualTo(GlobalConst.kPathAnalysisAlgorithmNone.ToString()));
                    Assert.That(options[0].TextContent, Is.EqualTo(kNoneName));
                    Assert.That(options[1].GetAttribute("value"), Is.EqualTo(kNetworkZoneTreeId.ToString()));
                    Assert.That(options[1].TextContent, Is.EqualTo(kNetworkZoneTreeName));
                });
            });
        }

        [Test]
        public async Task SettingsDefaults_PreselectsStoredPathAnalysisAlgorithm()
        {
            await using BunitContext context = CreateSavingContext(
                out RecordingSettingsApiConn apiConnection,
                out SimulatedGlobalConfig globalConfig);

            apiConnection.PathAnalysisAlgorithms = BuildPathAnalysisAlgorithms();
            globalConfig.PathAnalysisAlgorithm = kNetworkZoneTreeId;

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                IElement select = wrapper.Find("#pathAnalysisAlgorithm");
                string? boundValue = select.GetAttribute("value");

                Assert.Multiple(() =>
                {
                    Assert.That(boundValue, Is.EqualTo(kNetworkZoneTreeId.ToString()));
                    Assert.That(
                        wrapper.FindAll("#pathAnalysisAlgorithm option")
                            .Any(option => option.GetAttribute("value") == boundValue),
                        Is.True,
                        "the value bound to the select must match one of its options");
                });
            });
        }

        [Test]
        public async Task SettingsDefaults_SavePersistsSelectedPathAnalysisAlgorithm()
        {
            await using BunitContext context = CreateSavingContext(
                out RecordingSettingsApiConn apiConnection,
                out SimulatedGlobalConfig globalConfig);

            apiConnection.PathAnalysisAlgorithms = BuildPathAnalysisAlgorithms();
            globalConfig.PathAnalysisAlgorithm = GlobalConst.kPathAnalysisAlgorithmNone;

            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);
            wrapper.WaitForAssertion(() =>
                Assert.That(wrapper.Find("#pathAnalysisAlgorithm"), Is.Not.Null));

            wrapper.Find("#pathAnalysisAlgorithm").Change(kNetworkZoneTreeId.ToString());

            SettingsDefaults component = wrapper.FindComponent<SettingsDefaults>().Instance;
            ConfigData configData = GetPrivateField<ConfigData>(component, "configData");

            Assert.That(
                configData.PathAnalysisAlgorithm,
                Is.EqualTo(kNetworkZoneTreeId),
                "the option value has to be parsable into the bound property");

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(
                    apiConnection.LastUpsertConfigItems.Any(item =>
                        item.Key == "pathAnalysisAlgorithm"
                        && item.Value == kNetworkZoneTreeId.ToString()),
                    Is.True);
            });
        }

        private static BunitContext CreateContext()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                UiLanguages =
                [
                    new Language { Name = "English", CultureInfo = "en-US" },
                    new Language { Name = "German", CultureInfo = "de-DE" }
                ]
            };

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new DefaultsSettingsAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new RecordingEventMediator());
            context.Services.AddSingleton<ApiConnection>(new RecordingSettingsApiConn());
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static BunitContext CreateSavingContext(out RecordingSettingsApiConn apiConnection)
        {
            return CreateSavingContext(out apiConnection, out _);
        }

        private static BunitContext CreateSavingContext(
            out RecordingSettingsApiConn apiConnection,
            out SimulatedGlobalConfig globalConfig)
        {
            globalConfig = new()
            {
                UiLanguages =
                [
                    new Language { Name = "English", CultureInfo = "en-US" },
                    new Language { Name = "German", CultureInfo = "de-DE" }
                ]
            };

            apiConnection = new RecordingSettingsApiConn();

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new DefaultsSettingsAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<IEventMediator>(new RecordingEventMediator());
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            return context;
        }

        private static async Task InvokePrivateAsync(object component, string methodName, params object?[]? args)
        {
            MethodInfo method = component.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(component.GetType().FullName, methodName);
            Task task = (Task)(method.Invoke(component, args) ?? throw new InvalidOperationException($"{methodName} returned null task."));
            await task;
        }

        private static void SetPrivateField<T>(object component, string fieldName, T value)
        {
            Type type = component.GetType();
            FieldInfo? field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            PropertyInfo? property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(component, value);
                return;
            }

            throw new MissingMemberException(component.GetType().FullName, fieldName);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            return (T)field.GetValue(component)!;
        }

        private sealed class RecordingMessageSink
        {
            public List<(Exception? Exception, string Title, string Message, bool IsError)> Messages { get; } = [];

            public void Handler(Exception? exception, string title, string message, bool isError)
            {
                Messages.Add((exception, title, message, isError));
            }
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<SettingsDefaults>()));
        }

        private sealed class DefaultsSettingsAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public DefaultsSettingsAuthStateProvider(params string[] roles)
            {
                ClaimsIdentity identity = new(
                    roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    authenticationType: "Test",
                    nameType: ClaimTypes.Name,
                    roleType: ClaimTypes.Role);
                authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(authenticationState);
            }
        }
    }
}
