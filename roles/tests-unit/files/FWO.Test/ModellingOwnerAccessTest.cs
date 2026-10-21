using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services.Modelling;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingOwnerAccessTest
    {
        private static readonly int[] kResponsibleOwnerIds = [3, 4];

        [Test]
        public async Task ReporterViewAllUsesAllOwnersQuery()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(new List<string> { Roles.ReporterViewAll });
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig { User = { Roles = [Roles.ReporterViewAll] } };

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners, Arg.Any<object?>(), Arg.Any<string?>());
        }

        [Test]
        public async Task ReporterViewAllUsesAllOwnersWithConnQueryWhenRequested()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(new List<string> { Roles.ReporterViewAll });
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig { User = { Roles = [Roles.ReporterViewAll] } };

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay, withConn: true);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn, Arg.Any<object?>(), Arg.Any<string?>());
        }

        [Test]
        public async Task AuditorWithWorkflowExecutionModeUsesEditableOwnersWithoutConnectionAggregate()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(
                new List<string> { Roles.Auditor, Roles.Approver },
                new Claim("x-hasura-editable-owners", "{3,4}"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig { User = { Roles = [Roles.Auditor, Roles.Approver] } };

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, new[] { 3, 4 })),
                    Arg.Any<string?>());
            await apiConnection.DidNotReceive()
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersWithConn, Arg.Any<object?>(), Arg.Any<string?>());
        }

        [Test]
        public async Task ReporterUsesEditableOwnersClaim()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(new List<string> { Roles.Reporter }, new Claim("x-hasura-editable-owners", "{1,2}"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig();

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, new[] { 1, 2 })),
                    Arg.Any<string?>());
        }

        [Test]
        public async Task ReporterUsesNamespacedEditableOwnersClaim()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(
                new List<string> { Roles.Reporter },
                new Claim("https://hasura.io/jwt/claims/x-hasura-editable-owners", "{1,2}"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig();

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, new[] { 1, 2 })),
                    Arg.Any<string?>());
        }

        [Test]
        public async Task ReporterUsesJsonEditableOwnersClaim()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(
                new List<string> { Roles.Reporter },
                new Claim("x-hasura-editable-owners", "[11,12]"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig();

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, new[] { 11, 12 })),
                    Arg.Any<string?>());
        }

        [Test]
        public async Task ReporterWithoutClaimUsesEmptyOwnerships()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(new List<string> { Roles.Reporter });
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig();

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, Array.Empty<int>())),
                    Arg.Any<string?>());
        }

        [TestCase(Roles.ReporterViewAll)]
        [TestCase(Roles.Admin)]
        public async Task ResponsibleAppsIgnoreReadAllRoles(string readAllRole)
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(
                new List<string> { readAllRole, Roles.Requester },
                new Claim("x-hasura-editable-owners", "{3,4}"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig { User = { Roles = [readAllRole, Roles.Requester] } };

            await ModellingHandlerBase.GetResponsibleApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getEditableOwners,
                    Arg.Is<object?>(vars => HasOwnerIds(vars, kResponsibleOwnerIds)),
                    Arg.Any<string?>());
            await apiConnection.DidNotReceive()
                .SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners, Arg.Any<object?>(), Arg.Any<string?>());
        }

        [Test]
        public async Task ResponsibleAppsReportFailureAndReturnEmptyList()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(Task.FromException<List<FwoOwner>>(new InvalidOperationException("api down")));

            ClaimsPrincipal principal = BuildPrincipal(
                new List<string> { Roles.Requester },
                new Claim("x-hasura-editable-owners", "{3,4}"));
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig { User = { Roles = [Roles.Requester] } };
            Exception? reportedException = null;

            List<FwoOwner> apps = await ModellingHandlerBase.GetResponsibleApps(authStateTask, userConfig, apiConnection,
                (exception, _, _, _) => reportedException = exception);

            Assert.Multiple(() =>
            {
                Assert.That(apps, Is.Empty);
                Assert.That(reportedException, Is.InstanceOf<InvalidOperationException>());
            });
        }

        private static ClaimsPrincipal BuildPrincipal(List<string> roles, Claim? extraClaim = null)
        {
            List<Claim> claims = roles.ConvertAll(role => new Claim(ClaimTypes.Role, role));
            if (extraClaim != null)
            {
                claims.Add(extraClaim);
            }
            ClaimsIdentity identity = new(claims, "test");
            return new ClaimsPrincipal(identity);
        }

        private static bool HasOwnerIds(object? vars, int[] expected)
        {
            if (vars == null)
            {
                return false;
            }

            var prop = vars.GetType().GetProperty("appIds");
            if (prop?.GetValue(vars) is int[] ids)
            {
                return ids.Length == expected.Length && ids.OrderBy(id => id).SequenceEqual(expected.OrderBy(id => id));
            }

            return false;
        }

        private static void NoopDisplay(Exception? _, string __, string ___, bool ____)
        {
        }
    }
}
