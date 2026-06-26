using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowRequestContractTest
{
    [TestCase("""{"ipStart":"10.0.0.1","ipEnd":"10.0.0.2"}""")]
    public void GenerateAddressObjectNameRequest_RequiresNetMask(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GenerateAddressObjectNameRequest>(json));
    }

    [TestCase("""{"portEnd":443,"protocol":"tcp","typ":"service"}""")]
    [TestCase("""{"portStart":443,"protocol":"tcp","typ":"service"}""")]
    public void GenerateServiceObjectNameRequest_RequiresPortBounds(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GenerateServiceObjectNameRequest>(json));
    }

    [TestCase("""{"ipAddress":"10.0.0.1","minPrefixLength":24}""")]
    [TestCase("""{"ipAddress":"10.0.0.1","netMask":24}""")]
    public void GetNetObjectValidityRequest_RequiresPrefixParameters(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetNetObjectValidityRequest>(json));
    }

    [Test]
    public void GetRequestStatusRequest_RequiresTicketId()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetRequestStatusRequest>("{}"));
    }

    [Test]
    public void GetRequestStatusResponse_UsesExpectedJsonNames()
    {
        GetRequestStatusResponse response = new()
        {
            Status = "implementation",
            StatusComment = "latest"
        };

        string json = JsonSerializer.Serialize(response);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"status\":\"implementation\""));
            Assert.That(json, Does.Contain("\"statusComment\":\"latest\""));
        });
    }

    [TestCase("""{"protocol":"tcp","portEnd":443}""")]
    [TestCase("""{"protocol":"tcp","portStart":443}""")]
    public void GetServiceObjectIdRequest_RequiresPortBounds(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetServiceObjectIdRequest>(json));
    }

    [TestCase("""{"portStart":443,"portEnd":443}""")]
    public void GetServiceObjectIdRequest_RequiresProtocol(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetServiceObjectIdRequest>(json));
    }

    [TestCase("""{"ipEnd":"10.0.0.2"}""")]
    [TestCase("""{"ipStart":"10.0.0.1"}""")]
    public void GetAddressObjectIdRequest_RequiresIpBounds(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetAddressObjectIdRequest>(json));
    }
}
