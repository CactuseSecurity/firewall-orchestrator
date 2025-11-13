using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Test.Mocks;
using NetTools;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceCheckTest
    {
        private ComplianceCheck _complianceCheck = default!;
        private TimeSpan _maxAcceptableExecutionTime = TimeSpan.FromSeconds(60);
        private List<Rule>[] _ruleChunks = default!;

        [SetUp]
        public void SetUpTest()
        {
            _complianceCheck = new ComplianceCheck(new(), new SimulatedApiConnection());

            CompliancePolicy policy = new();
            ComplianceCriterionWrapper serviceCriterion = new();
            serviceCriterion.Content.CriterionType = nameof(CriterionType.ForbiddenService);
            ComplianceCriterionWrapper matrixCriterion = new();
            matrixCriterion.Content.CriterionType = nameof(CriterionType.Matrix);
            ComplianceCriterionWrapper assessabilityCriterion = new();
            assessabilityCriterion.Content.CriterionType = nameof(CriterionType.Assessability);
            policy.Criteria.AddRange([serviceCriterion, matrixCriterion, assessabilityCriterion]);
            _complianceCheck.Policy = policy;

            for (int i = 0; i < 50; i++)
            {
                ComplianceNetworkZone networkZone = new();
                _complianceCheck.NetworkZones.Add(networkZone);
            }

            MockReportCompliance complianceReport = new(new(""), new(), Basics.ReportType.ComplianceReport);

            _complianceCheck.ComplianceReport = complianceReport;
        }

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
    }
}
