using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Ui.Pages.Compliance;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class UiEditPolicyTest
    {
        private static MethodInfo GetPrivateMethod(string name)
        {
            return typeof(EditPolicy).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance) ??
                throw new MissingMethodException(typeof(EditPolicy).FullName, name);
        }

        private static void SetPrivateField<T>(EditPolicy component, string fieldName, T value)
        {
            FieldInfo? field = typeof(EditPolicy).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditPolicy).FullName, fieldName);
            }
            field.SetValue(component, value);
        }

        private static T GetPrivateField<T>(EditPolicy component, string fieldName)
        {
            FieldInfo? field = typeof(EditPolicy).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(typeof(EditPolicy).FullName, fieldName);
            }
            return (T)field.GetValue(component)!;
        }

        [Test]
        public async Task Save_AddModeWithEmptyName_DoesNotInsertPolicy()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
            EditPolicyTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            CompliancePolicy policy = new() { Name = "" };
            List<CompliancePolicy> policies = [];

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditPolicy>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.AddMode, true)
                    .Add(p => p.Policy, policy)
                    .Add(p => p.Policies, policies)));
            EditPolicy editPolicy = component.FindComponent<EditPolicy>().Instance;

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editPolicy, null)!;
            await saveTask;

            Assert.That(apiConn.AddPolicyCalls, Is.EqualTo(0));
            Assert.That(policies, Is.Empty);
        }

        [Test]
        public async Task Save_AddMode_InsertsPolicyAndCriteria()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
            EditPolicyTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            CompliancePolicy policy = new() { Name = "Policy" };
            List<CompliancePolicy> policies = [];
            ComplianceCriterion criterion = new() { Id = 5, Name = "Crit" };

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditPolicy>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.AddMode, true)
                    .Add(p => p.Policy, policy)
                    .Add(p => p.Policies, policies)));
            EditPolicy editPolicy = component.FindComponent<EditPolicy>().Instance;

            SetPrivateField(editPolicy, "CriteriaToAdd", new List<ComplianceCriterion> { criterion });

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editPolicy, null)!;
            await saveTask;

            Assert.That(apiConn.AddPolicyCalls, Is.EqualTo(1));
            Assert.That(apiConn.AddCritToPolicyCalls, Is.EqualTo(1));
            Assert.That(policies, Has.Count.EqualTo(1));
            Assert.That(policy.Id, Is.EqualTo(12));
            Assert.That(policy.Criteria, Has.Count.EqualTo(1));
            Assert.That(policy.Criteria[0].Content.Id, Is.EqualTo(criterion.Id));
        }

        [Test]
        public async Task Save_EditMode_RemovesCriteria()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
            EditPolicyTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            ComplianceCriterion criterion = new() { Id = 7, Name = "Crit" };
            CompliancePolicy policy = new()
            {
                Id = 42,
                Name = "Policy",
                Criteria = [ new ComplianceCriterionWrapper { Content = criterion } ]
            };

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditPolicy>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.AddMode, false)
                    .Add(p => p.Policy, policy)
                    .Add(p => p.Policies, new List<CompliancePolicy> { policy })));
            EditPolicy editPolicy = component.FindComponent<EditPolicy>().Instance;

            SetPrivateField(editPolicy, "CriteriaToDelete", new List<ComplianceCriterion> { criterion });

            Task saveTask = (Task)GetPrivateMethod("Save").Invoke(editPolicy, null)!;
            await saveTask;

            Assert.That(apiConn.RemoveCritFromPolicyCalls, Is.EqualTo(1));
            Assert.That(policy.Criteria, Is.Empty);
        }

        [Test]
        public async Task SelectableCriteria_ExcludesExistingAndPending()
        {
            await using Bunit.TestContext context = new();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddAuthorizationCore();
            context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
            context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
            EditPolicyTestApiConn apiConn = new();
            context.Services.AddSingleton<ApiConnection>(apiConn);
            context.Services.AddSingleton<DomEventService>();
            context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());

            ComplianceCriterion crit1 = new() { Id = 1, Name = "Crit1" };
            ComplianceCriterion crit2 = new() { Id = 2, Name = "Crit2" };
            CompliancePolicy policy = new()
            {
                Criteria = [ new ComplianceCriterionWrapper { Content = crit1 } ]
            };

            IRenderedComponent<CascadingAuthenticationState> component = context.Render<CascadingAuthenticationState>(parameters => parameters
                .AddChildContent<EditPolicy>(child => child
                    .Add(p => p.Display, true)
                    .Add(p => p.Policy, policy)));
            EditPolicy editPolicy = component.FindComponent<EditPolicy>().Instance;

            SetPrivateField(editPolicy, "AllCriteria", new List<ComplianceCriterion> { crit1, crit2 });
            SetPrivateField(editPolicy, "CriteriaToAdd", new List<ComplianceCriterion> { crit2 });

            List<ComplianceCriterion> selectable = (List<ComplianceCriterion>)GetPrivateMethod("SelectableCriteria").Invoke(editPolicy, null)!;

            Assert.That(selectable, Is.Empty);
        }
    }

    internal sealed class EditPolicyTestApiConn : SimulatedApiConnection
    {
        public int AddPolicyCalls { get; private set; }
        public int AddCritToPolicyCalls { get; private set; }
        public int RemoveCritFromPolicyCalls { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<ComplianceCriterion>) && query == ComplianceQueries.getCriteria)
            {
                return Task.FromResult((QueryResponseType)(object)new List<ComplianceCriterion>());
            }

            if (responseType == typeof(ReturnIdWrapper))
            {
                if (query == ComplianceQueries.addPolicy)
                {
                    AddPolicyCalls++;
                    ReturnIdWrapper wrapper = new() { ReturnIds = [ new ReturnId { InsertedId = 12 } ] };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper { ReturnIds = [ new ReturnId() ] });
            }

            if (responseType == typeof(ReturnId))
            {
                if (query == ComplianceQueries.addCritToPolicy)
                {
                    AddCritToPolicyCalls++;
                }
                else if (query == ComplianceQueries.removeCritFromPolicy)
                {
                    RemoveCritFromPolicyCalls++;
                }
                return Task.FromResult((QueryResponseType)(object)new ReturnId());
            }

            throw new NotImplementedException();
        }
    }

    internal sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            return Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal()));
        }
    }

}
