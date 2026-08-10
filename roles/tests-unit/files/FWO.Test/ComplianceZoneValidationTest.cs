using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test;

[TestFixture]
internal class ComplianceZoneValidationTest
{
    [Test]
    public void ResolveZonesForObjects_RejectsNullRequest()
    {
        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(null!, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("Request body is required"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsUnexpectedRootKey()
    {
        string json = """
        {
          "objects": [],
          "typo": true
        }
        """;

        ResolveZonesForObjectsRequest request = JsonSerializer.Deserialize<ResolveZonesForObjectsRequest>(json)!;

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("resolveZonesForObjects only accepts"));
        });
    }

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
    public void ResolveZonesForObjects_AllowsCidrMaskedIpBoundsAndNormalizesLeaf()
    {
        ResolveZonesForObjectsRequest.LeafObjectRequest leaf = new()
        {
            Name = "Network",
            Type = "network",
            IpStart = "10.0.0.1/24",
            IpEnd = "10.0.0.2/24"
        };
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = [leaf]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(leaf.IpStart, Is.EqualTo("10.0.0.1"));
            Assert.That(leaf.IpEnd, Is.EqualTo("10.0.0.2"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RoundTripsNestedObjects()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.GroupObjectRequest
                {
                    Name = "Root Group",
                    Members =
                    [
                        new ResolveZonesForObjectsRequest.LeafObjectRequest
                        {
                            Name = "Leaf",
                            Type = "network",
                            IpStart = "10.0.0.1",
                            IpEnd = "10.0.0.10"
                        }
                    ]
                }
            ]
        };

        string json = JsonSerializer.Serialize(request);
        ResolveZonesForObjectsRequest roundTrip = JsonSerializer.Deserialize<ResolveZonesForObjectsRequest>(json)!;

        Assert.That(roundTrip.Objects, Has.Count.EqualTo(1));
        Assert.That(roundTrip.Objects[0], Is.TypeOf<ResolveZonesForObjectsRequest.GroupObjectRequest>());
        ResolveZonesForObjectsRequest.GroupObjectRequest group = (ResolveZonesForObjectsRequest.GroupObjectRequest)roundTrip.Objects[0];
        Assert.That(group.Members[0], Is.TypeOf<ResolveZonesForObjectsRequest.LeafObjectRequest>());
        ResolveZonesForObjectsRequest.LeafObjectRequest leaf = (ResolveZonesForObjectsRequest.LeafObjectRequest)group.Members[0];
        Assert.Multiple(() =>
        {
            Assert.That(leaf.Type, Is.EqualTo("network"));
            Assert.That(leaf.IpStart, Is.EqualTo("10.0.0.1"));
            Assert.That(leaf.IpEnd, Is.EqualTo("10.0.0.10"));
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
    public void ResolveZonesForObjects_RejectsNullObjectEntry()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = [null!]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("cannot contain null entries"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsUnsupportedNodeType()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new UnsupportedObjectRequest
                {
                    Name = "Unsupported"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("unsupported object node type"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsMissingType()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.1"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("requires a non-empty 'type'"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsObjectsWithoutMembersOrLeafFields()
    {
        string json = """
        {
          "objects": [
            {
              "name": "Forgotten Group"
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
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must define either non-empty 'members'"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsUnsupportedTypeValue()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "alias",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.1"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("unsupported 'type' value"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsMissingIpEnd()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "10.0.0.1",
                    IpEnd = string.Empty
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("requires non-empty 'ipStart' and 'ipEnd'"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsInvalidIpStart()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "not-an-ip",
                    IpEnd = "10.0.0.1"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid 'ipStart' value"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsInvalidIpEnd()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "10.0.0.1",
                    IpEnd = "not-an-ip"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid 'ipEnd' value"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsMixedAddressFamilies()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "10.0.0.1",
                    IpEnd = "2001:db8::1"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("same address family"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsDescendingIpRange()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "Leaf",
                    Type = "network",
                    IpStart = "10.0.0.2",
                    IpEnd = "10.0.0.1"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must satisfy 'ipStart' <= 'ipEnd'"));
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

    [Test]
    public void ResolveZonesForObjects_RejectsIpv6Addresses()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects =
            [
                new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = "IPv6 Network",
                    Type = "network",
                    IpStart = "2001:db8::1",
                    IpEnd = "2001:db8::ffff"
                }
            ]
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("does not support IPv6 addresses"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipStart'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipEnd'"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsTooManyObjects()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = Enumerable.Range(0, 2048)
                .Select(index => (ResolveZonesForObjectsRequest.ObjectRequest)new ResolveZonesForObjectsRequest.GroupObjectRequest
                {
                    Name = $"Outer-{index}",
                    Members =
                    [
                        new ResolveZonesForObjectsRequest.GroupObjectRequest
                        {
                            Name = $"Inner-{index}",
                            Members =
                            [
                                new ResolveZonesForObjectsRequest.LeafObjectRequest
                                {
                                    Name = $"Leaf-{index}",
                                    Type = "host",
                                    IpStart = "10.0.0.1",
                                    IpEnd = "10.0.0.1"
                                }
                            ]
                        }
                    ]
                })
                .ToList()
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("at most 4096 objects"));
        });
    }

    [Test]
    public void ResolveZonesForObjects_RejectsTooManyRanges()
    {
        ResolveZonesForObjectsRequest request = new()
        {
            Objects = Enumerable.Range(0, 2049)
                .Select(index => (ResolveZonesForObjectsRequest.ObjectRequest)new ResolveZonesForObjectsRequest.LeafObjectRequest
                {
                    Name = $"Range-{index}",
                    Type = "ip_range",
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.2"
                })
                .ToList()
        };

        bool valid = ResolveZonesForObjectsRequestValidator.TryValidate(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("at most 2048 IP ranges"));
        });
    }

    private sealed class UnsupportedObjectRequest : ResolveZonesForObjectsRequest.ObjectRequest
    {
    }
}
