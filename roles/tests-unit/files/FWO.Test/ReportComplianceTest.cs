using FWO.Config.Api;
using FWO.Data;
using FWO.Report.Data.ViewData;
using FWO.Test.Mocks;
using FWO.Api.Client.Queries;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceTest
    {
        private MockReportCompliance _complianceReport => new(new(""), new(), Basics.ReportType.ComplianceReport);
        private MockReportCompliance _testReport = default!;
        private MockReportComplianceDiff _testDiffReport = default!;


        [SetUp]
        public void SetUpTest()
        {
            _testReport = _complianceReport;
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.ComplianceCheckMaxPrintedViolations = 2;
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            _testDiffReport = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport);
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
        public async Task ProcessChunksParallelized_DiffReport_CreatesCorrectDiffs()
        {
            // ARRANGE

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
                        },
                        type: ComplianceViolationType.NotAssessable

                    ),
                    CreateMockComplianceViolation(2,2, foundDate, type: ComplianceViolationType.MatrixViolation)
                ]
            };

            Rule abbreviated = new()
            {
                Id = 2,
                Name = "Testrule 2",
                Violations = [
                        CreateMockComplianceViolation(3,2, foundDate, type: ComplianceViolationType.MatrixViolation),
                        CreateMockComplianceViolation(4,2, foundDate, type: ComplianceViolationType.MatrixViolation),
                        CreateMockComplianceViolation(5,2, foundDate, type: ComplianceViolationType.MatrixViolation)
                    ]
            };

            Rule multiple = new()
            {
                Id = 3,
                Name = "Testrule 3",
                Violations = [
                    CreateMockComplianceViolation(6,3, foundDate, type: ComplianceViolationType.MatrixViolation),
                    CreateMockComplianceViolation(7,3, foundDate, type: ComplianceViolationType.ServiceViolation)
                ]
            };

            Rule singular = new()
            {
                Id = 4,
                Name = "Testrule 4",
                Violations = [
                    CreateMockComplianceViolation(8,4, foundDate, criterion:

                        new()
                        {
                            CriterionType = nameof(ComplianceViolationType.ServiceViolation)
                        },
                        type: ComplianceViolationType.ServiceViolation

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
            string controlAbbreviated = CreateViolationDetailsControlString(foundDate, 3) + "<br>" + CreateViolationDetailsControlString(foundDate, 4) + "<br>Too many violations to display (3), please check the system for details.";
            string controlMultiple = CreateViolationDetailsControlString(foundDate, 6) + "<br>" + CreateViolationDetailsControlString(foundDate, 7);
            string controlSingular = CreateViolationDetailsControlString(foundDate, 8);

            // ACT

            List<Rule> testResults = await _testDiffReport.ProcessChunksParallelized(ruleChunks, ct);

            // ASSERT

            Assert.That(testResults.Count == 4);
            Assert.That(notAssessable.ViolationDetails == controlNotAssessable);
            Assert.That(notAssessable.Compliance == ComplianceViolationType.NotAssessable);
            Assert.That(abbreviated.ViolationDetails == controlAbbreviated);
            Assert.That(multiple.ViolationDetails == controlMultiple);
            Assert.That(multiple.Compliance == ComplianceViolationType.MultipleViolations);
            Assert.That(singular.ViolationDetails == controlSingular);
            Assert.That(singular.Compliance == ComplianceViolationType.ServiceViolation);
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
        public async Task Generate_DiffReportFetchesViolationsBeforeRulesAndAttachesThem()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = false
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a"),
                CreateDiffViolation(2, 102, "rule-a"),
                CreateDiffViolation(3, 103, "rule-b", DateTime.Now.AddHours(-1))
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations);

            await report.Generate(2, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            int countQueryIndex = apiConnection.Queries.IndexOf(ComplianceQueries.countComplianceDiffViolations);
            int violationQueryIndex = apiConnection.Queries.IndexOf(ComplianceQueries.getComplianceDiffViolationsByChunk);
            int ruleQueryIndex = apiConnection.Queries.IndexOf(RuleQueries.getActiveRulesByUids);
            Assert.Multiple(() =>
            {
                Assert.That(countQueryIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(violationQueryIndex, Is.GreaterThan(countQueryIndex));
                Assert.That(ruleQueryIndex, Is.GreaterThan(violationQueryIndex));
                Assert.That(apiConnection.Queries, Does.Not.Contain(RuleQueries.countActiveRules));
                Assert.That(apiConnection.Queries, Does.Not.Contain(ComplianceQueries.getActiveViolationsBeforeDate));
                Assert.That(apiConnection.RequestedViolationOffsets, Is.EqualTo(new List<int> { 0, 2 }));
                Assert.That(report.Rules.Select(rule => rule.Uid), Is.EqualTo(new List<string?> { "rule-a", "rule-b" }));
                Assert.That(report.Rules.Single(rule => rule.Uid == "rule-a").Violations, Has.Count.EqualTo(2));
                Assert.That(apiConnection.IntervalViolationsWhere, Does.Not.ContainKey("removed_date"));
            });
        }

        [Test]
        public async Task Generate_DiffReportWithNonImpactRulesFetchesEveryActiveRule()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = false
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a")
            };
            List<Rule> activeRules = new()
            {
                CreateActiveRule("rule-a", CreateDiffViolation(11, 101, "rule-a")),
                CreateActiveRule("rule-b", CreateDiffViolation(12, 102, "rule-b"))
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, activeRules: activeRules);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.countActiveRules));
                Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.getRulesWithCurrentViolationsByChunk));
                Assert.That(apiConnection.Queries, Does.Not.Contain(RuleQueries.getActiveRulesByUids));
                Assert.That(report.Rules.Select(rule => rule.Uid), Is.EqualTo(new List<string?> { "rule-a", "rule-b" }));
                Assert.That(report.Rules.Single(rule => rule.Uid == "rule-a").Violations.Select(violation => violation.Id), Is.EqualTo(new List<int> { 1 }));
                Assert.That(report.Rules.Single(rule => rule.Uid == "rule-b").Violations, Is.Empty);
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-b").ViolationDetails,
                    Is.EqualTo(userConfig.GetText("no_changes_found")));
            });
        }

        [Test]
        public async Task Generate_DiffReportWithNonImpactRulesReturnsRulesForEmptyInterval()
        {
            MockReportComplianceDiff report = new(new(""), new(), Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<Rule> activeRules = new()
            {
                CreateActiveRule("rule-a")
            };
            DiffPipelineApiConnection apiConnection = new(new List<ComplianceViolation>(), activeRules: activeRules);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.countActiveRules));
                Assert.That(apiConnection.Queries, Does.Contain(RuleQueries.getRulesWithCurrentViolationsByChunk));
                Assert.That(report.Rules.Select(rule => rule.Uid), Is.EqualTo(new List<string?> { "rule-a" }));
                Assert.That(report.Rules.Single().Violations, Is.Empty);
            });
        }

        [Test]
        public async Task Generate_DiffReportFiltersPreviouslyNonCompliantRulesBeforeFetchingRules()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a"),
                CreateDiffViolation(2, 102, "rule-b"),
                CreateDiffViolation(3, 103, "rule-c")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-a", foundDate: DateTime.Now.AddDays(-8)),
                CreateDiffViolation(12, 12, "rule-c", foundDate: DateTime.Now.AddDays(-8))
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Dictionary<string, object> previousWhere = apiConnection.PreviousViolationsWhere!;
            Dictionary<string, object> foundDate = (Dictionary<string, object>)previousWhere["found_date"];
            List<Dictionary<string, object>> removalStates = (List<Dictionary<string, object>>)previousWhere["_or"];
            Dictionary<string, object> activeRemovalDate = (Dictionary<string, object>)removalStates[0]["removed_date"];
            Dictionary<string, object> laterRemovalDate = (Dictionary<string, object>)removalStates[1]["removed_date"];
            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.Queries.Count(query => query == ComplianceQueries.getActiveViolationsBeforeDate), Is.EqualTo(1));
                Assert.That(foundDate["_lt"], Is.TypeOf<DateTime>());
                Assert.That(removalStates, Has.Count.EqualTo(2));
                Assert.That(activeRemovalDate["_is_null"], Is.EqualTo(true));
                Assert.That(laterRemovalDate["_gte"], Is.TypeOf<DateTime>());
                Assert.That(apiConnection.RequestedRuleUids, Is.EqualTo(new List<string> { "rule-b" }));
                Assert.That(report.Rules.Select(rule => rule.Uid), Is.EqualTo(new List<string?> { "rule-b" }));
            });
        }

        [Test]
        public async Task Generate_DiffReportTreatsInitialViolationsAsPreviousWhenExcludedFromOutput()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true,
                ComplianceFilterOutInitialViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-a", foundDate: DateTime.Now.AddDays(-8), isInitial: true)
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Dictionary<string, object> initialViolationsFilter =
                (Dictionary<string, object>)apiConnection.IntervalViolationsWhere!["is_initial"];
            Assert.Multiple(() =>
            {
                Assert.That(initialViolationsFilter["_eq"], Is.EqualTo(false));
                Assert.That(apiConnection.PreviousViolationsWhere!.ContainsKey("is_initial"), Is.False);
                Assert.That(report.Rules, Is.Empty);
            });
        }

        [Test]
        public async Task Generate_DiffReportWarnsWhenExistingViolationFilterFails()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a")
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, failPreviousViolationFetch: true);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(report.Rules.Select(rule => rule.Uid), Is.EqualTo(new List<string?> { "rule-a" }));
                Assert.That(report.SetDescription(), Does.Contain("Existing-violation filter could not be applied"));
                Assert.That(report.ReportData.ExistingViolationsFilterFailed, Is.True);
            });
        }

        [Test]
        public async Task Generate_DiffReportLabelsPreviouslyNonCompliantRuleDistinctlyFromUnchangedRule()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a"),
                CreateDiffViolation(2, 102, "rule-b"),
                CreateDiffViolation(3, 103, "rule-d"),
                CreateDiffViolation(4, 104, "rule-e")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-a", foundDate: DateTime.Now.AddDays(-8)),
                CreateDiffViolation(13, 13, "rule-d", foundDate: DateTime.Now.AddDays(-8)),
                CreateDiffViolation(14, 14, "rule-e", foundDate: DateTime.Now.AddDays(-8))
            };
            ComplianceViolation notAssessableViolation = CreateDiffViolation(15, 104, "rule-e");
            notAssessableViolation.Type = ComplianceViolationType.NotAssessable;
            List<Rule> activeRules = new()
            {
                CreateActiveRule("rule-a", CreateDiffViolation(12, 101, "rule-a")),
                CreateActiveRule("rule-b"),
                CreateActiveRule("rule-c"),
                CreateActiveRule("rule-d"),
                CreateActiveRule("rule-e", notAssessableViolation)
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations, activeRules);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-a").ViolationDetails,
                    Is.EqualTo(userConfig.GetText("existing_violation_hidden_by_filter")));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-a").Compliance,
                    Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(
                    report.RuleViewData.Single(rule => rule.Uid == "rule-a").Compliance,
                    Is.EqualTo("FALSE"));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-c").ViolationDetails,
                    Is.EqualTo(userConfig.GetText("no_changes_found")));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-d").ViolationDetails,
                    Is.EqualTo(userConfig.GetText("no_changes_found")));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-d").Compliance,
                    Is.EqualTo(ComplianceViolationType.None));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-e").ViolationDetails,
                    Is.EqualTo(userConfig.GetText("existing_violation_hidden_by_filter_not_assessable")));
                Assert.That(
                    report.Rules.Single(rule => rule.Uid == "rule-e").Compliance,
                    Is.EqualTo(ComplianceViolationType.NotAssessable));
                Assert.That(
                    report.RuleViewData.Single(rule => rule.Uid == "rule-e").Compliance,
                    Is.EqualTo("NOT ASSESSABLE"));
                Assert.That(report.Rules.Single(rule => rule.Uid == "rule-b").Violations.Select(violation => violation.Id), Is.EqualTo(new List<int> { 2 }));
            });
        }

        [Test]
        public void DetermineCompliance_AppliesAssessabilityPrecedenceOverViolationCount()
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
                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.MatrixViolation,
                        ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.NotAssessable));

                // Several assessability issues must still read as not assessable, never as multiple violations.

                Assert.That(
                    report.DetermineCompliancePublic(CreateTypedViolations(
                        ComplianceViolationType.NotAssessable,
                        ComplianceViolationType.NotAssessable)),
                    Is.EqualTo(ComplianceViolationType.NotAssessable));
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
        public async Task Generate_DiffReportLabelsSuppressedRuleWithNotAssessableAndRealViolationAsNotAssessable()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-a", foundDate: DateTime.Now.AddDays(-8))
            };
            ComplianceViolation notAssessableViolation = CreateDiffViolation(13, 101, "rule-a");
            notAssessableViolation.Type = ComplianceViolationType.NotAssessable;
            Rule activeRule = CreateActiveRule("rule-a", CreateDiffViolation(12, 101, "rule-a"));
            activeRule.Violations.Add(notAssessableViolation);
            List<Rule> activeRules = new()
            {
                activeRule
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations, activeRules);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Rule suppressedRule = report.Rules.Single();
            Assert.Multiple(() =>
            {
                Assert.That(suppressedRule.Compliance, Is.EqualTo(ComplianceViolationType.NotAssessable));
                Assert.That(
                    suppressedRule.ViolationDetails,
                    Is.EqualTo(userConfig.GetText("existing_violation_hidden_by_filter_not_assessable")));
                Assert.That(
                    report.RuleViewData.Single().Compliance,
                    Is.EqualTo("NOT ASSESSABLE"));
            });
        }

        [Test]
        public async Task Generate_DiffReportRetainsTruncatedCurrentComplianceForSuppressedRule()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceCheckMaxPrintedViolations = 1,
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-a")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-a", foundDate: DateTime.Now.AddDays(-8))
            };
            Rule activeRule = CreateActiveRule("rule-a", CreateDiffViolation(12, 101, "rule-a"));
            activeRule.Violations.Add(CreateDiffViolation(13, 101, "rule-a"));
            activeRule.Violations.Add(CreateDiffViolation(14, 101, "rule-a"));
            Rule comparisonRule = CreateActiveRule("rule-b", CreateDiffViolation(22, 102, "rule-b"));
            comparisonRule.Violations.Add(CreateDiffViolation(23, 102, "rule-b"));
            comparisonRule.Violations.Add(CreateDiffViolation(24, 102, "rule-b"));
            List<Rule> activeRules = new()
            {
                activeRule
            };
            List<Rule>[] comparisonRuleChunks = new List<Rule>[1];
            comparisonRuleChunks[0] = new List<Rule> { comparisonRule };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations, activeRules);
            MockReportCompliance baseReport = new(new(""), userConfig, Basics.ReportType.ComplianceReport);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);
            await baseReport.ProcessChunksParallelized(comparisonRuleChunks, CancellationToken.None);

            Rule suppressedRule = report.Rules.Single();
            Assert.Multiple(() =>
            {
                Assert.That(comparisonRule.Compliance, Is.EqualTo(ComplianceViolationType.MatrixViolation));
                Assert.That(suppressedRule.Compliance, Is.EqualTo(comparisonRule.Compliance));
                Assert.That(
                    suppressedRule.ViolationDetails,
                    Is.EqualTo(userConfig.GetText("existing_violation_hidden_by_filter")));
            });
        }

        [Test]
        public async Task Generate_DiffReportDoesNotLabelOtherManagementRuleSharingUidAsFiltered()
        {
            SimulatedGlobalConfig globalConfig = new()
            {
                ComplianceDiffFilterExistingViolations = true
            };
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);
            MockReportComplianceDiff report = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport)
            {
                DiffReferenceInDays = 7,
                ShowNonImpactRules = true
            };
            List<Management> managements = new()
            {
                new Management { Id = 1, Uid = "mgmt-1", Name = "Management 1" },
                new Management { Id = 2, Uid = "mgmt-2", Name = "Management 2" }
            };
            List<ComplianceViolation> intervalViolations = new()
            {
                CreateDiffViolation(1, 101, "rule-shared", mgmtUid: "mgmt-1")
            };
            List<ComplianceViolation> previousViolations = new()
            {
                CreateDiffViolation(11, 11, "rule-shared", foundDate: DateTime.Now.AddDays(-8), mgmtUid: "mgmt-2")
            };
            List<Rule> activeRules = new()
            {
                CreateActiveRule("rule-shared", mgmtId: 1),
                CreateActiveRule("rule-shared", mgmtId: 2)
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations, activeRules, managements: managements);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            Assert.That(
                report.Rules.Single(rule => rule.MgmtId == 2).ViolationDetails,
                Is.EqualTo(userConfig.GetText("no_changes_found")),
                "mgmt-2's rule never had an interval violation to hide, even though mgmt-1 has a rule sharing the same UID.");
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

            violation.Type = type;

            return violation;
        }

        private static ComplianceViolation CreateDiffViolation(
            int id,
            int ruleId,
            string ruleUid,
            DateTime? removedDate = null,
            DateTime? foundDate = null,
            bool isInitial = false,
            string mgmtUid = "mgmt-1")
        {
            return new ComplianceViolation
            {
                Id = id,
                RuleId = ruleId,
                RuleUid = ruleUid,
                MgmtUid = mgmtUid,
                FoundDate = foundDate ?? DateTime.Now.AddHours(-2),
                RemovedDate = removedDate,
                IsInitial = isInitial,
                Details = $"Violation {id}",
                Criterion = new ComplianceCriterion
                {
                    CriterionType = "Matrix"
                },
                Type = ComplianceViolationType.MatrixViolation
            };
        }

        private static List<ComplianceViolation> CreateTypedViolations(params ComplianceViolationType[] types)
        {
            return types.Select(type => new ComplianceViolation { Type = type }).ToList();
        }

        private static Rule CreateActiveRule(string ruleUid, ComplianceViolation? currentViolation = null, int mgmtId = 1)
        {
            Rule rule = new()
            {
                Id = 1000 + mgmtId,
                Uid = ruleUid,
                MgmtId = mgmtId,
                Name = ruleUid,
                Action = "accept"
            };
            if (currentViolation != null)
            {
                rule.Violations.Add(currentViolation);
            }
            return rule;
        }

        private string CreateViolationDetailsControlString(DateTime foundDate, int violationId)
        {
            return $"Found: ({foundDate:dd.MM.yyyy} - {foundDate:HH:mm}) Test violation {violationId}";
        }

        private sealed class DiffPipelineApiConnection : SimulatedApiConnection
        {
            private readonly List<ComplianceViolation> _intervalViolations;
            private readonly List<ComplianceViolation> _previousViolations;
            private readonly List<Rule> _activeRules;
            private readonly bool _failPreviousViolationFetch;
            private readonly List<Management> _managements;

            public List<string> Queries { get; } = new();
            public List<string> RequestedRuleUids { get; } = new();
            public List<int> RequestedViolationOffsets { get; } = new();
            public Dictionary<string, object>? IntervalViolationsWhere { get; private set; }
            public Dictionary<string, object>? PreviousViolationsWhere { get; private set; }

            public DiffPipelineApiConnection(
                List<ComplianceViolation> intervalViolations,
                List<ComplianceViolation>? previousViolations = null,
                List<Rule>? activeRules = null,
                bool failPreviousViolationFetch = false,
                List<Management>? managements = null)
            {
                _intervalViolations = intervalViolations;
                _previousViolations = previousViolations ?? new List<ComplianceViolation>();
                _activeRules = activeRules ?? new List<Rule>();
                _failPreviousViolationFetch = failPreviousViolationFetch;
                _managements = managements ?? new List<Management> { new Management { Id = 1, Uid = "mgmt-1", Name = "Management 1" } };
            }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, FWO.Api.Client.QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);

                if (query == DeviceQueries.getManagementNames && typeof(QueryResponseType) == typeof(List<Management>))
                {
                    return Task.FromResult((QueryResponseType)(object)_managements);
                }

                if (query == ComplianceQueries.countComplianceDiffViolations && typeof(QueryResponseType) == typeof(AggregateCount))
                {
                    Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
                    IntervalViolationsWhere = (Dictionary<string, object>)queryVariables["where"];
                    AggregateCount count = new()
                    {
                        Aggregate = new Aggregate { Count = _intervalViolations.Count }
                    };
                    return Task.FromResult((QueryResponseType)(object)count);
                }

                if (query == ComplianceQueries.getComplianceDiffViolationsByChunk && typeof(QueryResponseType) == typeof(List<ComplianceViolation>))
                {
                    Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
                    int offset = (int)queryVariables["offset"];
                    int limit = (int)queryVariables["limit"];
                    RequestedViolationOffsets.Add(offset);
                    List<ComplianceViolation> page = _intervalViolations.Skip(offset).Take(limit).ToList();
                    return Task.FromResult((QueryResponseType)(object)page);
                }

                if (query == ComplianceQueries.getActiveViolationsBeforeDate && typeof(QueryResponseType) == typeof(List<ComplianceViolation>))
                {
                    if (_failPreviousViolationFetch)
                    {
                        throw new InvalidOperationException("Previous-violation lookup failed.");
                    }

                    Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
                    PreviousViolationsWhere = (Dictionary<string, object>)queryVariables["where"];
                    Dictionary<string, object> ruleUidFilter = (Dictionary<string, object>)PreviousViolationsWhere["rule_uid"];
                    List<string> ruleUids = (List<string>)ruleUidFilter["_in"];
                    List<ComplianceViolation> page = _previousViolations
                        .Where(violation => ruleUids.Contains(violation.RuleUid))
                        .ToList();
                    return Task.FromResult((QueryResponseType)(object)page);
                }

                if (query == RuleQueries.countActiveRules && typeof(QueryResponseType) == typeof(AggregateCount))
                {
                    AggregateCount count = new()
                    {
                        Aggregate = new Aggregate { Count = _activeRules.Count }
                    };
                    return Task.FromResult((QueryResponseType)(object)count);
                }

                if (query == RuleQueries.getRulesWithCurrentViolationsByChunk && typeof(QueryResponseType) == typeof(List<Rule>))
                {
                    Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
                    int offset = (int)queryVariables["offset"];
                    int limit = (int)queryVariables["limit"];
                    List<Rule> page = _activeRules.Skip(offset).Take(limit).ToList();
                    return Task.FromResult((QueryResponseType)(object)page);
                }

                if (query == RuleQueries.getActiveRulesByUids && typeof(QueryResponseType) == typeof(List<Rule>))
                {
                    Dictionary<string, object> queryVariables = (Dictionary<string, object>)variables!;
                    List<string> ruleUids = (List<string>)queryVariables["rule_uids"];
                    RequestedRuleUids.AddRange(ruleUids);
                    List<Rule> rules = ruleUids
                        .Select((ruleUid, index) => new Rule
                        {
                            Id = 1000 + index,
                            Uid = ruleUid,
                            MgmtId = 1,
                            Name = ruleUid,
                            Action = "accept"
                        })
                        .ToList();
                    return Task.FromResult((QueryResponseType)(object)rules);
                }

                throw new NotSupportedException($"Unexpected query: {query}");
            }
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
