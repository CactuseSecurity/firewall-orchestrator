using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the request contract of the flow compliance endpoints: the policy id request without keys, the address,
/// service and policy lists of the flow compliance state request, the aggregation of every error of one request,
/// and the unchanged plain text messages of the service range check the flow catalog still uses.
/// </summary>
[TestFixture]
[Parallelizable]
internal class FlowComplianceValidationTest
{
    private const string kValidFlowJson = """
        {
          "source": [{"ipRange":["10.0.0.1","10.0.0.2"]}],
          "destination": [{"ipHost":"10.0.1.1"}],
          "service": [{"portStart":443,"portEnd":443,"protocol":"TCP"}],
          "policies": [1,2]
        }
        """;

    private static readonly List<string> kNullListPaths = ["source", "destination", "service", "policies"];
    private static readonly List<string> kUnknownPolicyIdKeys = ["alpha", "zulu"];
    private static readonly List<string> kAggregatedPaths =
        ["source[0]", "destination[1]", "service[0].protocol", "service[1].portEnd", "policies[0]", "typo"];
    private static readonly List<string> kOutOfRangePortPaths = ["service[0].portStart", "service[0].portEnd"];

    /// <summary>Verifies a missing body is accepted, because the endpoint takes no input.</summary>
    [Test]
    public void ValidatePolicyIds_NullRequest_HasNoErrors()
    {
        RequestValidationErrorResponse result = FlowComplianceRequestValidator.ValidatePolicyIds(null);

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies an empty object is accepted.</summary>
    [Test]
    public void ValidatePolicyIds_EmptyObject_HasNoErrors()
    {
        RequestValidationErrorResponse result = FlowComplianceRequestValidator.ValidatePolicyIds(
            JsonSerializer.Deserialize<GetPolicyIdsRequest>("{}"));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies every unknown key is reported under its own path, in ordinal order.</summary>
    [Test]
    public void ValidatePolicyIds_UnknownKeys_ReportsEachKeyInOrdinalOrder()
    {
        RequestValidationErrorResponse result = FlowComplianceRequestValidator.ValidatePolicyIds(
            JsonSerializer.Deserialize<GetPolicyIdsRequest>("""{"zulu":1,"alpha":true}"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Is.EqualTo(kUnknownPolicyIdKeys));
            Assert.That(result.Errors[0].Message, Does.StartWith("'alpha' is not supported."));
            Assert.That(result.Errors[0].Message, Does.Contain(FlowComplianceRequestValidationSchema.kPolicyIdsEndpointName));
        });
    }

    /// <summary>Verifies a missing body is rejected with a help text naming every root key.</summary>
    [Test]
    public void ValidateFlowComplianceState_NullRequest_ReportsRootKeys()
    {
        RequestValidationErrorResponse result = FlowComplianceRequestValidator.ValidateFlowComplianceState(null);

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.Empty);
            Assert.That(result.Errors[0].Message, Does.StartWith("getFlowComplianceState requires a request body."));
            Assert.That(result.Errors[0].Message, Does.Contain("'source':").And.Contain("'policies':"));
        });
    }

    /// <summary>Verifies a request using every key passes.</summary>
    [Test]
    public void ValidateFlowComplianceState_ExpectedShape_HasNoErrors()
    {
        Assert.That(ValidateJson(kValidFlowJson).Errors, Is.Empty);
    }

    /// <summary>Verifies omitted lists default to empty lists and pass.</summary>
    [Test]
    public void ValidateFlowComplianceState_EmptyObject_HasNoErrors()
    {
        GetFlowComplianceStateRequest request = Deserialize("{}");

        Assert.Multiple(() =>
        {
            Assert.That(request.Source, Is.Empty);
            Assert.That(request.Destination, Is.Empty);
            Assert.That(request.Service, Is.Empty);
            Assert.That(request.Policies, Is.Empty);
            Assert.That(FlowComplianceRequestValidator.ValidateFlowComplianceState(request).Errors, Is.Empty);
        });
    }

    /// <summary>Verifies an explicit null replaces the empty default and is rejected for every list.</summary>
    [Test]
    public void ValidateFlowComplianceState_NullLists_ReportsEachList()
    {
        RequestValidationErrorResponse result = ValidateJson(
            """{"source":null,"destination":null,"service":null,"policies":null}""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Is.EqualTo(kNullListPaths));
            Assert.That(result.Errors.All(error => error.Message.Contains("must not be null")), Is.True);
        });
    }

    /// <summary>
    /// Verifies every oversized list is reported once and the entries of an oversized list are not checked one by one.
    /// </summary>
    [Test]
    public void ValidateFlowComplianceState_OversizedLists_ReportsListsWithoutCheckingEntries()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = Enumerable.Range(0, FlowComplianceRequestValidationSchema.kMaxRangeEntries + 1)
                .Select(_ => (AddressInputRequest?)new AddressInputRequest { IpHost = "not-an-address" })
                .ToList(),
            Policies = Enumerable.Range(0, FlowComplianceRequestValidationSchema.kMaxPolicies + 1).ToList()
        };

        RequestValidationErrorResponse result = FlowComplianceRequestValidator.ValidateFlowComplianceState(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(2));
            Assert.That(result.Errors[0].Path, Is.EqualTo("source"));
            Assert.That(result.Errors[0].Message,
                Does.Contain($"must not exceed {FlowComplianceRequestValidationSchema.kMaxRangeEntries} entries"));
            Assert.That(result.Errors[1].Path, Is.EqualTo("policies"));
            Assert.That(result.Errors[1].Message,
                Does.Contain($"must not exceed {FlowComplianceRequestValidationSchema.kMaxPolicies} entries"));
        });
    }

    /// <summary>Verifies lists at their maximum size pass.</summary>
    [Test]
    public void ValidateFlowComplianceState_ListsAtTheMaximum_HaveNoErrors()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = Enumerable.Range(0, FlowComplianceRequestValidationSchema.kMaxRangeEntries)
                .Select(_ => (AddressInputRequest?)new AddressInputRequest { IpHost = "10.0.0.1" })
                .ToList(),
            Policies = Enumerable.Range(1, FlowComplianceRequestValidationSchema.kMaxPolicies).ToList()
        };

        Assert.That(FlowComplianceRequestValidator.ValidateFlowComplianceState(request).Errors, Is.Empty);
    }

    /// <summary>Verifies every supported address representation of both address families passes.</summary>
    [TestCase("""{"ipHost":"10.0.0.10"}""")]
    [TestCase("""{"ipHost":"2001:db8::10"}""")]
    [TestCase("""{"ipNetwork":"10.0.0.0/24"}""")]
    [TestCase("""{"ipNetwork":"10.0.0.5/24"}""")]
    [TestCase("""{"ipNetwork":"2001:db8::/126"}""")]
    [TestCase("""{"ipNetwork":"0.0.0.0/0"}""")]
    [TestCase("""{"ipNetwork":"::/0"}""")]
    [TestCase("""{"ipRange":["10.0.0.1","10.0.0.2"]}""")]
    [TestCase("""{"ipRange":["2001:db8::1","2001:db8::ffff"]}""")]
    public void ValidateFlowComplianceState_SupportedAddress_HasNoErrors(string addressJson)
    {
        Assert.That(ValidateJson(WithSource(addressJson)).Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies an invalid address is reported once under its entry path, with the path named only once in the message.
    /// </summary>
    [TestCase("""{"ipNetwork":"10.0.0.1/33"}""")]
    [TestCase("""{"ipNetwork":"2001:db8::1/129"}""")]
    [TestCase("""{"ipNetwork":"10.0.0.1/not-a-prefix"}""")]
    [TestCase("""{"ipNetwork":"010.0.0.0/24"}""")]
    [TestCase("""{"ipNetwork":"fe80::%3/64"}""")]
    [TestCase("""{"ipNetwork":"::ffff:192.0.2.0/120"}""")]
    [TestCase("""{"ipNetwork":"10.0.0.0/24/24"}""")]
    [TestCase("""{"ipRange":["10.0.0.1/32","10.0.0.2"]}""")]
    [TestCase("""{"ipRange":["banana","10.0.0.2"]}""")]
    [TestCase("""{"ipRange":["10.0.0.1","::1"]}""")]
    [TestCase("""{"ipRange":["10.0.0.2","10.0.0.1"]}""")]
    [TestCase("""{"ipRange":["10.0.0.1"]}""")]
    [TestCase("""{"ipNetwork":"10.0.0.0/24","ipRange":["10.0.0.1","10.0.0.2"]}""")]
    [TestCase("{}")]
    public void ValidateFlowComplianceState_InvalidAddress_ReportsEntryPathOnce(string addressJson)
    {
        RequestValidationErrorResponse result = ValidateJson(WithSource(addressJson));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("source[0]"));
            Assert.That(result.Errors[0].Message, Does.StartWith("'source[0]' "));
            Assert.That(result.Errors[0].Message, Does.Not.Contain("'source[0]' 'source[0]'"));
        });
    }

    /// <summary>Verifies a null address entry is reported with the address keys it accepts.</summary>
    [Test]
    public void ValidateFlowComplianceState_NullAddressEntry_ReportsAddressKeys()
    {
        RequestValidationErrorResponse result = ValidateJson(WithSource("null"));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("source[0]"));
            Assert.That(result.Errors[0].Message, Does.Contain("exactly one").And.Contain("'ipHost'"));
        });
    }

    /// <summary>Verifies the removed legacy address keys are reported as unsupported under the entry path.</summary>
    [TestCase("ipStart")]
    [TestCase("ipEnd")]
    public void ValidateFlowComplianceState_RemovedLegacyAddressKey_IsUnsupported(string legacyKey)
    {
        RequestValidationErrorResponse result = ValidateJson(WithSource($$"""{"{{legacyKey}}":"10.0.0.1"}"""));

        RequestValidationError? legacyKeyError = result.Errors.SingleOrDefault(error => error.Path == $"source[0].{legacyKey}");
        Assert.That(legacyKeyError, Is.Not.Null);
        Assert.That(legacyKeyError!.Message, Does.Contain("is not supported").And.Contain("'ipHost'"));
    }

    /// <summary>Verifies the port bounds themselves pass.</summary>
    [TestCase(0, 0)]
    [TestCase(0, 65535)]
    [TestCase(65535, 65535)]
    public void ValidateFlowComplianceState_PortsAtTheBounds_HaveNoErrors(int portStart, int portEnd)
    {
        Assert.That(ValidateJson(WithService(portStart, portEnd, "TCP")).Errors, Is.Empty);
    }

    /// <summary>Verifies an empty or missing protocol is reported under the protocol path.</summary>
    [TestCase("""{"portStart":443,"portEnd":443,"protocol":""}""")]
    [TestCase("""{"portStart":443,"portEnd":443,"protocol":"  "}""")]
    [TestCase("""{"portStart":443,"portEnd":443}""")]
    public void ValidateFlowComplianceState_EmptyProtocol_ReportsProtocolPath(string serviceJson)
    {
        RequestValidationErrorResponse result = ValidateJson(WithServiceJson(serviceJson));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("service[0].protocol"));
            Assert.That(result.Errors[0].Message, Is.EqualTo("'service[0].protocol' must not be empty."));
        });
    }

    /// <summary>Verifies a port outside the allowed range is reported under its own path.</summary>
    [TestCase(-1, 443, "service[0].portStart")]
    [TestCase(443, 65536, "service[0].portEnd")]
    public void ValidateFlowComplianceState_PortOutsideRange_ReportsPortPath(int portStart, int portEnd, string expectedPath)
    {
        RequestValidationErrorResponse result = ValidateJson(WithService(portStart, portEnd, "TCP"));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo(expectedPath));
            Assert.That(result.Errors[0].Message, Is.EqualTo($"'{expectedPath}' must be between 0 and 65535."));
        });
    }

    /// <summary>Verifies both ports outside the range are reported together.</summary>
    [Test]
    public void ValidateFlowComplianceState_BothPortsOutsideRange_ReportsBothPorts()
    {
        RequestValidationErrorResponse result = ValidateJson(WithService(-1, 70000, "TCP"));

        Assert.That(result.Errors.Select(error => error.Path), Is.EqualTo(kOutOfRangePortPaths));
    }

    /// <summary>Verifies a start port above the end port is reported under the end port path.</summary>
    [Test]
    public void ValidateFlowComplianceState_DescendingPorts_ReportsEndPort()
    {
        RequestValidationErrorResponse result = ValidateJson(WithService(1024, 443, "TCP"));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("service[0].portEnd"));
            Assert.That(result.Errors[0].Message, Does.Contain("must not be lower than"));
        });
    }

    /// <summary>Verifies a null service entry is reported with the service keys it accepts.</summary>
    [Test]
    public void ValidateFlowComplianceState_NullServiceEntry_ReportsServiceKeys()
    {
        RequestValidationErrorResponse result = ValidateJson(WithServiceJson("null"));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("service[0]"));
            Assert.That(result.Errors[0].Message, Does.Contain("'protocol':").And.Contain("'portStart':").And.Contain("'portEnd':"));
        });
    }

    /// <summary>Verifies an unknown service key is reported under the path of its entry.</summary>
    [Test]
    public void ValidateFlowComplianceState_UnknownServiceKey_ReportsEntryPath()
    {
        RequestValidationErrorResponse result = ValidateJson(
            WithServiceJson("""{"portStart":443,"portEnd":443,"protocol":"TCP","typo":true}"""));

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("service[0].typo"));
            Assert.That(result.Errors[0].Message, Does.Contain("is not supported by 'service[0]'"));
        });
    }

    /// <summary>Verifies a policy id that is not greater than 0 is reported under its entry path with its value.</summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void ValidateFlowComplianceState_NonPositivePolicyId_ReportsEntryPath(int policyId)
    {
        RequestValidationErrorResponse result = ValidateJson($$"""{"policies":[1,{{policyId}}]}""");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("policies[1]"));
            Assert.That(result.Errors[0].Message,
                Is.EqualTo($"'policies[1]' must be a policy id greater than 0, but is {policyId}."));
        });
    }

    /// <summary>Verifies an unknown root key is reported with the valid root keys.</summary>
    [Test]
    public void ValidateFlowComplianceState_UnknownRootKey_ReportsRootKeys()
    {
        RequestValidationErrorResponse result = ValidateJson("""{"typo":true}""");

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors[0].Path, Is.EqualTo("typo"));
            Assert.That(result.Errors[0].Message, Does.Contain("the request root").And.Contain("'service':"));
        });
    }

    /// <summary>Verifies errors in every part of the request are reported together instead of stopping at the first.</summary>
    [Test]
    public void ValidateFlowComplianceState_ErrorsEverywhere_ReportsAllTogether()
    {
        RequestValidationErrorResponse result = ValidateJson("""
            {
              "source": [{"ipHost":"banana"}],
              "destination": [{"ipHost":"10.0.1.1"},{"ipNetwork":"10.0.0.1/33"}],
              "service": [{"portStart":443,"portEnd":443,"protocol":""},{"portStart":443,"portEnd":80,"protocol":"TCP"}],
              "policies": [0],
              "typo": true
            }
            """);

        Assert.That(result.Errors.Select(error => error.Path), Is.EqualTo(kAggregatedPaths));
    }

    /// <summary>Verifies the flow catalog check accepts a valid range.</summary>
    [Test]
    public void TryValidateServiceRange_ValidRange_HasNoError()
    {
        bool valid = FlowComplianceRequestValidator.TryValidateServiceRange(0, 65535, "service", 0, out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorMessage, Is.Null);
        });
    }

    /// <summary>Verifies the flow catalog check keeps its plain text messages unchanged.</summary>
    [TestCase(-1, 443, "'service' entry at index 2 has an invalid 'portStart' value. Allowed range is 0-65535.")]
    [TestCase(443, 65536, "'service' entry at index 2 has an invalid 'portEnd' value. Allowed range is 0-65535.")]
    [TestCase(1024, 443, "'service' entry at index 2 must satisfy 'portStart' <= 'portEnd'.")]
    public void TryValidateServiceRange_InvalidRange_KeepsFlowCatalogMessage(int portStart, int portEnd, string expectedMessage)
    {
        bool valid = FlowComplianceRequestValidator.TryValidateServiceRange(portStart, portEnd, "service", 2, out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorMessage, Is.EqualTo(expectedMessage));
        });
    }

    private static GetFlowComplianceStateRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;
    }

    private static RequestValidationErrorResponse ValidateJson(string json)
    {
        return FlowComplianceRequestValidator.ValidateFlowComplianceState(Deserialize(json));
    }

    /// <summary>Builds a request that is valid except for its only source entry.</summary>
    private static string WithSource(string addressJson)
    {
        return $$"""{"source":[{{addressJson}}],"destination":[{"ipHost":"10.0.1.1"}],"policies":[1]}""";
    }

    /// <summary>Builds a request that is valid except for its only service entry.</summary>
    private static string WithServiceJson(string serviceJson)
    {
        return $$"""{"source":[{"ipHost":"10.0.0.1"}],"service":[{{serviceJson}}],"policies":[1]}""";
    }

    private static string WithService(int portStart, int portEnd, string protocol)
    {
        return WithServiceJson($$"""{"portStart":{{portStart}},"portEnd":{{portEnd}},"protocol":"{{protocol}}"}""");
    }
}
