using System.Reflection;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.OpenApi;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the filter that keeps a model binding failure in the documented error shape, and the
/// wiring both it and the OpenAPI schema transformer depend on.
/// </summary>
[TestFixture]
internal class AggregatedValidationErrorsAttributeTest
{
    /// <summary>
    /// Order of the automatic model state filter of [ApiController]; ours has to run before it.
    /// </summary>
    private const int kModelStateInvalidFilterOrder = -2000;

    [Test]
    public void TheFilterRunsBeforeTheAutomaticModelStateFilter()
    {
        // Without this ordering the framework answers first and the documented shape never applies.
        Assert.That(new AggregatedValidationErrorsAttribute().Order, Is.LessThan(kModelStateInvalidFilterOrder));
    }

    [Test]
    public void TheChangeHistoryControllerUsesTheFilter()
    {
        Assert.That(typeof(WorkflowChangeHistoryController).GetCustomAttribute<AggregatedValidationErrorsAttribute>(),
            Is.Not.Null);
    }

    [Test]
    public void BindingFailuresAreReportedInTheDocumentedShape()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("options", "The JSON value could not be converted.");
        modelState.AddModelError("ticketId", "The JSON value could not be converted.");

        RequestValidationErrorResponse response = AggregatedValidationErrorsAttribute.BuildResponse(modelState);

        Assert.Multiple(() =>
        {
            Assert.That(response.Errors.Select(error => error.Path), Is.EquivalentTo(new List<string> { "options", "ticketId" }));
            Assert.That(response.Errors, Has.All.Property(nameof(RequestValidationError.Message)).Not.Empty);
        });
    }

    [Test]
    public void AnErrorCarryingOnlyAnExceptionStillGetsAMessage()
    {
        ModelStateDictionary modelState = new();
        modelState.TryAddModelException("request", new InvalidOperationException("boom"));

        RequestValidationErrorResponse response = AggregatedValidationErrorsAttribute.BuildResponse(modelState);

        Assert.Multiple(() =>
        {
            Assert.That(response.Errors, Has.Count.EqualTo(1));
            Assert.That(response.Errors[0].Message, Does.Contain("documented JSON type"));
        });
    }

    [Test]
    public void TheSchemaTransformerStaysRegisteredInProgram()
    {
        // The transformer is unit tested on its own, but nothing else fails when the registration is
        // dropped - the OpenAPI document would silently lose every required marker. The registration
        // is not reachable from a test without booting the host, so the source is asserted instead.
        string program = File.ReadAllText(LocateProgramFile());

        Assert.That(program, Does.Contain($"AddSchemaTransformer<{nameof(OpenApiRequiredSchemaTransformer)}>"));
    }

    private static string LocateProgramFile()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            FileInfo candidate = new(Path.Combine(directory.FullName,
                "roles", "middleware", "files", "FWO.Middleware.Server", "Program.cs"));
            if (candidate.Exists)
            {
                return candidate.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Ignore("The middleware sources are not reachable in this environment.");
        return string.Empty;
    }
}
