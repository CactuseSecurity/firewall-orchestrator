using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Middleware.Server.Responses;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides read-only application-address lookup endpoints.
/// </summary>
[Authorize]
[ApiController]
[Route("api/modelling")]
public class ApplicationAddressesController(ApiConnection apiConnection) : ControllerBase
{
    internal const int kMaxFilterTextLength = 256;
    internal const int kMaxFilterValues = 100;
    internal const int kMaxLimit = 1000;

    /// <summary>
    /// Returns every undeleted app-server address for each visible application.
    /// </summary>
    /// <remarks>
    /// Requires the <c>admin</c>, <c>auditor</c>, or <c>modeller</c> role. A caller with only the modeller role
    /// receives application addresses only for applications in the <c>x-hasura-editable-owners</c> JWT claim.
    /// The <c>options</c> root key defaults to <c>{}</c> when omitted. Every field in <c>options.filter</c> is
    /// nullable; an omitted or null field does not restrict the response. String filters are case-insensitive and
    /// accept <c>*</c> for any character sequence and <c>?</c> for a single character. Plain text without wildcards
    /// is matched as a contains search, matching the owner endpoint. Applications with an inactive lifecycle state
    /// are excluded unless <c>options.showOnlyActiveState</c> is set to <c>false</c>. Applications are ordered by
    /// name, so <c>options.limit</c> and <c>options.offset</c> page the result deterministically; without a limit
    /// every matching application is returned. Every matching application is returned even when it owns no address;
    /// its <c>addresses</c> list is then empty. Each address uses compact notation: a plain IP when start and end
    /// are equal, CIDR notation when start and end span exactly one network, and <c>ipStart-ipEnd</c> for any other
    /// range. Addresses that appear more than once for an application are returned only once.
    /// </remarks>
    [HttpPost("getIpDataForOwners")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(List<ApplicationAddressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Modeller}")]
    public async Task<ActionResult<List<ApplicationAddressResponse>>> Get(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GetApplicationAddressesRequest? request)
    {
        GetApplicationAddressesRequest effectiveRequest = request ?? new GetApplicationAddressesRequest();
        Dictionary<string, string[]> validationErrors = ValidateRequest(effectiveRequest);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors)
            {
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            GetApplicationAddressesOptions options = effectiveRequest.Options!;
            List<FwoOwner> applications = await GetApplicationsAsync(options);
            List<ModellingAppServer> appServers = await GetApplicationAddressesAsync(applications);
            return Ok(BuildResponses(applications, appServers));
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Application Addresses", "Error while fetching application addresses.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Validates the request and returns all detected field errors.
    /// </summary>
    internal static Dictionary<string, string[]> ValidateRequest(GetApplicationAddressesRequest request)
    {
        Dictionary<string, string[]> errors = [];
        if (request.Options is null)
        {
            AddError(errors, "options", "options must be an object when supplied.");
            return errors;
        }

        ValidateFilter(request.Options.Filter, errors);
        ValidateLimit(request.Options.Limit, errors);
        ValidateOffset(request.Options.Offset, errors);
        return errors;
    }

    /// <summary>
    /// Maps one application and its app-server records to the public response shape. Addresses that resolve to the
    /// same compact notation are returned only once, in the order the API delivered them.
    /// </summary>
    internal static ApplicationAddressResponse ToResponse(FwoOwner application, List<ModellingAppServer> appServers)
    {
        return new ApplicationAddressResponse
        {
            ApplicationId = application.Id,
            ApplicationName = application.Name,
            AppIdExternal = application.ExtAppId,
            Addresses = appServers
                .Select(appServer => IpOperations.ToCompactNotation(appServer.Ip, appServer.IpEnd))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static void ValidateFilter(ApplicationAddressFilter? filter, Dictionary<string, string[]> errors)
    {
        if (filter is null)
        {
            return;
        }

        ValidateFilterValueCount(filter.ApplicationId, "options.filter.applicationId", errors);
        ValidateFilterValueCount(filter.ApplicationName, "options.filter.applicationName", errors);
        ValidateFilterValueCount(filter.AppIdExternal, "options.filter.appIdExternal", errors);
        ValidatePositiveValues(filter.ApplicationId, "options.filter.applicationId", errors);
        ValidateFilterTextValues(filter.ApplicationName, "options.filter.applicationName", errors);
        ValidateFilterTextValues(filter.AppIdExternal, "options.filter.appIdExternal", errors);
    }

    private static void ValidateFilterValueCount<T>(List<T>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (values?.Count > kMaxFilterValues)
        {
            AddError(errors, fieldName, $"{fieldName} must not contain more than {kMaxFilterValues} values.");
        }
    }

    private static void ValidatePositiveValues(List<int>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (values is null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] <= 0)
            {
                AddError(errors, $"{fieldName}[{index}]", $"{fieldName}[{index}] must be a positive integer when supplied.");
            }
        }
    }

    private static void ValidateLimit(int? limit, Dictionary<string, string[]> errors)
    {
        if (limit is not null && (limit < 1 || limit > kMaxLimit))
        {
            AddError(errors, "options.limit", $"options.limit must be between 1 and {kMaxLimit} when supplied.");
        }
    }

    private static void ValidateOffset(int? offset, Dictionary<string, string[]> errors)
    {
        if (offset is < 0)
        {
            AddError(errors, "options.offset", "options.offset must not be negative when supplied.");
        }
    }

    private static void ValidateFilterTextValues(List<string>? values, string fieldName, Dictionary<string, string[]> errors)
    {
        if (values is null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            string? value = values[index];
            string indexedFieldName = $"{fieldName}[{index}]";
            if (value is null)
            {
                AddError(errors, indexedFieldName, $"{indexedFieldName} must not be null when supplied.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(errors, indexedFieldName, $"{indexedFieldName} must not be blank when supplied.");
                continue;
            }
            if (value.Length > kMaxFilterTextLength)
            {
                AddError(errors, indexedFieldName, $"{indexedFieldName} must not exceed {kMaxFilterTextLength} characters.");
            }
            if (value.Any(char.IsControl))
            {
                AddError(errors, indexedFieldName, $"{indexedFieldName} must not contain control characters.");
            }
        }
    }

    private async Task<List<FwoOwner>> GetApplicationsAsync(GetApplicationAddressesOptions options)
    {
        return await apiConnection.SendQueryAsync<List<FwoOwner>>(
            OwnerQueries.getApplicationIdentifiers,
            ApplicationAddressQueryBuilder.BuildApplicationVariables(options, User)) ?? [];
    }

    private async Task<List<ModellingAppServer>> GetApplicationAddressesAsync(List<FwoOwner> applications)
    {
        if (applications.Count == 0)
        {
            return [];
        }

        List<int> applicationIds = applications.Select(application => application.Id).ToList();
        return await apiConnection.SendQueryAsync<List<ModellingAppServer>>(
            ModellingQueries.getApplicationIpAddresses,
            ApplicationAddressQueryBuilder.BuildApplicationAddressVariables(applicationIds)) ?? [];
    }

    private static List<ApplicationAddressResponse> BuildResponses(
        List<FwoOwner> applications, List<ModellingAppServer> appServers)
    {
        Dictionary<int, List<ModellingAppServer>> appServersByApplicationId = appServers
            .Where(appServer => appServer.AppId is not null)
            .GroupBy(appServer => appServer.AppId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<ApplicationAddressResponse> responses = [];

        foreach (FwoOwner application in applications)
        {
            appServersByApplicationId.TryGetValue(application.Id, out List<ModellingAppServer>? applicationAppServers);
            responses.Add(ToResponse(application, applicationAppServers ?? []));
        }
        return responses;
    }

    private static void AddError(Dictionary<string, string[]> errors, string fieldName, string error)
    {
        string[] errorValues = new string[1];
        errorValues[0] = error;
        errors[fieldName] = errorValues;
    }
}
