using Microsoft.AspNetCore.Mvc;

namespace FWO.Middleware.Server.Requests;

internal static class VisibleInRequestFilterValidator
{
    public static bool TryValidate(IVisibleInRequestFilterRequest request, string endpointName, out ActionResult? errorResult)
    {
        if (request.AdditionalData is { Count: > 0 })
        {
            errorResult = BuildError(endpointName);
            return false;
        }

        if (request.Filter is null)
        {
            errorResult = null;
            return true;
        }

        if (request.Filter.AdditionalData is { Count: > 0 })
        {
            errorResult = BuildError(endpointName);
            return false;
        }

        errorResult = null;
        return true;
    }

    private static BadRequestObjectResult BuildError(string endpointName)
    {
        return new BadRequestObjectResult(
            $"{endpointName} only accepts {{}} or {{ \"filter\": {{}} }} or {{ \"filter\": {{ \"visibleInRequest\": true }} }} or {{ \"filter\": {{ \"visibleInRequest\": false }} }}. " +
            "Valid filter key: visibleInRequest, which controls whether request-visible objects are included.");
    }
}
