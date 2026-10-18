using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    /// <summary>
    /// Tests the credential selection of external ticket systems: only credentials usable for basic authentication
    /// are offered, and a reference that can no longer be resolved is not kept (GHSA-jhj7-f9vj-r9v8).
    /// </summary>
    [TestFixture]
    internal class UiSettingsExternalWorkflowListTest
    {
        private const int kPasswordCredentialId = 4;
        private const int kKeyPairCredentialId = 3;
        private const int kDeletedCredentialId = 9;

        [Test]
        public void OnlyPasswordCredentialsAreOffered()
        {
            using BunitContext context = new();
            SettingsExternalWorkflowList page = RenderPage(context);

            List<Credential> credentials = GetField<List<Credential>>(page, "credentials");

            Assert.That(credentials.Select(credential => credential.Id), Is.EqualTo(new List<int> { kPasswordCredentialId }));
        }

        [TestCase(kPasswordCredentialId, kPasswordCredentialId)]
        [TestCase(kKeyPairCredentialId, null)]
        [TestCase(kDeletedCredentialId, null)]
        [TestCase(null, null)]
        public void EditKeepsOnlySelectableCredential(int? storedCredentialId, int? expectedCredentialId)
        {
            using BunitContext context = new();
            SettingsExternalWorkflowList page = RenderPage(context);
            ExternalTicketSystem system = new() { Id = 1, Name = "Tufin", TypeId = BuiltInExternalTicketSystemTypes.TufinSecureChangeId, CredentialId = storedCredentialId };

            InvokePrivate(page, "EditExternalTicketSystem", system);

            Assert.Multiple(() =>
            {
                Assert.That(GetField<ExternalTicketSystem>(page, "editModel").CredentialId, Is.EqualTo(expectedCredentialId));
                Assert.That(GetField<Credential?>(page, "selectedCredential")?.Id, Is.EqualTo(expectedCredentialId));
            });
        }

        [TestCase(Roles.Auditor, false)]
        [TestCase(Roles.Admin, true)]
        public async Task AddAndEditOpenForAdminAndAuditorButOnlyAdminCanSaveOrDelete(string role, bool writeAllowed)
        {
            await using BunitContext context = new();
            IRenderedComponent<SettingsExternalWorkflowList> rendered = RenderPageAs(context, role);
            SetField(rendered.Instance, "extTicketSystems", new List<ExternalTicketSystem> { new() { Id = 1, Name = "Generic", TypeId = BuiltInExternalTicketSystemTypes.GenericId } });
            rendered.Render();

            Assert.Multiple(() =>
            {
                Assert.That(FindButton(rendered, "add_ext_ticket_system").HasAttribute("disabled"), Is.False);
                Assert.That(FindButton(rendered, "edit").HasAttribute("disabled"), Is.False);
                Assert.That(FindButton(rendered, "delete").HasAttribute("disabled"), Is.EqualTo(!writeAllowed));
            });

            FindButton(rendered, "edit").Click();

            rendered.WaitForAssertion(() =>
            {
                // the service data save and the save of the opened dialog
                List<AngleSharp.Dom.IElement> saveButtons = [.. rendered.FindAll("button").Where(button => button.TextContent.Trim() == "Save")];
                Assert.That(GetField<bool>(rendered.Instance, "editMode"), Is.True);
                Assert.That(saveButtons, Has.Count.EqualTo(2));
                Assert.That(saveButtons.All(button => button.HasAttribute("disabled") == !writeAllowed), Is.True);
            });
        }

        private static SettingsExternalWorkflowList RenderPage(BunitContext context)
        {
            return RenderPageAs(context, Roles.Admin, assignUserRole: false).Instance;
        }

        private static IRenderedComponent<SettingsExternalWorkflowList> RenderPageAs(BunitContext context, string role, bool assignUserRole = true)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<ApiConnection>(new ExternalWorkflowListTestApiConn());
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            SimulatedUserConfig userConfig = new() { ModIconify = false };
            if (assignUserRole)
            {
                userConfig.User.Roles = [role];
            }
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(role));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<SettingsExternalWorkflowList>());
            IRenderedComponent<SettingsExternalWorkflowList> rendered = wrapper.FindComponent<SettingsExternalWorkflowList>();
            wrapper.WaitForAssertion(() => Assert.That(GetField<bool>(rendered.Instance, "InitComplete"), Is.True));
            return rendered;
        }

        private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<SettingsExternalWorkflowList> rendered, string textKey)
        {
            string text = new SimulatedUserConfig().GetText(textKey);
            List<AngleSharp.Dom.IElement> matches = [.. rendered.FindAll("button").Where(button => button.TextContent.Trim() == text)];
            Assert.That(matches, Has.Count.EqualTo(1), $"button '{text}'");
            return matches[0];
        }

        private static void SetField<T>(SettingsExternalWorkflowList page, string name, T value)
        {
            FieldInfo field = typeof(SettingsExternalWorkflowList).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(SettingsExternalWorkflowList), name);
            field.SetValue(page, value);
        }

        private static T GetField<T>(SettingsExternalWorkflowList page, string name)
        {
            FieldInfo field = typeof(SettingsExternalWorkflowList).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingFieldException(nameof(SettingsExternalWorkflowList), name);
            return (T)field.GetValue(page)!;
        }

        private static void InvokePrivate(SettingsExternalWorkflowList page, string name, params object[] args)
        {
            MethodInfo method = typeof(SettingsExternalWorkflowList).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(nameof(SettingsExternalWorkflowList), name);
            method.Invoke(page, args);
        }

        private sealed class ExternalWorkflowListTestApiConn : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<Credential>) && query == DeviceQueries.getCredentialsWithoutSecrets)
                {
                    List<Credential> credentials =
                    [
                        new() { Id = kKeyPairCredentialId, Name = "key_pair", IsKeyPair = true },
                        new() { Id = kPasswordCredentialId, Name = "password" }
                    ];
                    return Task.FromResult((QueryResponseType)(object)credentials);
                }
                if (typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Management>());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }
    }
}
