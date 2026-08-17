using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Builds the uniform validation failure response for migrated REST request validators.
/// </summary>
public static class RequestValidationProblemDetailsFactory
{
    /// <summary>
    /// Gets the validation failure HTTP status code.
    /// </summary>
    public const int StatusCode = StatusCodes.Status400BadRequest;

    /// <summary>
    /// Gets the validation problem title.
    /// </summary>
    public const string Title = "One or more request validation errors occurred.";

    /// <summary>
    /// Creates a validation problem details object from collected request errors.
    /// </summary>
    public static ValidationProblemDetails Build(RequestValidationErrors validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        return Build(validationErrors.ToDictionary());
    }

    /// <summary>
    /// Creates a validation problem details object from field-path keyed errors.
    /// </summary>
    public static ValidationProblemDetails Build(Dictionary<string, string[]> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        return new ValidationProblemDetails(validationErrors)
        {
            Status = StatusCode,
            Title = Title
        };
    }

    /// <summary>
    /// Creates a bad-request result that contains the uniform validation problem details object.
    /// </summary>
    public static BadRequestObjectResult BadRequest(RequestValidationErrors validationErrors)
    {
        return new BadRequestObjectResult(Build(validationErrors));
    }
}
