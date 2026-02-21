using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Report;
using FWO.Ui.Display;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

#pragma warning disable CS1591
namespace FWO.Middleware.Server.Controllers
{
    /*[Authorize]*/
    [ApiController]
    [Route("api/[controller]")]
    public class RuleController(ApiConnection apiConnection) : ControllerBase
    {
        /// <summary>
        /// Get rules filtered by AdoIT ID
        /// </summary>
        /// <param name="request">Request containing AdoIT ID and user context</param>
        /// <returns>Filtered rules</returns>
        [HttpPost("GetByAdoIT")]
        /*[Authorize(Roles = $"{Roles.Auditor}")]*/
        public async Task<ActionResult<RulesByAdoItResponse>> GetByAdoIT(
            [FromBody] RulesByAdoItRequest request)
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
                string requestId = HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                                   ?? Guid.NewGuid().ToString();

                LogSiemEntry(request, requestId);

                List<RuleDetail> rules = await FetchRulesByAdoIT(request.Query.AdoIT, apiConnection);

                var response = new RulesByAdoItResponse
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
                Log.WriteError("Get Rules By AdoIT", "Error while fetching rules.", exception);
                return StatusCode(500, "Internal server error");
            }
        }
        
        [HttpPost("GetByIpAddress")]
        public async Task<ActionResult<RulesByAdoItResponse>> GetByIpAddress(
            [FromBody] RulesByIpAddressRequest request)
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
                
                string requestId = HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                                   ?? Guid.NewGuid().ToString();

                //LogSiemEntry(request, requestId);

                List<RuleDetail> rules = await FilterRules(request.Query.IpAddress, request.Query.Filter.Action, request.Query.Filter.MaxPrefixLength,request.Query.Filter.InField, apiConnection, userConfig);

                var response = new RulesByAdoItResponse
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
                Log.WriteError("Get Rules By AdoIT", "Error while fetching rules.", exception);
                return StatusCode(500, "Internal server error");
            }
        }

        private void LogSiemEntry(RulesByAdoItRequest request, string requestId)
        {
            // TODO: Implement SIEM logging
            Log.WriteInfo("portal-application",
                $"DateTime: {DateTime.Now}, " +
                $"RequestId: {requestId}, " +
                $"UserID: {request.RequestContext.UserID}, " +
                $"UserName: {request.RequestContext.UserName}, " +
                $"AdoIT: {request.Query.AdoIT}");
        }

        private async Task<List<RuleDetail>> FetchRulesByAdoIT(int adoItId, ApiConnection apiConnection)
        {
            var ruleIDs = await GetRuleIdsByAdoItAsync(adoItId, apiConnection);
            return await GetRulesByIdsAsync(ruleIDs, adoItId, apiConnection);
        }
        
        private async Task<List<RuleDetail>> GetRulesByIdsAsync(List<int> ruleIds,int adoIT, ApiConnection apiConnection)
        {
            if (ruleIds.Count == 0)
                return new List<RuleDetail>();

            var query = RuleQueries.getRuleDetailsById;

            var variables = new
            {
                rule_ids= ruleIds
            };

            var result = await apiConnection.SendQueryAsync<List<Rule>>(query, variables);
            string notFound = "Not Found in Database";
            return result
                .Select(r => new RuleDetail
                {
                    Uid = r.Uid ?? notFound,
                    Manager = r.MgmtId.ToString(),
                    Source = r.Froms
                        .Select(f => new NetworkObjectCopy
                        {
                            Name = f.Object.Name,
                            Ip = f.Object.IP
                        })
                        .ToList(),
                    SourceShort = r.Froms.FirstOrDefault()?.Object.Name ?? "",
                    Destination = r.Tos
                        .Select(t => new NetworkObjectCopy
                        {
                            Name = t.Object.Name,
                            Ip = t.Object.IP
                        })
                        .ToList(),
                    DestinationShort = r.Tos.FirstOrDefault()?.Object.Name ?? "",
                    Service = r.Services
                        .Select(s => new ServiceObject
                        {
                            Name = s.Content.Name,
                            Protocol = s.Content.Protocol?.Name ?? notFound,
                            Port = s.Content.SourcePort ?? -1
                        })
                        .ToList(),
                    ServiceShort = r.Services.FirstOrDefault()?.Content.Name ?? "",
                    ChangeID = r.CustomFields,
                    AdoIT = adoIT.ToString(),
                    Name = r.Name ?? notFound,
                    // CreationDate = r.RuleCreateRaw.ToString(),
                    // LastHitDate = r.RuleLastSeenRaw?.ToString() ?? "",
                    Action = r.Action
                })
                .ToList();
        }

        private async Task<List<int>> GetRuleIdsByAdoItAsync(int adoIt, ApiConnection apiConnection)
        {
            var query = @"
            query GetRuleIdsByAdoIt($adoIt: Int!) {
              rule_owner(
                where: {
                  owner_mapping_source_id: { _eq: 2 }
                  owner_id: { _eq: $adoIt }
                }
              ) {
                rule_id
              }
            }";

            var variables = new
            {
                adoIt
            };

            var result = await apiConnection.SendQueryAsync<List<RuleOwnerItem>>(query, variables);

            return result
                .Select(r => r.RuleId)
                .ToList();
        }

        private async Task<List<RuleDetail>> FilterRules(string ipAddress, string action, int maxPrefix, string inField,ApiConnection apiConnection, UserConfig userConfig)
        {
            var query = RuleQueries.getRuleDetailsById;

            var variables = new
            {
                rule_action= action
            };

            var result = await apiConnection.SendQueryAsync<List<Rule>>(query, variables);
            List<Rule> ruleItems = [];
            foreach (var rule in result)
            {
                bool isInRange;
                switch (inField)
                {
                    case "source":
                        isInRange = IsInRange(ipAddress, maxPrefix, rule.Froms.Select(source => source.Object).ToList());
                        break;
                    case "destination":
                        isInRange = IsInRange(ipAddress, maxPrefix, rule.Tos.Select(dest => dest.Object).ToList());
                        break;
                    case "both": 
                        bool sourceRange = IsInRange(ipAddress, maxPrefix, rule.Froms.Select(source => source.Object).ToList());
                        bool destRange = IsInRange(ipAddress, maxPrefix, rule.Tos.Select(dest => dest.Object).ToList());
                        isInRange = sourceRange || destRange;
                        break;
                    default: throw new NotImplementedException();
                }

                if (isInRange)
                {
                    ruleItems.Add(rule);
                }
            }

            return ConvertRuleList(ruleItems, userConfig);
        }

        private static List<RuleDetail> ConvertRuleList(List<Rule> inputList, UserConfig userConfig)
        {
            List<RuleDetail> output = new();
            string notFound = "Not Found in Database";
            foreach (var item in inputList)
            {
                RuleDetail rule = new();
                rule.Uid = item.Uid ?? notFound;
                rule.Manager = item.MgmtId.ToString();
                rule.Source = item.Froms
                    .Select(f => new NetworkObjectCopy
                    {
                        Name = f.Object.Name,
                        Ip = f.Object.IP
                    })
                    .ToList();
                rule.SourceShort = DisplaySourceOrDestinationPlain( item, true, userConfig);
                rule.Destination = item.Tos
                    .Select(t => new NetworkObjectCopy
                    {
                        Name = t.Object.Name,
                        Ip = t.Object.IP
                    })
                    .ToList();
                rule.DestinationShort = DisplaySourceOrDestinationPlain( item, false, userConfig);
                rule.Service = item.Services
                    .Select(s => new ServiceObject
                    {
                        Name = s.Content.Name,
                        Protocol = s.Content.Protocol?.Name ?? notFound,
                        Port = s.Content.SourcePort ?? -1
                    })
                    .ToList();
                rule.ServiceShort = DisplayServicesPlain(item, userConfig);
                rule.ChangeID = item.CustomFields;
                rule.Name = item.Name ?? notFound;
                rule.CreationDate = item.CreatedImport?.StartTime?.ToString() ?? notFound;
                rule.LastHitDate = item.Metadata.LastHit?.ToString() ?? notFound;
                rule.Action = item.Action;
                output.Add(rule);
            }

            return output;
        }
        
        private static bool IsInRange(string ipAddress, int maxPrefix, List<NetworkObject> objects)
        {
            foreach (var ipObject in objects)
            {
                bool ipInRange = IsInRange(ipAddress, ipObject.IP, ipObject.IpEnd);
                int rangePrefix = CommonPrefixLength(ipObject.IP, ipObject.IpEnd);
                if (rangePrefix >= maxPrefix)
                {
                    if (ipInRange)
                    {
                        return true;
                    }
                }
                break;
            }

            return false;
        }
        
        private static uint ToUInt32(string ipString)
        {
            ipString = NormalizeIpv4(ipString) ?? String.Empty;
            if (string.IsNullOrEmpty(ipString))
            {
                return 0;
            }

            var ip = IPAddress.Parse(ipString);
            var bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
                
        }
        
        private static string? NormalizeIpv4(string input)
        {
            if (string.IsNullOrEmpty(input.Trim()))
                return null;
            
            var addrPart = input.Split('/', 2)[0].Trim();

            if (!IPAddress.TryParse(addrPart, out var ip))
                return null;

            if (ip.AddressFamily != AddressFamily.InterNetwork)
                return null;
            
            return ip.ToString();
        }

        private static bool IsInRange(string ip, string? startIp, string? endIp)
        {
            if (startIp is null)
            {
                return false;
            }
            uint addr = ToUInt32(ip);
            uint start = ToUInt32(startIp);
            if (endIp is null)
            {
                return addr == start;
            }
            uint end = ToUInt32(endIp);
            if (start > end)
            {
                (start, end) = (end, start);
            }
            return addr >= start && addr <= end;
        }

        private static int CommonPrefixLength(string? ipA, string? ipB)
        {
            if (ipA is null)
            {
                return -1; //start shouldn't ever be null -> abort comparison
            }
            if (ipB is null)
            {
                return 32; // /32 is prefix of a specific IP address
            }
            uint a = ToUInt32(ipA);
            uint b = ToUInt32(ipB);
            uint diff = a ^ b;
            if (diff == 0)
                return 32;

            int leadingZeros = BitOperations.LeadingZeroCount(diff);
            return leadingZeros;
        }
        
        private static string DisplaySourceOrDestinationPlain(Rule rule, bool isSource, UserConfig userConfig)
        {
            var result = new StringBuilder();
            
            if ((isSource && rule.SourceNegated) || (!isSource && rule.DestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated"));
            }
            
            var networkLocations = isSource ? rule.Froms : rule.Tos;

            string joined = string.Join(Environment.NewLine, Array.ConvertAll(networkLocations, nwLoc => NetworkLocationToPlainText(nwLoc)));

            result.Append(joined);

            return result.ToString();
        }
    
        private static string NetworkLocationToPlainText(NetworkLocation networkLocation)
        {
            string userOutput = networkLocation.User.Name;

            string objectOutput= networkLocation.Object.Name;

        
            string nwLocation = DisplayNetworkLocationPlain(
                networkLocation,
                userOutput,
                objectOutput).ToString();
        
            return nwLocation;
        }
        
        private static StringBuilder DisplayNetworkLocationPlain(NetworkLocation userNetworkObject, string userName, string objName)
        {
            var result = new StringBuilder();
            
            if (userNetworkObject.User.Id > 0)
            {
                result.Append($"{userName}@");
            }
            
            result.Append(objName);
            
            if (userNetworkObject.Object.Type.Name != ObjectType.Group)
            {
                bool showIpinBrackets = true;

                result.Append(
                    NwObjDisplay.DisplayIp(
                        userNetworkObject.Object.IP,
                        userNetworkObject.Object.IpEnd,
                        userNetworkObject.Object.Type.Name,
                        showIpinBrackets));
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
            
            string joined = string.Join(Environment.NewLine, Array.ConvertAll(rule.Services, service => DisplayBase.DisplayService(service.Content, false, service.Content.Name).ToString()));
            result.Append(joined);

            return result.ToString();
        }
    }
}

public class RulesByAdoItRequest
{
    public RequestContext RequestContext { get; set; } = new();
    public RuleQuery Query { get; set; } = new();
}

public class RulesByAdoItResponse
{
    public string Request_Id { get; set; } = "";
    public RuleResult Result { get; set; } = new();
}

public class RulesByIpAddressRequest
{
    public RequestContext RequestContext { get; set; } = new();
    public IpQuery Query { get; set; } = new();
}

public class IpQuery
{
    public string IpAddress { get; set; } = "";
    public RuleFilter Filter { get; set; } = new();
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
    public string Ip { get; set; } = "";
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

public class RuleQuery
{
    public int AdoIT { get; set; }
}

public class RuleIdResult
{
    [JsonProperty("rule_owner")]
    public List<RuleOwnerItem> RuleOwner { get; set; } = new();
}

public class RuleOwnerItem
{
    [JsonProperty("rule_id")]
    public int RuleId { get; set; }
}

public class RuleItem
{
    [JsonProperty("rule_uid")]
    public string RuleUid { get; set; } = "";

    [JsonProperty("rule_id")]
    public long RuleId { get; set; }

    [JsonProperty("mgm_id")]
    public int MgmId { get; set; }

    [JsonProperty("rule_froms")]
    public List<RuleFrom> RuleFroms { get; set; } = new();

    [JsonProperty("rule_tos")]
    public List<RuleTo> RuleTos { get; set; } = new();

    [JsonProperty("rule_services")]
    public List<RuleServiceLink> RuleServices { get; set; } = new();

    [JsonProperty("rule_custom_fields")]
    public string? RuleCustomFields { get; set; }

    [JsonProperty("rule_name")]
    public string RuleName { get; set; } = "";

    // In your JSON these are numbers; treat as long and convert later
    [JsonProperty("rule_create")]
    public long RuleCreateRaw { get; set; }

    [JsonProperty("rule_last_seen")]
    public long? RuleLastSeenRaw { get; set; }

    [JsonProperty("rule_action")]
    public string RuleAction { get; set; } = "";
}

public class RuleDetailsResult
{
    [JsonProperty("rule")]
    public List<RuleItem> Rule { get; set; } = new();
}

public class RuleFrom
{
    [JsonProperty("object")]
    public NetworkObjectRaw Object { get; set; } = new();
}

public class RuleTo
{
    [JsonProperty("object")]
    public NetworkObjectRaw Object { get; set; } = new();
}

public class NetworkObjectRaw
{
    [JsonProperty("obj_name")]
    public string ObjName { get; set; } = "";

    [JsonProperty("obj_ip")]
    public string? ObjIpStart { get; set; }
    
    [JsonProperty("obj_ip_end")]
    public string? ObjIpEnd { get; set; }
}

public class RuleServiceLink
{
    [JsonProperty("service")]
    public ServiceRaw Service { get; set; } = new();
}

public class ServiceRaw
{
    [JsonProperty("svc_name")]
    public string SvcName { get; set; } = "";

    [JsonProperty("ip_proto_id")]
    public int? IpProtoId { get; set; }

    [JsonProperty("protocol_name")]
    public ProtocolName? ProtocolName { get; set; } = new();

    [JsonProperty("svc_port")]
    public int? SvcPort { get; set; }
}

public class ProtocolName
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";
}


#pragma warning restore CS1591
