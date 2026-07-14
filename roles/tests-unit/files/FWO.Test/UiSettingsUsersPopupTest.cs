using Bunit;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.EventMediator;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FWO.Test
{
    [TestFixture]
    internal class UiSettingsUsersPopupTest
    {
        private static void SetMember(object instance, string memberName, object? value)
        {
            Type type = instance.GetType();
            PropertyInfo? property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null)
            {
                property.SetValue(instance, value);
                return;
            }

            FieldInfo? field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            throw new MissingFieldException(type.FullName, memberName);
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

            throw new MissingFieldException(type.FullName, memberName);
        }

        private static MethodInfo GetPrivateMethod(Type type, string methodName)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, methodName);
        }

        private static async Task InvokePrivateTask(object instance, string methodName, params object[] args)
        {
            Task task = (Task)GetPrivateMethod(instance.GetType(), methodName).Invoke(instance, args)!;
            await task;
        }

        [Test]
        public async Task SearchUser_RejectsSearchPatternBelowMinimumLength()
        {
            SearchUser component = new();
            List<(string title, string message)> messages = [];
            SetMember(component, "userConfig", new SettingsUsersPopupUserConfig());
            SetMember(component, "apiConnection", new ThrowingApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "DisplayMessageInUi", (Action<Exception?, string, string, bool>)((_, title, message, _) => messages.Add((title, message))));
            SetMember(component, "selectedLdap", new UiLdapConnection { Id = 5, PatternLength = 3 });
            SetMember(component, "searchPattern", "ab");

            await InvokePrivateTask(component, "SearchInLdap");

            Assert.Multiple(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(1));
                Assert.That(messages[0].title, Is.EqualTo("search_users"));
                Assert.That(messages[0].message, Is.EqualTo("E52523"));
                Assert.That(GetMember<bool>(component, "SearchInProgress"), Is.False);
                Assert.That(GetMember<bool>(component, "SearchInLdapMode"), Is.False);
            });
        }

        [Test]
        public async Task SearchUser_AddLdapUser_InvokesDelegateAndClosesPopup()
        {
            SearchUser component = new();
            UiUser selectedUser = new() { Dn = "uid=test,ou=people,dc=example,dc=com", Name = "Test User" };
            int addCalls = 0;
            SetMember(component, "userConfig", new SettingsUsersPopupUserConfig());
            SetMember(component, "apiConnection", new ThrowingApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "selectedLdap", new UiLdapConnection { Id = 5, TenantLevel = 0 });
            SetMember(component, nameof(SearchUser.AddUser), (Func<UiUser, Task>)(user =>
            {
                addCalls++;
                Assert.That(user.Dn, Is.EqualTo(selectedUser.Dn));
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(component, "AddLdapUser", selectedUser);

            Assert.Multiple(() =>
            {
                Assert.That(addCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, nameof(SearchUser.Display)), Is.False);
            });
        }

        [Test]
        public async Task SearchUser_AddLdapGroup_InvokesDelegateAndClosesPopup()
        {
            SearchUser component = new();
            int addCalls = 0;
            string selectedGroupDn = "cn=test-group,ou=groups,dc=example,dc=com";
            SetMember(component, "userConfig", new SettingsUsersPopupUserConfig());
            SetMember(component, "apiConnection", new ThrowingApiConnection());
            SetMember(component, "middlewareClient", new MiddlewareClient("http://localhost/"));
            SetMember(component, "selectedLdap", new UiLdapConnection { Id = 5, TenantLevel = 0 });
            SetMember(component, nameof(SearchUser.AddGroup), (Func<string, Task>)(groupDn =>
            {
                addCalls++;
                Assert.That(groupDn, Is.EqualTo(selectedGroupDn));
                return Task.CompletedTask;
            }));

            await InvokePrivateTask(component, "AddLdapGroup", selectedGroupDn);

            Assert.Multiple(() =>
            {
                Assert.That(addCalls, Is.EqualTo(1));
                Assert.That(GetMember<bool>(component, nameof(SearchUser.Display)), Is.False);
            });
        }

        [Test]
        public void RemoveUser_SelectsFirstUserWhenPopupOpens()
        {
            RemoveUser component = new();
            SetMember(component, nameof(RemoveUser.Users), new List<UiUser>
            {
                new() { Dn = "uid=first,dc=example,dc=com" },
                new() { Dn = "uid=second,dc=example,dc=com" }
            });
            SetMember(component, nameof(RemoveUser.Display), true);

            GetPrivateMethod(typeof(RemoveUser), "OnParametersSet").Invoke(component, []);

            Assert.That(GetMember<UiUser>(component, "selectedUser").Dn, Is.EqualTo("uid=first,dc=example,dc=com"));
        }

        [Test]
        public async Task RemoveUser_RemoveButton_InvokesDelegateAndClosesPopup()
        {
            await using BunitContext context = new();
            SettingsUsersPopupUserConfig userConfig = new();
            userConfig.User.Roles = [Roles.Admin];
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new SettingsUsersPopupAuthStateProvider(Roles.Admin));
            context.Services.AddSingleton<UserConfig>(userConfig);
            context.Services.AddSingleton<DomEventService>();

            int removeCalls = 0;
            bool? displayChangedValue = null;
            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<RemoveUser>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.DisplayChanged, EventCallback.Factory.Create<bool>(this, value => displayChangedValue = value))
                    .Add(p => p.Remove, (Func<UiUser, Task>)(user =>
                    {
                        removeCalls++;
                        Assert.That(user.Dn, Is.EqualTo("uid=first,dc=example,dc=com"));
                        return Task.CompletedTask;
                    }))
                    .Add(p => p.Users, new List<UiUser>
                    {
                        new() { Dn = "uid=first,dc=example,dc=com" },
                        new() { Dn = "uid=second,dc=example,dc=com" }
                    })
                    .Add(p => p.Title, "RoleX")
                    .Add(p => p.Label, "user")));

            IRenderedComponent<RemoveUser> component = wrapper.FindComponent<RemoveUser>();
            component.FindAll("button").First(button => button.TextContent.Contains("remove")).Click();

            Assert.Multiple(() =>
            {
                Assert.That(removeCalls, Is.EqualTo(1));
                Assert.That(component.Instance.Display, Is.False);
                Assert.That(displayChangedValue, Is.EqualTo(false));
            });
        }

        private sealed class SettingsUsersPopupUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return key;
            }
        }

        private sealed class SettingsUsersPopupAuthStateProvider : AuthenticationStateProvider
        {
            private readonly ClaimsPrincipal principal;

            public SettingsUsersPopupAuthStateProvider(params string[] roles)
            {
                List<Claim> claims = [];
                foreach (string role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
                principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            }

            public override Task<AuthenticationState> GetAuthenticationStateAsync()
            {
                return Task.FromResult(new AuthenticationState(principal));
            }
        }

        private sealed class ThrowingApiConnection : SimulatedApiConnection
        {
            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                throw new NotImplementedException($"Unexpected query: {query}");
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
            {
                return null!;
            }
        }
    }
}
