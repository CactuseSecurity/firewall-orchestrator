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
    /// Generates an address object name.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("generateAddressObjectName")]
    public ActionResult<GenerateAddressObjectNameResponse> GenerateAddressObjectName([FromBody] GenerateAddressObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Generates a service object name.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("generateServiceObjectName")]
    public ActionResult<GenerateServiceObjectNameResponse> GenerateServiceObjectName([FromBody] GenerateServiceObjectNameRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Checks whether a network object definition is valid.
    /// This validation helper is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getNetObjectValidity")]
    public ActionResult<NetObjectValidityResponse> GetNetObjectValidity([FromBody] GetNetObjectValidityRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Checks whether a network group definition is valid.
    /// This validation helper is not scoped to a modeller or owner.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getNetGroupValidity")]
    public ActionResult<NetGroupValidityResponse> GetNetGroupValidity([FromBody] List<GetNetGroupValidityRequestItem> request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    /// <summary>
    /// Creates a new request.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}")]
    [HttpPost("createRequest")]
    [ProducesResponseType(typeof(CreateRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateRequestResponse>> CreateRequest([FromBody] CreateRequestRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body is missing.");
            }
            int requesterId = FWO.Basics.JwtClaimParser.ExtractIntClaimValues(User.Claims, "x-hasura-user-id").FirstOrDefault();
            CreateRequestResponse response = await flowRequestService.CreateRequestAsync(request, requesterId);
            return Ok(response);
        }
        catch (ArgumentException argumentException)
        {
            return BadRequest(argumentException.Message);
        }
        catch (Exception exception)
        {
            Log.WriteError("Create Request", "Error while creating workflow request.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Returns the status of an existing request.
    /// </summary>
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    [HttpPost("getRequestStatus")]
    [ProducesResponseType(typeof(GetRequestStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GetRequestStatusResponse>> GetRequestStatus([FromBody] GetRequestStatusRequest request)
    {
        if (request.TicketId <= 0)
        {
            return BadRequest("'ticketId' must be greater than 0.");
        }

        try
        {
            GetRequestStatusResponse? response = await flowRequestService.GetRequestStatusAsync(request.TicketId);
            return response == null ? NotFound() : Ok(response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Request Status", "Error while fetching workflow ticket status.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
