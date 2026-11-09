using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class UiMissingSshHostKeysWarningTest
    {
        private const string kWarningSelector = "[data-testid='missing-ssh-host-keys-warning']";
        private const string kMarkupName = "<b>asa</b>";
        private static readonly TimeSpan kRenderTimeout = TimeSpan.FromSeconds(10);

        [Test]
        public async Task SubscribesToOpenHostKeyAlertsOnly()
        {
            await using BunitContext context = new();
            HostKeyAlertApiConnection apiConnection = new();

            Render(context, apiConnection);

            using JsonDocument variables = JsonDocument.Parse(JsonSerializer.Serialize(apiConnection.Variables));
            Assert.That(variables.RootElement.GetProperty("alertCode").GetInt32(), Is.EqualTo((int)AlertCode.SshHostKeyRejected));
            Assert.That(variables.RootElement.GetProperty("limit").GetInt32(), Is.Positive);
        }

        [Test]
        public async Task ListsTheManagementsOfOpenAlertsAsTextAndHidesWhenTheyAreClosed()
        {
            await using BunitContext context = new();
            HostKeyAlertApiConnection apiConnection = new();
            IRenderedComponent<CascadingAuthenticationState> rendered = Render(context, apiConnection);
            List<MissingSshHostKeysWarning.HostKeyAlert> openAlerts =
            [
                CreateAlert(2, "zeta-asa"),
                CreateAlert(1, kMarkupName),
                CreateAlert(3, "zeta-asa")
            ];
            List<MissingSshHostKeysWarning.HostKeyAlert> noAlerts = [];

            Assert.That(rendered.FindAll(kWarningSelector), Is.Empty);

            apiConnection.Publish(openAlerts);
            rendered.WaitForAssertion(() =>
            {
                AngleSharp.Dom.IElement warning = rendered.Find(kWarningSelector);
                Assert.That(warning.TextContent, Does.Contain(kMarkupName + ", zeta-asa"));
                Assert.That(warning.QuerySelectorAll("b"), Is.Empty);
            }, kRenderTimeout);

            apiConnection.Publish(noAlerts);
            rendered.WaitForAssertion(() => Assert.That(rendered.FindAll(kWarningSelector), Is.Empty), kRenderTimeout);
        }

        private static MissingSshHostKeysWarning.HostKeyAlert CreateAlert(int managementId, string managementName)
        {
            return new MissingSshHostKeysWarning.HostKeyAlert
            {
                Id = managementId,
                ManagementId = managementId,
                Management = new MissingSshHostKeysWarning.HostKeyAlertManagement { Name = managementName }
            };
        }

        private static IRenderedComponent<CascadingAuthenticationState> Render(BunitContext context, HostKeyAlertApiConnection apiConnection)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AdminAuthStateProvider());
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<MissingSshHostKeysWarning>());
        }

        private sealed class HostKeyAlertApiConnection : SimulatedApiConnection
        {
            private GraphQlApiSubscription<List<MissingSshHostKeysWarning.HostKeyAlert>>.SubscriptionUpdate? update;

            public object? Variables { get; private set; }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription,
                object? variables = null, string? operationName = null)
            {
                update = subscriptionUpdateHandler as GraphQlApiSubscription<List<MissingSshHostKeysWarning.HostKeyAlert>>.SubscriptionUpdate;
                Variables = variables;
                // the component only disposes the subscription, which the null check covers
                return null!;
            }

            public void Publish(List<MissingSshHostKeysWarning.HostKeyAlert> alerts)
            {
                update?.Invoke(alerts);
            }
        }

        private sealed class AdminAuthStateProvider : AuthenticationStateProvider
        {
            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                ClaimsIdentity identity = new(new List<Claim> { new(ClaimTypes.Role, Roles.Admin) }, "Test", ClaimTypes.Name, ClaimTypes.Role);
                return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
            }
        }
    }
}
