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

        // Expected logs

        private const string PolicyIdZero = "Compliance Check - No Policy defined. Compliance check not possible.";
        private const string PolicyNull = "Compliance Check - Policy with id 1 not found.";
        private const string NoCriteria = "Compliance Check - Policy without criteria. Compliance check not possible";
        private const string NoRelevantManager = "Compliance Check - No relevant managements found. Compliance check not possible.";
        private const string NoViolationsA = "Compliance Check - Loaded 5 rules";
        private const string NoViolationsB = "Compliance Check - Found 0 violations.";
        private const string BasicSetup = "Compliance Check - Checked compliance for 5 rules and found 4 non-compliant rules";

        // Parameters for test configuration

        private const string ExpectedViolationDetailsAutoCalcTrue = "Matrix violation: source-uid-rule3 (3.0.0.0-4.0.0.0) (Zone: Auto-calculated Internet Zone) -> destination-uid-rule3 (128.0.0.0-168.0.0.0) (Zone: 128-168 Zone)";
        private const string ExpectedViolationDetailsAutoCalcFalse = "Matrix violation: source-uid-rule3 (3.0.0.0-4.0.0.0) (Zone: Internet/Local) -> destination-uid-rule3 (128.0.0.0-168.0.0.0) (Zone: 128-168 Zone)";

        [SetUp]
        public override void SetUpTest()
        {
            base.SetUpTest();
        }

        #endregion

        #region Tests - CheckAll

        [Test]
        public async Task CheckAll_PolicyIdZero_AbortCheckWithLog()
        {
            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId == 0, "Default policy ID should be zero for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(PolicyIdZero)), "Expected log message not found.");
        }

        [Test]
        public async Task CheckAll_PolicyNull_AbortCheckWithLog()
        {
            // Arrange

            await SetUpBasic();

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy == null, "Policy should be null for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(PolicyNull)), "Expected log message not found.");
        }

        [Test]
        public async Task CheckAll_NoRelevantManager_AbortWithLog()
        {
            // Arrange

            await SetUpBasic(createEmptyPolicy: true);

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoRelevantManager)), "Unexpected violations.");
        }

        [Test]
        public async Task CheckAll_NoCriteria_AbortCheckWithLog()
        {
            // Arrange

            await SetUpBasic(createEmptyPolicy: true, setupRelevantManagements: true);

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoCriteria)), "Expected log message not found.");
        }



        [Test]
        public async Task CheckAll_NoViolations_CompleteWithLog()
        {
            // Arrange

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true, setupNoViolations: true);

            AggregateCount count = new AggregateCount();
            count.Aggregate.Count = ComplianceCheck.RulesInCheck!.Count;
            ApiConnection
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countRules)
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(ComplianceCheck.RulesInCheck.Count == 5, "There should be 5 rules in check");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Count == 0, "There should be no violations for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoViolationsA)), "Unexpected violations.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoViolationsB)), "Unexpected violations.");
        }

        [Test]
        public async Task CheckAll_BasicSetup_CompleteWithLog()
        {
            // Arrange

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            AggregateCount count = new AggregateCount();
            count.Aggregate.Count = ComplianceCheck.RulesInCheck!.Count;
            ApiConnection
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countRules)
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Count == 4, "There should be four violations for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.Details == ExpectedViolationDetailsAutoCalcTrue));
        }

        [Test]
        public async Task CheckAll_BasicSetupWithoutAutoCalcZones_CompleteWithLog()
        {
            // Arrange

            GlobalConfig.AutoCalculateInternetZone = false;
            GlobalConfig.AutoCalculateUndefinedInternalZone = false;
            GlobalConfig.TreatDynamicAndDomainObjectsAsInternet = false;

            ComplianceCheck.NetworkZones = CreateNetworkZones(false, false);

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            AggregateCount count = new AggregateCount();
            count.Aggregate.Count = ComplianceCheck.RulesInCheck!.Count;
            ApiConnection
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countRules)
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.CheckAll();

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Count == 4, "There should be four violations for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.Details == ExpectedViolationDetailsAutoCalcFalse));
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
            await  SetUpBasic(createPolicy: true);

            List<ComplianceCriterion> criteria = Policy!.Criteria.Select(c => c.Content).ToList();

            RuleChunks = BuildFixedRuleChunksParallel(numberOfChunks, numberOfRulesPerChunk, ruleId);

            // Act

            DateTime executionStart = DateTime.Now;
            foreach (var chunk in RuleChunks)
            {
                foreach (var rule in chunk)
                {
                    await ComplianceCheck.CheckRuleCompliance(rule, criteria);
                }
            }
            DateTime executionEnd = DateTime.Now;
            TimeSpan executionTime = executionEnd - executionStart;

            // Assert

            Assert.That(executionTime < MaxAcceptableExecutionTime, $"Execution time was {executionTime.Seconds} s, expected: {MaxAcceptableExecutionTime.Seconds} s.");
        }

        #endregion

        #region Tests - ParseIpRange

        [Test]
        public void ParseIpRange_NwObjectOfTypeIpRange_AddedToReturnedList()
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
        public void ParseIpRange_NwObjectOfTypeIpRangeWithSubnetSuffix_AddedToReturnedList()
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
