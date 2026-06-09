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
}
