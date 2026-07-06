using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test;

/// <summary>
/// Tests API documentation examples.
/// </summary>
[TestFixture]
public class ApiExampleCatalogTest
{
    private ApiExampleCatalog catalog = null!;
    private JsonSerializerOptions serializerOptions = null!;

    /// <summary>
    /// Initializes API example test dependencies.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        ServiceCollection services = new();
        services.AddOptions<JsonOptions>().Configure(ApiDocumentationJsonOptions.Configure);
        services.AddApiExamples();
        ServiceProvider provider = services.BuildServiceProvider();
        catalog = provider.GetRequiredService<ApiExampleCatalog>();
        serializerOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
    }

    /// <summary>
    /// Verifies typed examples serialize with production JSON names.
    /// </summary>
    [Test]
    public void TypedExamplesUseProductionJsonPropertyNames()
    {
        Assert.That(catalog.TryGetExample(typeof(CreateRequestRequest), out object? example), Is.True);

        string json = JsonSerializer.Serialize(example, example!.GetType(), serializerOptions);

        Assert.That(json, Does.Contain("\"requestorName\""));
        Assert.That(json, Does.Contain("\"timeObjectId\""));
        Assert.That(json, Does.Not.Contain("\"RequestorName\""));
        Assert.That(json, Does.Not.Contain("\"TimeObjectId\""));
    }

    /// <summary>
    /// Verifies list responses are built from typed item examples.
    /// </summary>
    [Test]
    public void CatalogCreatesListExamplesFromItemProviders()
    {
        Assert.That(catalog.TryGetExample(typeof(List<GetOwnerResponse>), out object? example), Is.True);

        List<GetOwnerResponse> owners = (List<GetOwnerResponse>)example!;

        Assert.That(owners, Has.Count.EqualTo(1));
        Assert.That(owners[0].Name, Is.EqualTo("Payments"));
    }

    /// <summary>
    /// Verifies fallback examples still create real DTO instances.
    /// </summary>
    [Test]
    public void CatalogCreatesFallbackDtoInstances()
    {
        Assert.That(catalog.TryGetExample(typeof(GetPolicyIdsRequest), out object? example), Is.True);

        Assert.That(example, Is.TypeOf<GetPolicyIdsRequest>());
    }

    /// <summary>
    /// Verifies production JSON options keep explicit JSON property names.
    /// </summary>
    [Test]
    public void ProductionJsonOptionsRespectJsonPropertyNameAttributes()
    {
        GetOwnersRequest request = new()
        {
            OwnerLifeCycleStateId = 2,
            AppIdExternal = "APP-2",
            ShowOnlyActiveState = true
        };

        string json = JsonSerializer.Serialize(request, serializerOptions);

        Assert.That(json, Does.Contain("\"ownerLifecycleStateId\""));
        Assert.That(json, Does.Contain("\"appIdExternal\""));
        Assert.That(json, Does.Not.Contain("\"OwnerLifeCycleStateId\""));
    }
}
