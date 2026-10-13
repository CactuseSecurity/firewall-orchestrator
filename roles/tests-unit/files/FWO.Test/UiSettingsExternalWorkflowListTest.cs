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

        private static SettingsExternalWorkflowList RenderPage(BunitContext context)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddSingleton<ApiConnection>(new ExternalWorkflowListTestApiConn());
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddLocalization();
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<SettingsExternalWorkflowList>());
            SettingsExternalWorkflowList page = wrapper.FindComponent<SettingsExternalWorkflowList>().Instance;
            wrapper.WaitForAssertion(() => Assert.That(GetField<bool>(page, "InitComplete"), Is.True));
            return page;
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
