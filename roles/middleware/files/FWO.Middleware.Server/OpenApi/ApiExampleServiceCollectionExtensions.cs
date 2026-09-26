using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using System.Reflection;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Registers API documentation example services.
/// </summary>
public static class ApiExampleServiceCollectionExtensions
{
    /// <summary>
    /// Adds FWO-owned REST API examples and adapters.
    /// </summary>
    public static IServiceCollection AddApiExamples(this IServiceCollection services)
    {
        services.AddSingleton<ApiExampleObjectFactory>();
        services.AddSingleton<ApiExampleCatalog>();
        services.AddSingleton<IApiExampleProvider, CreateRequestRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetRequestStatusRequestExample>();
        services.AddSingleton<IApiExampleProvider, VisibleInRequestFilterExample>();
        services.AddSingleton<IApiExampleProvider, GetAddressGroupsRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetFlowComplianceStateRequestExample>();
        services.AddSingleton<IApiExampleProvider, ResolveZonesForObjectsRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetOwnersRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetAuditProofCriticalChangesRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetTicketRequestExample>();
        services.AddSingleton<IApiExampleProvider, CreateRequestResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetRequestStatusResponseExample>();
        services.AddSingleton<IApiExampleProvider, FlowComplianceStateResponseExample>();
        services.AddSingleton<IApiExampleProvider, ComplianceDesignatedZoneResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetPolicyIdsResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressGroupResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceObjectResponseListExample>();
        services.AddSingleton<IApiExampleProvider, ServiceGroupResponseExample>();
        services.AddSingleton<IApiExampleProvider, TimeObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressObjectIdResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceObjectIdResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetOwnerResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetAuditProofCriticalChangesResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetTicketResponseExample>();
        services.AddOpenApiEndpointDocumentationProviders();
        return services;
    }

    private static IServiceCollection AddOpenApiEndpointDocumentationProviders(this IServiceCollection services)
    {
        IEnumerable<Type> providerTypes = typeof(ApiExampleServiceCollectionExtensions).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && !type.IsInterface
                && typeof(IOpenApiEndpointDocumentationProvider).IsAssignableFrom(type));

        foreach (Type providerType in providerTypes)
        {
            services.AddSingleton(typeof(IOpenApiEndpointDocumentationProvider), providerType);
        }

        return services;
    }
}

/// <summary>
/// Provides a typed example for <see cref="CreateRequestRequest"/>.
/// </summary>
public sealed class CreateRequestRequestExample : ApiExampleProvider<CreateRequestRequest>
{
    /// <inheritdoc />
    public override CreateRequestRequest GetExample() => new()
    {
        RequestorName = "Alice Example",
        RequestorId = "alice",
        RuleContactName = "Bob Approver",
        RuleContactId = "bob",
        Title = "Allow HTTPS to application server",
        Rules =
        [
            new CreateRequestRequest.CreateRequestRuleRequest
            {
                Action = "accept",
                Name = "Allow app HTTPS",
                SourceObjects = [-1],
                DestinationObjects = [-3],
                ServiceObjects = [-2],
                TimeObjectId = -4,
                OwnerId = 42,
                ViolationJustification = "Business-approved application traffic."
            }
        ],
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
        AddressGroups =
        [
            new CreateRequestRequest.CreateAddressGroupRequest
            {
                Id = -3,
                Name = "app-servers",
                MemberIds = [-1]
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
        ServiceGroups =
        [
            new CreateRequestRequest.CreateServiceGroupRequest
            {
                Id = -5,
                Name = "web-services",
                MemberIds = [-2]
            }
        ],
        TimeObjects =
        [
            new CreateRequestRequest.CreateTimeObjectRequest
            {
                Id = "-4",
                Name = "Temporary rule window",
                StartTime = "2026-08-01T00:00:00Z",
                EndTime = "2026-08-31T23:59:59Z"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetRequestStatusRequest"/>.
/// </summary>
public sealed class GetRequestStatusRequestExample : ApiExampleProvider<GetRequestStatusRequest>
{
    /// <inheritdoc />
    public override GetRequestStatusRequest GetExample() => new()
    {
        TicketId = 12345
    };
}

/// <summary>
/// Provides a typed example for <see cref="VisibleInRequestFilter"/>.
/// </summary>
public sealed class VisibleInRequestFilterExample : ApiExampleProvider<VisibleInRequestFilter>
{
    /// <inheritdoc />
    public override VisibleInRequestFilter GetExample() => new()
    {
        VisibleInRequest = true
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetAddressGroupsRequest"/>.
/// The example documents the default response shape, so zone separation is switched off.
/// </summary>
public sealed class GetAddressGroupsRequestExample : ApiExampleProvider<GetAddressGroupsRequest>
{
    /// <inheritdoc />
    public override GetAddressGroupsRequest GetExample() => new()
    {
        Filter = new VisibleInRequestFilter
        {
            VisibleInRequest = true
        },
        Option = new AddressGroupsOption
        {
            SeparateZoneGroups = false
        }
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetFlowComplianceStateRequest"/>.
/// </summary>
public sealed class GetFlowComplianceStateRequestExample : ApiExampleProvider<GetFlowComplianceStateRequest>
{
    /// <inheritdoc />
    public override GetFlowComplianceStateRequest GetExample() => new()
    {
        Source =
        [
            new GetFlowComplianceStateRequest.IpRangeRequest
            {
                IpNetwork = "192.0.2.0/24"
            }
        ],
        Destination =
        [
            new GetFlowComplianceStateRequest.IpRangeRequest
            {
                IpStart = "198.51.100.20",
                IpEnd = "198.51.100.29"
            }
        ],
        Service =
        [
            new GetFlowComplianceStateRequest.ServiceRangeRequest
            {
                PortStart = 443,
                PortEnd = 443,
                Protocol = "tcp"
            }
        ],
        Policies = [17]
    };
}

/// <summary>
/// Provides a typed example for <see cref="ResolveZonesForObjectsRequest"/>.
/// </summary>
public sealed class ResolveZonesForObjectsRequestExample : ApiExampleProvider<ResolveZonesForObjectsRequest>
{
    /// <inheritdoc />
    public override ResolveZonesForObjectsRequest GetExample() => new()
    {
        Objects =
        [
            new ResolveZonesForObjectsRequest.GroupObjectRequest
            {
                Name = "preview-group",
                Members =
                [
                    new ResolveZonesForObjectsRequest.LeafObjectRequest
                    {
                        Name = "branch-a",
                        Type = "network",
                        IpStart = "10.0.0.1",
                        IpEnd = "10.0.0.1"
                    },
                    new ResolveZonesForObjectsRequest.GroupObjectRequest
                    {
                        Name = "branch-b",
                        Members =
                        [
                            new ResolveZonesForObjectsRequest.LeafObjectRequest
                            {
                                Name = "leaf",
                                Type = "ip_range",
                                IpStart = "10.0.1.1",
                                IpEnd = "10.0.1.10"
                            }
                        ]
                    }
                ]
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetOwnersRequest"/>.
/// </summary>
public sealed class GetOwnersRequestExample : ApiExampleProvider<GetOwnersRequest>
{
    /// <inheritdoc />
    public override GetOwnersRequest GetExample() => new()
    {
        OwnerId = 42,
        OwnerLifeCycleStateId = 1,
        Active = true,
        Name = "Payments",
        AppIdExternal = "APP-42",
        ShowDetails = true,
        ShowOnlyActiveState = true
    };
}

/// <summary>
/// Provides a typed example for <see cref="CreateRequestResponse"/>.
/// </summary>
public sealed class CreateRequestResponseExample : ApiExampleProvider<CreateRequestResponse>
{
    /// <inheritdoc />
    public override CreateRequestResponse GetExample() => new()
    {
        Status = "created",
        RequestId = 12345
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetRequestStatusResponse"/>.
/// </summary>
public sealed class GetRequestStatusResponseExample : ApiExampleProvider<GetRequestStatusResponse>
{
    /// <inheritdoc />
    public override GetRequestStatusResponse GetExample() => new() { Status = "in_progress" };
}

/// <summary>
/// Provides a typed example for <see cref="FlowComplianceStateResponse"/>.
/// </summary>
public sealed class FlowComplianceStateResponseExample : ApiExampleProvider<FlowComplianceStateResponse>
{
    /// <inheritdoc />
    public override FlowComplianceStateResponse GetExample() => new()
    {
        Policy = new FlowComplianceStateResponse.CompliancePolicyResponse
        {
            Id = 17,
            Name = "Internet access"
        },
        Violations =
        [
            new FlowComplianceStateResponse.ComplianceViolationResponse
            {
                Id = 3,
                Type = "missing-approval"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="ComplianceDesignatedZoneResponse"/>.
/// </summary>
public sealed class ComplianceDesignatedZoneResponseExample : ApiExampleProvider<ComplianceDesignatedZoneResponse>
{
    /// <inheritdoc />
    public override ComplianceDesignatedZoneResponse GetExample() => new()
    {
        Id = 7,
        Name = "DMZ",
        Description = "Demilitarized zone",
        IpRanges =
            [
                new ComplianceDesignatedZoneIpRangeResponse
                {
                    IpStart = "10.0.0.0",
                    IpEnd = "10.0.0.255"
                }
            ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetPolicyIdsResponse"/>.
/// </summary>
public sealed class GetPolicyIdsResponseExample : ApiExampleProvider<GetPolicyIdsResponse>
{
    /// <inheritdoc />
    public override GetPolicyIdsResponse GetExample() => new()
    {
        Policies =
        [
            new PolicyIdResponse
            {
                Id = 17,
                Name = "Internet access"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="AddressObjectResponse"/>.
/// </summary>
public sealed class AddressObjectResponseExample : ApiExampleProvider<AddressObjectResponse>
{
    /// <inheritdoc />
    public override AddressObjectResponse GetExample() => new()
    {
        Id = 1001,
        Name = "app-server-1",
        Type = "host",
        IpStart = "192.0.2.10",
        IpEnd = "192.0.2.10",
        State = "active",
        ShowInRequest = true
    };
}

/// <summary>
/// Provides a typed example for <see cref="AddressGroupResponse"/>.
/// </summary>
public sealed class AddressGroupResponseExample : ApiExampleProvider<AddressGroupResponse>
{
    /// <inheritdoc />
    public override AddressGroupResponse GetExample() => new()
    {
        Id = 2001,
        Name = "app-servers",
        State = "active",
        ShowInRequest = true,
        Members =
        [
            new AddressGroupResponse.AddressGroupMemberResponse
            {
                Id = 1001,
                Name = "app-server-1"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="ServiceObjectResponse"/>.
/// </summary>
public sealed class ServiceObjectResponseExample : ApiExampleProvider<ServiceObjectResponse>
{
    /// <inheritdoc />
    public override ServiceObjectResponse GetExample() => new()
    {
        Id = 3001,
        Name = "https",
        PortStart = 443,
        PortEnd = 443,
        Protocol = "tcp",
        State = "active",
        ShowInRequest = true
    };
}

/// <summary>
/// Provides service catalog response examples for port-based and protocol-only services.
/// </summary>
public sealed class ServiceObjectResponseListExample : ApiExampleProvider<List<ServiceObjectResponse>>
{
    /// <inheritdoc />
    public override List<ServiceObjectResponse> GetExample() => new()
    {
        new ServiceObjectResponse
        {
            Id = 3001,
            Name = "https",
            PortStart = 443,
            PortEnd = 443,
            Protocol = "tcp",
            State = "active",
            ShowInRequest = true
        },
        new ServiceObjectResponse
        {
            Id = 3002,
            Name = "icmp",
            PortStart = null,
            PortEnd = null,
            Protocol = "icmp",
            State = "active",
            ShowInRequest = true
        }
    };
}

/// <summary>
/// Provides a typed example for <see cref="ServiceGroupResponse"/>.
/// </summary>
public sealed class ServiceGroupResponseExample : ApiExampleProvider<ServiceGroupResponse>
{
    /// <inheritdoc />
    public override ServiceGroupResponse GetExample() => new()
    {
        Id = 4001,
        Name = "web-services",
        State = "active",
        ShowInRequest = true,
        Members =
        [
            new ServiceGroupResponse.ServiceGroupMemberResponse
            {
                Id = 3001,
                Name = "https"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="TimeObjectResponse"/>.
/// </summary>
public sealed class TimeObjectResponseExample : ApiExampleProvider<TimeObjectResponse>
{
    /// <inheritdoc />
    public override TimeObjectResponse GetExample() => new()
    {
        Id = 5001,
        Name = "Business hours",
        StartTime = "08:00",
        EndTime = "18:00",
        State = "active",
        ShowInRequest = true
    };
}

/// <summary>
/// Provides a typed example for <see cref="AddressObjectIdResponse"/>.
/// </summary>
public sealed class AddressObjectIdResponseExample : ApiExampleProvider<AddressObjectIdResponse>
{
    /// <inheritdoc />
    public override AddressObjectIdResponse GetExample() => new()
    {
        Name = "app-server-1",
        Id = 1001
    };
}

/// <summary>
/// Provides a typed example for <see cref="ServiceObjectIdResponse"/>.
/// </summary>
public sealed class ServiceObjectIdResponseExample : ApiExampleProvider<ServiceObjectIdResponse>
{
    /// <inheritdoc />
    public override ServiceObjectIdResponse GetExample() => new()
    {
        Name = "https",
        Id = 3001
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetOwnerResponse"/>.
/// </summary>
public sealed class GetOwnerResponseExample : ApiExampleProvider<GetOwnerResponse>
{
    /// <inheritdoc />
    public override GetOwnerResponse GetExample() => new()
    {
        Id = 42,
        Name = "Payments",
        AppIdExternal = "APP-42",
        Type = "standard",
        OwnerLifecycleState = new OwnerLifecycleStateResponse
        {
            Id = 1,
            Name = "active"
        },
        OwnerResponsibles =
        [
            new OwnerResponsibleResponse
            {
                Dn = "uid=alice,ou=users,dc=example,dc=com",
                ResponsibleType = 1
            }
        ],
        IsDefault = false,
        TenantId = 1,
        RecertInterval = 365,
        LastRecertCheck = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        RecertCheckParams = "{\"scope\":\"all-rules\"}",
        Criticality = "high",
        OwnerLifecycleStateId = 1,
        Active = true,
        ImportSource = "manual",
        CommonServicePossible = false,
        LastRecertified = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc),
        LastRecertifier = 7,
        LastRecertifierDn = "uid=certifier,ou=users,dc=example,dc=com",
        NextRecertDate = new DateTime(2027, 1, 10, 9, 0, 0, DateTimeKind.Utc),
        RecertActive = true,
        DecommDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
        AdditionalInfo = new Dictionary<string, string> { ["costCenter"] = "CC-42" }
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetAuditProofCriticalChangesRequest"/>.
/// </summary>
public sealed class GetAuditProofCriticalChangesRequestExample : ApiExampleProvider<GetAuditProofCriticalChangesRequest>
{
    /// <inheritdoc />
    public override GetAuditProofCriticalChangesRequest GetExample() => new()
    {
        TicketId = 1234,
        Options = new GetAuditProofCriticalChangesOptions
        {
            Filter = new AuditProofCriticalChangeFilter
            {
                ChangeTime = null,
                ChangeUserName = null,
                ChangeContent = null
            }
        }
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetAuditProofCriticalChangesResponse"/>.
/// </summary>
public sealed class GetAuditProofCriticalChangesResponseExample : ApiExampleProvider<GetAuditProofCriticalChangesResponse>
{
    /// <inheritdoc />
    public override GetAuditProofCriticalChangesResponse GetExample() => new()
    {
        Changes =
        [
            new AuditProofCriticalChangeResponse
            {
                // Unspecified on purpose: the stored column is timezone-naive, so the endpoint emits
                // no offset and the documented example has to render the same way.
                ChangeTime = new DateTime(2026, 9, 11, 8, 11, 0, DateTimeKind.Unspecified),
                ChangeUserName = "abc",
                ChangeUserId = 42,
                ChangeContent = "Updated workflow ticket"
            }
        ]
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetTicketRequest"/>.
/// </summary>
public sealed class GetTicketRequestExample : ApiExampleProvider<GetTicketRequest>
{
    /// <inheritdoc />
    public override GetTicketRequest GetExample() => new()
    {
        TicketId = 1234,
        Options = new GetTicketOptions
        {
            Filter = new TicketTaskFilter
            {
                TaskType = "access"
            }
        }
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetTicketResponse"/>.
/// </summary>
public sealed class GetTicketResponseExample : ApiExampleProvider<GetTicketResponse>
{
    // Unspecified on purpose: the stored columns are timezone-naive, so the endpoint emits no offset.
    private static readonly DateTime kCreationDate = new(2026, 9, 11, 8, 11, 0, DateTimeKind.Unspecified);

    /// <inheritdoc />
    public override GetTicketResponse GetExample() => new()
    {
        Id = 1234,
        Title = "Allow HTTPS to application server",
        StateId = 49,
        State = "Approval",
        Status = "in_progress",
        CreationDate = kCreationDate,
        Priority = 3,
        RequesterName = "alice",
        RequesterDn = "uid=alice,ou=users,dc=example,dc=com",
        Reason = "Alice Example (alice)",
        Locked = true,
        Tasks =
        [
            new TicketTaskResponse
            {
                Id = 5678,
                TaskNumber = 1,
                Title = "HTTPS to app server",
                TaskType = "access",
                StateId = 49,
                State = "Approval",
                RequestAction = "create",
                RuleActionId = 1,
                TrackingId = 1,
                ManagementId = 3,
                ManagementName = "Checkpoint R8x",
                DeviceIds = [7],
                Locked = true,
                Elements =
                [
                    new TicketElementResponse { Id = 1, Field = "source", Action = "create", Name = "client-net", Ip = "10.0.0.0/32", IpEnd = "10.0.0.255/32" },
                    new TicketElementResponse { Id = 2, Field = "destination", Action = "create", Name = "app-server", Ip = "192.168.1.10/32", IpEnd = "192.168.1.10/32" },
                    new TicketElementResponse { Id = 3, Field = "service", Action = "create", Name = "https", Port = 443, PortEnd = 443, ProtocolId = 6 }
                ],
                Approvals =
                [
                    new TicketApprovalResponse
                    {
                        Id = 91,
                        StateId = 49,
                        State = "Approval",
                        DateOpened = kCreationDate,
                        ApproverGroup = "cn=approvers,ou=groups,dc=example,dc=com",
                        InitialApproval = true
                    }
                ],
                Owners = [new TicketOwnerResponse { Id = 12, Name = "Payments", ExtAppId = "APP-4711" }],
                Comments = [new TicketCommentResponse { Id = 300, CreationDate = kCreationDate, CreatorName = "alice", Text = "Needed for go-live" }]
            }
        ]
    };
}
