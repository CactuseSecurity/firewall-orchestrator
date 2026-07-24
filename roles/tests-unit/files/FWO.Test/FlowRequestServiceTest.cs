using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Security.Claims;

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
        FlowRequestService service = new(apiConnection, new GlobalConfig());

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
    public async Task GetRequestStatusAsync_RefreshesWorkflowStateNamesOnEachCall()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            Ticket = new WfTicket { Id = 42, StateId = 7 },
            States = [new WfState { Id = 7, Name = "implementation" }]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig());

        GetRequestStatusResponse? first = await service.GetRequestStatusAsync(42);
        apiConnection.States = [new WfState { Id = 7, Name = "changed" }];
        GetRequestStatusResponse? second = await service.GetRequestStatusAsync(42);

        Assert.Multiple(() =>
        {
            Assert.That(first?.Status, Is.EqualTo("implementation"));
            Assert.That(second?.Status, Is.EqualTo("changed"));
            Assert.That(apiConnection.SentQueries.Count(query => query == RequestQueries.getStates), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetRequestStatusAsync_ReturnsNullForUnknownTicket()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn(), new GlobalConfig());

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetRequestStatusAsync_FallsBackToStateIdWhenStateNameMissing()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket { Id = 42, StateId = 99 }
        }, new GlobalConfig());

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.That(result?.Status, Is.EqualTo("99"));
    }

    [Test]
    public void GetRequestStatusAsync_ThrowsWhenExternalStatesCannotBeLoaded()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket { Id = 42, StateId = 7 },
            States = [new WfState { Id = 7, Name = "implementation" }],
            ExtStateErrors = ["external state query failed"]
        }, new GlobalConfig());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetRequestStatusAsync(42));
    }

    [Test]
    public async Task GetRequestStatusAsync_IgnoresNullCommentWrappers()
    {
        FlowRequestService service = new(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket
            {
                Id = 42,
                StateId = 7,
                Comments =
                [
                    new WfCommentDataHelper { Comment = null! },
                    NewComment("latest", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc))
                ]
            },
            States = [new WfState { Id = 7, Name = "implementation" }]
        }, new GlobalConfig());

        GetRequestStatusResponse? result = await service.GetRequestStatusAsync(42);

        Assert.That(result?.StatusComment, Is.EqualTo("latest"));
    }

    [Test]
    public async Task GetRequestStatus_ReturnsBadRequestForInvalidTicketId()
    {
        FlowRequestServiceApiConn apiConnection = new();
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));

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
        }, new GlobalConfig()));

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

    [Test]
    public async Task GetRequestStatus_ReturnsInternalServerErrorWhenExternalStatesCannotBeLoaded()
    {
        FlowRequestController controller = new(new FlowRequestService(new FlowRequestServiceApiConn
        {
            Ticket = new WfTicket { Id = 42, StateId = 7 },
            States = [new WfState { Id = 7, Name = "implementation" }],
            ExtStateErrors = ["external state query failed"]
        }, new GlobalConfig()));

        ActionResult<GetRequestStatusResponse> result = await controller.GetRequestStatus(new GetRequestStatusRequest { TicketId = 42 });

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        ObjectResult errorResult = (ObjectResult)result.Result!;
        Assert.Multiple(() =>
        {
            Assert.That(errorResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            Assert.That(errorResult.Value, Is.EqualTo("Internal server error"));
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsCreatedTicketAndResolvesTemporaryIds()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig());

        CreateRequestResponse response = await service.CreateRequestAsync(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Allow HTTPS to app server",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            TimeObjects =
            [
                new CreateRequestRequest.CreateTimeObjectRequest
                {
                    Id = "-3",
                    Name = "business-hours",
                    StartTime = "2026-07-01T08:00:00Z",
                    EndTime = "2026-07-01T18:00:00Z"
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    Name = "Allow app traffic",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2],
                    TimeObjectId = -3,
                    ViolationJustification = "Business approved."
                }
            ]
        }, 77);

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("draft"));
            Assert.That(response.RequestId, Is.EqualTo(100));
            Assert.That(apiConnection.SentQueries, Does.Contain(RequestQueries.getStates));
            Assert.That(apiConnection.SentQueries, Does.Contain(StmQueries.getRuleActions));
            Assert.That(apiConnection.SentQueries, Does.Contain(StmQueries.getIpProtocols));
            Assert.That(apiConnection.SentQueries, Contains.Item(RequestQueries.newTicket));
            Assert.That(apiConnection.SentQueries, Contains.Item(RequestQueries.getTicketById));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter!.Tasks, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].RuleAction, Is.EqualTo(1));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].Elements.WfElementList, Has.Count.EqualTo(3));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].Elements.WfElementList.All(element => element.NetworkId == null && element.ServiceId == null), Is.True);
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].NetworkGroupId, Is.Null);
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].ServiceGroupId, Is.Null);
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].TargetBeginDate, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].TargetEndDate, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].GetAddInfoValue(AdditionalInfoKeys.TimeObjectId), Is.EqualTo("-3"));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].GetAddInfoValue("timeStart"), Is.EqualTo(""));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].GetAddInfoValue("timeEnd"), Is.EqualTo(""));
        });
    }

    [Test]
    public async Task CreateRequest_UsesConfiguredInitialTicketState()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States =
            [
                new WfState { Id = 17, Name = "requested" },
                new WfState { Id = 0, Name = "draft" }
            ],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig { ReqApiTicketInitialStateId = 17 });

        CreateRequestResponse response = await service.CreateRequestAsync(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Configured state request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        }, 77);

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("requested"));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(GetVariable(apiConnection.NewTicketVariables, "state"), Is.EqualTo(17));
            Assert.That(apiConnection.CreatedTicket, Is.Not.Null);
            Assert.That(apiConnection.CreatedTicket!.Requester?.DbId, Is.EqualTo(77));
        });
    }

    [Test]
    public async Task CreateRequest_MapsDropActionToConfiguredRuleActionId()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }],
            RuleActions =
            [
                new RuleAction { Id = 1, Name = "accept", Allowed = true },
                new RuleAction { Id = 2, Name = "drop", Allowed = false }
            ]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig());

        CreateRequestResponse response = await service.CreateRequestAsync(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Drop blocked traffic",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "drop",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        }, 77);

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("draft"));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter!.Tasks, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].RuleAction, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CreateRequest_AllowsGroupCreateTasks()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }],
            RuleActions = [new RuleAction { Id = 1, Name = "accept", Allowed = true }]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig());

        CreateRequestResponse response = await service.CreateRequestAsync(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Grouped request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            AddressGroups =
            [
                new CreateRequestRequest.CreateAddressGroupRequest
                {
                    Id = -3,
                    Name = "app-servers",
                    MemberIds = [-1]
                }
            ],
            ServiceGroups =
            [
                new CreateRequestRequest.CreateServiceGroupRequest
                {
                    Id = -4,
                    Name = "web-services",
                    MemberIds = [-2]
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-3],
                    DestinationObjects = [-3],
                    ServiceObjects = [-4]
                }
            ]
        }, 77);

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("draft"));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter!.Tasks, Has.Count.EqualTo(3));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
            Assert.That(apiConnection.LastTicketWriter.Tasks[0].GetAddInfoValue(AdditionalInfoKeys.GrpName), Is.EqualTo("app-servers"));
            Assert.That(apiConnection.LastTicketWriter.Tasks[1].TaskType, Is.EqualTo(WfTaskType.group_create.ToString()));
            Assert.That(apiConnection.LastTicketWriter.Tasks[1].GetAddInfoValue(AdditionalInfoKeys.GrpName), Is.EqualTo("web-services"));
            Assert.That(apiConnection.LastTicketWriter.Tasks[2].TaskType, Is.EqualTo(WfTaskType.access.ToString()));
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsOkResponseAndUsesPayloadRequester()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }],
            Owners = [new FwoOwner { Id = 42, Name = "Finance" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Allow HTTPS to app server",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            AddressGroups =
            [
                new CreateRequestRequest.CreateAddressGroupRequest
                {
                    Id = -3,
                    Name = "app-servers",
                    MemberIds = [-1]
                }
            ],
            ServiceGroups =
            [
                new CreateRequestRequest.CreateServiceGroupRequest
                {
                    Id = -4,
                    Name = "web-services",
                    MemberIds = [-2]
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    OwnerId = 42,
                    SourceObjects = [-3],
                    DestinationObjects = [-3],
                    ServiceObjects = [-4]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            CreateRequestResponse response = (CreateRequestResponse)((OkObjectResult)result.Result!).Value!;
            Assert.That(response.Status, Is.EqualTo("draft"));
            Assert.That(response.RequestId, Is.EqualTo(100));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(apiConnection.LastTicketWriter!.Tasks, Has.Count.EqualTo(3));
            Assert.That(apiConnection.CreatedTicket!.Requester?.DbId, Is.EqualTo(77));
            Assert.That(apiConnection.CreatedTicket.Tasks[2].Owners, Has.Count.EqualTo(1));
            Assert.That(apiConnection.CreatedTicket.Tasks[2].Owners[0].Owner.Id, Is.EqualTo(42));
            Assert.That(apiConnection.LastTicketWriter!.Tasks[2].Owners.WfOwnerList, Has.Count.EqualTo(1));
            Assert.That(apiConnection.LastTicketWriter.Tasks[2].Owners.WfOwnerList[0].OwnerId, Is.EqualTo(42));
        });
    }

    [Test]
    public async Task CreateRequest_UsesMatrixLowestInputStateWhenInitialStateIsUnset()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States =
            [
                new WfState { Id = 3, Name = "open" },
                new WfState { Id = 17, Name = "requested" }
            ],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }],
            WorkflowConfigurations =
            [
                new WorkflowConfiguration
                {
                    Id = 1,
                    Name = "default",
                    Phases =
                    [
                        new WorkflowConfigurationPhase
                        {
                            TaskType = WfTaskType.master.ToString(),
                            Phase = WorkflowPhases.request.ToString(),
                            PhaseMatrix = new StateMatrixPhase
                            {
                                Id = 11,
                                Name = "request",
                                Phase = WorkflowPhases.request.ToString(),
                                Active = true,
                                LowestInputState = 3,
                                LowestStartState = 3,
                                LowestEndState = 17
                            }
                        }
                    ]
                }
            ]
        };
        FlowRequestService service = new(apiConnection, new GlobalConfig());

        CreateRequestResponse response = await service.CreateRequestAsync(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Unset state request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        }, 77);

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo("open"));
            Assert.That(response.RequestId, Is.EqualTo(100));
            Assert.That(GetVariable(apiConnection.NewTicketVariables, "state"), Is.EqualTo(3));
            Assert.That(apiConnection.SentQueries, Does.Contain(RequestQueries.getActiveStateMatrixConfiguration));
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsConfiguredInitialStateFromController()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States =
            [
                new WfState { Id = 17, Name = "requested" },
                new WfState { Id = 0, Name = "draft" }
            ],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig { ReqApiTicketInitialStateId = 17 }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Configured state request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            CreateRequestResponse response = (CreateRequestResponse)((OkObjectResult)result.Result!).Value!;
            Assert.That(response.Status, Is.EqualTo("requested"));
            Assert.That(response.RequestId, Is.EqualTo(100));
            Assert.That(apiConnection.LastTicketWriter, Is.Not.Null);
            Assert.That(GetVariable(apiConnection.NewTicketVariables, "state"), Is.EqualTo(17));
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForUnknownNumericProtocolId()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Unknown protocol request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "999",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("protocol"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForUnknownOwnerId()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }],
            Owners = [new FwoOwner { Id = 42, Name = "Finance" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Unknown owner request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    OwnerId = 999,
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("owner"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForUnknownTimeObjectId()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Unknown time object request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2],
                    TimeObjectId = -99
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("time object"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForWrongKindTimeObjectId()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Wrong kind time object request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2],
                    TimeObjectId = -1
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("time object"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForServiceObjectInSourceObjects()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Wrong source kind request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-2],
                    DestinationObjects = [-1],
                    ServiceObjects = [-2]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("source"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
        });
    }

    [Test]
    public async Task CreateRequest_ReturnsBadRequestForAddressObjectInServiceObjects()
    {
        FlowRequestServiceApiConn apiConnection = new()
        {
            States = [new WfState { Id = 0, Name = "draft" }],
            Protocols = [new IpProtocol { Id = 6, Name = "tcp" }]
        };
        FlowRequestController controller = new(new FlowRequestService(apiConnection, new GlobalConfig()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("x-hasura-user-id", "77"),
                        new Claim(ClaimTypes.Name, "Trusted Requester"),
                        new Claim("x-hasura-uuid", "uid=trusted,dc=fworch,dc=internal")
                    ],
                    "test"))
            }
        };

        ActionResult<CreateRequestResponse> result = await controller.CreateRequest(new CreateRequestRequest
        {
            RequestorName = "Payload Requester",
            RequestorId = "payload-requester",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Wrong service kind request",
            AddressObjects =
            [
                new CreateRequestRequest.CreateAddressObjectRequest
                {
                    Id = "-1",
                    Name = "app-server-1",
                    IpStart = "192.0.2.10",
                    IpEnd = "192.0.2.10"
                }
            ],
            ServiceObjects =
            [
                new CreateRequestRequest.CreateServiceObjectRequest
                {
                    Id = "-2",
                    Name = "https",
                    Protocol = "tcp",
                    PortStart = 443,
                    PortEnd = 443
                }
            ],
            Rules =
            [
                new CreateRequestRequest.CreateRequestRuleRequest
                {
                    Action = "accept",
                    SourceObjects = [-1],
                    DestinationObjects = [-1],
                    ServiceObjects = [-1]
                }
            ]
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("service"));
            Assert.That(apiConnection.LastTicketWriter, Is.Null);
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
        if (variables is IDictionary<string, object?> dictionary && dictionary.TryGetValue(propertyName, out object? value))
        {
            return value;
        }

        return variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
    }

    private sealed class FlowRequestServiceApiConn : SimulatedApiConnection
    {
        public List<string> SentQueries { get; } = [];
        public List<object?> SentVariables { get; } = [];
        public WfTicket? Ticket { get; set; }
        public List<WfState> States { get; set; } = [];
        public List<IpProtocol> Protocols { get; set; } = [];
        public List<FwoOwner> Owners { get; set; } = [];
        public List<RuleAction> RuleActions { get; set; } = [new RuleAction { Id = 1, Name = "accept", Allowed = true }];
        public List<WfExtState> ExtStates { get; set; } = [];
        public List<WorkflowConfiguration> WorkflowConfigurations { get; set; } =
        [
            new WorkflowConfiguration
            {
                Id = 1,
                Name = "default",
                Phases =
                [
                    new WorkflowConfigurationPhase
                    {
                        TaskType = WfTaskType.master.ToString(),
                        Phase = WorkflowPhases.request.ToString(),
                        PhaseMatrix = new StateMatrixPhase
                        {
                            Id = 11,
                            Name = "request",
                            Phase = WorkflowPhases.request.ToString(),
                            Active = true,
                            LowestInputState = 0,
                            LowestStartState = 0,
                            LowestEndState = 0
                        }
                    }
                ]
            }
        ];
        public string[]? ExtStateErrors { get; set; }
        public WfTicketWriter? LastTicketWriter { get; private set; }
        public object? NewTicketVariables { get; private set; }
        public WfTicket? CreatedTicket { get; private set; }
        private long nextId = 99;

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            SentQueries.Add(query);
            SentVariables.Add(variables);

            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(WfTicket))
            {
                WfTicket? ticket = CreatedTicket ?? Ticket;
                return Task.FromResult((QueryResponseType)(object?)ticket!);
            }

            if (responseType == typeof(List<WfState>))
            {
                return Task.FromResult((QueryResponseType)(object)States);
            }

            if (responseType == typeof(List<IpProtocol>))
            {
                return Task.FromResult((QueryResponseType)(object)Protocols);
            }

            if (responseType == typeof(List<FwoOwner>))
            {
                return Task.FromResult((QueryResponseType)(object)Owners);
            }

            if (responseType == typeof(List<RuleAction>))
            {
                return Task.FromResult((QueryResponseType)(object)RuleActions);
            }

            if (responseType == typeof(List<WorkflowConfiguration>))
            {
                return Task.FromResult((QueryResponseType)(object)WorkflowConfigurations);
            }

            if (responseType == typeof(ReturnId))
            {
                return Task.FromResult((QueryResponseType)(object)new ReturnId
                {
                    UpdatedIdLong = Convert.ToInt64(GetVariable(variables, "id") ?? 0)
                });
            }

            if (query == RequestQueries.newTicket)
            {
                NewTicketVariables = variables;
                LastTicketWriter = (WfTicketWriter?)GetVariable(variables, "requestTasks");
                CreatedTicket = BuildCreatedTicket(variables, ++nextId);
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { NewIdLong = CreatedTicket.Id }]
                });
            }

            throw new NotImplementedException($"Unsupported response type {responseType.Name}");
        }

        public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            SentQueries.Add(query);
            SentVariables.Add(variables);

            if (typeof(QueryResponseType) == typeof(List<WfExtState>))
            {
                if (ExtStateErrors != null)
                {
                    return Task.FromResult((ApiResponse<QueryResponseType>)(object)new ApiResponse<List<WfExtState>>(ExtStateErrors));
                }
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

        private static WfTicket BuildCreatedTicket(object? variables, long ticketId)
        {
            List<WfReqTask> tasks = [];
            WfTicketWriter? writer = (WfTicketWriter?)GetVariable(variables, "requestTasks");
            long taskId = 0;
            if (writer != null)
            {
                foreach (WfReqTaskWriter taskWriter in writer.Tasks)
                {
                    tasks.Add(new WfReqTask
                    {
                        Id = ++taskId,
                        Title = taskWriter.Title,
                        TaskNumber = taskWriter.TaskNumber,
                        TaskType = taskWriter.TaskType,
                        TicketId = ticketId,
                        StateId = taskWriter.StateId,
                        RequestAction = taskWriter.RequestAction,
                        Reason = taskWriter.Reason,
                        AdditionalInfo = taskWriter.AdditionalInfo,
                        Locked = taskWriter.Locked,
                        Tracking = taskWriter.Tracking,
                        RuleAction = taskWriter.RuleAction,
                        ManagementId = taskWriter.ManagementId,
                        Elements = [],
                        Approvals = [],
                        Owners = [.. taskWriter.Owners.WfOwnerList.Select(ownerWriter => new FwoOwnerDataHelper
                        {
                            Owner = new FwoOwner
                            {
                                Id = ownerWriter.OwnerId ?? 0
                            }
                        })]
                    });
                }
            }

            return new WfTicket
            {
                Id = ticketId,
                Title = Convert.ToString(GetVariable(variables, "title")) ?? "",
                StateId = Convert.ToInt32(GetVariable(variables, "state") ?? 0),
                Reason = Convert.ToString(GetVariable(variables, "reason")) ?? "",
                Locked = Convert.ToBoolean(GetVariable(variables, "locked") ?? false),
                Priority = Convert.ToInt32(GetVariable(variables, "priority") ?? 0),
                Deadline = GetVariable(variables, "deadline") is DateTime deadline ? deadline : null,
                Requester = new UiUser
                {
                    DbId = Convert.ToInt32(GetVariable(variables, "requesterId") ?? 0),
                    Name = Convert.ToString(GetVariable(variables, "requesterName")) ?? "",
                    Dn = Convert.ToString(GetVariable(variables, "requesterDn")) ?? ""
                },
                Tasks = tasks
            };
        }
    }
}
