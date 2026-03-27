using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NUnit.Framework;

namespace FWO.Test
{
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class RulesReportTreeViewTest : BunitContext
    {
        [SetUp]
        public void SetUp()
        {
            Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
            Services.AddScoped(_ => JSInterop.JSRuntime);
            Services.AddLocalization();
        }

        [Test]
        public void SearchFiltersRulesAndExpandsMatchingAncestors()
        {
            (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, RuleTreeItem section) = BuildTreeWithSection();
            Services.AddSingleton<IRuleTreeBuilder>(builder);

            IRenderedComponent<RulesReport> cut = Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, new List<ManagementReport> { management })
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule>()));

            cut.Find("#rules-report-search").Input("Alpha");

            cut.WaitForAssertion(() =>
            {
                Assert.That(section.IsExpanded, Is.True);
                Assert.That(cut.Markup, Does.Contain("Section A"));
                Assert.That(cut.Markup, Does.Contain("Alpha"));
                Assert.That(cut.Markup, Does.Not.Contain("Beta"));
            });
        }

        [Test]
        public void ClickingRuleRowSelectsRuleAndShowsDetails()
        {
            (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, _) = BuildTreeWithSection();
            Services.AddSingleton<IRuleTreeBuilder>(builder);

            List<Rule> selectedRules = [];

            IRenderedComponent<RulesReport> cut = Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, new List<ManagementReport> { management })
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, selectedRules)
                .Add(p => p.SelectedRulesChanged, EventCallback.Factory.Create<List<Rule>>(this, rules => selectedRules = rules)));

            cut.FindAll(".rules-report__row--header")
                .First(row => row.TextContent.Contains("Section A"))
                .QuerySelector("button")!
                .Click();

            cut.FindAll(".rules-report__row").First(row => row.TextContent.Contains("Alpha")).Click();

            cut.WaitForAssertion(() =>
            {
                Assert.That(selectedRules.Count, Is.EqualTo(1));
                Assert.That(selectedRules[0].Name, Is.EqualTo("Alpha"));
                Assert.That(cut.Find(".rules-report__detail"), Is.Not.Null);
            });
        }

        [Test]
        public void HidingNameColumnRemovesNameHeaderAndCell()
        {
            (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, _) = BuildTreeWithSection();
            Services.AddSingleton<IRuleTreeBuilder>(builder);

            IRenderedComponent<RulesReport> cut = Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, new List<ManagementReport> { management })
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule>()));

            IElement nameToggle = cut.FindAll(".rules-report__column-option")
                .First(option => option.TextContent.Contains("Name"))
                .QuerySelector("input")!;

            nameToggle.Change(false);

            cut.WaitForAssertion(() =>
            {
                Assert.That(cut.Find(".rules-report__header").TextContent, Does.Not.Contain("Name"));
                Assert.That(cut.FindAll(".rules-report__row").Any(row => row.TextContent.Contains("Alpha")), Is.False);
            });
        }

        private static (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, RuleTreeItem section) BuildTreeWithSection()
        {
            Rule alpha = new()
            {
                Id = 1,
                Name = "Alpha",
                Uid = "rule-alpha",
                Metadata = new RuleMetadata()
            };

            Rule beta = new()
            {
                Id = 2,
                Name = "Beta",
                Uid = "rule-beta",
                Metadata = new RuleMetadata()
            };

            RuleTreeItem root = new()
            {
                IsRoot = true,
                IsExpanded = true
            };

            RuleTreeItem section = new()
            {
                Header = "Section A",
                IsSectionHeader = true,
                Data = new Rule { SectionHeader = "Section A" },
                IsExpanded = false,
                Parent = root
            };

            RuleTreeItem alphaNode = new()
            {
                IsRule = true,
                Data = alpha,
                Parent = section
            };

            RuleTreeItem betaNode = new()
            {
                IsRule = true,
                Data = beta,
                Parent = root
            };

            section.Children.Add(alphaNode);
            root.Children.Add(section);
            root.Children.Add(betaNode);

            DeviceReport device = new()
            {
                Id = 1,
                Name = "Device A",
                RulebaseLinks =
                [
                    new RulebaseLink
                    {
                        IsInitial = true,
                        FromRulebaseId = 1,
                        NextRulebaseId = 1
                    }
                ]
            };

            ManagementReport management = new()
            {
                Id = 1,
                Name = "Management A",
                Devices = [device],
                Rulebases = []
            };

            RuleTreeBuilder builder = new();
            builder.RuleTreeCache[(management.Id, device.Id)] = root;

            return (management, device, builder, section);
        }
    }
}
