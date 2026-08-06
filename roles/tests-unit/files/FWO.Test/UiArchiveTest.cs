using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Ui.Pages.Reporting;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
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
        private const int kFewReports = 3;

        private sealed class ArchiveApiConnection : SimulatedApiConnection
        {
            public object? ReportQueryVariables { get; private set; }
            public object? SubscriptionVariables { get; private set; }
            public List<ReportFile> ArchivedReports { get; set; } = [];

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == ReportQueries.getGeneratedReports)
                {
                    ReportQueryVariables = variables;
                    return Task.FromResult((QueryResponseType)(object)ArchivedReports);
                }
                if (typeof(QueryResponseType) == typeof(List<FwoOwner>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoOwner>());
                }
                if (typeof(QueryResponseType) == typeof(List<ReportFile>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ReportFile>());
                }
                return Task.FromResult(default(QueryResponseType)!);
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                SubscriptionVariables = variables;
                return null!;
            }
        }

        [SetUp]
        public void SetUpTranslations()
        {
            SimulatedUserConfig.DummyTranslate.TryAdd("archive", "Archive");
            SimulatedUserConfig.DummyTranslate.TryAdd("actions", "Actions");
            SimulatedUserConfig.DummyTranslate.TryAdd("name", "Name");
            SimulatedUserConfig.DummyTranslate.TryAdd("report_type", "Report type");
            SimulatedUserConfig.DummyTranslate.TryAdd("template", "Template");
            SimulatedUserConfig.DummyTranslate.TryAdd("generation_date", "Generation date");
            SimulatedUserConfig.DummyTranslate.TryAdd("user", "User");
            SimulatedUserConfig.DummyTranslate.TryAdd("description", "Description");
            SimulatedUserConfig.DummyTranslate.TryAdd("all", "All");
            SimulatedUserConfig.DummyTranslate.TryAdd("archive_truncated", "Older reports exist but are not listed");
        }

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
            return context;
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

        [Test]
        public async Task TheInitialArchiveQueryIsBounded()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection);

            context.Render<Archive>();

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

            context.Render<Archive>();

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

            context.Render<Archive>();

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.EqualTo(kUserDbId));
            Assert.That(ReadOwnerRestriction(apiConnection.SubscriptionVariables), Is.EqualTo(kUserDbId));
        }

        [Test]
        public async Task TheArchiveQueryOfAUserWhoMaySeeEveryReportIsNotRestricted()
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Admin));

            context.Render<Archive>();

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.Null);
            Assert.That(ReadOwnerRestriction(apiConnection.SubscriptionVariables), Is.Null);
        }

        [TestCase(Roles.Auditor)]
        [TestCase(Roles.FwAdmin)]
        [TestCase(Roles.ReporterViewAll)]
        public async Task EveryRoleThatSeesAllReportsFetchesThemUnrestricted(string role)
        {
            ArchiveApiConnection apiConnection = new();
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, role));

            context.Render<Archive>();

            Assert.That(ReadOwnerRestriction(apiConnection.ReportQueryVariables), Is.Null);
        }

        [Test]
        public async Task TheArchiveSaysSoWhenOneReportMoreThanFitsExists()
        {
            // silently dropping them looks like the older reports had been deleted
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(shown + 1, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = context.Render<Archive>();

            Assert.That(archive.Find("div.alert").TextContent, Does.Contain("Older reports exist but are not listed"));
            Assert.That(archive.Find("div.alert").TextContent, Does.Contain(shown.ToString()));
        }

        [Test]
        public async Task TheArchiveDoesNotListTheSentinelRowItOnlyFetchedToDetectTruncation()
        {
            int shown = await GetArchiveDisplayLimit();
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(shown + 1, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = context.Render<Archive>();

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

            IRenderedComponent<Archive> archive = context.Render<Archive>();

            Assert.That(archive.Markup, Does.Not.Contain("Older reports exist but are not listed"));
            Assert.That(archive.Markup, Does.Contain($"report {shown - 1}"));
        }

        [Test]
        public async Task TheArchiveStaysQuietWhenEveryReportIsListed()
        {
            ArchiveApiConnection apiConnection = new() { ArchivedReports = BuildArchivedReports(kFewReports, kUserDbId) };
            await using BunitContext context = CreateContext(apiConnection, BuildUserConfig(kUserDbId, Roles.Reporter));

            IRenderedComponent<Archive> archive = context.Render<Archive>();

            Assert.That(archive.Markup, Does.Not.Contain("Older reports exist but are not listed"));
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