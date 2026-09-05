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
          "source": [{"ipStart":"10.0.0.1","ipEnd":"10.0.0.2"}],
          "destination": [{"ipStart":"10.0.1.1","ipEnd":"10.0.1.2"}],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
            Assert.That(request.Source[0].IpStart, Is.EqualTo("10.0.0.0"));
            Assert.That(request.Source[0].IpEnd, Is.EqualTo("10.0.0.255"));
            Assert.That(request.Destination[0].IpStart, Is.EqualTo("10.0.1.0"));
            Assert.That(request.Destination[0].IpEnd, Is.EqualTo("10.0.1.127"));
        });
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
            Assert.That(request.Source[0].IpStart, Is.EqualTo("2001:db8::"));
            Assert.That(request.Source[0].IpEnd, Is.EqualTo("2001:db8::3"));
            Assert.That(request.Destination[0].IpStart, Is.EqualTo("2001:db8:1::"));
            Assert.That(request.Destination[0].IpEnd, Is.EqualTo("2001:db8:1:0:ffff:ffff:ffff:ffff"));
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
            Assert.That(request.Source[0].IpStart, Is.EqualTo("0.0.0.0"));
            Assert.That(request.Source[0].IpEnd, Is.EqualTo("255.255.255.255"));
            Assert.That(request.Destination[0].IpStart, Is.EqualTo("::"));
            Assert.That(request.Destination[0].IpEnd, Is.EqualTo("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"));
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

    [Test]
    public void GetFlowComplianceState_RejectsIpv4MappedIpv6Network()
    {
        bool valid = TryValidateSourceNetwork("::ffff:192.0.2.0/120", out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("IPv4-mapped IPv6"));
        });
    }

    [Test]
    public void TryValidateIpRange_RejectsIpv4MappedIpv6Bound()
    {
        bool valid = FlowComplianceRequestValidator.TryValidateIpRange(
            "::ffff:192.0.2.10",
            "::ffff:192.0.2.11",
            "address",
            0,
            out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorMessage, Does.Contain("IPv4-mapped IPv6"));
        });
    }

    [TestCase("10.0.0.0")]
    [TestCase("2001:db8::")]
    [TestCase("/24")]
    [TestCase("10.0.0.0/24/24")]
    public void GetFlowComplianceState_RejectsNetworkWithoutSinglePrefixSeparator(string ipNetwork)
    {
        bool valid = TryValidateSourceNetwork(ipNetwork, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("requires a valid CIDR network"));
        });
    }

    [TestCase("10.0.0.1/24", "10.0.0.0/24")]
    [TestCase("2001:db8::1/126", "2001:db8::/126")]
    public void GetFlowComplianceState_RejectsNetworkWithHostBits(string ipNetwork, string expectedSuggestion)
    {
        bool valid = TryValidateSourceNetwork(ipNetwork, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("must not set host bits"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain(expectedSuggestion));
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
                    IpStart = "10.0.0.1",
                    IpEnd = "10.0.0.2"
                }
            ],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("not both"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsEntryWithoutNetworkAndRangeBounds()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest()],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("requires non-empty 'ipStart' and 'ipEnd', or a non-empty 'ipNetwork'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_AllowsHostMaskedRangeBoundsAndNormalizesRequest()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1/32", IpEnd = "10.0.0.2/32" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "2001:db8::1/128", IpEnd = "2001:db8::2/128" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorResult, Is.Null);
            Assert.That(request.Source[0].IpStart, Is.EqualTo("10.0.0.1"));
            Assert.That(request.Source[0].IpEnd, Is.EqualTo("10.0.0.2"));
            Assert.That(request.Destination[0].IpStart, Is.EqualTo("2001:db8::1"));
            Assert.That(request.Destination[0].IpEnd, Is.EqualTo("2001:db8::2"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsBroaderMaskedRangeBounds()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1/24", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("Only '/32' is allowed"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("use 'ipNetwork'"));
        });
    }

    [TestCase("10.0.0.1/32", "10.0.0.2/32")]
    [TestCase("2001:db8::1/128", "2001:db8::2/128")]
    public void TryValidateIpRange_AllowsHostMaskedBounds(string ipStart, string ipEnd)
    {
        bool valid = FlowComplianceRequestValidator.TryValidateIpRange(ipStart, ipEnd, "address", 0, out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorMessage, Is.Null);
        });
    }

    [TestCase("10.0.0.1/24", "10.0.0.2/32", "Only '/32' is allowed")]
    [TestCase("10.0.0.1/255.255.255.0", "10.0.0.2", "Only '/32' is allowed")]
    [TestCase("2001:db8::1/64", "2001:db8::2/128", "Only '/128' is allowed")]
    public void TryValidateIpRange_RejectsBroaderMaskedBounds(string ipStart, string ipEnd, string expectedDetail)
    {
        bool valid = FlowComplianceRequestValidator.TryValidateIpRange(ipStart, ipEnd, "address", 0, out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorMessage, Does.Contain(expectedDetail));
            Assert.That(errorMessage, Does.Contain("'ipStart'"));
        });
    }

    [Test]
    public void TryValidateIpRange_AllowsIpv6RangeBounds()
    {
        bool valid = FlowComplianceRequestValidator.TryValidateIpRange("2001:db8::1", "2001:db8::2", "address", 0, out string? errorMessage);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.True);
            Assert.That(errorMessage, Is.Null);
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
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
          "source": [{"ipStart":"10.0.0.1"}],
          "destination": [{"ipStart":"10.0.1.1","ipEnd":"10.0.1.2"}],
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
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipStart'"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMissingServiceProtocol()
    {
        string json = """
        {
          "source": [{"ipStart":"10.0.0.1","ipEnd":"10.0.0.2"}],
          "destination": [{"ipStart":"10.0.1.1","ipEnd":"10.0.1.2"}],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "banana", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipStart'"));
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("index 0"));
        });
    }

    [Test]
    public void GetFlowComplianceState_RejectsMixedAddressFamilies()
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1", IpEnd = "::1" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "2001:db8::1", IpEnd = "2001:db8::ffff" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.2", IpEnd = "10.0.0.1" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
            Service = [new GetFlowComplianceStateRequest.ServiceRangeRequest { PortStart = 443, PortEnd = 443, Protocol = "TCP" }],
            Policies = [1]
        };

        bool valid = FlowComplianceRequestValidator.TryValidateFlowComplianceState(request, out ActionResult? errorResult);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(errorResult, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)errorResult!).Value?.ToString(), Does.Contain("'ipStart' <= 'ipEnd'"));
        });
    }

    [TestCase(-5, 443, "portStart")]
    [TestCase(443, 70000, "portEnd")]
    public void GetFlowComplianceState_RejectsPortsOutsideAllowedRange(int portStart, int portEnd, string expectedField)
    {
        GetFlowComplianceStateRequest request = new()
        {
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
            Source = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.0.1", IpEnd = "10.0.0.2" }],
            Destination = [new GetFlowComplianceStateRequest.IpRangeRequest { IpStart = "10.0.1.1", IpEnd = "10.0.1.2" }],
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
