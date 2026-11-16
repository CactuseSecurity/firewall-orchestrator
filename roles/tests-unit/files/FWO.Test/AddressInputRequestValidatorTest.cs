using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the validation of one address block of a request: the path every error is reported under, null list
/// elements, keys the block does not define, and that the address rules of <see cref="AddressInputNormalizer"/>
/// are applied with their message reported once and unchanged.
/// </summary>
[TestFixture]
[Parallelizable]
internal class AddressInputRequestValidatorTest
{
    private const string kPath = "sources[2]";

    private static readonly List<string> kAddressKeys = ["ipHost", "ipNetwork", "ipRange"];
    private static readonly List<string> kUnknownKeyAndBlockPaths = [$"{kPath}.mask", kPath];
    private static readonly List<string> kLegacyKeyAndBlockPaths = [$"{kPath}.ipStart", $"{kPath}.ipEnd", kPath];

    /// <summary>Verifies each of the three address forms passes on its own.</summary>
    [TestCase("""{"ipHost":"10.1.0.5"}""")]
    [TestCase("""{"ipHost":"2001:db8::5"}""")]
    [TestCase("""{"ipNetwork":"10.1.0.0/24"}""")]
    [TestCase("""{"ipNetwork":"10.1.0.5/24"}""")]
    [TestCase("""{"ipRange":["10.1.0.5","10.1.0.9"]}""")]
    [TestCase("""{"ipRange":["10.1.0.5","10.1.0.5"]}""")]
    [TestCase("""{"ipRange":["2001:db8::1","2001:db8::ff"]}""")]
    public void Validate_WithValidBlock_AddsNoError(string json)
    {
        RequestValidationErrorResponse result = Validate(Deserialize(json));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies a null list element is reported under its own path and names every valid key.</summary>
    [Test]
    public void Validate_WithNullBlock_ReportsObjectRequiredWithAllKeys()
    {
        RequestValidationErrorResponse result = Validate(null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(kPath));
            Assert.That(result.Errors[0].Message, Does.StartWith($"'{kPath}' must be an object with exactly one of the following keys"));
            foreach (string key in kAddressKeys)
            {
                Assert.That(result.Errors[0].Message, Does.Contain($"'{key}'"));
            }
        });
    }

    /// <summary>
    /// Verifies the message of the normalizer is reported unchanged: it already starts with the path, so the
    /// validator must not prefix it a second time.
    /// </summary>
    [Test]
    public void Validate_WithInvalidBlock_ReportsPathOnce()
    {
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipRange":["10.1.0.9","10.1.0.5"]}"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(kPath));
            Assert.That(result.Errors[0].Message,
                Is.EqualTo($"'{kPath}' must order 'ipRange' from its lower to its upper address."));
        });
    }

    /// <summary>Verifies every rule of the address forms is enforced and reported under the path of the block.</summary>
    [TestCase("{}", "must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.")]
    [TestCase("""{"ipHost":""}""", "must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.")]
    [TestCase("""{"ipRange":[]}""", "must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.")]
    [TestCase("""{"ipHost":"10.1.0.5","ipNetwork":"10.1.0.0/24"}""", "must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.")]
    [TestCase("""{"ipHost":"10.1.0.5","ipRange":["10.1.0.5","10.1.0.9"]}""", "must define exactly one non-empty 'ipHost', 'ipNetwork', or 'ipRange'.")]
    [TestCase("""{"ipHost":"10.1.0.5/32"}""", "has an invalid 'ipHost' value.")]
    [TestCase("""{"ipHost":"10.1.0.0/24"}""", "has an invalid 'ipHost' value.")]
    [TestCase("""{"ipHost":"010.1.0.5"}""", "has an invalid 'ipHost' value.")]
    [TestCase("""{"ipHost":"no address"}""", "has an invalid 'ipHost' value.")]
    [TestCase("""{"ipNetwork":"10.1.0.5"}""", "has an invalid 'ipNetwork' value.")]
    [TestCase("""{"ipNetwork":"10.1.0.0/33"}""", "has an invalid 'ipNetwork' value.")]
    [TestCase("""{"ipRange":["10.1.0.5"]}""", "requires exactly two entries in 'ipRange'.")]
    [TestCase("""{"ipRange":["10.1.0.5","10.1.0.6","10.1.0.7"]}""", "requires exactly two entries in 'ipRange'.")]
    [TestCase("""{"ipRange":["10.1.0.5/32","10.1.0.9"]}""", "has an invalid 'ipRange[0]' value.")]
    [TestCase("""{"ipRange":["10.1.0.5","no address"]}""", "has an invalid 'ipRange[1]' value.")]
    [TestCase("""{"ipRange":["10.1.0.5","2001:db8::1"]}""", "must use the same address family for both 'ipRange' entries.")]
    [TestCase("""{"ipRange":["10.1.0.9","10.1.0.5"]}""", "must order 'ipRange' from its lower to its upper address.")]
    public void Validate_WithInvalidBlock_ReportsRuleUnderBlockPath(string json, string expectedReason)
    {
        RequestValidationErrorResponse result = Validate(Deserialize(json));

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(kPath));
            Assert.That(result.Errors[0].Message, Is.EqualTo($"'{kPath}' {expectedReason}"));
        });
    }

    /// <summary>Verifies a key the block does not define is reported under its own nested path.</summary>
    [Test]
    public void Validate_WithUnknownKey_ReportsKeyUnderNestedPath()
    {
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipNetwork":"10.1.0.0/24","ip":"10.1.0.1"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo($"{kPath}.ip"));
            Assert.That(result.Errors[0].Message, Does.Contain("'ipNetwork'"));
        });
    }

    /// <summary>Verifies an unknown key and an address error of the same block are both reported.</summary>
    [Test]
    public void Validate_WithUnknownKeyAndInvalidAddress_ReportsBoth()
    {
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipHost":"10.1.0.5/24","mask":"24"}"""));

        Assert.That(result.Errors.Select(error => error.Path), Is.EquivalentTo(kUnknownKeyAndBlockPaths));
    }

    /// <summary>
    /// Verifies a request in the removed ipStart/ipEnd format is rejected with both legacy keys reported as
    /// unsupported and the block reported as giving no address.
    /// </summary>
    [Test]
    public void Validate_WithLegacyStartEndKeys_ReportsLegacyKeysAndMissingAddress()
    {
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipStart":"10.1.0.5","ipEnd":"10.1.0.9"}"""));

        Assert.That(result.Errors.Select(error => error.Path), Is.EquivalentTo(kLegacyKeyAndBlockPaths));
    }

    /// <summary>Verifies the valid keys exposed for help texts are exactly the three address forms.</summary>
    [Test]
    public void Keys_ListsTheThreeAddressForms()
    {
        Assert.That(AddressInputRequestValidator.Keys.Select(key => key.JsonName), Is.EquivalentTo(kAddressKeys));
    }

    /// <summary>
    /// Verifies the key descriptions are plain text, since they are sent in validation errors, and each ends
    /// with a period, since they are joined into one sentence sequence.
    /// </summary>
    [Test]
    public void Keys_HavePlainTextDescriptionsEndingWithPeriod()
    {
        Assert.Multiple(() =>
        {
            foreach (RequestKeyDefinition key in AddressInputRequestValidator.Keys)
            {
                Assert.That(key.Description, Does.Not.Contain("<"), key.JsonName);
                Assert.That(key.Description, Does.EndWith("."), key.JsonName);
            }
        });
    }

    private static RequestValidationErrorResponse Validate(AddressInputRequest? address)
    {
        RequestValidationErrorResponse result = new();
        AddressInputRequestValidator.Validate(address, kPath, result);
        return result;
    }

    private static AddressInputRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AddressInputRequest>(json)!;
    }
}
