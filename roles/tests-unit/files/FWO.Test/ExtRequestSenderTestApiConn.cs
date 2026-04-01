using FWO.Api.Client.Queries;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Services;
using FWO.ExternalSystems.Tufin.SecureChange;
using GraphQL;
using System.Text.RegularExpressions;

namespace FWO.Test
{
    internal class ExtRequestSenderTestApiConn : SimulatedApiConnection
    {
        public List<string> UpdateExtRequestCreation = [];
        public List<string> UpdateExtRequestProcess = [];
        public List<string> LockOperations = [];
        public Dictionary<long, List<string>> TryLockStatesById = [];
        public List<string> Alerts = [];
        public List<long> AcknowledgedAlertIds = [];
        public List<string> OperationHistory = [];
        public int TriedToGetLdapsForHandleStateChange = 0;
        public HashSet<long> FailUpdateExtRequestProcessIds = [];
        public HashSet<long> InitiallyLockedRequestIds = [];
        public HashSet<long> ExpiredLockRequestIds = [];
        public HashSet<long> ClosedBeforeLockIds = [];
        public HashSet<long> UnavailableLockIds = [];
        public HashSet<long> OverdueRequestIds = [];
        public List<Alert> ExistingOpenAlerts = [];

        readonly ExternalTicketSystem ticketSystem = new()
        {
            Id = 1,
            Type = ExternalTicketSystemType.TufinSecureChange,
            Authorization = "xyz",
            Name = "Tufin",
            Url = "https://tufin-test.xxx.de/securechangeworkflow/api/securechange/",
            Templates =
            [
                new()
                {
                    TaskType = SCTaskType.NetworkObjectModify.ToString(),
                    TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Automatische Gruppenerstellung\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[@@TASKS@@]}}}}]}}}",
                    TasksTemplate = "{\"@xsi.type\": \"multi_group_change\",\"name\": \"Modify network object group\",\"group_change\": {\"name\": \"@@GROUPNAME@@\",\"management_name\": \"@@MANAGEMENT_NAME@@\",\"members\": {\"member\": @@MEMBERS@@},\"change_action\": \"CREATE\"}}",
                    ObjectTemplate = "{\"@type\": \"@@TYPE@@\", \"name\": \"@@OBJECTNAME@@\", \"object_type\": \"@@OBJECT_TYPE@@\", \"object_details\": \"@@OBJECT_DETAILS@@\", \"status\": \"@@STATUS@@\", \"comment\": \"@@COMMENT@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\", \"management_id\": @@MANAGEMENT_ID@@}",
                    ObjectTemplateShort = "{\"@type\": \"Object\", \"name\": \"@@OBJECTNAME@@\", \"status\": \"@@STATUS@@\", \"object_updated_status\": \"@@OBJUPDSTATUS@@\"}"
                },
                new()
                {
                    TaskType = SCTaskType.AccessRequest.ToString(),
                    TicketTemplate = "{\"ticket\":{\"subject\":\"@@TICKET_SUBJECT@@\",\"priority\":\"@@PRIORITY@@\",\"requester\":\"@@ONBEHALF@@\",\"domain_name\":\"\",\"workflow\":{\"name\":\"Standard Firewall Request\"},\"steps\":{\"step\":[{\"name\":\"Erfassung des Antrags\",\"tasks\":{\"task\":{\"fields\":{\"field\":[{\"@xsi.type\": \"multi_access_request\",\"name\": \"Zugang\",\"read_only\": false,\"access_request\":[@@TASKS@@]},{\"@xsi.type\": \"text_area\",\"name\": \"Grund für den Antrag\",\"read_only\": false,\"text\": \"@@REASON@@\"},{\"@xsi.type\": \"text_field\",\"name\": \"Anwendungs-ID\",\"text\": \"@@APPID@@\"},{\"@xsi.type\": \"checkbox\",\"name\": \"hinterlegt\",\"value\": true}]}}}}]}}}}",
                    TasksTemplate = "{\"order\": \"@@ORDERNAME@@\",\"verifier_result\": {\"status\": \"not run\"},\"use_topology\": true,\"targets\": {\"target\": {\"@type\": \"ANY\"}},\"action\": \"@@ACTION@@\",\"sources\":{\"source\":@@SOURCES@@},\"destinations\":{\"destination\":@@DESTINATIONS@@},\"services\":{\"service\":@@SERVICES@@},\"labels\":\"\",\"comment\": \"@@TASKCOMMENT@@\"}",
                    IpTemplate = "{\"@type\": \"IP\", \"ip_address\": \"@@IP@@\", \"netmask\": \"255.255.255.255\", \"cidr\": 32}",
                    NwObjGroupTemplate = "{\"@type\": \"Object\", \"object_name\": \"@@GROUPNAME@@\", \"management_name\": \"@@MANAGEMENT_NAME@@\"}",
                    ServiceTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"@@PROTOCOLNAME@@\", \"port\": @@PORT@@, \"name\": \"@@SERVICENAME@@\"}",
                    IcmpTemplate = "{\"@type\": \"PROTOCOL\", \"protocol\": \"ICMP\", \"type\": 8, \"name\": \"@@SERVICENAME@@\"}"
                }
            ]
        };

        private readonly string TicketContent = "{ \"ticketText\": \"\" }";
        // private readonly string TicketContent = "{\"ticketText\": \"{ \"ticket\": { \"subject\": \"tick2\"} }\" }";
        // private readonly string TicketContent = "{\"ticketText\": \"{ \u0022ticket\u0022: { \u0022subject\u0022: \u0022tick2\u0022} }\" }";
        // private readonly string TicketContent = "{\"ticketText\": {\"ticket\": { \"subject\": \"tick2\"} }, \"ticketId\": \"2\"}";
        // private readonly string TicketContent = "{\"ticketText\":\"{\u0022ticket\u0022:{\u0022subject\u0022:\u0022APP-3714: Create Group AZ03714 on checkpoint_demo(2)\u0022,\u0022priority\u0022:\u0022Low\u0022,\u0022requester\u0022:\u0022modeller\u0022,\u0022domain_name\u0022:\u0022\u0022,\u0022workflow\u0022:{\u0022name\u0022:\u0022Automatische Gruppenerstellung\u0022},\u0022steps\u0022:{\u0022step\u0022:[{\u0022name\u0022:\u0022Erfassung des Antrags\u0022,\u0022tasks\u0022:{\u0022task\u0022:{\u0022fields\u0022:{\u0022field\u0022:[{\u0022@xsi.type\u0022: \u0022multi_group_change\u0022,\u0022name\u0022: \u0022Modify network object group\u0022,\u0022group_change\u0022: {\u0022name\u0022: \u0022AZ03714\u0022,\u0022management_id\u0022: 1,\u0022management_name\u0022: \u0022ExtCP\u0022,\u0022members\u0022: {\u0022member\u0022: [{\u0022@type\u0022: \u0022host\u0022, \u0022name\u0022: \u0022d172-218-137-115.bchsia.telus.net\u0022, \u0022object_type\u0022: \u0022host\u0022, \u0022object_details\u0022: \u0022172.218.137.115/32\u0022, \u0022management_id\u0022: 1,\u0022status\u0022: \u0022ADDED\u0022, \u0022comment\u0022: \u0022\u0022, \u0022object_updated_status\u0022: \u0022NEW\u0022},{\u0022@type\u0022: \u0022host\u0022, \u0022name\u0022: \u0022d172-218-137-118.bchsia.telus.net\u0022, \u0022object_type\u0022: \u0022host\u0022, \u0022object_details\u0022: \u0022172.218.137.119/32\u0022, \u0022management_id\u0022: 1,\u0022status\u0022: \u0022ADDED\u0022, \u0022comment\u0022: \u0022\u0022, \u0022object_updated_status\u0022: \u0022NEW\u0022},{\u0022@type\u0022: \u0022host\u0022, \u0022name\u0022: \u0022tzhdfh-in-f165.1e100.net\u0022, \u0022object_type\u0022: \u0022host\u0022, \u0022object_details\u0022: \u0022172.217.67.165/32\u0022, \u0022management_id\u0022: 1,\u0022status\u0022: \u0022ADDED\u0022, \u0022comment\u0022: \u0022\u0022, \u0022object_updated_status\u0022: \u0022NEW\u0022},{\u0022@type\u0022: \u0022host\u0022, \u0022name\u0022: \u0022tzhdfh-in-f175.1e100.net\u0022, \u0022object_type\u0022: \u0022host\u0022, \u0022object_details\u0022: \u0022172.217.67.175/32\u0022, \u0022management_id\u0022: 1,\u0022status\u0022: \u0022ADDED\u0022, \u0022comment\u0022: \u0022\u0022, \u0022object_updated_status\u0022: \u0022NEW\u0022}]},\u0022change_action\u0022: \u0022CREATE\u0022}}]}}}}]}}}\",\"TicketId\":\"\"}";

        private static long ExtractId(object? variables)
        {
            string vars = variables?.ToString() ?? "";
            Match idMatch = Regex.Match(vars, @"id = (\d+)");
            return idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : 0;
        }

        private static DateTime? ExtractDateTime(object? variables, string propertyName)
        {
            object? propertyValue = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
            return propertyValue is DateTime dateTime ? dateTime : null;
        }

        private static List<string> ExtractStates(object? variables, string propertyName)
        {
            object? propertyValue = variables?.GetType().GetProperty(propertyName)?.GetValue(variables);
            return propertyValue is IEnumerable<string> states ? states.ToList() : [];
        }

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(ConfigItem[]))
            {
                ConfigItem[] configItems = SimulatedUserConfig.GetAsConfigs();
                GraphQLResponse<dynamic> response = new() { Data = configItems };
                return response.Data;
            }
            if (responseType == typeof(List<UiText>))
            {
                List<UiText> texts = [];
                GraphQLResponse<dynamic> response = new() { Data = texts };
                return response.Data;
            }
            else if (responseType == typeof(List<ExternalRequest>))
            {
                string serializedTicketSystem = System.Text.Json.JsonSerializer.Serialize(ticketSystem);
                List<ExternalRequest> openRequests =
                [
                    new(){ Id = 1, TicketId = 1, ExtTicketSystem = serializedTicketSystem, ExtRequestState = ExtStates.ExtReqInitialized.ToString(),
                        WaitCycles = 2, Locked = InitiallyLockedRequestIds.Contains(1), LockOwner = InitiallyLockedRequestIds.Contains(1) ? "other-worker" : null,
                        LockExpiresAt = ExpiredLockRequestIds.Contains(1) ? DateTime.Now.AddMinutes(-1) : DateTime.Now.AddMinutes(10),
                        CreationDate = OverdueRequestIds.Contains(1) ? new DateTime(2000, 1, 1) : DateTime.Now }, // wait
                    new(){ Id = 2, TicketId = 2, ExtTicketSystem = serializedTicketSystem, ExtRequestState = ExtStates.ExtReqInitialized.ToString(),
                        ExtRequestContent = TicketContent, Locked = InitiallyLockedRequestIds.Contains(2), LockOwner = InitiallyLockedRequestIds.Contains(2) ? "other-worker" : null,
                        LockExpiresAt = ExpiredLockRequestIds.Contains(2) ? DateTime.Now.AddMinutes(-1) : DateTime.Now.AddMinutes(10),
                        CreationDate = OverdueRequestIds.Contains(2) ? new DateTime(2000, 1, 1) : DateTime.Now }, // create
                    new(){ Id = 3, TicketId = 3, ExtTicketSystem = serializedTicketSystem, ExtRequestState = ExtStates.ExtReqFailed.ToString(),
                        ExtRequestContent = TicketContent, Locked = InitiallyLockedRequestIds.Contains(3), LockOwner = InitiallyLockedRequestIds.Contains(3) ? "other-worker" : null,
                        LockExpiresAt = ExpiredLockRequestIds.Contains(3) ? DateTime.Now.AddMinutes(-1) : DateTime.Now.AddMinutes(10),
                        CreationDate = OverdueRequestIds.Contains(3) ? new DateTime(2000, 1, 1) : DateTime.Now }, // create reject
                    new(){ Id = 4, TicketId = 4, ExtTicketSystem = serializedTicketSystem, ExtRequestState = ExtStates.ExtReqInProgress.ToString(),
                        ExtTicketId = "4711", Locked = InitiallyLockedRequestIds.Contains(4), LockOwner = InitiallyLockedRequestIds.Contains(4) ? "other-worker" : null,
                        LockExpiresAt = ExpiredLockRequestIds.Contains(4) ? DateTime.Now.AddMinutes(-1) : DateTime.Now.AddMinutes(10),
                        CreationDate = OverdueRequestIds.Contains(4) ? new DateTime(2000, 1, 1) : DateTime.Now }, // poll error
                    new(){ Id = 5, TicketId = 5, ExtTicketSystem = serializedTicketSystem, ExtRequestState = ExtStates.ExtReqInProgress.ToString(),
                        ExtTicketId = "4712", Locked = InitiallyLockedRequestIds.Contains(5), LockOwner = InitiallyLockedRequestIds.Contains(5) ? "other-worker" : null,
                        LockExpiresAt = ExpiredLockRequestIds.Contains(5) ? DateTime.Now.AddMinutes(-1) : DateTime.Now.AddMinutes(10),
                        CreationDate = OverdueRequestIds.Contains(5) ? new DateTime(2000, 1, 1) : DateTime.Now } // poll ok + new request
                ];
                DateTime currentTime = ExtractDateTime(variables, "currentTime") ?? DateTime.Now;
                openRequests = openRequests.Where(request => !request.Locked || request.LockExpiresAt < currentTime).ToList();
                GraphQLResponse<dynamic> response = new() { Data = openRequests };
                return response.Data;
            }
            else if (responseType == typeof(List<Alert>))
            {
                GraphQLResponse<dynamic> response = new() { Data = ExistingOpenAlerts };
                return response.Data;
            }
            else if (responseType == typeof(List<WfExtState>))
            {
                List<WfExtState>? extStates =
                [
                    new(){ Id = 1, Name = "ExtReqInitialized", StateId = 1 },
                    new(){ Id = 2, Name = "ExtReqRequested", StateId = 3 },
                    new(){ Id = 3, Name = "ExtReqDone", StateId = 631 }
                ];
                GraphQLResponse<dynamic> response = new() { Data = extStates };
                return response.Data;
            }
            else if (responseType == typeof(List<Ldap>))
            {
                TriedToGetLdapsForHandleStateChange++;
                List<Ldap>? ldaps =
                [
                    new(){ Id = 1, GroupSearchPath = "path", UserSearchPath = "dc=fworch,dc=internal" }
                ];
                GraphQLResponse<dynamic> response = new() { Data = ldaps };
                return response.Data;
            }
            else if (responseType == typeof(ReturnId))
            {
                if (query == ExtRequestQueries.updateExternalRequestWaitCycles)
                {
                    if (variables == null || (!variables.ToString()?.Contains("waitCycles = 1") ?? true))
                    {
                        throw new ArgumentException("wrong wait cycles");
                    }
                    long id = ExtractId(variables);
                    ReturnId returnId = new() { UpdatedIdLong = id };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.updateExtRequestCreation)
                {
                    UpdateExtRequestCreation.Add(variables?.ToString() ?? "no variables");
                    OperationHistory.Add($"creation:{variables}");
                    long id = ExtractId(variables);
                    ReturnId returnId = new() { UpdatedIdLong = id };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.updateExtRequestProcess)
                {
                    if (variables != null && FailUpdateExtRequestProcessIds.Any(id => variables.ToString()?.Contains($"id = {id}") ?? false))
                    {
                        throw new InvalidOperationException("simulated process update failure");
                    }
                    UpdateExtRequestProcess.Add(variables?.ToString() ?? "no variables");
                    OperationHistory.Add($"process:{variables}");
                    long id = ExtractId(variables);
                    ReturnId returnId = new() { UpdatedIdLong = id };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.updateExternalRequestLock)
                {
                    LockOperations.Add(variables?.ToString() ?? "no variables");
                    ReturnId returnId = new() { AffectedRows = 1 };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.renewExternalRequestLock)
                {
                    LockOperations.Add(variables?.ToString() ?? "no variables");
                    ReturnId returnId = new() { AffectedRows = 1 };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.tryLockExternalRequest)
                {
                    LockOperations.Add(variables?.ToString() ?? "no variables");
                    long id = ExtractId(variables);
                    List<string> states = ExtractStates(variables, "states");
                    TryLockStatesById[id] = states;
                    bool requestClosedBeforeLock = variables != null
                        && ClosedBeforeLockIds.Any(id => variables.ToString()?.Contains($"id = {id}") ?? false);
                    bool lockAvailable = variables != null
                        && !UnavailableLockIds.Any(id => variables.ToString()?.Contains($"id = {id}") ?? false)
                        && (!requestClosedBeforeLock || states.Contains(ExtStates.ExtReqAcknowledged.ToString()));
                    ReturnId returnId = new() { AffectedRows = lockAvailable ? 1 : 0 };
                    GraphQLResponse<dynamic> response = new() { Data = returnId };
                    return response.Data;
                }
                else if (query == ExtRequestQueries.updateExtRequestFinal)
                {
                    OperationHistory.Add($"final:{variables}");
                    long id = ExtractId(variables);
                    GraphQLResponse<dynamic> response = new() { Data = new ReturnId { UpdatedIdLong = id } };
                    return response.Data;
                }
                else if (query == MonitorQueries.acknowledgeAlert)
                {
                    long id = ExtractId(variables);
                    AcknowledgedAlertIds.Add(id);
                    GraphQLResponse<dynamic> response = new() { Data = new ReturnId { UpdatedIdLong = id } };
                    return response.Data;
                }
            }
            else if (responseType == typeof(ReturnIdWrapper))
            {
                if (query == MonitorQueries.addAlert && variables != null)
                {
                    Alerts.Add(variables.ToString() ?? "no variables");
                    GraphQLResponse<dynamic> alertResponse = new() { Data = new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 99 }] } };
                    return alertResponse.Data;
                }
                GraphQLResponse<dynamic> response = new() { Data = new ReturnIdWrapper { ReturnIds = [new ReturnId()] } };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
