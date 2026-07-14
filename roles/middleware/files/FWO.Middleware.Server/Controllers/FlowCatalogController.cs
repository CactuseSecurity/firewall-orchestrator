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
    private static readonly RequestRootValidationSchema AddressGroupsRootSchema = RequestRootValidationSchema.ForVisibleInRequest(nameof(GetAddressGroups));
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
            new RequestKeyDefinition("portStart", "Start port for the service object lookup."),
            new RequestKeyDefinition("portEnd", "End port for the service object lookup."),
            new RequestKeyDefinition("protocol", "Protocol name or protocol id for the service object lookup.")
        ]);
    private static readonly RequestRootValidationSchema AddressObjectIdRootSchema = new(
        nameof(GetAddressObjectId),
        [
            new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
            new RequestKeyDefinition("ipStart", "Start IP address for the address object lookup."),
            new RequestKeyDefinition("ipEnd", "End IP address for the address object lookup.")
        ]);
    private static readonly RequestFilterValidationSchema ServiceObjectIdFilterSchema = RequestFilterValidationSchema.ForVisibleInRequest(nameof(GetServiceObjectId));
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
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressGroups")]
    public async Task<ActionResult<List<AddressGroupResponse>>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressGroupsRootSchema, AddressGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
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

        if (!FlowComplianceRequestValidator.TryValidateServiceRange(request.PortStart, request.PortEnd, "service", 0, out string? serviceErrorMessage))
        {
            return BadRequest(serviceErrorMessage);
        }

        return Ok(await flowCatalogService.GetServiceObjectIdAsync(request.Protocol, request.PortStart, request.PortEnd, request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves an address object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
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

        if (!FlowComplianceRequestValidator.TryValidateIpRange(request.IpStart, request.IpEnd, "address", 0, out string? addressErrorMessage))
        {
            return BadRequest(addressErrorMessage);
        }

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
