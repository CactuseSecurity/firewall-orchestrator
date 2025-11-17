using System.Net;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Test.Mocks;
using NetTools;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceCheckTest
    {
        #region Configuration

        private ComplianceCheck _complianceCheck = default!;
        private TimeSpan _maxAcceptableExecutionTime = TimeSpan.FromSeconds(60);
        private List<Rule>[] _ruleChunks = default!;
        private GlobalConfig _globalConfig = default!;
        private UserConfig _userConfig = default!;
        private MockApiConnection _apiConnection = default!;
        private MockLogger _logger = default!;

        // Expected logs

        private const string PolicyIdZero = "Compliance Check - No Policy defined. Compliance check not possible.";
        private const string PolicyNull = "Compliance Check - Policy with id 1 not found.";
        private const string NoCriteria = "Compliance Check - Policy without criteria. Compliance check not possible";
        private const string NoRelevantManager = "Compliance Check - No relevant managements found. Compliance check not possible.";
        private const string NoViolations = "Compliance Check - Checked compliance for 5 rules and found 0 non-compliant rules";
        private const string BasicSetup = "Compliance Check - Checked compliance for 5 rules and found 3 non-compliant rules";

        // Parameters for test configuration

        private const string ForbiddenServiceUid = "forbidden-service-uid";

        [SetUp]
        public void SetUpTest()
        {
            LocalSettings.ComplianceCheckVerbose = true;

            _apiConnection = new();
            _logger = new();
            _globalConfig = new SimulatedGlobalConfig { AutoCalculateInternetZone = true, AutoCalculateUndefinedInternalZone = true, TreatDynamicAndDomainObjectsAsInternet = true };
            _userConfig = new UserConfig(_globalConfig, false);

            SimulatedUserConfig.DummyTranslate["internet_local_zone"] = "Internet/Local";
            SimulatedUserConfig.DummyTranslate["assess_broadcast"] = "Network objects in source or destination with 255.255.255.255/32";
            SimulatedUserConfig.DummyTranslate["assess_host_address"] = "Network objects in source or destination with 0.0.0.0/32";
            SimulatedUserConfig.DummyTranslate["assess_all_ips"] = "Network objects in source or destination with 0.0.0.0/0 or ::/0";
            SimulatedUserConfig.DummyTranslate["assess_ip_null"] = "Network objects in source or destination without IP";
            SimulatedUserConfig.DummyTranslate["H5839"] = "Matrix violation";
            SimulatedUserConfig.DummyTranslate["H5840"] = "Restricted Service";
            SimulatedUserConfig.DummyTranslate["H5841"] = "Assessability issue";
            
            _complianceCheck = new ComplianceCheck(_userConfig, _apiConnection, _logger.AsSub());
            _complianceCheck.NetworkZones = CreateNetworkZones(50);
            _complianceCheck.ComplianceReport = new(new(""), _userConfig, ReportType.ComplianceReport);
        }

        private Task SetUpBasic(bool createEmptyPolicy = false, bool setupRelevantManagements = false, bool createPolicy = false, bool createRules = false, bool setupNoViolations = false)
        {
            _globalConfig.ComplianceCheckPolicyId = 1;

            if (createEmptyPolicy)
            {
                _apiConnection.AsSub()
                    .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                    .Returns(new CompliancePolicy()); // Policy without criteria                
            }

            if (setupRelevantManagements)
            {
                _globalConfig.ComplianceCheckRelevantManagements = "1,2,3";

                _apiConnection.AsSub()
                    .SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames)
                    .Returns([new Management { Id = 1, Name = "Mgmt1" }]);
            }

            if (createPolicy)
            {
                _apiConnection.AsSub()
                    .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                    .Returns(CreatePolicy()); // Policy with criteria

                _apiConnection.AsSub()
                    .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, Arg.Any<object>())
                        .Returns(_complianceCheck.NetworkZones);
            }

            if (createRules)
            {
                _apiConnection.AsSub()
                    .SendQueryAsync<List<Rule>>(RuleQueries.getRulesForSelectedManagements)
                    .Returns(CreateRulesForComplianceCheckTest(setupNoViolations));
            }

            return Task.CompletedTask;

        }

        #endregion

        #region Tests - CheckAll

        [Test]
        public async Task CheckAll_PolicyIdZero_AbortCheckWithLog()
        {
            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId == 0, "Default policy ID should be zero for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(PolicyIdZero)), "Expected log message not found.");
        }

        [Test]
        public async Task CheckAll_PolicyNull_AbortCheckWithLog()
        {
            // Arrange

            await SetUpBasic();

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy == null, "Policy should be null for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(PolicyNull)), "Expected log message not found.");
        }

        [Test]
        public async Task CheckAll_NoRelevantManager_AbortWithLog()
        {
            // Arrange

            await SetUpBasic(createEmptyPolicy: true);

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(NoRelevantManager)), "Unexpected violations.");
        }

        [Test]
        public async Task CheckAll_NoCriteria_AbortCheckWithLog()
        {
            // Arrange

            await SetUpBasic(createEmptyPolicy: true, setupRelevantManagements: true);

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(NoCriteria)), "Expected log message not found.");
        }



        [Test]
        public async Task CheckAll_NoViolations_CompleteWithLog()
        {
            // Arrange

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true, setupNoViolations: true);

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(_complianceCheck.CurrentViolationsInCheck.Count == 0, "There should be no violations for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(NoViolations)), "Unexpected violations.");
        }

        [Test]
        public async Task CheckAll_BasicSetup_CompleteWithLog()
        {
            // Arrange

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(_complianceCheck.CurrentViolationsInCheck.Count == 2, "There should be one violation for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
        }


        #endregion

        #region Tests - CheckRuleCompliance

        [Test]
        public async Task CheckRuleCompliance_HeavyLoad_ExecutionTimeLessThanConfiguredLimit()
        {
            // Arrange

            int numberOfChunks = 100;
            int numberOfRulesPerChunk = 100;
            int ruleId = 1;

            _ruleChunks = BuildFixedRuleChunksParallel(numberOfChunks, numberOfRulesPerChunk, ruleId);

            // Act

            DateTime executionStart = DateTime.Now;
            foreach (var chunk in _ruleChunks)
            {
                foreach (var rule in chunk)
                {
                    await _complianceCheck.CheckRuleCompliance(rule);
                }
            }
            DateTime executionEnd = DateTime.Now;
            TimeSpan executionTime = executionEnd - executionStart;

            // Assert

            Assert.That(executionTime < _maxAcceptableExecutionTime, $"Execution time was {executionTime.Seconds} s, expected: {_maxAcceptableExecutionTime.Seconds} s.");
        }

        #endregion

        #region Tests - ParseIpRange
        /*
            This region is thought to be temporary. In the long run all data generation should be done by the test framework.
        */

        [Test]
        public async Task ParseIpRange_NwObjectOfTypeIpRange_AddedToReturnedList()
        {
            // Arrange

            NetworkObject networkObject = new();
            networkObject.IP = "0.0.0.0";
            networkObject.IpEnd = "255.255.255.255";
            networkObject.Type.Name = ObjectType.IPRange;

            // Act

            List<IPAddressRange> result = ComplianceCheck.ParseIpRange(networkObject);

            // Assert

            Assert.That(result.Count == 1);
            Assert.That(result.First().Begin.ToString() == networkObject.IP);
            Assert.That(result.First().End.ToString() == networkObject.IpEnd);
        }

        [Test]
        public async Task ParseIpRange_NwObjectOfTypeIpRangeWithSubnetSuffix_AddedToReturnedList()
        {
            // Arrange

            NetworkObject networkObject = new();
            networkObject.IP = "0.0.0.0/32";
            networkObject.IpEnd = "255.255.255.255/32";
            networkObject.Type.Name = ObjectType.IPRange;

            // Act

            List<IPAddressRange> result = ComplianceCheck.ParseIpRange(networkObject);

            // Assert

            Assert.That(result.Count == 1);
            Assert.That(result.First().Begin.ToString() == networkObject.IP.StripOffNetmask());
            Assert.That(result.First().End.ToString() == networkObject.IpEnd.StripOffNetmask());
        }

        #endregion

        #region Test-data-generation

        private List<Rule>[] BuildFixedRuleChunksParallel(int numberOfChunks, int numberOfRulesPerChunk, int startRuleId = 1, int? maxDegreeOfParallelism = null)
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
                        list.Add(CreateRule(baseId + j));
                    }

                    ruleChunks[i] = list;
                });

            return ruleChunks;
        }

        private List<Rule> CreateRulesForComplianceCheckTest(bool allCompliant = false)
        {
            if (allCompliant)
            {
                return new List<Rule>
                {
                    CreateRule(1),
                    CreateRule(2),
                    CreateRule(3),
                    CreateRule(4),
                    CreateRule(5)
                };
            }
            else
            {
                Rule ruleNotAssessable = CreateRule(1);
                ruleNotAssessable.Froms[0].Object.IP = "0.0.0.0/32";
                ruleNotAssessable.Froms[0].Object.IpEnd = "255.255.255.255/32";


                Rule ruleMatrixViolation = CreateRule(2);
                ruleMatrixViolation.Froms[0].Object.IP = "255.255.255.0/32";
                ruleMatrixViolation.Froms[0].Object.IpEnd = "255.255.255.254/32";

                // Update network zone to make sure matrix violation is detected
                ComplianceNetworkZone networkZone = _complianceCheck.NetworkZones.First();
                networkZone.Name = "Restricted Zone";
                networkZone.IPRanges = [(new IPAddressRange(IPAddress.Parse("255.255.255.0"), IPAddress.Parse("255.255.255.254")))];

                Rule ruleForbiddenService = CreateRule(3);
                ruleForbiddenService.Services[0].Content.Uid = ForbiddenServiceUid;

                Rule ruleCompliant1 = CreateRule(4);
                Rule ruleCompliant2 = CreateRule(5);

                return new List<Rule>
                {
                    ruleNotAssessable,
                    ruleMatrixViolation,
                    ruleForbiddenService,
                    ruleCompliant1,
                    ruleCompliant2
                };
            }
        }

        private Rule CreateRule(int ruleID)
        {
            Rule rule = new Rule
            {
                Id = ruleID,
                Action = "accept",
                MgmtId = 1
            };

            List<ServiceWrapper> services = new();
            List<NetworkLocation> froms = new();
            List<NetworkLocation> tos = new();

            for (int i = 0; i < 10; i++)
            {
                ServiceWrapper service = new();
                NetworkUser user = new();
                NetworkObject networkObject = new();
                networkObject.IP = "0.0.0.1/32";
                networkObject.IpEnd = "255.255.254.255/32";
                networkObject.Name = $"NwObject_Rule{ruleID}_Num{i+1}";
                NetworkLocation networkLocation = new(user, networkObject);

                if( i % 2 == 0 )
                {
                    tos.Add(networkLocation);
                }
                else
                {
                    froms.Add(networkLocation);
                }

                services.Add(service);
            }

            rule.Froms = froms.ToArray();
            rule.Tos = tos.ToArray();
            rule.Services = services.ToArray();

            return rule;
        }

        private CompliancePolicy CreatePolicy()
        {
            CompliancePolicy policy = new();
            ComplianceCriterionWrapper serviceCriterion = new();
            serviceCriterion.Content.CriterionType = nameof(CriterionType.ForbiddenService);
            serviceCriterion.Content.Content = ForbiddenServiceUid;
            ComplianceCriterionWrapper matrixCriterion = new();
            matrixCriterion.Content.CriterionType = nameof(CriterionType.Matrix);
            ComplianceCriterionWrapper assessabilityCriterion = new();
            assessabilityCriterion.Content.CriterionType = nameof(CriterionType.Assessability);
            policy.Criteria.AddRange([serviceCriterion, matrixCriterion, assessabilityCriterion]);
            return policy;
        }

        private List<ComplianceNetworkZone> CreateNetworkZones(int numberOfZones)
        {
            List<ComplianceNetworkZone> networkZones = new();

            for (int i = 0; i < numberOfZones; i++)
            {
                ComplianceNetworkZone networkZone = new();
                networkZones.Add(networkZone);
            }

            return networkZones;
        }

        #endregion
    }
}
