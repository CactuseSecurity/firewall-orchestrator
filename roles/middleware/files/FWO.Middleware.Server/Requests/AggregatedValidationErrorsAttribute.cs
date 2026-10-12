using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Reports model binding failures of an action in the aggregated
/// <see cref="RequestValidationErrorResponse"/> shape instead of the generic problem details of
/// <c>[ApiController]</c>.
/// </summary>
/// <remarks>
/// An endpoint that documents one error shape for status 400 has to produce it for every rejected
/// request, not only for the ones that survive binding. A body whose JSON types do not fit the
/// request contract - <c>{"options":[]}</c>, <c>{"ticketId":"abc"}</c> - fails while it is bound, and
/// the automatic model state filter of <c>[ApiController]</c> would answer it with validation
/// problem details, whose errors member is an object map rather than the documented array. A client
/// generated from the API documentation cannot read that.
/// <para>
/// The filter runs before the automatic one, which sits at order -2000, and short-circuits the
/// request by setting a result. It deliberately does not validate anything itself: what a bound
/// request means is decided by the validator of the endpoint.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AggregatedValidationErrorsAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Order placing this filter ahead of the automatic model state filter of [ApiController].
    /// </summary>
    private const int kBeforeModelStateInvalidFilter = -2100;

    /// <summary>
    /// Initializes a new instance of the type.
    /// </summary>
    public AggregatedValidationErrorsAttribute()
    {
        Order = kBeforeModelStateInvalidFilter;
    }

    /// <summary>
    /// Replaces the automatic problem details answer with the aggregated error response.
    /// </summary>
    /// <param name="context">Context of the action about to be executed.</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(BuildResponse(context.ModelState));
        }
    }

    /// <summary>
    /// Converts the model state entries into the aggregated error response.
    /// </summary>
    /// <param name="modelState">Model state left by binding.</param>
    /// <returns>One error per model state message, attributed to the key that carried it.</returns>
    public static RequestValidationErrorResponse BuildResponse(ModelStateDictionary modelState)
    {
        RequestValidationErrorResponse response = new();
        foreach (KeyValuePair<string, ModelStateEntry> entry in modelState)
        {
            foreach (ModelError error in entry.Value.Errors)
            {
                response.Errors.Add(new RequestValidationError
                {
                    Path = entry.Key,
                    Message = DescribeError(error)
                });
            }
        }

        return response;
    }

    /// <summary>
    /// Picks a caller-facing message for a model error.
    /// </summary>
    /// <param name="error">Model error left by binding.</param>
    /// <returns>The error message, or a generic one when binding recorded only an exception.</returns>
    private static string DescribeError(ModelError error)
    {
        return string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? "The request body could not be read. Check that every value has the documented JSON type."
            : error.ErrorMessage;
    }
}
