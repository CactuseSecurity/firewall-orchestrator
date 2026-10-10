using FWO.Basics;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides read-only flow catalog endpoints.
/// These endpoints are role-authorized, but they are not filtered on a modeller or owner basis.
/// </summary>
[Authorize]
[ApiController]
[Route("api/flow")]
public class FlowCatalogController : ControllerBase
{
    private static readonly RequestRootValidationSchema AddressObjectsRootSchema = RequestRootValidationSchema.ForPagedVisibleInRequest(nameof(GetAddressObjects), FlowCatalogPaging.kMaxObjectLimit);
    private static readonly RequestFilterValidationSchema AddressObjectsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetAddressObjects));
    private static readonly RequestRootValidationSchema AddressGroupsRootSchema = new(
        nameof(GetAddressGroups),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition("option", "Optional option container controlling the response shape."),
            .. FlowCatalogPaging.KeyDefinitions(FlowCatalogPaging.kMaxGroupLimit)
        ]);
    private static readonly RequestFilterValidationSchema AddressGroupsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetAddressGroups));
    private static readonly RequestRootValidationSchema ServiceObjectsRootSchema = RequestRootValidationSchema.ForPagedVisibleInRequest(nameof(GetServiceObjects), FlowCatalogPaging.kMaxObjectLimit);
    private static readonly RequestFilterValidationSchema ServiceObjectsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceObjects));
    private static readonly RequestRootValidationSchema ServiceGroupsRootSchema = RequestRootValidationSchema.ForPagedVisibleInRequest(nameof(GetServiceGroups), FlowCatalogPaging.kMaxGroupLimit);
    private static readonly RequestFilterValidationSchema ServiceGroupsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceGroups));
    private static readonly RequestRootValidationSchema TimeObjectsRootSchema = RequestRootValidationSchema.ForPagedVisibleInRequest(nameof(GetTimeObjects), FlowCatalogPaging.kMaxObjectLimit);
    private static readonly RequestFilterValidationSchema TimeObjectsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetTimeObjects));
    private static readonly RequestRootValidationSchema ServiceObjectIdRootSchema = new(
        nameof(GetServiceObjectId),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition(
                "portStart",
                "Required inclusive starting port. Send both port bounds as null only for an unambiguous portless service; otherwise provide both."),
            new RequestKeyDefinition(
                "portEnd",
                "Required inclusive ending port. Send both port bounds as null only for an unambiguous portless service; otherwise provide both."),
            new RequestKeyDefinition("protocol", "Protocol name or protocol id for the service object lookup.")
        ]);
    private static readonly RequestRootValidationSchema TimeObjectIdRootSchema = new(
        nameof(GetTimeObjectId),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition("startTime", "Start time for the time object lookup."),
            new RequestKeyDefinition("endTime", "End time for the time object lookup.")
        ]);
    private static readonly RequestRootValidationSchema AddressObjectIdRootSchema = new(
        nameof(GetAddressObjectId),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition("ipStart", "Start IP address for the address object lookup."),
            new RequestKeyDefinition("ipEnd", "End IP address for the address object lookup.")
        ]);
    private static readonly RequestFilterValidationSchema ServiceObjectIdFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceObjectId));
    private static readonly RequestFilterValidationSchema TimeObjectIdFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetTimeObjectId));
    private static readonly RequestFilterValidationSchema AddressObjectIdFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetAddressObjectId));
    private static readonly RequestRootValidationSchema ResolveFlowGroupsRootSchema = new(
        nameof(ResolveGroupMembers),
        [
            new RequestKeyDefinition("networkGroupIds", "Network Flow group IDs to resolve."),
            new RequestKeyDefinition("networkGroupNames", "Network Flow group names to resolve."),
            new RequestKeyDefinition("serviceGroupIds", "Service Flow group IDs to resolve."),
            new RequestKeyDefinition("serviceGroupNames", "Service Flow group names to resolve.")
        ]);

    private readonly FlowCatalogService flowCatalogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowCatalogController"/> class.
    /// </summary>
    /// <param name="flowCatalogService">The flow catalog service.</param>
    public FlowCatalogController(FlowCatalogService flowCatalogService)
    {
        this.flowCatalogService = flowCatalogService;
    }

    /// <summary>
    /// Returns address objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Returns one page of at most 1000 items (root keys <c>limit</c> and <c>offset</c>, ordered by name and id); the
    /// <c>X-Has-More</c> response header is <c>true</c> when further items follow.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressObjects")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    public async Task<ActionResult<List<AddressObjectResponse>>> GetAddressObjects([FromBody] GetAddressObjectsRequest request)
    {
        if (!TryValidatePagedRequest(request, AddressObjectsRootSchema, AddressObjectsFilterSchema, FlowCatalogPaging.kMaxObjectLimit, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return PageResult(await flowCatalogService.GetAddressObjectsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxObjectLimit), request.Offset));
    }

    /// <summary>
    /// Returns address groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Returns one page of at most 250 groups (root keys <c>limit</c> and <c>offset</c>, ordered by name and id); the
    /// <c>X-Has-More</c> response header is <c>true</c> when further groups follow.
    /// With separated zone groups the page is taken before the groups are separated.
    /// With 'option.separateZoneGroups' set to true the result is a
    /// <see cref="SeparatedAddressGroupsResponse"/> holding the zone groups separately;
    /// otherwise a flat JSON array of all groups is returned.
    /// Zone groups are recognized by the zone name patterns configured in the general flow settings.
    /// The documented response schema and the request example show the default flat array;
    /// the separated shape is not part of the generated schema.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressGroups")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    [ProducesResponseType(typeof(List<AddressGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!TryValidatePagedRequest(request, AddressGroupsRootSchema, AddressGroupsFilterSchema, FlowCatalogPaging.kMaxGroupLimit, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (!AddressGroupsOptionValidator.TryValidate(request.Option, out ActionResult? optionErrorResult))
        {
            return optionErrorResult!;
        }

        if (request.Option?.SeparateZoneGroups == true)
        {
            (SeparatedAddressGroupsResponse separatedGroups, bool hasMore) =
                await flowCatalogService.GetSeparatedAddressGroupsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxGroupLimit), request.Offset);
            ListPaging.SetHasMoreHeader(HttpContext, hasMore);
            return Ok(separatedGroups);
        }

        return PageResult(await flowCatalogService.GetAddressGroupsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxGroupLimit), request.Offset));
    }

    /// <summary>
    /// Returns service objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Returns one page of at most 1000 items (root keys <c>limit</c> and <c>offset</c>, ordered by name and id); the
    /// <c>X-Has-More</c> response header is <c>true</c> when further items follow.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjects")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    public async Task<ActionResult<List<ServiceObjectResponse>>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!TryValidatePagedRequest(request, ServiceObjectsRootSchema, ServiceObjectsFilterSchema, FlowCatalogPaging.kMaxObjectLimit, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return PageResult(await flowCatalogService.GetServiceObjectsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxObjectLimit), request.Offset));
    }

    /// <summary>
    /// Returns service groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Returns one page of at most 250 groups (root keys <c>limit</c> and <c>offset</c>, ordered by name and id); the
    /// <c>X-Has-More</c> response header is <c>true</c> when further groups follow.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceGroups")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    public async Task<ActionResult<List<ServiceGroupResponse>>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!TryValidatePagedRequest(request, ServiceGroupsRootSchema, ServiceGroupsFilterSchema, FlowCatalogPaging.kMaxGroupLimit, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return PageResult(await flowCatalogService.GetServiceGroupsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxGroupLimit), request.Offset));
    }

    /// <summary>
    /// Resolves the supplied request-visible Flow groups and returns their active members.
    /// Only explicitly requested IDs or names are resolved.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}, {Roles.Recertifier}, {Roles.WorkflowRolesList}")]
    [HttpPost("resolveGroupMembers")]
    public async Task<ActionResult<FlowGroupResolutionResult>> ResolveGroupMembers([FromBody] ResolveFlowGroupsRequest? request)
    {
        request ??= new ResolveFlowGroupsRequest();
        if (!RequestRootValidator.TryValidate(request, ResolveFlowGroupsRootSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }
        request.NetworkGroupIds ??= [];
        request.NetworkGroupNames ??= [];
        request.ServiceGroupIds ??= [];
        request.ServiceGroupNames ??= [];
        if (request.NetworkGroupIds.Count + request.NetworkGroupNames.Count
            + request.ServiceGroupIds.Count + request.ServiceGroupNames.Count > FlowGroupResolutionParameters.MaxSelectors)
        {
            return BadRequest($"At most {FlowGroupResolutionParameters.MaxSelectors} group selectors are allowed.");
        }

        if (request.NetworkGroupNames.Any(string.IsNullOrWhiteSpace)
            || request.ServiceGroupNames.Any(string.IsNullOrWhiteSpace))
        {
            return BadRequest("Group names must not be empty.");
        }

        return Ok(await flowCatalogService.ResolveFlowGroupMembersAsync(new FlowGroupResolutionParameters
        {
            NetworkGroupIds = request.NetworkGroupIds,
            NetworkGroupNames = request.NetworkGroupNames,
            ServiceGroupIds = request.ServiceGroupIds,
            ServiceGroupNames = request.ServiceGroupNames
        }));
    }

    /// <summary>
    /// Returns time objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Returns one page of at most 1000 items (root keys <c>limit</c> and <c>offset</c>, ordered by name and id); the
    /// <c>X-Has-More</c> response header is <c>true</c> when further items follow.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTimeObjects")]
    [EnableRateLimiting(ApiRateLimiting.kExpensivePolicy)]
    [RequestSizeLimit(ApiRateLimiting.kMaxExpensiveRequestBodyBytes)]
    public async Task<ActionResult<List<TimeObjectResponse>>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!TryValidatePagedRequest(request, TimeObjectsRootSchema, TimeObjectsFilterSchema, FlowCatalogPaging.kMaxObjectLimit, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return PageResult(await flowCatalogService.GetTimeObjectsAsync(request.Filter?.VisibleInRequest, FlowCatalogPaging.GetPageSize(request, FlowCatalogPaging.kMaxObjectLimit), request.Offset));
    }

    /// <summary>
    /// Resolves a service object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// It is not intended to identify custom protocol-only services because their technical definitions are ambiguous:
    /// when more than one service object matches (several portless services of the same protocol), the lookup returns
    /// 409 with the candidates instead of an id. Services with ports and the canonical ANY service are unique.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjectId")]
    [ProducesResponseType(typeof(ServiceObjectIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AmbiguousServiceObjectIdResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServiceObjectIdResponse>> GetServiceObjectId([FromBody] GetServiceObjectIdRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceObjectIdRootSchema, ServiceObjectIdFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.Protocol))
        {
            return BadRequest("'protocol' is required.");
        }

        if (request.PortStart.HasValue != request.PortEnd.HasValue)
        {
            return BadRequest("'portStart' and 'portEnd' must both be provided or both be null.");
        }

        if (request.PortStart.HasValue
            && !FlowComplianceRequestValidator.TryValidateServiceRange(request.PortStart.Value, request.PortEnd!.Value, "service", 0, out string? serviceErrorMessage))
        {
            return BadRequest(serviceErrorMessage);
        }

        List<ServiceObjectIdResponse> matches = await flowCatalogService.FindServiceObjectIdsAsync(
            request.Protocol, request.PortStart, request.PortEnd, request.Filter?.VisibleInRequest);
        if (matches.Count > 1)
        {
            return Conflict(new AmbiguousServiceObjectIdResponse
            {
                Message = "More than one service object matches the lookup; reference one of the candidates by id.",
                Candidates = matches
            });
        }

        return Ok(matches.Count == 1 ? matches[0] : new ServiceObjectIdResponse());
    }

    /// <summary>
    /// Resolves a time object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTimeObjectId")]
    public async Task<ActionResult<TimeObjectIdResponse>> GetTimeObjectId([FromBody] GetTimeObjectIdRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, TimeObjectIdRootSchema, TimeObjectIdFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (!request.StartTime.HasValue && !request.EndTime.HasValue)
        {
            return BadRequest("At least one of 'startTime' or 'endTime' is required.");
        }

        if (request.StartTime.HasValue && request.EndTime.HasValue && request.StartTime > request.EndTime)
        {
            return BadRequest("'startTime' must be <= 'endTime'.");
        }

        return Ok(await flowCatalogService.GetTimeObjectIdAsync(request.StartTime, request.EndTime, request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves an address object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// IPv4 and IPv6 ranges are accepted through ipStart and ipEnd.
    /// Optional host masks (/32 and /128) are ignored; all other masks are rejected.
    /// IPv6 values that only re-encode an IPv4 address are rejected as well, i.e. the IPv4-mapped form
    /// (::ffff:a.b.c.d) and the deprecated IPv4-compatible form (::a.b.c.d); use the IPv4 notation instead.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressObjectId")]
    public async Task<ActionResult<AddressObjectIdResponse>> GetAddressObjectId([FromBody] GetAddressObjectIdRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressObjectIdRootSchema, AddressObjectIdFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.IpStart) || string.IsNullOrWhiteSpace(request.IpEnd))
        {
            return BadRequest("'ipStart' and 'ipEnd' are required.");
        }

        if (!FlowComplianceRequestValidator.TryValidateAndNormalizeIpRange(
            request.IpStart,
            request.IpEnd,
            "address",
            0,
            out string normalizedIpStart,
            out string normalizedIpEnd,
            out string? addressErrorMessage))
        {
            return BadRequest(addressErrorMessage);
        }

        request.IpStart = normalizedIpStart;
        request.IpEnd = normalizedIpEnd;
        return Ok(await flowCatalogService.GetAddressObjectIdAsync(request.IpStart, request.IpEnd, request.Filter?.VisibleInRequest));
    }

    private static bool TryValidatePagedRequest<TRequest>(
        TRequest request,
        RequestRootValidationSchema rootSchema,
        RequestFilterValidationSchema filterSchema,
        int maxLimit,
        out ActionResult? errorResult)
        where TRequest : IVisibleInRequestFilterRequest, IPagedListRequest
    {
        if (!TryValidateVisibleInRequestRequest(request, rootSchema, filterSchema, out errorResult))
        {
            return false;
        }

        return FlowCatalogPaging.TryValidate(request, maxLimit, out errorResult);
    }

    /// <summary>
    /// Returns the items of a page and tells through the <c>X-Has-More</c> response header whether further items follow.
    /// </summary>
    private OkObjectResult PageResult<T>(ListPage<T> page)
    {
        ListPaging.SetHasMoreHeader(HttpContext, page.HasMore);
        return Ok(page.Items);
    }

    private static bool TryValidateVisibleInRequestRequest<TRequest>(
        TRequest request,
        RequestRootValidationSchema rootSchema,
        RequestFilterValidationSchema filterSchema,
        out ActionResult? errorResult)
        where TRequest : IVisibleInRequestFilterRequest
    {
        if (!RequestRootValidator.TryValidate(request, rootSchema, out errorResult))
        {
            return false;
        }

        return VisibleInRequestFilterValidator.TryValidate(request, filterSchema, out errorResult);
    }
}
