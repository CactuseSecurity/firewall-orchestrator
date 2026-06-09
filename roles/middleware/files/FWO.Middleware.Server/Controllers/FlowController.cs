using System.Globalization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Flow;
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
    private readonly ApiConnection apiConnection;

    public FlowController(ApiConnection apiConnection)
    {
        this.apiConnection = apiConnection;
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

        List<FlowNwObject> flowObjects = await LoadFlowNwObjectsAsync(request.Filter?.VisibleInRequest);
        return Ok(flowObjects.Select(ToAddressObjectResponse).ToList());
    }

    [HttpPost("getAddressGroups")]
    public async Task<ActionResult<List<AddressGroupResponse>>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, AddressGroupsRootSchema, AddressGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        List<FlowNwGroup> flowGroups = await LoadFlowNwGroupsAsync(request.Filter?.VisibleInRequest);
        return Ok(flowGroups.Select(ToAddressGroupResponse).ToList());
    }

    [HttpPost("getServiceObjects")]
    public async Task<ActionResult<List<ServiceObjectResponse>>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceObjectsRootSchema, ServiceObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        List<FlowSvcObject> flowObjects = await LoadFlowSvcObjectsAsync(request.Filter?.VisibleInRequest);
        return Ok(flowObjects.Select(ToServiceObjectResponse).ToList());
    }

    [HttpPost("getServiceGroups")]
    public async Task<ActionResult<List<ServiceGroupResponse>>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, ServiceGroupsRootSchema, ServiceGroupsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        List<FlowSvcGroup> flowGroups = await LoadFlowSvcGroupsAsync(request.Filter?.VisibleInRequest);
        return Ok(flowGroups.Select(ToServiceGroupResponse).ToList());
    }

    [HttpPost("getTimeObjects")]
    public async Task<ActionResult<List<TimeObjectResponse>>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!TryValidateVisibleInRequestRequest(request, TimeObjectsRootSchema, TimeObjectsFilterSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        List<FlowTimeObject> flowObjects = await LoadFlowTimeObjectsAsync(request.Filter?.VisibleInRequest);
        return Ok(flowObjects.Select(ToTimeObjectResponse).ToList());
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

    private async Task<List<FlowNwObject>> LoadFlowNwObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getAddressObjects",
            "nwobjects",
            "flow_nwobject",
            "nwobj_id",
            "flowNwObjectDetails",
            FlowQueries.flowNwObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowNwObject>>(query) ?? [];
    }

    private async Task<List<FlowNwGroup>> LoadFlowNwGroupsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getAddressGroups",
            "nwgroups",
            "flow_nwgroup",
            "nwgrp_id",
            "flowNwGroupDetails",
            FlowQueries.flowNwGroupDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowNwGroup>>(query) ?? [];
    }

    private async Task<List<FlowSvcObject>> LoadFlowSvcObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getServiceObjects",
            "svcobjects",
            "flow_svcobject",
            "svcobj_id",
            "flowSvcObjectDetails",
            FlowQueries.flowSvcObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowSvcObject>>(query) ?? [];
    }

    private async Task<List<FlowSvcGroup>> LoadFlowSvcGroupsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getServiceGroups",
            "svcgroups",
            "flow_svcgroup",
            "svcgrp_id",
            "flowSvcGroupDetails",
            FlowQueries.flowSvcGroupDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowSvcGroup>>(query) ?? [];
    }

    private async Task<List<FlowTimeObject>> LoadFlowTimeObjectsAsync(bool? visibleInRequest)
    {
        string query = BuildCatalogQuery(
            "getTimeObjects",
            "timeobjects",
            "flow_timeobject",
            "timeobj_id",
            "flowTimeObjectDetails",
            FlowQueries.flowTimeObjectDetailsFragment,
            visibleInRequest);

        return await apiConnection.SendQueryAsync<List<FlowTimeObject>>(query) ?? [];
    }

    private static string BuildCatalogQuery(
        string operationName,
        string topLevelAlias,
        string tableName,
        string idFieldName,
        string fragmentName,
        string fragmentText,
        bool? visibleInRequest)
    {
        string visibleInRequestClause = visibleInRequest.HasValue
            ? $"where: {{ show_in_request_module: {{ _eq: {GetGraphQlBoolean(visibleInRequest.Value)} }} }}, "
            : string.Empty;

        return fragmentText + $@"query {operationName} {{
  {topLevelAlias}: {tableName}({visibleInRequestClause}order_by: [{{ name: asc }}, {{ {idFieldName}: asc }}]) {{
    ...{fragmentName}
  }}
}}";
    }

    private static string GetGraphQlBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static AddressObjectResponse ToAddressObjectResponse(FlowNwObject flowObject)
    {
        return new AddressObjectResponse
        {
            Id = (int)flowObject.Id,
            Name = flowObject.Name ?? string.Empty,
            IpStart = flowObject.IpStart ?? string.Empty,
            IpEnd = flowObject.IpEnd ?? string.Empty,
            State = flowObject.State
        };
    }

    private static AddressGroupResponse ToAddressGroupResponse(FlowNwGroup flowGroup)
    {
        return new AddressGroupResponse
        {
            Id = (int)flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            Members = flowGroup.NwGroupMembers
                .Select(member => new AddressGroupResponse.AddressGroupMemberResponse
                {
                    Id = (int)member.NwObjectId,
                    Name = member.NwObject.Name ?? string.Empty
                })
                .ToList()
        };
    }

    private static ServiceObjectResponse ToServiceObjectResponse(FlowSvcObject flowObject)
    {
        return new ServiceObjectResponse
        {
            Id = (int)flowObject.Id,
            Name = flowObject.Name,
            PortStart = flowObject.PortStart ?? 0,
            PortEnd = flowObject.PortEnd ?? 0,
            Protocol = flowObject.ProtoId > 0 ? flowObject.ProtoId.ToString(CultureInfo.InvariantCulture) : string.Empty,
            State = flowObject.State
        };
    }

    private static ServiceGroupResponse ToServiceGroupResponse(FlowSvcGroup flowGroup)
    {
        return new ServiceGroupResponse
        {
            Id = (int)flowGroup.Id,
            Name = flowGroup.Name,
            State = flowGroup.State,
            Members = flowGroup.SvcGroupMembers
                .Select(member => new ServiceGroupResponse.ServiceGroupMemberResponse
                {
                    Id = (int)member.SvcObjectId,
                    Name = member.SvcObject.Name
                })
                .ToList()
        };
    }

    private static TimeObjectResponse ToTimeObjectResponse(FlowTimeObject flowObject)
    {
        return new TimeObjectResponse
        {
            Id = (int)flowObject.Id,
            Name = flowObject.Name,
            StartTime = flowObject.StartTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            EndTime = flowObject.EndTime?.ToString("o", CultureInfo.InvariantCulture) ?? string.Empty,
            State = flowObject.State
        };
    }
}
