using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Shared;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsPersonalTest
    {
        [Test]
        public async Task SettingsPersonal_RendersSharedAndRoleGatedSections_ForPrivilegedUser()
        {
            await using BunitContext context = CreateContext(Roles.Admin, Roles.Modeller, Roles.Recertifier, Roles.Reporter);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.FindComponents<Dropdown<Language>>(), Has.Count.EqualTo(1));
                Assert.That(wrapper.Find("#cbx_personal_iconify"), Is.Not.Null);
                Assert.That(wrapper.Find("#elementsPerFetch"), Is.Not.Null);
                Assert.That(wrapper.Find("#recertificationDisplayPeriod"), Is.Not.Null);
                Assert.That(wrapper.Find("#overviewDisplayLines"), Is.Not.Null);
            });
        }

        [Test]
        public async Task SettingsPersonal_HidesRoleGatedSections_ForWorkflowOnlyUser()
        {
            await using BunitContext context = CreateContext(Roles.WorkflowRolesList);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(wrapper.FindComponents<Dropdown<Language>>(), Has.Count.EqualTo(1));
                Assert.That(wrapper.Find("#cbx_personal_iconify"), Is.Not.Null);
                Assert.That(wrapper.FindAll("#elementsPerFetch"), Is.Empty);
                Assert.That(wrapper.FindAll("#recertificationDisplayPeriod"), Is.Empty);
                Assert.That(wrapper.FindAll("#overviewDisplayLines"), Is.Empty);
            });
        }

        [Test]
        public async Task SettingsPersonal_SavePersistsLanguageAndConfigChanges()
        {
            await using BunitContext context = CreateSavingContext(new[] { Roles.Admin, Roles.Modeller, Roles.Recertifier, Roles.Reporter }, out RecordingSettingsApiConn apiConnection, out UserConfig userConfig);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() => Assert.That(wrapper.Find("#cbx_personal_iconify"), Is.Not.Null));

            SettingsPersonal component = wrapper.FindComponent<SettingsPersonal>().Instance;
            SetPrivateField(component, "selectedLanguage", new Language { Name = "German", CultureInfo = "de-DE" });
            ConfigData configData = GetPrivateField<ConfigData>(component, "configData");
            configData.ElementsPerFetch += 1;

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateUserLanguageCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastUpdatedLanguage, Is.EqualTo("German"));
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
                Assert.That(apiConnection.LastUpsertConfigItems.Any(item => item.Key == "elementsPerFetch"), Is.True);
                Assert.That(userConfig.User.Language, Is.EqualTo("German"));
            });
        }

        [Test]
        public async Task SettingsPersonal_SaveSkipsLanguageUpdateWhenLanguageIsUnchanged()
        {
            await using BunitContext context = CreateSavingContext(new[] { Roles.Admin, Roles.Modeller }, out RecordingSettingsApiConn apiConnection, out _);
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderComponent(context);

            wrapper.WaitForAssertion(() => Assert.That(wrapper.Find("#cbx_personal_iconify"), Is.Not.Null));

            SettingsPersonal component = wrapper.FindComponent<SettingsPersonal>().Instance;
            ConfigData configData = GetPrivateField<ConfigData>(component, "configData");
            configData.ElementsPerFetch += 2;

            await InvokePrivateAsync(component, "Save");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.UpdateUserLanguageCallCount, Is.Zero);
                Assert.That(apiConnection.UpsertConfigCallCount, Is.EqualTo(1));
            });
        }

        private static BunitContext CreateContext(params string[] roles)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                UiLanguages =
                [
                    new Language { Name = "English", CultureInfo = "en-US" },
                    new Language { Name = "German", CultureInfo = "de-DE" }
                ]
            };
            SimulatedUserConfig userConfig = new();
            userConfig.User.Language = "English";
            userConfig.User.Roles = [.. roles];

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new PersonalSettingsAuthStateProvider(roles));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static BunitContext CreateSavingContext(string[] roles, out RecordingSettingsApiConn apiConnection, out UserConfig userConfig)
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                UiLanguages =
                [
                    new Language { Name = "English", CultureInfo = "en-US" },
                    new Language { Name = "German", CultureInfo = "de-DE" }
                ]
            };

            apiConnection = new RecordingSettingsApiConn();
            userConfig = new UserConfig(globalConfig, apiConnection, new UiUser
            {
                DbId = 42,
                Language = "English",
                Roles = [.. roles]
            });

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new PersonalSettingsAuthStateProvider(roles));
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
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
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(component.GetType().FullName, fieldName);
            return (T)field.GetValue(component)!;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (_, _, _, _) => { })
                    .AddChildContent<SettingsPersonal>()));
        }

        private sealed class PersonalSettingsAuthStateProvider : AuthenticationStateProvider
        {
            private readonly AuthenticationState authenticationState;

            public PersonalSettingsAuthStateProvider(IEnumerable<string> roles)
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
