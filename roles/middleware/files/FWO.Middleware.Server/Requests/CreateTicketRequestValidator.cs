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

        ValidateRootFields(request, result);
        ValidateRules(request.Rules, result);
        ValidateNestedCollections(request, result);

        return result;
    }

    /// <summary>Validates required root fields and root-level collection limits.</summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateRootFields(CreateTicketRequest request, RequestValidationErrorResponse result)
    {
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
    }

    /// <summary>Validates the required rules collection and each contained rule.</summary>
    /// <param name="rules">The rules supplied by the caller.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateRules(List<CreateTicketRequest.CreateTicketRuleRequest>? rules,
        RequestValidationErrorResponse result)
    {
        if (rules is not { Count: > 0 })
        {
            result.Errors.Add(BuildError("rules", "'rules' must contain at least one rule."));
            return;
        }

        if (rules.Count > CreateTicketRequest.kMaxCollectionCount)
        {
            result.Errors.Add(BuildError("rules", $"'rules' must not contain more than {CreateTicketRequest.kMaxCollectionCount} items."));
        }
        ValidateItems(rules, "rules", ValidateRule, result);
    }

    /// <summary>Validates the elements of all optional object, group, service, and time collections.</summary>
    /// <param name="request">The request containing the nested collections.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateNestedCollections(CreateTicketRequest request, RequestValidationErrorResponse result)
    {
        ValidateItems(request.AddressObjects, "addressObjects", ValidateAddressObject, result);
        ValidateItems(request.AddressGroups, "addressGroups", ValidateAddressGroup, result);
        ValidateItems(request.ServiceObjects, "serviceObjects", ValidateServiceObject, result);
        ValidateItems(request.ServiceGroups, "serviceGroups", ValidateServiceGroup, result);
        ValidateItems(request.TimeObjects, "timeObjects", ValidateTimeObject, result);
    }

    /// <summary>Runs an element validator for the bounded portion of a collection.</summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="values">The values to validate.</param>
    /// <param name="path">The request path of the collection.</param>
    /// <param name="validator">The element validator to invoke.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateItems<T>(List<T>? values, string path,
        Action<T, string, RequestValidationErrorResponse> validator, RequestValidationErrorResponse result)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < Math.Min(values.Count, CreateTicketRequest.kMaxCollectionCount); index++)
        {
            validator(values[index], $"{path}[{index}]", result);
        }
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

    /// <summary>Adds an error when a required string is null, empty, or whitespace.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="path">The request path of the value.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void AddRequiredError(string? value, string path, RequestValidationErrorResponse result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not be empty."));
        }
    }

    /// <summary>Validates a rule and all of its references and descriptive fields.</summary>
    /// <param name="rule">The rule to validate.</param>
    /// <param name="path">The request path of the rule.</param>
    /// <param name="result">The response receiving validation errors.</param>
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

    /// <summary>Validates one declared network object.</summary>
    /// <param name="value">The network object to validate.</param>
    /// <param name="path">The request path of the object.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateAddressObject(CreateTicketRequest.CreateAddressObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        if (value == null)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not be null."));
            return;
        }

        ValidateShortString(value.Predicate, $"{path}.predicate", result);
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateShortString(value.IpHost, $"{path}.ipHost", result);
        ValidateShortString(value.IpNetwork, $"{path}.ipNetwork", result);
        if (value.IpRange != null)
        {
            for (int index = 0; index < Math.Min(value.IpRange.Count, 2); index++)
            {
                ValidateShortString(value.IpRange[index], $"{path}.ipRange[{index}]", result);
            }
        }

        if (!AddressInputNormalizer.TryValidateAndNormalize(
            value,
            $"'{path}'",
            out NormalizedAddressBounds bounds,
            out string? errorMessage))
        {
            result.Errors.Add(BuildError(path, errorMessage!));
            return;
        }

        value.NormalizedIpStart = bounds.IpStart;
        value.NormalizedIpEnd = bounds.IpEnd;
    }

    /// <summary>Validates one declared network group and its members.</summary>
    /// <param name="value">The network group to validate.</param>
    /// <param name="path">The request path of the group.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateAddressGroup(CreateTicketRequest.CreateAddressGroupRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateReferences(value.MemberIds, $"{path}.memberIds", result);
    }

    /// <summary>Validates one declared service object.</summary>
    /// <param name="value">The service object to validate.</param>
    /// <param name="path">The request path of the object.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateServiceObject(CreateTicketRequest.CreateServiceObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Predicate, $"{path}.predicate", result);
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateShortString(value.Protocol, $"{path}.protocol", result);
    }

    /// <summary>Validates one declared service group and its members.</summary>
    /// <param name="value">The service group to validate.</param>
    /// <param name="path">The request path of the group.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateServiceGroup(CreateTicketRequest.CreateServiceGroupRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateReferences(value.MemberIds, $"{path}.memberIds", result);
    }

    /// <summary>Validates one declared time object.</summary>
    /// <param name="value">The time object to validate.</param>
    /// <param name="path">The request path of the object.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateTimeObject(CreateTicketRequest.CreateTimeObjectRequest value, string path,
        RequestValidationErrorResponse result)
    {
        ValidateShortString(value.Name, $"{path}.name", result);
        ValidateDateTimeString(value.StartTime, $"{path}.startTime", result);
        ValidateDateTimeString(value.EndTime, $"{path}.endTime", result);
    }

    /// <summary>Validates a reference collection's presence, size, and non-zero values.</summary>
    /// <param name="references">The references to validate.</param>
    /// <param name="path">The request path of the references.</param>
    /// <param name="result">The response receiving validation errors.</param>
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

    /// <summary>Validates the size and presence of a root-level collection.</summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="values">The collection to validate.</param>
    /// <param name="path">The request path of the collection.</param>
    /// <param name="result">The response receiving validation errors.</param>
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

    /// <summary>Validates the maximum length of an identifier-like string.</summary>
    /// <param name="value">The string to validate.</param>
    /// <param name="path">The request path of the string.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateShortString(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxIdentifierLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxIdentifierLength} characters."));
        }
    }

    /// <summary>Validates the maximum length of a descriptive text value.</summary>
    /// <param name="value">The text to validate.</param>
    /// <param name="path">The request path of the text.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateText(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxTextLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxTextLength} characters."));
        }
    }

    /// <summary>Validates the maximum length of a date-time representation.</summary>
    /// <param name="value">The date-time string to validate.</param>
    /// <param name="path">The request path of the date-time string.</param>
    /// <param name="result">The response receiving validation errors.</param>
    private static void ValidateDateTimeString(string? value, string path, RequestValidationErrorResponse result)
    {
        if (value?.Length > CreateTicketRequest.kMaxDateTimeLength)
        {
            result.Errors.Add(BuildError(path, $"'{path}' must not exceed {CreateTicketRequest.kMaxDateTimeLength} characters."));
        }
    }

    /// <summary>Validates the combined size of the rule justification and reason persisted as task reason.</summary>
    /// <param name="rule">The rule whose text values are combined.</param>
    /// <param name="path">The request path of the rule.</param>
    /// <param name="result">The response receiving validation errors.</param>
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

    /// <summary>Creates a structured validation error.</summary>
    /// <param name="path">The request path associated with the error.</param>
    /// <param name="message">The human-readable validation message.</param>
    /// <returns>The structured validation error.</returns>
    private static RequestValidationError BuildError(string path, string message)
    {
        return new RequestValidationError { Path = path, Message = message };
    }
}
