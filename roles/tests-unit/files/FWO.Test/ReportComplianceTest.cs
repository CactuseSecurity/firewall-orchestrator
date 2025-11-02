using FWO.Data;
using FWO.Test.Mocks;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceTest
    {
        private MockReportCompliance _complianceReport => new(new(""), new(), Basics.ReportType.Compliance);
        private MockReportCompliance _testReport = default!;
        private MockReportComplianceDiff _complianceDiffReport => new(new(""), new(){ComplianceCheckMaxPrintedViolations = 2}, Basics.ReportType.ComplianceDiff);
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
        public async Task ProcessChunksParallelized_DiffReport_CreatesCorrectDiffs()
        {
            // ARRANGE
            // not assessable -> only prints not assessable details
            // abbreviated -> abbreviated
            // multiple
            // singular

            CancellationToken ct = default;
            DateTime foundDate = DateTime.Now;

            _testDiffReport.DiffReferenceInDays = 7;

            Rule notAssessable = new()
            {
                Id = 1,
                Name = "Testrule 1",
                Violations = [
                    CreateMockComplianceViolation(1,1, foundDate, criterion:

                        new()
                        {
                            CriterionType = nameof(ComplianceViolationType.NotAssessable)
                        }

                    )
                ]
            };

            Rule abbreviated = new()
            {
                Id = 2,
                Name = "Testrule 2",
                Violations = [
                        CreateMockComplianceViolation(2,2, foundDate),
                        CreateMockComplianceViolation(3,2, foundDate),
                        CreateMockComplianceViolation(4,2, foundDate)
                    ]
            };

            Rule multiple = new()
            {
                Id = 3,
                Name = "Testrule 3",
                Violations = [
                    CreateMockComplianceViolation(5,3, foundDate),
                    CreateMockComplianceViolation(6,3, foundDate)
                ]
            };
            
            Rule singular = new()
            {
                Id = 4,
                Name = "Testrule 4",
                Violations = [
                    CreateMockComplianceViolation(7,4, foundDate, criterion:

                        new()
                        {
                            CriterionType = nameof(ComplianceViolationType.ServiceViolation)
                        }

                    )
                ]
            };

            List<Rule>[] ruleChunks =
            [
                new List<Rule>(){ notAssessable },
                new List<Rule>(){ abbreviated },
                new List<Rule>(){ multiple },
                new List<Rule>(){ singular }
            ];

            string controlNotAssessable = CreateViolationDetailsControlString(foundDate, 1);
            string controlAbbreviated = CreateViolationDetailsControlString(foundDate, 2) + "<br>" + CreateViolationDetailsControlString(foundDate, 3);
            string controlMultiple = CreateViolationDetailsControlString(foundDate, 4) + "<br>" + CreateViolationDetailsControlString(foundDate, 5) + "<br>Too many violations to display (3), please check the system for details.";
            string controlSingular = CreateViolationDetailsControlString(foundDate, 6);

            // ACT

            List<Rule> testResults = await _testDiffReport.ProcessChunksParallelized(ruleChunks, ct, new SimulatedApiConnection());

            // ASSERT

            Assert.That(testResults.Count == 4);
            Assert.That(notAssessable.ViolationDetails == controlNotAssessable && notAssessable.Compliance == ComplianceViolationType.NotAssessable);
            Assert.That(abbreviated.ViolationDetails == controlAbbreviated);
            Assert.That(multiple.ViolationDetails == controlMultiple && multiple.Compliance == ComplianceViolationType.MultipleViolations);
            Assert.That(singular.ViolationDetails == controlSingular && singular.Compliance == ComplianceViolationType.ServiceViolation);
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

        private ComplianceViolation CreateMockComplianceViolation(int id = 0, int ruleId = 0, DateTime? foundDate = null, DateTime? removedDate = null, string details = "", int policyId = 0, ComplianceCriterion? criterion = null, ComplianceViolationType type = ComplianceViolationType.None)
        {
            if (string.IsNullOrEmpty(details))
            {
                details = $"Test violation {id}";
            }

            if (criterion == null)
            {
                criterion = new()
                {
                    Id = 0
                };
            }

            ComplianceViolation violation = new()
            {
                Id = id,
                RuleId = ruleId,
                FoundDate = foundDate ?? DateTime.Now,
                Details = details,
                RiskScore = 0,
                PolicyId = policyId,
                CriterionId = criterion.Id,
                Criterion = criterion
            };

            if (violation.Type == null)
            {
                
            }
        }
        
        private string CreateViolationDetailsControlString(DateTime foundDate, int violationId)
        {
            return $"Found: ({foundDate:dd.MM.yyyy}) - {foundDate:hh:mm}) : Test violation {violationId}";
        }

    }
}