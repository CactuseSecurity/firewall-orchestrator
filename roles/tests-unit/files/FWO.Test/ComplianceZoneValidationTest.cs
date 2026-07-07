using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneValidationTest
{
    [Test]
    public void GetZonesForDraftObjects_AllowsNestedDraftGroups()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Root Group",
              "type": "group",
              "members": [
                {
                  "name": "Leaf",
                  "type": "network",
                  "ipStart": "10.0.0.1",
                  "ipEnd": "10.0.0.1"
                },
                {
                  "name": "Nested Group",
                  "type": "group",
                  "members": [
                    {
                      "name": "Nested Leaf",
                      "type": "ip_range",
                      "ipStart": "10.0.1.1",
                      "ipEnd": "10.0.1.10"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        GetZonesForDraftObjectsRequest request = JsonSerializer.Deserialize<GetZonesForDraftObjectsRequest>(json)!;

        bool valid = GetZonesForDraftObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void GetZonesForDraftObjects_RejectsUnknownNestedKey()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Root Group",
              "type": "group",
              "members": [
                {
                  "name": "Leaf",
                  "type": "network",
                  "ipStart": "10.0.0.1",
                  "ipEnd": "10.0.0.1",
                  "typo": true
                }
              ]
            }
          ]
        }
        """;

        GetZonesForDraftObjectsRequest request = JsonSerializer.Deserialize<GetZonesForDraftObjectsRequest>(json)!;

        bool valid = GetZonesForDraftObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("members entry at index 0"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("only accepts"));
        });
    }

    [Test]
    public void GetZonesForDraftObjects_RejectsLeafObjectsWithMembers()
    {
        GetZonesForDraftObjectsRequest request = new()
        {
            Objects =
            [
                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.1",
                    Members =
                    [
                        new GetZonesForDraftObjectsRequest.DraftObjectRequest
                        {
                            Name = "Child",
                            Type = "host",
                            IpStart = "10.0.0.2",
                            IpEnd = "10.0.0.2"
                        }
                    ]
                }
            ]
        };

        bool valid = GetZonesForDraftObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must not define 'members'"));
        });
    }

    [Test]
    public void GetZonesForDraftObjects_RejectsHostRanges()
    {
        GetZonesForDraftObjectsRequest request = new()
        {
            Objects =
            [
                new GetZonesForDraftObjectsRequest.DraftObjectRequest
                {
                    Name = "Host",
                    Type = "host",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.2"
                }
            ]
        };

        bool valid = GetZonesForDraftObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must use the same 'ipStart' and 'ipEnd'"));
        });
    }
}
