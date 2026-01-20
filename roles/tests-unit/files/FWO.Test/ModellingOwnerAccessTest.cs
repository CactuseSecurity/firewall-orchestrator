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
using FWO.Services;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class ModellingOwnerAccessTest
    {
        [Test]
        public async Task ReporterViewAllUsesAllOwnersQuery()
        {
            ApiConnection apiConnection = Substitute.For<ApiConnection>();
            apiConnection.SendQueryAsync<List<FwoOwner>>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<string?>())
                .Returns(new List<FwoOwner>());

            ClaimsPrincipal principal = BuildPrincipal(new List<string> { Roles.ReporterViewAll });
            Task<AuthenticationState> authStateTask = Task.FromResult(new AuthenticationState(principal));
            UserConfig userConfig = new SimulatedUserConfig();

            await ModellingHandlerBase.GetOwnApps(authStateTask, userConfig, apiConnection, NoopDisplay);

            await apiConnection.Received(1)
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
                return ids.Length == expected.Length && ids.SequenceEqual(expected);
            }

            return false;
        }

        private static void NoopDisplay(Exception? _, string __, string ___, bool ____)
        {
        }
    }
}
