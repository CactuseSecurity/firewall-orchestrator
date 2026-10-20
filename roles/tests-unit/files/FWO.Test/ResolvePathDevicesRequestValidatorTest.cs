using System.Text;
using System.Text.Json;
using FWO.Basics;
using FWO.Data;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the request contract of the resolvePathDevices endpoint: defaults, the source and destination lists,
/// the algorithm and matrix options including the configured defaults they fall back to, the response filter,
/// and the aggregation of every error of one request.
/// </summary>
[TestFixture]
[Parallelizable]
internal class ResolvePathDevicesRequestValidatorTest
{
    private const long kNetworkZoneTree = GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree;
    private const long kNone = GlobalConst.kPathAnalysisAlgorithmNone;
    private const int kDesignatedMatrixId = 7;
    private const int kNoDesignatedMatrix = 0;

    private const string kValidLists =
        "\"sources\":[{\"ipStart\":\"10.1.0.1\",\"ipEnd\":\"10.1.0.1\"}],\"destinations\":[{\"ipNetwork\":\"10.2.0.0/24\"}]";

    /// <summary>Verifies a request with only the required keys passes when the defaults are usable.</summary>
    [Test]
    public void Validate_MinimalRequestWithUsableDefaults_HasNoErrors()
    {
        RequestValidationErrorResponse result = Validate(Request());

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies a request using every option passes.</summary>
    [Test]
    public void Validate_RequestWithEveryOption_HasNoErrors()
    {
        RequestValidationErrorResponse result = Validate(
            WithOptions("""{"algorithm":"networkZoneTree","matrixId":3,"filter":{"id":4,"name":"fw-core"}}"""));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies an omitted, a null and an empty options object all yield the empty default.</summary>
    [TestCase("")]
    [TestCase(",\"options\":null")]
    [TestCase(",\"options\":{}")]
    public void Options_OmittedNullOrEmpty_DefaultToEmptyObject(string optionsJson)
    {
        ResolvePathDevicesRequest request = Deserialize(Request(optionsJson));

        Assert.Multiple(() =>
        {
            Assert.That(request.Options, Is.Not.Null);
            Assert.That(request.Options.Algorithm, Is.Null);
            Assert.That(request.Options.MatrixId, Is.Null);
            Assert.That(request.Options.Filter, Is.Null);
        });
    }

    /// <summary>Verifies a missing body is rejected on the request root and names the valid root keys.</summary>
    [Test]
    public void Validate_WithoutBody_ReportsRootError()
    {
        RequestValidationErrorResponse result = ResolvePathDevicesRequestValidator.Validate(null, kNetworkZoneTree, kDesignatedMatrixId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.Empty);
            Assert.That(result.Errors[0].Message, Does.Contain("requires a request body"));
            Assert.That(result.Errors[0].Message, Does.Contain("'sources'"));
        });
    }

    /// <summary>Verifies omitted lists are reported as required, each under its own path.</summary>
    [Test]
    public void Validate_WithoutLists_ReportsBothAsRequired()
    {
        RequestValidationErrorResponse result = Validate("{}");

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EquivalentTo(new List<string> { "sources", "destinations" }));
            Assert.That(MessageAt(result, "sources"), Is.EqualTo("'sources' is required."));
            Assert.That(MessageAt(result, "destinations"), Is.EqualTo("'destinations' is required."));
        });
    }

    /// <summary>Verifies an explicit null list is treated like an omitted one.</summary>
    [Test]
    public void Validate_WithNullList_ReportsRequired()
    {
        RequestValidationErrorResponse result = Validate("""{"sources":null,"destinations":[{"ipNetwork":"10.2.0.0/24"}]}""");

        Assert.That(MessageAt(result, "sources"), Is.EqualTo("'sources' is required."));
    }

    /// <summary>Verifies an empty list is rejected, since it would silently analyse nothing.</summary>
    [Test]
    public void Validate_WithEmptyList_ReportsNotEmpty()
    {
        RequestValidationErrorResponse result = Validate("""{"sources":[{"ipNetwork":"10.1.0.0/24"}],"destinations":[]}""");

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "destinations" }));
            Assert.That(MessageAt(result, "destinations"), Is.EqualTo("'destinations' must not be empty."));
        });
    }

    /// <summary>Verifies a list at the limit passes and one entry above it is rejected.</summary>
    [Test]
    public void Validate_ListLength_IsLimited()
    {
        int limit = ResolvePathDevicesRequestValidationSchema.kMaximumIpBlocksPerList;

        RequestValidationErrorResponse atLimit = Validate(BuildRequestWithSourceCount(limit));
        RequestValidationErrorResponse aboveLimit = Validate(BuildRequestWithSourceCount(limit + 1));

        Assert.Multiple(() =>
        {
            Assert.That(atLimit.Errors, Is.Empty);
            Assert.That(MessageAt(aboveLimit, "sources"), Is.EqualTo($"'sources' must not exceed {limit} entries."));
        });
    }

    /// <summary>Verifies errors of list entries carry the index of the entry, for both lists.</summary>
    [Test]
    public void Validate_WithInvalidEntries_ReportsEachUnderItsIndex()
    {
        RequestValidationErrorResponse result = Validate(
            """{"sources":[{"ipNetwork":"10.1.0.0/24"},null],"destinations":[{"ipNetwork":"10.2.0.0/24"},{"ipStart":"10.2.0.9","ipEnd":"10.2.0.1"}]}""");

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EquivalentTo(new List<string> { "sources[1]", "destinations[1]" }));
            Assert.That(MessageAt(result, "sources[1]"), Does.Contain("must be an object"));
            Assert.That(MessageAt(result, "destinations[1]"), Does.Contain("'ipStart' <= 'ipEnd'"));
        });
    }

    /// <summary>Verifies the requestable algorithm names are accepted, compared case-insensitively.</summary>
    [TestCase("networkZoneTree")]
    [TestCase("NETWORKZONETREE")]
    public void Validate_WithRequestableAlgorithm_HasNoErrors(string algorithm)
    {
        RequestValidationErrorResponse result = Validate(WithOptions($"{{\"algorithm\":\"{algorithm}\"}}"));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies an unknown algorithm is rejected, and so is an explicit none, which cannot resolve any device.
    /// The message repeats the value and lists the allowed names.
    /// </summary>
    [TestCase("routing")]
    [TestCase("none")]
    [TestCase("")]
    public void Validate_WithUnsupportedAlgorithm_ReportsAllowedValues(string algorithm)
    {
        RequestValidationErrorResponse result = Validate(WithOptions($"{{\"algorithm\":\"{algorithm}\"}}"));

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.algorithm" }));
            Assert.That(MessageAt(result, "options.algorithm"), Does.Contain($"unsupported value '{algorithm}'"));
            Assert.That(MessageAt(result, "options.algorithm"),
                Does.EndWith($"Allowed values: '{PathAnalysisAlgorithmNames.kNetworkZoneTree}'."));
        });
    }

    /// <summary>Verifies a request that relies on a configured default of none is stopped before it runs.</summary>
    [Test]
    public void Validate_WithoutAlgorithmAndDefaultNone_ReportsMissingDefault()
    {
        RequestValidationErrorResponse result = Validate(Request(), kNone);

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.algorithm" }));
            Assert.That(MessageAt(result, "options.algorithm"), Does.StartWith("No default path analysis algorithm is configured."));
        });
    }

    /// <summary>Verifies a configured default of none does not matter when the request names an algorithm.</summary>
    [Test]
    public void Validate_WithAlgorithmAndDefaultNone_HasNoErrors()
    {
        RequestValidationErrorResponse result = Validate(WithOptions("""{"algorithm":"networkZoneTree"}"""), kNone);

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies a matrix id is only accepted together with an explicitly named network zone tree algorithm.</summary>
    [Test]
    public void Validate_WithMatrixIdButNoAlgorithm_ReportsMissingAlgorithm()
    {
        RequestValidationErrorResponse result = Validate(WithOptions("""{"matrixId":3}"""));

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.matrixId" }));
            Assert.That(MessageAt(result, "options.matrixId"),
                Is.EqualTo("'options.matrixId' is only valid together with 'options.algorithm' set to 'networkZoneTree'."));
        });
    }

    /// <summary>Verifies a matrix id that is not positive is rejected.</summary>
    [TestCase(0)]
    [TestCase(-5)]
    public void Validate_WithNonPositiveMatrixId_ReportsRange(int matrixId)
    {
        RequestValidationErrorResponse result = Validate(
            WithOptions($"{{\"algorithm\":\"networkZoneTree\",\"matrixId\":{matrixId}}}"));

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.matrixId" }));
            Assert.That(MessageAt(result, "options.matrixId"), Is.EqualTo("'options.matrixId' must be greater than 0."));
        });
    }

    /// <summary>Verifies a missing designated matrix stops a request that would fall back to it.</summary>
    [TestCase("")]
    [TestCase(",\"options\":{\"algorithm\":\"networkZoneTree\"}")]
    public void Validate_WithoutMatrixIdAndNoDesignatedMatrix_ReportsMissingDefault(string optionsJson)
    {
        RequestValidationErrorResponse result = Validate(Request(optionsJson), kNetworkZoneTree, kNoDesignatedMatrix);

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.matrixId" }));
            Assert.That(MessageAt(result, "options.matrixId"), Is.EqualTo("No default zone matrix is configured. Specify 'options.matrixId'."));
        });
    }

    /// <summary>Verifies a missing designated matrix does not matter when the request names a matrix.</summary>
    [Test]
    public void Validate_WithMatrixIdAndNoDesignatedMatrix_HasNoErrors()
    {
        RequestValidationErrorResponse result = Validate(
            WithOptions("""{"algorithm":"networkZoneTree","matrixId":3}"""), kNetworkZoneTree, kNoDesignatedMatrix);

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies the missing designated matrix is not reported on top of an algorithm error, because then it is
    /// unclear which algorithm would run.
    /// </summary>
    [TestCase(",\"options\":{\"algorithm\":\"routing\"}", kNetworkZoneTree)]
    [TestCase("", kNone)]
    public void Validate_WithAlgorithmErrorAndNoDesignatedMatrix_ReportsOnlyTheAlgorithm(string optionsJson, long configuredAlgorithmId)
    {
        RequestValidationErrorResponse result = Validate(Request(optionsJson), configuredAlgorithmId, kNoDesignatedMatrix);

        Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.algorithm" }));
    }

    /// <summary>Verifies null filter keys apply no restriction and are accepted.</summary>
    [Test]
    public void Validate_WithNullFilterKeys_HasNoErrors()
    {
        RequestValidationErrorResponse result = Validate(WithOptions("""{"filter":{"id":null,"name":null}}"""));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies a filter id that is not positive is rejected, since it can never match a device.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_WithNonPositiveFilterId_ReportsRange(int id)
    {
        RequestValidationErrorResponse result = Validate(WithOptions($"{{\"filter\":{{\"id\":{id}}}}}"));

        Assert.That(MessageAt(result, "options.filter.id"), Is.EqualTo("'options.filter.id' must be greater than 0."));
    }

    /// <summary>Verifies an empty or blank filter name is rejected and the message points at null.</summary>
    [TestCase("")]
    [TestCase("   ")]
    public void Validate_WithBlankFilterName_ReportsNotEmpty(string name)
    {
        RequestValidationErrorResponse result = Validate(WithOptions($"{{\"filter\":{{\"name\":\"{name}\"}}}}"));

        Assert.Multiple(() =>
        {
            Assert.That(PathsOf(result), Is.EqualTo(new List<string> { "options.filter.name" }));
            Assert.That(MessageAt(result, "options.filter.name"), Does.Contain("use null"));
        });
    }

    /// <summary>Verifies keys the contract does not define are reported on every level.</summary>
    [Test]
    public void Validate_WithUnknownKeys_ReportsEachLevel()
    {
        RequestValidationErrorResponse result = Validate(
            Request(""","source":[],"options":{"algo":"x","filter":{"deviceName":"fw"}}"""));

        Assert.That(PathsOf(result), Is.EquivalentTo(new List<string> { "source", "options.algo", "options.filter.deviceName" }));
    }

    /// <summary>Verifies every error of one request is reported together, each under its own path.</summary>
    [Test]
    public void Validate_WithManyErrors_ReportsAllTogether()
    {
        RequestValidationErrorResponse result = Validate(
            """{"sources":[],"destinations":[{"ipNetwork":"10.2.0.5/24"}],"extra":1,"options":{"algorithm":"none","matrixId":0,"filter":{"id":0,"name":""}}}""");

        Assert.That(PathsOf(result), Is.EquivalentTo(new List<string>
        {
            "sources",
            "destinations[0]",
            "extra",
            "options.algorithm",
            "options.matrixId",
            "options.matrixId",
            "options.filter.id",
            "options.filter.name"
        }));
    }

    /// <summary>Verifies the same invalid request always yields the same errors in the same order.</summary>
    [Test]
    public void Validate_IsDeterministic()
    {
        string json = """{"sources":[null],"destinations":[],"b":1,"a":2,"options":{"matrixId":-1}}""";

        List<string> first = PathsOf(Validate(json));
        List<string> second = PathsOf(Validate(json));

        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>Verifies the not found error for a requested matrix is attributed to the matrix option.</summary>
    [Test]
    public void BuildUnknownMatrixError_ReportsMatrixOption()
    {
        RequestValidationErrorResponse result = ResolvePathDevicesRequestValidator.BuildUnknownMatrixError(42);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo("options.matrixId"));
            Assert.That(result.Errors[0].Message, Does.Contain("42"));
            Assert.That(result.Errors[0].Message, Does.Contain("does not exist or holds no configured zones"));
        });
    }

    /// <summary>Verifies the key descriptions of the schema state the limits the validator enforces.</summary>
    [Test]
    public void Schema_DescribesTheEnforcedRules()
    {
        string sources = DescriptionOf(ResolvePathDevicesRequestValidationSchema.RootKeys, "sources");
        string algorithm = DescriptionOf(ResolvePathDevicesRequestValidationSchema.OptionsKeys, "algorithm");
        string matrixId = DescriptionOf(ResolvePathDevicesRequestValidationSchema.OptionsKeys, "matrixId");

        Assert.Multiple(() =>
        {
            Assert.That(sources, Does.Contain(ResolvePathDevicesRequestValidationSchema.kMaximumIpBlocksPerList.ToString()));
            Assert.That(algorithm, Does.Contain(PathAnalysisAlgorithmNames.kNetworkZoneTree));
            Assert.That(matrixId, Does.Contain(PathAnalysisAlgorithmNames.kNetworkZoneTree));
            Assert.That(ResolvePathDevicesRequestValidationSchema.FilterKeys.Select(key => key.JsonName),
                Is.EquivalentTo(new List<string> { "id", "name" }));
        });
    }

    private static RequestValidationErrorResponse Validate(string json, long configuredAlgorithmId = kNetworkZoneTree,
        int designatedZoneMatrixId = kDesignatedMatrixId)
    {
        return ResolvePathDevicesRequestValidator.Validate(Deserialize(json), configuredAlgorithmId, designatedZoneMatrixId);
    }

    private static string Request(string additionalRootJson = "")
    {
        return "{" + kValidLists + additionalRootJson + "}";
    }

    private static string WithOptions(string optionsJson)
    {
        return Request(",\"options\":" + optionsJson);
    }

    private static ResolvePathDevicesRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ResolvePathDevicesRequest>(json)!;
    }

    private static List<string> PathsOf(RequestValidationErrorResponse result)
    {
        return [.. result.Errors.Select(error => error.Path)];
    }

    private static string MessageAt(RequestValidationErrorResponse result, string path)
    {
        return result.Errors.Single(error => error.Path == path).Message;
    }

    private static string DescriptionOf(IReadOnlyList<RequestKeyDefinition> keys, string jsonName)
    {
        return keys.Single(key => key.JsonName == jsonName).Description;
    }

    private static string BuildRequestWithSourceCount(int count)
    {
        StringBuilder sources = new();
        for (int index = 0; index < count; index++)
        {
            if (index > 0)
            {
                sources.Append(',');
            }
            sources.Append("""{"ipNetwork":"10.1.0.0/24"}""");
        }
        return "{\"sources\":[" + sources + "],\"destinations\":[{\"ipNetwork\":\"10.2.0.0/24\"}]}";
    }
}
