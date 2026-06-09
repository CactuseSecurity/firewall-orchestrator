using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FWO.Middleware.Server.Requests;
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

    internal sealed record RequestCase(
        string EndpointName,
        Func<string, object> Deserialize,
        RequestRootValidationSchema RootSchema,
        RequestFilterValidationSchema FilterSchema);
}
