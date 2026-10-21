using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Tests the validation of one IP address block of a request: the path every error is reported under,
/// null list elements and keys the block does not define.
/// </summary>
[TestFixture]
[Parallelizable]
internal class IpRangeOrNetworkRequestValidatorTest
{
    private const string kPath = "sources[2]";

    /// <summary>Verifies a valid block adds no error.</summary>
    [TestCase("""{"ipStart":"10.1.0.5","ipEnd":"10.1.0.9"}""")]
    [TestCase("""{"ipNetwork":"10.1.0.0/24"}""")]
    public void Validate_WithValidBlock_AddsNoError(string json)
    {
        RequestValidationErrorResponse result = Validate(Deserialize(json));

        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>Verifies a null list element is reported under its own path and names the valid keys.</summary>
    [Test]
    public void Validate_WithNullBlock_ReportsObjectRequired()
    {
        RequestValidationErrorResponse result = Validate(null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(kPath));
            Assert.That(result.Errors[0].Message, Does.StartWith($"'{kPath}' must be an object."));
            Assert.That(result.Errors[0].Message, Does.Contain("'ipStart'"));
            Assert.That(result.Errors[0].Message, Does.Contain("'ipEnd'"));
            Assert.That(result.Errors[0].Message, Does.Contain("'ipNetwork'"));
        });
    }

    /// <summary>Verifies an address error is prefixed with the path of the block.</summary>
    [Test]
    public void Validate_WithInvalidBlock_PrefixesParserErrorWithPath()
    {
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipStart":"10.1.0.9","ipEnd":"10.1.0.5"}"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(kPath));
            Assert.That(result.Errors[0].Message, Is.EqualTo($"'{kPath}' must satisfy 'ipStart' <= 'ipEnd'."));
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
        RequestValidationErrorResponse result = Validate(Deserialize("""{"ipStart":"10.1.0.5","mask":"24"}"""));

        Assert.That(result.Errors.Select(error => error.Path), Is.EquivalentTo(new List<string> { $"{kPath}.mask", kPath }));
    }

    /// <summary>Verifies the valid keys exposed for help texts are exactly the keys of the block.</summary>
    [Test]
    public void Keys_ListsTheThreeBlockKeys()
    {
        Assert.That(IpRangeOrNetworkRequestValidator.Keys.Select(key => key.JsonName),
            Is.EquivalentTo(new List<string> { "ipStart", "ipEnd", "ipNetwork" }));
    }

    private static RequestValidationErrorResponse Validate(IpRangeOrNetworkRequest? ipBlock)
    {
        RequestValidationErrorResponse result = new();
        IpRangeOrNetworkRequestValidator.Validate(ipBlock, kPath, result);
        return result;
    }

    private static IpRangeOrNetworkRequest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<IpRangeOrNetworkRequest>(json)!;
    }
}
