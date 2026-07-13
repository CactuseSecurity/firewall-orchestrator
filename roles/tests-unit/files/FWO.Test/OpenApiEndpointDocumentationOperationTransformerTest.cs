using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

namespace FWO.Test;

/// <summary>
/// Tests endpoint documentation generated for Scalar.
/// </summary>
[TestFixture]
public class OpenApiEndpointDocumentationOperationTransformerTest
{
    /// <summary>
    /// Verifies owner documentation is applied through the already registered OpenAPI transformer.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_AddsOwnerDescriptionAndResponseDescriptions()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiOperationTransformerContext context = CreateContext(new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(OwnersController).GetTypeInfo(),
            ActionName = nameof(OwnersController.Get)
        });
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(operation.Description, Does.Contain("Request body examples"));
            Assert.That(operation.Description, Does.Contain("```json"));
            Assert.That(operation.Description, Does.Contain("\"showDetails\": true"));
            Assert.That(operation.Description, Does.Contain("Response behavior"));
            Assert.That(operation.Description, Does.Contain(GetMaxFilterTextLength().ToString()));
            Assert.That(operation.RequestBody!.Description, Does.Contain("Optional owner lookup filters"));
            Assert.That(operation.Responses!["200"].Description, Does.Contain("JSON array"));
            Assert.That(operation.Responses["400"].Description, Does.Contain("unsupported property"));
        });
    }

    /// <summary>
    /// Verifies owner documentation examples are rendered as readable JSON code blocks.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_RendersFormattedJsonExamples()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, CreateOwnerContext(), CancellationToken.None);
        string description = operation.Description!.ReplaceLineEndings("\n");

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("""
```json
{
  "ownerLifecycleStateId": 1,
  "active": true
}
```
"""));
            Assert.That(description, Does.Contain("""
```json
[
  {
    "id": 42,
    "name": "Finance Portal",
    "appIdExternal": "APP-4711",
    "type": "standard",
"""));
            Assert.That(description, Does.Not.Contain("{\"active\":true"));
            Assert.That(description, Does.Not.Contain("{\"id\":42"));
        });
    }

    /// <summary>
    /// Verifies documented owner roles stay aligned with the controller authorization attribute.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_DocumentsAuthorizedRoles()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiOperationTransformerContext context = CreateOwnerContext();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();
        string[] authorizedRoles = GetOwnerEndpointRoles();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            foreach (string role in authorizedRoles)
            {
                Assert.That(operation.Description, Does.Contain($"`{role}`"));
            }
        });
    }

    /// <summary>
    /// Verifies documented owner response descriptions cover the controller response metadata.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_DocumentsProducedStatusCodes()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiOperationTransformerContext context = CreateOwnerContext();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            foreach (int statusCode in GetOwnerEndpointStatusCodes())
            {
                string key = statusCode.ToString();
                Assert.That(operation.Responses, Does.ContainKey(key));
                Assert.That(operation.Responses![key].Description, Is.Not.Empty);
            }
        });
    }

    /// <summary>
    /// Verifies documented wildcard behavior stays aligned with generated GraphQL filters.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_DocumentsWildcardBehavior()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, CreateOwnerContext(), CancellationToken.None);

        string wildcardVariables = SerializeOwnerQueryVariables(new GetOwnersRequest
        {
            Name = "Finance*",
            AppIdExternal = "APP-?"
        });
        string containsVariables = SerializeOwnerQueryVariables(new GetOwnersRequest { Name = "Accounting" });
        string escapedVariables = SerializeOwnerQueryVariables(new GetOwnersRequest
        {
            Name = "APP_1",
            AppIdExternal = "50%"
        });

        Assert.Multiple(() =>
        {
            Assert.That(operation.Description, Does.Contain("Plain text filters are matched as contains."));
            Assert.That(containsVariables, Does.Contain("\"name\":{\"_ilike\":\"%Accounting%\"}"));
            Assert.That(operation.Description, Does.Contain("`*` matches any character sequence"));
            Assert.That(wildcardVariables, Does.Contain("\"name\":{\"_ilike\":\"Finance%\"}"));
            Assert.That(operation.Description, Does.Contain("`?` matches one character"));
            Assert.That(wildcardVariables, Does.Contain("\"app_id_external\":{\"_ilike\":\"APP-_\"}"));
            Assert.That(operation.Description, Does.Contain("Literal `%`, `_`, and `\\` characters are matched verbatim."));
            Assert.That(escapedVariables, Does.Contain($"\"name\":{{\"_ilike\":{JsonSerializer.Serialize("%APP\\_1%")}}}"));
            Assert.That(escapedVariables, Does.Contain($"\"app_id_external\":{{\"_ilike\":{JsonSerializer.Serialize("%50\\%%")}}}"));
        });
    }

    /// <summary>
    /// Verifies documented active lifecycle defaults stay aligned with generated GraphQL filters.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_DocumentsActiveLifecycleDefault()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, CreateOwnerContext(), CancellationToken.None);

        string defaultVariables = SerializeOwnerQueryVariables(new GetOwnersRequest());
        string disabledVariables = SerializeOwnerQueryVariables(new GetOwnersRequest { ShowOnlyActiveState = false });

        Assert.Multiple(() =>
        {
            Assert.That(operation.Description, Does.Contain("`showOnlyActiveState` defaults to `true`"));
            Assert.That(defaultVariables, Does.Contain("\"owner_lifecycle_state\":{\"active_state\":{\"_eq\":true}}"));
            Assert.That(defaultVariables, Does.Contain("\"owner_lifecycle_state_id\":{\"_is_null\":true}"));
            Assert.That(operation.Description, Does.Contain("Set it to `false` to include inactive lifecycle states."));
            Assert.That(disabledVariables, Does.Not.Contain("active_state"));
        });
    }

    /// <summary>
    /// Verifies documented showDetails behavior stays aligned with response mapping.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithOwnerEndpoint_DocumentsDetailFieldBehavior()
    {
        OpenApiOperation operation = CreateOperation();
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, CreateOwnerContext(), CancellationToken.None);

        string coreResponse = JsonSerializer.Serialize(MapOwnerResponse(false));
        string detailResponse = JsonSerializer.Serialize(MapOwnerResponse(true));

        Assert.Multiple(() =>
        {
            Assert.That(operation.Description, Does.Contain("`showDetails` defaults to `false`"));
            Assert.That(operation.Description, Does.Contain("Detail fields are omitted unless `showDetails` is `true`."));
            Assert.That(coreResponse, Does.Not.Contain("\"tenantId\""));
            Assert.That(coreResponse, Does.Not.Contain("\"criticality\""));
            Assert.That(detailResponse, Does.Contain("\"tenantId\""));
            Assert.That(detailResponse, Does.Contain("\"criticality\""));
        });
    }

    /// <summary>
    /// Verifies unrelated endpoints are ignored.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithUnrelatedEndpoint_LeavesDescriptionUntouched()
    {
        OpenApiOperation operation = CreateOperation();
        operation.Description = "Existing description.";
        OpenApiOperationTransformerContext context = CreateContext(new ControllerActionDescriptor(), "api/flow/get-address-objects");
        OpenApiApiExampleOperationTransformer transformer = CreateTransformer();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.That(operation.Description, Is.EqualTo("Existing description."));
    }

    /// <summary>
    /// Verifies endpoint documentation providers are discovered without per-endpoint Program.cs registration.
    /// </summary>
    [Test]
    public void AddApiExamples_RegistersEndpointDocumentationProviders()
    {
        ServiceCollection services = new();
        services.AddApiExamples();
        ServiceProvider provider = services.BuildServiceProvider();

        IEnumerable<IOpenApiEndpointDocumentationProvider> providers = provider.GetServices<IOpenApiEndpointDocumentationProvider>();

        Assert.That(providers, Has.One.InstanceOf<OpenApiOwnerDocumentationProvider>());
    }

    /// <summary>
    /// Verifies request body examples from the catalog are applied to all media types.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithBodyParameter_AppliesRequestExample()
    {
        OpenApiOperation operation = CreateOperation();
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() }
        };
        OpenApiOperationTransformerContext context = CreateContext(new ControllerActionDescriptor(), "api/flow/get-owners");
        context.Description.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Source = BindingSource.Body,
            Type = typeof(GetOwnersRequest)
        });
        OpenApiApiExampleOperationTransformer transformer = CreateTransformerWithExamples();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        System.Text.Json.Nodes.JsonNode? example = operation.RequestBody.Content!["application/json"].Example;
        Assert.Multiple(() =>
        {
            Assert.That(example, Is.Not.Null);
            Assert.That(example!.ToJsonString(), Does.Contain("\"appIdExternal\":\"APP-42\""));
            Assert.That(example.ToJsonString(), Does.Contain("\"showDetails\":true"));
        });
    }

    /// <summary>
    /// Verifies response examples are applied per status code and void or unknown codes are skipped.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithSupportedResponseTypes_AppliesResponseExamples()
    {
        OpenApiOperation operation = CreateOperation();
        operation.Responses!["200"] = new OpenApiResponse
        {
            Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new OpenApiMediaType() }
        };
        OpenApiOperationTransformerContext context = CreateContext(new ControllerActionDescriptor(), "api/flow/get-owners");
        context.Description.SupportedResponseTypes.Add(new ApiResponseType { Type = typeof(List<GetOwnerResponse>), StatusCode = 200 });
        context.Description.SupportedResponseTypes.Add(new ApiResponseType { Type = typeof(void), StatusCode = 401 });
        context.Description.SupportedResponseTypes.Add(new ApiResponseType { Type = typeof(GetOwnerResponse), StatusCode = 400 });
        context.Description.SupportedResponseTypes.Add(new ApiResponseType { Type = typeof(GetOwnerResponse), StatusCode = 404 });
        OpenApiApiExampleOperationTransformer transformer = CreateTransformerWithExamples();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        System.Text.Json.Nodes.JsonNode? example = operation.Responses["200"].Content!["application/json"].Example;
        Assert.Multiple(() =>
        {
            Assert.That(example, Is.Not.Null);
            Assert.That(example!.ToJsonString(), Does.StartWith("["));
            Assert.That(example.ToJsonString(), Does.Contain("\"name\":\"Payments\""));
        });
    }

    /// <summary>
    /// Verifies operations without responses or request body content are left untouched.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithoutResponsesAndBodyContent_DoesNotThrow()
    {
        OpenApiOperation operation = new() { Responses = null, RequestBody = null };
        OpenApiOperationTransformerContext context = CreateContext(new ControllerActionDescriptor(), "api/flow/get-owners");
        context.Description.ParameterDescriptions.Add(new ApiParameterDescription
        {
            Source = BindingSource.Body,
            Type = typeof(GetOwnersRequest)
        });
        OpenApiApiExampleOperationTransformer transformer = CreateTransformerWithExamples();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.That(operation.Responses, Is.Null);
    }

    private static OpenApiApiExampleOperationTransformer CreateTransformerWithExamples()
    {
        ServiceCollection services = new();
        services.AddOptions<JsonOptions>().Configure(ApiDocumentationJsonOptions.Configure);
        services.AddApiExamples();
        ServiceProvider provider = services.BuildServiceProvider();

        return new OpenApiApiExampleOperationTransformer(
            provider.GetRequiredService<ApiExampleCatalog>(),
            provider.GetRequiredService<IOptions<JsonOptions>>(),
            provider.GetServices<IOpenApiEndpointDocumentationProvider>());
    }

    private static OpenApiApiExampleOperationTransformer CreateTransformer()
    {
        JsonOptions jsonOptions = new();
        ApiDocumentationJsonOptions.Configure(jsonOptions);

        return new OpenApiApiExampleOperationTransformer(
            new ApiExampleCatalog([], new ApiExampleObjectFactory()),
            Options.Create(jsonOptions),
            [new OpenApiOwnerDocumentationProvider()]);
    }

    private static OpenApiOperationTransformerContext CreateOwnerContext()
    {
        return CreateContext(new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(OwnersController).GetTypeInfo(),
            ActionName = nameof(OwnersController.Get)
        });
    }

    private static OpenApiOperation CreateOperation()
    {
        return new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody(),
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse(),
                ["400"] = new OpenApiResponse(),
                ["401"] = new OpenApiResponse(),
                ["403"] = new OpenApiResponse(),
                ["500"] = new OpenApiResponse()
            }
        };
    }

    private static OpenApiOperationTransformerContext CreateContext(ControllerActionDescriptor actionDescriptor, string? relativePath = null)
    {
        return new OpenApiOperationTransformerContext
        {
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
            Description = new ApiDescription
            {
                ActionDescriptor = actionDescriptor,
                RelativePath = relativePath
            }
        };
    }

    private static string[] GetOwnerEndpointRoles()
    {
        MethodInfo getMethod = typeof(OwnersController).GetMethod(nameof(OwnersController.Get))!;
        AuthorizeAttribute authorize = getMethod.GetCustomAttribute<AuthorizeAttribute>()!;
        return authorize.Roles!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<int> GetOwnerEndpointStatusCodes()
    {
        MethodInfo getMethod = typeof(OwnersController).GetMethod(nameof(OwnersController.Get))!;
        return getMethod.GetCustomAttributes<ProducesResponseTypeAttribute>()
            .Select(attribute => attribute.StatusCode);
    }

    private static int GetMaxFilterTextLength()
    {
        FieldInfo maxFilterTextLength = typeof(OwnersController).GetField("kMaxFilterTextLength", BindingFlags.NonPublic | BindingFlags.Static)!
            ?? throw new InvalidOperationException("Owner filter length constant is missing.");
        return (int)maxFilterTextLength.GetRawConstantValue()!;
    }

    private static string SerializeOwnerQueryVariables(GetOwnersRequest request)
    {
        MethodInfo buildQueryVariables = typeof(OwnersController).GetMethod("BuildQueryVariables", BindingFlags.NonPublic | BindingFlags.Static)!
            ?? throw new InvalidOperationException("Owner query variable builder is missing.");

        object variables = buildQueryVariables.Invoke(null, [request, new ClaimsPrincipal(new ClaimsIdentity())])!
            ?? throw new InvalidOperationException("Owner query variable builder returned null.");

        return JsonSerializer.Serialize(variables);
    }

    private static object MapOwnerResponse(bool showDetails)
    {
        MethodInfo toResponse = typeof(OwnersController).GetMethod("ToResponse", BindingFlags.NonPublic | BindingFlags.Static)!
            ?? throw new InvalidOperationException("Owner response mapper is missing.");

        object response = toResponse.Invoke(null,
        [
            new FWO.Data.FwoOwner
            {
                Id = 1,
                Name = "Application",
                ExtAppId = "APP-0001",
                TenantId = 7,
                Criticality = "high"
            },
            showDetails
        ]) ?? throw new InvalidOperationException("Owner response mapper returned null.");

        return response;
    }
}
