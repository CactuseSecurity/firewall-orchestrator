using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsUsersTest
    {
        [Test]
        public async Task SettingsUsers_InitializesAndRendersAdminActions()
        {
            await using BunitContext context = CreateContext();
            RecordingUsersPageApiConnection apiConnection = new()
            {
                ConnectedLdaps =
                [
                    new UiLdapConnection
                    {
                        Id = 1,
                        Name = "internal",
                        TenantLevel = 0,
                        Type = (int)LdapType.OpenLdap,
                        UserSearchPath = "ou=users,dc=fworch,dc=internal",
                        WriteUser = "cn=writer,ou=system,dc=fworch,dc=internal",
                        WriteUserPwd = "secret"
                    }
                ],
                Tenants =
                [
                    new Tenant { Id = 1, Name = "Tenant 1" }
                ]
            };
            TestMiddlewareClient middlewareClient = new("http://localhost/");
            middlewareClient.UseHandler(new SettingsUsersMiddlewareHandler());
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsUsers>());

            wrapper.WaitForAssertion(() =>
            {
                SettingsUsers component = wrapper.FindComponent<SettingsUsers>().Instance;
                SettingsUsersHandler handler = GetMember<SettingsUsersHandler>(component, "Handler");
                Assert.Multiple(() =>
                {
                    Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getLdapConnections));
                    Assert.That(apiConnection.Queries, Does.Contain(AuthQueries.getTenants));
                    Assert.That(GetMember<List<UiUser>>(handler, "UiUsers"), Has.Count.EqualTo(1));
                    Assert.That(GetMember<bool>(handler, "ShowSampleRemoveButton"), Is.False);
                });
            });

            Assert.Multiple(() =>
            {
                Assert.That(wrapper.Markup, Does.Contain("Users"));
                Assert.That(wrapper.Markup, Does.Contain("add_new_user"));
                Assert.That(wrapper.Markup, Does.Contain("synchronize"));
            });
        }

        private static BunitContext CreateContext()
        {
            BunitContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<GlobalConfig>(new SimulatedGlobalConfig());
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(EmptyStringLocalizer<>));
            return context;
        }

        private static SimulatedUserConfig CreateUserConfig()
        {
            SimulatedUserConfig userConfig = new();
            userConfig.User.Roles = new List<string> { Roles.Admin };
            userConfig.AllowManualOwnerAdmin = true;
            userConfig.ModIconify = false;
            return userConfig;
        }

        private static T GetMember<T>(object instance, string memberName)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                return (T)property.GetValue(instance)!;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(instance)!;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }

        private sealed class RecordingUsersPageApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = new();
            public List<UiLdapConnection> ConnectedLdaps { get; set; } = new();
            public List<Tenant> Tenants { get; set; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == AuthQueries.getLdapConnections && typeof(QueryResponseType) == typeof(List<UiLdapConnection>))
                {
                    return Task.FromResult((QueryResponseType)(object)ConnectedLdaps);
                }

                if (query == AuthQueries.getTenants && typeof(QueryResponseType) == typeof(List<Tenant>))
                {
                    return Task.FromResult((QueryResponseType)(object)Tenants);
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }
        }

        private sealed class SettingsUsersMiddlewareHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? "";
                if (request.Method == HttpMethod.Get && path.EndsWith("/User", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""
                        [
                          {
                            "Name": "user-one",
                            "UserId": 1,
                            "UserDn": "uid=user-one,ou=people,dc=fworch,dc=internal",
                            "Email": "user-one@example.invalid",
                            "Firstname": "User",
                            "Lastname": "One",
                            "TenantId": 1,
                            "LdapId": 1,
                            "PwChangeRequired": false
                          }
                        ]
                        """, Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class EmptyStringLocalizer<T> : IStringLocalizer<T>
        {
            public LocalizedString this[string name] => new(name, name, resourceNotFound: true);

            public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: true);

            public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => new List<LocalizedString>();

            public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
        }
    }
}
