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
