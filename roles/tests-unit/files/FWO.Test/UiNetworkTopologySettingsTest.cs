using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Services.EventMediator.Interfaces;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiNetworkTopologySettingsTest
    {
        private static readonly string[] kInternetExclusionRangeIds =
        [
            "cbx_internalZoneRange_10_0_0_0_8",
            "cbx_internalZoneRange_172_16_0_0_12",
            "cbx_internalZoneRange_192_168_0_0_16",
            "cbx_internalZoneRange_0_0_0_0_8",
            "cbx_internalZoneRange_127_0_0_0_8",
            "cbx_internalZoneRange_169_254_0_0_16",
            "cbx_internalZoneRange_224_0_0_0_4",
            "cbx_internalZoneRange_240_0_0_0_4",
            "cbx_internalZoneRange_255_255_255_255_32",
            "cbx_internalZoneRange_192_0_2_0_24",
            "cbx_internalZoneRange_198_51_100_0_24",
            "cbx_internalZoneRange_203_0_113_0_24",
            "cbx_internalZoneRange_100_64_0_0_10",
            "cbx_internalZoneRange_192_0_0_0_24",
            "cbx_internalZoneRange_192_88_99_0_24",
            "cbx_internalZoneRange_198_18_0_0_15"
        ];

        private sealed class NetworkTopologySettingsApiConnection : SimulatedApiConnection
        {
            public List<ComplianceCriterion> Matrices { get; } =
            [
                new ComplianceCriterion { Id = 1, Name = "First matrix" },
                new ComplianceCriterion { Id = 2, Name = "Second matrix" }
            ];

            public List<ConfigItem> LastUpsertConfigItems { get; private set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query,
                object? variables = null,
                string? operationName = null,
                QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ComplianceQueries.getMatrices && typeof(QueryResponseType) == typeof(List<ComplianceCriterion>))
                {
                    return Task.FromResult((QueryResponseType)(object)Matrices.ToList());
                }

                if (query == ComplianceQueries.getPolicies && typeof(QueryResponseType) == typeof(List<CompliancePolicy>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<CompliancePolicy>());
                }

                if (query == ConfigQueries.upsertConfigItems)
                {
                    PropertyInfo? configItemsProperty = variables?.GetType().GetProperty("config_items");
                    LastUpsertConfigItems = configItemsProperty == null
                        ? []
                        : ((IEnumerable<ConfigItem>)configItemsProperty.GetValue(variables)!).ToList();
                    return Task.FromResult(default(QueryResponseType)!);
                }

                throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
            }
        }

        [Test]
        public async Task InternetPageShowsAllExclusionRangesWhenAutomaticCalculationIsEnabled()
        {
            SimulatedGlobalConfig globalConfig = new() { AutoCalculateInternetZone = true };
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(Roles.Admin, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsInternet>(context);

            page.WaitForAssertion(() =>
            {
                List<string> renderedRangeIds = page.FindAll("input[id^='cbx_internalZoneRange_']")
                    .Select(element => element.Id)
                    .Where(id => id != null)
                    .Cast<string>()
                    .ToList();
                Assert.That(renderedRangeIds, Is.EquivalentTo(kInternetExclusionRangeIds));
            });
        }

        [Test]
        public async Task InternetPageHidesExclusionRangesWhenAutomaticCalculationIsDisabled()
        {
            SimulatedGlobalConfig globalConfig = new() { AutoCalculateInternetZone = false };
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(Roles.Admin, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsInternet>(context);

            page.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(page.FindAll("#lbl_auto_calc_internet_zone"), Has.Count.EqualTo(1));
                    Assert.That(page.FindAll("input[id^='cbx_internalZoneRange_']"), Is.Empty);
                });
            });
        }

        [Test]
        public async Task NetworkMatrixPageShowsExactlyTheFourMovedSettings()
        {
            SimulatedGlobalConfig globalConfig = CreateMatrixConfig();
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(Roles.Admin, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsNetworkMatrix>(context);

            page.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(page.FindAll("input[type='checkbox']"), Has.Count.EqualTo(3));
                    Assert.That(page.FindAll("#lbl_imported_matrix_readonly"), Has.Count.EqualTo(1));
                    Assert.That(page.FindComponents<Dropdown<ComplianceCriterion>>(), Has.Count.EqualTo(1));
                    Assert.That(page.Markup, Does.Contain("Designated zone matrix"));
                    Assert.That(page.Markup, Does.Contain("matrixAllowNestedZones"));
                    Assert.That(page.Markup, Does.Contain("sortMatrixByID"));
                });
            });
        }

        [Test]
        public async Task CompliancePageDoesNotShowMovedInternetOrMatrixSettings()
        {
            SimulatedGlobalConfig globalConfig = new();
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(Roles.Admin, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsCompliance>(context);

            page.WaitForAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(page.FindAll("#lbl_auto_calc_internet_zone"), Is.Empty);
                    Assert.That(page.FindAll("input[id^='cbx_internalZoneRange_']"), Is.Empty);
                    Assert.That(page.FindAll("#lbl_imported_matrix_readonly"), Is.Empty);
                    Assert.That(page.Markup, Does.Not.Contain("Designated zone matrix"));
                    Assert.That(page.Markup, Does.Not.Contain("matrixAllowNestedZones"));
                    Assert.That(page.Markup, Does.Not.Contain("sortMatrixByID"));
                });
            });
        }

        [TestCase(Roles.Admin, false)]
        [TestCase(Roles.Auditor, true)]
        [TestCase(Roles.FwAdmin, true)]
        public async Task InternetSaveButtonReflectsTheUserRole(string role, bool expectedDisabled)
        {
            SimulatedGlobalConfig globalConfig = new();
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(role, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsInternet>(context);

            page.WaitForAssertion(() =>
                Assert.That(FindSaveButton(page).HasAttribute("disabled"), Is.EqualTo(expectedDisabled)));
        }

        [TestCase(Roles.Admin, false)]
        [TestCase(Roles.Auditor, true)]
        [TestCase(Roles.FwAdmin, true)]
        public async Task NetworkMatrixSaveButtonReflectsTheUserRole(string role, bool expectedDisabled)
        {
            SimulatedGlobalConfig globalConfig = CreateMatrixConfig();
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(role, globalConfig, apiConnection);

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsNetworkMatrix>(context);

            page.WaitForAssertion(() =>
                Assert.That(FindSaveButton(page).HasAttribute("disabled"), Is.EqualTo(expectedDisabled)));
        }

        [Test]
        public async Task SavingNetworkMatrixWritesTheFourMovedConfigKeys()
        {
            SimulatedGlobalConfig globalConfig = CreateMatrixConfig();
            NetworkTopologySettingsApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(Roles.Admin, globalConfig, apiConnection);
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage<SettingsNetworkMatrix>(context);

            page.WaitForAssertion(() =>
                Assert.That(page.FindAll("input[type='checkbox']"), Has.Count.EqualTo(3)));
            page.FindAll("input[type='checkbox']")[0].Change(false);
            page.FindAll("input[type='checkbox']")[1].Change(true);
            page.FindAll("input[type='checkbox']")[2].Change(true);

            IRenderedComponent<Dropdown<ComplianceCriterion>> matrixDropdown =
                page.FindComponent<Dropdown<ComplianceCriterion>>();
            ComplianceCriterion selectedMatrix = apiConnection.Matrices.Single(matrix => matrix.Id == 2);
            await page.InvokeAsync(() => matrixDropdown.Instance.SelectedElementChanged.InvokeAsync(selectedMatrix));

            FindSaveButton(page).Click();

            page.WaitForAssertion(() =>
            {
                Dictionary<string, string?> savedValues = apiConnection.LastUpsertConfigItems
                    .ToDictionary(item => item.Key, item => item.Value);
                Assert.Multiple(() =>
                {
                    Assert.That(savedValues, Has.Count.EqualTo(4));
                    Assert.That(savedValues["importedMatrixReadOnly"], Is.EqualTo(bool.FalseString));
                    Assert.That(savedValues["complianceDesignatedZoneMatrix"], Is.EqualTo("2"));
                    Assert.That(savedValues["matrixAllowNestedZones"], Is.EqualTo(bool.TrueString));
                    Assert.That(savedValues["sortMatrixByID"], Is.EqualTo(bool.TrueString));
                });
            });
        }

        private static SimulatedGlobalConfig CreateMatrixConfig()
        {
            return new SimulatedGlobalConfig
            {
                ImportedMatrixReadOnly = true,
                ComplianceDesignatedZoneMatrixId = 1,
                MatrixAllowNestedZones = false,
                SortMatrixByID = false
            };
        }

        private static BunitContext CreateContext(
            string role,
            SimulatedGlobalConfig globalConfig,
            NetworkTopologySettingsApiConnection apiConnection)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles.Add(role);

            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<GlobalConfig>(globalConfig);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(new MockMiddlewareClient());
            context.Services.AddSingleton<IEventMediator>(new RecordingEventMediator());
            context.Services.AddScoped<DomEventService>();
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPage<PageType>(BunitContext context)
            where PageType : IComponent
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<PageType>());
        }

        private static IElement FindSaveButton(IRenderedComponent<CascadingAuthenticationState> page)
        {
            return page.FindAll("button")
                .Single(button => button.TextContent.Trim().Equals("Save", StringComparison.Ordinal));
        }
    }
}
