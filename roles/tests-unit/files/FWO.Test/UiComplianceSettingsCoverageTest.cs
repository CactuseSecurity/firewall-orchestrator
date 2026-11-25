using System.Reflection;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Test.Mocks;
using FWO.Ui.Pages.Settings;
using FWO.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RestSharp;
using System.Net;

namespace FWO.Test;

[TestFixture]
internal sealed class UiComplianceSettingsCoverageTest
{
    [Test]
    public async Task ComplianceFixCriteria_LoadsNonMatrixCriteriaAndRemovesSelectedCriterion()
    {
        ComplianceSettingsApiConnection apiConnection = new();
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderFixCriteria(context, messages);
        ComplianceFixCriteria component = wrapper.FindComponent<ComplianceFixCriteria>().Instance;

        List<ComplianceCriterion> criteria = GetField<List<ComplianceCriterion>>(component, "FixCriteria");
        Assert.That(criteria, Has.Count.EqualTo(1));
        SetField(component, "ActCrit", criteria[0]);

        await wrapper.InvokeAsync(() => InvokeTask(component, "RemoveCriterion"));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.RemoveCriterionCalls, Is.EqualTo(1));
            Assert.That(criteria, Is.Empty);
            Assert.That(GetField<bool>(component, "DeleteCritMode"), Is.False);
        });
    }

    [Test]
    public async Task ComplianceFixCriteria_AddEditDeleteAndCancelUpdateModes()
    {
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection());
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderFixCriteria(context);
        ComplianceFixCriteria component = wrapper.FindComponent<ComplianceFixCriteria>().Instance;
        ComplianceCriterion criterion = new() { Id = 7, Name = "Source", CriterionType = CriterionType.ForbidZonesAsSource.ToString() };

        Invoke(component, "AddCriterion");
        Assert.Multiple(() =>
        {
            Assert.That(GetField<bool>(component, "AddCritMode"), Is.True);
            Assert.That(GetField<bool>(component, "EditCritMode"), Is.True);
        });

        Invoke(component, "EditCriterion", criterion);
        Assert.That(GetField<ComplianceCriterion>(component, "ActCrit"), Is.SameAs(criterion));

        Invoke(component, "RequestDeleteCriterion", criterion);
        Assert.Multiple(() =>
        {
            Assert.That(GetField<bool>(component, "DeleteCritMode"), Is.True);
            Assert.That(GetField<string>(component, "DeleteMessage"), Does.Contain(criterion.Name));
        });

        Invoke(component, "Cancel");
        Assert.That(component.Display, Is.False);
    }

    [Test]
    public async Task ComplianceFixCriteria_RemoveCriterionFailureReportsError()
    {
        ComplianceSettingsApiConnection apiConnection = new() { ThrowOnRemoveCriterion = true };
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<CascadingAuthenticationState> wrapper = RenderFixCriteria(context, messages);
        ComplianceFixCriteria component = wrapper.FindComponent<ComplianceFixCriteria>().Instance;
        SetField(component, "ActCrit", new ComplianceCriterion { Id = 4, Name = "Failing" });

        await wrapper.InvokeAsync(() => InvokeTask(component, "RemoveCriterion"));

        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0].IsError, Is.True);
    }

    [Test]
    public async Task SettingsCompliance_RendersConfigurationAndSavesChanges()
    {
        ComplianceSettingsApiConnection apiConnection = new();
        SimulatedGlobalConfig globalConfig = new() { ComplianceCheckPolicyId = 5 };
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(apiConnection, globalConfig: globalConfig);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context, messages);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;

        page.WaitForAssertion(() => Assert.That(page.FindAll("input").Count, Is.GreaterThanOrEqualTo(7)));
        Invoke(component, "EditFixCrit");
        Assert.That(GetField<bool>(component, "EditFixCritMode"), Is.True);
        ConfigData editableConfig = GetField<ConfigData>(component, "configData");
        editableConfig.ComplianceCheckSleepTime++;

        await page.InvokeAsync(() => InvokeTask(component, "Save"));

        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.UpsertConfigCalls, Is.EqualTo(1));
            Assert.That(messages.Any(message => !message.IsError), Is.True);
        });
    }

    [Test]
    public async Task SettingsCompliance_DeleteAllViolationsShowsSuccessAndFailure()
    {
        ComplianceSettingsApiConnection apiConnection = new();
        await using BunitContext context = CreateContext(apiConnection);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;

        await page.InvokeAsync(() => InvokeTask(component, "DeleteAllViolations"));
        Assert.That(GetField<ComplianceCheckJobStatus>(component, "violationsDeleteStatus").Status,
            Is.EqualTo(ComplianceCheckExecutionStatus.Succeeded));

        apiConnection.ThrowOnDeleteAllViolations = true;
        await page.InvokeAsync(() => InvokeTask(component, "DeleteAllViolations"));
        Assert.That(GetField<ComplianceCheckJobStatus>(component, "violationsDeleteStatus").Status,
            Is.EqualTo(ComplianceCheckExecutionStatus.Failed));
    }

    [Test]
    public async Task SettingsCompliance_RunComplianceCheckReportsInvalidResponseAndException()
    {
        MockMiddlewareClient middlewareClient = new();
        middlewareClient.StartComplianceCheckResponse = new RestResponse<ComplianceCheckStartResult>(new RestRequest())
        {
            StatusCode = HttpStatusCode.BadRequest,
            ErrorMessage = "rejected",
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = false
        };
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection(), middlewareClient: middlewareClient);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context, messages);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;

        await page.InvokeAsync(() => InvokeTask(component, "RunComplianceCheck"));
        Assert.That(messages, Has.Count.EqualTo(1));

        middlewareClient.StartComplianceCheckException = new InvalidOperationException("unavailable");
        await page.InvokeAsync(() => InvokeTask(component, "RunComplianceCheck"));
        Assert.That(GetField<ComplianceCheckJobStatus>(component, "complianceCheckStatus").Status,
            Is.EqualTo(ComplianceCheckExecutionStatus.Failed));
    }

    [Test]
    public async Task SettingsCompliance_RefreshesRunningAndFinishedJobs()
    {
        MockMiddlewareClient middlewareClient = new();
        middlewareClient.ComplianceCheckStatusResponse = CreateStatusResponse(ComplianceCheckExecutionStatus.Running);
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection(), middlewareClient: middlewareClient);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;
        SetField(component, "complianceCheckJobId", "job-1");

        await page.InvokeAsync(() => InvokeTask(component, "RefreshComplianceStatus"));
        Assert.That(GetField<bool>(component, "complianceCheckRunning"), Is.True);

        middlewareClient.ComplianceCheckStatusResponse = CreateStatusResponse(ComplianceCheckExecutionStatus.Succeeded);
        await page.InvokeAsync(() => InvokeTask(component, "RefreshComplianceStatus"));

        Assert.That(GetField<bool>(component, "complianceCheckRunning"), Is.False);
    }

    [Test]
    public async Task SettingsCompliance_RefreshStatusFailureStopsPollingAndReportsError()
    {
        MockMiddlewareClient middlewareClient = new MockMiddlewareClient
        {
            ComplianceCheckStatusResponse = new RestResponse<ComplianceCheckJobStatus>(new RestRequest())
            {
                StatusCode = HttpStatusCode.BadRequest,
                ErrorMessage = "status unavailable",
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = false
            }
        };
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection(), middlewareClient: middlewareClient);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context, messages);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;
        SetField(component, "complianceCheckJobId", "job-2");

        await page.InvokeAsync(() => InvokeTask(component, "RefreshComplianceStatus"));

        Assert.Multiple(() =>
        {
            Assert.That(GetField<bool>(component, "complianceCheckRunning"), Is.False);
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].IsError, Is.True);
        });
    }

    [Test]
    public async Task SettingsCompliance_RefreshesFinishedJobSuccessfully()
    {
        MockMiddlewareClient middlewareClient = new MockMiddlewareClient
        {
            ComplianceCheckStatusResponse = CreateStatusResponse(ComplianceCheckExecutionStatus.Succeeded)
        };
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection(), middlewareClient: middlewareClient);
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;
        SetField(component, "complianceCheckJobId", "job-3");

        await page.InvokeAsync(() => InvokeTask(component, "RefreshComplianceStatus"));

        Assert.Multiple(() =>
        {
            Assert.That(middlewareClient.ComplianceCheckStatusCallCount, Is.EqualTo(1));
            Assert.That(GetField<ComplianceCheckJobStatus>(component, "complianceCheckStatus").Status,
                Is.EqualTo(ComplianceCheckExecutionStatus.Succeeded));
        });
    }

    [TestCase(ComplianceCheckExecutionStatus.Succeeded, "alert alert-success")]
    [TestCase(ComplianceCheckExecutionStatus.Failed, "alert alert-danger")]
    [TestCase(ComplianceCheckExecutionStatus.Running, "alert alert-info")]
    public async Task SettingsCompliance_StatusCssClassesAreMapped(ComplianceCheckExecutionStatus status, string expected)
    {
        await using BunitContext context = CreateContext(new ComplianceSettingsApiConnection());
        IRenderedComponent<CascadingAuthenticationState> page = RenderSettingsCompliance(context);
        SettingsCompliance component = page.FindComponent<SettingsCompliance>().Instance;

        string result = (string)(Invoke(component, "GetStatusCssClasses", status) ?? "");

        Assert.That(result, Is.EqualTo(expected));
    }

    private static BunitContext CreateContext(
        ComplianceSettingsApiConnection apiConnection,
        SimulatedGlobalConfig? globalConfig = null,
        MockMiddlewareClient? middlewareClient = null)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<GlobalConfig>(globalConfig ?? new SimulatedGlobalConfig());
        context.Services.AddSingleton<MiddlewareClient>(middlewareClient ?? new MockMiddlewareClient());
        context.Services.AddScoped<DomEventService>();
        context.Services.AddLocalization();
        return context;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderFixCriteria(
        BunitContext context,
        List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
    {
        Action<Exception?, string, string, bool> callback = (exception, title, message, isError) =>
            messages?.Add((exception, title, message, isError));
        return context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                .Add(value => value.Value, callback)
                .AddChildContent<ComplianceFixCriteria>(component => component
                    .Add(p => p.Display, true))));
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderSettingsCompliance(
        BunitContext context,
        List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
    {
        Action<Exception?, string, string, bool> callback = (exception, title, message, isError) =>
            messages?.Add((exception, title, message, isError));
        return context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                .Add(value => value.Value, callback)
                .AddChildContent<SettingsCompliance>()));
    }

    private static object? Invoke(object instance, string methodName, params object?[] parameters)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, parameters);
    }

    private static RestResponse<ComplianceCheckJobStatus> CreateStatusResponse(ComplianceCheckExecutionStatus status)
    {
        return new RestResponse<ComplianceCheckJobStatus>(new RestRequest())
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ComplianceCheckJobStatus { JobId = "job", Status = status },
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = true
        };
    }

    private static async Task InvokeTask(object instance, string methodName)
    {
        await (Task)(Invoke(instance, methodName) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }

    private static T GetField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)(field.GetValue(instance) ?? throw new InvalidOperationException($"{fieldName} is null."));
    }

    private static void SetField<T>(object instance, string fieldName, T value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    private sealed class ComplianceSettingsApiConnection : SimulatedApiConnection
    {
        private readonly List<ComplianceCriterion> criteria =
        [
            new ComplianceCriterion { Id = 1, Name = "Matrix", CriterionType = CriterionType.Matrix.ToString() },
            new ComplianceCriterion { Id = 2, Name = "Source", CriterionType = CriterionType.ForbidZonesAsSource.ToString() }
        ];

        public int RemoveCriterionCalls { get; private set; }
        public int UpsertConfigCalls { get; private set; }
        public bool ThrowOnRemoveCriterion { get; init; }
        public bool ThrowOnDeleteAllViolations { get; set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == ComplianceQueries.getCriteria && typeof(QueryResponseType) == typeof(List<ComplianceCriterion>))
            {
                return Task.FromResult((QueryResponseType)(object)criteria.ToList());
            }
            if (query == ComplianceQueries.removeCriterion)
            {
                if (ThrowOnRemoveCriterion)
                {
                    throw new InvalidOperationException("criterion removal failed");
                }
                RemoveCriterionCalls++;
                return Task.FromResult(default(QueryResponseType)!);
            }
            if (query == ComplianceQueries.getPolicies && typeof(QueryResponseType) == typeof(List<CompliancePolicy>))
            {
                return Task.FromResult((QueryResponseType)(object)new List<CompliancePolicy>
                {
                    new CompliancePolicy { Id = 5, Name = "Policy 5" }
                });
            }
            if (query == ConfigQueries.upsertConfigItems)
            {
                UpsertConfigCalls++;
                return Task.FromResult(default(QueryResponseType)!);
            }
            if (query == ComplianceQueries.deleteAllViolations)
            {
                if (ThrowOnDeleteAllViolations)
                {
                    throw new InvalidOperationException("violation deletion failed");
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }
    }
}
