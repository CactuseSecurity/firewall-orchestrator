using System.Net;
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
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getAddressGroups")]
    public ActionResult<List<AddressGroupResponse>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceObjects")]
    public ActionResult<List<ServiceObjectResponse>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceGroups")]
    public ActionResult<List<ServiceGroupResponse>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getTimeObjects")]
    public ActionResult<List<TimeObjectResponse>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getFlowComplianceState")]
    public ActionResult<List<FlowComplianceStateResponse>> GetFlowComplianceState([FromBody] GetFlowComplianceStateRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidateIpRanges(request.Source, "source", out string? sourceError))
        {
            return BadRequest(sourceError);
        }

        if (!TryValidateIpRanges(request.Destination, "destination", out string? destinationError))
        {
            return BadRequest(destinationError);
        }

        if (!TryValidateServiceRanges(request.Service, out string? serviceError))
        {
            return BadRequest(serviceError);
        }

        if (request.Policies.Count == 0)
        {
            return BadRequest("The policies array must contain at least one policy id.");
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getPolicyIds")]
    public ActionResult<List<PolicyIdResponse>> GetPolicyIds([FromBody] GetPolicyIdsRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getServiceObjectId")]
    public ActionResult<ServiceObjectIdResponse> GetServiceObjectId([FromBody] GetServiceObjectIdRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidatePortRange(request.PortStart, request.PortEnd, out string? portError))
        {
            return BadRequest(portError);
        }

        if (!TryValidateProtocol(request.Protocol, out string? protocolError))
        {
            return BadRequest(protocolError);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getAddressObjectId")]
    public ActionResult<AddressObjectIdResponse> GetAddressObjectId([FromBody] GetAddressObjectIdRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidateIpRange(request.IpStart, request.IpEnd, out string? ipError))
        {
            return BadRequest(ipError);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("generateAddressObjectName")]
    public ActionResult<GenerateAddressObjectNameResponse> GenerateAddressObjectName([FromBody] GenerateAddressObjectNameRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidateIpRange(request.IpStart, request.IpEnd, out string? ipError))
        {
            return BadRequest(ipError);
        }

        if (!TryValidateNetMask(request.NetMask, out string? netMaskError))
        {
            return BadRequest(netMaskError);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("generateServiceObjectName")]
    public ActionResult<GenerateServiceObjectNameResponse> GenerateServiceObjectName([FromBody] GenerateServiceObjectNameRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidatePortRange(request.PortStart, request.PortEnd, out string? portError))
        {
            return BadRequest(portError);
        }

        if (!TryValidateProtocol(request.Protocol, out string? protocolError))
        {
            return BadRequest(protocolError);
        }

        if (string.IsNullOrWhiteSpace(request.Typ))
        {
            return BadRequest("The field typ must not be empty.");
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getNetObjectValidity")]
    public ActionResult<NetObjectValidityResponse> GetNetObjectValidity([FromBody] GetNetObjectValidityRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryValidateIpAddress(request.IpAddress, out string? ipError))
        {
            return BadRequest(ipError);
        }

        if (!TryValidateNetMask(request.NetMask, out string? netMaskError))
        {
            return BadRequest(netMaskError);
        }

        if (!TryValidateMinPrefixLength(request.MinPrefixLength, out string? prefixError))
        {
            return BadRequest(prefixError);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getNetGroupValidity")]
    public ActionResult<NetGroupValidityResponse> GetNetGroupValidity([FromBody] List<GetNetGroupValidityRequestItem> request)
    {
        if (request is null || request.Count == 0)
        {
            return BadRequest("The request body must contain at least one network range.");
        }

        if (!TryValidateIpRanges(request, "request", out string? rangeError))
        {
            return BadRequest(rangeError);
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("createRequest")]
    public ActionResult<CreateRequestResponse> CreateRequest([FromBody] CreateRequestRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestorName))
        {
            return BadRequest("The field requestorName must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestorId))
        {
            return BadRequest("The field requestorId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleContactName))
        {
            return BadRequest("The field ruleContactName must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.RuleContactId))
        {
            return BadRequest("The field ruleContactId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("The field title must not be empty.");
        }

        if (request.Rules.Count == 0)
        {
            return BadRequest("The rules array must contain at least one rule.");
        }

        foreach (CreateRequestRequest.CreateRequestRuleRequest rule in request.Rules)
        {
            if (!TryValidateRule(rule, out string? ruleError))
            {
                return BadRequest(ruleError);
            }
        }

        foreach (CreateRequestRequest.CreateAddressObjectRequest addressObject in request.AddressObjects)
        {
            if (!TryValidateAddressObject(addressObject, out string? addressError))
            {
                return BadRequest(addressError);
            }
        }

        foreach (CreateRequestRequest.CreateAddressGroupRequest addressGroup in request.AddressGroups)
        {
            if (string.IsNullOrWhiteSpace(addressGroup.Name))
            {
                return BadRequest("Address group names must not be empty.");
            }
        }

        foreach (CreateRequestRequest.CreateServiceObjectRequest serviceObject in request.ServiceObjects)
        {
            if (!TryValidateServiceObject(serviceObject, out string? serviceError))
            {
                return BadRequest(serviceError);
            }
        }

        foreach (CreateRequestRequest.CreateServiceGroupRequest serviceGroup in request.ServiceGroups)
        {
            if (string.IsNullOrWhiteSpace(serviceGroup.Name))
            {
                return BadRequest("Service group names must not be empty.");
            }
        }

        foreach (CreateRequestRequest.CreateTimeObjectRequest timeObject in request.TimeObjects)
        {
            if (string.IsNullOrWhiteSpace(timeObject.Name))
            {
                return BadRequest("Time object names must not be empty.");
            }
        }

        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpPost("getRequestStatus")]
    public ActionResult<GetRequestStatusResponse> GetRequestStatus([FromBody] GetRequestStatusRequest request)
    {
        return request is null
            ? BadRequest("Request body is required.")
            : StatusCode(StatusCodes.Status501NotImplemented);
    }

    private static bool TryValidateIpAddress(string? value, out string? error)
    {
        if (!string.IsNullOrWhiteSpace(value) && IPAddress.TryParse(value, out _))
        {
            error = null;
            return true;
        }

        error = "The IP address must be a valid IP address.";
        return false;
    }

    private static bool TryValidateIpRange(string? ipStart, string? ipEnd, out string? error)
    {
        if (!TryValidateIpAddress(ipStart, out error))
        {
            error = "The field ipStart must be a valid IP address.";
            return false;
        }

        if (!TryValidateIpAddress(ipEnd, out error))
        {
            error = "The field ipEnd must be a valid IP address.";
            return false;
        }

        if (IPAddress.TryParse(ipStart, out IPAddress? startIp) &&
            IPAddress.TryParse(ipEnd, out IPAddress? endIp) &&
            CompareIpAddresses(startIp, endIp) > 0)
        {
            error = "The field ipStart must not be greater than ipEnd.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateIpRanges(IEnumerable<GetFlowComplianceStateRequest.IpRangeRequest> ranges, string fieldName, out string? error)
    {
        int rangeCount = 0;
        foreach (GetFlowComplianceStateRequest.IpRangeRequest range in ranges)
        {
            rangeCount++;
            if (!TryValidateIpRange(range.IpStart, range.IpEnd, out error))
            {
                error = $"The {fieldName} array contains an invalid IP range: {error}";
                return false;
            }
        }

        if (rangeCount == 0)
        {
            error = $"The {fieldName} array must contain at least one item.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateIpRanges(IEnumerable<GetNetGroupValidityRequestItem> ranges, string fieldName, out string? error)
    {
        int rangeCount = 0;
        foreach (GetNetGroupValidityRequestItem range in ranges)
        {
            rangeCount++;
            if (!TryValidateIpRange(range.IpStart, range.IpEnd, out error))
            {
                error = $"The {fieldName} array contains an invalid IP range: {error}";
                return false;
            }
        }

        if (rangeCount == 0)
        {
            error = $"The {fieldName} array must contain at least one item.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateServiceRanges(IEnumerable<GetFlowComplianceStateRequest.ServiceRangeRequest> ranges, out string? error)
    {
        int rangeCount = 0;
        foreach (GetFlowComplianceStateRequest.ServiceRangeRequest range in ranges)
        {
            rangeCount++;
            if (!TryValidatePortRange(range.PortStart, range.PortEnd, out error))
            {
                error = $"The service array contains an invalid port range: {error}";
                return false;
            }

            if (!TryValidateProtocol(range.Protocol, out error))
            {
                error = $"The service array contains an invalid protocol: {error}";
                return false;
            }
        }

        if (rangeCount == 0)
        {
            error = "The service array must contain at least one item.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidatePortRange(int portStart, int portEnd, out string? error)
    {
        if (portStart < 0 || portStart > 65535)
        {
            error = "The field portStart must be between 0 and 65535.";
            return false;
        }

        if (portEnd < 0 || portEnd > 65535)
        {
            error = "The field portEnd must be between 0 and 65535.";
            return false;
        }

        if (portStart > portEnd)
        {
            error = "The field portStart must not be greater than portEnd.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateProtocol(string? protocol, out string? error)
    {
        if (!string.IsNullOrWhiteSpace(protocol))
        {
            error = null;
            return true;
        }

        error = "The protocol must not be empty.";
        return false;
    }

    private static bool TryValidateNetMask(int netMask, out string? error)
    {
        if (netMask is >= 0 and <= 32)
        {
            error = null;
            return true;
        }

        error = "The netMask must be between 0 and 32.";
        return false;
    }

    private static bool TryValidateMinPrefixLength(int minPrefixLength, out string? error)
    {
        if (minPrefixLength is >= 0 and <= 32)
        {
            error = null;
            return true;
        }

        error = "The minPrefixLength must be between 0 and 32.";
        return false;
    }

    private static bool TryValidateRule(CreateRequestRequest.CreateRequestRuleRequest rule, out string? error)
    {
        if (rule is null)
        {
            error = "The rules array contains an empty item.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.Action))
        {
            error = "Rule action must not be empty.";
            return false;
        }

        if (!rule.Action.Equals("allow", StringComparison.OrdinalIgnoreCase) &&
            !rule.Action.Equals("deny", StringComparison.OrdinalIgnoreCase))
        {
            error = "Rule action must be either allow or deny.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            error = "Rule names must not be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.ViolationJustification))
        {
            error = "Rule violationJustification must not be empty.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateAddressObject(CreateRequestRequest.CreateAddressObjectRequest addressObject, out string? error)
    {
        if (addressObject is null)
        {
            error = "The addressObjects array contains an empty item.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(addressObject.Name))
        {
            error = "Address object names must not be empty.";
            return false;
        }

        if (!TryValidateIpRange(addressObject.IpStart, addressObject.IpEnd, out error))
        {
            error = $"The addressObjects array contains an invalid IP range: {error}";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateServiceObject(CreateRequestRequest.CreateServiceObjectRequest serviceObject, out string? error)
    {
        if (serviceObject is null)
        {
            error = "The serviceObjects array contains an empty item.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(serviceObject.Name))
        {
            error = "Service object names must not be empty.";
            return false;
        }

        if (!TryValidatePortRange(serviceObject.PortStart, serviceObject.PortEnd, out error))
        {
            error = $"The serviceObjects array contains an invalid port range: {error}";
            return false;
        }

        if (!TryValidateProtocol(serviceObject.Protocol, out error))
        {
            error = $"The serviceObjects array contains an invalid protocol: {error}";
            return false;
        }

        error = null;
        return true;
    }

    private static int CompareIpAddresses(IPAddress left, IPAddress right)
    {
        byte[] leftBytes = left.MapToIPv6().GetAddressBytes();
        byte[] rightBytes = right.MapToIPv6().GetAddressBytes();

        for (int index = 0; index < leftBytes.Length; index++)
        {
            if (leftBytes[index] < rightBytes[index])
            {
                return -1;
            }

            if (leftBytes[index] > rightBytes[index])
            {
                return 1;
            }
        }

        return 0;
    }
}
