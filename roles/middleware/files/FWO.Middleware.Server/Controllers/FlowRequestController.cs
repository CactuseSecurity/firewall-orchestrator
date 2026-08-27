using FWO.Basics;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides flow request endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/flow")]
public class FlowRequestController : ControllerBase
{
    internal static readonly RequestValidationSchema CreateRequestSchema = RequestValidationSchema
        .EndpointWithOptions(nameof(CreateRequest))
        .RequiredString("title")
        .OptionalList("rules", rule => rule
            .OptionalString("action")
            .OptionalString("name")
            .OptionalList("sourceObjects")
            .OptionalList("destinationObjects")
            .OptionalList("serviceObjects")
            .OptionalInt("timeObjectId")
            .OptionalInt("ownerId")
            .OptionalString("violationJustification"))
        .OptionalList("addressObjects", addressObject => addressObject
            .OptionalString("id")
            .OptionalString("name")
            .OptionalString("ipStart")
            .OptionalString("ipEnd"))
        .OptionalList("addressGroups", addressGroup => addressGroup
            .OptionalInt("id")
            .OptionalString("name")
            .OptionalList("memberIds"))
        .OptionalList("serviceObjects", serviceObject => serviceObject
            .OptionalString("id")
            .OptionalString("name")
            .OptionalString("protocol")
            .OptionalInt("portStart")
            .OptionalInt("portEnd"))
        .OptionalList("serviceGroups", serviceGroup => serviceGroup
            .OptionalInt("id")
            .OptionalString("name")
            .OptionalList("memberIds"))
        .OptionalList("timeObjects", timeObject => timeObject
            .OptionalString("id")
            .OptionalString("name")
            .OptionalString("startTime")
            .OptionalString("endTime"));
    internal static readonly RequestValidationSchema RequestStatusSchema = RequestValidationSchema
        .EndpointWithOptions(nameof(GetRequestStatus))
        .RequiredInt("ticketId");

    private readonly FlowRequestService flowRequestService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowRequestController"/> class.
    /// </summary>
    /// <param name="flowRequestService">The flow request service.</param>
    public FlowRequestController(FlowRequestService flowRequestService)
    {
        this.flowRequestService = flowRequestService;
    }

    /// <summary>
    /// Creates a new request. Only <c>title</c> is required; at least one change must be supplied in <c>rules</c>,
    /// <c>addressObjects</c>, <c>addressGroups</c>, <c>serviceObjects</c>, <c>serviceGroups</c>, or <c>timeObjects</c>.
    /// The optional <c>options</c> object defaults to <c>{}</c> when omitted. Unknown root and nested request fields
    /// are rejected with <see cref="ValidationProblemDetails"/> before the workflow request is created.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("createRequest")]
    [ProducesResponseType(typeof(CreateRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateRequestResponse>> CreateRequest([FromBody] CreateRequestRequest? request)
    {
        try
        {
            if (!RequestValidator.TryValidate(request, CreateRequestSchema, out ActionResult? errorResult))
            {
                return errorResult!;
            }
            if (TryValidateCreateRequestSemantics(request!, out errorResult))
            {
                return errorResult!;
            }
            int requesterId = FWO.Basics.JwtClaimParser.ExtractIntClaimValues(User.Claims, "x-hasura-user-id").FirstOrDefault();
            CreateRequestResponse response = await flowRequestService.CreateRequestAsync(request!, requesterId);
            return Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            return BuildValidationError("$", argumentException.Message);
        }
        catch (Exception exception)
        {
            Log.WriteError("Create Request", "Error while creating workflow request.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns the status of an existing request. The <c>ticketId</c> root property is required and must be a
    /// positive 64-bit integer. The optional <c>options</c> object defaults to <c>{}</c> when omitted.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getRequestStatus")]
    [ProducesResponseType(typeof(GetRequestStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetRequestStatusResponse>> GetRequestStatus([FromBody] GetRequestStatusRequest? request)
    {
        if (!RequestValidator.TryValidate(request, RequestStatusSchema, out ActionResult? errorResult))
        {
            return errorResult!;
        }
        if (request!.TicketId is not > 0)
        {
            return BuildValidationError("ticketId", "The ticket id must be a positive 64-bit integer.");
        }

        try
        {
            GetRequestStatusResponse? response = await flowRequestService.GetRequestStatusAsync(request.TicketId.Value);
            return response == null ? NotFound() : Ok(response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Request Status", "Error while fetching workflow ticket status.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private static bool TryValidateCreateRequestSemantics(CreateRequestRequest request, out ActionResult? errorResult)
    {
        RequestValidationErrors errors = new();
        AddRequiredStringError(errors, request.Title, "title", "request title");
        if (!HasRequestedChanges(request))
        {
            errors.Add("$", "At least one change is required.");
        }

        if (!errors.HasErrors)
        {
            errorResult = null;
            return false;
        }

        errorResult = RequestValidationProblemDetailsFactory.BadRequest(errors);
        return true;
    }

    private static void AddRequiredStringError(RequestValidationErrors errors, string value, string fieldPath, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(fieldPath, $"The {description} must not be empty.");
        }
    }

    private static bool HasRequestedChanges(CreateRequestRequest request)
    {
        return request.Rules.Count > 0
            || request.AddressObjects.Count > 0
            || request.AddressGroups.Count > 0
            || request.ServiceObjects.Count > 0
            || request.ServiceGroups.Count > 0
            || request.TimeObjects.Count > 0;
    }

    private static BadRequestObjectResult BuildValidationError(string fieldPath, string message)
    {
        RequestValidationErrors errors = new();
        errors.Add(fieldPath, message);
        return RequestValidationProblemDetailsFactory.BadRequest(errors);
    }
}
