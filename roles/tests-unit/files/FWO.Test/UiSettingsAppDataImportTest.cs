using System.Reflection;
using System.Text.Json;
using Bunit;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Client;
using FWO.Services.EventMediator.Events;
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
internal sealed class UiSettingsAppDataImportTest
{
    [Test]
    public async Task AppDataImport_RendersInitializedSettingsAndRoles()
    {
        SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
        AppDataImportApiConnection apiConnection = new();
        MockMiddlewareClient middlewareClient = CreateMiddlewareClient();
        await using BunitContext context = CreateContext(globalConfig, apiConnection, middlewareClient);

        IRenderedComponent<CascadingAuthenticationState> page = context.Render<CascadingAuthenticationState>(parameters =>
            parameters.AddChildContent<SettingsAppDataImport>());

        page.WaitForAssertion(() =>
        {
            Assert.That(page.FindAll("input[type='checkbox']"), Is.Not.Empty);
            Assert.That(page.Markup, Does.Contain("Owner type"));
            Assert.That(page.Markup, Does.Contain("import_app_data"));
        });
    }

    [Test]
    public async Task AppDataImport_FileUploadErrorIsReported()
    {
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(CreateGlobalConfig(), new AppDataImportApiConnection(), CreateMiddlewareClient());
        IRenderedComponent<CascadingAuthenticationState> page = RenderComponent(context, messages);
        SettingsAppDataImport component = page.FindComponent<SettingsAppDataImport>().Instance;
        InvalidOperationException exception = new("invalid CSV");

        await page.InvokeAsync(() => Invoke(component, "OnFileUploadEvent", new FileUploadEventArgs(false, "apps.csv",
            new ErrorBaseModel { Message = "CSV rejected", InternalException = exception })));

        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Exception, Is.SameAs(exception));
            Assert.That(messages[0].IsError, Is.True);
        });
    }

    [Test]
    public async Task AppDataImport_CsvImportEventOpensResultPopup()
    {
        await using BunitContext context = CreateContext(CreateGlobalConfig(), new AppDataImportApiConnection(), CreateMiddlewareClient());
        IRenderedComponent<CascadingAuthenticationState> page = RenderComponent(context);
        SettingsAppDataImport component = page.FindComponent<SettingsAppDataImport>().Instance;
        AppServerImportEventArgs eventArgs = new(true)
        {
            Appserver = ["app-1"],
            Errors =
            [
                new CSVFileUploadErrorModel { EntryData = "bad", Message = "invalid" }
            ]
        };

        await page.InvokeAsync(() => Invoke(component, "OnCSVAppServerImportEvent", eventArgs));

        Assert.Multiple(() =>
        {
            Assert.That(GetField<bool>(component, "ShowCSVImportPopUp"), Is.True);
            Assert.That(GetField<List<string>>(component, "Appserver"), Has.Count.EqualTo(1));
            Assert.That(GetField<List<CSVFileUploadErrorModel>>(component, "CSVImportErrors"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AppDataImport_SaveFailureReportsError()
    {
        AppDataImportApiConnection apiConnection = new() { ThrowOnUpsert = true };
        List<(Exception? Exception, string Title, string Message, bool IsError)> messages = [];
        await using BunitContext context = CreateContext(CreateGlobalConfig(), apiConnection, CreateMiddlewareClient());
        IRenderedComponent<CascadingAuthenticationState> page = RenderComponent(context, messages);
        SettingsAppDataImport component = page.FindComponent<SettingsAppDataImport>().Instance;
        ConfigData configData = GetField<ConfigData>(component, "configData");
        configData.ImportAppDataScriptArgs = "--changed";

        await page.InvokeAsync(() => InvokeTask(component, "Save"));

        Assert.That(messages.Any(message => message.IsError), Is.True);
    }

    [Test]
    public void AppDataImport_AddSettingChangeOnlyAddsChangedValuesAndRedactsSecrets()
    {
        List<string> changes = [];
        InvokeStaticVoid("AddSettingChange", changes, "Path", "old", "new", false);
        InvokeStaticVoid("AddSettingChange", changes, "Same", "same", "same", false);
        InvokeStaticVoid("AddSettingChange", changes, "Script", "old-secret", "new-secret", true);

        Assert.That(changes, Is.EqualTo(new List<string>
        {
            "Path: 'old' -> 'new'",
            "Script: changed"
        }));
    }

    [Test]
    public void AppDataImport_AddPathQueuesTrimmedExtensionlessPath()
    {
        SettingsAppDataImport component = new();
        SetField(component, "PathsToAdd", new List<string>());
        SetField(component, "actPath", "/usr/local/fworch/scripts/customizing/apps.py");

        Invoke(component, "AddPath");

        Assert.Multiple(() =>
        {
            Assert.That(GetField<List<string>>(component, "PathsToAdd"), Is.EqualTo(
                new List<string> { "/usr/local/fworch/scripts/customizing/apps.py" }));
            Assert.That(GetField<string>(component, "actPath"), Is.Empty);
        });
    }

    [Test]
    public void AppDataImport_AddSubnetPathRejectsWhitespace()
    {
        SettingsAppDataImport component = new();
        SetField(component, "PathsToAddSubnet", new List<string>());
        SetField(component, "SubnetActPath", "   ");

        Invoke(component, "AddPathSubnet");

        Assert.That(GetField<List<string>>(component, "PathsToAddSubnet"), Is.Empty);
    }

    [Test]
    public void AppDataImport_ParseRolesSupportsLegacyAndDictionaryFormats()
    {
        Dictionary<int, List<string>> legacy = InvokeStatic<Dictionary<int, List<string>>>("ParseRolesWithImport", "[\"auditor\"]");
        Dictionary<int, List<string>> current = InvokeStatic<Dictionary<int, List<string>>>("ParseRolesWithImport", "{\"3\":[\"modeller\"],\"bad\":[\"ignored\"]}");
        Dictionary<int, List<string>> empty = InvokeStatic<Dictionary<int, List<string>>>("ParseRolesWithImport", " ");

        Assert.Multiple(() =>
        {
            Assert.That(legacy[GlobalConst.kOwnerResponsibleTypeSupporting], Is.EqualTo(new List<string> { "auditor" }));
            Assert.That(current[3], Is.EqualTo(new List<string> { "modeller" }));
            Assert.That(empty, Is.Empty);
        });
    }

    [Test]
    public void AppDataImport_BuildRolesBlocksDisallowedRoleCategories()
    {
        List<OwnerResponsibleType> types =
        [
            new OwnerResponsibleType { Id = 1, Name = "Owner type", AllowModelling = false, AllowRecertification = false }
        ];
        List<string> roles = [Roles.Modeller, Roles.Recertifier, Roles.Auditor];
        Dictionary<int, List<string>> selected = new()
        {
            [1] = [Roles.Modeller, Roles.Recertifier, Roles.Auditor]
        };

        Dictionary<int, Dictionary<string, bool>> result = InvokeStatic<Dictionary<int, Dictionary<string, bool>>>(
            "BuildRolesByTypeDict", types, roles, selected);

        Assert.Multiple(() =>
        {
            Assert.That(result[1][Roles.Modeller], Is.False);
            Assert.That(result[1][Roles.Recertifier], Is.False);
            Assert.That(result[1][Roles.Auditor], Is.True);
        });
    }

    [Test]
    public void AppDataImport_PrepareConfigDataAppliesPendingPathsAndRoles()
    {
        SettingsAppDataImport component = new();
        ConfigData configData = new()
        {
            ImportAppDataPath = "[]",
            ImportSubnetDataPath = "[]",
            RolesWithAppDataImport = "{}"
        };
        SetField(component, "configData", configData);
        SetField(component, "appDataPaths", new List<string>());
        SetField(component, "SubnetDataPaths", new List<string>());
        SetField(component, "PathsToAdd", new List<string> { "/usr/local/fworch/scripts/customizing/apps.py" });
        SetField(component, "PathsToAddSubnet", new List<string> { "/usr/local/fworch/etc/subnets.json" });
        SetField(component, "PathsToDelete", new List<string>());
        SetField(component, "PathsToDeleteSubnet", new List<string>());
        SetField(component, "ownerResponsibleTypes", new List<OwnerResponsibleType>
        {
            new OwnerResponsibleType { Id = 1, AllowModelling = true, AllowRecertification = true }
        });
        SetField(component, "rolesByTypeDict", new Dictionary<int, Dictionary<string, bool>>
        {
            [1] = new Dictionary<string, bool> { [Roles.Auditor] = true }
        });
        DateTime appStart = new(2026, 1, 2, 3, 4, 0, DateTimeKind.Utc);
        DateTime subnetStart = new(2026, 2, 3, 4, 5, 0, DateTimeKind.Utc);
        SetField(component, "appDataDate", appStart.Date);
        SetField(component, "appDataTime", appStart);
        SetField(component, "subnetDataDate", subnetStart.Date);
        SetField(component, "subnetDataTime", subnetStart);

        Invoke(component, "PrepareConfigData");

        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Deserialize<List<string>>(configData.ImportAppDataPath), Is.EqualTo(
                new List<string> { "/usr/local/fworch/scripts/customizing/apps" }));
            Assert.That(JsonSerializer.Deserialize<List<string>>(configData.ImportSubnetDataPath), Is.EqualTo(
                new List<string> { "/usr/local/fworch/etc/subnets" }));
            Assert.That(configData.RolesWithAppDataImport, Does.Contain(Roles.Auditor));
        });
    }

    private static SimulatedGlobalConfig CreateGlobalConfig()
    {
        return new SimulatedGlobalConfig
        {
            ImportAppDataPath = "[\"/usr/local/fworch/scripts/customizing/apps\"]",
            ImportSubnetDataPath = "[\"/usr/local/fworch/etc/subnets\"]",
            RolesWithAppDataImport = "{\"1\":[\"auditor\"]}",
            ImportAppDataStartAt = new DateTime(2026, 1, 2, 3, 4, 0),
            ImportSubnetDataStartAt = new DateTime(2026, 2, 3, 4, 5, 0)
        };
    }

    private static MockMiddlewareClient CreateMiddlewareClient()
    {
        return new MockMiddlewareClient
        {
            GetAllRolesResponse = new RestResponse<List<RoleGetReturnParameters>>(new RestRequest())
            {
                StatusCode = HttpStatusCode.OK,
                Data =
                [
                    new RoleGetReturnParameters { Role = "cn=admin,ou=roles,dc=fworch,dc=internal" },
                    new RoleGetReturnParameters { Role = "cn=anonymous,ou=roles,dc=fworch,dc=internal" },
                    new RoleGetReturnParameters { Role = "cn=auditor,ou=roles,dc=fworch,dc=internal" }
                ],
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            }
        };
    }

    private static BunitContext CreateContext(SimulatedGlobalConfig globalConfig,
        AppDataImportApiConnection apiConnection, MockMiddlewareClient middlewareClient)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, AllowAllAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new AllowAllAuthStateProvider(Roles.Admin));
        context.Services.AddSingleton<ApiConnection>(apiConnection);
        context.Services.AddSingleton<GlobalConfig>(globalConfig);
        context.Services.AddSingleton<UserConfig>(new SimulatedUserConfig());
        context.Services.AddSingleton<MiddlewareClient>(middlewareClient);
        context.Services.AddSingleton<FWO.Services.EventMediator.Interfaces.IEventMediator>(new RecordingEventMediator());
        context.Services.AddScoped<DomEventService>();
        context.Services.AddLocalization();
        return context;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderComponent(
        BunitContext context,
        List<(Exception? Exception, string Title, string Message, bool IsError)>? messages = null)
    {
        Action<Exception?, string, string, bool> callback = (exception, title, message, isError) =>
            messages?.Add((exception, title, message, isError));
        return context.Render<CascadingAuthenticationState>(parameters => parameters
            .AddChildContent<CascadingValue<Action<Exception?, string, string, bool>>>(child => child
                .Add(value => value.Value, callback)
                .AddChildContent<SettingsAppDataImport>()));
    }

    private static object? Invoke(object instance, string methodName, params object?[] arguments)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, arguments);
    }

    private static async Task InvokeTask(object instance, string methodName)
    {
        await (Task)(Invoke(instance, methodName) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }

    private static void InvokeStaticVoid(string methodName, params object?[] arguments)
    {
        MethodInfo method = typeof(SettingsAppDataImport).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(SettingsAppDataImport).FullName, methodName);
        method.Invoke(null, arguments);
    }

    private static T InvokeStatic<T>(string methodName, params object?[] arguments)
    {
        MethodInfo method = typeof(SettingsAppDataImport).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(SettingsAppDataImport).FullName, methodName);
        return (T)(method.Invoke(null, arguments) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }

    private static T GetField<T>(SettingsAppDataImport component, string fieldName)
    {
        FieldInfo? field = typeof(SettingsAppDataImport).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            return (T)(field.GetValue(component) ?? throw new InvalidOperationException($"{fieldName} is null."));
        }

        PropertyInfo property = typeof(SettingsAppDataImport).GetProperty(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMemberException(typeof(SettingsAppDataImport).FullName, fieldName);
        return (T)(property.GetValue(component) ?? throw new InvalidOperationException($"{fieldName} is null."));
    }

    private static void SetField<T>(SettingsAppDataImport component, string fieldName, T value)
    {
        FieldInfo field = typeof(SettingsAppDataImport).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(typeof(SettingsAppDataImport).FullName, fieldName);
        field.SetValue(component, value);
    }

    private sealed class AppDataImportApiConnection : SimulatedApiConnection
    {
        public bool ThrowOnUpsert { get; init; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (query == OwnerQueries.getOwnerResponsibleTypes && typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>))
            {
                List<OwnerResponsibleType> types =
                [
                    new OwnerResponsibleType { Id = 1, Name = "Owner type", Active = true, AllowModelling = true, AllowRecertification = true }
                ];
                return Task.FromResult((QueryResponseType)(object)types);
            }
            if (query == ConfigQueries.upsertConfigItems)
            {
                if (ThrowOnUpsert)
                {
                    throw new InvalidOperationException("configuration persistence failed");
                }
                return Task.FromResult(default(QueryResponseType)!);
            }
            throw new NotImplementedException($"Unhandled query {query} for {typeof(QueryResponseType).Name}");
        }
    }
}
