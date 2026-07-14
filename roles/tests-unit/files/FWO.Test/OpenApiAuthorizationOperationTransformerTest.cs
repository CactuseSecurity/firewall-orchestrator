using FWO.Middleware.Server.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests that the bearer security requirement is only documented for endpoints that require authorization.
/// </summary>
[TestFixture]
public class OpenApiAuthorizationOperationTransformerTest
{
    /// <summary>
    /// Authorized endpoints must advertise the bearer security requirement.
    /// </summary>
    [Test]
    public void Apply_WithAuthorizeMetadata_AddsBearerRequirement()
    {
        OpenApiOperation operation = ApplyTransformer(new AuthorizeAttribute());

        Assert.That(operation.Security, Is.Not.Null);
        Assert.That(operation.Security, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Anonymous endpoints with no authorization metadata must not advertise a bearer requirement.
    /// </summary>
    [Test]
    public void Apply_WithoutAuthorizeMetadata_AddsNoRequirement()
    {
        OpenApiOperation operation = ApplyTransformer();

        Assert.That(operation.Security, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Endpoints explicitly opting out via AllowAnonymous must not advertise a bearer requirement.
    /// </summary>
    [Test]
    public void Apply_WithAllowAnonymousMetadata_AddsNoRequirement()
    {
        OpenApiOperation operation = ApplyTransformer(new AuthorizeAttribute(), new AllowAnonymousAttribute());

        Assert.That(operation.Security, Is.Null.Or.Empty);
    }

    private static OpenApiOperation ApplyTransformer(params object[] endpointMetadata)
    {
        OpenApiOperation operation = new();
        OpenApiAuthorizationOperationTransformer.ApplyAuthorizationRequirement(operation, endpointMetadata);
        return operation;
    }
}
