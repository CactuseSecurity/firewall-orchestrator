using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Test.Mocks;

namespace FWO.Test
{
    internal partial class ExportTest
    {
        private static ReportData ConstructNatRuleReport()
        {
            NatRule = InitRule1(false);
            NatRule.NatData = new NatData()
            {
                TranslatedSourceNegated = false,
                TranslatedFroms =
                [
                    new (TestUser2, TestIp1Changed)
                ],
                TranslatedDestinationNegated = true,
                TranslatedTos =
                [
                    new (new NetworkUser(), TestIp1Changed),
                    new (new NetworkUser(), TestIpNew)
                ],
                TranslatedServiceNegated = false,
                TranslatedServices =
                [
                    new (){ Content = TestService1 },
                    new (){ Content = TestService2 }
                ]
            };
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        ReportObjects = [TestIp1, TestIp2, TestIpRange, TestIpNew, TestIp1Changed],
                        ReportServices = [TestService1, TestService2],
                        ReportUsers = [TestUser2],
                        Devices =
                        [
                            new (){ Name = "TestDev"}
                        ]
                    }
                ]
            };
        }

        private static ReportData ConstructChangeReport(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule1Changed = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            Rule2Changed = InitRule2(resolved);
            if (resolved)
            {
                Rule1Changed.Froms[0].Object.ObjectGroupFlats[0].Object = TestIp1Changed;
                Rule1Changed.Tos = [new (new NetworkUser(), new NetworkObject(){
                    Type = new NetworkObjectType() { Name = ObjectType.Group },
                    ObjectGroupFlats =
                    [
                        new (){ Object = TestIpRange },
                        new (){ Object = TestIpNew }
                    ]
                })];
            }
            else
            {
                Rule1Changed.Froms[0].Object = TestIp1Changed;
                Rule1Changed.Tos =
                [
                    new (new NetworkUser(), TestIpRange),
                    new (new NetworkUser(), TestIpNew)
                ];
            }
            Rule1Changed.Uid = "";
            Rule1Changed.ServiceNegated = true;
            Rule1Changed.Comment = "new comment";

            Rule2Changed.DestinationNegated = false;
            Rule2Changed.ServiceNegated = false;
            Rule2Changed.Disabled = true;

            RuleChange ruleChange1 = new()
            {
                ChangeAction = 'I',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                NewRule = Rule1
            };
            RuleChange ruleChange2 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule1,
                NewRule = Rule1Changed
            };
            RuleChange ruleChange3 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule2,
                NewRule = Rule2Changed
            };
            RuleChange ruleChange4 = new()
            {
                ChangeAction = 'D',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldRule = Rule2
            };
            ObjectChange objectChange1 = new()
            {
                ChangeAction = 'I',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                NewObject = TestIp1
            };
            ObjectChange objectChange2 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldObject = TestIp1,
                NewObject = TestIp1Changed
            };
            ObjectChange objectChange3 = new()
            {
                ChangeAction = 'D',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldObject = TestIp2
            };
            ServiceChange serviceChange1 = new()
            {
                ChangeAction = 'I',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                NewService = TestService1
            };
            ServiceChange serviceChange2 = new()
            {
                ChangeAction = 'C',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldService = TestService1,
                NewService = TestService2
            };
            ServiceChange serviceChange3 = new()
            {
                ChangeAction = 'D',
                ChangeImport = new ChangeImport() { Time = new DateTime(2023, 04, 05, 12, 0, 0) },
                OldService = TestService1
            };

            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        Devices =
                        [
                            new ()
                            {
                                Name = "TestDev",
                            }
                        ],
                        RuleChanges = [ruleChange1, ruleChange2, ruleChange3, ruleChange4],
                        ObjectChanges = [objectChange1, objectChange2, objectChange3],
                        ServiceChanges = [serviceChange1, serviceChange2, serviceChange3]
                    }
                ]
            };
        }

        private static async Task<ReportData> ConstructAppRulesReport()
        {
            ReportData reportData = ConstructRuleReportData(false);
            ModellingVarianceAnalysisTestApiConn apiConnection = new();
            reportData.ManagementData = await ReportAppRules.PrepareAppRulesReport(reportData.ManagementData, new ModellingFilter(), apiConnection, 1);
            return reportData;
        }

        private static ReportData ConstructConnectionReport()
        {
            ModellingAppServer AppServer1 = new() { Id = 11, Number = 1, Name = "AppServer1", Ip = "1.0.0.0" };
            ModellingAppServer AppServer2 = new() { Id = 12, Number = 2, Name = "AppServer2", Ip = "2.0.0.0" };
            ModellingAppRole AppRole1 = new() { Id = 21, Number = 3, Name = "AppRole1", IdString = "AR1", Comment = "CommAR1", AppServers = [new() { Content = AppServer1 }] };
            ModellingService Service1 = new() { Id = 31, Number = 1, Name = "Service1", Port = 1234, Protocol = new() { Id = 6, Name = "TCP" } };
            ModellingService Service2 = new() { Id = 32, Number = 2, Name = "Service2", Port = 2345, Protocol = new() { Id = 17, Name = "UDP" } };
            ModellingServiceGroup ServiceGroup1 = new() { Id = 41, Number = 3, Name = "ServiceGroup1", Comment = "CommSG1", Services = [new() { Content = Service1 }] };
            ModellingConnection Conn1 = new()
            {
                Id = 101,
                Name = "Conn1",
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppRoles = [new() { Content = AppRole1 }],
                Services = [new() { Content = Service1 }],
                ServiceGroups = [new() { Content = ServiceGroup1 }]
            };
            ModellingConnection Inter2 = new()
            {
                Id = 102,
                Name = "Inter2",
                DestinationAppServers = [new() { Content = AppServer2 }],
                DestinationAppRoles = [new() { Content = new() { Name = "noRole" } }],
                Services = [new() { Content = Service2 }],
                ServiceGroups = [new() { }]
            };
            ModellingConnection ComSvc3 = new()
            {
                Id = 103,
                Name = "ComSvc3",
                App = new() { Name = "App1" },
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppServers = [new() { Content = AppServer2 }],
                Services = [new() { Content = Service2 }],
                ServiceGroups = [new() { }]
            };

            ReportData reportData = new()
            {
                OwnerData =
                [
                    new ()
                    {
                        Name = "TestOwner",
                        Owner = new()
                        {
                            Name = "TestOwner",
                            ExtAppId = "APP-1234"
                        },
                        Connections = [Conn1, Inter2, ComSvc3],
                        RegularConnections = [Conn1],
                        Interfaces = [Inter2],
                        CommonServices = [ComSvc3],
                    }
                ],
                GlobalComSvc = [new() { GlobalComSvcs = [ComSvc3] }]
            };
            reportData.OwnerData[0].PrepareObjectData(true);
            return reportData;
        }

        private static ReportData ConstructVarianceReport()
        {
            ModellingAppServer AppServer1 = new() { Id = 11, Number = 1, Name = "AppServer1", Ip = "1.0.0.0" };
            ModellingAppServer AppServer2 = new() { Id = 12, Number = 2, Name = "AppServer2", Ip = "2.0.0.0" };
            ModellingAppRole AppRole1 = new() { Id = 21, Number = 3, Name = "AppRole1", IdString = "AR1", Comment = "CommAR1", AppServers = [new() { Content = AppServer1 }] };
            ModellingAppRole AppRole2 = new() { Id = 22, Number = 4, Name = "AppRole2", IdString = "AR2", Comment = "CommAR2", AppServers = [new() { Content = AppServer2 }] };
            NetworkSubnet Subnet1 = new() { Id = 1, Name = "Net1", Ip = "1.0.0.0" };
            ModellingNetworkArea Area1 = new() { Id = 51, Number = 5, Name = "Area50", IdString = "NA50", IpData = [new() { Content = Subnet1 }] };
            Dictionary<int, List<ModellingAppRole>> MissAR = new() { [0] = [AppRole1] };
            Dictionary<int, List<ModellingAppRole>> DiffAR = new() { [0] = [AppRole2] };
            ModellingService Service1 = new() { Id = 31, Number = 1, Name = "Service1", Port = 1234, Protocol = new() { Id = 6, Name = "TCP" } };
            ModellingServiceGroup ServiceGroup1 = new() { Id = 41, Number = 3, Name = "ServiceGroup1", Comment = "CommSG1", Services = [new() { Content = Service1 }] };
            ModellingConnection Conn1 = new()
            {
                Id = 101,
                Name = "Conn1",
                SourceAppServers = [new() { Content = AppServer1 }],
                SourceAppRoles = [new() { Content = AppRole1 }],
                SourceAreas = [new() { Content = Area1 }],
                DestinationAppRoles = [new() { Content = AppRole2 }],
                Services = [new() { Content = Service1 }],
                ServiceGroups = [new() { Content = ServiceGroup1 }]
            };
            ModellingConnection Conn2 = new()
            {
                Id = 102,
                Name = "Conn2",
                SourceAppServers = [new() { Content = AppServer1 }],
                DestinationAppRoles = [new() { Content = AppRole2 }],
                Services = [new() { Content = Service1 }],
            };
            Rule1 = InitRule1(true);

            ReportData reportData = new()
            {
                OwnerData =
                [
                    new ()
                    {
                        Name = "TestOwner",
                        Connections = [Conn1],
                        RegularConnections = [Conn1],
                        MissingAppRoles = MissAR,
                        DifferingAppRoles = DiffAR,
                        RuleDifferences = [],
                        ModelledConnectionsCount = 2,
                        AppRoleStats = new()
                        {
                            ModelledAppRolesCount = 2,
                            AppRolesOk = 0,
                            AppRolesMissingCount = 1,
                            AppRolesDifferenceCount = 1
                        }
                    }
                ]
            };
            reportData.OwnerData[0].RuleDifferences.Add(new() { ModelledConnection = Conn2, ImplementedRules = [Rule1] });
            reportData.OwnerData[0].PrepareObjectData(true);
            return reportData;
        }

    }
}
