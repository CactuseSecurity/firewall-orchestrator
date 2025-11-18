using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Test.Fixtures;
using FWO.Test.Mocks;
using NetTools;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceCheckTest : ComplianceCheckTestFixture
    {
        #region Configuration

        private ComplianceCheck _complianceCheck = default!;
        private TimeSpan _maxAcceptableExecutionTime = TimeSpan.FromSeconds(60);
        private List<Rule>[] _ruleChunks = default!;
        private GlobalConfig _globalConfig = default!;
        private UserConfig _userConfig = default!;
        private MockApiConnection _apiConnection = default!;
        private MockLogger _logger = default!;
        private CompliancePolicy? _policy = null;

        // Expected logs

        private const string PolicyIdZero = "Compliance Check - No Policy defined. Compliance check not possible.";
        private const string PolicyNull = "Compliance Check - Policy with id 1 not found.";
        private const string NoCriteria = "Compliance Check - Policy without criteria. Compliance check not possible";
        private const string NoRelevantManager = "Compliance Check - No relevant managements found. Compliance check not possible.";
        private const string NoViolations = "Compliance Check - Checked compliance for 5 rules and found 0 non-compliant rules";
        private const string BasicSetup = "Compliance Check - Checked compliance for 5 rules and found 4 non-compliant rules";

        // Parameters for test configuration

        private const string ForbiddenServiceUid = "forbidden-service-uid";
        private const string ExpectedViolationDetailsAutoCalcTrue = "Matrix violation: source-uid-rule3 (3.0.0.0-4.0.0.0) (Zone: Auto-calculated Internet Zone) -> destination-uid-rule3 (128.0.0.0-168.0.0.0) (Zone: 128-168 Zone)";
        private const string ExpectedViolationDetailsAutoCalcFalse = "Matrix violation: source-uid-rule3 (3.0.0.0-4.0.0.0) (Zone: Internet/Local) -> destination-uid-rule3 (128.0.0.0-168.0.0.0) (Zone: 128-168 Zone)";

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
            _complianceCheck.NetworkZones = CreateNetworkZones(true, true);
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
                _policy = CreatePolicy(ForbiddenServiceUid);
                _complianceCheck.Policy = _policy;

                _apiConnection.AsSub()
                    .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                    .Returns(_policy); // Policy with criteria

                _apiConnection.AsSub()
                    .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, Arg.Any<object>())
                        .Returns(_complianceCheck.NetworkZones);
            }

            if (createRules)
            {
                if (!setupNoViolations)
                {
                    ComplianceCriterion? matrix = _complianceCheck.Policy!.Criteria
                        .FirstOrDefault(c => c.Content.CriterionType == nameof(CriterionType.Matrix))?.Content;

                    _complianceCheck.NetworkZones = CreateNetworkZones(true, true);                
                }

                _apiConnection.AsSub()
                    .SendQueryAsync<List<Rule>>(RuleQueries.getRulesForSelectedManagements)
                    .Returns(CreateRulesForComplianceCheckTest(setupNoViolations, ForbiddenServiceUid));
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
            Assert.That(_complianceCheck.CurrentViolationsInCheck.Count == 4, "There should be four violations for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
            Assert.That(_complianceCheck.CurrentViolationsInCheck.ElementAt(2).Details == ExpectedViolationDetailsAutoCalcTrue);
        }

        [Test]
        public async Task CheckAll_BasicSetupWithoutAutoCalcZones_CompleteWithLog()
        {
            // Arrange

            _globalConfig.AutoCalculateInternetZone = false;
            _globalConfig.AutoCalculateUndefinedInternalZone = false;
            _globalConfig.TreatDynamicAndDomainObjectsAsInternet = false;

            _complianceCheck.NetworkZones = CreateNetworkZones(false, false);

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            // Act

            await _complianceCheck.CheckAll();

            // Assert

            Assert.That(_globalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(_complianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(_complianceCheck.CurrentViolationsInCheck.Count == 4, "There should be four violations for this test.");
            Assert.That(_logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
            Assert.That(_complianceCheck.CurrentViolationsInCheck.ElementAt(2).Details == ExpectedViolationDetailsAutoCalcFalse);
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
    }
}
