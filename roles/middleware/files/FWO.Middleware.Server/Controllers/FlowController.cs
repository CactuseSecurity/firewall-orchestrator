using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FlowController : ControllerBase
{
    [HttpPost("getAddressObjects")]
    public ActionResult<List<AddressObjectResponse>> GetAddressObjects([FromBody] GetAddressObjectsRequest request)
    {
        if (!VisibleInRequestFilterValidator.TryValidate(request, nameof(GetAddressObjects), out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getAddressGroups")]
    public ActionResult<List<AddressGroupResponse>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!VisibleInRequestFilterValidator.TryValidate(request, nameof(GetAddressGroups), out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceObjects")]
    public ActionResult<List<ServiceObjectResponse>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!VisibleInRequestFilterValidator.TryValidate(request, nameof(GetServiceObjects), out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceGroups")]
    public ActionResult<List<ServiceGroupResponse>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!VisibleInRequestFilterValidator.TryValidate(request, nameof(GetServiceGroups), out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getTimeObjects")]
    public ActionResult<List<TimeObjectResponse>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!VisibleInRequestFilterValidator.TryValidate(request, nameof(GetTimeObjects), out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
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
}
