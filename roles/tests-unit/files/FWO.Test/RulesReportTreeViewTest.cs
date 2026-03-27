using AngleSharp.Dom;
using AngleSharp.Html.Dom;
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

            cut.FindAll(".tree-view__row--header")
                .First(row => row.TextContent.Contains("Section A"))
                .QuerySelector("button")!
                .Click();

            cut.FindAll(".tree-view__row").First(row => row.TextContent.Contains("Alpha")).Click();

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
                Assert.That(cut.Find(".tree-view__header").TextContent, Does.Not.Contain("Name"));
                Assert.That(cut.FindAll(".tree-view__row").Any(row => row.TextContent.Contains("Alpha")), Is.False);
            });
        }

        [Test]
        public void SortingNumberColumnUsesCustomNumericComparer()
        {
            (ManagementReport management, RuleTreeBuilder builder) = BuildFlatRuleTree(
                CreateRule(1, "Ten", 10),
                CreateRule(2, "Two", 2));

            Services.AddSingleton<IRuleTreeBuilder>(builder);

            IRenderedComponent<RulesReport> cut = Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, new List<ManagementReport> { management })
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule>()));

            cut.FindAll(".tree-view__header-button")
                .First(button => button.TextContent.Contains("No."))
                .Click();

            cut.WaitForAssertion(() =>
            {
                IReadOnlyList<IElement> rows = cut.FindAll(".tree-view__row");
                Assert.That(rows.First().TextContent, Does.Contain("Two"));
            });

            cut.FindAll(".tree-view__header-button")
                .First(button => button.TextContent.Contains("No."))
                .Click();

            cut.WaitForAssertion(() =>
            {
                IReadOnlyList<IElement> rows = cut.FindAll(".tree-view__row");
                Assert.That(rows.First().TextContent, Does.Contain("Ten"));
            });
        }

        [Test]
        public void HeaderCloseButtonHidesColumnAndUnchecksCheckbox()
        {
            (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, _) = BuildTreeWithSection();
            Services.AddSingleton<IRuleTreeBuilder>(builder);

            IRenderedComponent<RulesReport> cut = Render<RulesReport>(parameters => parameters
                .Add(p => p.Managements, new List<ManagementReport> { management })
                .Add(p => p.SelectedReportType, ReportType.Rules)
                .Add(p => p.SelectedRules, new List<Rule>()));

            IElement nameHeaderRemoveButton = cut.FindAll(".tree-view__header-cell")
                .First(header => header.TextContent.Contains("Name"))
                .QuerySelector(".tree-view__header-remove")!;

            nameHeaderRemoveButton.Click();

            cut.WaitForAssertion(() =>
            {
                Assert.That(cut.Find(".tree-view__header").TextContent, Does.Not.Contain("Name"));

                IHtmlInputElement nameToggle = (IHtmlInputElement)cut.FindAll(".rules-report__column-option")
                    .First(option => option.TextContent.Contains("Name"))
                    .QuerySelector("input")!;

                Assert.That(nameToggle.IsChecked, Is.False);
            });
        }

        private static (ManagementReport management, DeviceReport device, RuleTreeBuilder builder, RuleTreeItem section) BuildTreeWithSection()
        {
            Rule alpha = CreateRule(1, "Alpha", 1);
            Rule beta = CreateRule(2, "Beta", 2);

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

        private static (ManagementReport management, RuleTreeBuilder builder) BuildFlatRuleTree(params Rule[] rules)
        {
            RuleTreeItem root = new()
            {
                IsRoot = true,
                IsExpanded = true
            };

            foreach (Rule rule in rules)
            {
                root.Children.Add(new RuleTreeItem
                {
                    IsRule = true,
                    Data = rule,
                    Parent = root
                });
            }

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

            return (management, builder);
        }

        private static Rule CreateRule(long id, string name, int ruleOrderNumber)
        {
            return new()
            {
                Id = id,
                Name = name,
                Uid = $"rule-{name.ToLowerInvariant()}",
                RuleOrderNumber = ruleOrderNumber,
                DisplayOrderNumberString = ruleOrderNumber.ToString(),
                Metadata = new RuleMetadata()
            };
        }
    }
}
