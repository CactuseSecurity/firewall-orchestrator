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
        ValidateShortString(request.PreWorkflowTicketReference, "preWorkflowTicketReference", result);
        ValidateShortString(request.RequestorName, "requestorName", result);
        ValidateShortString(request.RequestorId, "requestorId", result);
        ValidateShortString(request.RuleContactName, "ruleContactName", result);
        ValidateShortString(request.RuleContactId, "ruleContactId", result);
        ValidateShortString(request.Title, "title", result);
        ValidateCollection(request.AddressObjects, "addressObjects", result);
        ValidateCollection(request.AddressGroups, "addressGroups", result);
        ValidateCollection(request.ServiceObjects, "serviceObjects", result);
        ValidateCollection(request.ServiceGroups, "serviceGroups", result);
        ValidateCollection(request.TimeObjects, "timeObjects", result);
        ValidateText(request.Notes, "notes", result);

        if (request.Rules is not { Count: > 0 })
        {
            result.Errors.Add(BuildError("rules", "'rules' must contain at least one rule."));
        }
        else
        {
            if (request.Rules.Count > CreateTicketRequest.kMaxCollectionCount)
            {
                result.Errors.Add(BuildError("rules", $"'rules' must not contain more than {CreateTicketRequest.kMaxCollectionCount} items."));
            }
            for (int index = 0; index < Math.Min(request.Rules.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateRule(request.Rules[index], $"rules[{index}]", result);
            }
        }

        if (request.AddressObjects is { Count: > 0 } addressObjects)
        {
            for (int index = 0; index < Math.Min(addressObjects.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateAddressObject(addressObjects[index], $"addressObjects[{index}]", result);
            }
        }
        if (request.AddressGroups is { Count: > 0 } addressGroups)
        {
            for (int index = 0; index < Math.Min(addressGroups.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateAddressGroup(addressGroups[index], $"addressGroups[{index}]", result);
            }
        }
        if (request.ServiceObjects is { Count: > 0 } serviceObjects)
        {
            for (int index = 0; index < Math.Min(serviceObjects.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateServiceObject(serviceObjects[index], $"serviceObjects[{index}]", result);
            }
        }
        if (request.ServiceGroups is { Count: > 0 } serviceGroups)
        {
            for (int index = 0; index < Math.Min(serviceGroups.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateServiceGroup(serviceGroups[index], $"serviceGroups[{index}]", result);
            }
        }
        if (request.TimeObjects is { Count: > 0 } timeObjects)
        {
            for (int index = 0; index < Math.Min(timeObjects.Count, CreateTicketRequest.kMaxCollectionCount); index++)
            {
                ValidateTimeObject(timeObjects[index], $"timeObjects[{index}]", result);
            }
        }

        return result;
    }

    /// <summary>
    /// Wraps a late request validation failure in the endpoint's standard response shape.
    /// </summary>
    /// <param name="message">Validation message produced by the request builder.</param>
    /// <returns>A structured validation response.</returns>
    public static RequestValidationErrorResponse BuildServiceError(string message)
    {
        return new RequestValidationErrorResponse
        {
            Errors = [new RequestValidationError { Path = "request", Message = message }]
        };
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
        ValidateShortString(rule.Predicate, $"{path}.predicate", result);
        ValidateShortString(rule.Action, $"{path}.action", result);
        ValidateShortString(rule.Name, $"{path}.name", result);
        ValidateText(rule.Description, $"{path}.description", result);
        ValidateText(rule.Reason, $"{path}.reason", result);
        ValidateText(rule.ViolationJustification, $"{path}.violationJustification", result);
        ValidateCombinedTaskReason(rule, path, result);
        ValidateReferences(rule.SourceObjects, $"{path}.sourceObjects", result);
        ValidateReferences(rule.SourceGroups, $"{path}.sourceGroups", result);
        ValidateReferences(rule.DestinationObjects, $"{path}.destinationObjects", result);
        ValidateReferences(rule.DestinationGroups, $"{path}.destinationGroups", result);
        ValidateReferences(rule.ServiceObjects, $"{path}.serviceObjects", result);
        ValidateReferences(rule.ServiceGroups, $"{path}.serviceGroups", result);
    }

    private static void ValidateAddressObject(CreateTicketRequest.CreateAddressObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Predicate, $"{path}.predicate", result);
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateShortString(value.IpStart, $"{path}.ipStart", result);
        ValidateShortString(value.IpEnd, $"{path}.ipEnd", result);
    }

    private static void ValidateAddressGroup(CreateTicketRequest.CreateAddressGroupRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateReferences(value.MemberIds, $"{path}.memberIds", result);
    }

    private static void ValidateServiceObject(CreateTicketRequest.CreateServiceObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Predicate, $"{path}.predicate", result);
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateShortString(value.Protocol, $"{path}.protocol", result);
    }

    private static void ValidateServiceGroup(CreateTicketRequest.CreateServiceGroupRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateReferences(value.MemberIds, $"{path}.memberIds", result);
    }

    private static void ValidateTimeObject(CreateTicketRequest.CreateTimeObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateDateTimeString(value.StartTime, $"{path}.startTime", result);
        ValidateDateTimeString(value.EndTime, $"{path}.endTime", result);
    }

    private static void ValidateReferences(List<long>? references, string path,
        RequestValidationErrorResponse result)
    {
        if (references == null)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must be an array with at most {CreateTicketRequest.kMaxCollectionCount} items."));
        }
        else
        {
            if (references.Count > CreateTicketRequest.kMaxCollectionCount)
            {
                result.Errors.Add(BuildError(path, $"'{path}' must not contain more than {CreateTicketRequest.kMaxCollectionCount} items."));
            }
            if (references.Any(reference => reference == 0))
            {
                result.Errors.Add(BuildError(path, $"'{path}' must contain only non-zero integers."));
            }
        }
    }

    private static void ValidateCollection<T>(List<T>? values, string path, RequestValidationErrorResponse result)
    {
        if (values == null)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must be an array with at most {CreateTicketRequest.kMaxCollectionCount} items."));
        }
        else if (values.Count > CreateTicketRequest.kMaxCollectionCount)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not contain more than {CreateTicketRequest.kMaxCollectionCount} items."));
        }
    }

    private static void ValidateShortString(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxIdentifierLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxIdentifierLength} characters."));
        }
    }

    private static void ValidateText(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxTextLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxTextLength} characters."));
        }
    }

    private static void ValidateDateTimeString(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxDateTimeLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxDateTimeLength} characters."));
        }
    }

    private static void ValidateCombinedTaskReason(CreateTicketRequest.CreateTicketRuleRequest rule, string path,
        RequestValidationErrorResponse result)
    {
        int combinedLength = (rule.ViolationJustification?.Length ?? 0) + (rule.Reason?.Length ?? 0);
        if (!string.IsNullOrWhiteSpace(rule.ViolationJustification) && !string.IsNullOrWhiteSpace(rule.Reason))
        {
            combinedLength++;
        }
        if (combinedLength > CreateTicketRequest.kMaxTextLength)
        {
            result.Errors.Add(BuildError($"{path}.reason",
                $"the combined violationJustification and reason text must not exceed {CreateTicketRequest.kMaxTextLength} characters."));
        }
    }

    private static RequestValidationError BuildError(string path, string message)
    {
        return new RequestValidationError { Path = path, Message = message };
    }
}
