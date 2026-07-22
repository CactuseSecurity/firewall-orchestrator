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
        services.AddSingleton<IApiExampleProvider, GenerateAddressObjectNameRequestExample>();
        services.AddSingleton<IApiExampleProvider, GenerateServiceObjectNameRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetNetObjectValidityRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetNetGroupValidityRequestItemExample>();
        services.AddSingleton<IApiExampleProvider, CreateRequestRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetRequestStatusRequestExample>();
        services.AddSingleton<IApiExampleProvider, VisibleInRequestFilterExample>();
        services.AddSingleton<IApiExampleProvider, GetFlowComplianceStateRequestExample>();
        services.AddSingleton<IApiExampleProvider, GetOwnersRequestExample>();
        services.AddSingleton<IApiExampleProvider, GenerateAddressObjectNameResponseExample>();
        services.AddSingleton<IApiExampleProvider, GenerateServiceObjectNameResponseExample>();
        services.AddSingleton<IApiExampleProvider, NetObjectValidityResponseExample>();
        services.AddSingleton<IApiExampleProvider, NetGroupValidityResponseExample>();
        services.AddSingleton<IApiExampleProvider, CreateRequestResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetRequestStatusResponseExample>();
        services.AddSingleton<IApiExampleProvider, FlowComplianceStateResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetPolicyIdsResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressGroupResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceGroupResponseExample>();
        services.AddSingleton<IApiExampleProvider, TimeObjectResponseExample>();
        services.AddSingleton<IApiExampleProvider, AddressObjectIdResponseExample>();
        services.AddSingleton<IApiExampleProvider, ServiceObjectIdResponseExample>();
        services.AddSingleton<IApiExampleProvider, GetOwnerResponseExample>();
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
/// Provides a typed example for <see cref="GenerateAddressObjectNameRequest"/>.
/// </summary>
public sealed class GenerateAddressObjectNameRequestExample : ApiExampleProvider<GenerateAddressObjectNameRequest>
{
    /// <inheritdoc />
    public override GenerateAddressObjectNameRequest GetExample() => new()
    {
        IpStart = "192.0.2.10",
        IpEnd = "192.0.2.10",
        NetMask = 32
    };
}

/// <summary>
/// Provides a typed example for <see cref="GenerateServiceObjectNameRequest"/>.
/// </summary>
public sealed class GenerateServiceObjectNameRequestExample : ApiExampleProvider<GenerateServiceObjectNameRequest>
{
    /// <inheritdoc />
    public override GenerateServiceObjectNameRequest GetExample() => new()
    {
        PortStart = 443,
        PortEnd = 443,
        Protocol = "tcp",
        Typ = "service"
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetNetObjectValidityRequest"/>.
/// </summary>
public sealed class GetNetObjectValidityRequestExample : ApiExampleProvider<GetNetObjectValidityRequest>
{
    /// <inheritdoc />
    public override GetNetObjectValidityRequest GetExample() => new()
    {
        IpAddress = "192.0.2.10",
        NetMask = 32,
        MinPrefixLength = 24
    };
}

/// <summary>
/// Provides a typed example for <see cref="GetNetGroupValidityRequestItem"/>.
/// </summary>
public sealed class GetNetGroupValidityRequestItemExample : ApiExampleProvider<GetNetGroupValidityRequestItem>
{
    /// <inheritdoc />
    public override GetNetGroupValidityRequestItem GetExample() => new()
    {
        IpStart = "192.0.2.10",
        IpEnd = "192.0.2.20"
    };
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
                IpStart = "192.0.2.10",
                IpEnd = "192.0.2.10"
            }
        ],
        Destination =
        [
            new GetFlowComplianceStateRequest.IpRangeRequest
            {
                IpStart = "198.51.100.20",
                IpEnd = "198.51.100.20"
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
/// Provides a typed example for <see cref="GenerateAddressObjectNameResponse"/>.
/// </summary>
public sealed class GenerateAddressObjectNameResponseExample : ApiExampleProvider<GenerateAddressObjectNameResponse>
{
    /// <inheritdoc />
    public override GenerateAddressObjectNameResponse GetExample() => new() { Name = "host-192-0-2-10" };
}

/// <summary>
/// Provides a typed example for <see cref="GenerateServiceObjectNameResponse"/>.
/// </summary>
public sealed class GenerateServiceObjectNameResponseExample : ApiExampleProvider<GenerateServiceObjectNameResponse>
{
    /// <inheritdoc />
    public override GenerateServiceObjectNameResponse GetExample() => new() { Name = "tcp-443" };
}

/// <summary>
/// Provides a typed example for <see cref="NetObjectValidityResponse"/>.
/// </summary>
public sealed class NetObjectValidityResponseExample : ApiExampleProvider<NetObjectValidityResponse>
{
    /// <inheritdoc />
    public override NetObjectValidityResponse GetExample() => new() { IsValid = true };
}

/// <summary>
/// Provides a typed example for <see cref="NetGroupValidityResponse"/>.
/// </summary>
public sealed class NetGroupValidityResponseExample : ApiExampleProvider<NetGroupValidityResponse>
{
    /// <inheritdoc />
    public override NetGroupValidityResponse GetExample() => new() { IsValid = true };
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
