using FWO.Config.Api;
using FWO.Data;
using FWO.Report.Data.ViewData;
using FWO.Test.Mocks;
using FWO.Api.Client.Queries;
using NUnit.Framework;
using static FWO.Test.ComplianceReportTestData;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceTest
    {
        private MockReportCompliance _complianceReport => new(new(""), new(), Basics.ReportType.ComplianceReport);
        private MockReportCompliance _testReport = default!;

        [SetUp]
        public void SetUpTest()
        {
            _testReport = _complianceReport;
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

            List<Rule> testResults = await _testReport.ProcessChunksParallelized(ruleChunks, ct);

            // ASSERT

            Assert.That(testResults.Count == _testReport.RuleViewData.Count, $"Rules: {testResults.Count} - RuleViewData: {_testReport.RuleViewData.Count}");

        }

        [Test]
        public async Task Generate_UsesActiveRuleCountForChunkPaging()
        {
            ActiveRuleCountApiConnection apiConnection = new();

            await _testReport.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.countActiveRules));
                Assert.That(apiConnection.Queries, Does.Not.Contain(RuleQueries.countRules));
            });
        }

        [Test]
        public void CreateQueryVariables_UsesConfiguredRelevantManagementIds()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceCheckRelevantManagements = "9,10"
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportCompliance report = new(new(""), userConfig, Basics.ReportType.ComplianceReport);

            Dictionary<string, object> queryVariables = report.CreateQueryVariablesPublic(0, 100, RuleQueries.getRulesWithCurrentViolationsByChunk);

            Assert.That(queryVariables.ContainsKey("mgm_ids"), Is.True);
            Assert.That((List<int>)queryVariables["mgm_ids"], Is.EqualTo(new List<int> { 9, 10 }));
        }

        [Test]
        public void CreateQueryVariables_UsesLoadedManagementIdsWhenNoConfiguredFilter()
        {
            MockReportCompliance report = new(new(""), new(), Basics.ReportType.ComplianceReport)
            {
                Managements =
                [
                    new Management { Id = 3 },
                    new Management { Id = 4 }
                ]
            };

            Dictionary<string, object> queryVariables = report.CreateQueryVariablesPublic(0, 100, RuleQueries.getRulesWithCurrentViolationsByChunk);

            Assert.That(queryVariables.ContainsKey("mgm_ids"), Is.True);
            Assert.That((List<int>)queryVariables["mgm_ids"], Is.EqualTo(new List<int> { 3, 4 }));
        }

        [Test]
        public void DetermineCompliance_ReportsNotAssessableOnlyWhenNoRealViolationRemains()
        {
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(new SimulatedGlobalConfig()), Basics.ReportType.ComplianceReport);

            Assert.Multiple(() =>
            {
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations()),
                    Is.EqualTo(ComplianceViolationType.None));
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(ComplianceViolationType.MatrixViolation)),
                    Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.MatrixViolation,
                        ComplianceViolationType.ServiceViolation)),
                    Is.EqualTo(ComplianceViolationType.MultipleViolations));

                // Every criterion records assessability issues per object, so a single one of them must not
                // outrank the violations the other objects of the same rule did produce.

                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.MatrixViolation,
                        ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.MultipleViolations));
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.NotAssessable,
                        ComplianceViolationType.MinimumCIDRLengthViolation)),
                    Is.EqualTo(ComplianceViolationType.MultipleViolations));

                // Several assessability issues without any real violation must read as not assessable,
                // never as multiple violations.

                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.NotAssessable,
                        ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.NotAssessable));
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.NotAssessable));
            });
        }

        [Test]
        public void SetComplianceDataForRule_RetainsRealViolationAlongsidePartialAssessabilityIssue()
        {
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(new SimulatedGlobalConfig()), Basics.ReportType.ComplianceReport);
            ComplianceViolation matrixViolation = CreateMockComplianceViolation(1, 1, DateTime.Now, type: ComplianceViolationType.MatrixViolation);
            matrixViolation.Details = "Matrix violation";
            ComplianceViolation partialAssessabilityIssue = CreateMockComplianceViolation(2, 1, DateTime.Now, criterion: new()
            {
                CriterionType = nameof(CriterionType.Matrix)
            }, type: ComplianceViolationType.NotAssessable);
            partialAssessabilityIssue.Details = "Object has no matching zone";
            Rule rule = new()
            {
                Violations = [matrixViolation, partialAssessabilityIssue]
            };

            report.SetComplianceDataForRulePublic(rule);

            Assert.Multiple(() =>
            {
                Assert.That(rule.Compliance, Is.EqualTo(ComplianceViolationType.MultipleViolations));
                Assert.That(rule.ViolationDetails, Does.Contain(matrixViolation.Details));
                Assert.That(rule.ViolationDetails, Does.Contain(partialAssessabilityIssue.Details));
            });
        }

        [Test]
        public void SetComplianceDataForRule_SeveralAssessabilityCriterionIssuesStayNotAssessable()
        {
            // A rule whose only violations are assessability issues stays not assessable, however many objects
            // reported one and whichever criterion recorded them.
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(new SimulatedGlobalConfig()), Basics.ReportType.ComplianceReport);
            ComplianceCriterion assessabilityCriterion = new() { CriterionType = nameof(CriterionType.Assessability) };
            ComplianceViolation firstIssue = CreateMockComplianceViolation(1, 1, DateTime.Now, criterion: assessabilityCriterion, type: ComplianceViolationType.NotAssessable);
            ComplianceViolation secondIssue = CreateMockComplianceViolation(2, 1, DateTime.Now, criterion: assessabilityCriterion, type: ComplianceViolationType.NotAssessable);
            Rule rule = new()
            {
                Violations = [firstIssue, secondIssue]
            };

            report.SetComplianceDataForRulePublic(rule);

            Assert.That(rule.Compliance, Is.EqualTo(ComplianceViolationType.NotAssessable));
        }

        [Test]
        public void SetComplianceDataForRule_RetainsRealViolationAlongsideAssessabilityCriterionIssue()
        {
            // The Assessability criterion also records one issue per object, so an unassessable object of an
            // otherwise assessable rule must not hide the violation found on its remaining objects.
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(new SimulatedGlobalConfig()), Basics.ReportType.ComplianceReport);
            ComplianceViolation matrixViolation = CreateMockComplianceViolation(1, 1, DateTime.Now, type: ComplianceViolationType.MatrixViolation);
            matrixViolation.Details = "Matrix violation";
            ComplianceViolation ruleAssessabilityIssue = CreateMockComplianceViolation(2, 1, DateTime.Now, criterion: new()
            {
                CriterionType = nameof(CriterionType.Assessability)
            }, type: ComplianceViolationType.NotAssessable);
            ruleAssessabilityIssue.Details = "Object without address";
            Rule rule = new()
            {
                Violations = [matrixViolation, ruleAssessabilityIssue]
            };

            report.SetComplianceDataForRulePublic(rule);

            Assert.Multiple(() =>
            {
                Assert.That(rule.Compliance, Is.EqualTo(ComplianceViolationType.MultipleViolations));
                Assert.That(rule.ViolationDetails, Does.Contain(ruleAssessabilityIssue.Details));
                Assert.That(rule.ViolationDetails, Does.Contain(matrixViolation.Details));
            });
        }

        [Test]
        public void GetViewDataFromRules_SeveralAssessabilityCriterionIssuesStayNotAssessable()
        {
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(new SimulatedGlobalConfig()), Basics.ReportType.ComplianceReport);
            ComplianceCriterion assessabilityCriterion = new() { CriterionType = nameof(CriterionType.Assessability) };
            ComplianceViolation firstIssue = CreateMockComplianceViolation(1, 1, DateTime.Now, criterion: assessabilityCriterion, type: ComplianceViolationType.NotAssessable);
            ComplianceViolation secondIssue = CreateMockComplianceViolation(2, 1, DateTime.Now, criterion: assessabilityCriterion, type: ComplianceViolationType.NotAssessable);
            Rule rule = new()
            {
                Violations = [firstIssue, secondIssue]
            };
            List<Rule> rules = [rule];

            report.GetViewDataFromRules(rules);

            Assert.That(rule.Compliance, Is.EqualTo(ComplianceViolationType.NotAssessable));
        }

        [Test]
        public void DetermineCompliance_PrintedViolationLimitKeepsRealViolationDecisive()
        {
            // With a limit of one printed violation the state describes that single violation, so it must be the
            // real one whichever position the API returned it in - otherwise the list order decides whether the
            // rule reads as not assessable and its real violation disappears from the report.
            SimulatedGlobalConfig singleViolationConfig = new()
            {
                ComplianceCheckMaxPrintedViolations = 1
            };
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(singleViolationConfig), Basics.ReportType.ComplianceReport);
            List<ComplianceViolation> assessabilityIssueFirst = CreateTypedViolations(
                ComplianceViolationType.NotAssessable,
                ComplianceViolationType.MatrixViolation);
            List<ComplianceViolation> realViolationFirst = CreateTypedViolations(
                ComplianceViolationType.MatrixViolation,
                ComplianceViolationType.NotAssessable);

            Assert.Multiple(() =>
            {
                Assert.That(
                    report.DetermineCompliancePublic(assessabilityIssueFirst),
                    Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(
                    report.DetermineCompliancePublic(realViolationFirst),
                    Is.EqualTo(ComplianceViolationType.MatrixViolation));

                // A rule without any real violation still reads as not assessable under the same limit.

                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.NotAssessable,
                        ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.NotAssessable));
            });
        }

        [Test]
        public void SetComplianceDataForRule_PrintsRealViolationBeforeAssessabilityIssue()
        {
            SimulatedGlobalConfig singleViolationConfig = new()
            {
                ComplianceCheckMaxPrintedViolations = 1
            };
            MockReportCompliance report = new(new(""), UserConfig.ForTextOnly(singleViolationConfig), Basics.ReportType.ComplianceReport);
            ComplianceViolation assessabilityIssue = CreateMockComplianceViolation(1, 1, DateTime.Now, criterion: new()
            {
                CriterionType = nameof(CriterionType.Assessability)
            }, type: ComplianceViolationType.NotAssessable);
            assessabilityIssue.Details = "Object without address";
            ComplianceViolation matrixViolation = CreateMockComplianceViolation(2, 1, DateTime.Now, type: ComplianceViolationType.MatrixViolation);
            matrixViolation.Details = "Matrix violation";
            Rule rule = new()
            {
                Violations = [assessabilityIssue, matrixViolation]
            };

            report.SetComplianceDataForRulePublic(rule);

            Assert.Multiple(() =>
            {
                Assert.That(rule.Compliance, Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(rule.ViolationDetails, Does.Contain(matrixViolation.Details));
                Assert.That(rule.ViolationDetails, Does.Not.Contain(assessabilityIssue.Details));
                Assert.That(rule.ViolationDetails, Does.Contain("Too many violations to display (2)"));
            });
        }

        [Test]
        public void DetermineCompliance_CountsOnlyViolationsWithinThePrintedViolationLimit()
        {
            SimulatedGlobalConfig singleViolationConfig = new()
            {
                ComplianceCheckMaxPrintedViolations = 1
            };
            SimulatedGlobalConfig twoViolationConfig = new()
            {
                ComplianceCheckMaxPrintedViolations = 2
            };
            MockReportCompliance singleViolationReport = new(new(""), UserConfig.ForTextOnly(singleViolationConfig), Basics.ReportType.ComplianceReport);
            MockReportCompliance twoViolationReport = new(new(""), UserConfig.ForTextOnly(twoViolationConfig), Basics.ReportType.ComplianceReport);
            List<ComplianceViolation> violations = CreateTypedViolations(
                ComplianceViolationType.MatrixViolation,
                ComplianceViolationType.ServiceViolation,
                ComplianceViolationType.MatrixViolation);

            Assert.Multiple(() =>
            {
                Assert.That(
                    singleViolationReport.DetermineCompliancePublic(violations),
                    Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(
                    twoViolationReport.DetermineCompliancePublic(violations),
                    Is.EqualTo(ComplianceViolationType.MultipleViolations));
            });
        }

        [Test]
        public void ExportToCsv_IncludesExpirationTimeColumnAndValue()
        {
            MockReportCompliance report = new(new(""), new(), Basics.ReportType.ComplianceReport);
            report.RuleViewData =
            [
                new RuleViewData
                {
                    MgmtId = "1",
                    MgmtName = "Mgmt",
                    Uid = "uid-1",
                    Name = "Rule 1",
                    Source = "src",
                    SourceShort = "src-short",
                    Destination = "dst",
                    DestinationShort = "dst-short",
                    Services = "svc",
                    ServicesShort = "svc-short",
                    Action = "accept",
                    InstallOn = "fw1",
                    Compliance = "FALSE",
                    ViolationDetails = "detail",
                    ChangeID = "chg-1",
                    AdoITID = "ado-1",
                    Comment = "comment",
                    LastModified = "2026-03-24",
                    ExpirationTime = "2026-12-24 11:22:33",
                    RulebaseId = "7",
                    RulebaseName = "rb",
                    Enabled = "TRUE",
                    Show = true
                }
            ];

            string csv = report.ExportToCsv();

            Assert.That(csv, Does.Contain("\"ExpirationTime\""));
            Assert.That(csv, Does.Contain("\"2026-12-24 11:22:33\""));
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

        private sealed class ActiveRuleCountApiConnection : SimulatedApiConnection
        {
            public List<string> Queries { get; } = new();

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == DeviceQueries.getManagementNames && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)new List<Management>());
                }

                if (query == RuleQueries.countActiveRules && typeof(QueryResponseType) == typeof(AggregateCount))
                {
                    return Task.FromResult((QueryResponseType)(object)new AggregateCount());
                }

                throw new NotSupportedException($"Unexpected query: {query}");
            }
        }

    }
}
