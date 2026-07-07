using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneValidationTest
{
    [Test]
    public void ResolveZonesForObjects_AllowsNestedGroups()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Root Group",
              "members": [
                {
                  "name": "Leaf",
                  "type": "network",
                  "ipStart": "10.0.0.1",
                  "ipEnd": "10.0.0.1"
                },
                {
                  "name": "Nested Group",
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

        ResolveZonesForObjectsRequest request = JsonSerializer.Deserialize<ResolveZonesForObjectsRequest>(json)!;

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsEmptyRequest()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = []
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must contain at least one entry"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsEmptyGroups()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.GroupObjectRequest
                {
                    Name = "Empty Group",
                    Members = []
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must contain at least one member"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsUnknownNestedKey()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Root Group",
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

        ResolveZonesForObjectsRequest request = JsonSerializer.Deserialize<ResolveZonesForObjectsRequest>(json)!;

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("members entry at index 0"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("only accepts"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsGroupsWithIpFields()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Group",
              "members": [
                {
                  "name": "Leaf",
                  "type": "host",
                  "ipStart": "10.0.0.2",
                  "ipEnd": "10.0.0.2"
                }
              ],
              "ipStart": "10.0.0.1",
              "ipEnd": "10.0.0.1"
            }
          ]
        }
        """;

        ResolveZonesForObjectsRequest request = JsonSerializer.Deserialize<ResolveZonesForObjectsRequest>(json)!;

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("only accepts"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsHostRanges()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Host",
                    Type = "host",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.2"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must use the same 'ipStart' and 'ipEnd'"));
        });
    }
}
