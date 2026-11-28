using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportTemplateSelectDeviceTest
    {
        private const int kManagementId = 1;
        private const int kSelectedDeviceId = 11;
        private const int kOtherDeviceId = 12;
        private const int kAvailableRulebaseId = 21;
        private const int kVanishedRulebaseId = 22;

        private sealed record DisplayedMessage(Exception? Exception, string Title);

        private sealed class SelectDeviceApiConnection : SimulatedApiConnection
        {
            public HashSet<string> FailingQueries { get; } = [];
            public List<string> SentQueries { get; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                SentQueries.Add(query);
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException("simulated api failure");
                }
                if (query == DeviceQueries.getDevicesByManagement)
                {
                    // a fresh, unselected copy as the api would deliver it
                    return Task.FromResult((QueryResponseType)(object)BuildManagements(selectFirstDevice: false));
                }
                if (query == DeviceQueries.getManagementsForRulebaseReporting)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<RulebaseManagementSelect>
                    {
                        new() { Id = kManagementId, Name = "management", Rulebases = [new RulebaseSelect { Id = kAvailableRulebaseId, Name = "start rulebase" }] }
                    });
                }
                throw new NotImplementedException($"Unhandled query {query}");
            }

            public int CountQuery(string query)
            {
                return SentQueries.Count(sentQuery => sentQuery == query);
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];
        private DeviceFilter? committedDeviceFilter;
        private List<SelectedRulebase>? committedRulebases;
        private bool? committedDisplay;

        private static List<ManagementSelect> BuildManagements(bool selectFirstDevice)
        {
            return
            [
                new ManagementSelect
                {
                    Id = kManagementId,
                    Name = "management",
                    Devices =
                    [
                        new DeviceSelect { Id = kSelectedDeviceId, Name = "first gateway", Selected = selectFirstDevice },
                        new DeviceSelect { Id = kOtherDeviceId, Name = "second gateway" }
                    ]
                }
            ];
        }

        private static BunitContext CreateContext(SelectDeviceApiConnection apiConnection, UserConfig? userConfig = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddLocalization();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig ?? new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private IRenderedComponent<ReportTemplateSelectDevice> Render(BunitContext context, ReportType reportType, bool display = true,
            DeviceFilter? deviceFilter = null, bool managementRulebaseView = false, List<SelectedRulebase>? selectedRulebases = null)
        {
            return context.Render<ReportTemplateSelectDevice>(parameters => parameters
                .AddCascadingValue<Action<Exception?, string, string, bool>>((exception, title, _, _) =>
                    displayedMessages.Add(new DisplayedMessage(exception, title)))
                .Add(p => p.Display, display)
                .Add(p => p.DisplayChanged, (bool shown) => committedDisplay = shown)
                .Add(p => p.DeviceFilter, deviceFilter ?? new DeviceFilter(BuildManagements(selectFirstDevice: true)))
                .Add(p => p.DeviceFilterChanged, (DeviceFilter filter) => committedDeviceFilter = filter)
                .Add(p => p.SelectedReportType, reportType)
                .Add(p => p.ManagementRulebaseView, managementRulebaseView)
                .Add(p => p.SelectedRulebases, selectedRulebases ?? [])
                .Add(p => p.SelectedRulebasesChanged, (List<SelectedRulebase> rulebases) => committedRulebases = rulebases));
        }

        private static IElement FooterButton(IRenderedComponent<ReportTemplateSelectDevice> dialog, string icon)
        {
            return dialog.FindAll("button").First(button => button.QuerySelector("span")?.ClassName == icon);
        }

        private static string Text(BunitContext context, string key)
        {
            return context.Services.GetRequiredService<UserConfig>().GetText(key);
        }

        private static string Title(IRenderedComponent<ReportTemplateSelectDevice> dialog)
        {
            return dialog.FindComponent<PopUp>().Instance.Title;
        }

        [Test]
        public async Task AHiddenDialogFetchesNothing()
        {
            SelectDeviceApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            Render(context, ReportType.Rules, display: false);

            Assert.That(apiConnection.SentQueries, Is.Empty);
        }

        [Test]
        public async Task OpeningTheDialogOffersTheCurrentDevicesAndKeepsTheTemplatesSelection()
        {
            SelectDeviceApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules);

            DeviceFilter offeredFilter = dialog.FindComponent<DeviceSelection>().Instance.DeviceFilter;
            Assert.That(apiConnection.CountQuery(DeviceQueries.getDevicesByManagement), Is.EqualTo(1));
            Assert.That(offeredFilter.Managements[0].Devices.Select(device => device.Selected), Is.EqualTo((List<bool>)[true, false]));
            Assert.That(dialog.FindComponent<DeviceSelection>().Instance.CollapseAll, Is.False);
            Assert.That(Title(dialog), Is.EqualTo(Text(context, "select_device")));
            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.Zero);
        }

        [TestCase(ReportType.AppRules)]
        [TestCase(ReportType.Recertification)]
        public async Task ReportTypesOnManagementLevelOfferAManagementSelection(ReportType reportType)
        {
            await using BunitContext context = CreateContext(new SelectDeviceApiConnection());

            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, reportType);

            Assert.That(dialog.FindComponents<ManagementSelection>(), Has.Count.EqualTo(1));
            Assert.That(dialog.FindComponents<DeviceSelection>(), Is.Empty);
            Assert.That(Title(dialog), Is.EqualTo(Text(context, "select_management")));
            // a device is selected already, so the select all button has to offer clearing instead
            Assert.That(dialog.FindComponent<ManagementSelection>().Instance.SelectAll, Is.False);
        }

        [Test]
        public async Task TheManagementRulebasesViewOffersTheRulebasesAndDropsVanishedOnes()
        {
            SelectDeviceApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);
            List<SelectedRulebase> selectedRulebases =
            [
                new SelectedRulebase { ManagementId = kManagementId, RulebaseId = kAvailableRulebaseId },
                new SelectedRulebase { ManagementId = kManagementId, RulebaseId = kVanishedRulebaseId }
            ];

            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules,
                managementRulebaseView: true, selectedRulebases: selectedRulebases);

            RulebaseSelection rulebaseSelection = dialog.FindComponent<RulebaseSelection>().Instance;
            Assert.That(Title(dialog), Is.EqualTo(Text(context, "select_rulebase")));
            Assert.That(rulebaseSelection.Managements.Select(management => management.Name), Is.EqualTo((List<string>)["management"]));
            Assert.That(rulebaseSelection.SelectedRulebases.Select(rulebase => rulebase.RulebaseId), Is.EqualTo((List<int>)[kAvailableRulebaseId]));
        }

        [Test]
        public async Task ManyDevicesAreShownCollapsed()
        {
            SimulatedUserConfig userConfig = new() { MinCollapseAllDevices = 1 };
            await using BunitContext context = CreateContext(new SelectDeviceApiConnection(), userConfig);

            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules);

            Assert.That(dialog.FindComponent<DeviceSelection>().Instance.CollapseAll, Is.True);
        }

        [Test]
        public async Task CommittingHandsTheSelectionBackAndClosesTheDialog()
        {
            await using BunitContext context = CreateContext(new SelectDeviceApiConnection());
            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules);
            DeviceFilter offeredFilter = dialog.FindComponent<DeviceSelection>().Instance.DeviceFilter;
            offeredFilter.Managements[0].Devices[1].Selected = true;

            FooterButton(dialog, Icons.Save).Click();

            Assert.That(committedDisplay, Is.False);
            Assert.That(committedRulebases, Is.Not.Null);
            Assert.That(committedDeviceFilter!.Managements[0].Devices.Select(device => device.Selected), Is.EqualTo((List<bool>)[true, true]));
        }

        [Test]
        public async Task CancellingRestoresTheSelectionTheDialogWasOpenedWith()
        {
            await using BunitContext context = CreateContext(new SelectDeviceApiConnection());
            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules,
                selectedRulebases: [new SelectedRulebase { ManagementId = kManagementId, RulebaseId = kAvailableRulebaseId }]);
            DeviceFilter offeredFilter = dialog.FindComponent<DeviceSelection>().Instance.DeviceFilter;
            offeredFilter.Managements[0].Devices[0].Selected = false;
            offeredFilter.Managements[0].Devices[1].Selected = true;

            FooterButton(dialog, Icons.Cancel).Click();

            Assert.That(committedDisplay, Is.False);
            Assert.That(committedDeviceFilter!.Managements[0].Devices.Select(device => device.Selected), Is.EqualTo((List<bool>)[true, false]));
            Assert.That(committedRulebases!.Select(rulebase => rulebase.RulebaseId), Is.EqualTo((List<int>)[kAvailableRulebaseId]));
        }

        [Test]
        public async Task TheDevicesAreFetchedOnlyOncePerOpening()
        {
            SelectDeviceApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);
            IRenderedComponent<ReportTemplateSelectDevice> dialog = Render(context, ReportType.Rules);

            dialog.Render(parameters => parameters.Add(p => p.SelectedReportType, ReportType.Statistics));
            int queriesWhileOpen = apiConnection.CountQuery(DeviceQueries.getDevicesByManagement);
            FooterButton(dialog, Icons.Save).Click();
            dialog.Render(parameters => parameters.Add(p => p.Display, true));

            Assert.That(queriesWhileOpen, Is.EqualTo(1));
            Assert.That(apiConnection.CountQuery(DeviceQueries.getDevicesByManagement), Is.EqualTo(2));
        }

        [Test]
        public async Task AFailingDeviceFetchIsReportedInTheUi()
        {
            SelectDeviceApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(DeviceQueries.getDevicesByManagement);
            await using BunitContext context = CreateContext(apiConnection);

            Render(context, ReportType.Rules);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(Text(context, "object_fetch")));
        }

        [Test]
        public async Task AFailingRulebaseFetchIsReportedInTheUi()
        {
            SelectDeviceApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(DeviceQueries.getManagementsForRulebaseReporting);
            await using BunitContext context = CreateContext(apiConnection);

            Render(context, ReportType.Rules, managementRulebaseView: true);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task AnUnexpectedErrorWhileOpeningIsReportedInsteadOfTakingTheDialogDown()
        {
            await using BunitContext context = CreateContext(new SelectDeviceApiConnection());

            context.Render<ReportTemplateSelectDevice>(parameters => parameters
                .AddCascadingValue<Action<Exception?, string, string, bool>>((exception, title, _, _) =>
                    displayedMessages.Add(new DisplayedMessage(exception, title)))
                .Add(p => p.Display, true)
                .Add(p => p.ManagementRulebaseView, true)
                .Add(p => p.DeviceFilter, null!));

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<NullReferenceException>());
        }
    }
}
