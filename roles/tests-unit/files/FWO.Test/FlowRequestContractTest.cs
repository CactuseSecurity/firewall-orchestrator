using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowRequestContractTest
{

    [Test]
    public void GetTicketStatusRequest_RequiresTicketId()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GetTicketStatusRequest>("{}"));
    }

    [Test]
    public void GetTicketStatusRequest_SupportsBigintTicketId()
    {
        const long ticketId = (long)int.MaxValue + 1;

        GetTicketStatusRequest? request = JsonSerializer.Deserialize<GetTicketStatusRequest>($$"""{"ticketId":{{ticketId}}}""");

        Assert.That(request?.TicketId, Is.EqualTo(ticketId));
    }

    [Test]
    public void CreateTicketRequest_UsesEmptyOptionsAndDefaultsSortTasksToFalse()
    {
        CreateTicketRequest? request = JsonSerializer.Deserialize<CreateTicketRequest>(
            """{"requestorName":"Alice Example","requestorId":"alice","ruleContactName":"Bob Approver","ruleContactId":"bob","title":"Allow HTTPS"}""");

        Assert.Multiple(() =>
        {
            Assert.That(request?.Options, Is.Not.Null);
            Assert.That(request?.Options.SortTasks, Is.Null);
        });
    }

    [Test]
    public void CreateTicketRequest_SerializesExplicitSortTasksInsideOptions()
    {
        CreateTicketRequest request = new()
        {
            RequestorName = "Alice Example",
            RequestorId = "alice",
            RuleContactName = "Bob Approver",
            RuleContactId = "bob",
            Title = "Allow HTTPS",
            Options = new CreateTicketRequest.CreateTicketOptions { SortTasks = true }
        };

        string json = JsonSerializer.Serialize(request);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"options\":{"));
            Assert.That(json, Does.Contain("\"sortTasks\":true"));
            Assert.That(json, Does.Not.Contain("\"sortTasks\":false"));
        });
    }

    [Test]
    public void GetTicketStatusResponse_UsesExpectedJsonNames()
    {
        GetTicketStatusResponse response = new()
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

    [Test]
    public void GetServiceObjectIdRequest_AllowsExplicitNullPortBounds()
    {
        GetServiceObjectIdRequest? request = JsonSerializer.Deserialize<GetServiceObjectIdRequest>(
            """{"protocol":"ANY","portStart":null,"portEnd":null}""");

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.PortStart, Is.Null);
            Assert.That(request.PortEnd, Is.Null);
            Assert.That(request.Protocol, Is.EqualTo("ANY"));
        });
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
