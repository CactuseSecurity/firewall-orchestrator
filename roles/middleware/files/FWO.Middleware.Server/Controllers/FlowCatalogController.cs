using FWO.Basics;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private static readonly RequestRootValidationSchema AddressObjectsRootSchema = RequestRootValidationSchema.ForVisibleInRequest(nameof(GetAddressObjects));
    private static readonly RequestFilterValidationSchema AddressObjectsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetAddressObjects));
    private static readonly RequestRootValidationSchema AddressGroupsRootSchema = new(
        nameof(GetAddressGroups),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition("option", "Optional option container controlling the response shape.")
        ]);
    private static readonly RequestFilterValidationSchema AddressGroupsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetAddressGroups));
    private static readonly RequestRootValidationSchema ServiceObjectsRootSchema = RequestRootValidationSchema.ForVisibleInRequest(nameof(GetServiceObjects));
    private static readonly RequestFilterValidationSchema ServiceObjectsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceObjects));
    private static readonly RequestRootValidationSchema ServiceGroupsRootSchema = RequestRootValidationSchema.ForVisibleInRequest(nameof(GetServiceGroups));
    private static readonly RequestFilterValidationSchema ServiceGroupsFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceGroups));
    private static readonly RequestRootValidationSchema TimeObjectsRootSchema = RequestRootValidationSchema.ForVisibleInRequest(nameof(GetTimeObjects));
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
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressObjects")]
    public async Task<ActionResult<List<AddressObjectResponse>>> GetAddressObjects([FromBody] GetAddressObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressObjectsRootSchema, AddressObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressObjectsAsync(request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns address groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// With 'option.separateZoneGroups' set to true the result is a
    /// <see cref="SeparatedAddressGroupsResponse"/> holding the zone groups separately;
    /// otherwise a flat JSON array of all groups is returned.
    /// Zone groups are recognized by the zone name patterns configured in the general flow settings.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressGroups")]
    [ProducesResponseType(typeof(List<AddressGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressGroupsRootSchema, AddressGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (!AddressGroupsOptionValidator.TryValidate(request.Option, out ActionResult? optionErrorResult))
        {
            return optionErrorResult!;
        }

        if (request.Option?.SeparateZoneGroups == true)
        {
            return Ok(await flowCatalogService.GetSeparatedAddressGroupsAsync(request.Filter?.VisibleInRequest));
        }

        return Ok(await flowCatalogService.GetAddressGroupsAsync(request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns service objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjects")]
    public async Task<ActionResult<List<ServiceObjectResponse>>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceObjectsRootSchema, ServiceObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceObjectsAsync(request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns service groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceGroups")]
    public async Task<ActionResult<List<ServiceGroupResponse>>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceGroupsRootSchema, ServiceGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceGroupsAsync(request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns time objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTimeObjects")]
    public async Task<ActionResult<List<TimeObjectResponse>>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, TimeObjectsRootSchema, TimeObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetTimeObjectsAsync(request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves a service object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// It is not intended to identify custom protocol-only services because their technical definitions are ambiguous.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjectId")]
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

        return Ok(await flowCatalogService.GetServiceObjectIdAsync(request.Protocol, request.PortStart, request.PortEnd, request.Filter?.VisibleInRequest));
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
    /// Optional /32 masks on ipStart and ipEnd are ignored; all other masks are rejected.
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
