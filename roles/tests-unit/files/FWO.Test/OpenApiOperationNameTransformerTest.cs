using FWO.Middleware.Server;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests OpenAPI operation name generation for Scalar API documentation.
/// </summary>
[TestFixture]
internal class OpenApiOperationNameTransformerTest
{
    /// <summary>
    /// Verifies controller actions receive readable operation IDs.
    /// </summary>
    [Test]
    public void CreateOperationId_WithControllerAction_ReturnsControllerAndActionName()
    {
        ApiDescription description = new()
        {
            ActionDescriptor = new ControllerActionDescriptor
            {
                ControllerName = "AuthenticationToken",
                ActionName = "GetTokenPair"
            }
        };

        string operationId = OpenApiOperationNameTransformer.CreateOperationId(description);

        Assert.That(operationId, Is.EqualTo("AuthenticationToken_GetTokenPair"));
    }

    /// <summary>
    /// Verifies non-controller endpoints still receive stable operation IDs.
    /// </summary>
    [Test]
    public void CreateOperationId_WithoutControllerAction_ReturnsMethodAndPathName()
    {
        ApiDescription description = new()
        {
            ActionDescriptor = new ActionDescriptor(),
            HttpMethod = "POST",
            RelativePath = "api/flow/get-address-objects/{ownerId}"
        };

        string operationId = OpenApiOperationNameTransformer.CreateOperationId(description);

        Assert.That(operationId, Is.EqualTo("POST_api_flow_get_address_objects_ownerId"));
    }

    /// <summary>
    /// Verifies real endpoint paths are used as display names.
    /// </summary>
    [Test]
    public void CreateEndpointPath_WithRelativePath_ReturnsAbsolutePath()
    {
        ApiDescription description = new()
        {
            RelativePath = "api/AuthenticationToken/GetTokenPair"
        };

        string endpointPath = OpenApiOperationNameTransformer.CreateEndpointPath(description);

        Assert.That(endpointPath, Is.EqualTo("/api/AuthenticationToken/GetTokenPair"));
    }

    /// <summary>
    /// Verifies endpoint paths are promoted to the prominent operation summary.
    /// </summary>
    [Test]
    public async Task TransformAsync_WithExistingSummary_PromotesEndpointPathAndPreservesSummaryAsDescription()
    {
        OpenApiOperation operation = new()
        {
            Summary = "Generates a new access and refresh token pair.",
            Description = "Returns token data."
        };
        OpenApiOperationTransformerContext context = new()
        {
            DocumentName = "v1",
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
            Description = new ApiDescription
            {
                ActionDescriptor = new ControllerActionDescriptor
                {
                    ControllerName = "AuthenticationToken",
                    ActionName = "GetTokenPair"
                },
                RelativePath = "api/AuthenticationToken/GetTokenPair"
            }
        };
        OpenApiOperationNameTransformer transformer = new();

        await transformer.TransformAsync(operation, context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(operation.OperationId, Is.EqualTo("AuthenticationToken_GetTokenPair"));
            Assert.That(operation.Summary, Is.EqualTo("/api/AuthenticationToken/GetTokenPair"));
            Assert.That(operation.Description, Is.EqualTo("Generates a new access and refresh token pair.\n\nReturns token data."));
        });
    }
}
