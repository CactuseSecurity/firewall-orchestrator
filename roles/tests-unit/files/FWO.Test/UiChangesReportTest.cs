using AngleSharp.Dom;
using Bunit;
using FWO.Basics;
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
    internal class UiChangesReportTest
    {
        private const char kAdded = 'I';
        private const char kModified = 'C';
        private const char kDeleted = 'D';

        private readonly SimulatedUserConfig userConfig = new();
        private int selectionChanges;

        private BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<UserConfig>(userConfig);
            return context;
        }

        private static RuleChange BuildRuleChange(char changeAction, string ruleName)
        {
            return new RuleChange
            {
                ChangeAction = changeAction,
                ChangeImport = new ChangeImport { Time = new DateTime(2026, 4, 5, 6, 7, 8) },
                OldRule = new Rule { Id = 1, Name = ruleName, Action = "drop" },
                NewRule = new Rule { Id = 2, Name = ruleName, Action = "accept" }
            };
        }

        private static ManagementReport BuildManagement(string name, params RuleChange[] ruleChanges)
        {
            return new ManagementReport { Id = 1, Name = name, RuleChanges = ruleChanges };
        }

        private IRenderedComponent<ChangesReport> Render(BunitContext context, List<ManagementReport> managements,
            ReportType reportType = ReportType.Changes, bool includeObjects = false, List<RuleChange>? selectedChanges = null)
        {
            return context.Render<ChangesReport>(parameters => parameters
                .Add(p => p.Managements, managements)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.IncludeObjects, includeObjects)
                .Add(p => p.SelectedRuleChanges, selectedChanges ?? [])
                .Add(p => p.SelectedRuleChangesChanged, (List<RuleChange> _) => selectionChanges++));
        }

        private static List<string> ColumnTitles(IElement table)
        {
            return [.. table.QuerySelectorAll("thead th span:not([aria-hidden])").Select(title => title.TextContent.Trim())];
        }

        private List<string> Titles(params string[] keys)
        {
            return [.. keys.Select(userConfig.GetText)];
        }

        [Test]
        public async Task ManagementsWithoutRuleChangesAreLeftOut()
        {
            await using BunitContext context = CreateContext();
            ManagementReport unchanged = new() { Id = 2, Name = "unchanged", RuleChanges = [] };
            ManagementReport unknown = new() { Id = 3, Name = "not queried", RuleChanges = null };

            IRenderedComponent<ChangesReport> component = Render(context,
                [BuildManagement("changed", BuildRuleChange(kAdded, "new rule")), unchanged, unknown]);

            Assert.That(component.FindComponents<Collapse>().Select(collapse => collapse.Instance.Title), Is.EqualTo((List<string>)["changed"]));
        }

        [Test]
        public async Task WithoutASelectionListNothingIsShown()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ChangesReport> component = context.Render<ChangesReport>(parameters => parameters
                .Add(p => p.Managements, [BuildManagement("changed", BuildRuleChange(kAdded, "new rule"))])
                .Add(p => p.SelectedRuleChanges, null));

            Assert.That(component.FindAll("table"), Is.Empty);
        }

        [Test]
        public async Task TheRuleChangesShowWhenAndHowEachRuleChanged()
        {
            await using BunitContext context = CreateContext();
            RuleChange modification = BuildRuleChange(kModified, "changed rule");

            IRenderedComponent<ChangesReport> component = Render(context, [BuildManagement("changed", modification)]);

            IElement table = component.Find("table");
            Assert.That(ColumnTitles(table), Is.EqualTo(Titles("change_time", "change_type", "name", "source_zone", "source", "destination_zone",
                "destination", "services", "action", "track", "enabled", "enforcing_devices", "uid", "comment")));
            string row = component.Find("tbody tr").TextContent;
            Assert.That(row, Does.Contain(modification.ChangeImport.Time.ToString()));
            Assert.That(row, Does.Contain("changed rule"));
            Assert.That(row, Does.Contain("drop").And.Contain("accept"));
        }

        [Test]
        public async Task ANatChangesReportAlsoShowsTheTranslation()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<ChangesReport> component = Render(context, [BuildManagement("changed", BuildRuleChange(kDeleted, "old rule"))], ReportType.NatChanges);

            Assert.That(ColumnTitles(component.Find("table")), Is.SupersetOf(Titles("trans_source", "trans_destination", "trans_services")));
        }

        [Test]
        public async Task ClickingAChangeSelectsItWithItsManagementAndClickingItAgainDeselectsIt()
        {
            await using BunitContext context = CreateContext();
            RuleChange change = BuildRuleChange(kAdded, "new rule");
            List<RuleChange> selection = [];
            IRenderedComponent<ChangesReport> component = Render(context, [BuildManagement("changed", change)], selectedChanges: selection);

            component.Find("tbody tr").Click();
            List<RuleChange> afterFirstClick = [.. selection];
            component.Find("tbody tr").Click();

            Assert.That(afterFirstClick, Is.EqualTo(new List<RuleChange> { change }));
            Assert.That(change.RulebaseName, Is.EqualTo("changed"));
            Assert.That(selection, Is.Empty);
            Assert.That(selectionChanges, Is.EqualTo(2));
        }

        [Test]
        public async Task ObjectChangesAreOnlyShownWhenIncluded()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagement("changed", BuildRuleChange(kAdded, "new rule"));
            management.ObjectChanges = [new ObjectChange { ChangeAction = kAdded, NewObject = new NetworkObject { Name = "new host" } }];
            management.ServiceChanges = [new ServiceChange { ChangeAction = kAdded, NewService = new NetworkService { Name = "new service" } }];
            management.UserChanges = [new UserChange { ChangeAction = kAdded, NewUser = new NetworkUser { Name = "new user" } }];

            IRenderedComponent<ChangesReport> component = Render(context, [management]);

            Assert.That(component.FindAll("table"), Has.Count.EqualTo(1));
            Assert.That(component.Markup, Does.Not.Contain("new host"));
        }

        [Test]
        public async Task IncludedObjectChangesShowNetworkObjectsServicesAndUsers()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagement("changed", BuildRuleChange(kAdded, "new rule"));
            management.ObjectChanges = [new ObjectChange { ChangeAction = kAdded, NewObject = new NetworkObject { Name = "new host", IP = "10.0.0.1/32" } }];
            management.ServiceChanges = [new ServiceChange { ChangeAction = kDeleted, OldService = new NetworkService { Name = "old service" } }];
            management.UserChanges = [new UserChange { ChangeAction = kModified, OldUser = new NetworkUser { Name = "user" }, NewUser = new NetworkUser { Name = "user" } }];

            IRenderedComponent<ChangesReport> component = Render(context, [management], includeObjects: true);

            List<IElement> tables = [.. component.FindAll("table")];
            Assert.That(tables, Has.Count.EqualTo(4));
            Assert.That(ColumnTitles(tables[1]), Is.EqualTo(Titles("change_time", "change_type", "name", "type", "ip_address", "members", "uid", "comment")));
            Assert.That(ColumnTitles(tables[2]), Is.EqualTo(Titles("change_time", "change_type", "name", "type", "protocol", "port", "members", "uid", "comment")));
            Assert.That(ColumnTitles(tables[3]), Is.EqualTo(Titles("change_time", "change_type", "name", "comment")));
            Assert.That(tables[1].TextContent, Does.Contain("new host"));
            Assert.That(tables[2].TextContent, Does.Contain("old service"));
            Assert.That(tables[3].TextContent, Does.Contain("user"));
        }

        [Test]
        public async Task EmptyObjectChangeListsShowNoTables()
        {
            await using BunitContext context = CreateContext();
            ManagementReport management = BuildManagement("changed", BuildRuleChange(kAdded, "new rule"));
            management.ObjectChanges = [];
            management.ServiceChanges = null;
            management.UserChanges = [];

            IRenderedComponent<ChangesReport> component = Render(context, [management], includeObjects: true);

            Assert.That(component.FindAll("table"), Has.Count.EqualTo(1));
        }
    }
}
