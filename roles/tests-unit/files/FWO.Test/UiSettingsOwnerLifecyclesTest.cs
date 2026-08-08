using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test
{
    [TestFixture]
    [NonParallelizable]
    internal class UiSettingsOwnerLifecyclesTest
    {
        [Test]
        public async Task SettingsOwnerLifecycles_InitializesAndShowsAdminActions()
        {
            await using BunitContext context = CreateContext();
            RecordingOwnerLifecycleApiConnection apiConnection = new()
            {
                OwnerLifeCycleStates =
                [
                    new OwnerLifeCycleState { Id = 1, Name = "Active", ActiveState = true },
                    new OwnerLifeCycleState { Id = 2, Name = "Dormant", ActiveState = false }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsOwnerLifecycles>());

            wrapper.WaitForAssertion(() =>
            {
                SettingsOwnerLifecycles component = wrapper.FindComponent<SettingsOwnerLifecycles>().Instance;
                Assert.Multiple(() =>
                {
                    Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.getOwnerLifeCycleStates));
                    Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates"), Has.Count.EqualTo(2));
                });
            });

            Assert.Multiple(() =>
            {
                Assert.That(wrapper.Markup, Does.Contain("owner_lifecycle_states"));
                Assert.That(wrapper.Markup, Does.Contain("add_owner_lc_state"));
                Assert.That(wrapper.Markup, Does.Contain("Edit"));
                Assert.That(wrapper.Markup, Does.Contain("Delete"));
            });
        }

        [Test]
        public async Task SaveLifeCycle_AddModePersistsNewState()
        {
            await using BunitContext context = CreateContext();
            RecordingOwnerLifecycleApiConnection apiConnection = new()
            {
                NewId = 10,
                OwnerLifeCycleStates =
                [
                    new OwnerLifeCycleState { Id = 1, Name = "Active", ActiveState = true }
                ]
            };
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsOwnerLifecycles>());
            SettingsOwnerLifecycles component = wrapper.FindComponent<SettingsOwnerLifecycles>().Instance;

            wrapper.WaitForAssertion(() => Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates"), Has.Count.EqualTo(1)));
            SetMember(component, "AddLifeCycleMode", true);
            SetMember(component, "EditLifeCycleMode", true);
            SetMember(component, "actLifeCycleState", new OwnerLifeCycleState { Id = 0, Name = "Retired", ActiveState = false });

            await wrapper.InvokeAsync(async () => await InvokePrivateTask(component, "SaveLifeCycle"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.newOwnerLifeCycle));
                Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates"), Has.Count.EqualTo(2));
                Assert.That(GetMember<bool>(component, "AddLifeCycleMode"), Is.False);
                Assert.That(GetMember<bool>(component, "EditLifeCycleMode"), Is.False);
                Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates")[1].Id, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task SaveLifeCycle_UpdateModePersistsEditedState()
        {
            await using BunitContext context = CreateContext();
            RecordingOwnerLifecycleApiConnection apiConnection = new();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsOwnerLifecycles>());
            SettingsOwnerLifecycles component = wrapper.FindComponent<SettingsOwnerLifecycles>().Instance;

            OwnerLifeCycleState current = new() { Id = 5, Name = "Draft", ActiveState = true };
            SetMember(component, "OwnerLifeCycleStates", new List<OwnerLifeCycleState> { current });
            SetMember(component, "actLifeCycleState", new OwnerLifeCycleState(current) { Name = "Published" });
            SetMember(component, "AddLifeCycleMode", false);
            SetMember(component, "EditLifeCycleMode", true);

            await wrapper.InvokeAsync(async () => await InvokePrivateTask(component, "SaveLifeCycle"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.updateOwnerLifeCycle));
                Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates")[0].Name, Is.EqualTo("Published"));
                Assert.That(GetMember<bool>(component, "EditLifeCycleMode"), Is.False);
                Assert.That(GetMember<bool>(component, "AddLifeCycleMode"), Is.False);
            });
        }

        [Test]
        public async Task DeleteLifeCycle_RemovesSelectedState()
        {
            await using BunitContext context = CreateContext();
            RecordingOwnerLifecycleApiConnection apiConnection = new();
            context.Services.AddSingleton<ApiConnection>(apiConnection);
            context.Services.AddSingleton<UserConfig>(CreateUserConfig());

            IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<SettingsOwnerLifecycles>());
            SettingsOwnerLifecycles component = wrapper.FindComponent<SettingsOwnerLifecycles>().Instance;

            OwnerLifeCycleState deletedState = new() { Id = 7, Name = "Obsolete", ActiveState = false };
            SetMember(component, "OwnerLifeCycleStates", new List<OwnerLifeCycleState> { deletedState });
            SetMember(component, "actLifeCycleState", deletedState);
            SetMember(component, "DeleteLifeCycleMode", true);

            await wrapper.InvokeAsync(async () => await InvokePrivateTask(component, "DeleteLifeCycle"));

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(OwnerQueries.deleteOwnerLifeCycle));
                Assert.That(GetMember<List<OwnerLifeCycleState>>(component, "OwnerLifeCycleStates"), Is.Empty);
                Assert.That(GetMember<bool>(component, "DeleteLifeCycleMode"), Is.False);
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

        private static void SetMember<T>(object instance, string memberName, T value)
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

            throw new MissingMemberException(type.FullName, memberName);
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

        private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
            Task task = (Task)method.Invoke(instance, args)!;
            await task;
        }

        private sealed class RecordingOwnerLifecycleApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = new();
            public List<OwnerLifeCycleState> OwnerLifeCycleStates { get; set; } = new();
            public int NewId { get; set; } = 10;

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == OwnerQueries.getOwnerLifeCycleStates && typeof(QueryResponseType) == typeof(List<OwnerLifeCycleState>))
                {
                    List<OwnerLifeCycleState> result = OwnerLifeCycleStates.Select(state => new OwnerLifeCycleState(state)).ToList();
                    return Task.FromResult((QueryResponseType)(object)result);
                }

                if (query == OwnerQueries.newOwnerLifeCycle && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    ReturnId[] returnIds = new ReturnId[1];
                    returnIds[0] = new ReturnId { NewId = NewId };
                    return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = returnIds });
                }

                if (query == OwnerQueries.updateOwnerLifeCycle && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = GetIntValue(variables, "id") });
                }

                if (query == OwnerQueries.deleteOwnerLifeCycle && typeof(QueryResponseType) == typeof(ReturnId))
                {
                    return Task.FromResult((QueryResponseType)(object)new ReturnId { DeletedId = GetIntValue(variables, "id") });
                }

                throw new InvalidOperationException($"Unexpected query: {query}");
            }

            private static int GetIntValue(object? variables, string name)
            {
                PropertyInfo? property = variables?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return property == null ? 0 : Convert.ToInt32(property.GetValue(variables));
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
