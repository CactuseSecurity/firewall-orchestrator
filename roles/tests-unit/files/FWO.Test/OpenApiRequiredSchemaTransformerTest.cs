using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the schema transformer that documents a property as required without letting the
/// deserializer enforce it, and the request contract that depends on it.
/// </summary>
[TestFixture]
internal class OpenApiRequiredSchemaTransformerTest
{
    [Test]
    public async Task TicketIdIsRequiredInTheGeneratedSchema()
    {
        OpenApiSchema schema = await TransformSchemaOf<GetAuditProofCriticalChangesRequest>();

        Assert.That(schema.Required, Is.Not.Null.And.Contains("ticketId"));
    }

    [Test]
    public async Task OptionalKeysStayOutOfTheRequiredList()
    {
        OpenApiSchema requestSchema = await TransformSchemaOf<GetAuditProofCriticalChangesRequest>();
        OpenApiSchema filterSchema = await TransformSchemaOf<AuditProofCriticalChangeFilter>();

        Assert.Multiple(() =>
        {
            Assert.That(requestSchema.Required, Does.Not.Contain("options"));
            Assert.That(filterSchema.Required ?? new HashSet<string>(), Is.Empty);
        });
    }

    [Test]
    public async Task TheRequiredMarkerUsesTheJsonNameNotTheClrName()
    {
        OpenApiSchema schema = await TransformSchemaOf<RenamedPropertyProbe>();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Required, Does.Contain("wire_name"));
            Assert.That(schema.Required, Does.Not.Contain(nameof(RenamedPropertyProbe.ClrName)));
        });
    }

    [Test]
    public async Task AnUnannotatedTypeIsLeftUntouched()
    {
        OpenApiSchema schema = await TransformSchemaOf<UnannotatedProbe>();

        Assert.That(schema.Required ?? new HashSet<string>(), Is.Empty);
    }

    [Test]
    public void TheAttributeDoesNotMakeTheDeserializerRejectTheMissingKey()
    {
        // The whole point of the attribute: documentation only. If it ever started enforcing, an
        // omitted ticketId would throw here instead of reaching the aggregating validator.
        GetAuditProofCriticalChangesRequest request =
            JsonSerializer.Deserialize<GetAuditProofCriticalChangesRequest>("{\"options\":{}}")!;

        Assert.That(request.TicketId, Is.Null);
    }

    private static async Task<OpenApiSchema> TransformSchemaOf<TRequest>()
    {
        OpenApiSchema schema = new();
        OpenApiRequiredSchemaTransformer transformer = new();
        await transformer.TransformAsync(schema, CreateContext(typeof(TRequest)), CancellationToken.None);
        return schema;
    }

    private static OpenApiSchemaTransformerContext CreateContext(Type type)
    {
        JsonTypeInfo typeInfo = JsonSerializerOptions.Default.GetTypeInfo(type);
        return new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            JsonTypeInfo = typeInfo,
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = new EmptyServiceProvider()
        };
    }

    private sealed class RenamedPropertyProbe
    {
        [OpenApiRequired]
        [JsonPropertyName("wire_name")]
        public long? ClrName { get; set; }
    }

    private sealed class UnannotatedProbe
    {
        [JsonPropertyName("plain")]
        public long? Plain { get; set; }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
