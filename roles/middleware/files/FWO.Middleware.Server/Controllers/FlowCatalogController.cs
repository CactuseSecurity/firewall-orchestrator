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
    private static readonly RequestValidationSchema AddressObjectsSchema = CreateVisibleInRequestSchema(nameof(GetAddressObjects));
    private static readonly RequestValidationSchema AddressGroupsSchema = CreateVisibleInRequestSchema(nameof(GetAddressGroups));
    private static readonly RequestValidationSchema ServiceObjectsSchema = CreateVisibleInRequestSchema(nameof(GetServiceObjects));
    private static readonly RequestValidationSchema ServiceGroupsSchema = CreateVisibleInRequestSchema(nameof(GetServiceGroups));
    private static readonly RequestValidationSchema TimeObjectsSchema = CreateVisibleInRequestSchema(nameof(GetTimeObjects));
    private static readonly RequestValidationSchema ServiceObjectIdSchema = CreateVisibleInRequestSchema(nameof(GetServiceObjectId))
        .RequiredInt("portStart")
        .RequiredInt("portEnd")
        .RequiredString("protocol");
    private static readonly RequestValidationSchema TimeObjectIdSchema = CreateVisibleInRequestSchema(nameof(GetTimeObjectId))
        .OptionalString("startTime")
        .OptionalString("endTime");
    private static readonly RequestValidationSchema AddressObjectIdSchema = CreateVisibleInRequestSchema(nameof(GetAddressObjectId))
        .RequiredString("ipStart")
        .RequiredString("ipEnd");

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
    [ProducesResponseType(typeof(List<AddressObjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AddressObjectResponse>>> GetAddressObjects([FromBody] GetAddressObjectsRequest request)
    {
        if (!RequestValidator.TryValidate(request, AddressObjectsSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressObjectsAsync(request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns address groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressGroups")]
    [ProducesResponseType(typeof(List<AddressGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AddressGroupResponse>>> GetAddressGroups([FromBody] GetAddressGroupsRequest request)
    {
        if (!RequestValidator.TryValidate(request, AddressGroupsSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetAddressGroupsAsync(request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns service objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjects")]
    [ProducesResponseType(typeof(List<ServiceObjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ServiceObjectResponse>>> GetServiceObjects([FromBody] GetServiceObjectsRequest request)
    {
        if (!RequestValidator.TryValidate(request, ServiceObjectsSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceObjectsAsync(request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns service groups for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceGroups")]
    [ProducesResponseType(typeof(List<ServiceGroupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ServiceGroupResponse>>> GetServiceGroups([FromBody] GetServiceGroupsRequest request)
    {
        if (!RequestValidator.TryValidate(request, ServiceGroupsSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetServiceGroupsAsync(request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Returns time objects for the requested visibility filter from the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTimeObjects")]
    [ProducesResponseType(typeof(List<TimeObjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<TimeObjectResponse>>> GetTimeObjects([FromBody] GetTimeObjectsRequest request)
    {
        if (!RequestValidator.TryValidate(request, TimeObjectsSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        return Ok(await flowCatalogService.GetTimeObjectsAsync(request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves a service object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getServiceObjectId")]
    [ProducesResponseType(typeof(ServiceObjectIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServiceObjectIdResponse>> GetServiceObjectId([FromBody] GetServiceObjectIdRequest request)
    {
        if (!RequestValidator.TryValidate(request, ServiceObjectIdSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.Protocol))
        {
            return BuildValidationError("protocol", "'protocol' is required.");
        }

        if (!FlowComplianceRequestValidator.TryValidateServiceRange(request.PortStart!.Value, request.PortEnd!.Value, "service", 0, out string? serviceErrorMessage))
        {
            return BuildValidationError("service[0]", serviceErrorMessage!);
        }

        return Ok(await flowCatalogService.GetServiceObjectIdAsync(request.Protocol, request.PortStart.Value, request.PortEnd.Value, request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves a time object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getTimeObjectId")]
    [ProducesResponseType(typeof(TimeObjectIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TimeObjectIdResponse>> GetTimeObjectId([FromBody] GetTimeObjectIdRequest request)
    {
        if (!RequestValidator.TryValidate(request, TimeObjectIdSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (!request.StartTime.HasValue && !request.EndTime.HasValue)
        {
            return BuildValidationError(RequestFieldPath.Root, "At least one of 'startTime' or 'endTime' is required.");
        }

        if (request.StartTime.HasValue && request.EndTime.HasValue && request.StartTime > request.EndTime)
        {
            return BuildValidationError("startTime", "'startTime' must be <= 'endTime'.");
        }

        return Ok(await flowCatalogService.GetTimeObjectIdAsync(request.StartTime, request.EndTime, request.Options?.Filter?.VisibleInRequest));
    }

    /// <summary>
    /// Resolves an address object identifier from the supplied lookup request against the shared flow catalog.
    /// This lookup is not scoped to a modeller or owner.
    /// Optional /32 masks on ipStart and ipEnd are ignored; all other masks are rejected.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getAddressObjectId")]
    [ProducesResponseType(typeof(AddressObjectIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AddressObjectIdResponse>> GetAddressObjectId([FromBody] GetAddressObjectIdRequest request)
    {
        if (!RequestValidator.TryValidate(request, AddressObjectIdSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }

        if (string.IsNullOrWhiteSpace(request.IpStart) || string.IsNullOrWhiteSpace(request.IpEnd))
        {
            return BuildValidationError("ipStart", "'ipStart' and 'ipEnd' are required.");
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
            return BuildValidationError("address[0]", addressErrorMessage!);
        }

        request.IpStart = normalizedIpStart;
        request.IpEnd = normalizedIpEnd;
        return Ok(await flowCatalogService.GetAddressObjectIdAsync(request.IpStart, request.IpEnd, request.Options?.Filter?.VisibleInRequest));
    }

    private static RequestValidationSchema CreateVisibleInRequestSchema(string endpointName)
    {
        return RequestValidationSchema.EndpointWithOptions(endpointName, options => options
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest")));
    }

    private static BadRequestObjectResult BuildValidationError(string fieldPath, string message)
    {
        RequestValidationErrors errors = new();
        errors.Add(fieldPath, message);
        return RequestValidationProblemDetailsFactory.BadRequest(errors);
    }
}
