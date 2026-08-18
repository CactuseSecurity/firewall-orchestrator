using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adds validation-focused documentation for migrated flow and zone endpoints.
/// </summary>
public sealed class OpenApiFlowValidationDocumentationProvider : IOpenApiEndpointDocumentationProvider
{
    /// <inheritdoc />
    public bool Matches(ApiDescription description)
    {
        return TryGetControllerAction(description, out Type? controllerType, out string? actionName)
            && IsDocumentedAction(controllerType, actionName);
    }

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation)
    {
        if (operation.RequestBody != null)
        {
            operation.RequestBody.Description = "Request bodies are validated after JSON binding. Unknown JSON properties and missing required fields return a uniform ValidationProblemDetails response.";
        }

        ApplyResponseDescription(operation, "400", "The request body failed request-shape or endpoint-specific validation and is returned as ValidationProblemDetails.");
        ApplyResponseDescription(operation, "401", "The caller did not provide a valid JWT access token.");
        ApplyResponseDescription(operation, "403", "The caller is authenticated but does not have the required role.");
        ApplyResponseDescription(operation, "500", "The middleware server could not complete the request.");

        operation.Description = CreateDescription(operation.Description);
    }

    private static bool TryGetControllerAction(ApiDescription description, out Type? controllerType, out string? actionName)
    {
        if (description.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            controllerType = controllerAction.ControllerTypeInfo?.AsType();
            actionName = controllerAction.ActionName;
            return controllerType != null && actionName != null;
        }

        controllerType = null;
        actionName = null;
        return false;
    }

    private static bool IsDocumentedAction(Type? controllerType, string? actionName)
    {
        if (controllerType == typeof(FlowComplianceController))
        {
            return actionName == nameof(FlowComplianceController.GetFlowComplianceState)
                || actionName == nameof(FlowComplianceController.GetPolicyIds);
        }

        if (controllerType == typeof(FlowCatalogController))
        {
            return actionName == nameof(FlowCatalogController.GetAddressObjects)
                || actionName == nameof(FlowCatalogController.GetAddressGroups)
                || actionName == nameof(FlowCatalogController.GetServiceObjects)
                || actionName == nameof(FlowCatalogController.GetServiceGroups)
                || actionName == nameof(FlowCatalogController.GetTimeObjects)
                || actionName == nameof(FlowCatalogController.GetAddressObjectId)
                || actionName == nameof(FlowCatalogController.GetServiceObjectId)
                || actionName == nameof(FlowCatalogController.GetTimeObjectId);
        }

        return controllerType == typeof(ComplianceZoneController)
            && actionName == nameof(ComplianceZoneController.ResolveZonesForObjects);
    }

    private static void ApplyResponseDescription(OpenApiOperation operation, string statusCode, string description)
    {
        if (operation.Responses?.TryGetValue(statusCode, out IOpenApiResponse? response) == true)
        {
            response.Description = description;
        }
    }

    private static string CreateDescription(string? existingDescription)
    {
        string prefix = string.IsNullOrWhiteSpace(existingDescription)
            ? string.Empty
            : existingDescription.TrimEnd() + Environment.NewLine + Environment.NewLine;

        return prefix + """
Validation behavior:

- Validation failures return `400 Bad Request` with `ValidationProblemDetails`.
- Error keys use `$` for body-level errors and dot notation for fields, for example `filter.visibleInRequest`.
- List items use index notation, for example `source[0].ipStart`.
- Unknown root properties and unknown nested properties are rejected.
- Optional objects and optional fields may be omitted.
- Missing required fields are reported before downstream API or database queries run.
- Malformed JSON and wrong JSON value types are handled by ASP.NET model binding before endpoint validation.
""";
    }
}
