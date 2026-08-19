using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class RequestValidatorTest
{
    private static readonly string[] kUnknownTypoError = new string[] { "Unknown field 'typo'." };
    private static readonly string[] kUnknownFilterVisibleInRequestTypoError = new string[] { "Unknown field 'filter.visibleInRequestTypo'." };
    private static readonly string[] kMissingIpStartError = new string[] { "Required field 'ipStart' is missing." };
    private static readonly string[] kMissingIpEndError = new string[] { "Required field 'ipEnd' is missing." };
    private static readonly string[] kMissingOptionsError = new string[] { "Required field 'options' is missing." };
    private static readonly string[] kMissingAdditionalDataRootError = new string[] { "Request object '$' must implement IRequestWithAdditionalData so unknown fields can be validated." };
    private static readonly string[] kMissingAdditionalDataChildError = new string[] { "Request object 'child' must implement IRequestWithAdditionalData so unknown fields can be validated." };
    private static readonly string[] kUnknownListItemTypoError = new string[] { "Unknown field 'items[0].typo'." };
    private static readonly string[] kCollectedShapeErrorKeys = new string[] { "filter.typo", "unexpected", "ipStart", "ipEnd" };
    private static readonly string[] kUnknownFilterTypoError = new string[] { "Unknown field 'filter.typo'." };
    private static readonly string[] kUnknownUnexpectedError = new string[] { "Unknown field 'unexpected'." };

    [Test]
    public void Validate_AllowsEmptyObjectWhenNoFieldsAreRequired()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("empty")
            .ObjectRoot()
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest"));

        RequestValidationErrors errors = RequestValidator.Validate(new TestRequest(), schema);

        Assert.That(errors.HasErrors, Is.False);
    }

    [Test]
    public void Validate_AllowsOptionalNestedObject()
    {
        RequestValidationSchema schema = BuildFilterSchema();
        TestRequest request = Deserialize<TestRequest>(
            """
            {
              "filter": {
                "visibleInRequest": true
              }
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.HasErrors, Is.False);
    }

    [Test]
    public void Validate_RejectsUnknownRootKey()
    {
        RequestValidationSchema schema = BuildFilterSchema();
        TestRequest request = Deserialize<TestRequest>(
            """
            {
              "typo": true
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["typo"], Is.EqualTo(kUnknownTypoError));
    }

    [Test]
    public void Validate_RejectsUnknownNestedKey()
    {
        RequestValidationSchema schema = BuildFilterSchema();
        TestRequest request = Deserialize<TestRequest>(
            """
            {
              "filter": {
                "visibleInRequestTypo": true
              }
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["filter.visibleInRequestTypo"], Is.EqualTo(kUnknownFilterVisibleInRequestTypoError));
    }

    [Test]
    public void Validate_RejectsMissingRequiredField()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("lookup")
            .ObjectRoot()
            .RequiredString("ipStart")
            .RequiredString("ipEnd");
        TestLookupRequest request = Deserialize<TestLookupRequest>(
            """
            {
              "ipEnd": "10.0.0.2"
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["ipStart"], Is.EqualTo(kMissingIpStartError));
    }

    [Test]
    public void Validate_RejectsNullRequiredObject()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("options")
            .ObjectRoot()
            .RequiredObject("options", options => options
                .OptionalInt("limit"));
        TestOptionsRequest request = Deserialize<TestOptionsRequest>(
            """
            {
              "options": null
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["options"], Is.EqualTo(kMissingOptionsError));
    }

    [Test]
    public void Validate_AllowsNullOptionalObject()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("filter")
            .ObjectRoot()
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest"));
        TestRequest request = Deserialize<TestRequest>(
            """
            {
              "filter": null
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.HasErrors, Is.False);
    }

    [Test]
    public void Validate_RejectsRequestObjectWithoutAdditionalDataContract()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("bad")
            .ObjectRoot()
            .OptionalString("name");

        RequestValidationErrors errors = RequestValidator.Validate(new MissingAdditionalDataRequest(), schema);

        Assert.That(errors.ToDictionary()[RequestFieldPath.Root], Is.EqualTo(kMissingAdditionalDataRootError));
    }

    [Test]
    public void Validate_RejectsNestedObjectWithoutAdditionalDataContract()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("bad")
            .ObjectRoot()
            .OptionalObject("child", child => child
                .OptionalString("name"));
        MissingNestedAdditionalDataRequest request = new()
        {
            Child = new MissingAdditionalDataRequest()
        };

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["child"], Is.EqualTo(kMissingAdditionalDataChildError));
    }

    [Test]
    public void Validate_ReportsUnknownListItemKeysWithIndexPaths()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("items")
            .ObjectRoot()
            .OptionalList("items", item => item
                .OptionalString("name"));
        TestListRequest request = Deserialize<TestListRequest>(
            """
            {
              "items": [
                {
                  "name": "first",
                  "typo": true
                }
              ]
            }
            """);

        RequestValidationErrors errors = RequestValidator.Validate(request, schema);

        Assert.That(errors.ToDictionary()["items[0].typo"], Is.EqualTo(kUnknownListItemTypoError));
    }

    [Test]
    public void Validate_CollectsMultipleShapeErrors()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("lookup")
            .ObjectRoot()
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest"))
            .RequiredString("ipStart")
            .RequiredString("ipEnd");
        TestLookupRequest request = Deserialize<TestLookupRequest>(
            """
            {
              "filter": {
                "typo": true
              },
              "unexpected": false
            }
            """);

        Dictionary<string, string[]> errors = RequestValidator.Validate(request, schema).ToDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(errors.Keys, Is.EquivalentTo(kCollectedShapeErrorKeys));
            Assert.That(errors["filter.typo"], Is.EqualTo(kUnknownFilterTypoError));
            Assert.That(errors["unexpected"], Is.EqualTo(kUnknownUnexpectedError));
            Assert.That(errors["ipStart"], Is.EqualTo(kMissingIpStartError));
            Assert.That(errors["ipEnd"], Is.EqualTo(kMissingIpEndError));
        });
    }

    [Test]
    public void TryValidate_ReturnsValidationProblemDetails()
    {
        RequestValidationSchema schema = RequestValidationSchema.Endpoint("lookup")
            .ObjectRoot()
            .RequiredString("ipStart");

        bool valid = RequestValidator.TryValidate(new TestLookupRequest(), schema, out ActionResult? errorResult);
        ValidationProblemDetails problemDetails = (ValidationProblemDetails)((BadRequestObjectResult)errorResult!).Value!;

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(problemDetails.Status, Is.EqualTo(400));
            Assert.That(problemDetails.Errors["ipStart"], Is.EqualTo(kMissingIpStartError));
        });
    }

    private static RequestValidationSchema BuildFilterSchema()
    {
        return RequestValidationSchema.Endpoint("filter")
            .ObjectRoot()
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest"));
    }

    private static TRequest Deserialize<TRequest>(string json)
    {
        return JsonSerializer.Deserialize<TRequest>(json)!;
    }

    private sealed class TestRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("filter")]
        public TestFilter? Filter { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestFilter : IRequestWithAdditionalData
    {
        [JsonPropertyName("visibleInRequest")]
        public bool? VisibleInRequest { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestLookupRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("filter")]
        public TestFilter? Filter { get; set; }

        [JsonPropertyName("ipStart")]
        public string? IpStart { get; set; }

        [JsonPropertyName("ipEnd")]
        public string? IpEnd { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestOptionsRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("options")]
        public TestOptions? Options { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestOptions : IRequestWithAdditionalData
    {
        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestListRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("items")]
        public List<TestListItem>? Items { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class TestListItem : IRequestWithAdditionalData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class MissingNestedAdditionalDataRequest : IRequestWithAdditionalData
    {
        [JsonPropertyName("child")]
        public MissingAdditionalDataRequest? Child { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    private sealed class MissingAdditionalDataRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
