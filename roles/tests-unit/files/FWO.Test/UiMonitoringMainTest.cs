using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiMonitoringMainTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(MonitoringMain).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(MonitoringMain).FullName, name);
        }

        private static void SetPrivateField<T>(MonitoringMain component, string fieldName, T value)
        {
            FieldInfo? field = typeof(MonitoringMain).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(MonitoringMain).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        [Test]
        public async Task Acknowledge_RemovesAlertAndCallsApi()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider());
            MonitoringMainTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitoringMain>());
            MonitoringMain monitoring = component.FindComponent<MonitoringMain>().Instance;

            Alert alert = new() { Id = 123, Source = "test" };
            List<Alert> alerts = new() { alert };
            SetPrivateField(monitoring, "alertEntrys", alerts);

            Task acknowledgeTask = (Task)GetPrivateMethod("Acknowledge").Invoke(monitoring, new object[] { alert })!;
            await acknowledgeTask;

            Assert.That(alerts, Is.Empty);
            Assert.That(apiConn.AcknowledgedAlertIds, Is.EqualTo(new[] { 123L }));
        }

        [Test]
        public async Task AcknowledgeAllOpen_ClearsAlertsWhenSuccessful()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider());
            MonitoringMainTestApiConn apiConn = new() { AckAllAffectedRows = 2 };
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<MonitoringMain>());
            MonitoringMain monitoring = component.FindComponent<MonitoringMain>().Instance;

            List<Alert> alerts = new()
            {
                new Alert { Id = 1, Source = "test" },
                new Alert { Id = 2, Source = "test" }
            };
            SetPrivateField(monitoring, "alertEntrys", alerts);

            Task acknowledgeTask = (Task)GetPrivateMethod("AcknowledgeAllOpen").Invoke(monitoring, null)!;
            await acknowledgeTask;

            Assert.That(alerts, Is.Empty);
            Assert.That(apiConn.AcknowledgeAllCalls, Is.EqualTo(1));
        }
    }

    internal sealed class MonitoringMainTestApiConn : SimulatedApiConnection
    {
        public List<long> AcknowledgedAlertIds { get; } = [];
        public int AcknowledgeAllCalls { get; private set; }
        public int AckAllAffectedRows { get; set; } = 1;

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                if (query == MonitorQueries.acknowledgeAlert)
                {
                    if (variables != null)
                    {
                        object? idValue = variables.GetType().GetProperty("id")?.GetValue(variables);
                        if (idValue != null)
                        {
                            AcknowledgedAlertIds.Add(Convert.ToInt64(idValue));
                        }
                    }
                    return Task.FromResult((QueryResponseType)(object)new ReturnId());
                }

                if (query == MonitorQueries.acknowledgeAllOpenAlerts)
                {
                    AcknowledgeAllCalls++;
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = AckAllAffectedRows });
                }
            }

            throw new NotImplementedException();
        }
    }

    internal sealed class MonitoringTestAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal principal;

        public MonitoringTestAuthStateProvider(params string[] roles)
        {
            List<Claim> claims = [];
            foreach (string role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            ClaimsIdentity identity = new(claims, "Test");
            principal = new ClaimsPrincipal(identity);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(principal));
        }
    }
}
