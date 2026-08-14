using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiZonesConfigurationTest
    {
        private sealed class ZonesConfigurationApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (typeof(QueryResponseType) == typeof(List<ComplianceNetworkZone>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ComplianceNetworkZone>());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("add", "Add");
            SimulatedUserConfig.DummyTranslate.TryAdd("edit_zone", "Edit zone");
            SimulatedUserConfig.DummyTranslate.TryAdd("delete_zone", "Delete zone");
        }

        private static BunitContext CreateContext(NetworkZoneService networkZoneService)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new ZonesConfigurationApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton(networkZoneService);
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static IRenderedComponent<ZonesConfiguration> Render(BunitContext context)
        {
            return context.Render<ZonesConfiguration>(parameters => parameters
                .Add(p => p.SelectedMatrix, new ComplianceCriterion { Id = 1, Name = "matrix" })
                .Add(p => p.ReadonlyMode, false));
        }

        [Test]
        public async Task SubscribesToTheZoneEventsWhileItIsRendered()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);

            Render(context);

            // proves the probe below detects an attached handler, so the dispose tests are meaningful
            Assert.Multiple(() =>
            {
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.False);
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.False);
            });
            Assert.DoesNotThrow(() => networkZoneService.InvokeOnEditZone(new ComplianceNetworkZone { Id = 5 }));
        }

        [Test]
        public async Task DisposingDetachesTheEditZoneHandler()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            page.Instance.Dispose();

            // an attached handler would still run against the disposed component and re-render it
            Assert.DoesNotThrow(() => networkZoneService.InvokeOnEditZone(new ComplianceNetworkZone { Id = 5 }));
            Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.True);
        }

        [Test]
        public async Task DisposingDetachesTheDeleteZoneHandler()
        {
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);
            IRenderedComponent<ZonesConfiguration> page = Render(context);

            page.Instance.Dispose();

            Assert.DoesNotThrow(() => networkZoneService.InvokeOnDeleteZone(new ComplianceNetworkZone { Id = 5 }));
            Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.True);
        }

        [Test]
        public async Task RenderingThePageRepeatedlyDoesNotAccumulateHandlers()
        {
            // this is the actual leak: the service outlives the page, so every visit used to leave
            // another handler behind that kept the disposed page and its zone data alive
            NetworkZoneService networkZoneService = new();
            await using BunitContext context = CreateContext(networkZoneService);

            for (int visit = 0; visit < 3; visit++)
            {
                IRenderedComponent<ZonesConfiguration> page = Render(context);
                page.Instance.Dispose();
            }

            Assert.Multiple(() =>
            {
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnEditZone)), Is.True);
                Assert.That(HasNoSubscribers(networkZoneService, nameof(NetworkZoneService.OnDeleteZone)), Is.True);
            });
        }

        private static bool HasNoSubscribers(NetworkZoneService service, string eventName)
        {
            System.Reflection.FieldInfo? field = typeof(NetworkZoneService)
                .GetField(eventName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(service) is not Delegate handler || handler.GetInvocationList().Length == 0;
        }
    }
}
