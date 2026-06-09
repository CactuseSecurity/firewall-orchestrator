using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FlowController : ControllerBase
{
    private readonly FlowCatalogService flowCatalogService;

    public FlowController(FlowCatalogService flowCatalogService)
    {
        this.flowCatalogService = flowCatalogService;
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
    #endregion

    [HttpPost("getAddressObjects")]
    public async Task<ActionResult<List<AddressObjectResponse>>> GetAddressObjects([FromBody] GetAddressObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressObjectsRootSchema, AddressObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressObjectsAsync(request.Filter?.VisibleInRequest));
    }

    [HttpPost("getAddressGroups")]
    public async Task<ActionResult<List<AddressGroupResponse>>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressGroupsRootSchema, AddressGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressGroupsAsync(request.Filter?.VisibleInRequest));
    }

    [HttpPost("getServiceObjects")]
    public async Task<ActionResult<List<ServiceObjectResponse>>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceObjectsRootSchema, ServiceObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceObjectsAsync(request.Filter?.VisibleInRequest));
    }

    [HttpPost("getServiceGroups")]
    public async Task<ActionResult<List<ServiceGroupResponse>>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceGroupsRootSchema, ServiceGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceGroupsAsync(request.Filter?.VisibleInRequest));
    }

    [HttpPost("getTimeObjects")]
    public async Task<ActionResult<List<TimeObjectResponse>>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, TimeObjectsRootSchema, TimeObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetTimeObjectsAsync(request.Filter?.VisibleInRequest));
    }

    [HttpPost("getFlowComplianceState")]
    public ActionResult<List<FlowComplianceStateResponse>> GetFlowComplianceState([FromBody] GetFlowComplianceStateRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getPolicyIds")]
    public ActionResult<List<PolicyIdResponse>> GetPolicyIds([FromBody] GetPolicyIdsRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceObjectId")]
    public ActionResult<ServiceObjectIdResponse> GetServiceObjectId([FromBody] GetServiceObjectIdRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getAddressObjectId")]
    public ActionResult<AddressObjectIdResponse> GetAddressObjectId([FromBody] GetAddressObjectIdRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("generateAddressObjectName")]
    public ActionResult<GenerateAddressObjectNameResponse> GenerateAddressObjectName([FromBody] GenerateAddressObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("generateServiceObjectName")]
    public ActionResult<GenerateServiceObjectNameResponse> GenerateServiceObjectName([FromBody] GenerateServiceObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getNetObjectValidity")]
    public ActionResult<NetObjectValidityResponse> GetNetObjectValidity([FromBody] GetNetObjectValidityRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getNetGroupValidity")]
    public ActionResult<NetGroupValidityResponse> GetNetGroupValidity([FromBody] List<GetNetGroupValidityRequestItem> request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("createRequest")]
    public ActionResult<CreateRequestResponse> CreateRequest([FromBody] CreateRequestRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

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
