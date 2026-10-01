using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using FWO.Services.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the workflow/getTicket endpoint: its route and authorization, the request contract including
/// defaults and aggregated validation, the task filter, and the mapping of the ticket onto the response.
/// </summary>
[TestFixture]
internal class GetTicketEndpointTest
{
    private const long kTicketId = 42;
    private const int kRequestStateId = 0;
    private const int kApprovalStateId = 49;
    private const int kDoneStateId = 99;
    // Unspecified on purpose: the request tables use timezone-naive columns, so this is the kind a
    // stored row deserializes to.
    private static readonly DateTime kTargetBegin = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Unspecified);
    private static readonly DateTime kFirstCommentTime = new(2026, 9, 11, 9, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime kSecondCommentTime = new(2026, 9, 12, 9, 0, 0, DateTimeKind.Unspecified);
    private static readonly List<string> kExpectedQueries = [RequestQueries.getTicketById, RequestQueries.getStates, RequestQueries.getExtStates];
    private static readonly List<long> kAccessTaskIds = [501];
    private static readonly List<int> kSelectedDevices = [7, 8];
    private static readonly List<long> kAscendingIds = [1, 2, 3];
    private static readonly List<string> kCommentTextsOldestFirst = ["first", "second"];
    private static readonly string[] kExtStateErrors = ["external state query failed"];
    private static readonly List<string> kMultipleErrorPaths = ["ticketId", "options.filter.unknownFilterKey", "options.unknownOptionKey", "unknownRootKey"];

    [Test]
    public void GetTicketUsesWorkflowRouteAndReadOnlyRoles()
    {
        MethodInfo endpoint = typeof(WorkflowTicketController).GetMethod(nameof(WorkflowTicketController.GetTicket))!;

        Assert.Multiple(() =>
        {
            Assert.That(typeof(WorkflowTicketController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/workflow"));
            Assert.That(endpoint.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("getTicket"));
            Assert.That(endpoint.GetCustomAttribute<AuthorizeAttribute>()?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.Auditor}"));
            Assert.That(typeof(WorkflowTicketController).GetCustomAttribute<AggregatedValidationErrorsAttribute>(), Is.Not.Null);
        });
    }

    [Test]
    public void OptionsDefaultToAnEmptyObject()
    {
        GetTicketRequest omitted = JsonSerializer.Deserialize<GetTicketRequest>("""{"ticketId":42}""")!;
        GetTicketRequest explicitNull = JsonSerializer.Deserialize<GetTicketRequest>("""{"ticketId":42,"options":null}""")!;

        Assert.Multiple(() =>
        {
            Assert.That(omitted.Options, Is.Not.Null);
            Assert.That(omitted.Options.Filter, Is.Null);
            Assert.That(explicitNull.Options, Is.Not.Null);
            Assert.That(explicitNull.Options.Filter, Is.Null);
        });
    }

    [Test]
    public void AnOmittedTicketIdStaysNullAndIsReported()
    {
        GetTicketRequest request = JsonSerializer.Deserialize<GetTicketRequest>("{}")!;

        RequestValidationErrorResponse result = GetTicketRequestValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(request.TicketId, Is.Null);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo("ticketId"));
            Assert.That(result.Errors[0].Message, Does.Contain("required"));
        });
    }

    [Test]
    public void EveryFilterKeyIsNullableAndMatchesAScalarTaskField()
    {
        Dictionary<string, Type> responseFields = JsonFields(typeof(TicketTaskResponse));

        foreach ((string jsonName, Type filterType) in JsonFields(typeof(TicketTaskFilter)))
        {
            Assert.That(responseFields.ContainsKey(jsonName), Is.True, $"filter key {jsonName} has no response field");
            Assert.That(filterType.IsValueType ? Nullable.GetUnderlyingType(filterType) != null : true, Is.True, $"filter key {jsonName} is not nullable");
            Type responseType = Nullable.GetUnderlyingType(responseFields[jsonName]) ?? responseFields[jsonName];
            Type filterValueType = Nullable.GetUnderlyingType(filterType) ?? filterType;
            Assert.That(filterValueType, Is.EqualTo(responseType), $"filter key {jsonName} has another type than its response field");
        }
    }

    [Test]
    public void EveryScalarTaskFieldIsFilterable()
    {
        Dictionary<string, Type> filterFields = JsonFields(typeof(TicketTaskFilter));
        IEnumerable<string> scalarResponseFields = JsonFields(typeof(TicketTaskResponse))
            .Where(field => field.Value == typeof(string) || field.Value.IsValueType)
            .Select(field => field.Key);

        Assert.That(filterFields.Keys, Is.SupersetOf(scalarResponseFields));
    }

    [Test]
    public void SchemaDescribesExactlyTheBoundFilterKeys()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GetTicketValidationSchema.FilterKeys.Select(key => key.JsonName), Is.EquivalentTo(JsonFields(typeof(TicketTaskFilter)).Keys));
            Assert.That(GetTicketValidationSchema.OptionsKeys.Select(key => key.JsonName), Is.EquivalentTo(JsonFields(typeof(GetTicketOptions)).Keys));
            Assert.That(GetTicketValidationSchema.RootKeys.Select(key => key.JsonName), Is.EquivalentTo(JsonFields(typeof(GetTicketRequest)).Keys));
        });
    }

    [Test]
    public void ValidationAcceptsTheDocumentedRequest()
    {
        GetTicketRequest request = JsonSerializer.Deserialize<GetTicketRequest>(
            """{"ticketId":42,"options":{"filter":{"taskType":"access","stateId":null,"targetBeginDate":"2026-09-11T08:11:00"}}}""")!;

        Assert.That(GetTicketRequestValidator.Validate(request).Errors, Is.Empty);
    }

    [Test]
    public void ValidationReportsEveryErrorOfOneRequestTogether()
    {
        GetTicketRequest request = JsonSerializer.Deserialize<GetTicketRequest>(
            """{"ticketId":0,"unknownRootKey":1,"options":{"unknownOptionKey":true,"filter":{"unknownFilterKey":"x"}}}""")!;

        RequestValidationErrorResponse result = GetTicketRequestValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Is.EquivalentTo(kMultipleErrorPaths));
            Assert.That(result.Errors.Single(error => error.Path == "ticketId").Message, Does.Contain("greater than 0"));
            Assert.That(result.Errors.Single(error => error.Path == "options.filter.unknownFilterKey").Message, Does.Contain("'taskType'"));
        });
    }

    [Test]
    public void ValidationRejectsAMissingRequestBody()
    {
        RequestValidationErrorResponse result = GetTicketRequestValidator.Validate(null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.Empty);
            Assert.That(result.Errors[0].Message, Does.Contain("getTicket requires a request body"));
        });
    }

    [Test]
    public async Task GetTicketAsyncReturnsTheTicketWithAllDetails()
    {
        GetTicketApiConn apiConnection = new() { Ticket = BuildTicket() };
        WorkflowTicketService service = new(apiConnection, new GlobalConfig());

        GetTicketResponse? result = await service.GetTicketAsync(kTicketId, null);

        Assert.That(result, Is.Not.Null);
        TicketTaskResponse accessTask = result!.Tasks[0];
        Assert.Multiple(() =>
        {
            Assert.That(apiConnection.SentQueries, Is.EqualTo(kExpectedQueries));
            Assert.That(result.Id, Is.EqualTo(kTicketId));
            Assert.That(result.Title, Is.EqualTo("Allow HTTPS"));
            Assert.That(result.State, Is.EqualTo("Approval"));
            Assert.That(result.Status, Is.EqualTo("external_approval"));
            Assert.That(result.RequesterName, Is.EqualTo("alice"));
            Assert.That(result.RequesterDn, Is.EqualTo("uid=alice"));
            Assert.That(result.Reason, Is.Empty);
            Assert.That(result.Comments.Select(comment => comment.Text), Is.EqualTo(kCommentTextsOldestFirst));
            Assert.That(result.Tasks.Select(task => task.TaskNumber), Is.Ordered);
            Assert.That(accessTask.State, Is.EqualTo("Approval"));
            Assert.That(accessTask.ManagementName, Is.EqualTo("mgmt"));
            Assert.That(accessTask.CurrentHandlerName, Is.EqualTo("bob"));
            Assert.That(accessTask.DeviceIds, Is.EqualTo(kSelectedDevices));
            Assert.That(accessTask.TargetBeginDate?.Kind, Is.EqualTo(DateTimeKind.Unspecified));
            Assert.That(accessTask.Elements[0].Ip, Is.EqualTo("10.0.0.1/32"));
            Assert.That(accessTask.Elements[0].FlowNetworkObjectId, Is.EqualTo(17));
            Assert.That(accessTask.Approvals[0].State, Is.EqualTo("Approval"));
            Assert.That(accessTask.Approvals[0].ApproverGroup, Is.EqualTo("cn=approvers"));
            Assert.That(accessTask.Approvals[0].Comments[0].CreatorName, Is.EqualTo("carol"));
            Assert.That(accessTask.ImplementationTasks[0].State, Is.EqualTo("Done"));
            Assert.That(accessTask.ImplementationTasks[0].Elements[0].Port, Is.EqualTo(443));
            Assert.That(accessTask.ImplementationTasks[0].Elements[0].DeviceId, Is.Null);
            Assert.That(accessTask.Owners[0].ExtAppId, Is.EqualTo("APP-1"));
            Assert.That(accessTask.Comments[0].Text, Is.EqualTo("task comment"));
        });
    }

    [Test]
    public async Task GetTicketAsyncReturnsNullForAnUnknownTicket()
    {
        WorkflowTicketService service = new(new GetTicketApiConn(), new GlobalConfig());

        Assert.That(await service.GetTicketAsync(kTicketId, null), Is.Null);
    }

    [Test]
    public void GetTicketAsyncFailsWhenExternalStatesCannotBeLoaded()
    {
        WorkflowTicketService service = new(new GetTicketApiConn { Ticket = BuildTicket(), ExtStateErrors = kExtStateErrors }, new GlobalConfig());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetTicketAsync(kTicketId, null));
    }

    [Test]
    public async Task FilterRestrictsTheTasksCaseInsensitively()
    {
        WorkflowTicketService service = new(new GetTicketApiConn { Ticket = BuildTicket() }, new GlobalConfig());

        GetTicketResponse? result = await service.GetTicketAsync(kTicketId, new TicketTaskFilter { TaskType = "ACCESS" });

        Assert.That(result!.Tasks.Select(task => task.Id), Is.EqualTo(kAccessTaskIds));
    }

    [Test]
    public async Task AFilterThatExcludesEveryTaskStillReturnsTheTicket()
    {
        WorkflowTicketService service = new(new GetTicketApiConn { Ticket = BuildTicket() }, new GlobalConfig());

        GetTicketResponse? result = await service.GetTicketAsync(kTicketId, new TicketTaskFilter { TaskType = "access", Locked = false });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Tasks, Is.Empty);
        });
    }

    [Test]
    public void NullFilterKeysApplyNoRestriction()
    {
        TicketTaskResponse task = new() { Id = 1, Title = "t" };

        Assert.Multiple(() =>
        {
            Assert.That(TicketResponseMapper.Matches(task, null), Is.True);
            Assert.That(TicketResponseMapper.Matches(task, new TicketTaskFilter()), Is.True);
        });
    }

    [Test]
    public void EveryFilterKeyRestrictsTheResult()
    {
        TicketTaskResponse task = new();

        foreach (PropertyInfo property in FilterProperties())
        {
            TicketTaskFilter filter = new();
            property.SetValue(filter, NonMatchingValue(property.PropertyType));
            Assert.That(TicketResponseMapper.Matches(task, filter), Is.False, $"filter key {property.Name} does not restrict the result");
        }
    }

    [Test]
    public void TheSameInstantSelectsTheTaskHoweverItIsSpelled()
    {
        TicketTaskResponse task = new() { TargetBeginDate = kTargetBegin };
        DateTime utc = DateTime.SpecifyKind(kTargetBegin, DateTimeKind.Local).ToUniversalTime();

        Assert.Multiple(() =>
        {
            Assert.That(TicketResponseMapper.Matches(task, new TicketTaskFilter { TargetBeginDate = kTargetBegin }), Is.True);
            Assert.That(TicketResponseMapper.Matches(task, new TicketTaskFilter { TargetBeginDate = utc }), Is.True);
            Assert.That(TicketResponseMapper.Matches(task, new TicketTaskFilter { TargetBeginDate = kTargetBegin.AddMinutes(1) }), Is.False);
        });
    }

    [Test]
    public void NullStoredValuesAreMappedToTheEmptyStringsOfTheContract()
    {
        WfTicket ticket = new()
        {
            Id = kTicketId,
            Title = null!,
            RequesterDn = null,
            Comments = [new WfCommentDataHelper { Comment = null! }],
            Tasks = [new WfReqTask { Title = null!, Reason = null, Owners = [new FwoOwnerDataHelper { Owner = null! }] }]
        };

        GetTicketResponse result = TicketResponseMapper.Map(ticket, new WfStateDict(), "0", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Title, Is.Empty);
            Assert.That(result.RequesterDn, Is.Empty);
            Assert.That(result.Comments, Is.Empty);
            Assert.That(result.Tasks[0].Title, Is.Empty);
            Assert.That(result.Tasks[0].Reason, Is.Empty);
            Assert.That(result.Tasks[0].Owners, Is.Empty);
            Assert.That(result.Tasks[0].State, Is.EqualTo("0"));
        });
    }

    [Test]
    public void NestedTaskListsAreOrderedByIdWhateverOrderTheApiReturns()
    {
        WfTicket ticket = new()
        {
            Id = kTicketId,
            Tasks =
            [
                new WfReqTask
                {
                    Id = 501,
                    Elements = [new WfReqElement { Id = 3 }, new WfReqElement { Id = 1 }, new WfReqElement { Id = 2 }],
                    Approvals = [new WfApproval { Id = 3 }, new WfApproval { Id = 1 }, new WfApproval { Id = 2 }],
                    Owners =
                    [
                        new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 3 } },
                        new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 1 } },
                        new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 2 } }
                    ],
                    ImplementationTasks =
                    [
                        new WfImplTask
                        {
                            Id = 601,
                            ImplElements = [new WfImplElement { Id = 3 }, new WfImplElement { Id = 1 }, new WfImplElement { Id = 2 }]
                        }
                    ]
                }
            ]
        };

        TicketTaskResponse task = TicketResponseMapper.Map(ticket, new WfStateDict(), "0", null).Tasks[0];

        Assert.Multiple(() =>
        {
            Assert.That(task.Elements.Select(element => element.Id), Is.EqualTo(kAscendingIds));
            Assert.That(task.Approvals.Select(approval => approval.Id), Is.EqualTo(kAscendingIds));
            Assert.That(task.Owners.Select(owner => (long)owner.Id), Is.EqualTo(kAscendingIds));
            Assert.That(task.ImplementationTasks[0].Elements.Select(element => element.Id), Is.EqualTo(kAscendingIds));
        });
    }

    [Test]
    public async Task ControllerReturnsTheTicket()
    {
        WorkflowTicketController controller = new(new WorkflowTicketService(new GetTicketApiConn { Ticket = BuildTicket() }, new GlobalConfig()));

        ActionResult<GetTicketResponse> result = await controller.GetTicket(new GetTicketRequest { TicketId = kTicketId });

        Assert.That(((OkObjectResult)result.Result!).Value, Is.TypeOf<GetTicketResponse>());
    }

    [Test]
    public async Task ControllerRejectsAnInvalidRequestBeforeTheApiIsQueried()
    {
        GetTicketApiConn apiConnection = new() { Ticket = BuildTicket() };
        WorkflowTicketController controller = new(new WorkflowTicketService(apiConnection, new GlobalConfig()));

        ActionResult<GetTicketResponse> result = await controller.GetTicket(new GetTicketRequest { TicketId = -1 });

        Assert.Multiple(() =>
        {
            Assert.That(((BadRequestObjectResult)result.Result!).Value, Is.TypeOf<RequestValidationErrorResponse>());
            Assert.That(apiConnection.SentQueries, Is.Empty);
        });
    }

    [Test]
    public async Task ControllerReportsAnUnknownTicketAsNotFound()
    {
        WorkflowTicketController controller = new(new WorkflowTicketService(new GetTicketApiConn(), new GlobalConfig()));

        ActionResult<GetTicketResponse> result = await controller.GetTicket(new GetTicketRequest { TicketId = kTicketId });

        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)((NotFoundObjectResult)result.Result!).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(errors.Errors[0].Path, Is.EqualTo("ticketId"));
            Assert.That(errors.Errors[0].Message, Is.EqualTo("Workflow ticket with 'ticketId' 42 does not exist."));
        });
    }

    [Test]
    public async Task ControllerReportsApiFailuresAsInternalServerError()
    {
        WorkflowTicketController controller = new(new WorkflowTicketService(new GetTicketApiConn { ThrowOnTicketQuery = true }, new GlobalConfig()));

        ActionResult<GetTicketResponse> result = await controller.GetTicket(new GetTicketRequest { TicketId = kTicketId });

        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public void ResponseUsesCamelCaseJsonNames()
    {
        string json = JsonSerializer.Serialize(TicketResponseMapper.Map(BuildTicket(), new WfStateDict(), "s", null));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"implementationTasks\""));
            Assert.That(json, Does.Contain("\"approvals\""));
            Assert.That(json, Does.Contain("\"requesterName\""));
            Assert.That(json, Does.Not.Contain("\"reqtasks\""));
            Assert.That(json, Does.Not.Contain("\"RemovedElements\""));
        });
    }

    private static Dictionary<string, Type> JsonFields(Type type)
    {
        return type.GetProperties()
            .Select(property => (Property: property, Name: property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .Where(field => field.Name != null)
            .ToDictionary(field => field.Name!, field => field.Property.PropertyType);
    }

    private static IEnumerable<PropertyInfo> FilterProperties()
    {
        return typeof(TicketTaskFilter).GetProperties()
            .Where(property => property.GetCustomAttribute<JsonPropertyNameAttribute>() != null);
    }

    /// <summary>
    /// Returns a value no default-constructed task carries for the supplied filter key type.
    /// </summary>
    private static object NonMatchingValue(Type filterType)
    {
        Type valueType = Nullable.GetUnderlyingType(filterType) ?? filterType;
        return valueType switch
        {
            _ when valueType == typeof(string) => "no-such-value",
            _ when valueType == typeof(int) => 12345,
            _ when valueType == typeof(long) => 12345L,
            _ when valueType == typeof(bool) => true,
            _ when valueType == typeof(DateTime) => kTargetBegin,
            _ => throw new NotSupportedException($"No non-matching value for {valueType.Name}")
        };
    }

    private static WfTicket BuildTicket()
    {
        WfReqTask accessTask = new()
        {
            Id = 501,
            TaskNumber = 1,
            Title = "HTTPS",
            TaskType = WfTaskType.access.ToString(),
            StateId = kApprovalStateId,
            TargetBeginDate = kTargetBegin,
            ManagementId = 3,
            OnManagement = new Management { Id = 3, Name = "mgmt" },
            CurrentHandler = new UiUser { Name = "bob" },
            Locked = true,
            Elements = [new WfReqElement { Id = 1, Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", FlowNetworkObjectId = 17 }],
            Approvals =
            [
                new WfApproval
                {
                    Id = 91,
                    StateId = kApprovalStateId,
                    ApproverGroup = "cn=approvers",
                    Comments = [NewComment("approval comment", "carol", kFirstCommentTime)]
                }
            ],
            ImplementationTasks =
            [
                new WfImplTask
                {
                    Id = 601,
                    TaskNumber = 1,
                    StateId = kDoneStateId,
                    ImplElements = [new WfImplElement { Id = 2, Field = ElemFieldType.service.ToString(), Port = 443, PortEnd = 443, ProtoId = 6 }]
                }
            ],
            Owners = [new FwoOwnerDataHelper { Owner = new FwoOwner { Id = 12, Name = "Payments", ExtAppId = "APP-1" } }],
            Comments = [NewComment("task comment", "alice", kFirstCommentTime)]
        };
        accessTask.SetDeviceList(kSelectedDevices);

        return new WfTicket
        {
            Id = kTicketId,
            Title = "Allow HTTPS",
            StateId = kApprovalStateId,
            Requester = new UiUser { Name = "alice" },
            RequesterDn = "uid=alice",
            Reason = null,
            Locked = true,
            Tasks =
            [
                new WfReqTask { Id = 502, TaskNumber = 2, Title = "group", TaskType = WfTaskType.group_create.ToString(), StateId = kRequestStateId },
                accessTask
            ],
            Comments =
            [
                NewComment("second", "alice", kSecondCommentTime),
                NewComment("first", "alice", kFirstCommentTime)
            ]
        };
    }

    private static WfCommentDataHelper NewComment(string text, string creator, DateTime creationDate)
    {
        return new WfCommentDataHelper
        {
            Comment = new WfComment { CommentText = text, Creator = new UiUser { Name = creator }, CreationDate = creationDate }
        };
    }

    private sealed class GetTicketApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public WfTicket? Ticket { get; set; }
        public bool ThrowOnTicketQuery { get; set; }
        public string[]? ExtStateErrors { get; set; }

        private static readonly List<WfState> kStates =
        [
            new WfState { Id = kApprovalStateId, Name = "Approval" },
            new WfState { Id = kDoneStateId, Name = "Done" }
        ];

        private static readonly List<WfExtState> kExtStates =
        [
            new WfExtState { Name = "external_approval", StateId = kApprovalStateId }
        ];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);
            if (query == RequestQueries.getTicketById)
            {
                if (ThrowOnTicketQuery)
                {
                    throw new InvalidOperationException("simulated API failure");
                }
                return Task.FromResult((QueryResponseType)(object?)Ticket!);
            }
            if (query == RequestQueries.getStates)
            {
                return Task.FromResult((QueryResponseType)(object)kStates);
            }
            throw new NotImplementedException(query);
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            SentQueries.Add(query);
            ApiResponse<List<WfExtState>> response = ExtStateErrors != null ? new(ExtStateErrors) : new(kExtStates);
            return Task.FromResult((ApiResponse<QueryResponseType>)(object)response);
        }
    }
}
