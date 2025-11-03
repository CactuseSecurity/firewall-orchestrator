using FWO.Data;
using FWO.Test.Mocks;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceTest
    {
        private static MockReportCompliance _complianceReport => new(new(""), new(), Basics.ReportType.Compliance);
        private MockReportCompliance _testReport = default!;
        private static MockReportComplianceDiff _complianceDiffReport => new(new(""), new(), Basics.ReportType.ComplianceDiff);
        private MockReportComplianceDiff _testDiffReport = default!;


        [SetUp]
        public void SetUpTest()
        {
            _testReport = _complianceReport;
            _testDiffReport = _complianceDiffReport;
            _testDiffReport.MockPostProcessDiffReportsRule = true;
        }

        [Test]
        public async Task ProcessChunksParallelized_BigDataSet_EvaluatesAllRules()
        {
            // ARRANGE

            CancellationToken ct = default;
            int numberOfChunks = 100;
            int numberOfRulesPerChunk = 100;
            int ruleId = 1;

            List<Rule>[] ruleChunks = BuildFixedRuleChunksParallel(numberOfChunks, numberOfRulesPerChunk, ruleId);

            // ACT

            List<Rule> testResults = await _testReport.ProcessChunksParallelized(ruleChunks, ct, new SimulatedApiConnection());

            // ASSERT

            Assert.That(testResults.Count == _testReport.RuleViewData.Count, $"Rules: {testResults.Count} - RuleViewData: {_testReport.RuleViewData.Count}");

        }

        [Test]
        public async Task ProcessChunksParallelized_MinimalTestData_CreatesCorrectDiffs()
        {
            // ARRANGE

            CancellationToken ct = default;
            List<Rule>[] ruleChunks = new List<Rule>[2];

            _testDiffReport.DiffReferenceInDays = 7;

            Rule rule1 = new()
            {
                Id = 1,
                Name = "Test Rule 1",
                Violations = new List<ComplianceViolation>()
            };

            Rule rule2 = new()
            {
                Id = 2,
                Name = "Test Rule 2",
                Violations = new List<ComplianceViolation>()
            };

            ComplianceViolation unchanged = new()
            {
                Id = 1,
                RuleId = 1,
                FoundDate = DateTime.Now.AddDays(-(_testDiffReport.DiffReferenceInDays + 1)),
                Details = "Test violation 1",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 1
            };

            ComplianceViolation removed = new()
            {
                Id = 2,
                RuleId = 1,
                FoundDate = DateTime.Now.AddDays(-(_testDiffReport.DiffReferenceInDays + 1)),
                RemovedDate = DateTime.Now.AddDays(-1),
                Details = "Test violation 2",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 2
            };

            ComplianceViolation irrelevant = new()
            {
                Id = 3,
                RuleId = 2,
                FoundDate = DateTime.Now.AddDays(-(_testDiffReport.DiffReferenceInDays + 2)),
                RemovedDate = DateTime.Now.AddDays(-(_testDiffReport.DiffReferenceInDays + 1)),
                Details = "Test violation 3",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 2
            };

            ComplianceViolation added = new()
            {
                Id = 4,
                RuleId = 2,
                FoundDate = DateTime.Now.AddDays(-1),
                Details = "Test violation 4",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 3
            };

            rule1.Violations.AddRange([unchanged, removed]);
            rule2.Violations.AddRange([irrelevant, added]);

            ruleChunks[0] = new List<Rule> { rule1 };
            ruleChunks[1] = new List<Rule> { rule2 };

            string controlRule1 = $"Removed: ({removed.RemovedDate:dd.MM.yyyy} - {removed.RemovedDate:hh:mm}) : Test violation 2";
            string controlRule2 = $"Found: ({added.FoundDate:dd.MM.yyyy} - {added.FoundDate:hh:mm}) : Test violation 4";

            // ACT

            List<Rule> testResults = await _testDiffReport.ProcessChunksParallelized(ruleChunks, ct, new SimulatedApiConnection());

            // ASSERT

            Assert.That(testResults.First(r => r.Id == rule1.Id).ViolationDetails == controlRule1, message: $"{testResults.First(r => r.Id == rule1.Id).ViolationDetails} VS. {controlRule1}");
            Assert.That(testResults.First(r => r.Id == rule2.Id).ViolationDetails == controlRule2, message: $"{testResults.First(r => r.Id == rule2.Id).ViolationDetails} VS. {controlRule2}");
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
                        list.Add(new Rule { Id = baseId + j });
                    }

                    ruleChunks[i] = list;
                });

            return ruleChunks;
        }

    }
}
