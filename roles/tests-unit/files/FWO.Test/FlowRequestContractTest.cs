using System.Text.Json;
using FWO.Middleware.Server.Requests;
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

    [TestCase("""{"endTime":"2026-06-01T17:30:00Z"}""")]
    [TestCase("""{"startTime":"2026-06-01T08:00:00Z"}""")]
    public void GetTimeObjectIdRequest_RequiresTimeBounds(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetTimeObjectIdRequest>(json));
    }
}
