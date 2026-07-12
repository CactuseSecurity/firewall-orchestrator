using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Data;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Middleware.Server.OpenApi;

/// <summary>
/// Adds detailed owner endpoint documentation to the generated Scalar/OpenAPI document.
/// </summary>
public sealed class OpenApiOwnerDocumentationProvider : IOpenApiEndpointDocumentationProvider
{
    private const string kOwnerEndpointPath = "api/owners/get";
    private static readonly JsonSerializerOptions DocumentationJsonSerializerOptions = CreateDocumentationJsonSerializerOptions();

    /// <inheritdoc />
    public bool Matches(ApiDescription description)
    {
        if (description.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            return controllerAction.ControllerTypeInfo?.AsType() == typeof(OwnersController)
                && string.Equals(controllerAction.ActionName, nameof(OwnersController.Get), StringComparison.Ordinal);
        }

        return string.Equals(description.RelativePath, kOwnerEndpointPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation)
    {
        operation.Description = OwnerEndpointDescription;

        if (operation.RequestBody != null)
        {
            operation.RequestBody.Description = OwnerRequestDescription;
        }

        ApplyResponseDescription(operation, "200", "Returns a JSON array of owners visible to the caller.");
        ApplyResponseDescription(operation, "400", "The request body contains an unsupported property, an invalid id, or an invalid text filter.");
        ApplyResponseDescription(operation, "401", "The caller did not provide a valid JWT access token.");
        ApplyResponseDescription(operation, "403", "The caller is authenticated but does not have the required role.");
        ApplyResponseDescription(operation, "500", "The middleware server could not fetch owner data.");
    }

    private static void ApplyResponseDescription(OpenApiOperation operation, string statusCode, string description)
    {
        if (operation.Responses?.TryGetValue(statusCode, out IOpenApiResponse? response) == true)
        {
            response.Description = description;
        }
    }

    private static string OwnerEndpointDescription => $$"""
Returns owners visible to the authenticated caller. Use this endpoint when external systems need owner metadata, owner lifecycle state, or the detailed owner fields used by modelling and recertification workflows.

Requires one of the roles `admin`, `auditor`, or `modeller`. `modeller` callers only receive owners listed in their `x-hasura-editable-owners` JWT claim. `admin` and `auditor` callers are not restricted by that claim.

All request fields are optional and filters are combined with logical AND. Unknown JSON properties are rejected with `400 Bad Request`.

Request body examples:

{{CreateJsonCodeBlocks(CreateOwnerRequestExamples())}}

Response example:

{{CreateJsonCodeBlock(CreateOwnerResponseExamples())}}

Field behavior:

- `ownerId` and `ownerLifecycleStateId` must be greater than `0` when supplied.
- `name` and `appIdExternal` are case-insensitive text filters.
- Plain text filters are matched as contains.
- `*` matches any character sequence and `?` matches one character.
- Literal `%`, `_`, and `\` characters are matched verbatim.
- Text filters must not exceed {{OwnersController.kMaxFilterTextLength}} characters and must not contain control characters.
- `showDetails` defaults to `false`. Set it to `true` to include responsibles, tenant id, recertification fields, criticality, lifecycle state id, import source, decommission date, and additional info.
- `showOnlyActiveState` defaults to `true`. Owners whose lifecycle state is inactive are excluded; owners without any lifecycle state are still returned. Set it to `false` to include inactive lifecycle states.

Response behavior:

- The response body is always a JSON array.
- The `type` field is derived from `appIdExternal`: `standard` when the external app id contains `app` case-insensitively, otherwise `infrastructure`.
- Detail fields are omitted unless `showDetails` is `true`.
- Nullable detail fields are omitted when their value is empty.
""";

    private const string OwnerRequestDescription = """
Optional owner lookup filters. Submit `{}` or an empty body to use the default filter behavior. Set `showDetails` to `true` to return the complete owner response shape.
""";

    private static JsonSerializerOptions CreateDocumentationJsonSerializerOptions()
    {
        JsonSerializerOptions options = ApiDocumentationJsonOptions.CreateSerializerOptions();
        options.WriteIndented = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        return options;
    }

    private static List<GetOwnersRequest> CreateOwnerRequestExamples()
    {
        return
        [
            new(),
            new()
            {
                Active = true,
                OwnerLifeCycleStateId = 1
            },
            new()
            {
                OwnerId = 42
            },
            new()
            {
                Name = "Finance*",
                AppIdExternal = "APP-?"
            },
            new()
            {
                ShowDetails = true
            }
        ];
    }

    private static List<GetOwnerResponse> CreateOwnerResponseExamples()
    {
        return
        [
            OwnersController.ToResponse(
                new FwoOwner
                {
                    Id = 42,
                    Name = "Finance Portal",
                    ExtAppId = "APP-4711",
                    OwnerLifeCycleState = new OwnerLifeCycleState
                    {
                        Id = 1,
                        Name = "Active"
                    }
                },
                false),
            OwnersController.ToResponse(
                new FwoOwner
                {
                    Id = 43,
                    Name = "Finance Network",
                    ExtAppId = "NET-4712"
                },
                false)
        ];
    }

    private static string CreateJsonCodeBlocks(IEnumerable<object> examples)
    {
        return string.Join(Environment.NewLine + Environment.NewLine, examples.Select(CreateJsonCodeBlock));
    }

    private static string CreateJsonCodeBlock(object example)
    {
        return $"""
```json
{JsonSerializer.Serialize(example, DocumentationJsonSerializerOptions)}
```
""";
    }
}
