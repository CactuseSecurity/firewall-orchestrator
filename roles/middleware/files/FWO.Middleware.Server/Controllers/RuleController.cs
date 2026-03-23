using System.Buffers.Binary;
using System.Net;
using System.Numerics;
using System.Text;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Basics.Comparer;
using FWO.Config.Api;
using FWO.Data;
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
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection);
            UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });

            string requestId = HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                               ?? Guid.NewGuid().ToString();

            LogSiemEntry(request, requestId);

            List<RuleDetail> rules;

            if (request.Query.OwnerId is not null)
            {
                rules = await FetchRulesByOwnerId(request.Query.OwnerId ?? -1, userConfig);
            }
            else if (!string.IsNullOrWhiteSpace(request.Query.IpAddress))
            {
                if (!IPAddress.TryParse(request.Query.IpAddress, out IPAddress? ipAddress))
                {
                    return BadRequest(
                        "The IPAddress must be a valid IPv4 address.");
                }

                RuleFilter queryFilter = request.Query.Filter ?? new RuleFilter();
                if (string.IsNullOrEmpty(queryFilter.Action))
                {
                    return BadRequest(
                        "The field Action must be filled with either \"accept\", \"deny\" or \"any\".");
                }

                if (queryFilter.MaxPrefixLength < 0 || queryFilter.MaxPrefixLength > 32)
                {
                    return BadRequest(
                        $"The value for MaxPrefixLength {queryFilter.MaxPrefixLength} must be between 0 and 32.");
                }

                if (string.IsNullOrEmpty(queryFilter.InField))
                {
                    return BadRequest(
                        $"The field InField must be filled with either \"{FilterFields.Source}\", \"{FilterFields.Destination}\" or \"{FilterFields.Both}\".");
                }

                rules = await FilterRules(
                    ipAddress,
                    queryFilter.Action,
                    queryFilter.MaxPrefixLength,
                    queryFilter.InField,
                    userConfig
                );
            }
            else
            {
                return BadRequest("Either OwnerId or IpAddress must be provided.");
            }

            var response = new RulesByFilterResponse
            {
                Request_Id = requestId,
                Result = new RuleResult
                {
                    Count = rules.Count,
                    Rules = rules
                }
            };

            return Ok(response);
        }
        catch (Exception exception)
        {
            Log.WriteError("Get Rules By Filter", "Error while fetching rules.", exception);
            return StatusCode(500, "Internal server error");
        }
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
                    + $"MaxPrefixLength: {request.Query.Filter.MaxPrefixLength}"
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

    private async Task<List<RuleDetail>> FilterRules(IPAddress ipAddress, string action, int maxPrefix,
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
                FilterFields.Source => ipHelper.IsInRange(ipAddress, maxPrefix, sourceObjects),
                FilterFields.Destination => ipHelper.IsInRange(ipAddress, maxPrefix, destObjects),
                FilterFields.Both => ipHelper.IsInRange(ipAddress, maxPrefix, sourceObjects) ||
                                     ipHelper.IsInRange(ipAddress, maxPrefix, destObjects),
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
        const string notFound = "Not Found in Database";

        return inputList.Select(item => new RuleDetail
        {
            Uid = item.Uid ?? notFound,
            Manager = item.MgmtId.ToString(),
            Source = FlattenRuleNetworkObjects(item.Froms.Select(r => r.Object).ToList())
                .Select(s => new NetworkObjectCopy
                {
                    Name = s.Name,
                    Type = s.Type.Name,
                    Ip = s.IP
                })
                .ToList(),
            SourceShort = DisplaySourceOrDestinationPlain(item, isSource: true, userConfig),
            Destination = FlattenRuleNetworkObjects(item.Tos.Select(r => r.Object).ToList())
                .Select(d => new NetworkObjectCopy
                {
                    Name = d.Name,
                    Type = d.Type.Name,
                    Ip = d.IP
                })
                .ToList(),
            DestinationShort = DisplaySourceOrDestinationPlain(item, isSource: false, userConfig),
            Service = item.Services
                .Select(s => new ServiceObject
                {
                    Name = s.Content.Name,
                    Protocol = s.Content.Protocol?.Name ?? notFound,
                    Port = s.Content.SourcePort ?? -1
                })
                .ToList(),
            ServiceShort = DisplayServicesPlain(item, userConfig),
            ChangeID = item.CustomFields,
            Name = item.Name ?? notFound,
            CreationDate = item.CreatedImport?.StartTime?.ToString() ?? notFound,
            LastHitDate = item.Metadata.LastHit?.ToString() ?? notFound,
            Action = item.Action,
            AdoIT = item.RuleOwner.FirstOrDefault()?.OwnerId.ToString() ?? notFound
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

    private static string DisplayServicesPlain(Rule rule, UserConfig userConfig)
    {
        StringBuilder result = new();
        if (rule.ServiceNegated)
        {
            result.AppendLine(userConfig.GetText("negated") + "<br>");
        }

        string joined = string.Join(Environment.NewLine,
            Array.ConvertAll(rule.Services,
                service => DisplayBase.DisplayService(service.Content, false, service.Content.Name).ToString()));
        result.Append(joined);

        return result.ToString();
    }

    private static List<NetworkObject> FlattenRuleNetworkObjects(List<NetworkObject> list)
    {
        return list
            .SelectMany(obj =>
                new[] { obj }
                    .Concat(obj.ObjectGroupFlats
                        .Select(g => g.Object)
                    )
            ).OfType<NetworkObject>().ToList();
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
    private readonly Dictionary<string, IPAddress?> _parseCache = new();

    public bool IsInRange(IPAddress ipAddress, int maxPrefix, List<NetworkObject> objects)
    {
        var cleanedObjects = objects.Where(obj => obj.Type.Name != "group");

        foreach (var ipObject in cleanedObjects)
        {
            var start = ParseAndCache(ipObject.IP);
            var end = ParseAndCache(ipObject.IpEnd);

            if (start is null)
            {
                continue;
            }

            bool ipInRange = IsInRange(ipAddress, start, end);
            int rangePrefix = CommonPrefixLength(start, end);

            if (rangePrefix >= maxPrefix && ipInRange)
            {
                return true;
            }

            if (rangePrefix < maxPrefix)
            {
                break;
            }
        }

        return false;
    }

    private IPAddress? ParseAndCache(string? ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString)) return null;

        var sanitized = SanitizeIpString(ipString);
        if (_parseCache.TryGetValue(sanitized, out var cached))
            return cached;

        IPAddress.TryParse(sanitized, out var parsed);
        _parseCache[sanitized] = parsed;

        return parsed;
    }

    private static string SanitizeIpString(string? ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString)) return string.Empty;

        var trimmed = ipString.AsSpan().Trim();
        var slashIndex = trimmed.IndexOf('/');

        return (slashIndex >= 0 ? trimmed[..slashIndex] : trimmed).Trim().ToString();
    }

    private static bool IsInRange(IPAddress ip, IPAddress? startIp, IPAddress? endIp)
    {
        if (startIp is null) return false;
        if (endIp is null) return startIp.Equals(ip);

        var comparer = new IPAdressComparer();
        if (comparer.Compare(startIp, endIp) > 0)
            (startIp, endIp) = (endIp, startIp);

        return comparer.Compare(startIp, ip) <= 0 &&
               comparer.Compare(endIp, ip) >= 0;
    }

    private static int CommonPrefixLength(IPAddress? ipA, IPAddress? ipB)
    {
        if (ipA is null) return -1;
        if (ipB is null) return 32;

        uint a = BinaryPrimitives.ReadUInt32BigEndian(ipA.GetAddressBytes());
        uint b = BinaryPrimitives.ReadUInt32BigEndian(ipB.GetAddressBytes());
        uint diff = a ^ b;

        return diff == 0 ? 32 : BitOperations.LeadingZeroCount(diff);
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
    public string Request_Id { get; set; } = "";
    public RuleResult Result { get; set; } = new();
}

public class RuleFilter
{
    public int MaxPrefixLength { get; set; }
    public string InField { get; set; } = "";
    public string Action { get; set; } = "";
}

public class RuleResult
{
    public int Count { get; set; }
    public List<RuleDetail> Rules { get; set; } = new();
}

public class RuleDetail
{
    public string Uid { get; set; } = "";
    public string Manager { get; set; } = "";
    public List<NetworkObjectCopy> Source { get; set; } = new();
    public string SourceShort { get; set; } = "";
    public List<NetworkObjectCopy> Destination { get; set; } = new();
    public string DestinationShort { get; set; } = "";
    public List<ServiceObject> Service { get; set; } = new();
    public string ServiceShort { get; set; } = "";
    public string ChangeID { get; set; } = "";
    public string AdoIT { get; set; } = "";
    public string Name { get; set; } = "";
    public string CreationDate { get; set; } = "";
    public string LastHitDate { get; set; } = "";
    public string Action { get; set; } = "";
}

public class NetworkObjectCopy
{
    public string Name { get; set; } = "";
    public string? Ip { get; set; } = "";
    public string? Type { get; set; } = "";
}

public class ServiceObject
{
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "";
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
