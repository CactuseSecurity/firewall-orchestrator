using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Client;
using FWO.Services;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.NetworkModelling;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    internal class UiRequestRecertPopupTest
    {
        [Test]
        public void CheckImplementation_RunsVarianceQueriesWithAmbientRole()
        {
            RequestRecertPopupTestApiConn apiConn = new();
            SimulatedUserConfig userConfig = CreateUserConfig();
            FwoOwner selectedApp = new() { Id = 7, Name = "App" };
            ModellingAppHandler appHandler = new(apiConn, userConfig, selectedApp, DefaultInit.DoNothing, true)
            {
                Connections = [CreateConnection(41)]
            };
            apiConn.SetAmbientRole(CreatePrincipal(Roles.Auditor), [Roles.Modeller, Roles.Admin, Roles.Auditor]);

            using BunitContext context = new();
            IRenderedComponent<CascadingAuthenticationState> wrapper = RenderPopup(context, apiConn, userConfig, appHandler);

            wrapper.WaitForAssertion(() =>
            {
                Assert.That(apiConn.WasOnlySentWithRole(DeviceQueries.getManagementNames, Roles.Auditor), Is.True);
                Assert.That(apiConn.WasOnlySentWithRole(ModellingQueries.getNwGroupObjects, Roles.Auditor), Is.True);
                Assert.That(apiConn.WasOnlySentWithRole(ModellingQueries.getAppZonesByAppId, Roles.Auditor), Is.True);
            });
        }

        private static IRenderedComponent<CascadingAuthenticationState> RenderPopup(BunitContext context, RequestRecertPopupTestApiConn apiConn, SimulatedUserConfig userConfig, ModellingAppHandler appHandler)
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddLocalization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new RequestRecertPopupAuthStateProvider(Roles.Auditor));
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<IRuleTreeBuilder, RuleTreeBuilder>();

            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<RequestRecertPopup>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.AppHandler, appHandler)
                    .Add(p => p.CanRecertify, true)));
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            return new()
            {
                ModNamingConvention = "{}",
                ModIntegrationMode = ModIntegrationMode.FullyIntegrated,
                ModModelledMarker = "FWO:",
                User = { Ownerships = [7], Roles = [Roles.Auditor] }
            };
        }

        private static ModellingConnection CreateConnection(int id)
        {
            return new()
            {
                Id = id,
                Name = $"Conn{id}",
                SourceAppRoles =
                [
                    new() { Content = new() { Id = 101, IdString = "AR1", Name = "AR1" } }
                ],
                DestinationAppServers =
                [
                    new() { Content = new() { Id = 201, Name = "Server1", Ip = "10.0.1.1/32" } }
                ],
                Services =
                [
                    new() { Content = new() { Id = 301, Name = "HTTPS", ProtoId = 6, Port = 443 } }
                ]
            };
        }

        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                "Test",
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        private sealed class RequestRecertPopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public RequestRecertPopupAuthStateProvider(params string[] roles)
            {
                principal = CreatePrincipal(roles);
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class RequestRecertPopupTestApiConn : SimulatedApiConnection
        {
            private string activeRole = "";
            private readonly Stack<string> previousRoles = new();
            private readonly List<(string Query, string Role)> queries = [];

            public override void SetBestRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                SetRole(targetRoleList.First(role => user.IsInRole(role)));
            }

            public override void SetRole(string role)
            {
                previousRoles.Push(activeRole);
                activeRole = role;
            }

            public override string GetActRole()
            {
                return activeRole;
            }

            public override void SetAmbientRole(ClaimsPrincipal user, List<string> targetRoleList)
            {
                activeRole = targetRoleList.FirstOrDefault(role => user.IsInRole(role)) ?? "";
            }

            public override void SwitchBack()
            {
                activeRole = previousRoles.TryPop(out string? previousRole) ? previousRole : "";
            }

            public bool WasOnlySentWithRole(string query, string role)
            {
                List<(string Query, string Role)> matchingQueries = [.. queries.Where(q => q.Query == query)];
                return matchingQueries.Count > 0 && matchingQueries.All(q => q.Role == role);
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                queries.Add((query, activeRole));
                if (query == ExtRequestQueries.getLatestTicketId)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<TicketId>());
                }
                if (query == RequestQueries.getExtStates)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<WfExtState>
                    {
                        new() { Name = ExtStates.ExtReqDone.ToString(), StateId = 90 },
                        new() { Name = ExtStates.ExtReqRejected.ToString(), StateId = 91 }
                    });
                }
                if (query == DeviceQueries.getManagementNames)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Management>());
                }
                if (query == ModellingQueries.getNwGroupObjects)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingNetworkArea>());
                }
                if (query == ModellingQueries.getAppZonesByAppId)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<ModellingAppZone>());
                }
                throw new AssertionException($"Unexpected query: {query}");
            }
        }
    }
}
