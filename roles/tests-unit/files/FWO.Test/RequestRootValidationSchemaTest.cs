using FWO.Middleware.Server.Requests;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests that named schema key collections preserve the visibility and paging definitions.
/// </summary>
[TestFixture]
internal class RequestRootValidationSchemaTest
{
    private static readonly List<string> kPagedKeyNames = ["filter", "limit", "offset"];
    private static readonly List<string> kVisibilityKeyNames = ["filter"];

    /// <summary>
    /// Paging schemas must preserve the endpoint-specific maximum and all supported keys.
    /// </summary>
    [TestCase(FlowCatalogPaging.kMaxObjectLimit)]
    [TestCase(FlowCatalogPaging.kMaxGroupLimit)]
    public void PagedVisibilitySchemaPreservesDefinitions(int maxLimit)
    {
        RequestRootValidationSchema schema = RequestRootValidationSchema.ForPagedVisibleInRequest("catalog", maxLimit);

        Assert.Multiple(() =>
        {
            Assert.That(schema.EndpointName, Is.EqualTo("catalog"));
            Assert.That(schema.AllowedKeys.Select(key => key.JsonName), Is.EqualTo(kPagedKeyNames));
            Assert.That(schema.AllowedKeys.Single(key => key.JsonName == "limit").Description, Is.EqualTo(FlowCatalogPaging.DescribeLimit(maxLimit)));
            Assert.That(schema.AllowedKeys.Single(key => key.JsonName == "offset").Description, Is.EqualTo(FlowCatalogPaging.kOffsetDescription));
        });
    }

    /// <summary>
    /// Creating a paged schema must not add paging keys to the shared visibility-only definition.
    /// </summary>
    [Test]
    public void PagedSchemaDoesNotChangeVisibilityOnlySchema()
    {
        RequestRootValidationSchema.ForPagedVisibleInRequest("catalog", FlowCatalogPaging.kMaxObjectLimit);

        RequestRootValidationSchema schema = RequestRootValidationSchema.ForVisibleInRequest("lookup");

        Assert.That(schema.AllowedKeys.Select(key => key.JsonName), Is.EqualTo(kVisibilityKeyNames));
    }
}
