using System.Text.Json;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowRequestContractTest
{
    private static readonly string[] kMissingProtocolError = new string[] { "Required field 'protocol' is missing." };

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
    public void GetServiceObjectIdRequest_BindsMissingPortBoundsForSchemaValidation(string json)
    {
        GetServiceObjectIdRequest request = JsonSerializer.Deserialize<GetServiceObjectIdRequest>(json)!;

        RequestValidationErrors errors = RequestValidator.Validate(request, CreateServiceObjectIdSchema());

        Assert.That(errors.ToDictionary().Keys, Does.Contain(request.PortStart is null ? "portStart" : "portEnd"));
    }

    [TestCase("""{"portStart":443,"portEnd":443}""")]
    public void GetServiceObjectIdRequest_BindsMissingProtocolForSchemaValidation(string json)
    {
        GetServiceObjectIdRequest request = JsonSerializer.Deserialize<GetServiceObjectIdRequest>(json)!;

        RequestValidationErrors errors = RequestValidator.Validate(request, CreateServiceObjectIdSchema());

        Assert.That(errors.ToDictionary()["protocol"], Is.EqualTo(kMissingProtocolError));
    }

    [TestCase("""{"ipEnd":"10.0.0.2"}""")]
    [TestCase("""{"ipStart":"10.0.0.1"}""")]
    public void GetAddressObjectIdRequest_BindsMissingIpBoundsForSchemaValidation(string json)
    {
        GetAddressObjectIdRequest request = JsonSerializer.Deserialize<GetAddressObjectIdRequest>(json)!;

        RequestValidationErrors errors = RequestValidator.Validate(request, CreateAddressObjectIdSchema());

        Assert.That(errors.ToDictionary().Keys, Does.Contain(request.IpStart is null ? "ipStart" : "ipEnd"));
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

    [Test]
    public void StandardRequestDto_DefaultsOmittedOptionsToEmptyObject()
    {
        GetAddressObjectsRequest? request = JsonSerializer.Deserialize<GetAddressObjectsRequest>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.Options, Is.Not.Null);
            Assert.That(request.Options!.Filter, Is.Null);
        });
    }

    [Test]
    public void StandardRequestDto_BindsVisibleInRequestUnderOptionsFilter()
    {
        GetAddressObjectsRequest? request = JsonSerializer.Deserialize<GetAddressObjectsRequest>(
            """{"options":{"filter":{"visibleInRequest":true}}}""");

        Assert.That(request?.Options?.Filter?.VisibleInRequest, Is.True);
    }

    private static RequestValidationSchema CreateServiceObjectIdSchema()
    {
        return CreateVisibleInRequestSchema("GetServiceObjectId")
            .RequiredInt("portStart")
            .RequiredInt("portEnd")
            .RequiredString("protocol");
    }

    private static RequestValidationSchema CreateAddressObjectIdSchema()
    {
        return CreateVisibleInRequestSchema("GetAddressObjectId")
            .RequiredString("ipStart")
            .RequiredString("ipEnd");
    }

    private static RequestValidationSchema CreateVisibleInRequestSchema(string endpointName)
    {
        return RequestValidationSchema.EndpointWithOptions(endpointName, options => options
            .OptionalObject("filter", filter => filter
                .OptionalBool("visibleInRequest")));
    }
}
