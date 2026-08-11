using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowRequestContractTest
{

    [Test]
    public void GetRequestStatusRequest_RequiresTicketId()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetRequestStatusRequest>("{}"));
    }

    [Test]
    public void GetRequestStatusRequest_SupportsBigintTicketId()
    {
        const long ticketId = (long)int.MaxValue + 1;

        GetRequestStatusRequest? request = JsonSerializer.Deserialize<GetRequestStatusRequest>($$"""{"ticketId":{{ticketId}}}""");

        Assert.That(request?.TicketId, Is.EqualTo(ticketId));
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

    [Test]
    public void GetTimeObjectIdRequest_AllowsMissingStartTime()
    {
        GetTimeObjectIdRequest? request = JsonSerializer.Deserialize<GetTimeObjectIdRequest>("""{"endTime":"2026-06-01T17:30:00Z"}""");

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.StartTime, Is.Null);
            Assert.That(request.EndTime, Is.EqualTo(new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero)));
        });
    }

    [Test]
    public void GetTimeObjectIdRequest_AllowsMissingEndTime()
    {
        GetTimeObjectIdRequest? request = JsonSerializer.Deserialize<GetTimeObjectIdRequest>("""{"startTime":"2026-06-01T08:00:00Z"}""");

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.StartTime, Is.EqualTo(new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)));
            Assert.That(request.EndTime, Is.Null);
        });
    }

    [Test]
    public void GetTimeObjectIdRequest_AllowsMissingBothBounds()
    {
        GetTimeObjectIdRequest? request = JsonSerializer.Deserialize<GetTimeObjectIdRequest>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.StartTime, Is.Null);
            Assert.That(request.EndTime, Is.Null);
        });
    }
}
