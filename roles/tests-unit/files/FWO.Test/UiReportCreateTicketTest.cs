using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the dialog. Creating the ticket itself is not covered here: it hands rulebase ids to a parameter that
    /// expects device ids (see reporting-test-coverage-follow-up.md), and a test would only pin that behavior.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportCreateTicketTest
    {
        private readonly List<bool> displayChanges = [];

        private static BunitContext CreateContext(params string[] roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(roles));
            context.Services.AddSingleton<ApiConnection>(new SimulatedApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig { User = { Roles = [.. roles] } });
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            return context;
        }

        private IRenderedComponent<ReportCreateTicket> Render(BunitContext context, bool display, List<Rule> selectedRules)
        {
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ReportCreateTicket>(dialog => dialog
                    .Add(p => p.Display, display)
                    .Add(p => p.DisplayChanged, (bool shown) => displayChanges.Add(shown))
                    .Add(p => p.SelectedRules, selectedRules)));
            return wrapper.FindComponent<ReportCreateTicket>();
        }

        private static IElement CreateButton(IRenderedComponent<ReportCreateTicket> component)
        {
            return component.FindComponent<PopUp>().FindAll("button.btn-danger").Single();
        }

        [Test]
        public async Task AHiddenDialogShowsNothing()
        {
            await using BunitContext context = CreateContext(Roles.Requester);

            IRenderedComponent<ReportCreateTicket> component = Render(context, display: false, [new Rule { Uid = "uid-1" }]);

            Assert.That(component.FindComponent<PopUp>().Instance.Show, Is.False);
            Assert.That(component.Instance.Message, Is.Empty);
        }

        [Test]
        public async Task TheDialogListsTheRulesToDeleteWithTheirUidOrNameOrId()
        {
            await using BunitContext context = CreateContext(Roles.Requester);
            UserConfig userConfig = context.Services.GetRequiredService<UserConfig>();
            List<Rule> rules =
            [
                new Rule { Id = 1, RulebaseId = 7, Uid = "uid-1" },
                new Rule { Id = 2, RulebaseId = 7, Uid = null, Name = "named rule" },
                new Rule { Id = 3, RulebaseId = 7, Uid = null, Name = null }
            ];

            IRenderedComponent<ReportCreateTicket> component = Render(context, display: true, rules);

            Assert.That(component.Instance.MessageHeader, Is.EqualTo(userConfig.GetText("rules_to_delete") + ":"));
            Assert.That(component.Instance.Message, Is.EqualTo("uid-1<br>named rule<br>3"));
            Assert.That(component.Find("p").TextContent, Is.EqualTo(component.Instance.MessageHeader));
        }

        [Test]
        public async Task RuleUidsAreEncodedBeforeTheyAreShownAsMarkup()
        {
            await using BunitContext context = CreateContext(Roles.Requester);

            IRenderedComponent<ReportCreateTicket> component = Render(context, display: true, [new Rule { Id = 1, Uid = "<b onclick=\"x\">uid</b>" }]);

            Assert.That(component.FindAll("b"), Is.Empty);
            Assert.That(component.FindAll("p")[1].TextContent, Is.EqualTo("<b onclick=\"x\">uid</b>"));
        }

        [Test]
        public async Task OpeningTheDialogAgainClearsTheReason()
        {
            await using BunitContext context = CreateContext(Roles.Requester);
            IRenderedComponent<ReportCreateTicket> component = Render(context, display: true, [new Rule { Id = 1, Uid = "uid-1" }]);
            component.Find("textarea").Change("old reason");

            component.Render(parameters => parameters.Add(p => p.Display, true));

            Assert.That(component.Find("textarea").GetAttribute("value"), Is.Empty);
        }

        [Test]
        public async Task CancellingClosesTheDialog()
        {
            await using BunitContext context = CreateContext(Roles.Requester);
            IRenderedComponent<ReportCreateTicket> component = Render(context, display: true, [new Rule { Id = 1, Uid = "uid-1" }]);

            component.FindComponent<PopUp>().Find("button.btn-secondary").Click();

            Assert.That(displayChanges, Is.EqualTo((List<bool>)[false]));
            Assert.That(component.Instance.Display, Is.False);
        }

        [TestCase(Roles.Requester, false)]
        [TestCase(Roles.Admin, false)]
        [TestCase(Roles.Auditor, true)]
        public async Task OnlyRequestersAndAdminsMayCreateTheTicket(string role, bool disabled)
        {
            await using BunitContext context = CreateContext(role);

            IRenderedComponent<ReportCreateTicket> component = Render(context, display: true, [new Rule { Id = 1, Uid = "uid-1" }]);

            Assert.That(CreateButton(component).HasAttribute("disabled"), Is.EqualTo(disabled));
        }
    }
}
