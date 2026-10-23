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

    [Test]
    public void ValidationNormalizesEveryAddressRepresentationForWorkflowConstruction()
    {
        CreateTicketRequest request = CreateValidRequest();
        request.AddressObjects =
        [
            new() { Id = -1, Name = "host", IpHost = "192.0.2.10" },
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
    public void ValidationCoversAllNestedCreateTicketCollections()
    {
        CreateTicketRequest request = new()
        {
            PreWorkflowTicketReference = "previous-ticket",
            RequestorName = "Requester",
            RequestorId = "requester-id",
            RuleContactName = "Contact",
            RuleContactId = "contact-id",
            Title = "Ticket",
            Notes = "Ticket notes",
            Rules =
            [
                new CreateTicketRequest.CreateTicketRuleRequest
                {
                    Predicate = "create",
                    Action = "accept",
                    Name = "Rule",
                    Description = "Task description",
                    Reason = "Task reason",
                    ViolationJustification = "Justification",
                    SourceObjects = [1],
                    SourceGroups = [2],
                    DestinationObjects = [3],
                    DestinationGroups = [4],
                    ServiceObjects = [5],
                    ServiceGroups = [6]
                }
            ],
            AddressObjects =
            [
                new CreateTicketRequest.CreateAddressObjectRequest
                {
                    Predicate = "create",
                    Name = "Address",
                    IpRange = ["192.0.2.1", "192.0.2.2"]
                }
            ],
            AddressGroups =
            [new CreateTicketRequest.CreateAddressGroupRequest { Name = "Address group", MemberIds = [1] }],
            ServiceObjects =
            [
                new CreateTicketRequest.CreateServiceObjectRequest
                {
                    Predicate = "create",
                    Name = "Service",
                    Protocol = "tcp"
                }
            ],
            ServiceGroups =
            [new CreateTicketRequest.CreateServiceGroupRequest { Name = "Service group", MemberIds = [1] }],
            TimeObjects =
            [
                new CreateTicketRequest.CreateTimeObjectRequest
                {
                    Name = "Time object",
                    StartTime = "2026-01-01",
                    EndTime = "2026-01-31"
                }
            ]
        };

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);

        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ValidationHandlesNullCollectionsAndMissingRules()
    {
        CreateTicketRequest request = new()
        {
            AddressObjects = null!,
            AddressGroups = null!,
            ServiceObjects = null!,
            ServiceGroups = null!,
            TimeObjects = null!,
            Rules = null!
        };

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("rules"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("addressObjects"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("addressGroups"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("serviceObjects"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("serviceGroups"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("timeObjects"));
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

    [TestCase("ipStart")]
    [TestCase("ipEnd")]
    public void ValidationRejectsAddressObjectsWithOnlyRemovedLegacyFields(string legacyField)
    {
        string json = $$"""
        {
          "requestorName": "Alice",
          "requestorId": "alice",
          "ruleContactName": "Bob",
          "ruleContactId": "bob",
          "title": "Legacy address input",
          "addressObjects": [{"id":-1,"name":"legacy","{{legacyField}}":"192.0.2.10"}],
          "rules": [{"action":"accept"}]
        }
        """;
        CreateTicketRequest request = JsonSerializer.Deserialize<CreateTicketRequest>(json)!;

        RequestValidationErrorResponse result = CreateTicketRequestValidator.Validate(request);

        Assert.That(result.Errors.Single(error => error.Path == "addressObjects[0]").Message,
            Does.Contain("exactly one"));
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
