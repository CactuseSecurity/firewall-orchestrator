using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Shared;
using GraphQL.Client.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Components outside the modelling pages that render stored rule or management data as markup must
    /// encode it (GHSA-mm29-m6vq-gwgg). The page size form must not rely on an inline handler, which the
    /// content security policy refuses.
    /// </summary>
    [TestFixture]
    internal class UiHtmlEncodingComponentsTest
    {
        private const string kPayload = "<img src=x onerror=console.log(1)>\"'&amp;";
        private const int kMgmtId = 1;
        private const int kRulebaseId = 2;
        private const int kPageSize = 25;

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(new PayloadApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            return context;
        }

        private static string RenderedText(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> component)
        {
            return string.Concat(component.Nodes.Select(node => node.TextContent));
        }

        private static List<Rule> PayloadRules()
        {
            return [new Rule { Id = 3, MgmtId = kMgmtId, RulebaseId = kRulebaseId, Uid = kPayload, Name = kPayload }];
        }

        [Test]
        public async Task RuleSelector_EncodesManagementRulebaseAndRule()
        {
            await using BunitContext context = CreateContext();
            List<Rule> selectedRules = PayloadRules();

            IRenderedComponent<RuleSelector> component = context.Render<RuleSelector>(parameters => parameters
                .Add(p => p.SelectedRules, selectedRules)
                .Add(p => p.Readonly, true));

            Assert.Multiple(() =>
            {
                Assert.That(component.FindAll("img"), Is.Empty, "a stored value created an <img> element");
                Assert.That(component.FindAll(".col-sm-2").Select(element => element.TextContent), Is.EqualTo(PayloadApiConnection.ExpectedNames),
                    "management and rulebase names are shown as text");
                Assert.That(component.Find(".col-sm-8").TextContent, Does.Contain(kPayload), "the rule is shown as text");
            });
        }

        [Test]
        public async Task ReportCreateTicket_EncodesRuleUids()
        {
            await using BunitContext context = CreateContext();
            List<Rule> selectedRules = PayloadRules();

            IRenderedComponent<ReportCreateTicket> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<ReportCreateTicket>(ticket => ticket
                    .Add(p => p.Display, true)
                    .Add(p => p.SelectedRules, selectedRules)))
                .FindComponent<ReportCreateTicket>();

            Assert.Multiple(() =>
            {
                Assert.That(component.Instance.Message, Is.EqualTo(HtmlOutputEncoder.EncodeText(kPayload)));
                Assert.That(component.FindAll("img"), Is.Empty, "a rule uid created an <img> element");
                Assert.That(RenderedText(component), Does.Contain(kPayload), "the rule uid is shown as text");
            });
        }

        [Test]
        public async Task PageSizeComponent_SubmitsWithoutInlineHandler()
        {
            await using BunitContext context = CreateContext();
            List<int> submittedSizes = [];

            IRenderedComponent<PageSizeComponent> component = context.Render<PageSizeComponent>(parameters => parameters
                .Add(p => p.PageSize, kPageSize)
                .Add(p => p.PageSizeCallback, size => submittedSizes.Add(size)));
            component.Find("form").Submit();

            Assert.Multiple(() =>
            {
                Assert.That(component.Find("form").HasAttribute("onsubmit"), Is.False, "the form carries an inline handler");
                Assert.That(submittedSizes, Has.Count.EqualTo(2), "the page size is submitted on initialization and on submit");
                Assert.That(submittedSizes, Is.All.EqualTo(kPageSize));
            });
        }

        /// <summary>
        /// Answers the management query of the rule selector with a management and rulebase carrying the
        /// payload, and every other query with an empty result.
        /// </summary>
        private sealed class PayloadApiConnection : SimulatedApiConnection
        {
            public static readonly List<string> ExpectedNames = [kPayload, kPayload];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == DeviceQueries.getManagementsWithRulebases && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    List<Management> managements = [new Management { Id = kMgmtId, Name = kPayload, Rulebases = [new Rulebase { Id = kRulebaseId, Name = kPayload }] }];
                    return Task.FromResult((QueryResponseType)(object)managements);
                }
                return Task.FromResult(Activator.CreateInstance<QueryResponseType>());
            }
        }
    }
}
