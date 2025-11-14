using FWO.Basics;
using FWO.Compliance;
using FWO.Config.Api;
using FWO.Data;
using FWO.Test.Mocks;
using NetTools;
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

        [SetUp]
        public void SetUpTest()
        {
            _globalConfig = new SimulatedGlobalConfig { AutoCalculateInternetZone = true, AutoCalculateUndefinedInternalZone = true, TreatDynamicAndDomainObjectsAsInternet = true };
            _userConfig = new UserConfig(_globalConfig, false);
            _complianceCheck = new ComplianceCheck(_userConfig, new SimulatedApiConnection());
            _complianceCheck.Policy = CreatePolicy();
            _complianceCheck.NetworkZones = CreateNetworkZones(50);
            _complianceCheck.ComplianceReport = new(new(""), _userConfig, ReportType.ComplianceReport);
            LocalSettings.ComplianceCheckVerbose = true;
        }

        #endregion

        #region Tests - CheckAll

        [Test]
        public async Task CheckAll_PolicyIdZero_AbortCheckWithLog()
        {
            // Arrange



            // Act

            await _complianceCheck.CheckAll();

            // Assert

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

        private Rule CreateRule(int ruleID)
        {
            Rule rule = new Rule
            {
                Id = ruleID,
                Action = "accept"
            };

            List<ServiceWrapper> services = new();
            List<NetworkLocation> froms = new();
            List<NetworkLocation> tos = new();

            for (int i = 0; i < 5; i++)
            {
                ServiceWrapper service = new();
                NetworkUser user = new();
                NetworkObject networkObject = new();
                networkObject.IP = "0.0.0.0/32";
                networkObject.IpEnd = "255.255.255.255/32";
                NetworkLocation networkLocation = new(user, networkObject);
                froms.Add(networkLocation);
                tos.Add(networkLocation);
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
