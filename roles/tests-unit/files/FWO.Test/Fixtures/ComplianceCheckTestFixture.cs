using System.Net;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Test.Mocks;
using NetTools;
using NSubstitute;

namespace FWO.Test.Fixtures
{
    public class ComplianceCheckTestFixture
    {
        protected virtual ComplianceCheck ComplianceCheck { get; set; } = default!;
        protected virtual TimeSpan MaxAcceptableExecutionTime { get; set; } = TimeSpan.FromSeconds(60);
        protected virtual List<Rule>[] RuleChunks { get; set; } = default!;
        protected virtual GlobalConfig GlobalConfig { get; set; } = default!;
        protected virtual UserConfig UserConfig { get; set; } = default!;
        protected virtual MockApiConnection ApiConnection { get; set; } = default!;
        protected virtual MockLogger Logger { get; set; } = default!;
        protected virtual CompliancePolicy? Policy { get; set; } = null;

        private const string ForbiddenServiceUid = "forbidden-service-uid";

        public virtual void SetUpTest()
        {
            LocalSettings.ComplianceCheckVerbose = true;

            ApiConnection = new();
            Logger = new();
            GlobalConfig = new SimulatedGlobalConfig { AutoCalculateInternetZone = true, AutoCalculateUndefinedInternalZone = true, TreatDynamicAndDomainObjectsAsInternet = true };
            UserConfig = new UserConfig(GlobalConfig, false);

            SimulatedUserConfig.DummyTranslate["internet_local_zone"] = "Internet/Local";
            SimulatedUserConfig.DummyTranslate["assess_broadcast"] = "Network objects in source or destination with 255.255.255.255/32";
            SimulatedUserConfig.DummyTranslate["assess_host_address"] = "Network objects in source or destination with 0.0.0.0/32";
            SimulatedUserConfig.DummyTranslate["assess_all_ips"] = "Network objects in source or destination with 0.0.0.0/0 or ::/0";
            SimulatedUserConfig.DummyTranslate["assess_ip_null"] = "Network objects in source or destination without IP";
            SimulatedUserConfig.DummyTranslate["H5839"] = "Matrix violation";
            SimulatedUserConfig.DummyTranslate["H5840"] = "Restricted Service";
            SimulatedUserConfig.DummyTranslate["H5841"] = "Assessability issue";
            
            ComplianceCheck = new ComplianceCheck(UserConfig, ApiConnection, Logger.AsSub());
            ComplianceCheck.NetworkZones = CreateNetworkZones(true, true);
            ComplianceCheck.ComplianceReport = new(new(""), UserConfig, ReportType.ComplianceReport);
        }

        protected virtual Task SetUpBasic(bool createEmptyPolicy = false, bool setupRelevantManagements = false, bool createPolicy = false, bool createRules = false, bool setupNoViolations = false)
        {
            GlobalConfig.ComplianceCheckPolicyId = 1;

            if (createEmptyPolicy)
            {
                ApiConnection.AsSub()
                    .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                    .Returns(new CompliancePolicy()); // Policy without criteria                
            }

            if (setupRelevantManagements)
            {
                GlobalConfig.ComplianceCheckRelevantManagements = "1,2,3";

                ApiConnection.AsSub()
                    .SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames)
                    .Returns([new Management { Id = 1, Name = "Mgmt1" }]);
            }

            if (createPolicy)
            {
                Policy = CreatePolicy(ForbiddenServiceUid);
                ComplianceCheck.Policy = Policy;

                ApiConnection.AsSub()
                    .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                    .Returns(Policy); // Policy with criteria

                ApiConnection.AsSub()
                    .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, Arg.Any<object>())
                        .Returns(ComplianceCheck.NetworkZones);
            }

            if (createRules)
            {
                if (!setupNoViolations)
                {
                    ComplianceCriterion? matrix = ComplianceCheck.Policy!.Criteria
                        .FirstOrDefault(c => c.Content.CriterionType == nameof(CriterionType.Matrix))?.Content;

                    ComplianceCheck.NetworkZones = CreateNetworkZones(true, true);                
                }

                ComplianceCheck.RulesInCheck = CreateRulesForComplianceCheckTest(setupNoViolations, ForbiddenServiceUid);

                ApiConnection.AsSub()
                    .SendQueryAsync<List<Rule>>(RuleQueries.getRulesForSelectedManagements, Arg.Any<object?>())
                    .Returns(ComplianceCheck.RulesInCheck);
            }

            return Task.CompletedTask;
        }

        protected virtual List<Rule>[] BuildFixedRuleChunksParallel(int numberOfChunks, int numberOfRulesPerChunk, int startRuleId = 1, int? maxDegreeOfParallelism = null)
        {
            if (numberOfChunks <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfChunks));
            if (numberOfRulesPerChunk < 0) throw new ArgumentOutOfRangeException(nameof(numberOfRulesPerChunk));

            var ruleChunks = new List<Rule>[numberOfChunks];

            Parallel.For(
                0, numberOfChunks,
                new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount },
                i =>
                {
                    var list = new List<Rule>(numberOfRulesPerChunk);
                    int baseId = startRuleId + i * numberOfRulesPerChunk;

                    for (int j = 0; j < numberOfRulesPerChunk; j++)
                    {
                        list.Add(CreateSimpleRule(baseId + j));
                    }

                    ruleChunks[i] = list;
                });

            return ruleChunks;
        }

        protected virtual List<Rule> CreateRulesForComplianceCheckTest(bool allCompliant = false, string forbiddenServiceUid = "")
        {
            if (allCompliant)
            {
                return new List<Rule>
                {
                    CreateSimpleRule(1),
                    CreateSimpleRule(2),
                    CreateSimpleRule(3),
                    CreateSimpleRule(4),
                    CreateSimpleRule(5)
                };
            }
            else
            {
                Rule ruleNotAssessable = CreateSimpleRule(1);
                ruleNotAssessable.Froms[0].Object.IP = "0.0.0.0/32";
                ruleNotAssessable.Froms[0].Object.IpEnd = "255.255.255.255/32";

                Rule ruleMatrixViolation = CreateSimpleRule(2, destinationHigh: true);

                Rule ruleMatrixViolationAutoCalcInternet = CreateSimpleRule(3);
                ruleMatrixViolationAutoCalcInternet.Froms[0] = new NetworkLocation(
                    new NetworkUser(),
                    CreateNetworkObject(3, "source", ObjectType.IPRange) // will be in Internet zone
                );

                Rule ruleForbiddenService = CreateSimpleRule(4);
                ruleForbiddenService.Services[0].Content.Uid = forbiddenServiceUid;


                Rule ruleCompliant = CreateSimpleRule(5);

                return new List<Rule>
                {
                    ruleNotAssessable,
                    ruleMatrixViolation,
                    ruleMatrixViolationAutoCalcInternet,
                    ruleForbiddenService,
                    ruleCompliant
                };
            }
        }

        protected virtual Rule CreateSimpleRule(int ruleID, bool sourceHigh = false, bool destinationHigh = false)
        {
            Rule rule = new Rule
            {
                Id = ruleID,
                Action = "accept",
                MgmtId = 1
            };

            NetworkUser user = new();
            NetworkObject source = CreateNetworkObject(ruleID, "source", ObjectType.IPRange, sourceHigh);
            NetworkObject destination = CreateNetworkObject(ruleID, "destination", ObjectType.IPRange, destinationHigh);
            ServiceWrapper service = new();
            service.Content.Uid = $"service-uid-{ruleID}";

            rule.Froms = [new(user, source)];
            rule.Tos = [new(user, destination)];
            rule.Services = [service];

            return rule;
        }

        protected virtual NetworkObject CreateNetworkObject(int ruleId, string uidPrefix, string objectTypeName, bool? highIpRange = null)
        {
            NetworkObject networkObject = new();

            if (highIpRange != null)
            {
                if (highIpRange.Value)
                {
                    networkObject.IP = "193.0.0.0/32";
                    networkObject.IpEnd = "198.0.0.0/32";
                }
                else
                {
                    networkObject.IP = "128.0.0.0/32";
                    networkObject.IpEnd = "168.0.0.0/32";
                }
            }
            else // for setting up objects with IPs that are undefined, but not part of a reserved range
            {
                networkObject.IP = "3.0.0.0/32";
                networkObject.IpEnd = "4.0.0.0/32";
            }

            networkObject.Name = $"{uidPrefix}-uid-rule{ruleId}";
            networkObject.Type.Name = objectTypeName;

            return networkObject;
        }

        protected virtual CompliancePolicy CreatePolicy(string forbiddenServiceUid = "")
        {
            CompliancePolicy policy = new();
            ComplianceCriterionWrapper serviceCriterion = new();
            serviceCriterion.Content.CriterionType = nameof(CriterionType.ForbiddenService);
            serviceCriterion.Content.Content = forbiddenServiceUid;
            ComplianceCriterionWrapper matrixCriterion = new();
            matrixCriterion.Content.CriterionType = nameof(CriterionType.Matrix);
            ComplianceCriterionWrapper assessabilityCriterion = new();
            assessabilityCriterion.Content.CriterionType = nameof(CriterionType.Assessability);
            policy.Criteria.AddRange([serviceCriterion, matrixCriterion, assessabilityCriterion]);
            return policy;
        }

        protected virtual List<ComplianceNetworkZone> CreateNetworkZones(bool createInternetZone, bool createUndefinedInternalZone)
        {
            List<ComplianceNetworkZone> networkZones = new()
            {
                new()
                {
                    Id = 1,
                    CriterionId = 1,
                    Name = "128-168 Zone",
                    IdString = "zone_1",
                    IPRanges =
                    [
                        new IPAddressRange(
                            IPAddress.Parse("128.0.0.0"),
                            IPAddress.Parse("168.0.0.0")
                        )
                    ]
                },
                new()
                {
                    Id = 2,
                    CriterionId = 1,
                    Name = "193-198 Zone",
                    IdString = "zone_2",
                    IPRanges =
                    [
                        new IPAddressRange(
                            IPAddress.Parse("193.0.0.0"),
                            IPAddress.Parse("198.0.0.0")
                        )
                    ]
                }
            };

            if (createInternetZone)
            {
                networkZones.Add(
                    new()
                    {
                        Id = 3,
                        CriterionId = 1,
                        Name = "Auto-calculated Internet Zone",
                        IdString = "AUTO_CALCULATED_ZONE_INTERNET",
                        IsAutoCalculatedInternetZone = true,
                        IPRanges =
                        [
                            new IPAddressRange(
                                IPAddress.Parse("0.0.0.0"),
                                IPAddress.Parse("9.255.255.255")
                            ),
                            new IPAddressRange(
                                IPAddress.Parse("11.0.0.0"),
                                IPAddress.Parse("127.255.255.255")
                            ),
                            new IPAddressRange(
                                IPAddress.Parse("168.0.0.1"),
                                IPAddress.Parse("192.255.255.255")
                            ),
                            new IPAddressRange(
                                IPAddress.Parse("198.0.0.1"),
                                IPAddress.Parse("255.255.255.255")
                            )
                        ]
                    }
                ); 
            }

            if (createUndefinedInternalZone)
            {
                networkZones.Add(
                    new()
                    {
                        Id = 4,
                        CriterionId = 1,
                        Name = "Auto-calculated Undefined-Interal Zone",
                        IdString = "AUTO_CALCULATED_ZONE_UNDEFINED_INTERNAL",
                        IsAutoCalculatedUndefinedInternalZone = true,
                        IPRanges =
                        [
                            new IPAddressRange(
                                IPAddress.Parse("10.0.0.0"),
                                IPAddress.Parse("10.255.255.255")
                            )
                        ]
                    }
                ); 
            }

            foreach (ComplianceNetworkZone zone in networkZones.Where(zone => !zone.IsAutoCalculatedUndefinedInternalZone).ToList())
            {
                zone.AllowedCommunicationDestinations = [zone];
            }

            return networkZones;
        }
    }
}