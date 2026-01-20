using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class UiRsbRuleTabTest : BunitContext
    {
        /// <summary>
        /// Ensures the rule tab updates when the selected rule list changes.
        /// </summary>
        [Test]
        public void RuleTabSelectionUpdatesOnChange()
        {
            Rule ruleAlpha = new() { Id = 1, MgmtId = 1, Name = "Alpha", DeviceName = "Device A" };
            Rule ruleBeta = new() { Id = 2, MgmtId = 1, Name = "Beta", DeviceName = "Device B" };

            List<Rule> ruleDetails = [new Rule { Id = 1 }, new Rule { Id = 2 }];
            ReportBase report = SimulatedReport.DetailedReport(ReportType.Rules);

            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddSingleton<ApiConnection>(new UiRsbRuleTabApiConn(ruleDetails));
            Services.AddScoped(_ => JSInterop.JSRuntime);

            IRenderedComponent<RightSidebar> cut = Render<RightSidebar>(parameters => parameters
                .Add(p => p.AllTabVisible, false)
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule>()));

            cut.WaitForAssertion(() => Assert.That(FindActiveTabTitle(cut), Is.EqualTo("Report")));

            cut = Render<RightSidebar>(parameters => parameters
                .Add(p => p.AllTabVisible, false)
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule> { ruleAlpha }));

            cut.WaitForAssertion(() =>
            {
                Assert.That(FindActiveTabTitle(cut), Is.EqualTo("Rule"));
                Assert.That(cut.Markup, Does.Contain("Alpha"));
            });

            cut = Render<RightSidebar>(parameters => parameters
                .Add(p => p.AllTabVisible, false)
                .Add(p => p.CurrentReport, report)
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule> { ruleBeta }));

            cut.WaitForAssertion(() =>
            {
                Assert.That(FindActiveTabTitle(cut), Is.EqualTo("Rule"));
                Assert.That(cut.Markup, Does.Contain("Beta"));
                Assert.That(cut.Markup, Does.Not.Contain("Alpha"));
            });
        }

        private static string FindActiveTabTitle(IRenderedComponent<RightSidebar> cut)
        {
            IElement? activeLink = cut.FindAll("a.nav-link").FirstOrDefault(el => el.ClassList.Contains("nav-link-active"));
            return activeLink?.TextContent.Trim() ?? string.Empty;
        }
    }

    internal class UiRsbRuleTabApiConn : SimulatedApiConnection
    {
        private readonly List<Rule> ruleDetails;

        public UiRsbRuleTabApiConn(List<Rule> ruleDetails)
        {
            this.ruleDetails = ruleDetails;
        }

        /// <summary>
        /// Returns rule details for rule-tab fetches during tests.
        /// </summary>
        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
            string query,
            object? variables = null,
            string? operationName = null)
        {
            await DefaultInit.DoNothing();
            if (typeof(QueryResponseType) == typeof(List<Rule>))
            {
                return (QueryResponseType)(object)ruleDetails;
            }
            if (typeof(QueryResponseType) == typeof(List<ManagementReport>))
            {
                return (QueryResponseType)(object)new List<ManagementReport>();
            }
            throw new NotImplementedException();
        }
    }
}
