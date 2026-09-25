using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates a <see cref="GetTicketRequest"/> completely and reports every detected key-level error in
/// one result.
/// </summary>
public static class GetTicketRequestValidator
{
    /// <summary>
    /// Validates the whole request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected errors; empty when the request is valid.</returns>
    public static RequestValidationErrorResponse Validate(GetTicketRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetTicketValidationSchema.kRootPath,
                $"{GetTicketValidationSchema.kEndpointName} requires a request body. Valid root keys: " +
                AggregatedRequestKeyValidation.DescribeKeys(GetTicketValidationSchema.RootKeys)));
            return result;
        }

        ValidateTicketId(request, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.AdditionalData, GetTicketValidationSchema.kRootPath,
            GetTicketValidationSchema.RootKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.AdditionalData, GetTicketValidationSchema.kOptionsPath,
            GetTicketValidationSchema.OptionsKeys, result);
        AggregatedRequestKeyValidation.AddUnsupportedKeyErrors(request.Options.Filter?.AdditionalData, GetTicketValidationSchema.kFilterPath,
            GetTicketValidationSchema.FilterKeys, result);
        return result;
    }

    /// <summary>
    /// Builds the error reported for a well-formed ticket id that names no workflow ticket.
    /// </summary>
    /// <param name="ticketId">Ticket id the caller supplied.</param>
    /// <returns>An error response attributing the failure to the <c>ticketId</c> key.</returns>
    /// <remarks>
    /// Whether the ticket exists can only be answered by the API, so this error is raised after the
    /// key-level validation above. It uses the same shape so a caller parses one error contract for
    /// every rejected request of this endpoint.
    /// </remarks>
    public static RequestValidationErrorResponse BuildUnknownTicketError(long ticketId)
    {
        RequestValidationErrorResponse result = new();
        result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetTicketValidationSchema.kTicketIdPath,
            GetTicketValidationSchema.DescribeUnknownTicket(ticketId)));
        return result;
    }

    private static void ValidateTicketId(GetTicketRequest request, RequestValidationErrorResponse result)
    {
        if (request.TicketId == null)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetTicketValidationSchema.kTicketIdPath,
                $"'{GetTicketValidationSchema.kTicketIdPath}' is required."));
        }
        else if (request.TicketId <= 0)
        {
            result.Errors.Add(AggregatedRequestKeyValidation.BuildError(GetTicketValidationSchema.kTicketIdPath,
                $"'{GetTicketValidationSchema.kTicketIdPath}' must be greater than 0."));
        }
    }
}
