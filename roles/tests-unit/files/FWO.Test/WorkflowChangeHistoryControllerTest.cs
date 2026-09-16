using System.Reflection;
using System.Text.Json;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
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

        Assert.Multiple(() =>
        {
            Assert.That(OkResponse(result).Changes, Is.Empty);
            // An existing ticket without such changes stays a 200 with an empty list: only an id that
            // names no ticket at all is an error.
            Assert.That(apiConnection.Queries, Does.Contain(RequestQueries.getTicketIdIfExists));
        });
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
    public async Task TheExistenceProbeIsSkippedWhenTheTicketAlreadyReturnedChanges()
    {
        AuditProofCriticalChangesApiConnection apiConnection = CreateApiConnection();
        WorkflowChangeHistoryController controller = new(new WorkflowChangeHistoryService(apiConnection));

        await controller.GetAuditProofCriticalChanges(new GetAuditProofCriticalChangesRequest { TicketId = 1234 });

        // A returned change already proves the ticket exists, so the common case stays at one round trip.
        Assert.That(apiConnection.Queries, Is.EqualTo(new List<string> { RequestQueries.getAuditProofCriticalChangesForTicket }));
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
        FileInfo? metadataFile = LocateMetadata();
        if (metadataFile == null)
        {
            Assert.Ignore("The Hasura metadata is not reachable in this environment.");
            return;
        }

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
    public void MiddlewareServerMaySelectEveryColumnTheQueryTouches()
    {
        FileInfo? metadataFile = LocateMetadata();
        if (metadataFile == null)
        {
            Assert.Ignore("The Hasura metadata is not reachable in this environment.");
            return;
        }

        List<string> columns = MiddlewareServerSelectColumns(metadataFile, "public", "change_history");

        // The query selects change_time, changer, changer_id and change_text and filters and orders
        // on ticket_id, module, audit_proof_critical and id. Hasura resolves all of them through the
        // select permission of the role the middleware runs as.
        Assert.That(columns, Is.SupersetOf(new List<string>
        {
            "id", "changer", "changer_id", "change_text", "change_time", "ticket_id", "module", "audit_proof_critical"
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

    private static async Task<GetAuditProofCriticalChangesResponse> AllChanges()
    {
        return (await new WorkflowChangeHistoryService(CreateApiConnection()).GetAuditProofCriticalChangesAsync(1234, null))!;
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
