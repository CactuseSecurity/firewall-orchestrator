using System.Net;
using System.Net.Sockets;
using System.Numerics;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Logging;
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
                string requestId = HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                                   ?? Guid.NewGuid().ToString();

                //LogSiemEntry(request, requestId);

                List<RuleDetail> rules = await FilterRules(request.Query.IpAddress, request.Query.Filter.Action, request.Query.Filter.MaxPrefixLength,request.Query.Filter.InField, apiConnection);

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

            var result = await apiConnection.SendQueryAsync<List<RuleItem>>(query, variables);

            return result
                .Select(r => new RuleDetail
                {
                    Uid = r.RuleUid,
                    Manager = r.MgmId.ToString(),
                    Source = r.RuleFroms
                        .Select(f => new NetworkObjectCopy
                        {
                            Name = f.Object.ObjName,
                            Ip = f.Object.ObjIpStart ?? ""
                        })
                        .ToList(),
                    SourceShort = r.RuleFroms.FirstOrDefault()?.Object.ObjName ?? "",
                    Destination = r.RuleTos
                        .Select(t => new NetworkObjectCopy
                        {
                            Name = t.Object.ObjName,
                            Ip = t.Object.ObjIpStart ?? ""
                        })
                        .ToList(),
                    DestinationShort = r.RuleTos.FirstOrDefault()?.Object.ObjName ?? "",
                    Service = r.RuleServices
                        .Select(s => new ServiceObject
                        {
                            Name = s.Service.SvcName,
                            Protocol = s.Service.ProtocolName?.Name ?? "Protocol name unavailable",
                            Port = s.Service.SvcPort ?? 0
                        })
                        .ToList(),
                    ServiceShort = r.RuleServices.FirstOrDefault()?.Service.SvcName ?? "",
                    ChangeID = r.RuleCustomFields ?? "",
                    AdoIT = adoIT.ToString(),
                    Name = r.RuleName,
                    CreationDate = r.RuleCreateRaw.ToString(),
                    LastHitDate = r.RuleLastSeenRaw?.ToString() ?? "",
                    Action = r.RuleAction
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
            };;

            var result = await apiConnection.SendQueryAsync<List<RuleOwnerItem>>(query, variables);

            return result
                .Select(r => r.RuleId)
                .ToList();
        }

        private async Task<List<RuleDetail>> FilterRules(string ipAddress, string action, int maxPrefix, string inField,ApiConnection apiConnection)
        {
            var query = RuleQueries.getRuleDetailsById;

            var variables = new
            {
                rule_action= action
            };

            var result = await apiConnection.SendQueryAsync<List<RuleItem>>(query, variables);
            List<RuleItem> ruleItems = [];
            foreach (var rule in result)
            {
                if (rule.RuleId == 2)
                {
                    Log.WriteError("uwu");
                }
                bool isInRange = false;
                switch (inField)
                {
                    case "source":
                        isInRange = IsInRange(ipAddress, maxPrefix, rule.RuleFroms.Select(source => source.Object).ToList());
                        break;
                    case "destination":
                        isInRange = IsInRange(ipAddress, maxPrefix, rule.RuleTos.Select(dest => dest.Object).ToList());
                        break;
                    case "both": 
                        bool sourceRange = IsInRange(ipAddress, maxPrefix, rule.RuleFroms.Select(source => source.Object).ToList());
                        bool destRange = IsInRange(ipAddress, maxPrefix, rule.RuleTos.Select(dest => dest.Object).ToList());
                        isInRange = sourceRange || destRange;
                        break;
                    default: throw new NotImplementedException();
                }

                if (isInRange)
                {
                    ruleItems.Add(rule);
                }
            }

            return ConvertRuleList(ruleItems);
        }

        private static List<RuleDetail> ConvertRuleList(List<RuleItem> inputList)
        {
            List<RuleDetail> output = new();
            foreach (var item in inputList)
            {
                RuleDetail rule = new();
                rule.Uid = item.RuleUid;
                rule.Manager = item.MgmId.ToString();
                rule.Source = item.RuleFroms
                    .Select(f => new NetworkObjectCopy
                    {
                        Name = f.Object.ObjName,
                        Ip = f.Object.ObjIpStart ?? ""
                    })
                    .ToList();
                rule.SourceShort = item.RuleFroms.FirstOrDefault()?.Object.ObjName ?? "";
                rule.Destination = item.RuleTos
                    .Select(t => new NetworkObjectCopy
                    {
                        Name = t.Object.ObjName,
                        Ip = t.Object.ObjIpStart ?? ""
                    })
                    .ToList();
                rule.DestinationShort = item.RuleTos.FirstOrDefault()?.Object.ObjName ?? "";
                rule.Service = item.RuleServices
                    .Select(s => new ServiceObject
                    {
                        Name = s.Service.SvcName,
                        Protocol = s.Service.ProtocolName?.Name ?? "Protocol name unavailable",
                        Port = s.Service.SvcPort ?? -1
                    })
                    .ToList();
                rule.ServiceShort = item.RuleServices.FirstOrDefault()?.Service.SvcName ?? "";
                rule.ChangeID = item.RuleCustomFields ?? "";
                rule.Name = item.RuleName;
                rule.CreationDate = item.RuleCreateRaw.ToString();
                rule.LastHitDate = item.RuleLastSeenRaw?.ToString() ?? "";
                rule.Action = item.RuleAction;
                output.Add(rule);
            }

            return output;
        }
        
        private static bool IsInRange(string ipAddress, int maxPrefix, List<NetworkObjectRaw> objects)
        {
            foreach (var ipObject in objects)
            {
                bool ipInRange = IsInRange(ipAddress, ipObject.ObjIpStart, ipObject.ObjIpEnd);
                int rangePrefix = CommonPrefixLength(ipObject.ObjIpStart, ipObject.ObjIpEnd);
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

public class RuleResultRoot
{
    public RuleItem[] Rule { get; set; } = Array.Empty<RuleItem>();
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
