using FWO.Middleware.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using NUnit.Framework;
using System.Net;

namespace FWO.Test;

/// <summary>
/// Tests legacy Swagger route redirects used by middleware readiness checks.
/// </summary>
[TestFixture]
internal class SwaggerRedirectMiddlewareTest
{
    private const string kApiDocsRoute = "/api-docs";

    /// <summary>
    /// Verifies legacy Swagger paths redirect without ambiguous endpoint routing.
    /// </summary>
    [TestCase("/swagger")]
    [TestCase("/swagger/")]
    [TestCase("/swagger/index.html")]
    public async Task LegacySwaggerPathRedirectsToOpenApiDocument(string requestPath)
    {
        await using WebApplication app = CreateTestApplication();
        await app.StartAsync();

        using HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(requestPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.OriginalString, Is.EqualTo(kApiDocsRoute));
    }

    /// <summary>
    /// Verifies routes that merely start with the same letters are not redirected.
    /// </summary>
    [Test]
    public async Task NonSwaggerPathContinuesToNextEndpoint()
    {
        await using WebApplication app = CreateTestApplication();
        await app.StartAsync();

        using HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/swagger-status");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Creates a minimal test application with the Swagger redirect middleware.
    /// </summary>
    private static WebApplication CreateTestApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        WebApplication app = builder.Build();
        app.UseSwaggerRedirect(kApiDocsRoute);
        app.MapGet("/swagger-status", () => Results.Ok());

        return app;
    }
}
