using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
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
    internal class UiArchiveTest
    {
        private const int kUserDbId = 17;
        private const int kOtherUserDbId = 18;
        private const int kFewReports = 3;
        private const int kNavbarHeight = 50;
        private const string kOldReportsHint = "Older reports exist but are not listed";

        private sealed record DisplayedMessage(Exception? Exception, string Title, string Text, bool Error);

        private sealed class ArchiveApiConnection : SimulatedApiConnection
        {
            public object? ReportQueryVariables { get; private set; }
            public object? SubscriptionVariables { get; private set; }
            public object? DeleteVariables { get; private set; }
            public object? ReportContentVariables { get; private set; }
            public List<ReportFile> ArchivedReports { get; set; } = [];
            public List<FwoOwner> Owners { get; set; } = [];
            public ReportFile ReportContent { get; set; } = new();
            public bool FailArchiveQuery { get; set; }
            public bool FailReportContentQuery { get; set; }
            public bool FailDelete { get; set; }

            /// <summary>
            /// Holds back the answer to the report content query until the test releases it, so that the
            /// page can be inspected while the download is still being prepared.
            /// </summary>
            public TaskCompletionSource? ReportContentGate { get; set; }

            public Action<Exception>? SubscriptionErrorHandler { get; private set; }
            public GraphQlApiSubscription<List<ReportFile>>.SubscriptionUpdate? SubscriptionUpdateHandler { get; private set; }

            public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getGeneratedReports)
                {
                    ReportQueryVariables = variables;
                    ThrowIf(FailArchiveQuery);
                    return (QueryResponseType)(object)ArchivedReports;
                }
                if (query == ReportQueries.getGeneratedReport)
                {
                    ReportContentVariables = variables;
                    if (ReportContentGate != null)
                    {
                        await ReportContentGate.Task;
                    }
                    ThrowIf(FailReportContentQuery);
                    return (QueryResponseType)(object)new ReportFile[] { ReportContent };
                }
                if (query == ReportQueries.deleteGeneratedReport)
                {
                    DeleteVariables = variables;
                    ThrowIf(FailDelete);
                    return default!;
                }
                if (query == OwnerQueries.getEditableOwners)
                {
                    return (QueryResponseType)(object)Owners;
                }
                return default!;
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                SubscriptionVariables = variables;
                SubscriptionErrorHandler = exceptionHandler;
                SubscriptionUpdateHandler = subscriptionUpdateHandler as GraphQlApiSubscription<List<ReportFile>>.SubscriptionUpdate;
                return null!;
            }

            private static void ThrowIf(bool fail)
            {
                if (fail)
                {
                    throw new InvalidOperationException("simulated api failure");
                }
            }
        }

        private readonly List<DisplayedMessage> displayedMessages = [];

        private static BunitContext CreateContext(ArchiveApiConnection apiConnection, UserConfig? userConfig = null)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new MonitoringTestAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(userConfig ?? new SimulatedUserConfig());
            context.Services.AddScoped<DomEventService>();
            // the sidebar only shows its content once the navbar height is known
            context.Services.GetRequiredService<DomEventService>().InvokeNavbarHeightChanged(kNavbarHeight);
            return context;
        }

        private IRenderedComponent<Archive> RenderArchive(BunitContext context)
        {
            // the delete dialog checks the user's roles, so it needs the authentication state
            IRenderedComponent<CascadingAuthenticationState> page = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                    .Add(p => p.Value, (exception, title, text, error) =>
                        displayedMessages.Add(new DisplayedMessage(exception, title, text, error)))
                    .AddChildContent<Archive>()));
            return page.FindComponent<Archive>();
        }

        private static SimulatedUserConfig BuildUserConfig(int userDbId, params string[] roles)
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.DbId = userDbId;
            userConfig.User.Roles = [.. roles];
            return userConfig;
        }

        private static int? ReadLimit(object? variables)
        {
            return variables?.GetType().GetProperty("limit")?.GetValue(variables) as int?;
        }

        private static int? ReadReportId(object? variables)
        {
            return variables?.GetType().GetProperty("report_id")?.GetValue(variables) as int?;
        }

        /// <summary>
        /// Reads the owner the archive query is restricted to, or null if it fetches every user's reports.
        /// </summary>
        private static int? ReadOwnerRestriction(object? variables)
        {
            object? where = variables?.GetType().GetProperty("where")?.GetValue(variables);
            object? ownerCondition = where?.GetType().GetProperty("report_owner_id")?.GetValue(where);
            return ownerCondition?.GetType().GetProperty("_eq")?.GetValue(ownerCondition) as int?;
        }

        private static List<ReportFile> BuildArchivedReports(int count, int owningUserId)
        {
            List<ReportFile> reports = [];
            for (int report = 0; report < count; report++)
            {
                reports.Add(new ReportFile
                {
                    Id = report,
                    Name = $"report {report}",
                    OwningUserId = owningUserId
                });
            }
            return reports;
        }

        private static ReportFile BuildReport(int id, string name, ReportType reportType, int owningUserId, bool readOnly = false)
        {
            return new ReportFile
            {
                Id = id,
                Name = name,
                Type = (int)reportType,
                OwningUserId = owningUserId,
                ReadOnly = readOnly
            };
        }

        private static List<string> ListedReportNames(IRenderedComponent<Archive> archive)
        {
            return [.. archive.FindAll("tbody tr td:nth-child(2)").Select(cell => cell.TextContent.Trim())];
        }

        private static async Task SelectReportType(IRenderedComponent<Archive> archive, ReportType reportType)
        {
            Dropdown<ReportType> dropdown = archive.FindComponent<Dropdown<ReportType>>().Instance;
            await archive.InvokeAsync(() => dropdown.SelectedElementChanged.InvokeAsync(reportType));
        }

        [Test]
        public async Task TheInitialArchiveQueryIsBounded()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            RenderArchive(context);

            Assert.That(ReadLimit(apiConnection.ReportQueryVariables), Is.Not.Null);
            Assert.That(ReadLimit(apiConnection.ReportQueryVariables), Is.GreaterThan(0));
        }

        [Test]
        public async Task TheArchiveSubscriptionIsBoundedWithTheSameLimit()
        {
            // the subscription re-sends the whole list to every viewer on every new report, so an
            // unbounded one grows for as long as the installation keeps archiving reports
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            RenderArchive(context);

            Assert.That(ReadLimit(apiConnection.SubscriptionVariables), Is.Not.Null);
            Assert.That(ReadLimit(apiConnection.SubscriptionVariables),
                Is.EqualTo(ReadLimit(apiConnection.ReportQueryVariables)));
        }

        [Test]
        public async Task TheArchiveQueryOfAUserWhoMayOnlySeeOwnReportsIsRestrictedToThem()
        {
            // without the restriction the limit would be applied across every user's reports first, so
            // this user would lose all of theirs as soon as others had archived more recent ones
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            RenderArchive(context);

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.EqualTo(kUserDbId));
            Assert.That(ReadOwnerRestriction(apiConnection.SubscriptionVariables), Is.EqualTo(kUserDbId));
        }

        [Test]
        public async Task TheArchiveQueryOfAUserWhoMaySeeEveryReportIsNotRestricted()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            RenderArchive(context);

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.Null);
            Assert.That(ReadOwnerRestriction(apiConnection.SubscriptionVariables), Is.Null);
        }

        [TestCase(Roles.Auditor)]
        [TestCase(Roles.ReporterViewAll)]
        public async Task EveryRoleThatSeesAllReportsFetchesThemUnrestricted(string role)
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, role));

            RenderArchive(context);

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.Null);
        }

        [Test]
        public async Task TheArchiveSaysSoWhenOneReportMoreThanFitsExists()
        {
            // silently dropping them looks like the older reports had been deleted
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(shown + 1, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.Find("div.alert").TextContent, Does.Contain(kOldReportsHint));
            Assert.That(archive.Find("div.alert").TextContent, Does.Contain(shown.ToString()));
        }

        [Test]
        public async Task TheArchiveDoesNotListTheSentinelRowItOnlyFetchedToDetectTruncation()
        {
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(shown + 1, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.Markup, Does.Contain($"report {shown - 1}"));
            Assert.That(archive.Markup, Does.Not.Contain($"report {shown}"));
        }

        [Test]
        public async Task TheArchiveStaysQuietWhenExactlyAsManyReportsAsFitExist()
        {
            // the query asks for one row beyond the shown count, so a full page of shown rows is not
            // truncated - warning here would be a false alarm for an installation of exactly that size
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(shown, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.Markup, Does.Not.Contain(kOldReportsHint));
            Assert.That(archive.Markup, Does.Contain($"report {shown - 1}"));
        }

        [Test]
        public async Task TheArchiveStaysQuietWhenEveryReportIsListed()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(kFewReports, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.Markup, Does.Not.Contain(kOldReportsHint));
        }

        [Test]
        public async Task AUserWhoMayOnlySeeOwnReportsDoesNotSeeOtherUsersReports()
        {
            // the query is restricted already, but the page filters again in case a subscription update
            // or a differently configured permission hands it reports of other users
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "mine", ReportType.Rules, kUserDbId),
                    BuildReport(2, "foreign", ReportType.Rules, kOtherUserDbId)
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(ListedReportNames(archive), Is.EqualTo((List<string>)["mine"]));
        }

        [Test]
        public async Task TheReportTypeChooserOnlyOffersTypesOfListedReports()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "rules", ReportType.Rules, kUserDbId),
                    BuildReport(2, "changes", ReportType.Changes, kUserDbId)
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.FindComponent<Dropdown<ReportType>>().Instance.Elements,
                Is.EquivalentTo((List<ReportType>)[ReportType.Rules, ReportType.Changes]));
        }

        [Test]
        public async Task TheReportTypeChooserShowsTheLocalizedTypeNames()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(1, "rules", ReportType.Rules, kUserDbId)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.FindComponent<Dropdown<ReportType>>().Find("input").Focus();

            Assert.That(archive.FindAll("button.dropdown-item").Select(button => button.TextContent.Trim()),
                Does.Contain(SimulatedUserConfig.DummyTranslate[nameof(ReportType.Rules)]));
        }

        [Test]
        public async Task SelectingAReportTypeListsOnlyReportsOfThatType()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "rules", ReportType.Rules, kUserDbId),
                    BuildReport(2, "changes", ReportType.Changes, kUserDbId)
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            await SelectReportType(archive, ReportType.Changes);

            Assert.That(ListedReportNames(archive), Is.EqualTo((List<string>)["changes"]));
        }

        [Test]
        public async Task SelectingAllReportTypesListsEveryReportAgain()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "rules", ReportType.Rules, kUserDbId),
                    BuildReport(2, "changes", ReportType.Changes, kUserDbId)
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);
            await SelectReportType(archive, ReportType.Changes);

            await SelectReportType(archive, ReportType.Undefined);

            Assert.That(ListedReportNames(archive), Is.EquivalentTo((List<string>)["rules", "changes"]));
            Assert.That(archive.FindComponent<Dropdown<ReportType>>().Find("input").GetAttribute("value"),
                Is.EqualTo(context.Services.GetRequiredService<UserConfig>().GetText("all")));
        }

        [Test]
        public async Task RecertificationEventsOfferAnOwnerFilterNamingTheSuperOwnerAll()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "recert APP-1", ReportType.RecertificationEvent, kUserDbId),
                    BuildReport(2, "recert APP-2", ReportType.RecertificationEvent, kUserDbId)
                ],
                Owners =
                [
                    new FwoOwner { Id = 0, Name = "super owner" },
                    new FwoOwner { Id = 1, Name = "first", ExtAppId = "APP-1" },
                    new FwoOwner { Id = 2, Name = "unlisted", ExtAppId = "APP-9" },
                    new FwoOwner { Id = 3, Name = "without app id" }
                ]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            await SelectReportType(archive, ReportType.RecertificationEvent);

            Dropdown<FwoOwner> ownerDropdown = archive.FindComponent<Dropdown<FwoOwner>>().Instance;
            string allText = context.Services.GetRequiredService<UserConfig>().GetText("all");
            Assert.That(ownerDropdown.Elements.Select(owner => owner.Name), Is.EquivalentTo((List<string>)[allText, "first", "without app id"]));
            Assert.That(ownerDropdown.SelectedElement?.Name, Is.EqualTo(allText));
            Assert.That(ListedReportNames(archive), Is.EquivalentTo((List<string>)["recert APP-1", "recert APP-2"]));
        }

        [Test]
        public async Task SelectingAnOwnerListsOnlyTheRecertificationEventsOfItsApplication()
        {
            FwoOwner firstOwner = new() { Id = 1, Name = "first", ExtAppId = "APP-1" };
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports =
                [
                    BuildReport(1, "recert APP-1", ReportType.RecertificationEvent, kUserDbId),
                    BuildReport(2, "recert APP-2", ReportType.RecertificationEvent, kUserDbId)
                ],
                Owners = [new FwoOwner { Id = 0, Name = "super owner" }, firstOwner]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);
            await SelectReportType(archive, ReportType.RecertificationEvent);

            IRenderedComponent<Dropdown<FwoOwner>> ownerDropdown = archive.FindComponent<Dropdown<FwoOwner>>();
            ownerDropdown.Find("input").Focus();
            Assert.That(ownerDropdown.FindAll("button.dropdown-item").Select(button => button.TextContent.Trim()), Does.Contain("first"));
            await archive.InvokeAsync(() => ownerDropdown.Instance.SelectedElementChanged.InvokeAsync(firstOwner));

            Assert.That(ListedReportNames(archive), Is.EqualTo((List<string>)["recert APP-1"]));
        }

        [Test]
        public async Task TheOwnerFilterIsHiddenForOtherReportTypes()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(1, "rules", ReportType.Rules, kUserDbId)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.FindComponents<Dropdown<FwoOwner>>(), Is.Empty);
        }

        [Test]
        public async Task AFailingArchiveQueryIsReportedInTheUi()
        {
            ArchiveApiConnection apiConnection = new() { FailArchiveQuery = true };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            RenderArchive(context);

            Assert.That(displayedMessages, Has.Count.EqualTo(1));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Error, Is.True);
        }

        [Test]
        public async Task ASubscriptionUpdateReplacesTheListedReports()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(1, "old", ReportType.Rules, kUserDbId)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            await archive.InvokeAsync(() => apiConnection.SubscriptionUpdateHandler!(
                [BuildReport(1, "old", ReportType.Rules, kUserDbId), BuildReport(2, "new", ReportType.Changes, kUserDbId)]));

            archive.WaitForAssertion(() => Assert.That(ListedReportNames(archive), Is.EquivalentTo((List<string>)["old", "new"])));
            Assert.That(archive.FindComponent<Dropdown<ReportType>>().Instance.Elements,
                Is.EquivalentTo((List<ReportType>)[ReportType.Rules, ReportType.Changes]));
        }

        [Test]
        public async Task ASubscriptionUpdateBeyondTheLimitShowsTheTruncationHint()
        {
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            await archive.InvokeAsync(() => apiConnection.SubscriptionUpdateHandler!(BuildArchivedReports(shown + 1, kUserDbId)));

            archive.WaitForAssertion(() => Assert.That(archive.Find("div.alert").TextContent, Does.Contain(kOldReportsHint)));
        }

        [Test]
        public async Task ASubscriptionErrorIsReportedInTheUi()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);
            InvalidOperationException subscriptionError = new("subscription lost");

            await archive.InvokeAsync(() => apiConnection.SubscriptionErrorHandler!(subscriptionError));

            archive.WaitForAssertion(() => Assert.That(displayedMessages.Select(message => message.Exception), Does.Contain(subscriptionError)));
        }

        [Test]
        public async Task DownloadingAReportFetchesItsContentAndOpensTheDownloadDialog()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports = [BuildReport(5, "rules", ReportType.Rules, kUserDbId)],
                ReportContent = new ReportFile { Id = 5, Name = "rules with content", Html = "<p>content</p>" }
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.Find("tbody button.btn-primary").Click();

            archive.WaitForAssertion(() => Assert.That(archive.FindComponent<ReportDownloadPopUp>().Instance.Show, Is.True));
            Assert.That(ReadReportId(apiConnection.ReportContentVariables), Is.EqualTo(5));
            Assert.That(archive.FindComponent<ReportDownloadPopUp>().Instance.ReportFile?.Name, Is.EqualTo("rules with content"));
        }

        [Test]
        public async Task WhileTheReportContentIsFetchedTheActionsAreReplacedByAProgressIndicator()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports = [BuildReport(5, "rules", ReportType.Rules, kUserDbId)],
                ReportContentGate = new TaskCompletionSource()
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.Find("tbody button.btn-primary").Click();

            Assert.That(archive.FindComponents<Exporting>(), Is.Not.Empty);
            Assert.That(archive.FindAll("tbody button"), Is.Empty);
            apiConnection.ReportContentGate.SetResult();
            archive.WaitForAssertion(() => Assert.That(archive.FindComponents<Exporting>(), Is.Empty));
        }

        [Test]
        public async Task ClosingTheDownloadDialogHidesIt()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(5, "rules", ReportType.Rules, kUserDbId)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);
            archive.Find("tbody button.btn-primary").Click();
            archive.WaitForAssertion(() => Assert.That(archive.FindComponent<ReportDownloadPopUp>().Instance.Show, Is.True));

            await archive.InvokeAsync(() => archive.FindComponent<ReportDownloadPopUp>().Instance.OnClose());
            archive.Render();

            Assert.That(archive.FindComponent<ReportDownloadPopUp>().Instance.Show, Is.False);
        }

        [Test]
        public async Task AFailingReportContentFetchIsReportedInTheUi()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports = [BuildReport(5, "rules", ReportType.Rules, kUserDbId)],
                FailReportContentQuery = true
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.Find("tbody button.btn-primary").Click();

            archive.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Error, Is.False);
        }

        [Test]
        public async Task OwnReportsCanBeDeleted()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(5, "mine", ReportType.Rules, kUserDbId)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.FindAll("tbody button.btn-danger"), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ReadOnlyReportsCannotBeDeleted()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(5, "mine", ReportType.Rules, kUserDbId, readOnly: true)] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.That(archive.FindAll("tbody button.btn-danger"), Is.Empty);
        }

        [Test]
        public async Task OtherUsersReportsCanOnlyBeDeletedByAnAdmin()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [BuildReport(5, "foreign", ReportType.Rules, kOtherUserDbId)] };
            await using BunitContext adminContext = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            await using BunitContext auditorContext = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Auditor));

            IRenderedComponent<Archive> adminArchive = RenderArchive(adminContext);
            IRenderedComponent<Archive> auditorArchive = RenderArchive(auditorContext);

            Assert.That(adminArchive.FindAll("tbody button.btn-danger"), Has.Count.EqualTo(1));
            Assert.That(auditorArchive.FindAll("tbody button.btn-danger"), Is.Empty);
            Assert.That(ListedReportNames(auditorArchive), Is.EqualTo((List<string>)["foreign"]));
        }

        [Test]
        public async Task ConfirmingTheDeletionDeletesTheChosenReport()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports = [BuildReport(5, "first", ReportType.Rules, kUserDbId), BuildReport(6, "second", ReportType.Rules, kUserDbId)]
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.FindAll("tbody button.btn-danger")[1].Click();
            IRenderedComponent<ConfirmDelete> confirmDelete = archive.FindComponent<ConfirmDelete>();
            Assert.That(confirmDelete.Instance.Display, Is.True);
            Assert.That(confirmDelete.Instance.DeleteMessage, Does.Contain("\"second\""));
            confirmDelete.Find("button.btn-danger").Click();

            archive.WaitForAssertion(() => Assert.That(ReadReportId(apiConnection.DeleteVariables), Is.EqualTo(6)));
            Assert.That(archive.FindComponent<ConfirmDelete>().Instance.Display, Is.False);
            Assert.That(displayedMessages, Is.Empty);
        }

        [Test]
        public async Task AFailingDeletionIsReportedInTheUi()
        {
            ArchiveApiConnection apiConnection = new()
            {
                ArchivedReports = [BuildReport(5, "mine", ReportType.Rules, kUserDbId)],
                FailDelete = true
            };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            archive.Find("tbody button.btn-danger").Click();
            archive.FindComponent<ConfirmDelete>().Find("button.btn-danger").Click();

            archive.WaitForAssertion(() => Assert.That(displayedMessages, Has.Count.EqualTo(1)));
            Assert.That(displayedMessages[0].Exception, Is.InstanceOf<InvalidOperationException>());
            Assert.That(displayedMessages[0].Error, Is.False);
        }

        [Test]
        public async Task TheListShowsTypeGenerationDateAndDescriptionOfEachReport()
        {
            ReportFile report = BuildReport(5, "rules", ReportType.Rules, kUserDbId);
            report.GenerationDateStart = new DateTime(2026, 11, 27, 13, 14, 15, DateTimeKind.Utc);
            report.Description = "monthly";
            ArchiveApiConnection apiConnection = new() { ArchivedReports = [report] };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            IRenderedComponent<Archive> archive = RenderArchive(context);

            string row = archive.Find("tbody tr").TextContent;
            Assert.That(row, Does.Contain(SimulatedUserConfig.DummyTranslate[nameof(ReportType.Rules)]));
            Assert.That(row, Does.Contain("2026-11-27 13:14:15Z"));
            Assert.That(row, Does.Contain("monthly"));
        }

        [Test]
        public async Task DisposingTheArchiveWithoutSubscriptionDoesNotThrow()
        {
            ArchiveApiConnection apiConnection = new() { FailArchiveQuery = true };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));
            IRenderedComponent<Archive> archive = RenderArchive(context);

            Assert.DoesNotThrow(archive.Instance.Dispose);
        }

        /// <summary>
        /// Reads the row limit the page asks for, so that the truncation tests keep working if it changes.
        /// </summary>
        private static async Task<int> GetArchiveLimit()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            context.Render<Archive>();

            return ReadLimit(apiConnection.ReportQueryVariables)
                ?? throw new InvalidOperationException("the archive query is not bounded at all");
        }

        /// <summary>
        /// Number of reports the archive actually lists: the query fetches one sentinel row on top of it.
        /// </summary>
        private static async Task<int> GetArchiveDisplayLimit()
        {
            return await GetArchiveLimit() - 1;
        }
    }
}
