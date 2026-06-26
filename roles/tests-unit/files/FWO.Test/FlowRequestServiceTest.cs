using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowRequestServiceTest
{
    [Test]
    public async Task GetRequestStatusAsync_ReturnsStateNameAndLatestComment()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            Ticket = new WfTicket
            {
                Id = 42,
                StateId = 7,
                Comments =
                [
                    NewComment("first", new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc)),
                    NewComment("latest", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc))
                ]
            },
            States =
            [
                new WfState { Id = 7, Name = "implementation" }
            ],
            ExtStates =
            [
                new WfExtState { Name = ExtStates.ExtReqDone.ToString(), StateId = 7 },
                new WfExtState { Name = "external_implementation", StateId = 7 }
            ]
        };
        FlowRequestService service = new(apiConnection);

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Status, Is.EqualTo("external_implementation"));
            Assert.That(result.StatusComment, Is.EqualTo("latest"));
            Assert.That(apiConnection.SentQueries, Is.EqualTo(new[] { RequestQueries.getTicketById, RequestQueries.getStates, RequestQueries.getExtStates }));
            Assert.That(GetVariable(apiConnection.SentVariables[0], "id"), Is.EqualTo(42));
        });
    }

    [Test]
    public async Task GetRequestStatusAsync_ReturnsNullForUnknownTicket()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn());

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRequestStatusAsync_FallsBackToStateIdWhenStateNameMissing()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket { Id = 42, StateId = 99 }
        });

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.That(result?.Status, Is.EqualTo("99"));
    }

    [Test]
    public async Task GetRequestStatus_ReturnsBadRequestForInvalidTicketId()
    {
        FlowRequestServiceApiConn apiConnection = new();
        FlowRequestController controller = new(new FlowRequestService(apiConnection));

        ActionResult<GetRequestStatusResponse> result = await controller.GetRequestStatus(new GetRequestStatusRequest { TicketId = 0 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("ticketId"));
            Assert.That(apiConnection.SentQueries, Is.Empty);
        });
    }

    [Test]
    public async Task GetRequestStatus_ReturnsOkResponseForExistingTicket()
    {
        FlowRequestController controller = new(new FlowRequestService(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket
            {
                Id = 42,
                StateId = 7,
                Comments = [NewComment("ready", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc))]
            },
            States = [new WfState { Id = 7, Name = "implementation" }]
        }));

        ActionResult<GetRequestStatusResponse> result = await controller.GetRequestStatus(new GetRequestStatusRequest { TicketId = 42 });

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        OkObjectResult okResult = (OkObjectResult)result.Result!;
        GetRequestStatusResponse response = (GetRequestStatusResponse)okResult.Value!;
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("implementation"));
            Assert.That(response.StatusComment, Is.EqualTo("ready"));
        });
    }

    private static WfCommentDataHelper NewComment(string text, DateTime creationDate)
    {
        return new WfCommentDataHelper(new WfComment
        {
            CreationDate = creationDate,
            CommentText = text
        });
    }

    private static object? GetVariable(object? variables, string propertyName)
    {
        return variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
    }

    private sealed class FlowRequestServiceApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public List<object?> SentVariables { get; } = [];
        public WfTicket? Ticket { get; set; }
        public List<WfState> States { get; set; } = [];
        public List<WfExtState> ExtStates { get; set; } = [];

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);
            SentVariables.Add(variables);

            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(WfTicket))
            {
                return Task.FromResult((QueryResponseType)(object?)Ticket!);
            }

            if (responseType == typeof(List<WfState>))
            {
                return Task.FromResult((QueryResponseType)(object)States);
            }

            throw new NotImplementedException($"Unsupported response type {responseType.Name}");
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            SentQueries.Add(query);
            SentVariables.Add(variables);

            if (typeof(QueryResponseType) == typeof(List<WfExtState>))
            {
                return Task.FromResult((ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(ExtStates));
            }

            throw new NotImplementedException($"Unsupported response type {typeof(QueryResponseType).Name}");
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override void SetAuthHeader(string jwt)
        {
        }

        public override void SetRole(string role)
        {
        }

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
        }

        public override void SwitchBack()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override void DisposeSubscriptions<T>()
        {
        }

        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
