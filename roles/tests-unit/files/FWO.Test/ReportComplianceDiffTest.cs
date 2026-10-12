using FWO.Config.Api;
using FWO.Data;
using FWO.Test.Mocks;
using FWO.Api.Client.Queries;
using NUnit.Framework;
using static FWO.Test.ComplianceReportTestData;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportComplianceDiffTest
    {
        private MockReportComplianceDiff _testDiffReport = default!;

        [SetUp]
        public void SetUpTest()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.ComplianceCheckMaxPrintedViolations = 2;
            UserConfig userConfig = UserConfig.ForTextOnly(globalConfig);

            _testDiffReport = new(new(""), userConfig, Basics.ReportType.ComplianceDiffReport);
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
                            CriterionType = nameof(CriterionType.Assessability)
                        },
                        type: ComplianceViolationType.NotAssessable

                    )
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
                            CriterionType = nameof(CriterionType.ForbiddenService)
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

            Assert.That(testResults, Has.Count.EqualTo(4));
            Assert.That(notAssessable.ViolationDetails, Is.EqualTo(controlNotAssessable));
            Assert.That(notAssessable.Compliance, Is.EqualTo(ComplianceViolationType.NotAssessable));
            Assert.That(abbreviated.ViolationDetails, Is.EqualTo(controlAbbreviated));
            Assert.That(multiple.ViolationDetails, Is.EqualTo(controlMultiple));
            Assert.That(multiple.Compliance, Is.EqualTo(ComplianceViolationType.MultipleViolations));
            Assert.That(singular.ViolationDetails, Is.EqualTo(controlSingular));
            Assert.That(singular.Compliance, Is.EqualTo(ComplianceViolationType.ServiceViolation));
        }

        [Test]
        public async Task ProcessChunksParallelized_DiffReport_KeepsRealViolationOfPartiallyAssessableRule()
        {
            // ARRANGE

            CancellationToken ct = default;
            DateTime foundDate = DateTime.Now;

            _testDiffReport.DiffReferenceInDays = 7;

            ComplianceCriterion assessabilityCriterion = new() { CriterionType = nameof(CriterionType.Assessability) };
            Rule partiallyAssessable = new()
            {
                Id = 5,
                Name = "Testrule 5",
                Violations = [
                    CreateMockComplianceViolation(9, 5, foundDate, criterion: assessabilityCriterion, type: ComplianceViolationType.NotAssessable),
                    CreateMockComplianceViolation(10, 5, foundDate, type: ComplianceViolationType.MatrixViolation)
                ]
            };
            List<Rule>[] ruleChunks = [new List<Rule>() { partiallyAssessable }];

            // The real violation is printed first, so it survives the printed-violation limit; the assessability
            // note follows it.
            string controlDetails = CreateViolationDetailsControlString(foundDate, 10) + "<br>" + CreateViolationDetailsControlString(foundDate, 9);

            // ACT

            await _testDiffReport.ProcessChunksParallelized(ruleChunks, ct);

            // ASSERT
            // One unassessable object must not hide the violation found on the other objects of the same rule.

            Assert.Multiple(() =>
            {
                Assert.That(partiallyAssessable.ViolationDetails, Is.EqualTo(controlDetails));
                Assert.That(partiallyAssessable.Compliance, Is.EqualTo(ComplianceViolationType.MultipleViolations));
            });
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
        public async Task Generate_DiffReportLabelsSuppressedRuleWithNotAssessableAndRealViolationAsNonCompliant()
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
            notAssessableViolation.Criterion = new ComplianceCriterion
            {
                CriterionType = nameof(CriterionType.Assessability)
            };
            Rule activeRule = CreateActiveRule("rule-a", CreateDiffViolation(12, 101, "rule-a"));
            activeRule.Violations.Add(notAssessableViolation);
            List<Rule> activeRules = new()
            {
                activeRule
            };
            DiffPipelineApiConnection apiConnection = new(intervalViolations, previousViolations, activeRules);

            await report.Generate(100, apiConnection, _ => Task.CompletedTask, CancellationToken.None);

            // The suppressed rule carries a real violation next to the assessability issue, so its retained state
            // must stay non-compliant instead of being flattened to not assessable.

            Rule suppressedRule = report.Rules.Single();
            Assert.Multiple(() =>
            {
                Assert.That(suppressedRule.Compliance, Is.EqualTo(ComplianceViolationType.MultipleViolations));
                Assert.That(
                    suppressedRule.ViolationDetails,
                    Is.EqualTo(userConfig.GetText("existing_violation_hidden_by_filter")));
                Assert.That(
                    report.RuleViewData.Single().Compliance,
                    Is.EqualTo("FALSE"));
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

    }
}
