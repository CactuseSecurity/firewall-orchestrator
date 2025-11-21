using GraphQL;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services;
using AngleSharp.Common;

namespace FWO.Test
{
    internal class ExtTicketHandlerTestApiConn : SimulatedApiConnection
    {
        public string? CloseHistoryMessage { get; set; }
        public List<string> History = [];
        public string? AddHistoryMessage { get; set; }
        public string? AddExtRequestVars { get; set; }
        readonly string masterStateMatrix = "{\"config_value\":{\"request\":{\"matrix\":{\"0\":[0,49,620],\"49\":[49,620],\"620\":[620],\"1\":[1,2,3,4,630,631],\"2\":[1,2,3,4,630,631],\"3\":[3,4,630,631],\"4\":[4,630,631],\"630\":[630],\"631\":[631]},\"derived_states\":{\"0\":0,\"49\":49,\"620\":620,\"1\":1,\"2\":2,\"3\":3,\"4\":4,\"630\":630,\"631\":631},\"lowest_input_state\":0,\"lowest_start_state\":1,\"lowest_end_state\":49,\"active\":true},\"approval\":{\"matrix\":{\"49\":[60],\"60\":[60,99,610],\"99\":[99],\"610\":[610]},\"derived_states\":{\"49\":49,\"60\":60,\"99\":99,\"610\":610},\"lowest_input_state\":49,\"lowest_start_state\":60,\"lowest_end_state\":99,\"active\":false},\"planning\":{\"matrix\":{\"99\":[110],\"110\":[110,120,130,149],\"120\":[120,110,130,149],\"130\":[130,110,120,149,610],\"149\":[149],\"610\":[610]},\"derived_states\":{\"99\":99,\"110\":110,\"120\":110,\"130\":110,\"149\":149,\"610\":610},\"lowest_input_state\":99,\"lowest_start_state\":110,\"lowest_end_state\":149,\"active\":false},\"verification\":{\"matrix\":{\"149\":[160],\"160\":[160,199,610],\"199\":[199],\"610\":[610]},\"derived_states\":{\"149\":149,\"160\":160,\"199\":199,\"610\":610},\"lowest_input_state\":149,\"lowest_start_state\":160,\"lowest_end_state\":199,\"active\":false},\"implementation\":{\"matrix\":{\"99\":[210],\"210\":[210,220,249],\"220\":[220,210,249,610],\"249\":[249],\"610\":[610],\"49\":[49,600,610]},\"derived_states\":{\"99\":99,\"210\":210,\"220\":210,\"249\":249,\"610\":610,\"49\":49},\"lowest_input_state\":49,\"lowest_start_state\":210,\"lowest_end_state\":249,\"active\":true},\"review\":{\"matrix\":{\"249\":[260],\"260\":[260,270,299],\"270\":[210,270,260,299,610],\"299\":[299],\"610\":[610]},\"derived_states\":{\"249\":249,\"260\":260,\"270\":260,\"299\":299,\"610\":610},\"lowest_input_state\":249,\"lowest_start_state\":260,\"lowest_end_state\":299,\"active\":false},\"recertification\":{\"matrix\":{\"299\":[310],\"310\":[310,349,400],\"349\":[349],\"400\":[400]},\"derived_states\":{\"299\":299,\"310\":310,\"349\":349,\"400\":400},\"lowest_input_state\":299,\"lowest_start_state\":310,\"lowest_end_state\":349,\"active\":false}}}";
        readonly static WfReqElement srcARElem = new()
        {
            Id = 1,
            RequestAction = RequestAction.create.ToString(),
            GroupName = "ARxx12345-100",
            Field = ElemFieldType.source.ToString()
        };
        readonly static WfReqElement dstARElem = new()
        {
            Id = 2,
            RequestAction = RequestAction.create.ToString(),
            GroupName = "ARxx12345-101",
            Field = ElemFieldType.destination.ToString()
        };
        readonly static WfReqElement svcElem = new()
        {
            Id = 3,
            RequestAction = RequestAction.create.ToString(),
            Name = "Svc1",
            Port = 1000,
            ProtoId = 6,
            Field = ElemFieldType.service.ToString()
        };
        readonly static WfReqElement ruleElem = new()
        {
            Id = 3,
            RequestAction = RequestAction.create.ToString(),
            Name = "Rule1",
            RuleUid = "1234567",
            Field = ElemFieldType.rule.ToString()
        };


        readonly static WfReqTask reqTask1 = new()
        {
            Id = 1,
            Title = "Task1",
            TicketId = 123,
            TaskNumber = 1,
            TaskType = WfTaskType.group_create.ToString(),
            ManagementId = 1,
            Elements = [srcARElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}"
        };
        readonly static WfReqTask reqTask2 = new()
        {
            Id = 2,
            Title = "Task2",
            TicketId = 123,
            TaskNumber = 2,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}"
        };
        readonly static WfReqTask reqTask3 = new()
        {
            Id = 3,
            Title = "Task3",
            TicketId = 123,
            TaskNumber = 3,
            TaskType = WfTaskType.access.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}"
        };
        readonly static WfReqTask reqTask4 = new()
        {
            Id = 4,
            Title = "Task4",
            TicketId = 123,
            TaskNumber = 4,
            TaskType = WfTaskType.rule_modify.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem, ruleElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}",
            SelectedDevices = "[1]"
        };
        readonly static WfReqTask reqTask5 = new()
        {
            Id = 5,
            Title = "Task5",
            TicketId = 123,
            TaskNumber = 5,
            TaskType = WfTaskType.rule_modify.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem, ruleElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}",
            SelectedDevices = "[2]"
        };
           readonly static WfReqTask reqTask6 = new()
        {
            Id = 6,
            Title = "Task6",
            TicketId = 123,
            TaskNumber = 6,
            TaskType = WfTaskType.rule_delete.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem, ruleElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}",
            SelectedDevices = "[1]"
        };
        readonly static WfReqTask reqTask7 = new()
        {
            Id = 7,
            Title = "Task7",
            TicketId = 123,
            TaskNumber = 7,
            TaskType = WfTaskType.rule_delete.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem, ruleElem],
            AdditionalInfo = "{\"ConnId\":\"1\"}",
            SelectedDevices = "[2]"
        };
        readonly static WfReqTask reqTask8 = new()
        {
            Id = 8,
            Title = "Task8",
            TicketId = 123,
            TaskNumber = 8,
            TaskType = WfTaskType.rule_delete.ToString(),
            ManagementId = 1,
            Elements = [srcARElem, dstARElem, svcElem, ruleElem],
            AdditionalInfo = "{\"ConnId\":\"2\"}",
            SelectedDevices = "[1,2]"
        };

        readonly static WfTicket ticket123 = new(){ Id = 123, Title = "Ticket1", Tasks = [reqTask1, reqTask2, reqTask3, reqTask4, reqTask5, reqTask6, reqTask7, reqTask8] };


        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if(responseType == typeof(List<WfExtState>))
            {
                List<WfExtState>? extStates = 
                [
                    new(){ Id = 1, Name = "ExtReqInitialized", StateId = 1 },
                    new(){ Id = 2, Name = "ExtReqRequested", StateId = 3 },
                    new(){ Id = 3, Name = "ExtReqDone", StateId = 631 }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = extStates };
                return response.Data;
            }
            else if(responseType == typeof(List<IpProtocol>))
            {
                List<IpProtocol>? ipProtocols = 
                [
                    new(){ Id = 6, Name = "TCP"},
                    new(){ Id = 17, Name = "UDP" }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = ipProtocols };
                return response.Data;
            }
            else if(responseType == typeof(List<Device>))
            {
                List<Device>? devices = 
                [
                    new(){ Id = 1, Name = "TestGw1" },
                    new(){ Id = 2, Name = "TestGw2" }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = devices };
                return response.Data;
            }
            else if(responseType == typeof(List<FwoOwner>))
            {
                List<FwoOwner>? owners = 
                [
                    new(){ Id = 1, Name = "Owner1" }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = owners };
                return response.Data;
            }
            else if(responseType == typeof(List<WfState>))
            {
                List<WfState>? states = 
                [
                    new(){ Id = 0, Name = "Draft" },
                    new(){ Id = 1, Name = "ExtInit" },
                    new(){ Id = 3, Name = "ExtRequested" },
                    new(){ Id = 631, Name = "ExtDone" }
                ];
                GraphQLResponse<dynamic> response = new(){ Data = states };
                return response.Data;
            }
            else if(responseType == typeof(List<GlobalStateMatrixHelper>))
            {
                List<GlobalStateMatrixHelper> globalStateMatrixHelpers = [ new(){ ConfData = masterStateMatrix } ];
                GraphQLResponse<dynamic> response = new(){ Data = globalStateMatrixHelpers };
                return response.Data;
            }
            else if(responseType == typeof(List<WfTicket>))
            {
                List<WfTicket>? tickets = [ ticket123 ];
                GraphQLResponse<dynamic> response = new(){ Data = tickets };
                return response.Data;
            }
            else if(responseType == typeof(WfTicket))
            {
                GraphQLResponse<dynamic> response = new(){ Data = ticket123 };
                return response.Data;
            }
            else if(responseType == typeof(ReturnId))
            {
                if(query == RequestQueries.updateRequestTaskAdditionalInfo && variables != null)
                {
                    string? Vars = variables.ToString();
                    if(Vars != null)
                    {
                        if(Vars.Contains("id = 1"))
                        {
                            reqTask1.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4711\"}";
                        }
                        else if(Vars.Contains("id = 2"))
                        {
                            reqTask2.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4712\"}";
                        }
                        else if(Vars.Contains("id = 3"))
                        {
                            reqTask3.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4712\"}";
                        }
                        else if(Vars.Contains("id = 4"))
                        {
                            reqTask4.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4713\"}";
                        }
                        else if(Vars.Contains("id = 5"))
                        {
                            reqTask5.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4713\"}";
                        }
                        else if(Vars.Contains("id = 6"))
                        {
                            reqTask6.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4714\"}";
                        }
                        else if(Vars.Contains("id = 7"))
                        {
                            reqTask7.AdditionalInfo = "{\"ConnId\":\"1\",\"ExtIcketId\":\"4714\"}";
                        }
                        else if(Vars.Contains("id = 8"))
                        {
                            reqTask8.AdditionalInfo = "{\"ConnId\":\"2\",\"ExtIcketId\":\"4714\"}";
                        }
                    }
                }
                ReturnId returnId = new(){ UpdatedIdLong = 1 };
                GraphQLResponse<dynamic> response = new(){ Data = returnId };
                return response.Data;
            }
            else if(responseType == typeof(ReturnIdWrapper))
            {
                if(query == ModellingQueries.addHistoryEntry && variables != null)
                {
                    string? hist = variables.ToString();
                    if(hist != null)
                    {
                        History.Add(hist);
                    }
                }
                else if(query == ExtRequestQueries.addExtRequest && variables != null)
                {
                    AddExtRequestVars = variables.ToString();
                }
                ReturnIdWrapper ReturnIdWrap = new(){ ReturnIds = [ new() ] };
                GraphQLResponse<dynamic> response = new(){ Data = ReturnIdWrap };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
