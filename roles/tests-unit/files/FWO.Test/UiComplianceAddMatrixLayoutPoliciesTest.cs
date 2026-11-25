using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using FWO.Ui.Pages.Compliance;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal sealed class UiComplianceAddMatrixLayoutPoliciesTest
{
    [Test]
    public async Task AddMatrix_WhenDisplayed_RendersInputAndCancelClosesDialog()
    {
        await using BunitContext context = CreateContext(Roles.Admin, new ComplianceUiApiConnection());

        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<AddMatrix>(child => child.Add(p => p.Display, true)));
        IRenderedComponent<AddMatrix> component = wrapper.FindComponent<AddMatrix>();

        Assert.That(component.FindAll("input").Any(input => input.GetAttribute("type") == "text"), Is.True);

        component.FindAll("button").Single(button => button.TextContent.Contains("cancel", StringComparison.OrdinalIgnoreCase)).Click();

        Assert.That(component.Instance.Display, Is.False);
    }

    [Test]
    public async Task AddMatrix_SaveWithEmptyName_DoesNotCreateCriterion()
    {
        ComplianceUiApiConnection apiConnection = new();
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection);
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<AddMatrix>(child => child.Add(p => p.Display, true)));
        AddMatrix component = wrapper.FindComponent<AddMatrix>().Instance;

        Task saveTask = InvokePrivateTask(component, "Save");
        await saveTask;

        Assert.That(apiConnection.AddCriterionCalls, Is.Zero);
        Assert.That(component.Display, Is.True);
    }

    [Test]
    public async Task AddMatrix_SaveWithName_CreatesCriterionAndClosesDialog()
    {
        ComplianceUiApiConnection apiConnection = new();
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection);
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<AddMatrix>(child => child.Add(p => p.Display, true)));
        AddMatrix component = wrapper.FindComponent<AddMatrix>().Instance;
        SetPrivateField(component, "MatrixName", "New matrix");

        await InvokePrivateTask(component, "Save");

        Assert.That(apiConnection.AddCriterionCalls, Is.EqualTo(1));
        Assert.That(component.Display, Is.False);
    }

    [Test]
    public async Task ComplianceLayout_ForAdmin_RendersPolicyAndMatrixLinks()
    {
        await using BunitContext context = CreateContext(Roles.Admin, new ComplianceUiApiConnection());
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<ComplianceLayout>());
        IRenderedComponent<ComplianceLayout> layout = wrapper.FindComponent<ComplianceLayout>();
        DomEventService eventService = context.Services.GetRequiredService<DomEventService>();

        await layout.InvokeAsync(() => eventService.InvokeNavbarHeightChanged(50));

        layout.WaitForAssertion(() =>
        {
            Assert.That(layout.Find("a[href='/compliance/policies']"), Is.Not.Null);
            Assert.That(layout.Find("a[href='/compliance/matrix']"), Is.Not.Null);
            Assert.That(layout.Find("a[href='/compliance/checks']"), Is.Not.Null);
        });
    }

    [Test]
    public async Task CompliancePolicies_LoadsPoliciesAndAddButtonOpensEditor()
    {
        ComplianceUiApiConnection apiConnection = new();
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection);

        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CompliancePolicies>());

        wrapper.WaitForAssertion(() => Assert.That(wrapper.Markup, Does.Contain("Existing policy")));
        CompliancePolicies page = wrapper.FindComponent<CompliancePolicies>().Instance;

        wrapper.FindAll("button").Single(button => button.TextContent.Contains("add", StringComparison.OrdinalIgnoreCase)).Click();

        Assert.That(GetPrivateField<bool>(page, "AddMode"), Is.True);
        Assert.That(GetPrivateField<bool>(page, "EditMode"), Is.True);
    }

    [Test]
    public async Task CompliancePolicies_DeletePolicy_DisablesAndRemovesSelectedPolicy()
    {
        ComplianceUiApiConnection apiConnection = new();
        await using BunitContext context = CreateContext(Roles.Admin, apiConnection);
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CompliancePolicies>());
        CompliancePolicies page = wrapper.FindComponent<CompliancePolicies>().Instance;
        List<CompliancePolicy> policies = GetPrivateField<List<CompliancePolicy>>(page, "policies");
        CompliancePolicy selectedPolicy = policies.Single();
        SetPrivateField(page, "SelectedPolicy", selectedPolicy);

        await InvokePrivateTask(page, "DeletePolicy");

        Assert.That(apiConnection.DisablePolicyCalls, Is.EqualTo(1));
        Assert.That(policies, Is.Empty);
        Assert.That(GetPrivateField<bool>(page, "DeleteMode"), Is.False);
    }

    [Test]
    public async Task CompliancePolicies_RequestDelete_SetsConfirmationState()
    {
        await using BunitContext context = CreateContext(Roles.Admin, new ComplianceUiApiConnection());
        IRenderedComponent<CascadingAuthenticationState> wrapper = context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CompliancePolicies>());
        CompliancePolicies page = wrapper.FindComponent<CompliancePolicies>().Instance;
        CompliancePolicy policy = GetPrivateField<List<CompliancePolicy>>(page, "policies").Single();

        InvokePrivate(page, "RequestDelete", policy);

        Assert.That(GetPrivateField<bool>(page, "DeleteMode"), Is.True);
        Assert.That(GetPrivateField<string>(page, "deleteMessage"), Does.Contain(policy.Name));
    }

    private static BunitContext CreateContext(string role, ComplianceUiApiConnection apiConnection)
    {
        SimulatedUserConfig userConfig = new();
        userConfig.User.Roles = [role];
        userConfig.SetExecutionMode(role);

        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(role));
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        context.Services.AddSingleton<UserConfig>(userConfig);
        context.Services.AddSingleton<DomEventService>();
        context.Services.AddLocalization();
        return context;
    }

    private static async Task InvokePrivateTask(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        await (Task)(method.Invoke(instance, null) ?? throw new InvalidOperationException($"Method {methodName} returned null."));
    }

    private static void InvokePrivate(object instance, string methodName, params object[] parameters)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        method.Invoke(instance, parameters);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"Field {fieldName} is null."));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }

        PropertyInfo property = instance.GetType().GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        property.SetValue(instance, value);
    }

    private sealed class ComplianceUiApiConnection : SimulatedApiConnection
    {
        public int AddCriterionCalls { get; private set; }
        public int DisablePolicyCalls { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == ComplianceQueries.getPolicies && typeof(QueryResponseType) == typeof(List<CompliancePolicy>))
            {
                List<CompliancePolicy> policies =
                [new CompliancePolicy { Id = 7, Name = "Existing policy" }];
                return Task.FromResult((QueryResponseType)(object)policies);
            }

            if (query == ComplianceQueries.disablePolicy && typeof(QueryResponseType) == typeof(ReturnId))
            {
                DisablePolicyCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId { UpdatedId = 7 });
            }

            if (query == ComplianceQueries.getCriteria && typeof(QueryResponseType) == typeof(List<ComplianceCriterion>))
            {
                return Task.FromResult((QueryResponseType)(object)new List<ComplianceCriterion>());
            }

            if (query == ComplianceQueries.addCriterion && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                AddCriterionCalls++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { InsertedId = 8 }]
                });
            }

            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }
    }
}
