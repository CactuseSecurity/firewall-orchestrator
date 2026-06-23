using FWO.Api.Client;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test;

[TestFixture]
internal class FlowControllerValidationTest
{
    [TestCaseSource(nameof(RequestCases))]
    public void FlowControllerValidation_AllowsEmptyRootObject(RequestCase requestCase)
    {
        object request = requestCase.Deserialize("{}");

        Assert.Multiple(() =>
        {
            Assert.That(RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)request, requestCase.RootSchema, out var rootError), Is.True);
            Assert.That(rootError, Is.Null);
            Assert.That(VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)request, requestCase.FilterSchema, out var filterError), Is.True);
            Assert.That(filterError, Is.Null);
        });
    }

    [TestCaseSource(nameof(RequestCases))]
    public void FlowControllerValidation_AllowsEmptyFilterObject(RequestCase requestCase)
    {
        object request = requestCase.Deserialize("""{"filter":{}}""");

        Assert.Multiple(() =>
        {
            Assert.That(RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)request, requestCase.RootSchema, out var rootError), Is.True);
            Assert.That(rootError, Is.Null);
            Assert.That(VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)request, requestCase.FilterSchema, out var filterError), Is.True);
            Assert.That(filterError, Is.Null);
        });
    }

    [TestCaseSource(nameof(RequestCases))]
    public void FlowControllerValidation_AllowsVisibleInRequestTrueAndFalse(RequestCase requestCase)
    {
        object trueRequest = requestCase.Deserialize("""{"filter":{"visibleInRequest":true}}""");
        object falseRequest = requestCase.Deserialize("""{"filter":{"visibleInRequest":false}}""");

        Assert.Multiple(() =>
        {
            Assert.That(RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)trueRequest, requestCase.RootSchema, out var trueRootError), Is.True);
            Assert.That(trueRootError, Is.Null);
            Assert.That(VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)trueRequest, requestCase.FilterSchema, out var trueFilterError), Is.True);
            Assert.That(trueFilterError, Is.Null);

            Assert.That(RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)falseRequest, requestCase.RootSchema, out var falseRootError), Is.True);
            Assert.That(falseRootError, Is.Null);
            Assert.That(VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)falseRequest, requestCase.FilterSchema, out var falseFilterError), Is.True);
            Assert.That(falseFilterError, Is.Null);
        });
    }

    [TestCaseSource(nameof(RequestCases))]
    public void FlowControllerValidation_RejectsUnknownRootKeys(RequestCase requestCase)
    {
        object request = requestCase.Deserialize("""{"filter":{"visibleInRequest":true},"typo":1}""");

        bool valid = RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)request, requestCase.RootSchema, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain(requestCase.EndpointName));
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain("'filter'"));
        });
    }

    [TestCaseSource(nameof(RequestCases))]
    public void FlowControllerValidation_RejectsUnknownFilterKeys(RequestCase requestCase)
    {
        object request = requestCase.Deserialize("""{"filter":{"visibleInRequest":true,"typo":1}}""");

        bool valid = VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)request, requestCase.FilterSchema, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain(requestCase.EndpointName));
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain("'visibleInRequest'"));
        });
    }

    [TestCaseSource(nameof(LookupRequestCases))]
    public void FlowControllerValidation_AllowsLookupRequestShapes(LookupRequestCase requestCase)
    {
        object request = requestCase.Deserialize(requestCase.ValidJson);

        Assert.Multiple(() =>
        {
            Assert.That(RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)request, requestCase.RootSchema, out var rootError), Is.True);
            Assert.That(rootError, Is.Null);
            Assert.That(VisibleInRequestFilterValidator.TryValidate((IVisibleInRequestFilterRequest)request, requestCase.FilterSchema, out var filterError), Is.True);
            Assert.That(filterError, Is.Null);
        });
    }

    [TestCaseSource(nameof(LookupRequestCases))]
    public void FlowControllerValidation_RejectsUnknownLookupRootKeys(LookupRequestCase requestCase)
    {
        object request = requestCase.Deserialize(requestCase.InvalidRootJson);

        bool valid = RequestRootValidator.TryValidate((IRequestWithRootAdditionalData)request, requestCase.RootSchema, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(valid, Is.False);
            Assert.That(error, Is.TypeOf<BadRequestObjectResult>());
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain(requestCase.EndpointName));
            Assert.That(((BadRequestObjectResult)error!).Value?.ToString(), Does.Contain(requestCase.ExpectedRootKey));
        });
    }

    [Test]
    public async Task FlowControllerValidation_GetServiceObjectId_RejectsMissingProtocol()
    {
        FlowCatalogController controller = new(new FlowCatalogService(new ValidationApiConnection()));

        ActionResult<ServiceObjectIdResponse> result = await controller.GetServiceObjectId(new GetServiceObjectIdRequest
        {
            PortStart = 443,
            PortEnd = 443,
            Protocol = string.Empty
        });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("'protocol'"));
    }

    [Test]
    public async Task FlowControllerValidation_GetServiceObjectId_RejectsInvalidPortRange()
    {
        FlowCatalogController controller = new(new FlowCatalogService(new ValidationApiConnection()));

        ActionResult<ServiceObjectIdResponse> result = await controller.GetServiceObjectId(new GetServiceObjectIdRequest
        {
            PortStart = 1024,
            PortEnd = 443,
            Protocol = "tcp"
        });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("'portStart' <= 'portEnd'"));
    }

    [Test]
    public async Task FlowControllerValidation_GetAddressObjectId_RejectsMissingIpBounds()
    {
        FlowCatalogController controller = new(new FlowCatalogService(new ValidationApiConnection()));

        ActionResult<AddressObjectIdResponse> result = await controller.GetAddressObjectId(new GetAddressObjectIdRequest
        {
            IpStart = string.Empty,
            IpEnd = "10.0.0.2"
        });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("'ipStart'"));
    }

    [Test]
    public async Task FlowControllerValidation_GetAddressObjectId_RejectsInvalidIpRange()
    {
        FlowCatalogController controller = new(new FlowCatalogService(new ValidationApiConnection()));

        ActionResult<AddressObjectIdResponse> result = await controller.GetAddressObjectId(new GetAddressObjectIdRequest
        {
            IpStart = "banana",
            IpEnd = "10.0.0.2"
        });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("invalid 'ipStart'"));
    }

    [Test]
    public async Task FlowControllerValidation_GetTimeObjectId_RejectsInvalidTimeRange()
    {
        FlowCatalogController controller = new(new FlowCatalogService(new ValidationApiConnection()));

        ActionResult<TimeObjectIdResponse> result = await controller.GetTimeObjectId(new GetTimeObjectIdRequest
        {
            StartTime = new DateTimeOffset(2026, 6, 1, 17, 30, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero)
        });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(((BadRequestObjectResult)result.Result!).Value?.ToString(), Does.Contain("'startTime' must be <= 'endTime'"));
    }

    private static IEnumerable<TestCaseData> RequestCases()
    {
        yield return new TestCaseData(new RequestCase(
            "GetAddressObjects",
            json => JsonSerializer.Deserialize<GetAddressObjectsRequest>(json)!,
            RequestRootValidationSchema.ForVisibleInRequest("GetAddressObjects"),
            RequestFilterValidationSchema.ForVisibleInRequest("GetAddressObjects")));

        yield return new TestCaseData(new RequestCase(
            "GetAddressGroups",
            json => JsonSerializer.Deserialize<GetAddressGroupsRequest>(json)!,
            RequestRootValidationSchema.ForVisibleInRequest("GetAddressGroups"),
            RequestFilterValidationSchema.ForVisibleInRequest("GetAddressGroups")));

        yield return new TestCaseData(new RequestCase(
            "GetServiceObjects",
            json => JsonSerializer.Deserialize<GetServiceObjectsRequest>(json)!,
            RequestRootValidationSchema.ForVisibleInRequest("GetServiceObjects"),
            RequestFilterValidationSchema.ForVisibleInRequest("GetServiceObjects")));

        yield return new TestCaseData(new RequestCase(
            "GetServiceGroups",
            json => JsonSerializer.Deserialize<GetServiceGroupsRequest>(json)!,
            RequestRootValidationSchema.ForVisibleInRequest("GetServiceGroups"),
            RequestFilterValidationSchema.ForVisibleInRequest("GetServiceGroups")));

        yield return new TestCaseData(new RequestCase(
            "GetTimeObjects",
            json => JsonSerializer.Deserialize<GetTimeObjectsRequest>(json)!,
            RequestRootValidationSchema.ForVisibleInRequest("GetTimeObjects"),
            RequestFilterValidationSchema.ForVisibleInRequest("GetTimeObjects")));
    }

    private static IEnumerable<TestCaseData> LookupRequestCases()
    {
        yield return new TestCaseData(new LookupRequestCase(
            "GetServiceObjectId",
            json => JsonSerializer.Deserialize<GetServiceObjectIdRequest>(json)!,
            new RequestRootValidationSchema(
                "GetServiceObjectId",
                [
                    new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
                    new RequestKeyDefinition("portStart", "Start port for the service object lookup."),
                    new RequestKeyDefinition("portEnd", "End port for the service object lookup."),
                    new RequestKeyDefinition("protocol", "Protocol name or protocol id for the service object lookup.")
                ]),
            RequestFilterValidationSchema.ForVisibleInRequest("GetServiceObjectId"),
            """{"filter":{"visibleInRequest":true},"portStart":443,"portEnd":443,"protocol":"TCP"}""",
            """{"filter":{"visibleInRequest":true},"portStart":443,"portEnd":443,"protocol":"TCP","typo":1}""",
            "portStart"));

        yield return new TestCaseData(new LookupRequestCase(
            "GetAddressObjectId",
            json => JsonSerializer.Deserialize<GetAddressObjectIdRequest>(json)!,
            new RequestRootValidationSchema(
                "GetAddressObjectId",
                [
                    new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
                    new RequestKeyDefinition("ipStart", "Start IP address for the address object lookup."),
                    new RequestKeyDefinition("ipEnd", "End IP address for the address object lookup.")
                ]),
            RequestFilterValidationSchema.ForVisibleInRequest("GetAddressObjectId"),
            """{"filter":{"visibleInRequest":false},"ipStart":"10.0.0.1","ipEnd":"10.0.0.2"}""",
            """{"filter":{"visibleInRequest":false},"ipStart":"10.0.0.1","ipEnd":"10.0.0.2","typo":1}""",
            "ipStart"));

        yield return new TestCaseData(new LookupRequestCase(
            "GetTimeObjectId",
            json => JsonSerializer.Deserialize<GetTimeObjectIdRequest>(json)!,
            new RequestRootValidationSchema(
                "GetTimeObjectId",
                [
                    new RequestKeyDefinition("filter", "Optional filter container for request-visible settings."),
                    new RequestKeyDefinition("startTime", "Start time for the time object lookup."),
                    new RequestKeyDefinition("endTime", "End time for the time object lookup.")
                ]),
            RequestFilterValidationSchema.ForVisibleInRequest("GetTimeObjectId"),
            """{"filter":{"visibleInRequest":true},"startTime":"2026-06-01T08:00:00Z","endTime":"2026-06-01T17:30:00Z"}""",
            """{"filter":{"visibleInRequest":true},"startTime":"2026-06-01T08:00:00Z","endTime":"2026-06-01T17:30:00Z","typo":1}""",
            "startTime"));
    }

    internal sealed record RequestCase(
        string EndpointName,
        Func<string, object> Deserialize,
        RequestRootValidationSchema RootSchema,
        RequestFilterValidationSchema FilterSchema);

    internal sealed record LookupRequestCase(
        string EndpointName,
        Func<string, object> Deserialize,
        RequestRootValidationSchema RootSchema,
        RequestFilterValidationSchema FilterSchema,
        string ValidJson,
        string InvalidRootJson,
        string ExpectedRootKey);

    private sealed class ValidationApiConnection : SimulatedApiConnection
    {
        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            throw new InvalidOperationException("Validation should return before the API is queried.");
        }

        public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null)
        {
            throw new NotImplementedException();
        }

        public override void SetAuthHeader(string jwt)
        {
        }

        public override void SetRole(string role)
        {
        }

        public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
        {
        }

        public override void SwitchBack()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override void DisposeSubscriptions<T>()
        {
        }

        public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
