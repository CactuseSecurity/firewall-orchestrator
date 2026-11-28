using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportTenantSelectionTest
    {
        private sealed record DisplayedMessage(Exception? Exception, string Title);

        private sealed class TenantApiConnection(List<Tenant> tenants, bool fail = false) : SimulatedApiConnection
        {
            public int TenantQueries { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query != AuthQueries.getTenants)
                {
                    throw new NotImplementedException(query);
                }
                TenantQueries++;
                return fail
                    ? throw new InvalidOperationException("simulated api failure")
                    : Task.FromResult((QueryResponseType)(object)tenants.ToList());
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];
        private readonly SimulatedUserConfig userConfig = new();

        private BunitContext CreateContext(TenantApiConnection apiConnection)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            return context;
        }

        private IRenderedComponent<ReportTenantSelection> Render(BunitContext context, bool tenantFilteringAllowed, Action<Tenant?>? tenantChanged = null)
        {
            return context.Render<ReportTenantSelection>(parameters => parameters
                .AddCascadingValue<Action<Exception?, string, string, bool>>((exception, title, _, _) => displayedMessages.Add(new DisplayedMessage(exception, title)))
                .Add(p => p.TenantFilteringAllowed, tenantFilteringAllowed)
                .Add(p => p.SelectedTenantChanged, (Tenant? tenant) => tenantChanged?.Invoke(tenant)));
        }

        [Test]
        public async Task WithoutTenantFilteringTheUsersOwnTenantIsShown()
        {
            TenantApiConnection apiConnection = new([new Tenant { Id = 1, Name = "a" }, new Tenant { Id = 2, Name = "b" }]);
            userConfig.User.Tenant = new Tenant { Id = 2, Name = "own tenant" };
            await using BunitContext context = CreateContext(apiConnection);

            IRenderedComponent<ReportTenantSelection> component = Render(context, tenantFilteringAllowed: false);

            Assert.That(component.Find("h5").TextContent.Trim(), Is.EqualTo($"{userConfig.GetText("tenant")} own tenant"));
            Assert.That(component.FindComponents<Dropdown<Tenant>>(), Is.Empty);
            Assert.That(apiConnection.TenantQueries, Is.Zero);
        }

        [Test]
        public async Task WithTenantFilteringTheTenantsAreOfferedByName()
        {
            await using BunitContext context = CreateContext(new TenantApiConnection(
                [new Tenant { Id = 2, Name = "zulu" }, new Tenant { Id = 1, Name = "alpha" }, new Tenant { Id = 3, Name = "mike" }]));

            IRenderedComponent<ReportTenantSelection> component = Render(context, tenantFilteringAllowed: true);
            IRenderedComponent<Dropdown<Tenant>> dropdown = component.FindComponent<Dropdown<Tenant>>();
            dropdown.Find("input").Focus();

            Assert.That(component.Find("h5").TextContent.Trim(), Is.EqualTo(userConfig.GetText("tenant_view")));
            Assert.That(dropdown.Instance.Elements.Select(tenant => tenant.Name), Is.EqualTo((List<string>)["alpha", "mike", "zulu"]));
            Assert.That(dropdown.FindAll("button.dropdown-item").Select(item => item.TextContent.Trim()), Is.EqualTo((List<string>)["alpha", "mike", "zulu"]));
        }

        [Test]
        public async Task ChoosingATenantIsHandedToThePage()
        {
            Tenant chosen = new() { Id = 2, Name = "second" };
            Tenant? reported = null;
            await using BunitContext context = CreateContext(new TenantApiConnection([new Tenant { Id = 1, Name = "first" }, chosen]));
            IRenderedComponent<ReportTenantSelection> component = Render(context, tenantFilteringAllowed: true, tenant => reported = tenant);

            Dropdown<Tenant> dropdown = component.FindComponent<Dropdown<Tenant>>().Instance;
            await component.InvokeAsync(() => dropdown.SelectedElementChanged.InvokeAsync(chosen));

            Assert.That(reported, Is.SameAs(chosen));
        }

        [Test]
        public async Task ASingleTenantNeedsNoChoice()
        {
            userConfig.User.Tenant = new Tenant { Id = 1, Name = "only tenant" };
            await using BunitContext context = CreateContext(new TenantApiConnection([new Tenant { Id = 1, Name = "only tenant" }]));

            IRenderedComponent<ReportTenantSelection> component = Render(context, tenantFilteringAllowed: true);

            Assert.That(component.FindComponents<Dropdown<Tenant>>(), Is.Empty);
            Assert.That(component.Find("h5").TextContent, Does.Contain("only tenant"));
        }

        [Test]
        public async Task AFailingTenantFetchIsReported()
        {
            await using BunitContext context = CreateContext(new TenantApiConnection([], fail: true));

            IRenderedComponent<ReportTenantSelection> component = Render(context, tenantFilteringAllowed: true);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(userConfig.GetText("object_fetch")));
            Assert.That(component.FindComponents<Dropdown<Tenant>>(), Is.Empty);
        }
    }
}
