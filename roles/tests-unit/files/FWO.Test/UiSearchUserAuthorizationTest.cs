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
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSearchUserAuthorizationTest
    {
        [Test]
        public async Task SearchUser_EnablesAssignForAdminRoleWhenExecutionModeIsUserRoles()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller]);

            IRenderedComponent<SearchUser> component = RenderSearchUser(context);

            component.WaitForAssertion(() =>
            {
                var assignButtons = FindAssignButtons(component);
                Assert.That(assignButtons, Has.Count.EqualTo(2));
                Assert.That(assignButtons.Any(button => button.HasAttribute("disabled")), Is.False);
            });
        }

        [Test]
        public async Task SearchUser_DisablesAssignWithoutAdminRole()
        {
            await using BunitContext context = CreateContext([Roles.Modeller]);

            IRenderedComponent<SearchUser> component = RenderSearchUser(context);

            component.WaitForAssertion(() =>
            {
                var assignButtons = FindAssignButtons(component);
                Assert.That(assignButtons, Has.Count.EqualTo(2));
                Assert.That(assignButtons.All(button => button.HasAttribute("disabled")), Is.True);
            });
        }

        [Test]
        public async Task SelectFromLdap_EnablesAssignForAdminRoleWhenExecutionModeIsUserRoles()
        {
            await using BunitContext context = CreateContext([Roles.Admin, Roles.Modeller]);

            IRenderedComponent<SelectFromLdap> component = RenderSelectFromLdap(context);

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
            context.Services.AddSingleton<IAuthorizationService, RoleAuthorizationService>();
            context.Services.AddScoped<DomEventService>();
            context.Services.AddSingleton<ApiConnection>(new SearchUserTestApiConnection());
            SimulatedUserConfig userConfig = new()
            {
                User = { Name = "testuser", Roles = roles }
            };
            userConfig.SetExecutionMode(GlobalConst.kUserRolesSelection);
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<MiddlewareClient>(new MiddlewareClient("http://localhost/"));
            context.Services.AddSingleton<AuthenticationStateProvider>(new SearchUserAuthStateProvider(roles));
            return context;
        }

        private static IRenderedComponent<SearchUser> RenderSearchUser(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SearchUser>(childParameters => childParameters
                    .Add(parameter => parameter.Display, true)
                    .Add(parameter => parameter.UserSearchMode, true)
                    .Add(parameter => parameter.GroupSearchMode, true)))
                .FindComponent<SearchUser>();
        }

        private static IRenderedComponent<SelectFromLdap> RenderSelectFromLdap(BunitContext context)
        {
            return context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SelectFromLdap>(childParameters => childParameters
                    .Add(parameter => parameter.Display, true)
                    .Add(parameter => parameter.UserSelect, true)
                    .Add(parameter => parameter.GroupSelect, true)
                    .Add(parameter => parameter.LdapUsers, new List<UiUser>
                    {
                        new()
                        {
                            DbId = 2,
                            Name = "ldapuser",
                            Dn = "uid=ldapuser,ou=people,dc=example,dc=invalid"
                        }
                    })
                    .Add(parameter => parameter.LdapGroups, ["cn=testgroup,ou=groups,dc=example,dc=invalid"])))
                .FindComponent<SelectFromLdap>();
        }

        private static List<AngleSharp.Dom.IElement> FindAssignButtons<TComponent>(IRenderedComponent<TComponent> component)
            where TComponent : Microsoft.AspNetCore.Components.IComponent
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

        private sealed class RoleAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            {
                foreach (RolesAuthorizationRequirement roleRequirement in requirements.OfType<RolesAuthorizationRequirement>())
                {
                    if (!roleRequirement.AllowedRoles.Any(user.IsInRole))
                    {
                        return Task.FromResult(AuthorizationResult.Failed());
                    }
                }

                return Task.FromResult(AuthorizationResult.Success());
            }

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            {
                return Task.FromResult(AuthorizationResult.Success());
            }
        }
    }
}
