using System.Text.Json;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

/// <summary>
/// Covers the create-ticket request validation contract.
/// </summary>
[TestFixture]
internal class CreateTicketEndpointTest
{
    private static readonly List<string> kMultipleErrorPaths =
    [
        "requestorName", "requestorId", "ruleContactName", "ruleContactId", "title",
        "rules[0].sourceObjects"
    ];

    [Test]
    public void ValidationReportsAllErrorsWithPrecisePaths()
    {
        const string invalidRequest =
            """{"requestorName":"","requestorId":"","ruleContactName":"","ruleContactId":"","title":"","rules":["""
            + """{"action":"accept","sourceObjects":[0]}]}""";
        CreateTicketRequest request = JsonSerializer.Deserialize<CreateTicketRequest>(invalidRequest)!;

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Is.EquivalentTo(kMultipleErrorPaths));
            Assert.That(result.Errors.Single(error => error.Path == "rules[0].sourceObjects").Message,
                Does.Contain("non-zero integers"));
        });
    }

    [Test]
    public async Task ControllerReturnsAggregatedValidationResponseBeforeCreatingTicket()
    {
        WorkflowTicketController controller = new(null!);

        ActionResult<CreateTicketResponse> result = await controller.CreateTicket(new CreateTicketRequest
        {
            RequestorName = "",
            RequestorId = "",
            RuleContactName = "",
            RuleContactId = "",
            Title = "",
            Rules = [new CreateTicketRequest.CreateTicketRuleRequest { SourceObjects = [0] }]
        });

        RequestValidationErrorResponse errors = (RequestValidationErrorResponse)((BadRequestObjectResult)result.Result!).Value!;
        Assert.That(errors.Errors.Select(error => error.Path), Does.Contain("rules[0].sourceObjects"));
    }

    [Test]
    public void ValidationNormalizesEveryAddressRepresentationForWorkflowConstruction()
    {
        CreateTicketRequest request = CreateValidRequest();
        request.AddressObjects =
        [
            new() { Id = -1, Name = "host", IpHost = "192.000.002.010" },
            new() { Id = -2, Name = "network", IpNetwork = "198.51.100.0/25" },
            new() { Id = -3, Name = "range", IpRange = ["2001:db8::1", "2001:db8::10"] }
        ];

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);
        List<WorkflowTicketEntity> entities = request.AddressObjects
            .Select(address => WorkflowTicketEntity.FromAddressObject(address.Id, address))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Is.Empty);
            Assert.That((entities[0].IpStart, entities[0].IpEnd), Is.EqualTo(("192.0.2.10", "192.0.2.10")));
            Assert.That((entities[1].IpStart, entities[1].IpEnd), Is.EqualTo(("198.51.100.0", "198.51.100.127")));
            Assert.That((entities[2].IpStart, entities[2].IpEnd), Is.EqualTo(("2001:db8::1", "2001:db8::10")));
        });
    }

    [Test]
    public void ValidationReportsAddressInputErrorsAtTheAddressObjectPath()
    {
        CreateTicketRequest request = CreateValidRequest();
        request.AddressObjects =
        [
            new()
            {
                Id = -1,
                Name = "ambiguous",
                IpHost = "192.0.2.10",
                IpNetwork = "192.0.2.0/24"
            }
        ];

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);

        Assert.That(result.Errors.Single(error => error.Path == "addressObjects[0]").Message, Does.Contain("exactly one"));
    }

    private static CreateTicketRequest CreateValidRequest()
    {
        return new CreateTicketRequest
        {
            RequestorName = "Alice",
            RequestorId = "alice",
            RuleContactName = "Bob",
            RuleContactId = "bob",
            Title = "Address input test",
            Rules = [new CreateTicketRequest.CreateTicketRuleRequest { Action = "accept" }]
        };
    }

}
