using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data.Modelling;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the getAuditProofCriticalChanges endpoint: its route and authorization, the request contract
/// including defaults and aggregated validation, the response filtering, and the GraphQL query
/// behind it.
/// </summary>
[TestFixture]
internal class WorkflowChangeHistoryControllerTest
{
    private static readonly List<string> kControllerRoutes = new() { "api/workflow" };
    private static readonly DateTime kFirstChangeTime = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Utc);
    private static readonly DateTime kSecondChangeTime = new(2026, 9, 12, 9, 22, 0, DateTimeKind.Utc);

    [Test]
    public void GetAuditProofCriticalChangesUsesWorkflowRouteAndAuditRoles()
    {
        RouteAttribute[] controllerRoutes = typeof(WorkflowChangeHistoryController).GetCustomAttributes<RouteAttribute>().ToArray();
        MethodInfo endpoint = EndpointMethod();

        Assert.Multiple(() =>
        {
            Assert.That(controllerRoutes.Select(route => route.Template), Is.EquivalentTo(kControllerRoutes));
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("getAuditProofCriticalChanges"));
            Assert.That(endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
        });
    }

    [Test]
    public void QueryReadsOnlyAuditProofCriticalChangesOfTheTicket()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("query getAuditProofCriticalChangesForTicket"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("$ticketId: bigint!"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("ticket_id: { _eq: $ticketId }"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("audit_proof_critical: { _eq: true }"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket,
                Does.Contain("order_by: [{ change_time: desc }, { id: desc }]"));
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Not.Contain("change_source"));
        });
    }

    [Test]
    public void OptionsDefaultToAnEmptyObject()
    {
        GetAuditProofCriticalChangesRequest omitted = Deserialize("{\"ticketId\":1234}");
        GetAuditProofCriticalChangesRequest explicitNull = Deserialize("{\"ticketId\":1234,\"options\":null}");
        GetAuditProofCriticalChangesRequest empty = Deserialize("{\"ticketId\":1234,\"options\":{}}");

        Assert.Multiple(() =>
        {
            Assert.That(omitted.Options, Is.Not.Null);
            Assert.That(omitted.Options.Filter, Is.Null);
            Assert.That(explicitNull.Options, Is.Not.Null);
            Assert.That(explicitNull.Options.Filter, Is.Null);
            Assert.That(empty.Options.Filter, Is.Null);
        });
    }

    [Test]
    public void FilterKeysAreNullableAndBindByTheirDocumentedNames()
    {
        GetAuditProofCriticalChangesRequest request = Deserialize(
            "{\"ticketId\":1234,\"options\":{\"filter\":{\"changeTime\":\"2026-09-11T08:11:00Z\",\"changeUserName\":\"abc\",\"changeContent\":null}}}");

        Assert.Multiple(() =>
        {
            Assert.That(request.TicketId, Is.EqualTo(1234));
            Assert.That(request.Options.Filter!.ChangeTime, Is.EqualTo(kFirstChangeTime));
            Assert.That(request.Options.Filter.ChangeUserName, Is.EqualTo("abc"));
            Assert.That(request.Options.Filter.ChangeContent, Is.Null);
        });
    }

    [Test]
    public void ValidationAcceptsTheDocumentedRequest()
    {
        RequestValidationErrorResponse errors = GetAuditProofCriticalChangesRequestValidator.Validate(
            Deserialize("{\"ticketId\":1234,\"options\":{\"filter\":{\"changeUserName\":\"abc\"}}}"));

        Assert.That(errors.Errors, Is.Empty);
    }

    [Test]
    public void ValidationReportsEveryErrorOfOneRequestTogether()
    {
        RequestValidationErrorResponse errors = GetAuditProofCriticalChangesRequestValidator.Validate(
            Deserialize("{\"ticketId\":0,\"ticket_id\":1,\"options\":{\"limit\":5,\"filter\":{\"changer\":\"abc\"}}}"));

        List<string> paths = errors.Errors.Select(error => error.Path).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(paths, Is.EquivalentTo(new List<string> { "ticketId", "ticket_id", "options.limit", "options.filter.changer" }));
            Assert.That(errors.Errors.Single(error => error.Path == "ticketId").Message,
                Does.Contain("is required and must be greater than 0"));
            Assert.That(errors.Errors.Single(error => error.Path == "options.filter.changer").Message,
                Does.Contain("'changeUserName'"));
        });
    }

    [Test]
    public void AnOmittedTicketIdIsReportedByTheAggregatingValidator()
    {
        RequestValidationErrorResponse errors = GetAuditProofCriticalChangesRequestValidator.Validate(
            Deserialize("{\"options\":{\"filter\":{\"changer\":\"abc\"}}}"));

        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Select(error => error.Path),
                Is.EquivalentTo(new List<string> { "ticketId", "options.filter.changer" }));
            Assert.That(errors.Errors.Single(error => error.Path == "ticketId").Message,
                Does.Contain("is required"));
        });
    }

    [Test]
    public void ValidationRejectsAMissingRequestBody()
    {
        RequestValidationErrorResponse errors = GetAuditProofCriticalChangesRequestValidator.Validate(null);

        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors, Has.Count.EqualTo(1));
            Assert.That(errors.Errors[0].Path, Is.Empty);
            Assert.That(errors.Errors[0].Message, Does.Contain("requires a request body"));
        });
    }

    [Test]
    public async Task GetAuditProofCriticalChangesReturnsTheRecordedChangesNewestFirst()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 1234 });

        GetAuditProofCriticalChangesResponse response = OkResponse(result);
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string> { RequestQueries.getAuditProofCriticalChangesForTicket }));
            Assert.That(SerializeVariables(apiConnection.LastVariables), Does.Contain("\"ticketId\":1234"));
            Assert.That(response.Changes, Has.Count.EqualTo(2));
            Assert.That(response.Changes[0].ChangeTime, Is.EqualTo(kSecondChangeTime));
            Assert.That(response.Changes[0].ChangeUserName, Is.EqualTo("DEF"));
            Assert.That(response.Changes[0].ChangeContent, Is.EqualTo("Updated workflow request task"));
            Assert.That(response.Changes[1].ChangeUserName, Is.EqualTo("abc"));
            Assert.That(response.Changes[1].ChangeContent, Is.EqualTo("Updated workflow ticket"));
        });
    }

    [Test]
    public async Task GetAuditProofCriticalChangesReturnsAnEmptyListForATicketWithoutChanges()
    {
        AuditProofCriticalChangesApiConnection apiConnection = new();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 4321 });

        Assert.That(OkResponse(result).Changes, Is.Empty);
    }

    [Test]
    public async Task ChangeUserNameFilterMatchesCaseInsensitively()
    {
        GetAuditProofCriticalChangesResponse response = await FilteredChanges(new AuditProofCriticalChangeFilter { ChangeUserName = "def" });

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].ChangeUserName, Is.EqualTo("DEF"));
        });
    }

    [Test]
    public async Task ChangeContentFilterMatchesCaseInsensitively()
    {
        GetAuditProofCriticalChangesResponse response = await FilteredChanges(new AuditProofCriticalChangeFilter { ChangeContent = "updated workflow ticket" });

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].ChangeUserName, Is.EqualTo("abc"));
        });
    }

    [Test]
    public async Task ChangeTimeFilterSelectsTheChangeOfThatTimestamp()
    {
        GetAuditProofCriticalChangesResponse response = await FilteredChanges(new AuditProofCriticalChangeFilter { ChangeTime = kFirstChangeTime });

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.Changes[0].ChangeTime, Is.EqualTo(kFirstChangeTime));
        });
    }

    [Test]
    public async Task NullFilterKeysApplyNoRestriction()
    {
        GetAuditProofCriticalChangesResponse response = await FilteredChanges(new AuditProofCriticalChangeFilter());

        Assert.That(response.Changes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FilterKeysCombineAndCanExcludeEveryChange()
    {
        GetAuditProofCriticalChangesResponse response = await FilteredChanges(new AuditProofCriticalChangeFilter
        {
            ChangeUserName = "abc",
            ChangeContent = "Updated workflow request task"
        });

        Assert.That(response.Changes, Is.Empty);
    }

    [Test]
    public async Task InvalidRequestsAreRejectedBeforeTheApiIsQueried()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 0 });

        BadRequestObjectResult badRequest = (BadRequestObjectResult)result.Result!;
        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)badRequest.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors.Select(error => error.Path), Is.EquivalentTo(new List<string> { "ticketId" }));
            Assert.That(apiConnection.Queries, Is.Empty);
        });
    }

    [Test]
    public async Task ApiFailuresAreReportedAsAnInternalServerError()
    {
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(new FailingApiConnection()));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 1234 });

        ObjectResult error = (ObjectResult)result.Result!;
        Assert.That(error.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public void MiddlewareServerMaySelectEveryColumnTheQueryTouches()
    {
        FileInfo? metadataFile = LocateMetadata();
        if (metadataFile == null)
        {
            Assert.Ignore("The Hasura metadata is not reachable in this environment.");
            return;
        }

        List<string> columns = MiddlewareServerChangeHistoryColumns(metadataFile);

        // The query selects change_time, changer and change_text and filters and orders on
        // ticket_id, audit_proof_critical and id. Hasura resolves all of them through the select
        // permission of the role the middleware runs as.
        Assert.That(columns, Is.SupersetOf(new List<string>
        {
            "id", "changer", "change_text", "change_time", "ticket_id", "audit_proof_critical"
        }));
    }

    private static List<string> MiddlewareServerChangeHistoryColumns(FileInfo metadataFile)
    {
        using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
        foreach (JsonElement source in metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources").EnumerateArray())
        {
            foreach (JsonElement table in source.GetProperty("tables").EnumerateArray())
            {
                if (table.GetProperty("table").GetProperty("name").GetString() != "change_history"
                    || !table.TryGetProperty("select_permissions", out JsonElement selectPermissions))
                {
                    continue;
                }

                foreach (JsonElement permission in selectPermissions.EnumerateArray())
                {
                    if (permission.GetProperty("role").GetString() == Roles.MiddlewareServer)
                    {
                        return permission.GetProperty("permission").GetProperty("columns")
                            .EnumerateArray().Select(column => column.GetString() ?? "").ToList();
                    }
                }
            }
        }

        throw new AssertionException("No middleware-server select permission on change_history found.");
    }

    private static FileInfo? LocateMetadata()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            FileInfo candidate = new(Path.Combine(directory.FullName, "roles", "api", "files", "replace_metadata.json"));
            if (candidate.Exists)
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static async Task<GetAuditProofCriticalChangesResponse> FilteredChanges(AuditProofCriticalChangeFilter filter)
    {
        WorkflowChangeHistoryService service = new(CreateApiConnection());
        return await service.GetAuditProofCriticalChangesAsync(1234, filter);
    }

    private static GetAuditProofCriticalChangesResponse OkResponse(ActionResult<GetAuditProofCriticalChangesResponse> result)
    {
        return (GetAuditProofCriticalChangesResponse)((OkObjectResult)result.Result!).Value!;
    }

    private static MethodInfo EndpointMethod()
    {
        return typeof(WorkflowChangeHistoryController).GetMethod(nameof(WorkflowChangeHistoryController.GetAuditProofCriticalChanges))!;
    }

    private static GetAuditProofCriticalChangesRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GetAuditProofCriticalChangesRequest>(json)!;
    }

    private static string SerializeVariables(object? variables)
    {
        return JsonSerializer.Serialize(variables);
    }

    private static AuditProofCriticalChangesApiConnection CreateApiConnection()
    {
        return new AuditProofCriticalChangesApiConnection
        {
            Entries =
            [
                new ModellingHistoryEntry
                {
                    ChangeTime = kSecondChangeTime,
                    Changer = "DEF",
                    ChangeText = "Updated workflow request task"
                },
                new ModellingHistoryEntry
                {
                    ChangeTime = kFirstChangeTime,
                    Changer = "abc",
                    ChangeText = "Updated workflow ticket"
                }
            ]
        };
    }

    private sealed class AuditProofCriticalChangesApiConnection : SimulatedApiConnection
    {
        public List<ModellingHistoryEntry> Entries { get; set; } = [];

        public List<string> Queries { get; } = [];

        public object? LastVariables { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            Queries.Add(query);
            LastVariables = variables;
            if (query == RequestQueries.getAuditProofCriticalChangesForTicket && typeof(QueryResponseType) == typeof(List<ModellingHistoryEntry>))
            {
                return Task.FromResult((QueryResponseType)(object)Entries);
            }

            throw new AssertionException($"Unexpected query: {query}");
        }
    }

    private sealed class FailingApiConnection : SimulatedApiConnection
    {
        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            throw new InvalidOperationException("Api unavailable.");
        }
    }
}
