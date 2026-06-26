using System.Security.Claims;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSearchUserAuthorizationTest
    {
        [Test]
        public async Task SearchUser_DisablesAssignWhenExecutionModeDoesNotAllowAdmin()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller]);

            IRenderedComponent<SearchUser> component = RenderSearchUser(context, true);

            component.WaitForAssertion(() =>
            {
                var assignButtons = FindAssignButtons(component);
                Assert.That(assignButtons, Has.Count.EqualTo(2));
                Assert.That(assignButtons.All(button => button.HasAttribute("disabled")), Is.True);
            });
        }

        [Test]
        public async Task SearchUser_CanUsePlainAdminAuthorizationForRoleAssignment()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller]);

            IRenderedComponent<SearchUser> component = RenderSearchUser(context, false);

            component.WaitForAssertion(() =>
            {
                var assignButtons = FindAssignButtons(component);
                Assert.That(assignButtons, Has.Count.EqualTo(2));
                Assert.That(assignButtons.Any(button => button.HasAttribute("disabled")), Is.False);
            });
        }

        private static BunitContext CreateContext(List<string> roles)
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorization();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new SearchUserTestApiConnection());
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig
            {
                User = { Name = "testuser", Roles = roles }
            });
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<AuthenticationStateProvider>(new SearchUserAuthStateProvider(roles));
            return context;
        }

        private static IRenderedComponent<SearchUser> RenderSearchUser(BunitContext context, bool useExecutionModeAuthorization)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SearchUser>(childParameters => childParameters
                    .Add(parameter => parameter.Display, true)
                    .Add(parameter => parameter.UserSearchMode, true)
                    .Add(parameter => parameter.GroupSearchMode, true)
                    .Add(parameter => parameter.UseExecutionModeAuthorization, useExecutionModeAuthorization)))
                .FindComponent<SearchUser>();
        }

        private static List<AngleSharp.Dom.IElement> FindAssignButtons(IRenderedComponent<SearchUser> component)
        {
            return component.FindAll("button")
                .Where(button => button.TextContent.Trim() == "assign")
                .ToList();
        }

        private sealed class SearchUserAuthStateProvider(IEnumerable<string> roles) : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
                roles.Select(role => new Claim(ClaimTypes.Role, role)),
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role));

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class SearchUserTestApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (query == AuthQueries.getLdapConnections)
                {
                    object connections = new List<UiLdapConnection>();
                    return Task.FromResult((QueryResponseType)connections);
                }
                if (query == AuthQueries.getUsers)
                {
                    object users = new List<UiUser>
                    {
                        new()
                        {
                            DbId = 1,
                            Name = "testuser",
                            Dn = "uid=testuser,ou=people,dc=example,dc=invalid"
                        }
                    };
                    return Task.FromResult((QueryResponseType)users);
                }
                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }
    }
}
