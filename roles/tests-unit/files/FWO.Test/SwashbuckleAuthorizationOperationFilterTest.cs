using FWO.Middleware.Server.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using NUnit.Framework;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace FWO.Test;

/// <summary>
/// Tests that the bearer security requirement is only documented for endpoints that require authorization.
/// </summary>
[TestFixture]
public class SwashbuckleAuthorizationOperationFilterTest
{
    /// <summary>
    /// Authorized endpoints must advertise the bearer security requirement.
    /// </summary>
    [Test]
    public void Apply_WithAuthorizeMetadata_AddsBearerRequirement()
    {
        OpenApiOperation operation = ApplyFilter(new AuthorizeAttribute());

        Assert.That(operation.Security, Is.Not.Null);
        Assert.That(operation.Security, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Anonymous endpoints (no authorization metadata) must not advertise a bearer requirement.
    /// </summary>
    [Test]
    public void Apply_WithoutAuthorizeMetadata_AddsNoRequirement()
    {
        OpenApiOperation operation = ApplyFilter();

        Assert.That(operation.Security, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Endpoints explicitly opting out via AllowAnonymous must not advertise a bearer requirement.
    /// </summary>
    [Test]
    public void Apply_WithAllowAnonymousMetadata_AddsNoRequirement()
    {
        OpenApiOperation operation = ApplyFilter(new AuthorizeAttribute(), new AllowAnonymousAttribute());

        Assert.That(operation.Security, Is.Null.Or.Empty);
    }

    private static OpenApiOperation ApplyFilter(params object[] endpointMetadata)
    {
        OpenApiOperation operation = new();
        ApiDescription apiDescription = new()
        {
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata }
        };
        MethodInfo methodInfo = typeof(SwashbuckleAuthorizationOperationFilterTest)
            .GetMethod(nameof(ApplyFilter), BindingFlags.NonPublic | BindingFlags.Static)!;
        OperationFilterContext context = new(apiDescription, null!, null!, new OpenApiDocument(), methodInfo);

        new SwashbuckleAuthorizationOperationFilter().Apply(operation, context);
        return operation;
    }
}
