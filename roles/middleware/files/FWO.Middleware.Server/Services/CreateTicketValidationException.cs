using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Represents multiple semantic validation failures found while preparing a workflow ticket.
/// </summary>
internal sealed class CreateTicketValidationException : ArgumentException
{
    /// <summary>
    /// Initializes a new instance with the collected request errors.
    /// </summary>
    /// <param name="errors">Collected validation errors.</param>
    public CreateTicketValidationException(IEnumerable<RequestValidationError> errors)
        : base("The create-ticket request contains semantic validation errors.")
    {
        Errors = new RequestValidationErrorResponse { Errors = errors.ToList() };
    }

    /// <summary>Gets the structured validation response.</summary>
    public RequestValidationErrorResponse Errors { get; }
}
