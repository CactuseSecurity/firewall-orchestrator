using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Test.Mocks;

namespace FWO.Test
{
    internal partial class ExportTest
    {
        private static NetworkLocation[] InitFroms(bool resolved, bool user = false)
        {
            if (resolved)
            {
                return [ new NetworkLocation(user ? TestUser1 : new NetworkUser(), new NetworkObject(){
                    Type = new NetworkObjectType() { Name = ObjectType.Group },
                    ObjectGroupFlats =
                    [
                        new(){ Object = TestIp1 },
                        new(){ Object = TestIp2 }
                    ]
                })];
            }
            else
            {
                return
                [
                    new(user ? TestUser1 : new NetworkUser(), TestIp1),
                    new(user ? TestUser1 : new NetworkUser(), TestIp2)
                ];
            }
        }

        private static NetworkLocation[] InitTos(bool resolved, bool user = false)
        {
            if (resolved)
            {
                return [ new NetworkLocation(user ? TestUser2 : new NetworkUser(), new NetworkObject(){
                    Type = new NetworkObjectType() { Name = ObjectType.Group },
                    ObjectGroupFlats =
                    [
                        new(){ Object = TestIpRange }
                    ]
                })];
            }
            else
            {
                return
                [
                    new(user ? TestUser2 : new NetworkUser(), TestIpRange),
                ];
            }
        }

        private static ServiceWrapper[] InitServices(NetworkService service, bool resolved)
        {
            if (resolved)
            {
                return [new ServiceWrapper(){ Content = new NetworkService(){
                    Type = new NetworkServiceType() { Name = ServiceType.Group },
                    ServiceGroupFlats =
                    [
                        new GroupFlat<NetworkService>(){ Object = service }
                    ]
                } }];
            }
            else
            {
                return
                [
                    new(){ Content = service },
                ];
            }
        }

        private static Rule InitRule1(bool resolved)
        {
            var srcZoneNames = new[] { "srczn1", "srczn2", "srczn3" };
            var dstZoneNames = new[] { "dstzn1", "dstzn2", "dstzn3" };


            return new Rule()
            {
                Name = "TestRule1",
                Action = "accept",
                Comment = "comment1",
                Disabled = false,
                DisplayOrderNumber = 1,
                Track = "none",
                Uid = "uid1",
                RuleFromZones = srcZoneNames.Select(name => new ZoneWrapper { Content = new NetworkZone { Name = name } }).ToArray(),
                SourceNegated = false,
                Froms = InitFroms(resolved),
                RuleToZones = dstZoneNames.Select(name => new ZoneWrapper { Content = new NetworkZone { Name = name } }).ToArray(),
                DestinationNegated = false,
                Tos = InitTos(resolved),
                ServiceNegated = false,
                Services = InitServices(TestService1, resolved),
                Metadata = new RuleMetadata() { LastHit = new DateTime(2022, 04, 19) },
                LastSeenImport = new ImportControl { StartTime = new DateTime(2023, 04, 05) }
            };
        }

        private static Rule InitRule2(bool resolved)
        {
            return new Rule()
            {
                Name = "TestRule2",
                Action = "deny",
                Comment = "comment2",
                Disabled = false,
                DisplayOrderNumber = 2,
                Track = "none",
                Uid = "uid2:123",
                SourceNegated = true,
                Froms = InitFroms(resolved, true),
                DestinationNegated = true,
                Tos = InitTos(resolved, true),
                ServiceNegated = true,
                Services = InitServices(TestService2, resolved),
                LastSeenImport = new ImportControl { StartTime = new DateTime(2023, 04, 05) }
            };
        }

        //If possible use ConstructReportRules below so that this can be deprecated
        private static ReportData ConstructRuleReportData(bool resolved)
        {
            Rule1 = InitRule1(resolved);
            Rule2 = InitRule2(resolved);
            return new ReportData()
            {
                ManagementData =
                [
                    new ()
                    {
                        Name = "TestMgt",
                        ReportObjects = [TestIp1, TestIp2, TestIpRange],
                        ReportServices = [TestService1, TestService2],
                        ReportUsers = [TestUser1, TestUser2],
                        Devices =
                        [
                            new ()
                            {
                                Name = "TestDev"
                            }
                        ]
                    }
                ]
            };
        }

        private static ReportRules ConstructReportRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, Rule[] rules)
        {
            RulebaseReport[] rulebases = [
                MockReportRules.CreateRulebaseReport(numberOfRules: 2)
            ];
            rulebases.First().Rules = rules;
            RulebaseLink[] rulebaseLinks = [
                new() {IsInitial = true,NextRulebaseId = rulebases[0].Id}
            ];

            MockReportRules reportRules = new MockReportRules(query, userConfig, reportType);

            var managementData = reportRules.ReportData.ManagementData.First();

            managementData.Rulebases = rulebases;
            managementData.Devices.First().RulebaseLinks = rulebaseLinks;

            managementData.Name = "TestMgt";
            managementData.ReportObjects = [TestIp1, TestIp2, TestIpRange];
            managementData.ReportServices = [TestService1, TestService2];
            managementData.ReportUsers = [TestUser1, TestUser2];

            reportRules.ReportData.ManagementData = [managementData];

            reportRules.TryBuildMockRuleTree();

            return reportRules;
        }

        private static async Task<ReportRules> ConstructAppReportRules(DynGraphqlQuery query, UserConfig userConfig,
            ReportType reportType, Rule[] rules)
        {
            ReportRules report = ConstructReportRules(query, userConfig, reportType, rules);
            ModellingVarianceAnalysisTestApiConn apiConnection = new();
            report.ReportData.ManagementData = await ReportAppRules.PrepareAppRulesReport(report.ReportData.ManagementData, new ModellingFilter(), apiConnection, 1);
            return report;
        }

        private Rule[] ConstructRuleReportRules(bool resolved)
        {
            return [
                InitRule1(resolved),
                InitRule2(resolved)
            ];
        }

        private Rule[] ConstructRecertReportRules(bool resolved)
        {
            Rule rule1 = InitRule1(resolved);
            rule1.Metadata.RuleRecertification =
            [
                new ()
                {
                    NextRecertDate  = DateTime.Now.AddDays(5),
                    FwoOwner = new FwoOwner(){ Name = "TestOwner1" },
                    IpMatch = TestIp1.Name
                },
                new ()
                {
                    NextRecertDate  = DateTime.Now.AddDays(-5),
                    FwoOwner = new FwoOwner(){ Name = "TestOwner2" },
                    IpMatch = TestIp2.Name
                }
            ];
            Rule rule2 = InitRule2(resolved);
            rule2.Metadata.RuleRecertification =
            [
                new ()
                {
                    NextRecertDate  = DateTime.Now,
                    FwoOwner = new FwoOwner(){ Name = "TestOwner1" },
                    IpMatch = TestIpRange.Name
                }
            ];
            return [rule1, rule2];
        }

        private static ReportRules ConstructReportRulesWithoutRules(bool resolved, DynGraphqlQuery query, UserConfig userConfig, ReportType reportType)
        {
            RulebaseReport[] rulebases = [
                MockReportRules.CreateRulebaseReport(numberOfRules: 0)
            ];
            RulebaseLink[] rulebaseLinks = [
                new() {IsInitial = true,NextRulebaseId = rulebases[0].Id}
            ];

            MockReportRules reportRules = new MockReportRules(query, userConfig, reportType);

            var managementData = reportRules.ReportData.ManagementData.First();

            managementData.Rulebases = rulebases;
            managementData.Devices.First().RulebaseLinks = rulebaseLinks;

            Rule[] rules =
            [
            ];

            managementData.Rulebases.First().Rules = rules;
            managementData.Name = "TestMgt";
            managementData.ReportObjects = [TestIp1, TestIp2, TestIpRange];
            managementData.ReportServices = [TestService1, TestService2];
            managementData.ReportUsers = [TestUser1, TestUser2];

            reportRules.ReportData.ManagementData = [managementData];

            reportRules.TryBuildMockRuleTree();

            return reportRules;
        }

    }
}
