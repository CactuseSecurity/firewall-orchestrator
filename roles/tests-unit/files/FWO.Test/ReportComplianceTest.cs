using FWO.Data;
using FWO.Report;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceTest
    {
        private ReportCompliance _complianceReport => new(new(""), new(), Basics.ReportType.Compliance);
        private ReportCompliance _testReport = default!;

        [SetUp]
        public void SetUpTest()
        {
            _testReport = _complianceReport;
        }

        [Test, Ignore("temporarily disabled for importer-rework")]
        public async Task ProcessChunksParallelized_MinimalTestData_CreatesCorrectDiffs()
        {
            // ARRANGE

            CancellationToken ct = default;
            List<Rule>[] ruleChunks = new List<Rule>[2];

            _testReport.DiffReferenceInDays = 7;
            _testReport.IsDiffReport = true;

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
                FoundDate = DateTime.Now.AddDays(-(_testReport.DiffReferenceInDays + 1)),
                Details = "Test violation 1",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 1
            };

            ComplianceViolation removed = new()
            {
                Id = 2,
                RuleId = 1,
                FoundDate = DateTime.Now.AddDays(-(_testReport.DiffReferenceInDays + 1)),
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
                FoundDate = DateTime.Now.AddDays(-(_testReport.DiffReferenceInDays + 2)),
                RemovedDate = DateTime.Now.AddDays(-(_testReport.DiffReferenceInDays + 1)),
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

            // List<Rule> testResults = await _testReport.ProcessChunksParallelized(ruleChunks, ct);

            // ASSERT

            // Assert.That(testResults.First(r => r.Id == rule1.Id).ViolationDetails == controlRule1, message: $"{testResults.First(r => r.Id == rule1.Id).ViolationDetails} VS. {controlRule1}");
            // Assert.That(testResults.First(r => r.Id == rule2.Id).ViolationDetails == controlRule2 , message: $"{testResults.First(r => r.Id == rule2.Id).ViolationDetails} VS. {controlRule2}");
        }
    }
}