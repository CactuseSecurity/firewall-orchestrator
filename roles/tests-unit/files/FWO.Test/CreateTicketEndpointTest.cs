using System.Text.Json;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
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
    public async Task ValidationRejectsOversizedPersistedStringsAndCollections()
    {
        CreateTicketRequest request = new()
        {
            RequestorName = new string('r', CreateTicketRequest.kMaxIdentifierLength + 1),
            RequestorId = "requestor",
            RuleContactName = "Contact",
            RuleContactId = "contact",
            Title = "Ticket",
            Notes = new string('n', CreateTicketRequest.kMaxTextLength + 1),
            Rules = Enumerable.Range(0, CreateTicketRequest.kMaxCollectionCount + 1)
                .Select(_ => new CreateTicketRequest.CreateTicketRuleRequest { Action = "accept" }).ToList(),
            AddressGroups =
            [
                new CreateTicketRequest.CreateAddressGroupRequest
                {
                    Id = -1,
                    Name = "group",
                    MemberIds = Enumerable.Range(1, CreateTicketRequest.kMaxCollectionCount + 1).Select(value => (long)-value).ToList()
                }
            ]
        };

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);
        ActionResult<CreateTicketResponse> controllerResult = await new WorkflowTicketController(null!).CreateTicket(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("requestorName"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("notes"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("rules"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("addressGroups[0].memberIds"));
            Assert.That(result.Errors.All(error => error.Message.Contains(CreateTicketRequest.kMaxCollectionCount.ToString())
                || error.Message.Contains(CreateTicketRequest.kMaxIdentifierLength.ToString())
                || error.Message.Contains(CreateTicketRequest.kMaxTextLength.ToString())), Is.True);
            Assert.That(controllerResult.Result, Is.TypeOf<BadRequestObjectResult>());
        });
    }

}
