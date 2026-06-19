using FWO.Api.Client;
using FWO.Basics;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class ApiConnectionRoleScopeExtensionsTest
    {
        [Test]
        public async Task RunWithNamedRoleScopeUsesExpectedRoles()
        {
            ClaimsPrincipal user = CreateUser(Roles.Admin, Roles.Auditor, Roles.Modeller, Roles.FwAdmin,
                Roles.Reporter, Roles.ReporterViewAll, Roles.Recertifier, Roles.Requester,
                Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer);

            await AssertRoleScope(user, (connection, principal) => connection.RunWithAdminOrAuditorRole(principal, CompletedAction),
                [Roles.Admin, Roles.Auditor]);
            await AssertRoleScope(user, (connection, principal) => connection.RunWithModellingRole(principal, CompletedAction),
                [Roles.Modeller, Roles.Admin, Roles.Auditor]);
            await AssertRoleScope(user, (connection, principal) => connection.RunWithMonitoringRole(principal, CompletedAction),
                [Roles.Admin, Roles.FwAdmin, Roles.Auditor]);
            await AssertRoleScope(user, (connection, principal) => connection.RunWithReportingRole(principal, CompletedAction),
                [Roles.ReporterViewAll, Roles.Reporter, Roles.Modeller, Roles.Recertifier, Roles.Admin, Roles.Auditor, Roles.FwAdmin]);
            await AssertRoleScope(user, (connection, principal) => connection.RunWithRecertificationRole(principal, CompletedAction),
                [Roles.Recertifier, Roles.Admin, Roles.Auditor]);
            await AssertRoleScope(user, (connection, principal) => connection.RunWithWorkflowRole(principal, CompletedAction),
                [Roles.Requester, Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer,
                    Roles.Admin, Roles.FwAdmin, Roles.Auditor]);
        }

        [Test]
        public async Task RunWithNamedRoleScopeReturnsResultAndSwitchesBack()
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser(Roles.Requester);

            string result = await connection.RunWithWorkflowRole(user, async () =>
            {
                Assert.That(connection.ActiveRole, Is.EqualTo(Roles.Requester));
                await Task.CompletedTask;
                return "done";
            });

            Assert.That(result, Is.EqualTo("done"));
            Assert.That(connection.ActiveRole, Is.Empty);
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        [Test]
        public void SetBestRoleForReportUsesExpectedRoles()
        {
            AssertReportRoles(ReportType.Owners, [Roles.Admin, Roles.FwAdmin, Roles.Auditor]);
            AssertReportRoles(ReportType.ComplianceReport, [Roles.Admin, Roles.FwAdmin, Roles.Auditor]);
            AssertReportRoles(ReportType.AppRules, [Roles.Admin, Roles.Modeller, Roles.Recertifier, Roles.Auditor]);
            AssertReportRoles(ReportType.TicketReport, [Roles.Admin, Roles.FwAdmin, Roles.Auditor, Roles.Requester,
                Roles.Approver, Roles.Planner, Roles.Implementer, Roles.Reviewer]);
            AssertReportRoles(ReportType.Rules, [Roles.Admin, Roles.FwAdmin, Roles.ReporterViewAll, Roles.Reporter,
                Roles.Recertifier, Roles.Auditor]);
            AssertReportRoles(ReportType.Undefined, [Roles.ReporterViewAll, Roles.Reporter, Roles.Modeller,
                Roles.Recertifier, Roles.Admin, Roles.Auditor, Roles.FwAdmin]);
        }

        private static async Task AssertRoleScope(ClaimsPrincipal user,
            Func<TrackingApiConnection, ClaimsPrincipal, Task> action, List<string> expectedRoles)
        {
            TrackingApiConnection connection = new();

            await action(connection, user);

            Assert.That(connection.LastTargetRoles, Is.EqualTo(expectedRoles));
            Assert.That(connection.SwitchBackCount, Is.EqualTo(1));
        }

        private static void AssertReportRoles(ReportType reportType, List<string> expectedRoles)
        {
            TrackingApiConnection connection = new();
            ClaimsPrincipal user = CreateUser(expectedRoles.ToArray());

            connection.SetBestRoleForReport(user, reportType);

            Assert.That(connection.LastTargetRoles, Is.EqualTo(expectedRoles));
        }

        private static Task CompletedAction()
        {
            return Task.CompletedTask;
        }

        private static ClaimsPrincipal CreateUser(params string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private sealed class TrackingApiConnection : ApiConnection
        {
            private readonly Stack<string> previousRoles = new();

            public string ActiveRole { get; private set; } = "";
            public int SwitchBackCount { get; private set; }
            public List<string> LastTargetRoles { get; private set; } = [];

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                LastTargetRoles = [.. targetRoleList];
                string selectedRole = targetRoleList.First(role => user.IsInRole(role));
                SetRole(selectedRole);
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(ActiveRole);
                ActiveRole = role;
            }

            public override void SwitchBack()
            {
                SwitchBackCount++;
                ActiveRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            public override void SetAuthHeader(string jwt)
            { }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
                string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new NotImplementedException();
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query,
                object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>()
            { }

            protected override void Dispose(bool disposing)
            { }
        }
    }
}
