using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class RequestValidationContractTest
{
    private static readonly string[] kOptionsLimitKey = new string[] { "options.limit" };
    private static readonly string[] kOptionsLimitErrors = new string[]
    {
        "options.limit must be positive.",
        "options.limit must not exceed 1000."
    };
    private static readonly string[] kUnknownFilterTypoError = new string[] { "Unknown field 'filter.typo'." };
    private static readonly string[] kMissingIpStartError = new string[] { "Required field 'ipStart' is missing." };
    private static readonly string[] kRootPathKey = new string[] { RequestFieldPath.Root };
    private static readonly string[] kRequestBodyRequiredError = new string[] { "Request body is required." };
    private static readonly string[] kUnknownUnexpectedError = new string[] { "Unknown field 'unexpected'." };

    [Test]
    public void RequestFieldPath_BuildsCanonicalPaths()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RequestFieldPath.Child(RequestFieldPath.Root, "filter"), Is.EqualTo("filter"));
            Assert.That(RequestFieldPath.Child("options", "filter"), Is.EqualTo("options.filter"));
            Assert.That(RequestFieldPath.Indexed("options.filter.applicationName", 0), Is.EqualTo("options.filter.applicationName[0]"));
        });
    }

    [Test]
    public void RequestValidationErrors_CollectsMultipleErrorsPerField()
    {
        RequestValidationErrors errors = new();

        errors.Add("options.limit", "options.limit must be positive.");
        errors.Add("options.limit", "options.limit must not exceed 1000.");

        Dictionary<string, string[]> errorDictionary = errors.ToDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(errors.HasErrors, Is.True);
            Assert.That(errorDictionary.Keys, Is.EquivalentTo(kOptionsLimitKey));
            Assert.That(errorDictionary["options.limit"], Is.EqualTo(kOptionsLimitErrors));
        });
    }

    [Test]
    public void RequestValidationErrors_AddsContractMessages()
    {
        RequestValidationErrors errors = new();

        errors.AddUnknownField("filter.typo");
        errors.AddMissingRequiredField("ipStart");

        Dictionary<string, string[]> errorDictionary = errors.ToDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(errorDictionary["filter.typo"], Is.EqualTo(kUnknownFilterTypoError));
            Assert.That(errorDictionary["ipStart"], Is.EqualTo(kMissingIpStartError));
        });
    }

    [Test]
    public void RequestValidationErrors_UsesRootPathForBodyLevelErrors()
    {
        RequestValidationErrors errors = new();

        errors.Add(RequestFieldPath.Root, "Request body is required.");

        Dictionary<string, string[]> errorDictionary = errors.ToDictionary();

        Assert.Multiple(() =>
        {
            Assert.That(errorDictionary.Keys, Is.EquivalentTo(kRootPathKey));
            Assert.That(errorDictionary[RequestFieldPath.Root], Is.EqualTo(kRequestBodyRequiredError));
        });
    }

    [Test]
    public void RequestValidationProblemDetailsFactory_BuildsUniformBadRequest()
    {
        RequestValidationErrors errors = new();
        errors.AddUnknownField("unexpected");

        BadRequestObjectResult result = RequestValidationProblemDetailsFactory.BadRequest(errors);
        ValidationProblemDetails problemDetails = (ValidationProblemDetails)result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problemDetails.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problemDetails.Title, Is.EqualTo(RequestValidationProblemDetailsFactory.Title));
            Assert.That(problemDetails.Errors["unexpected"], Is.EqualTo(kUnknownUnexpectedError));
        });
    }
}
