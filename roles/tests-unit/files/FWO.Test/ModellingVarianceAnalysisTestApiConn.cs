using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Basics;
using FWO.Services;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Data.Workflow;

namespace FWO.Test
{
    internal class ModellingVarianceAnalysisTestApiConn : SimulatedApiConnection
    {
        static readonly NetworkObject NwObj1 = new() { Id = 10, Name = "AppServerUnchanged", IP = "1.2.3.4", Type = new() { Name = ObjectType.Host } };
        static readonly NetworkObject NwObj2 = new() { Id = 11, Name = "AppServerOld", IP = "1.0.0.0", Type = new() { Name = ObjectType.Host } };
        static readonly NetworkObject Nwgroup1 = new() { Id = 1, Name = "AR504711-001", Type = new() { Name = ObjectType.Group }, ObjectGroupFlats = [new() { Object = NwObj1 }, new() { Object = NwObj2 }] };
        static readonly NetworkObject Nwgroup3 = new() { Id = 3, Name = "AR504711-003", Type = new() { Name = ObjectType.Group }, ObjectGroupFlats = [new() { Object = NwObj1 }] };
        static readonly NetworkObject SpecObj1 = new() { Id = 21, Name = "SpecObj1", Type = new() { Name = "Something else" } };
        static readonly NetworkObject SpecObj2 = new() { Id = 21, Name = "SpecObj2", Type = new() { Name = "Something else" } };

        static readonly ModellingAppServer AppServer1 = new() { Id = 13, Name = "AppServerUnchanged", Ip = "1.2.3.4/32", IpEnd = "1.2.3.4/32" };
        static readonly ModellingAppServer AppServer2 = new() { Id = 14, Name = "AppServerNew1_32", Ip = "1.1.1.1/32", IpEnd = "1.1.1.1/32" };
        static readonly ModellingAppServer AppServer3 = new() { Id = 15, Name = "AppServerNew2", Ip = "2.2.2.2/32", IpEnd = "2.2.2.2/32" };
        static readonly NetworkObject AZProd = new() { Id = 3, Name = "AZ4711", Type = new() { Name = ObjectType.Group }, ObjectGroupFlats = [new() { Object = NwObj1 }, new() { Object = NwObj2 }] };
        static readonly ModellingAppZone AZExist = new() { Id = 3, Name = "AZ4711", IdString = "AZ4711", AppServers = [new() { Content = AppServer1 }, new() { Content = AppServer2 }] };
        static readonly NetworkService Svc1 = new() { Id = 1, DestinationPort = 1000, DestinationPortEnd = 2000, Name = "Service1", ProtoId = 6 };
        static readonly NetworkService Svc2 = new() { Id = 2, DestinationPort = 990, DestinationPortEnd = 1998, Name = "Service1", ProtoId = 6 };
        static readonly Rule Rule1 = new()
        {
            Name = "FWOC1",
            MgmtId = 1,
            Froms = [ new(new(), NwObj2) ],
            Tos = [ new(new(), Nwgroup1) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule2 = new()
        {
            Name = "xxxFWOC2yyy",
            MgmtId = 1,
            Froms = [ new(new(), NwObj1) ],
            Tos = [ new(new(), Nwgroup3) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule3 = new()
        {
            Id = 3,
            Name = "NonModelledRule",
            Comment = "XXX3",
            Froms = [ new(new(), NwObj1) ],
            RulebaseId = 3
        };
        static readonly Rule Rule4 = new()
        {
            Name = "FWOC4",
            MgmtId = 1,
            Froms = [ new(new(), SpecObj1), new(new(), NwObj1) ],
            Tos = [ new(new(), SpecObj2) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule5 = new()
        {
            Name = "FWOC1again",
            MgmtId = 1,
            Froms = [ new(new(), NwObj2) ],
            Tos = [ new(new(), Nwgroup1) ],
            Services = [ new(){ Content = Svc2 } ]
        };
        static readonly Rule Rule6 = new()
        {
            Name = "FWOC5",
            MgmtId = 1,
            Froms = [ new(new(), SpecObj1), new(new(), NwObj1) ],
            Tos = [ new(new(), SpecObj2) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule7 = new()
        {
            Name = "FWOC7_mgt1",
            MgmtId = 1,
            Froms = [ new(new(), Nwgroup1) ],
            Tos = [ new(new(), NwObj1) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule8 = new()
        {
            Name = "FWOC7_mgt2",
            MgmtId = 2,
            Froms = [ new(new(), Nwgroup1) ],
            Tos = [ new(new(), NwObj2) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule9 = new()
        {
            Name = "FWOC7_mgt3",
            MgmtId = 3,
            Froms = [ new(new(), Nwgroup1) ],
            Tos = [ new(new(), NwObj1), new(new(), NwObj2) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly DeviceReport DevRep1 = new()
        {
            Id = 1,
            RulebaseLinks = [ new() { GatewayId = 1, NextRulebaseId = 3 } ]
        };

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<Management>))
            {
                if (query == ReportQueries.getRelevantImportIdsAtTime)
                {
                    GraphQLResponse<dynamic> response = new() { Data = new List<Management>() { new() { Import = new() { ImportAggregate = new() { ImportAggregateMax = new() { RelevantImportId = 1 } } } } } };
                    return response.Data;
                }
                else
                {
                    List<Management>? managements =
                    [
                        new(){ Id = 1, Name = "Checkpoint1", ExtMgtData = "{\"id\":\"1\",\"name\":\"CheckpointExt\"}", Devices = [ new(){ Id = 1 }] }
                    ];
                    GraphQLResponse<dynamic> response = new() { Data = managements };
                    return response.Data;
                }
            }
            else if (responseType == typeof(List<NetworkObject>))
            {
                List<NetworkObject>? nwObjects = [];
                if (query == ObjectQueries.getNetworkObjectsForManagement)
                {
                    if (variables != null)
                    {
                        var objTypeIds = variables.GetType().GetProperties().First(o => o.Name == "objTypeIds").GetValue(variables, null);
                        if (objTypeIds != null && ((int[])objTypeIds)[0] == 2)
                        {
                            nwObjects =
                            [
                                Nwgroup1, AZProd
                            ];
                        }
                        else
                        {
                            nwObjects =
                            [
                                NwObj1, NwObj2
                            ];
                        }
                    }
                }
                GraphQLResponse<dynamic> response = new() { Data = nwObjects };
                return response.Data;
            }
            else if (responseType == typeof(List<ModellingAppZone>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<ModellingAppZone>() { AZExist } };
                return response.Data;
            }
            else if (responseType == typeof(List<ModellingAppServer>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<ModellingAppServer>() { AppServer1, AppServer2, AppServer3 } };
                return response.Data;
            }
            else if (responseType == typeof(List<Rule>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<Rule>() { new(Rule1), new(Rule2), new(Rule3), new(Rule4), new(Rule5), new(Rule6), new(Rule7), new(Rule8), new(Rule9) } };
                return response.Data;
            }
            else if (responseType == typeof(List<ModellingConnection>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<ModellingConnection>() { new() { Id = 2 }, new() { Id = 4 } } };
                return response.Data;
            }
            else if (responseType == typeof(ReturnId) && query == ModellingQueries.updateConnectionProperties)
            {
                if (variables != null)
                {
                    List<int> connIds = [1, 2, 3, 4, 5, 6, 7, 8, 9];
                    var connId = variables.GetType().GetProperties().First(o => o.Name == "id").GetValue(variables, null);
                    if (connId != null && connIds.Contains((int)connId))
                    {
                        GraphQLResponse<dynamic> response = new();
                        return response.Data;
                    }
                    throw new ArgumentException($"ConnId {connId} is not valid");
                }
                throw new ArgumentException($"No Variables");
            }
            else if (responseType == typeof(List<ModellingNetworkArea>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<ModellingNetworkArea>() { new() { Id = 1 }, new() { Id = 3 } } };
                return response.Data;
            }
            else if (responseType == typeof(List<TicketId>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<TicketId>() { new() { Id = 1 } } };
                return response.Data;
            }
            else if (responseType == typeof(WfTicket))
            {
                GraphQLResponse<dynamic> response = new() { Data = new WfTicket() { StateId = 631, CreationDate = new(1967,1,10,8,0,0, DateTimeKind.Utc), CompletionDate = new(2025,6,26,8,0,0, DateTimeKind.Utc), Requester = new(){Name = "Walter"}} };
                return response.Data;
            }
            else if (responseType == typeof(List<DeviceReport>))
            {
                GraphQLResponse<dynamic> response = new() { Data = new List<DeviceReport> { DevRep1 } };
                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
