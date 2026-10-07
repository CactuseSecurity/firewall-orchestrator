using System.Text.Json;
using FWO.Middleware.Server.Requests;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowComplianceValidationTest
{
    [Test]
    public void GetPolicyIds_AllowsEmptyBody()
    {
        GetPolicyIdsRequest request = JsonSerializer.Deserialize<GetPolicyIdsRequest>("{}")!;

        bool valid = FlowComplianceRequestValidator.TryValidatePolicyIds(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void GetFlowComplianceState_AllowsExpectedShape()
    {
        string json = """
        {
          "source": [{"ipRange":["10.0.0.1","10.0.0.2"]}],
          "destination": [{"ipHost":"10.0.1.1"}],
          "service": [{"portStart":443,"portEnd":443,"protocol":"TCP"}],
          "policies": [1,2]
        }
        """;

        GetFlowComplianceStateRequest request = JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void GetFlowComplianceState_AllowsPortZero()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 0, PortEnd = 0, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void GetFlowComplianceState_ExpandsIpv4Network()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "10.0.0.0/24" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "10.0.1.0/25" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(request.Source[0].NormalizedIpStart, Is.EqualTo("10.0.0.0"));
            Assert.That(request.Source[0].NormalizedIpEnd, Is.EqualTo("10.0.0.255"));
            Assert.That(request.Destination[0].NormalizedIpStart, Is.EqualTo("10.0.1.0"));
            Assert.That(request.Destination[0].NormalizedIpEnd, Is.EqualTo("10.0.1.127"));
        });
        Assert.That(FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out errorResult), Is.True);
        Assert.That(errorResult, Is.Null);
        Assert.That(request.Source[0].IpNetwork, Is.EqualTo("10.0.0.0/24"));
        Assert.That(request.Destination[0].IpNetwork, Is.EqualTo("10.0.1.0/25"));
    }

    [TestCase("10.0.0.10", "10.0.0.10")]
    [TestCase("2001:db8::10", "2001:db8::10")]
    [TestCase("192.000.002.010", "192.0.2.8")]
    public void GetFlowComplianceState_AcceptsIpHost(string ipNetwork, string expectedAddress)
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpHost = ipNetwork }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpHost = "192.0.2.1" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(request.Source[0].NormalizedIpStart, Is.EqualTo(expectedAddress));
            Assert.That(request.Source[0].NormalizedIpEnd, Is.EqualTo(expectedAddress));
        });
        Assert.That(FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out errorResult), Is.True);
        Assert.That(errorResult, Is.Null);
        Assert.That(request.Source[0].IpHost, Is.EqualTo(ipNetwork));
    }

    [Test]
    public void GetFlowComplianceState_ExpandsIpv6Network()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "2001:db8::/126" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "2001:db8:1::/64" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(request.Source[0].NormalizedIpStart, Is.EqualTo("2001:db8::"));
            Assert.That(request.Source[0].NormalizedIpEnd, Is.EqualTo("2001:db8::3"));
            Assert.That(request.Destination[0].NormalizedIpStart, Is.EqualTo("2001:db8:1::"));
            Assert.That(request.Destination[0].NormalizedIpEnd, Is.EqualTo("2001:db8:1:0:ffff:ffff:ffff:ffff"));
        });
    }

    [Test]
    public void GetFlowComplianceState_ExpandsWholeAddressSpaceNetworks()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "0.0.0.0/0" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = "::/0" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(request.Source[0].NormalizedIpStart, Is.EqualTo("0.0.0.0"));
            Assert.That(request.Source[0].NormalizedIpEnd, Is.EqualTo("255.255.255.255"));
            Assert.That(request.Destination[0].NormalizedIpStart, Is.EqualTo("::"));
            Assert.That(request.Destination[0].NormalizedIpEnd, Is.EqualTo("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"));
        });
    }

    [TestCase("10.0.0.1/33")]
    [TestCase("2001:db8::1/129")]
    [TestCase("10.0.0.1/not-a-prefix")]
    public void GetFlowComplianceState_RejectsInvalidNetworkPrefix(string ipNetwork)
    {
        bool valid = TryValidateSourceNetwork(ipNetwork, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid"));
        });
    }

    [TestCase("/24")]
    [TestCase("10.0.0.0/24/24")]
    public void GetFlowComplianceState_RejectsNetworkWithoutSinglePrefixSeparator(string ipNetwork)
    {
        bool valid = TryValidateSourceNetwork(ipNetwork, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid 'ipNetwork'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_NetworkErrorNamesTheEntryOnlyOnce()
    {
        bool valid = TryValidateSourceNetwork("10.0.0.1/33", out ActionResult? errorResult);
        string message = ((BadRequestObjectResult)errorResult!).Value?.ToString() ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(message, Does.StartWith("'source' entry at index 0 "));
            Assert.That(message, Does.Not.Contain("''"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsNetworkTogetherWithRangeBounds()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source =
            [
                new GetFlowComplianceStateRequest.IpRangeRequest
                {
                    IpNetwork = "10.0.0.0/24",
                    IpRange = ["10.0.0.1", "10.0.0.2"]
                }
            ],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("exactly one"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsEntryWithoutNetworkAndRangeBounds()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest()],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("exactly one"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMasksInRangeEndpoints()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1/32", "10.0.0.2/32"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["2001:db8::1/128", "2001:db8::2/128"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid 'ipRange[0]'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsBroaderMaskedRangeBounds()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1/24", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("invalid 'ipRange[0]'"));
        });
    }

    /// <summary>
    /// Validates a request whose only source entry is the supplied CIDR network.
    /// </summary>
    private static bool TryValidateSourceNetwork(string ipNetwork, out ActionResult? errorResult)
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpNetwork = ipNetwork }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        return FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out errorResult);
    }

    [Test]
    public void GetFlowComplianceState_RejectsUnknownRootKey()
    {
        string json = """
        {
          "source": [],
          "destination": [],
          "service": [],
          "policies": [],
          "typo": true
        }
        """;

        GetFlowComplianceStateRequest request = JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("getFlowComplianceState"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsUnknownNestedServiceKey()
    {
        string json = """
        {
          "source": [],
          "destination": [],
          "service": [{"portStart":443,"portEnd":443,"protocol":"TCP","typo":true}],
          "policies": [1]
        }
        """;

        GetFlowComplianceStateRequest request = JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("service"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'portStart'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMissingIpBounds()
    {
        string json = """
        {
          "source": [{"ipRange":["10.0.0.1"]}],
          "destination": [{"ipHost":"10.0.1.1"}],
          "service": [{"portStart":443,"portEnd":443,"protocol":"TCP"}],
          "policies": [1]
        }
        """;

        GetFlowComplianceStateRequest request = JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'source'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipRange'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMissingServiceProtocol()
    {
        string json = """
        {
          "source": [{"ipRange":["10.0.0.1","10.0.0.2"]}],
          "destination": [{"ipHost":"10.0.1.1"}],
          "service": [{"portStart":443,"portEnd":443}],
          "policies": [1]
        }
        """;

        GetFlowComplianceStateRequest request = JsonSerializer.Deserialize<GetFlowComplianceStateRequest>(json)!;

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'service'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'protocol'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsUnparseableIpAddress()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["banana", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipRange[0]'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("index 0"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMixedAddressFamilies()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1", "::1"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("same address family"));
        });
    }

    [Test]
    public void GetFlowComplianceState_AllowsIpv6Ranges()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["2001:db8::1", "2001:db8::ffff"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsDescendingIpRange()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.2", "10.0.0.1"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("lower to its upper address"));
        });
    }

    [TestCase(-5, 443, "portStart")]
    [TestCase(443, 70000, "portEnd")]
    public void GetFlowComplianceState_RejectsPortsOutsideAllowedRange(int portStart, int portEnd, string expectedField)
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = portStart, PortEnd = portEnd, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain(expectedField));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("0-65535"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsDescendingPortRange()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 1024, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'portStart' <= 'portEnd'"));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void GetFlowComplianceState_RejectsNonPositivePolicyIds(int policyId)
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.0.1", "10.0.0.2"] }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpRange = ["10.0.1.1", "10.0.1.2"] }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [policyId]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'policies'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("positive integers"));
        });
    }
}
