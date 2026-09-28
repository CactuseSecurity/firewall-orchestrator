using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using GraphQL;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
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
    // Unspecified on purpose: change_history.change_time is a timezone-naive column, so this is the
    // kind Newtonsoft produces for a stored row. Building fixtures as Utc hid the mismatch of F3.
    private static readonly DateTime kFirstChangeTime = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Unspecified);
    private static readonly DateTime kSecondChangeTime = new(2026, 9, 12, 9, 22, 0, DateTimeKind.Unspecified);

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
            // F2: without the module guard a row inserted under module = 'modelling' would be reported
            // here as a workflow change, and every workflow role may insert one.
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("module: { _eq: \"workflow\" }"));
            // F1: changer alone is caller-supplied free text, changer_id is the trustworthy identity.
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket, Does.Contain("changer_id"));
            // F7: change_time is nullable and Postgres sorts nulls first on a descending order.
            Assert.That(RequestQueries.getAuditProofCriticalChangesForTicket,
                Does.Contain("order_by: [{ change_time: desc_nulls_last }, { id: desc }]"));
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
            Assert.That(request.Options.Filter!.ChangeTime, Is.EqualTo(new DateTime(2026, 9, 11, 8, 11, 0, DateTimeKind.Utc)));
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
                Does.Contain("must be greater than 0"));
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
    public void AnOmittedTicketIdStaysNullInsteadOfSilentlyBecomingZero()
    {
        GetAuditProofCriticalChangesRequest omitted = Deserialize("{\"options\":{}}");
        GetAuditProofCriticalChangesRequest supplied = Deserialize("{\"ticketId\":0}");

        // Under-posting guard: a non-nullable long would make both of these 0 and hide the
        // difference between a key the caller never sent and a zero it sent on purpose.
        Assert.Multiple(() =>
        {
            Assert.That(omitted.TicketId, Is.Null);
            Assert.That(supplied.TicketId, Is.EqualTo(0));
            Assert.That(GetAuditProofCriticalChangesRequestValidator.Validate(omitted).Errors
                .Single(error => error.Path == "ticketId").Message, Does.Contain("is required"));
            Assert.That(GetAuditProofCriticalChangesRequestValidator.Validate(supplied).Errors
                .Single(error => error.Path == "ticketId").Message, Does.Contain("must be greater than 0"));
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
            Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
            {
                RequestQueries.getAuditProofCriticalChangesForTicket,
                RequestQueries.getWorkflowTaskHistoryForTicket,
                RequestQueries.getTicketById
            }));
            Assert.That(SerializeVariables(apiConnection.LastVariables), Does.Contain("\"id\":1234"));
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

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Changes, Is.Empty);
            // An existing ticket without such changes stays a 200 with an empty list: only an id that
            // names no ticket at all is an error.
            Assert.That(apiConnection.Queries, Does.Contain(RequestQueries.getTicketIdIfExists));
        });
    }

    [Test]
    public async Task NonEmptyAuditTrailIncludesRequestTaskDiffAndManualImplementationChanges()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory =
        [
            new ModellingHistoryEntry
            {
                Id = 1,
                ChangeType = (int)ModellingTypes.ChangeType.Insert,
                ObjectType = (int)ChangeHistoryObjectType.RequestTask,
                ObjectId = 10,
                NewData = StoredRequestTaskSnapshot(new WfReqTask { Id = 10, Title = "Original", Tracking = 1 })
            },
            new ModellingHistoryEntry
            {
                Id = 2,
                ChangeType = (int)ModellingTypes.ChangeType.Update,
                ObjectType = (int)ChangeHistoryObjectType.RequestTask,
                ObjectId = 10,
                NewData = StoredRequestTaskSnapshot(new WfReqTask { Id = 10, Title = "Planned", Tracking = 1 })
            },
            new ModellingHistoryEntry
            {
                Id = 3,
                ChangeType = (int)ModellingTypes.ChangeType.Update,
                ObjectType = (int)ChangeHistoryObjectType.ImplementationTask,
                ObjectId = 20,
                ChangeTime = kSecondChangeTime,
                Changer = "planner",
                ChangerId = 8,
                AuditProofCritical = true,
                OldData = new Dictionary<string, object> { ["deviceId"] = 1 },
                NewData = new Dictionary<string, object> { ["deviceId"] = 2 }
            }
        ];
        apiConnection.CurrentTasks = [new WfReqTask { Id = 10, Title = "Planned", Tracking = 1 }];

        GetAuditProofCriticalChangesResponse response = await AllChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.TaskDiff, Is.Not.Null);
            Assert.That(response.TaskDiff!.RequestTaskDiffs, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].RequestTaskId, Is.EqualTo(10));
            // Both sides carry the stored key names, so a consumer can compare them field by field.
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Original.GetProperty("title").GetString(), Is.EqualTo("Original"));
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Current!.Value.GetProperty("title").GetString(), Is.EqualTo("Planned"));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges[0].ImplementationTaskId, Is.EqualTo(20));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges[0].Current!.Value.GetProperty("deviceId").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task AnUnchangedRequestTaskProducesNoDiff()
    {
        WfReqTask task = new() { Id = 10, Title = "Unchanged", Tracking = 1, Start = kFirstChangeTime };
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory = [RequestTaskInsert(10, StoredRequestTaskSnapshot(task))];
        apiConnection.CurrentTasks = [task];

        GetAuditProofCriticalChangesResponse response = await AllChanges(apiConnection);

        // The stored snapshot went through the GraphQL client serializer (camelCase keys, ISO dates),
        // the current state is projected in the service: both have to meet in the same shape.
        Assert.That(response.TaskDiff!.RequestTaskDiffs, Is.Empty);
    }

    [Test]
    public async Task ADeletedRequestTaskIsReportedWithoutACurrentState()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory = [RequestTaskInsert(10, StoredRequestTaskSnapshot(new WfReqTask { Id = 10, Title = "Removed" }))];

        GetAuditProofCriticalChangesResponse response = await AllChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.TaskDiff!.RequestTaskDiffs, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Original.GetProperty("title").GetString(), Is.EqualTo("Removed"));
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Current, Is.Null);
        });
    }

    [Test]
    public async Task WorkflowWrittenFieldsAndOwnerOrderDoNotCountAsARequestChange()
    {
        WfReqTask requested = new() { Id = 10, Title = "Same", Owners = [Owner(1), Owner(2)] };
        WfReqTask current = new()
        {
            Id = 10,
            Title = "Same",
            Start = kFirstChangeTime,
            Stop = kSecondChangeTime,
            AdditionalInfo = "{\"ExtTicketId\":\"4711\"}",
            Owners = [Owner(2), Owner(1)]
        };
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory = [RequestTaskInsert(10, StoredRequestTaskSnapshot(requested))];
        apiConnection.CurrentTasks = [current];

        GetAuditProofCriticalChangesResponse response = await AllChanges(apiConnection);

        // State transitions set start and stop, workflow actions write additional info, and owners
        // come back in load order: none of that is a change to what was requested.
        Assert.That(response.TaskDiff!.RequestTaskDiffs, Is.Empty);
    }

    [Test]
    public async Task AContentChangeNextToWorkflowWrittenFieldsIsStillReported()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory = [RequestTaskInsert(10, StoredRequestTaskSnapshot(new WfReqTask { Id = 10, Reason = "Requested" }))];
        apiConnection.CurrentTasks = [new WfReqTask { Id = 10, Reason = "Rewritten", Start = kFirstChangeTime }];

        GetAuditProofCriticalChangesResponse response = await AllChanges(apiConnection);

        Assert.Multiple(() =>
        {
            Assert.That(response.TaskDiff!.RequestTaskDiffs, Has.Count.EqualTo(1));
            // The comparison ignores start, the returned snapshot still carries it.
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Current!.Value.GetProperty("reason").GetString(), Is.EqualTo("Rewritten"));
            Assert.That(response.TaskDiff.RequestTaskDiffs[0].Current!.Value.GetProperty("start").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task TheFilterAlsoRestrictsTheManualImplementationTaskChanges()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        apiConnection.TaskHistory =
        [
            ImplementationTaskChange(20, "DEF", "Updated workflow implementation task"),
            ImplementationTaskChange(21, "abc", "Updated workflow implementation task")
        ];
        WorkflowChangeHistoryService service = new(apiConnection);

        GetAuditProofCriticalChangesResponse response = (await service.GetAuditProofCriticalChangesAsync(1234,
            new AuditProofCriticalChangeFilter { ChangeUserName = "def" }))!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff!.ManualImplementationTaskChanges, Has.Count.EqualTo(1));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges[0].ImplementationTaskId, Is.EqualTo(20));
            Assert.That(response.TaskDiff.ManualImplementationTaskChanges[0].ChangeContent, Is.EqualTo("Updated workflow implementation task"));
        });
    }

    [Test]
    public void TaskHistoryQuerySelectsTheChangeTextTheFilterMatchesOn()
    {
        Assert.That(RequestQueries.getWorkflowTaskHistoryForTicket, Does.Contain("change_text"));
    }

    [Test]
    public async Task EmptyAuditTrailOmitsTaskDiff()
    {
        GetAuditProofCriticalChangesResponse response = await new WorkflowChangeHistoryService(new AuditProofCriticalChangesApiConnection())
            .GetAuditProofCriticalChangesAsync(4321, null) ?? throw new AssertionException("Ticket should exist.");

        Assert.That(response.TaskDiff, Is.Null);
    }

    [Test]
    public void ExistenceQueryReadsNothingButTheIdOfTheNamedTicket()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RequestQueries.getTicketIdIfExists, Does.Contain("query getTicketIdIfExists"));
            Assert.That(RequestQueries.getTicketIdIfExists, Does.Contain("$ticketId: bigint!"));
            Assert.That(RequestQueries.getTicketIdIfExists, Does.Contain("request_ticket(where: { id: { _eq: $ticketId } }, limit: 1)"));
            // request_ticket_by_pk resolves to JSON null for an unknown id, which the API connection
            // cannot deserialize; the filtered list answers with an empty array instead.
            Assert.That(RequestQueries.getTicketIdIfExists, Does.Not.Contain("request_ticket_by_pk"));
        });
    }

    [Test]
    public async Task AnUnknownTicketIdIsReportedAsNotFoundInsteadOfAnEmptyList()
    {
        AuditProofCriticalChangesApiConnection apiConnection = new() { ExistingTicketIds = [] };
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 999 });

        NotFoundObjectResult notFound = (NotFoundObjectResult)result.Result!;
        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)notFound.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(notFound.StatusCode, Is.EqualTo(404));
            Assert.That(errors.Errors.Select(error => error.Path), Is.EquivalentTo(new List<string> { "ticketId" }));
            Assert.That(errors.Errors[0].Message, Does.Contain("does not exist"));
            Assert.That(errors.Errors[0].Message, Does.Contain("999"));
        });
    }

    [Test]
    public async Task TheServiceReportsAnUnknownTicketAsNull()
    {
        AuditProofCriticalChangesApiConnection apiConnection = new() { ExistingTicketIds = [] };

        GetAuditProofCriticalChangesResponse? response =
            await new WorkflowChangeHistoryService(apiConnection).GetAuditProofCriticalChangesAsync(999, null);

        Assert.That(response, Is.Null);
    }

    [Test]
    public async Task TaskHistoryIsLoadedWhenTheTicketReturnedChanges()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 1234 });

        // A returned change proves the ticket exists, so no existence probe is needed; the task-state
        // evidence still needs its own history query.
        Assert.That(apiConnection.Queries, Is.EqualTo(new List<string>
        {
            RequestQueries.getAuditProofCriticalChangesForTicket,
            RequestQueries.getWorkflowTaskHistoryForTicket,
            RequestQueries.getTicketById
        }));
    }

    [Test]
    public async Task AFilterThatExcludesEveryChangeStaysAnEmptyResultNotANotFound()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        ActionResult<GetAuditProofCriticalChangesResponse> result = await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest
        {
            TicketId = 1234,
            Options = new GetAuditProofCriticalChangesOptions { Filter = new AuditProofCriticalChangeFilter { ChangeUserName = "nobody" } }
        });

        Assert.That(OkResponse(result).Changes, Is.Empty);
    }

    [Test]
    public void MiddlewareServerMaySelectTheTicketIdTheExistenceQueryReads()
    {
        FileInfo metadataFile = LocateMetadata();

        List<string> columns = MiddlewareServerSelectColumns(metadataFile, "request", "ticket");

        Assert.That(columns, Does.Contain("id"));
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
    public void MiddlewareServerMaySelectEveryColumnTheQueriesTouch()
    {
        FileInfo metadataFile = LocateMetadata();

        List<string> columns = MiddlewareServerSelectColumns(metadataFile, "public", "change_history");

        // The audit query and task-history query select these columns and filter and order on the
        // ticket/module/audit markers. Hasura resolves all of them through this role's permission.
        Assert.That(columns, Is.SupersetOf(new List<string>
        {
            "id", "changer", "changer_id", "change_text", "change_time", "ticket_id", "module", "audit_proof_critical",
            "change_type", "object_type", "object_id", "old_data", "new_data"
        }));
    }

    /// <summary>
    /// Reads the columns the middleware-server role may select from one tracked table.
    /// </summary>
    /// <param name="metadataFile">Hasura metadata of the installation.</param>
    /// <param name="schema">Database schema of the table.</param>
    /// <param name="table">Database name of the table.</param>
    /// <returns>The permitted columns.</returns>
    private static List<string> MiddlewareServerSelectColumns(FileInfo metadataFile, string schema, string table)
    {
        using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataFile.FullName));
        foreach (JsonElement source in metadata.RootElement.GetProperty("args").GetProperty("metadata").GetProperty("sources").EnumerateArray())
        {
            foreach (JsonElement trackedTable in source.GetProperty("tables").EnumerateArray())
            {
                if (!MatchesTable(trackedTable, schema, table)
                    || !trackedTable.TryGetProperty("select_permissions", out JsonElement selectPermissions))
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

        throw new AssertionException($"No middleware-server select permission on {schema}.{table} found.");
    }

    private static bool MatchesTable(JsonElement trackedTable, string schema, string table)
    {
        JsonElement identifier = trackedTable.GetProperty("table");
        return identifier.GetProperty("name").GetString() == table
            && identifier.GetProperty("schema").GetString() == schema;
    }

    private static FileInfo LocateMetadata()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            FileInfo repositoryMetadata = new(Path.Combine(directory.FullName, "roles", "api", "files", "replace_metadata.json"));
            if (repositoryMetadata.Exists)
            {
                return repositoryMetadata;
            }

            FileInfo installedMetadata = new(Path.Combine(directory.FullName, "replace_metadata.json"));
            if (installedMetadata.Exists)
            {
                return installedMetadata;
            }

            directory = directory.Parent;
        }

        throw new AssertionException("The Hasura metadata is not reachable in this environment.");
    }

    [Test]
    public async Task EveryChangeCarriesTheTrustworthyUserIdNextToTheFreeTextName()
    {
        // F1: changer is free text any workflow role may choose, changer_id is preset by the API from
        // the authenticated session. An audit reader needs the latter to attribute a change.
        GetAuditProofCriticalChangesResponse response = await AllChanges();

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes[0].ChangeUserId, Is.EqualTo(8));
            Assert.That(response.Changes[1].ChangeUserId, Is.EqualTo(42));
            Assert.That(response.Changes[1].ChangeUserName, Is.EqualTo("abc"));
        });
    }

    [Test]
    public async Task AChangeWrittenByAutomationReportsNoUserId()
    {
        AuditProofCriticalChangesApiConnection apiConnection = new()
        {
            Entries =
            [
                new ModellingHistoryEntry
                {
                    ChangeTime = kFirstChangeTime,
                    Changer = "middleware-server",
                    ChangerId = null,
                    ChangeText = "Updated workflow ticket"
                }
            ]
        };

        GetAuditProofCriticalChangesResponse response =
            (await new WorkflowChangeHistoryService(apiConnection).GetAuditProofCriticalChangesAsync(1234, null))!;

        Assert.That(response.Changes[0].ChangeUserId, Is.Null);
    }

    [Test]
    public async Task TheSameInstantSelectsTheSameChangeHoweverItIsSpelled()
    {
        // F3: the stored column is timezone-naive. A trailing Z keeps UTC ticks while an explicit
        // offset is converted to local time while binding, so both have to be reduced to the wall
        // clock of the installation before they are compared.
        DateTime utcInstant = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Utc);
        DateTime storedWallClock = DateTime.SpecifyKind(utcInstant.ToLocalTime(), DateTimeKind.Unspecified);

        AuditProofCriticalChangesApiConnection apiConnection = new()
        {
            Entries =
            [
                new ModellingHistoryEntry { ChangeTime = storedWallClock, Changer = "abc", ChangerId = 42, ChangeText = "Updated workflow ticket" }
            ]
        };
        WorkflowChangeHistoryService service = new(apiConnection);

        GetAuditProofCriticalChangesResponse fromUtcForm = (await service.GetAuditProofCriticalChangesAsync(
            1234, new AuditProofCriticalChangeFilter { ChangeTime = utcInstant }))!;
        GetAuditProofCriticalChangesResponse fromOffsetForm = (await service.GetAuditProofCriticalChangesAsync(
            1234, new AuditProofCriticalChangeFilter { ChangeTime = DateTime.SpecifyKind(storedWallClock, DateTimeKind.Local) }))!;
        GetAuditProofCriticalChangesResponse fromNaiveForm = (await service.GetAuditProofCriticalChangesAsync(
            1234, new AuditProofCriticalChangeFilter { ChangeTime = storedWallClock }))!;

        Assert.Multiple(() =>
        {
            Assert.That(fromUtcForm.Changes, Has.Count.EqualTo(1), "trailing Z form");
            Assert.That(fromOffsetForm.Changes, Has.Count.EqualTo(1), "explicit offset form");
            Assert.That(fromNaiveForm.Changes, Has.Count.EqualTo(1), "form without offset");
        });
    }

    [Test]
    public async Task ADifferentInstantStillSelectsNothing()
    {
        DateTime utcInstant = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Utc);
        DateTime storedWallClock = DateTime.SpecifyKind(utcInstant.ToLocalTime(), DateTimeKind.Unspecified);

        AuditProofCriticalChangesApiConnection apiConnection = new()
        {
            Entries =
            [
                new ModellingHistoryEntry { ChangeTime = storedWallClock, Changer = "abc", ChangerId = 42, ChangeText = "Updated workflow ticket" }
            ]
        };

        GetAuditProofCriticalChangesResponse response = (await new WorkflowChangeHistoryService(apiConnection)
            .GetAuditProofCriticalChangesAsync(1234, new AuditProofCriticalChangeFilter { ChangeTime = utcInstant.AddHours(1) }))!;

        Assert.That(response.Changes, Is.Empty);
    }

    [Test]
    public async Task NullColumnsAreReportedAsEmptyStringsNotNull()
    {
        // F8: changer and change_text are nullable columns, the response properties are not.
        AuditProofCriticalChangesApiConnection apiConnection = new()
        {
            Entries =
            [
                new ModellingHistoryEntry { ChangeTime = kFirstChangeTime, Changer = null!, ChangerId = null, ChangeText = null! }
            ]
        };

        GetAuditProofCriticalChangesResponse response =
            (await new WorkflowChangeHistoryService(apiConnection).GetAuditProofCriticalChangesAsync(1234, null))!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Changes[0].ChangeUserName, Is.Empty);
            Assert.That(response.Changes[0].ChangeContent, Is.Empty);
        });
    }

    [Test]
    public async Task StoredTimestampsAreReportedWithoutAKind()
    {
        // F6: the endpoint must not advertise an offset it does not have.
        GetAuditProofCriticalChangesResponse response = await AllChanges();

        Assert.That(response.Changes[0].ChangeTime!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    private static async Task<GetAuditProofCriticalChangesResponse> AllChanges(AuditProofCriticalChangesApiConnection? apiConnection = null)
    {
        return (await new WorkflowChangeHistoryService(apiConnection ?? CreateApiConnection()).GetAuditProofCriticalChangesAsync(1234, null))!;
    }

    /// <summary>
    /// Returns a request-task snapshot as it reads back from change_history.new_data: projected by the
    /// write path and serialized as a GraphQL variable by the client serializer.
    /// </summary>
    private static JToken StoredRequestTaskSnapshot(WfReqTask task)
    {
        GraphQLRequest request = new() { Variables = new { newData = WfDbAccess.RequestTaskHistorySnapshot(task) } };
        return JObject.Parse(new NewtonsoftJsonSerializer().SerializeToString(request))["variables"]!["newData"]!;
    }

    private static ModellingHistoryEntry RequestTaskInsert(long requestTaskId, JToken snapshot)
    {
        return new ModellingHistoryEntry
        {
            ChangeType = (int)ModellingTypes.ChangeType.Insert,
            ObjectType = (int)ChangeHistoryObjectType.RequestTask,
            ObjectId = requestTaskId,
            NewData = snapshot
        };
    }

    private static FwoOwnerDataHelper Owner(int ownerId)
    {
        return new FwoOwnerDataHelper { Owner = new FwoOwner { Id = ownerId } };
    }

    private static ModellingHistoryEntry ImplementationTaskChange(long implementationTaskId, string changer, string changeText)
    {
        return new ModellingHistoryEntry
        {
            ChangeType = (int)ModellingTypes.ChangeType.Update,
            ObjectType = (int)ChangeHistoryObjectType.ImplementationTask,
            ObjectId = implementationTaskId,
            ChangeTime = kSecondChangeTime,
            Changer = changer,
            ChangeText = changeText,
            AuditProofCritical = true
        };
    }

    private static async Task<GetAuditProofCriticalChangesResponse> FilteredChanges(AuditProofCriticalChangeFilter filter)
    {
        WorkflowChangeHistoryService service = new(CreateApiConnection());
        return (await service.GetAuditProofCriticalChangesAsync(1234, filter))!;
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
                    ChangerId = 8,
                    ChangeText = "Updated workflow request task"
                },
                new ModellingHistoryEntry
                {
                    ChangeTime = kFirstChangeTime,
                    Changer = "abc",
                    ChangerId = 42,
                    ChangeText = "Updated workflow ticket"
                }
            ]
        };
    }

    private sealed class AuditProofCriticalChangesApiConnection : SimulatedApiConnection
    {
        public List<ModellingHistoryEntry> Entries { get; set; } = [];

        public List<ModellingHistoryEntry> TaskHistory { get; set; } = [];

        public List<WfReqTask> CurrentTasks { get; set; } = [];

        /// <summary>
        /// Gets or sets the ticket ids the simulated API knows. Defaults to the ids the fixtures use,
        /// so a test that only cares about changes does not have to declare the ticket as well.
        /// </summary>
        public List<long> ExistingTicketIds { get; set; } = new() { 1234, 4321 };

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

            if (query == RequestQueries.getWorkflowTaskHistoryForTicket && typeof(QueryResponseType) == typeof(List<ModellingHistoryEntry>))
            {
                return Task.FromResult((QueryResponseType)(object)TaskHistory);
            }

            if (query == RequestQueries.getTicketById && typeof(QueryResponseType) == typeof(WfTicket))
            {
                return Task.FromResult((QueryResponseType)(object)new WfTicket { Id = TicketIdOf(variables), Tasks = CurrentTasks });
            }

            if (query == RequestQueries.getTicketIdIfExists && typeof(QueryResponseType) == typeof(List<WfTicketBase>))
            {
                List<WfTicketBase> tickets = ExistingTicketIds.Contains(TicketIdOf(variables))
                    ? [new WfTicketBase { Id = TicketIdOf(variables) }]
                    : [];
                return Task.FromResult((QueryResponseType)(object)tickets);
            }

            throw new AssertionException($"Unexpected query: {query}");
        }

        private static long TicketIdOf(object? variables)
        {
            return (long)(variables?.GetType().GetProperty("ticketId")?.GetValue(variables) ?? 0L);
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
