using FWO.Middleware.Server.Responses;

namespace FWO.Middleware.Server.Requests;

/// <summary>
/// Validates the key-level constraints of a create-ticket request and reports all errors together.
/// </summary>
public static class CreateTicketRequestValidator
{
    /// <summary>
    /// Validates the request without stopping at the first error.
    /// </summary>
    /// <param name="request">Request to validate, or null when no body was supplied.</param>
    /// <returns>All detected validation errors.</returns>
    public static RequestValidationErrorResponse Validate(CreateTicketRequest? request)
    {
        RequestValidationErrorResponse result = new();
        if (request == null)
        {
            result.Errors.Add(BuildError("", "createTicket requires a request body."));
            return result;
        }

        AddRequiredError(request.RequestorName, "requestorName", result);
        AddRequiredError(request.RequestorId, "requestorId", result);
        AddRequiredError(request.RuleContactName, "ruleContactName", result);
        AddRequiredError(request.RuleContactId, "ruleContactId", result);
        AddRequiredError(request.Title, "title", result);

        if (request.Rules is not { Count: > 0 })
        {
            result.Errors.Add(BuildError("rules", "'rules' must contain at least one rule."));
        }
        else
        {
            for (int index = 0; index < request.Rules.Count; index++)
            {
                ValidateRule(request.Rules[index], $"rules[{index}]", result);
            }
        }

        return result;
    }

    private static void AddRequiredError(string? value, string path, RequestValidationErrorResponse result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not be empty."));
        }
    }

    private static void ValidateRule(CreateTicketRequest.CreateTicketRuleRequest rule, string path,
        RequestValidationErrorResponse result)
    {
        AddRequiredError(rule.Action, $"{path}.action", result);
        ValidateReferences(rule.SourceObjects, $"{path}.sourceObjects", result);
        ValidateReferences(rule.SourceGroups, $"{path}.sourceGroups", result);
        ValidateReferences(rule.DestinationObjects, $"{path}.destinationObjects", result);
        ValidateReferences(rule.DestinationGroups, $"{path}.destinationGroups", result);
        ValidateReferences(rule.ServiceObjects, $"{path}.serviceObjects", result);
        ValidateReferences(rule.ServiceGroups, $"{path}.serviceGroups", result);
    }

    private static void ValidateReferences(List<long>? references, string path,
        RequestValidationErrorResponse result)
    {
        if (references == null || references.Any(reference => reference == 0))
        {
            result.Errors.Add(BuildError(path, $"'{path}' must contain only non-zero integers."));
        }
    }

    private static RequestValidationError BuildError(string path, string message)
    {
        return new RequestValidationError { Path = path, Message = message };
    }
}
