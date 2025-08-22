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
        public async Task GetViolationDiffs_MinimalTestData_CreatesCorrectDiffs()
        {
            // ARRANGE

            _testReport.DiffReferenceInDays = 7;

            Dictionary<ComplianceViolation, char> violationDiffs = new();

            List<ComplianceViolation> allViolations = new();

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
                RuleId = 1,
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
                RuleId = 1,
                FoundDate = DateTime.Now.AddDays(-1),
                Details = "Test violation 4",
                RiskScore = 0,
                PolicyId = 1,
                CriterionId = 3
            };

            allViolations.AddRange([unchanged, removed, irrelevant, added]);

            _testReport.Violations.AddRange([unchanged, added]);

            // ACT

            // violationDiffs = await _testReport.GetViolationDiffs(allViolations);

            // ASSERT

            Assert.That(!violationDiffs.Keys.Contains(unchanged));
            Assert.That(violationDiffs[removed] == '-');
            Assert.That(!violationDiffs.Keys.Contains(irrelevant));
            Assert.That(violationDiffs[added] == '+');

        }


    }

}