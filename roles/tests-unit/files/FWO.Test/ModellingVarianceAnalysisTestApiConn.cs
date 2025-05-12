using FWO.Api.Client.Queries;
using GraphQL;
using FWO.Basics;
using FWO.Services;
using FWO.Data;
using FWO.Data.Modelling;

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
        static readonly ModellingAppZone AZExist = new() { Id = 3, Name = "AZ4711", IdString = "AZ4711", AppServers = new() { new() { Content = AppServer1 }, new() { Content = AppServer2 } } };
        static readonly NetworkService Svc1 = new() { Id = 1, DestinationPort = 1000, DestinationPortEnd = 2000, Name = "Service1", ProtoId = 6 };
        static readonly Rule Rule1 = new() 
        {
            Name = "FWOC1" ,
            Froms = [ new(new(), NwObj2) ],
            Tos = [ new(new(), Nwgroup1) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule2 = new() 
        {
            Name = "xxxFWOC2yyy",
            Froms = [ new(new(), NwObj1) ],
            Tos = [ new(new(), Nwgroup3) ],
            Services = [ new(){ Content = Svc1 } ]
        };
        static readonly Rule Rule3 = new() { Name = "NonModelledRule", Comment = "XXX3" };
        static readonly Rule Rule4 = new() 
        {
            Name = "FWOC4",
            Froms = [ new(new(), SpecObj1), new(new(), Nwgroup1) ],
            Tos = [ new(new(), SpecObj2) ],
            Services = [ new(){ Content = Svc1 } ]
        };

        public override async Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null)
        {
            await DefaultInit.DoNothing(); // qad avoid compiler warning
            Type responseType = typeof(QueryResponseType);
            if (responseType == typeof(List<Management>))
            {
                List<Management>? managements =
                [
                    new(){ Id = 1, Name = "Checkpoint1", ExtMgtData = "{\"id\":\"1\",\"name\":\"CheckpointExt\"}" }
                ];
                GraphQLResponse<dynamic> response = new() { Data = managements };
                return response.Data;
            }
            else if (responseType == typeof(List<NetworkObject>))
            {
                List<NetworkObject>? nwObjects = [];
                if (query == ObjectQueries.getNetworkObjectsForManagement)
                {
                    if (variables != null)
                    {
                        var objTypeIds = variables.GetType().GetProperties().First(o => o.Name == "objTypeIds").GetValue(variables, null);
                        if (objTypeIds != null && ( (int[])objTypeIds )[0] == 2)
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
                GraphQLResponse<dynamic> response = new() { Data = new List<Rule>() { Rule1, Rule2, Rule3, Rule4 } };

                return response.Data;
            }

            throw new NotImplementedException();
        }
    }
}
