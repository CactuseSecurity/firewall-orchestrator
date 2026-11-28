using Bunit;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiStatisticsReportTest
    {
        private readonly SimulatedUserConfig userConfig = new();

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static ObjectStatistics Count(int count)
        {
            return new ObjectStatistics { ObjectAggregate = new ObjectAggregate { ObjectCount = count } };
        }

        private static ManagementReport BuildManagement(string name, int offset, bool ignore = false)
        {
            return new ManagementReport
            {
                Name = name,
                Ignore = ignore,
                NetworkObjectStatistics = Count(offset + 1),
                ServiceObjectStatistics = Count(offset + 2),
                UserObjectStatistics = Count(offset + 3),
                RuleStatistics = Count(offset + 4),
                UnusedRulesStatistics = Count(offset + 5),
                Devices =
                [
                    new DeviceReport { Name = $"{name} gateway", RuleStatistics = Count(offset + 6), UnusedRulesStatistics = Count(offset + 7) }
                ]
            };
        }

        private static List<List<string>> Rows(AngleSharp.Dom.IElement table)
        {
            return [.. table.QuerySelectorAll("tbody tr").Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToList())];
        }

        [Test]
        public async Task WithoutManagementsNothingIsShown()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<StatisticsReport> component = context.Render<StatisticsReport>(parameters => parameters
                .Add(p => p.Managements, []));

            Assert.That(component.FindAll("table"), Is.Empty);
            Assert.That(component.Markup, Does.Not.Contain(userConfig.GetText("glob_no_obj")));
        }

        [Test]
        public async Task TheGlobalStatisticsAreShownAboveTheManagements()
        {
            await using BunitContext context = CreateContext();
            ManagementReport globalStats = BuildManagement("global", 100);

            IRenderedComponent<StatisticsReport> component = context.Render<StatisticsReport>(parameters => parameters
                .Add(p => p.Managements, [BuildManagement("first", 0)])
                .Add(p => p.GlobalStats, globalStats));

            Assert.That(component.Find("h5").TextContent, Is.EqualTo(userConfig.GetText("glob_no_obj")));
            Assert.That(Rows(component.FindAll("table")[0]), Is.EqualTo(new List<List<string>> { new() { "101", "102", "103", "104", "105" } }));
        }

        [Test]
        public async Task EachManagementShowsItsTotalsAndItsGateways()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<StatisticsReport> component = context.Render<StatisticsReport>(parameters => parameters
                .Add(p => p.Managements, [BuildManagement("first", 0), BuildManagement("second", 10)]));

            List<Collapse> managements = [.. component.FindComponents<Collapse>().Select(collapse => collapse.Instance)];
            List<AngleSharp.Dom.IElement> tables = [.. component.FindAll("table")];
            Assert.That(managements.Select(management => management.Title), Is.EqualTo((List<string>)["first", "second"]));
            Assert.That(Rows(tables[1]), Is.EqualTo(new List<List<string>> { new() { "1", "2", "3", "4", "5" } }));
            Assert.That(Rows(tables[2]), Is.EqualTo(new List<List<string>> { new() { "first gateway", "6", "7" } }));
            Assert.That(Rows(tables[4]), Is.EqualTo(new List<List<string>> { new() { "second gateway", "16", "17" } }));
        }

        [Test]
        public async Task IgnoredManagementsAreLeftOut()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<StatisticsReport> component = context.Render<StatisticsReport>(parameters => parameters
                .Add(p => p.Managements, [BuildManagement("shown", 0), BuildManagement("ignored", 10, ignore: true)]));

            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Is.EqualTo((List<string>)["shown"]));
        }
    }
}
