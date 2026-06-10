using FWO.Basics;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides flow catalog, compliance, and request endpoints.
/// </summary>
[Authorize(Roles = $"{Roles.Admin}")]
[ApiController]
[Route("api/[controller]")]
public class FlowController : ControllerBase
{
    private readonly FlowCatalogService flowCatalogService;
    private readonly FlowComplianceService flowComplianceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowController"/> class.
    /// </summary>
    /// <param name="flowCatalogService">The flow catalog service.</param>
    /// <param name="flowComplianceService">The flow compliance service.</param>
    public FlowController(FlowCatalogService flowCatalogService, FlowComplianceService flowComplianceService)
    {
        this.flowCatalogService = flowCatalogService;
        this.flowComplianceService = flowComplianceService;
    }

    #region Schemas
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
    #endregion

    /// <summary>
    /// Returns address objects for the requested visibility filter.
    /// </summary>
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
    /// Returns address groups for the requested visibility filter.
    /// </summary>
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
    /// Returns service objects for the requested visibility filter.
    /// </summary>
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
    /// Returns service groups for the requested visibility filter.
    /// </summary>
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
    /// Returns time objects for the requested visibility filter.
    /// </summary>
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
    /// Returns the compliance state for the requested flows.
    /// </summary>
    [HttpPost("getFlowComplianceState")]
    public async Task<ActionResult<List<FlowComplianceStateResponse>>> GetFlowComplianceState([FromBody] GetFlowComplianceStateRequest request)
    {
        if (!FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowComplianceService.GetFlowComplianceStateAsync(request));
    }

    /// <summary>
    /// Returns the policy identifiers for the current dataset.
    /// </summary>
    [HttpPost("getPolicyIds")]
    public async Task<ActionResult<List<PolicyIdResponse>>> GetPolicyIds([FromBody] GetPolicyIdsRequest request)
    {
        if (!FlowComplianceRequestValidator.TryValidatePolicyIds(request, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowComplianceService.GetPolicyIdsAsync());
    }

    /// <summary>
    /// Resolves a service object identifier from the supplied lookup request.
    /// </summary>
    [HttpPost("getServiceObjectId")]
    public async Task<ActionResult<ServiceObjectIdResponse>> GetServiceObjectId([FromBody] GetServiceObjectIdRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceObjectIdRootSchema, ServiceObjectIdFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceObjectIdAsync(request.Protocol, request.PortStart, request.PortEnd, request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves an address object identifier from the supplied lookup request.
    /// </summary>
    [HttpPost("getAddressObjectId")]
    public async Task<ActionResult<AddressObjectIdResponse>> GetAddressObjectId([FromBody] GetAddressObjectIdRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressObjectIdRootSchema, AddressObjectIdFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressObjectIdAsync(request.IpStart, request.IpEnd, request.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Generates an address object name.
    /// </summary>
    [HttpPost("generateAddressObjectName")]
    public ActionResult<GenerateAddressObjectNameResponse> GenerateAddressObjectName([FromBody] GenerateAddressObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Generates a service object name.
    /// </summary>
    [HttpPost("generateServiceObjectName")]
    public ActionResult<GenerateServiceObjectNameResponse> GenerateServiceObjectName([FromBody] GenerateServiceObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Checks whether a network object definition is valid.
    /// </summary>
    [HttpPost("getNetObjectValidity")]
    public ActionResult<NetObjectValidityResponse> GetNetObjectValidity([FromBody] GetNetObjectValidityRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Checks whether a network group definition is valid.
    /// </summary>
    [HttpPost("getNetGroupValidity")]
    public ActionResult<NetGroupValidityResponse> GetNetGroupValidity([FromBody] List<GetNetGroupValidityRequestItem> request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Creates a new request.
    /// </summary>
    [HttpPost("createRequest")]
    public ActionResult<CreateRequestResponse> CreateRequest([FromBody] CreateRequestRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Returns the status of an existing request.
    /// </summary>
    [HttpPost("getRequestStatus")]
    public ActionResult<GetRequestStatusResponse> GetRequestStatus([FromBody] GetRequestStatusRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
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
