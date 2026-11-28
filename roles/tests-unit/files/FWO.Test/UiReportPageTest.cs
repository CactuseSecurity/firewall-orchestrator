using AngleSharp.Dom;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Middleware.Client;
using FWO.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Pages.Reporting.Reports;
using FWO.Ui.Services;
using FWO.Ui.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
// 'Report' on its own collides with the FWO.Report namespace
using ReportPage = FWO.Ui.Pages.Reporting.Report;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiReportPageTest
    {
        private const int kNavbarHeight = 50;
        private const int kAppId = 5;
        private const int kTenantId = 3;
        private const string kOwnerAddInfoKey = "cost center";

        private sealed record DisplayedMessage(Exception? Exception, string Title, string Text, bool Error);

        private sealed class ReportPageApiConnection : SimulatedApiConnection
        {
            public List<ManagementSelect> Managements { get; set; } = [];
            public List<RulebaseManagementSelect> RulebaseManagements { get; set; } = [];
            public List<FwoOwner> Owners { get; set; } = [];
            public Tenant[] Tenants { get; set; } = [];
            public List<ManagementReport> ManagementReports { get; set; } = [];
            public HashSet<string> FailingQueries { get; } = [];
            public List<string> SentQueries { get; } = [];
            public object? OwnerQueryVariables { get; private set; }
            public bool FailRoleSelection { get; set; }

            /// <summary>
            /// Holds back every query answer until the test releases it, so that the page can be
            /// inspected while a report is still being generated.
            /// </summary>
            public TaskCompletionSource? QueryGate { get; set; }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                SentQueries.Add(query);
                if (QueryGate != null)
                {
                    await QueryGate.Task;
                }
                if (FailingQueries.Contains(query))
                {
                    throw new InvalidOperationException("simulated api failure");
                }
                if (query == DeviceQueries.getDevicesByManagement)
                {
                    return (QueryResponseType)(object)Managements;
                }
                if (query == DeviceQueries.getManagementsForRulebaseReporting)
                {
                    return (QueryResponseType)(object)RulebaseManagements;
                }
                if (query == AuthQueries.getTenants)
                {
                    // the tenant selection reads a list, the template loading an array
                    return typeof(QueryResponseType) == typeof(List<Tenant>)
                        ? (QueryResponseType)(object)Tenants.ToList()
                        : (QueryResponseType)(object)Tenants;
                }
                if (typeof(QueryResponseType) == typeof(List<ManagementReport>) && ManagementReports.Count > 0)
                {
                    return (QueryResponseType)(object)ManagementReports.ConvertAll(management => new ManagementReport { Id = management.Id });
                }
                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    if (query == OwnerQueries.getEditableOwners)
                    {
                        OwnerQueryVariables = variables;
                    }
                    return (QueryResponseType)(object)Owners;
                }
                return CreateEmptyResponse<QueryResponseType>();
            }

            public override async Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
            {
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    // the right sidebar needs at least one management with an import to look at
                    SentQueries.Add(query);
                    return new ApiResponse<QueryResponseType>((QueryResponseType)(object)new List<ManagementReport> { new() { Id = 1 } });
                }
                return await base.SendQuerySafeAsync<QueryResponseType>(query, variables, operationName);
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            {
                if (FailRoleSelection)
                {
                    throw new InvalidOperationException("simulated role selection failure");
                }
            }

            public int CountQuery(string query)
            {
                return SentQueries.Count(sentQuery => sentQuery == query);
            }

            private static QueryResponseType CreateEmptyResponse<QueryResponseType>()
            {
                Type responseType = typeof(QueryResponseType);
                if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return Activator.CreateInstance<QueryResponseType>();
                }
                if (responseType.IsArray)
                {
                    return (QueryResponseType)(object)Array.CreateInstance(responseType.GetElementType()!, 0);
                }
                return default!;
            }
        }

        private static readonly List<Type> ReportViews =
        [
            typeof(RulesReport), typeof(ComplianceReport), typeof(ChangesReport), typeof(StatisticsReport), typeof(ConnectionsReport),
            typeof(VariancesReport), typeof(OwnerRecertReport), typeof(OwnersReport), typeof(RecertEventReport), typeof(ReportedTickets)
        ];

        private readonly List<DisplayedMessage> displayedMessages = [];

        private static BunitContext CreateContext(ReportPageApiConnection? apiConnection = null, UserConfig? userConfig = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection ?? new ReportPageApiConnection());
            context.Services.AddSingleton<UserConfig>(userConfig ?? new SimulatedUserConfig());
            context.Services.AddSingleton<IRuleTreeBuilder>(new RuleTreeBuilder());
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddScoped<DomEventService>();
            // the sidebars only show their content once the navbar height is known
            context.Services.GetRequiredService<DomEventService>().InvokeNavbarHeightChanged(kNavbarHeight);
            return context;
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPage(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters =>
                parameters.AddChildContent<ReportPage>());
        }

        /// <summary>
        /// Renders the page with a message handler that records every message it shows in the ui.
        /// </summary>
        private IRenderedComponent<ReportPage> RenderReportPage(BunitContext context, string? appId = null)
        {
            IRenderedComponent<CascadingAuthenticationState> page = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (exception, title, text, error) =>
                        displayedMessages.Add(new DisplayedMessage(exception, title, text, error)))
                    .AddChildContent<ReportPage>(report => report.Add(p => p.AppId, appId))));
            return page.FindComponent<ReportPage>();
        }

        private static SimulatedUserConfig BuildUserConfig(params string[] roles)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.DbId = 1;
            userConfig.User.Roles = [.. roles];
            return userConfig;
        }

        private static List<ManagementSelect> BuildManagements(bool deviceSelected)
        {
            return
            [
                new ManagementSelect
                {
                    Id = 1,
                    Name = "management",
                    Devices = [new DeviceSelect { Id = 11, Name = "gateway", Selected = deviceSelected }]
                }
            ];
        }

        private static ReportPageApiConnection BuildApiWithSelectedDevice()
        {
            return new ReportPageApiConnection { Managements = BuildManagements(deviceSelected: true) };
        }

        private static List<RulebaseManagementSelect> BuildRulebaseManagements()
        {
            return [new RulebaseManagementSelect { Id = 1, Name = "management", Rulebases = [new RulebaseSelect { Id = 21, Name = "start rulebase" }] }];
        }

        private static async Task SelectReportType(IRenderedComponent<ReportPage> page, ReportType reportType)
        {
            Dropdown<ReportType> dropdown = page.FindComponents<Dropdown<ReportType>>()[0].Instance;
            await page.InvokeAsync(() => dropdown.SelectedElementChanged.InvokeAsync(reportType));
        }

        private static ReportType SelectedReportType(IRenderedComponent<ReportPage> page)
        {
            return page.FindComponents<Dropdown<ReportType>>()[0].Instance.SelectedElement;
        }

        /// <summary>
        /// Finds the button showing the given icon, as buttons only show their icon by default.
        /// </summary>
        private static IElement ButtonWithIcon(IRenderedComponent<ReportPage> page, string icon)
        {
            return page.FindAll("button").First(button => button.QuerySelector("span")?.ClassName == icon);
        }

        private static IElement GenerateButton(IRenderedComponent<ReportPage> page)
        {
            return ButtonWithIcon(page, Icons.GenerateReport);
        }

        private static List<IElement> CreateTicketButtons(IRenderedComponent<ReportPage> page)
        {
            return [.. page.FindAll("button").Where(button => button.QuerySelector("span")?.ClassName == Icons.Workflow)];
        }

        private static async Task GenerateReport(IRenderedComponent<ReportPage> page)
        {
            await page.InvokeAsync(() => GenerateButton(page).Click());
            page.WaitForAssertion(() => Assert.That(GetField<bool>(page.Instance, "processing"), Is.False));
        }

        private static T GetField<T>(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{fieldName} field not found");
            return (T)field.GetValue(component)!;
        }

        private static void SetField(ReportPage reportPage, string fieldName, object? value)
        {
            FieldInfo field = typeof(ReportPage).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{fieldName} field not found");
            field.SetValue(reportPage, value);
        }

        /// <summary>
        /// Puts a report of the given type on the page, as a finished generation would, without going
        /// through the generators - their data fetching is covered by their own tests.
        /// </summary>
        private static async Task ShowReport(IRenderedComponent<ReportPage> page, ReportBase report)
        {
            await page.InvokeAsync(() =>
            {
                SetField(page.Instance, "currentReport", report);
                page.Render();
            });
        }

        private static ReportBase ConstructReport(BunitContext context, ReportType reportType)
        {
            ReportTemplate template = new("", new ReportParams((int)reportType, new DeviceFilter()));
            template.ReportParams.UnusedFilter = new UnusedFilter { UnusedForDays = 30 };
            return ReportBase.ConstructReport(template, context.Services.GetRequiredService<UserConfig>(), new RuleTreeBuilder());
        }

        private static bool ShowsComponentOfType<TComponent>(IRenderedComponent<ReportPage> page) where TComponent : IComponent
        {
            return page.FindComponents<TComponent>().Count > 0;
        }

        /// <summary>
        /// Whether the page currently shows a component of the given type, for test cases that name it as data.
        /// </summary>
        private static bool ShowsComponent(IRenderedComponent<ReportPage> page, Type componentType)
        {
            MethodInfo showsComponentOfType = typeof(UiReportPageTest).GetMethod(nameof(ShowsComponentOfType), BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{nameof(ShowsComponentOfType)} not found");
            return (bool)showsComponentOfType.MakeGenericMethod(componentType).Invoke(null, [page])!;
        }

        private List<string> DisplayedTexts()
        {
            return [.. displayedMessages.Select(message => message.Text)];
        }

        [Test]
        public async Task TheReportPageRenders()
        {
            await using BunitContext context = CreateContext();

            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);

            Assert.That(page.FindComponent<ReportPage>(), Is.Not.Null);
        }

        /// <summary>
        /// Reads the page's cancellation token source. It is private because nothing but the page
        /// itself should touch it, but its lifetime is exactly what these tests are about.
        /// </summary>
        private static CancellationTokenSource GetTokenSource(ReportPage reportPage)
        {
            return GetField<CancellationTokenSource>(reportPage, "tokenSource");
        }

        private static bool IsDisposed(CancellationTokenSource tokenSource)
        {
            try
            {
                _ = tokenSource.Token;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        [Test]
        public async Task DisposingThePageCancelsAGenerationThatIsStillRunning()
        {
            // without this an abandoned generation keeps building a complete report into memory for
            // a circuit that is already gone
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.That(tokenSource.IsCancellationRequested, Is.True);
        }

        [Test]
        public async Task DisposingThePageReleasesItsTokenSource()
        {
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task DisposingThePageTwiceDoesNotThrow()
        {
            // the framework disposes the page, and losing the circuit disposes it again - a second
            // Cancel on the already disposed source would take the teardown down with it
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);

            reportPage.Dispose();

            Assert.DoesNotThrow(reportPage.Dispose);
            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task DisposingThePageSurvivesAFailingCancellation()
        {
            // Cancel rethrows whatever a cancellation callback threw, and that must not escape the
            // teardown - the token source still has to be released
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            ReportPage reportPage = page.FindComponent<ReportPage>().Instance;
            CancellationTokenSource tokenSource = GetTokenSource(reportPage);
            tokenSource.Token.Register(() => throw new InvalidOperationException("callback failed"));

            Assert.DoesNotThrow(reportPage.Dispose);
            Assert.That(IsDisposed(tokenSource), Is.True);
        }

        [Test]
        public async Task GeneratingAReportReleasesThePreviousTokenSource()
        {
            // every generation used to leave its predecessor behind undisposed
            await using BunitContext context = CreateContext();
            IRenderedComponent<CascadingAuthenticationState> page = RenderPage(context);
            IRenderedComponent<ReportPage> reportPage = page.FindComponent<ReportPage>();

            reportPage.Find("button.btn-primary").Click();
            CancellationTokenSource firstTokenSource = GetTokenSource(reportPage.Instance);
            reportPage.Find("button.btn-primary").Click();
            CancellationTokenSource secondTokenSource = GetTokenSource(reportPage.Instance);

            Assert.That(secondTokenSource, Is.Not.SameAs(firstTokenSource));
            Assert.That(IsDisposed(firstTokenSource), Is.True);
            Assert.That(IsDisposed(secondTokenSource), Is.False);
        }

        [Test]
        public async Task AUserWithoutAnySelectableReportTypeGetsAnEmptyChooserAndNoDataIsFetched()
        {
            ReportPageApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig());

            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            Assert.That(page.FindComponents<Dropdown<ReportType>>()[0].Instance.Elements, Is.Empty);
            Assert.That(apiConnection.CountQuery(DeviceQueries.getDevicesByManagement), Is.Zero);
        }

        [Test]
        public async Task TheInitializationFetchesTheManagementsAndStartsWithTheRulesReport()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));

            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            Assert.That(apiConnection.CountQuery(DeviceQueries.getDevicesByManagement), Is.EqualTo(1));
            Assert.That(SelectedReportType(page), Is.EqualTo(ReportType.Rules));
            Assert.That(page.FindComponents<DeviceSelection>(), Is.Not.Empty);
            Assert.That(page.FindComponents<RightSidebar>(), Is.Not.Empty);
            // the rulebases are only needed in management rulebases view, which is not the default
            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.Zero);
        }

        [Test]
        public async Task TheReportTypeChooserShowsTheLocalizedTypeNames()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            page.FindComponents<Dropdown<ReportType>>()[0].Find("input").Focus();

            Assert.That(page.FindAll("button.dropdown-item").Select(button => button.TextContent.Trim()),
                Does.Contain(SimulatedUserConfig.DummyTranslate[nameof(ReportType.Changes)]));
        }

        [Test]
        public async Task AReportTypeThatIsNotAvailableIsReplacedByTheFirstAvailableOne()
        {
            SimulatedUserConfig userConfig = BuildUserConfig(Roles.Admin);
            userConfig.AvailableReportTypes = System.Text.Json.JsonSerializer.Serialize(new List<ReportType> { ReportType.Changes, ReportType.Statistics });
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), userConfig);

            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            Assert.That(SelectedReportType(page), Is.EqualTo(ReportType.Changes));
            Assert.That(page.FindAll("#includeObjectChanges"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AFailingInitializationIsReportedInTheUi()
        {
            ReportPageApiConnection apiConnection = new();
            apiConnection.FailingQueries.Add(DeviceQueries.getDevicesByManagement);
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));

            RenderReportPage(context);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(context.Services.GetRequiredService<UserConfig>().GetText("object_fetch")));
        }

        [Test]
        public async Task TheManagementRulebasesViewAsDefaultLoadsTheRulebasesRightAway()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(true), RulebaseManagements = BuildRulebaseManagements() };
            SimulatedUserConfig userConfig = BuildUserConfig(Roles.Admin);
            userConfig.DefaultManagementRulebaseView = true;
            await using BunitContext context = CreateContext(apiConnection, userConfig);

            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.EqualTo(1));
            Assert.That(page.FindComponent<RulebaseSelection>().Instance.Managements.Select(management => management.Name),
                Is.EqualTo((List<string>)["management"]));
        }

        [Test]
        public async Task TheRulebasesAreLoadedOnlyOnce()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(true), RulebaseManagements = BuildRulebaseManagements() };
            SimulatedUserConfig userConfig = BuildUserConfig(Roles.Admin);
            userConfig.DefaultManagementRulebaseView = true;
            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, ReportType.Changes);
            await SelectReportType(page, ReportType.Rules);

            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.EqualTo(1));
        }

        [Test]
        public async Task AFailingRulebaseFetchIsReportedInTheUi()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            apiConnection.FailingQueries.Add(DeviceQueries.getManagementsForRulebaseReporting);
            SimulatedUserConfig userConfig = BuildUserConfig(Roles.Admin);
            userConfig.DefaultManagementRulebaseView = true;
            await using BunitContext context = CreateContext(apiConnection, userConfig);

            RenderReportPage(context);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task SwitchingToTheManagementRulebasesViewLoadsTheRulebases()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(true), RulebaseManagements = BuildRulebaseManagements() };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            RulesViewSelect rulesViewSelect = page.FindComponent<RulesViewSelect>().Instance;
            await page.InvokeAsync(() => rulesViewSelect.ManagementRulebaseViewChanged.InvokeAsync(true));

            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.EqualTo(1));
            Assert.That(page.FindComponents<RulebaseSelection>(), Is.Not.Empty);
            Assert.That(page.FindComponents<DeviceSelection>(), Is.Empty);
        }

        [Test]
        public async Task AnInjectedAppIdSelectsItsOwnerAndGeneratesTheConnectionsReportRightAway()
        {
            ReportPageApiConnection apiConnection = new()
            {
                Owners = [new FwoOwner { Id = 2, Name = "app owner", ExtAppId = "APP-5", AdditionalInfo = new() { [kOwnerAddInfoKey] = "4711" } }]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));

            IRenderedComponent<ReportPage> page = RenderReportPage(context, kAppId.ToString());

            Assert.That(apiConnection.OwnerQueryVariables?.GetType().GetProperty("appIds")?.GetValue(apiConnection.OwnerQueryVariables), Is.EqualTo(kAppId));
            Assert.That(SelectedReportType(page), Is.EqualTo(ReportType.Connections));
            page.WaitForAssertion(() => Assert.That(page.FindComponents<ConnectionsReport>(), Is.Not.Empty));
            Assert.That(page.FindComponent<ReportModellingParamSelection>().Instance.AvailableAddInfoNames, Is.EqualTo((List<string>)[kOwnerAddInfoKey]));
        }

        [Test]
        public async Task AnInjectedAppIdWithoutMatchingOwnerDoesNotGenerateAReport()
        {
            ReportPageApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));

            IRenderedComponent<ReportPage> page = RenderReportPage(context, kAppId.ToString());

            Assert.That(page.FindComponents<ConnectionsReport>(), Is.Empty);
            Assert.That(page.FindComponent<ReportModellingParamSelection>().Instance.AvailableAddInfoNames, Is.Null);
        }

        [Test]
        public async Task ChangingTheReportTypeDropsTheInjectedAppIdFromTheAddress()
        {
            await using BunitContext context = CreateContext(new ReportPageApiConnection(), BuildUserConfig(Roles.Admin));
            NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
            navigationManager.NavigateTo($"{PageName.ReportGeneration}/{kAppId}");
            IRenderedComponent<ReportPage> page = RenderReportPage(context, kAppId.ToString());

            await SelectReportType(page, ReportType.Owners);

            Assert.That(navigationManager.Uri, Does.EndWith(PageName.ReportGeneration));
            Assert.That(page.FindComponents<ReportModellingParamSelection>(), Is.Empty);
        }

        [TestCase(ReportType.Connections, typeof(ReportModellingParamSelection))]
        [TestCase(ReportType.Owners, typeof(ReportOwnerParamSelection))]
        [TestCase(ReportType.Recertification, typeof(ReportRecertParamSelection))]
        [TestCase(ReportType.OwnerRecertification, typeof(ReportRecertParamSelection))]
        [TestCase(ReportType.TicketReport, typeof(ReportWorkflowParamSelection))]
        [TestCase(ReportType.Rules, typeof(RulesViewSelect))]
        [TestCase(ReportType.Rules, typeof(ReportTenantSelection))]
        [TestCase(ReportType.AppRules, typeof(ManagementSelection))]
        [TestCase(ReportType.Statistics, typeof(DeviceSelection))]
        public async Task EachReportTypeShowsItsParameterSelection(ReportType reportType, Type expectedComponent)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, ReportType.Changes);
            await SelectReportType(page, reportType);

            Assert.That(ShowsComponent(page, expectedComponent), Is.True);
        }

        [TestCase(ReportType.Statistics)]
        [TestCase(ReportType.AppRules)]
        [TestCase(ReportType.RecertEventReport)]
        public async Task ReportTypesWithoutTenantRelatedRulesHideTheTenantSelection(ReportType reportType)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, reportType);

            Assert.That(page.FindComponents<ReportTenantSelection>(), Is.Empty);
        }

        [TestCase(ReportType.UnusedRules, "input[type=number]")]
        [TestCase(ReportType.Changes, "#includeObjectChanges")]
        [TestCase(ReportType.ComplianceReport, "#complianceShowAnyRule")]
        [TestCase(ReportType.ComplianceDiffReport, "#complianceFilterOutInitialViolations")]
        public async Task EachReportTypeShowsItsOwnSettings(ReportType reportType, string selector)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, reportType);

            Assert.That(page.FindAll(selector), Has.Count.EqualTo(1));
        }

        [TestCase(ReportType.ComplianceReport)]
        [TestCase(ReportType.TicketReport)]
        [TestCase(ReportType.Owners)]
        public async Task ReportTypesWithoutRulesHideTheRightSidebar(ReportType reportType)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, reportType);

            Assert.That(page.FindComponents<RightSidebar>(), Is.Empty);
        }

        [Test]
        public async Task ReportTypesThatNeedAllDevicesSelectThemAndReleaseThemAgainOnTheWayBack()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(deviceSelected: false) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            DeviceSelect device = apiConnection.Managements[0].Devices[0];

            await SelectReportType(page, ReportType.AppRules);
            bool selectedForAppRules = device.Selected;
            await SelectReportType(page, ReportType.Rules);

            Assert.That(selectedForAppRules, Is.True);
            Assert.That(device.Selected, Is.False);
        }

        [Test]
        public async Task ReportTypesThatNeedAllDevicesKeepAnExistingDeviceSelection()
        {
            ReportPageApiConnection apiConnection = new()
            {
                Managements =
                [
                    new ManagementSelect
                    {
                        Id = 1,
                        Devices = [new DeviceSelect { Id = 11, Selected = true }, new DeviceSelect { Id = 12, Selected = false }]
                    }
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await SelectReportType(page, ReportType.RecertEventReport);

            Assert.That(apiConnection.Managements[0].Devices[1].Selected, Is.False);
        }

        [Test]
        public async Task AFailingRoleSwitchOnAReportTypeChangeDoesNotTakeThePageDown()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            apiConnection.FailRoleSelection = true;

            await SelectReportType(page, ReportType.Changes);

            Assert.That(page.FindComponents<ReportTemplateComponent>(), Is.Not.Empty);
            Assert.That(displayedMessages, Is.Empty);
        }

        [Test]
        public async Task GeneratingWithoutASelectedDeviceIsRefused()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(deviceSelected: false) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await GenerateReport(page);

            Assert.That(displayedMessages.Select(message => message.Title), Is.EqualTo((List<string>)["No device selected"]));
            Assert.That(page.FindComponents<RulesReport>(), Is.Empty);
        }

        [Test]
        public async Task GeneratingTheManagementRulebasesViewWithoutASelectedRulebaseIsRefused()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(true), RulebaseManagements = BuildRulebaseManagements() };
            SimulatedUserConfig userConfig = BuildUserConfig(Roles.Admin);
            userConfig.DefaultManagementRulebaseView = true;
            await using BunitContext context = CreateContext(apiConnection, userConfig);
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await GenerateReport(page);

            Assert.That(displayedMessages.Select(message => message.Title), Is.EqualTo((List<string>)["no_rulebase_selected"]));
        }

        [Test]
        public async Task ARulesReportWithoutRulesSaysSo()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await GenerateReport(page);

            page.WaitForAssertion(() => Assert.That(DisplayedTexts(), Does.Contain(SimulatedUserConfig.DummyTranslate["E4002"])));
            Assert.That(page.FindComponents<RulesReport>(), Is.Not.Empty);
        }

        [Test]
        public async Task AChangesReportWithoutChangesSaysSo()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            apiConnection.ManagementReports = [new ManagementReport { Id = 1 }];
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.Changes);

            await GenerateReport(page);

            page.WaitForAssertion(() => Assert.That(DisplayedTexts(), Does.Contain("E4003")));
            Assert.That(page.FindComponents<ChangesReport>(), Is.Not.Empty);
        }

        [Test]
        public async Task AnUnusedRulesReportNamesTheDevicesWithoutUsageData()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.UnusedRules);

            await GenerateReport(page);

            page.WaitForAssertion(() => Assert.That(DisplayedTexts(), Does.Contain("E4004gateway")));
        }

        [Test]
        public async Task AFailingGenerationIsReportedAndLeavesNoReportBehind()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await GenerateReport(page);
            displayedMessages.Clear();
            apiConnection.FailRoleSelection = true;

            await GenerateReport(page);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Title, Is.EqualTo(context.Services.GetRequiredService<UserConfig>().GetText("generate_report")));
            Assert.That(page.FindComponents<RulesReport>(), Is.Empty);
        }

        [Test]
        public async Task ARunningGenerationShowsProgressAndCanBeStopped()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            apiConnection.QueryGate = new TaskCompletionSource();

            await page.InvokeAsync(() => GenerateButton(page).Click());
            Assert.That(page.FindComponents<Loading>(), Is.Not.Empty);
            CancellationTokenSource tokenSource = GetTokenSource(page.Instance);
            await page.InvokeAsync(() => page.FindAll("button.btn-danger").First(button => button.QuerySelector("span")?.ClassName == Icons.Cancel).Click());

            Assert.That(tokenSource.IsCancellationRequested, Is.True);
            Assert.That(DisplayedTexts(), Does.Contain(SimulatedUserConfig.DummyTranslate["E1003"]));
            apiConnection.QueryGate.SetResult();
            page.WaitForAssertion(() => Assert.That(page.FindComponents<Loading>(), Is.Empty));
        }

        [TestCase(ReportType.Rules, typeof(RulesReport))]
        [TestCase(ReportType.NatRules, typeof(RulesReport))]
        [TestCase(ReportType.ComplianceReport, typeof(ComplianceReport))]
        [TestCase(ReportType.Changes, typeof(ChangesReport))]
        [TestCase(ReportType.Statistics, typeof(StatisticsReport))]
        [TestCase(ReportType.Connections, typeof(ConnectionsReport))]
        [TestCase(ReportType.VarianceAnalysis, typeof(VariancesReport))]
        [TestCase(ReportType.OwnerRecertification, typeof(OwnerRecertReport))]
        [TestCase(ReportType.Owners, typeof(OwnersReport))]
        [TestCase(ReportType.RecertEventReport, typeof(RecertEventReport))]
        [TestCase(ReportType.TicketReport, typeof(ReportedTickets))]
        public async Task EachReportTypeIsShownWithItsOwnView(ReportType reportType, Type expectedComponent)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, reportType);

            await ShowReport(page, ConstructReport(context, reportType));

            Assert.That(ShowsComponent(page, expectedComponent), Is.True);
        }

        [Test]
        public async Task ARecertificationEventReportIsNotShownOnTheGenerationPage()
        {
            // recertification events only exist in the archive, the page has no view for them
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            await ShowReport(page, ConstructReport(context, ReportType.RecertificationEvent));

            Assert.That(ReportViews.Where(view => ShowsComponent(page, view)), Is.Empty);
        }

        [Test]
        public async Task ATicketReportPassesItsTimeRangeToTheView()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.TicketChangeReport);
            ReportBase report = ConstructReport(context, ReportType.TicketChangeReport);
            report.Query.QueryVariables["ticket_time_start"] = "2026-01-01";
            report.Query.QueryVariables["ticket_time_end"] = "2026-02-01";

            await ShowReport(page, report);

            ReportedTickets reportedTickets = page.FindComponent<ReportedTickets>().Instance;
            Assert.That(reportedTickets.TimeRangeStart, Is.EqualTo("2026-01-01"));
            Assert.That(reportedTickets.TimeRangeEnd, Is.EqualTo("2026-02-01"));
        }

        [Test]
        public async Task AReportDurationBelowOneSecondIsNotShownInFractions()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await ShowReport(page, ConstructReport(context, ReportType.Rules));

            page.Instance.reportGenerationDuration = 0.5;
            page.Render();

            Assert.That(page.FindAll("small").Select(small => small.TextContent), Does.Contain("report_duration <1 seconds. "));
        }

        [TestCase(5.0, 5.0, "seconds")]
        [TestCase(900.0, 15.0, "minutes")]
        public async Task TheReportDurationIsShownInAFittingUnit(double seconds, double shownValue, string shownUnit)
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await ShowReport(page, ConstructReport(context, ReportType.Rules));

            page.Instance.reportGenerationDuration = seconds;
            page.Render();

            Assert.That(page.Markup, Does.Contain($"report_duration {shownValue:0.00} {shownUnit}"));
        }

        [Test]
        public async Task TheNumberOfReportedElementsIsShown()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            ReportBase report = ConstructReport(context, ReportType.Rules);
            report.ReportData.ElementsCount = 3;

            await ShowReport(page, report);

            Assert.That(page.Markup, Does.Contain("Elements: 3."));
        }

        [Test]
        public async Task SelectedUnusedRulesCanBeTurnedIntoADeleteTicket()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.UnusedRules);
            await ShowReport(page, ConstructReport(context, ReportType.UnusedRules));
            Assert.That(CreateTicketButtons(page), Is.Empty);

            RulesReport rulesReport = page.FindComponent<RulesReport>().Instance;
            await page.InvokeAsync(() => rulesReport.SelectedRulesChanged.InvokeAsync([new Rule { Id = 1 }]));
            CreateTicketButtons(page)[0].Click();

            Assert.That(page.FindComponent<ReportCreateTicket>().Instance.Display, Is.True);
            Assert.That(page.FindComponent<ReportCreateTicket>().Instance.SelectedRules.Select(rule => rule.Id), Is.EqualTo((List<long>)[1]));
        }

        [Test]
        public async Task TheRightSidebarShowsTheSelectedRulesAndCanClearThem()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await ShowReport(page, ConstructReport(context, ReportType.Rules));
            RulesReport rulesReport = page.FindComponent<RulesReport>().Instance;
            await page.InvokeAsync(() => rulesReport.SelectedRulesChanged.InvokeAsync([new Rule { Id = 1 }]));
            Assert.That(page.FindComponent<RightSidebar>().Instance.SelectedRules, Has.Count.EqualTo(1));

            RightSidebar rightSidebar = page.FindComponent<RightSidebar>().Instance;
            await page.InvokeAsync(() => rightSidebar.SelectedRulesChanged.InvokeAsync([]));

            Assert.That(page.FindComponent<RightSidebar>().Instance.SelectedRules, Is.Empty);
        }

        [Test]
        public async Task TheRightSidebarShowsBothRuleVersionsOfSelectedChangesAndCanClearThem()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.Changes);
            await ShowReport(page, ConstructReport(context, ReportType.Changes));
            RuleChange modification = new() { ChangeAction = 'C', OldRule = new Rule { Id = 1 }, NewRule = new Rule { Id = 2 } };

            ChangesReport changesReport = page.FindComponent<ChangesReport>().Instance;
            await page.InvokeAsync(() => changesReport.SelectedRuleChangesChanged.InvokeAsync([modification]));
            List<long> shownRuleIds = [.. page.FindComponent<RightSidebar>().Instance.SelectedRules.Select(rule => rule.Id)];
            RightSidebar rightSidebar = page.FindComponent<RightSidebar>().Instance;
            await page.InvokeAsync(() => rightSidebar.SelectedRulesChanged.InvokeAsync([]));

            Assert.That(shownRuleIds, Is.EqualTo((List<long>)[1, 2]));
            Assert.That(page.FindComponent<RightSidebar>().Instance.SelectedRules, Is.Empty);
        }

        [Test]
        public async Task ChoosingATenantRestrictsTheRightSidebarAndDropsTheReportData()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            ReportBase report = ConstructReport(context, ReportType.Rules);
            report.ReportData.ElementsCount = 3;
            await ShowReport(page, report);
            Assert.That(page.FindComponent<RightSidebar>().Instance.AllTabVisible, Is.True);

            ReportTenantSelection tenantSelection = page.FindComponent<ReportTenantSelection>().Instance;
            await page.InvokeAsync(() => tenantSelection.SelectedTenantChanged.InvokeAsync(new Tenant { Id = kTenantId, Name = "tenant" }));

            Assert.That(page.FindComponent<RightSidebar>().Instance.AllTabVisible, Is.False);
            Assert.That(report.ReportData.ElementsCount, Is.Zero);
        }

        [Test]
        public async Task LoadingATemplateTakesOverItsFilterReportTypeAndTenant()
        {
            ReportPageApiConnection apiConnection = BuildApiWithSelectedDevice();
            apiConnection.Tenants = [new Tenant { Id = kTenantId, Name = "tenant" }];
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            ReportTemplate template = new("gateway=gw1", new ReportParams((int)ReportType.Changes, new DeviceFilter()));
            template.ReportParams.TenantFilter = new TenantFilter(new Tenant { Id = kTenantId });

            ReportTemplateComponent templateComponent = page.FindComponent<ReportTemplateComponent>().Instance;
            await page.InvokeAsync(() => templateComponent.OnTemplateLoad!(template));

            page.WaitForAssertion(() => Assert.That(SelectedReportType(page), Is.EqualTo(ReportType.Changes)));
            Assert.That(page.Find("form input").GetAttribute("value"), Is.EqualTo("gateway=gw1"));
            Assert.That(page.FindComponent<ReportTenantSelection>().Instance.SelectedTenant?.Id, Is.EqualTo(kTenantId));
        }

        [Test]
        public async Task LoadingAnOwnerReportTemplateHidesTheRightSidebar()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            ReportTemplate template = new("", new ReportParams((int)ReportType.Owners, new DeviceFilter()));

            ReportTemplateComponent templateComponent = page.FindComponent<ReportTemplateComponent>().Instance;
            await page.InvokeAsync(() => templateComponent.OnTemplateLoad!(template));

            page.WaitForAssertion(() => Assert.That(SelectedReportType(page), Is.EqualTo(ReportType.Owners)));
            Assert.That(page.FindComponents<RightSidebar>(), Is.Empty);
        }

        [Test]
        public async Task LoadingAManagementRulebasesViewTemplateLoadsTheRulebases()
        {
            ReportPageApiConnection apiConnection = new() { Managements = BuildManagements(true), RulebaseManagements = BuildRulebaseManagements() };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            ReportTemplate template = new("", new ReportParams((int)ReportType.Rules, new DeviceFilter()) { ManagementRulebaseView = true });

            ReportTemplateComponent templateComponent = page.FindComponent<ReportTemplateComponent>().Instance;
            await page.InvokeAsync(() => templateComponent.OnTemplateLoad!(template));

            page.WaitForAssertion(() => Assert.That(page.FindComponents<RulebaseSelection>(), Is.Not.Empty));
            Assert.That(apiConnection.CountQuery(DeviceQueries.getManagementsForRulebaseReporting), Is.EqualTo(1));
        }

        [Test]
        public async Task LoadingAModellingTemplatePrefersTheOwnerOfTheInjectedApp()
        {
            FwoOwner appOwner = new() { Id = 2, Name = "current app owner" };
            ReportPageApiConnection apiConnection = new() { Owners = [appOwner] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context, kAppId.ToString());
            ReportTemplate template = new("", new ReportParams((int)ReportType.Connections, new DeviceFilter()));
            template.ReportParams.ModellingFilter.SelectedTemplateOwner = new FwoOwner { Id = 2, Name = "stale owner copy" };

            ReportTemplateComponent templateComponent = page.FindComponent<ReportTemplateComponent>().Instance;
            await page.InvokeAsync(() => templateComponent.OnTemplateLoad!(template));

            page.WaitForAssertion(() => Assert.That(template.ReportParams.ModellingFilter.SelectedOwner, Is.SameAs(appOwner)));
        }

        [Test]
        public async Task SavingAsTemplateHandsTheCurrentSettingsToTheTemplateEditor()
        {
            ReportPageApiConnection apiConnection = new() { Owners = [new FwoOwner { Id = 2, Name = "app owner" }] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context, kAppId.ToString());
            page.Find("form input").Change("owner=x");

            ButtonWithIcon(page, Icons.Template).Click();

            ReportTemplateComponent templateComponent = page.FindComponent<ReportTemplateComponent>().Instance;
            ReportTemplate templateInEdit = templateComponent.reportTemplateInEdit;
            Assert.That(templateInEdit.Filter, Is.EqualTo("owner=x"));
            Assert.That(templateInEdit.ReportParams.ReportType, Is.EqualTo((int)ReportType.Connections));
            Assert.That(templateInEdit.ReportParams.ModellingFilter.SelectedTemplateOwner.Name, Is.EqualTo("app owner"));
        }

        [Test]
        public async Task ChangingTheReportTimeOpensTheTimeSelection()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            string reportTimeHeader = context.Services.GetRequiredService<UserConfig>().GetText("report_time");
            page.FindAll("h5").First(header => header.TextContent == reportTimeHeader).ParentElement!
                .QuerySelector("button")!.Click();

            // the template editor has a time selection of its own, the page's one comes last
            Assert.That(page.FindComponents<ReportSelectTime>()[^1].Instance.Display, Is.True);
        }

        [Test]
        public async Task AnInvalidFilterIsUnderlined()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            page.Find("form input").Input("src=");

            Assert.That(page.Find("span.error-underline").TextContent, Is.Not.Empty);
        }

        [Test]
        public async Task AValidFilterIsNotUnderlined()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);

            page.Find("form input").Input("src=1.2.3.4");

            Assert.That(page.Find("span.error-underline").TextContent, Is.Empty);
        }

        [Test]
        public async Task TheComplianceCheckboxesFollowTheUsersChoice()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.ComplianceDiffReport);

            page.Find("#complianceShowAnyRule").Change(false);
            page.Find("#complianceFilterOutInitialViolations").Change(true);

            Assert.That(page.Find("#complianceShowAnyRule").HasAttribute("checked"), Is.False);
            Assert.That(page.Find("#complianceFilterOutInitialViolations").HasAttribute("checked"), Is.True);
        }

        [Test]
        public async Task AGeneratedOwnerRecertificationReportOffersTheAdditionalInfoKeysOfItsOwners()
        {
            ReportPageApiConnection apiConnection = new()
            {
                Owners = [new FwoOwner { Id = 2, Name = "owner", AdditionalInfo = new() { ["region"] = "north", [kOwnerAddInfoKey] = "4711", [" "] = "blank key" } }]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.OwnerRecertification);

            await GenerateReport(page);

            page.WaitForAssertion(() => Assert.That(page.FindComponents<OwnerRecertReport>(), Is.Not.Empty));
            // blank keys are dropped and the rest is sorted
            Assert.That(page.FindComponent<ReportModellingParamSelection>().Instance.AvailableAddInfoNames, Is.EqualTo((List<string>)[kOwnerAddInfoKey, "region"]));
        }

        [Test]
        public async Task ARegeneratedComplianceReportRefreshesTheShownViewAndItsAnyRuleSetting()
        {
            await using BunitContext context = CreateContext(BuildApiWithSelectedDevice(), BuildUserConfig(Roles.Admin));
            IRenderedComponent<ReportPage> page = RenderReportPage(context);
            await SelectReportType(page, ReportType.ComplianceReport);
            await ShowReport(page, ConstructReport(context, ReportType.ComplianceReport));
            page.Find("#complianceShowAnyRule").Change(false);

            await GenerateReport(page);

            page.WaitForAssertion(() => Assert.That(page.FindComponents<ComplianceReport>(), Is.Not.Empty));
            ReportBase currentReport = GetField<ReportBase>(page.Instance, "currentReport");
            Assert.That(currentReport, Is.InstanceOf<ReportCompliance>());
            Assert.That(page.Find("#complianceShowAnyRule").HasAttribute("checked"), Is.EqualTo(((ReportCompliance)currentReport).ShowNonImpactRules));
        }
    }
}
