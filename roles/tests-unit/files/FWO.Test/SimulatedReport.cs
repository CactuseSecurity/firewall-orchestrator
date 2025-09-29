using FWO.Api.Client;
using FWO.Data.Report;
using FWO.Config.Api;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Basics;
using FWO.Data;

namespace FWO.Test
{
    internal class SimulatedReport : ReportBase
    {
        public static SimulatedReport DetailedReport(ReportType reportType = ReportType.Rules) => new(new DynGraphqlQuery(""), new SimulatedUserConfig(), reportType)
        {
            ReportData = new()
            {
                ManagementData =
                [
                    new ManagementReport
                    {
                        Id = 1,
                        Name = "Management 1",
                        Devices = 
                        [
                            new DeviceReport
                            {
                                Id = 1,
                                Name = "Device 1",
                                RulebaseLinks =
                                [
                                    new RulebaseLink
                                    {
                                        GatewayId = 1,
                                        NextRulebaseId = 1,
                                        IsInitial = true
                                    }
                                ]
                            }
                        ],
                        Objects = 
                        [
                            new() { Id = 1, Name = "Object 1" },
                            new() { Id = 2, Name = "Object 2" }
                        ],
                        Services = 
                        [
                            new() { Id = 1, Name = "Service 1" },
                            new() { Id = 2, Name = "Service 2" }
                        ],
                        Users = 
                        [
                            new() { Id = 1, Name = "User 1" },
                            new() { Id = 2, Name = "User 2" }
                        ],
                        ReportObjects =
                        [
                            new() { Id = 1, Name = "Report Object 1" },
                            new() { Id = 2, Name = "Report Object 2" }
                        ],
                        ReportServices =
                        [
                            new() { Id = 1, Name = "Report Service 1" },
                            new() { Id = 2, Name = "Report Service 2" }
                        ],
                        ReportUsers =
                        [
                            new() { Id = 1, Name = "Report User 1" },
                            new() { Id = 2, Name = "Report User 2" }
                        ],
                        Rulebases =
                        [
                            new RulebaseReport
                            {
                                Id = 1,
                                Name = "Rulebase 1",
                                Rules =
                                [
                                    new Rule
                                    {
                                        Id = 1,
                                        Name = "Rule 1",
                                        Source = "Object 1",
                                        Destination = "Object 2",
                                        Service = "Service 1",
                                        Action = "accept"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        public SimulatedReport(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        {
        }

        override public Task<bool> GetObjectsForManagementInReport(Dictionary<string, object> objQueryVariables, ObjCategory objects, int maxFetchCycles, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            callback(DetailedReport().ReportData);
            return Task.FromResult(true);
        }

        override public Task Generate(int elementsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        override public Task<bool> GetObjectsInReport(int objectsPerFetch, ApiConnection apiConnection, Func<ReportData, Task> callback)
        {
            return Task.FromResult(true);
        }

        override public string ExportToCsv()
        {
            return "";
        }

        override public string ExportToJson()
        {
            return "";
        }

        override public string ExportToHtml()
        {
            return "";
        }

        override public string SetDescription()
        {
            return "";
        }
    }
}