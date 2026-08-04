using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiArchiveTest
    {
        private sealed class ArchiveApiConnection : SimulatedApiConnection
        {
            public object? ReportQueryVariables { get; private set; }
            public object? SubscriptionVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getGeneratedReports)
                {
                    ReportQueryVariables = variables;
                    return Task.FromResult((QueryResponseType)(object)new List<ReportFile>());
                }
                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }
                if (typeof(QueryResponseType) == typeof(List<ReportFile>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ReportFile>());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                SubscriptionVariables = variables;
                return null!;
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("archive", "Archive");
            SimulatedUserConfig.DummyTranslate.TryAdd("actions", "Actions");
            SimulatedUserConfig.DummyTranslate.TryAdd("name", "Name");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("template", "Template");
            SimulatedUserConfig.DummyTranslate.TryAdd("generation_date", "Generation date");
            SimulatedUserConfig.DummyTranslate.TryAdd("user", "User");
            SimulatedUserConfig.DummyTranslate.TryAdd("description", "Description");
            SimulatedUserConfig.DummyTranslate.TryAdd("all", "All");
        }

        private static BunitContext CreateContext(ArchiveApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static int? ReadLimit(object? variables)
        {
            return variables?.GetType().GetProperty("limit")?.GetValue(variables) as int?;
        }

        [Test]
        public async Task TheInitialArchiveQueryIsBounded()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            context.Render<Archive>();

            Assert.That(ReadLimit(apiConnection.ReportQueryVariables), Is.Not.Null);
            Assert.That(ReadLimit(apiConnection.ReportQueryVariables), Is.GreaterThan(0));
        }

        [Test]
        public async Task TheArchiveSubscriptionIsBoundedWithTheSameLimit()
        {
            // the subscription re-sends the whole list to every viewer on every new report, so an
            // unbounded one grows for as long as the installation keeps archiving reports
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            context.Render<Archive>();

            Assert.That(ReadLimit(apiConnection.SubscriptionVariables), Is.Not.Null);
            Assert.That(ReadLimit(apiConnection.SubscriptionVariables),
                Is.EqualTo(ReadLimit(apiConnection.ReportQueryVariables)));
        }
    }
}
