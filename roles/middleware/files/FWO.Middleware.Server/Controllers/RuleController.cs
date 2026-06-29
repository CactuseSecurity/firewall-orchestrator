using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Networking;
using FWO.Logging;
using FWO.Ui.Display;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Provides endpoints for retrieving firewall rules filtered by owner or IP-related criteria.
/// </summary>
/// <remarks>
/// This controller uses the central API connection to expose a filtered rule search meant for administrative overview of existing rules.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RuleController(ApiConnection apiConnection) : ControllerBase
{
    private const int OwnerMappingIdCustomField = 2;

    /// <summary>
    /// Returns firewall rules that match the specified filtering options.
    /// </summary>
    /// <remarks>
    /// Exactly one of <c>OwnerId</c> or <c>IpAddress</c> must be provided in the request query.
    /// When <c>OwnerId</c> is set, all rules mapped to the given owner are returned.
    /// When <c>IpAddress</c> is set, rules are filtered by IP range using the supplied <c>Filter</c> fields:
    /// </remarks>
    /// <returns>
    /// An <see cref="ActionResult{T}"/> containing a <see cref="RulesByFilterResponse"/> on success,
    /// or a suitable error result on failure.
    /// </returns>
    [HttpPost("GetRulesByFilter")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}")]
    public async Task<ActionResult<RulesByFilterResponse>> GetRulesByFilter(
        [FromBody] RulesByFilterRequest request)
    {
        try
        {
            string? filterSelectionValidationError = ValidateFilterSelection(request.Query.OwnerId, request.Query.IpAddress);
            if (filterSelectionValidationError is not null)
            {
                return BadRequest(filterSelectionValidationError);
            }

            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection);
            UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection);

            string requestId = HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                               ?? Guid.NewGuid().ToString();

            LogSiemEntry(request, requestId);

            List<RuleDetail> rules;
            if (request.Query.OwnerId is not null)
            {
                rules = await FetchRulesByOwnerId(
                    request.Query.OwnerId.Value,
                    userConfig);
            }
            else
            {
                (List<RuleDetail>? fetchedRules, string? validationError) = await FetchRulesByIpAddress(
                    request.Query.IpAddress,
                    request.Query.Filter,
                    userConfig);

                if (validationError is not null)
                {
                    return BadRequest(validationError);
                }

                rules = fetchedRules ?? [];
            }

            return Ok(CreateRulesByFilterResponse(requestId, rules));
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Rules By Filter", "Error while fetching rules.", exception);
            return StatusCode(500, "Internal server error");
        }
    }

    private static string? ValidateFilterSelection(int? ownerId, string? ipAddress)
    {
        bool hasOwnerId = ownerId is not null;
        bool hasIpAddress = !string.IsNullOrWhiteSpace(ipAddress);

        if (hasOwnerId && hasIpAddress)
        {
            return "Exactly one of OwnerId or IpAddress must be provided.";
        }

        if (!hasOwnerId && !hasIpAddress)
        {
            return "Either OwnerId or IpAddress must be provided.";
        }

        return null;
    }

    private async Task<(List<RuleDetail>? Rules, string? Error)> FetchRulesByIpAddress(
        string? ipAddress,
        RuleFilter? queryFilter,
        UserConfig userConfig)
    {
        if (!IPAddress.TryParse(ipAddress, out IPAddress? parsedIpAddress) || parsedIpAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return (null, "The IPAddress must be a valid IPv4 address.");
        }

        RuleFilter effectiveFilter = queryFilter ?? new RuleFilter();
        string? validationError = ValidateIpFilter(effectiveFilter);
        if (validationError is not null)
        {
            return (null, validationError);
        }

        return (await FilterRules(
            parsedIpAddress,
            effectiveFilter.Action,
            effectiveFilter.MinPrefixLength,
            effectiveFilter.InField,
            userConfig), null);
    }

    private static string? ValidateIpFilter(RuleFilter queryFilter)
    {
        if (string.IsNullOrWhiteSpace(queryFilter.Action) ||
            (queryFilter.Action != RuleActions.Accept &&
             queryFilter.Action != RuleActions.Deny &&
             queryFilter.Action != RuleActions.Any))
        {
            return "The field Action must be filled with either \"accept\", \"deny\" or \"any\".";
        }

        if (queryFilter.MinPrefixLength < 0 || queryFilter.MinPrefixLength > 32)
        {
            return $"The value for MinPrefixLength {queryFilter.MinPrefixLength} must be between 0 and 32.";
        }

        if (string.IsNullOrWhiteSpace(queryFilter.InField) ||
            (queryFilter.InField != FilterFields.Source &&
             queryFilter.InField != FilterFields.Destination &&
             queryFilter.InField != FilterFields.Both))
        {
            return $"The field InField must be filled with either \"{FilterFields.Source}\", \"{FilterFields.Destination}\" or \"{FilterFields.Both}\".";
        }

        return null;
    }

    private static RulesByFilterResponse CreateRulesByFilterResponse(string requestId, List<RuleDetail> rules)
    {
        return new RulesByFilterResponse
        {
            RequestId = requestId,
            Result = new RuleResult
            {
                Count = rules.Count,
                Rules = rules
            }
        };
    }

    private void LogSiemEntry(RulesByFilterRequest request, string requestId)
    {
        var info = $"DateTime: {DateTime.UtcNow:O}, " +
                   $"RequestId: {requestId}, " +
                   $"UserID: {request.RequestContext.UserID}, " +
                   $"UserName: {request.RequestContext.UserName}, ";
        if (request.Query.OwnerId is not null)
        {
            info += $"OwnerId: {request.Query.OwnerId}";
        }
        else if (request.Query.IpAddress is not null && request.Query.Filter is not null)
        {
            info += $"IpAddress: {request.Query.IpAddress}"
                    + "Filter: {"
                    + $"MinPrefixLength: {request.Query.Filter.MinPrefixLength}"
                    + $"InField: {request.Query.Filter.InField}"
                    + $"Action: {request.Query.Filter.Action}"
                    + "}";
        }

        Log.WriteInfo("Log type: portal-application", info);
    }

    private async Task<List<RuleDetail>> FetchRulesByOwnerId(int ownerId,
        UserConfig userConfig)
    {
        var ruleIDs = await GetRuleIdsByOwnerId(ownerId);
        return await GetRulesByIdsAsync(ruleIDs, userConfig);
    }

    private async Task<List<RuleDetail>> GetRulesByIdsAsync(List<int> ruleIds, UserConfig userConfig)
    {
        if (ruleIds.Count == 0)
            return new List<RuleDetail>();

        var query = RuleQueries.getRuleDetailsById;

        var variables = new
        {
            rule_ids = ruleIds
        };

        var result = await apiConnection.SendQueryAsync<List<Rule>>(query, variables);
        return ConvertRuleList(result, userConfig);
    }

    private async Task<List<int>> GetRuleIdsByOwnerId(int ownerId)
    {
        var query = RuleQueries.getRuleIdsByRuleOwner;

        var variables = new
        {
            ownerId,
            owner_mapping_source = OwnerMappingIdCustomField
        };

        var result = await apiConnection.SendQueryAsync<List<RuleOwnerItem>>(query, variables);

        return result
            .Select(r => r.RuleId)
            .ToList();
    }

    private async Task<List<RuleDetail>> FilterRules(IPAddress ipAddress, string action, int minPrefix,
        string inField, UserConfig userConfig)
    {
        IpFilterHelper ipHelper = new IpFilterHelper();

        var query = RuleQueries.getRuleDetailsById;
        string? ruleAction = SanitizeRuleAction(action);
        var variables = new { rule_action = ruleAction };

        var result = await apiConnection.SendQueryAsync<List<Rule>>(query, variables);
        List<Rule> ruleItems = [];

        foreach (var rule in result)
        {
            var sourceObjects = FlattenRuleNetworkObjects(
                rule.Froms.Select(source => source.Object).ToList());
            var destObjects = FlattenRuleNetworkObjects(
                rule.Tos.Select(dest => dest.Object).ToList());

            bool isInRange = inField switch
            {
                FilterFields.Source => ipHelper.IsInRange(ipAddress, minPrefix, sourceObjects),
                FilterFields.Destination => ipHelper.IsInRange(ipAddress, minPrefix, destObjects),
                FilterFields.Both => ipHelper.IsInRange(ipAddress, minPrefix, sourceObjects) ||
                                     ipHelper.IsInRange(ipAddress, minPrefix, destObjects),
                _ => throw new ArgumentException($"Invalid InField: {inField}")
            };

            if (isInRange)
            {
                ruleItems.Add(rule);
            }
        }

        return ConvertRuleList(ruleItems, userConfig);
    }

    private static List<RuleDetail> ConvertRuleList(List<Rule> inputList, UserConfig userConfig)
    {
        string notFound = RuleFieldSourceResolver.NotFoundValue;
        string ownerCustomFieldKey = userConfig.GlobalConfig?.CustomFieldOwnerKey ?? "";
        string changeIdCustomFieldKey = userConfig.GlobalConfig?.CustomFieldChangeIdKey ?? "";

        return inputList.Select(item =>
        {
            List<NetworkService> flattenedServices = FlattenRuleServices(item.Services.Select(s => s.Content).ToList());

            return new RuleDetail
            {
                Uid = item.Uid ?? notFound,
                Manager = item.MgmtId.ToString(),
                Source = FlattenRuleNetworkObjects(item.Froms.Select(r => r.Object).ToList())
                    .Select(s => new NetworkObjectCopy
                    {
                        Name = s.Name,
                        Type = s.Type.Name,
                        Ip = DisplayBase.DisplayIp(s.IP, s.IpEnd)
                    })
                    .ToList(),
                SourceShort = DisplaySourceOrDestinationPlain(item, isSource: true, userConfig),
                Destination = FlattenRuleNetworkObjects(item.Tos.Select(r => r.Object).ToList())
                    .Select(d => new NetworkObjectCopy
                    {
                        Name = d.Name,
                        Type = d.Type.Name,
                        Ip = DisplayBase.DisplayIp(d.IP, d.IpEnd)
                    })
                    .ToList(),
                DestinationShort = DisplaySourceOrDestinationPlain(item, isSource: false, userConfig),
                Service = flattenedServices
                    .Select(s => new ServiceObject
                    {
                        Name = s.Name,
                        Protocol = s.Protocol?.Name ?? notFound,
                        Port = s.DestinationPort ?? -1
                    })
                    .ToList(),
                ServiceShort = DisplayServicesPlain(flattenedServices, item.ServiceNegated, userConfig),
                Name = item.Name ?? notFound,
                CreationDate = item.CreatedImport?.StartTime?.ToString() ?? notFound,
                LastHitDate = item.Metadata.LastHit?.ToString() ?? notFound,
                Action = item.Action,
                OwnerInformation = RuleFieldSourceResolver.ResolveOwnerInformation(item, ownerCustomFieldKey),
                AdditionalInformation = RuleFieldSourceResolver.ResolveAdditionalInformation(item, changeIdCustomFieldKey),
                Comment = item.Comment ?? notFound,
                Time = item.RuleTimes.Where(ruleTimeObject => ruleTimeObject.TimeObj is not null).Select(ruleTimeObject => ruleTimeObject.TimeObj!.Name).ToList()
            };
        }).ToList();
    }

    private static string DisplaySourceOrDestinationPlain(Rule rule, bool isSource, UserConfig userConfig)
    {
        var result = new StringBuilder();

        if ((isSource && rule.SourceNegated) || (!isSource && rule.DestinationNegated))
        {
            result.AppendLine(userConfig.GetText("negated"));
        }

        var networkLocations = isSource ? rule.Froms : rule.Tos;

        string joined = string.Join(Environment.NewLine,
            Array.ConvertAll(networkLocations, NetworkLocationToPlainText));

        result.Append(joined);

        return result.ToString();
    }

    private static string NetworkLocationToPlainText(NetworkLocation networkLocation)
    {
        string userOutput = networkLocation.User?.Name ?? string.Empty;

        string objectOutput = networkLocation.Object?.Name ?? string.Empty;


        string nwLocation = DisplayNetworkLocationPlain(
            networkLocation,
            userOutput,
            objectOutput).ToString();

        return nwLocation;
    }

    private static StringBuilder DisplayNetworkLocationPlain(NetworkLocation userNetworkObject, string userName,
        string objName)
    {
        var result = new StringBuilder();

        if (userNetworkObject.User?.Id > 0)
        {
            result.Append($"{userName}@");
        }

        result.Append(objName);

        if (userNetworkObject.Object.Type.Name != ObjectType.Group)
        {
            bool showIpInBrackets = true;

            result.Append(
                NwObjDisplay.DisplayIp(
                    userNetworkObject.Object.IP,
                    userNetworkObject.Object.IpEnd,
                    userNetworkObject.Object.Type.Name,
                    showIpInBrackets));
        }

        return result;
    }

    private static string DisplayServicesPlain(IEnumerable<NetworkService> services, bool serviceNegated, UserConfig userConfig)
    {
        StringBuilder result = new();
        if (serviceNegated)
        {
            result.AppendLine(userConfig.GetText("negated") + "<br>");
        }

        string joined = string.Join(Environment.NewLine,
            services.Select(service => DisplayBase.DisplayService(service, false, service.Name).ToString()));
        result.Append(joined);

        return result.ToString();
    }

    private static List<NetworkObject> FlattenRuleNetworkObjects(List<NetworkObject> list)
    {
        return NetworkObject.FlattenRuleNetworkObjects(list)
            .Where(HasType)
            .Distinct()
            .ToList();
    }

    private static List<NetworkService> FlattenRuleServices(List<NetworkService> list)
    {
        return NetworkService.FlattenRuleServices(list)
            .Where(HasType)
            .Distinct()
            .ToList();
    }

    private static bool HasType(NetworkObject networkObject)
    {
        return !string.IsNullOrWhiteSpace(networkObject.Type?.Name);
    }

    private static bool HasType(NetworkService networkService)
    {
        return !string.IsNullOrWhiteSpace(networkService.Type?.Name);
    }

    private static string? SanitizeRuleAction(string action)
    {
        string? ruleAction = null;
        switch (action)
        {
            case RuleActions.Accept:
            case RuleActions.Deny:
                ruleAction = action;
                break;
            case RuleActions.Any:
            default: break;
        }

        return ruleAction;
    }

    private static class FilterFields
    {
        public const string Source = "source";
        public const string Destination = "destination";
        public const string Both = "both";
    }
}

#pragma warning disable CS1591
public sealed class IpFilterHelper
{
    private readonly NetworkObjectRangeAnalyzer _rangeAnalyzer = new();

    public bool IsInRange(IPAddress ipAddress, int minPrefix, List<NetworkObject> objects)
    {
        return _rangeAnalyzer.MatchesIpFilter(ipAddress, minPrefix, objects);
    }
}

public class RulesByFilterRequest
{
    public RequestContext RequestContext { get; set; } = new();
    public RulesByFilterQuery Query { get; set; } = new();
}

public class RulesByFilterQuery
{
    public int? OwnerId { get; set; }
    public string? IpAddress { get; set; }

    public RuleFilter? Filter { get; set; }
}

public class RulesByFilterResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = "";

    [JsonPropertyName("result")]
    public RuleResult Result { get; set; } = new();
}

public class RuleFilter
{
    public int MinPrefixLength { get; set; }
    public string InField { get; set; } = "";
    public string Action { get; set; } = "";
}

public class RuleResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("rules")]
    public List<RuleDetail> Rules { get; set; } = new();
}

public class RuleDetail
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("manager")]
    public string Manager { get; set; } = "";

    [JsonPropertyName("source")]
    public List<NetworkObjectCopy> Source { get; set; } = new();

    [JsonPropertyName("sourceShort")]
    public string SourceShort { get; set; } = "";

    [JsonPropertyName("destination")]
    public List<NetworkObjectCopy> Destination { get; set; } = new();

    [JsonPropertyName("destinationShort")]
    public string DestinationShort { get; set; } = "";

    [JsonPropertyName("service")]
    public List<ServiceObject> Service { get; set; } = new();

    [JsonPropertyName("serviceShort")]
    public string ServiceShort { get; set; } = "";

    [JsonPropertyName("ownerInformation")]
    public OwnerInformation OwnerInformation { get; set; } = new();

    [JsonPropertyName("additionalInformation")]
    public AdditionalInformation AdditionalInformation { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = [];

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("creationDate")]
    public string CreationDate { get; set; } = "";

    [JsonPropertyName("lastHitDate")]
    public string LastHitDate { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
}

public class OwnerInformation
{
    [JsonPropertyName("extAppId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExtAppId { get; set; }

    [JsonPropertyName("ownerIds")]
    public List<int> OwnerIds { get; set; } = [];
}

public class AdditionalInformation
{
    [JsonPropertyName("changeId")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChangeId { get; set; }
}

public class NetworkObjectCopy
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("ip")]
    public string? Ip { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; } = "";
}

public class ServiceObject
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; }
}

public class RequestContext
{
    public string UserName { get; set; } = "";
    public string UserID { get; set; } = "";
}

public class RuleOwnerItem
{
    [JsonProperty("rule_id")] public int RuleId { get; set; }
}
#pragma warning restore CS1591
