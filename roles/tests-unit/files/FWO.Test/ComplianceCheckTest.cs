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
using System.Collections.Concurrent;
using System.Reflection;

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

        private const string ExpectedViolationDetailsAutoCalcTrue = "Matrix violation: Auto-calculated Internet Zone (source-uid-rule3 (3.0.0.0-4.0.0.0)) -> 128-168 Zone (destination-uid-rule3 (128.0.0.0-168.0.0.0))";
        private const string ExpectedViolationDetailsAutoCalcFalse = "Matrix violation: Internet/Local (source-uid-rule3 (3.0.0.0-4.0.0.0)) -> 128-168 Zone (destination-uid-rule3 (128.0.0.0-168.0.0.0))";

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
            SetUpViolationCount(1);

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

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

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

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

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

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

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

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
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countActiveRules, Arg.Any<object>())
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(ComplianceCheck.RulesInCheck.Count == 5, "There should be 5 rules in check");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Count == 0, "There should be no violations for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoViolationsA)), "Unexpected violations.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(NoViolationsB)), "Unexpected violations.");
        }

        [Test]
        public async Task AreRulesCompliant_AllSelectedPoliciesPass_ReturnsTrue()
        {
            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true, setupNoViolations: true);

            bool compliant = await ComplianceCheck.AreRulesCompliant([1, 2], ComplianceCheck.RulesInCheck!);

            Assert.That(compliant, Is.True);
        }

        [Test]
        public async Task AreRulesCompliant_OneSelectedPolicyFails_ReturnsFalse()
        {
            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], ComplianceCheck.RulesInCheck!);

            Assert.That(compliant, Is.False);
        }

        [Test]
        public async Task AreRulesCompliant_MultipleMatrixCriteria_AllMatricesAreEvaluated()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 101,
                            Name = "Matrix A",
                            CriterionType = nameof(CriterionType.Matrix)
                        }
                    },
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 102,
                            Name = "Matrix B",
                            CriterionType = nameof(CriterionType.Matrix)
                        }
                    }
                ]
            };

            List<ComplianceNetworkZone> matrixAZones = CreateNetworkZones(true, true, 101);
            matrixAZones[0].Name = "Matrix A Destination";
            matrixAZones[1].Name = "Matrix A Source";

            List<ComplianceNetworkZone> matrixBZones = CreateNetworkZones(true, true, 102);
            matrixBZones[0].Name = "Matrix B Destination";
            matrixBZones[1].Name = "Matrix B Source";

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            ApiConnection.AsSub()
                .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix,
                    Arg.Is<object>(vars => HasCriterionId(vars, 101)))
                .Returns(matrixAZones);

            ApiConnection.AsSub()
                .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix,
                    Arg.Is<object>(vars => HasCriterionId(vars, 102)))
                .Returns(matrixBZones);

            Rule violatingRule = CreateSimpleRule(56, sourceHigh: true);
            violatingRule.Uid = "rule-56";

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [violatingRule]);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(2));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Select(v => v.CriterionId), Is.EquivalentTo(new[] { 101, 102 }));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(v => v.Details.Contains("Matrix A Source")), Is.True);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(v => v.Details.Contains("Matrix B Source")), Is.True);
        }

        [Test]
        public async Task CheckRuleCompliance_ForbiddenServiceUidCriterion_RemainsSupported()
        {
            Rule rule = CreateRuleWithService("forbidden-service-uid", "Forbidden Legacy Service", 6, "TCP", 443);
            ComplianceCriterion criterion = new()
            {
                Id = 1,
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = "forbidden-service-uid"
            };

            bool ruleIsCompliant = await ComplianceCheck.CheckRuleCompliance(rule, [criterion]);

            List<ComplianceViolation> violations = GetCurrentViolations();
            Assert.That(ruleIsCompliant, Is.False);
            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations.Single().Details, Does.Contain("Forbidden Legacy Service"));
        }

        [Test]
        public async Task CheckRuleCompliance_ForbiddenServiceProtocolPortCriterion_MatchesExactService()
        {
            Rule rule = CreateRuleWithService("svc-https", "HTTPS", 6, "TCP", 443);
            ComplianceCriterion criterion = new()
            {
                Id = 1,
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = "443/TCP"
            };

            bool ruleIsCompliant = await ComplianceCheck.CheckRuleCompliance(rule, [criterion]);

            List<ComplianceViolation> violations = GetCurrentViolations();
            Assert.That(ruleIsCompliant, Is.False);
            Assert.That(violations.Count, Is.EqualTo(1));
            Assert.That(violations.Single().Details, Does.Contain("443/TCP"));
        }

        [Test]
        public async Task CheckRuleCompliance_ForbiddenServiceProtocolPortCriterion_MatchesOverlappingRange()
        {
            Rule rule = CreateRuleWithService("svc-range", "Ephemeral TCP", 6, "TCP", 1500, 1600);
            ComplianceCriterion criterion = new()
            {
                Id = 1,
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = "1000-1550/TCP"
            };

            bool ruleIsCompliant = await ComplianceCheck.CheckRuleCompliance(rule, [criterion]);

            Assert.That(ruleIsCompliant, Is.False);
            Assert.That(GetCurrentViolations(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CheckRuleCompliance_ForbiddenServiceProtocolPortCriterion_ChecksFlattenedServiceGroups()
        {
            NetworkService groupMember = CreateNetworkService("svc-member", "HTTPS", 6, "TCP", 443);
            NetworkService serviceGroup = new()
            {
                Uid = "svc-group",
                Name = "Service Group",
                Type = new NetworkServiceType { Name = ServiceType.Group },
                ServiceGroupFlats =
                [
                    new GroupFlat<NetworkService> { Object = groupMember }
                ]
            };
            Rule rule = CreateRuleWithServiceGroup(serviceGroup);
            ComplianceCriterion criterion = new()
            {
                Id = 1,
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = "443/TCP"
            };

            bool ruleIsCompliant = await ComplianceCheck.CheckRuleCompliance(rule, [criterion]);

            Assert.That(ruleIsCompliant, Is.False);
            Assert.That(GetCurrentViolations(), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AreRulesCompliant_MinimumCIDRLengthPolicyFails_ReturnsFalse()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 11,
                            Name = "MinimumCIDRLength",
                            CriterionType = nameof(CriterionType.MinimumCIDRLength),
                            Content = "24"
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            Rule violatingRule = CreateSimpleRule(51);
            violatingRule.Uid = "rule-51";
            violatingRule.Froms[0].Object.IP = "10.0.0.0/8";
            violatingRule.Froms[0].Object.IpEnd = "10.255.255.255/8";

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [violatingRule]);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(1));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("24"));
        }

        [Test]
        public async Task AreRulesCompliant_ForbidZonesAsSourcePolicyFails_ReturnsFalse()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 12,
                            Name = "ForbidZonesAsSource",
                            CriterionType = nameof(CriterionType.ForbidZonesAsSource),
                            Content = "ZONE"
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            Rule violatingRule = CreateSimpleRule(52);
            violatingRule.Uid = "rule-52";
            violatingRule.Froms[0].Object.Name = "DMZ_ZONE";

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [violatingRule]);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(1));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("rule-52"));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("ZONE"));
        }

        [Test]
        public async Task AreRulesCompliant_ForbidZonesAsDestinationPolicyFails_ReturnsFalse()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 13,
                            Name = "ForbidZonesAsDestination",
                            CriterionType = nameof(CriterionType.ForbidZonesAsDestination),
                            Content = "partner"
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            Rule violatingRule = CreateSimpleRule(53);
            violatingRule.Uid = "rule-53";
            violatingRule.Tos[0].Object.Name = "PARTNER_ZONE";

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [violatingRule]);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(1));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("rule-53"));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("partner"));
        }

        [Test]
        public async Task AreRulesCompliant_ForbidBidirectionalDuplicatePolicyFails_ReturnsFalse()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 14,
                            Name = "ForbidBidirectionalDuplicate",
                            CriterionType = nameof(CriterionType.ForbidBidirectionalDuplicate)
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            Rule forwardRule = CreateSimpleRule(54);
            forwardRule.Uid = "rule-54";
            forwardRule.Services[0].Content.ProtoId = 6;
            forwardRule.Services[0].Content.DestinationPort = 443;
            forwardRule.Services[0].Content.DestinationPortEnd = 443;
            forwardRule.Services[0].Content.SourcePort = 0;
            forwardRule.Services[0].Content.SourcePortEnd = 0;

            Rule reverseRule = CreateSimpleRule(55);
            reverseRule.Uid = "rule-55";
            reverseRule.Froms[0].Object.IP = forwardRule.Tos[0].Object.IP;
            reverseRule.Froms[0].Object.IpEnd = forwardRule.Tos[0].Object.IpEnd;
            reverseRule.Tos[0].Object.IP = forwardRule.Froms[0].Object.IP;
            reverseRule.Tos[0].Object.IpEnd = forwardRule.Froms[0].Object.IpEnd;
            reverseRule.Services[0].Content.ProtoId = 6;
            reverseRule.Services[0].Content.SourcePort = 443;
            reverseRule.Services[0].Content.SourcePortEnd = 443;
            reverseRule.Services[0].Content.DestinationPort = 0;
            reverseRule.Services[0].Content.DestinationPortEnd = 0;

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [forwardRule, reverseRule]);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(2));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(v => v.Details.Contains("rule-54")), Is.True);
        }

        [Test]
        public async Task AreRulesCompliant_ForbidBidirectionalDuplicateIgnoresOtherManagements_ReturnsTrue()
        {
            await SetUpBasic(setupRelevantManagements: true);

            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = 14,
                            Name = "ForbidBidirectionalDuplicate",
                            CriterionType = nameof(CriterionType.ForbidBidirectionalDuplicate)
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            Rule forwardRule = CreateSimpleRule(154);
            forwardRule.Uid = "rule-154";
            forwardRule.MgmtId = 1;
            forwardRule.Services[0].Content.ProtoId = 6;
            forwardRule.Services[0].Content.DestinationPort = 443;
            forwardRule.Services[0].Content.DestinationPortEnd = 443;
            forwardRule.Services[0].Content.SourcePort = 0;
            forwardRule.Services[0].Content.SourcePortEnd = 0;

            Rule reverseRuleOtherManagement = CreateSimpleRule(155);
            reverseRuleOtherManagement.Uid = "rule-155";
            reverseRuleOtherManagement.MgmtId = 2;
            reverseRuleOtherManagement.Froms[0].Object.IP = forwardRule.Tos[0].Object.IP;
            reverseRuleOtherManagement.Froms[0].Object.IpEnd = forwardRule.Tos[0].Object.IpEnd;
            reverseRuleOtherManagement.Tos[0].Object.IP = forwardRule.Froms[0].Object.IP;
            reverseRuleOtherManagement.Tos[0].Object.IpEnd = forwardRule.Froms[0].Object.IpEnd;
            reverseRuleOtherManagement.Services[0].Content.ProtoId = 6;
            reverseRuleOtherManagement.Services[0].Content.SourcePort = 443;
            reverseRuleOtherManagement.Services[0].Content.SourcePortEnd = 443;
            reverseRuleOtherManagement.Services[0].Content.DestinationPort = 0;
            reverseRuleOtherManagement.Services[0].Content.DestinationPortEnd = 0;

            bool compliant = await ComplianceCheck.AreRulesCompliant([1], [forwardRule, reverseRuleOtherManagement]);

            Assert.That(compliant, Is.True);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Is.Empty);
        }

        [Test]
        public async Task CheckAll_BasicSetup_CompleteWithLog()
        {
            // Arrange

            await SetUpBasic(setupRelevantManagements: true, createPolicy: true, createRules: true);

            AggregateCount count = new AggregateCount();
            count.Aggregate.Count = ComplianceCheck.RulesInCheck!.Count;
            ApiConnection
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countActiveRules, Arg.Any<object>())
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

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
                .AsSub().SendQueryAsync<AggregateCount>(RuleQueries.countActiveRules, Arg.Any<object>())
                .Returns(Task.FromResult(count));

            // Act

            await ComplianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

            // Assert

            Assert.That(GlobalConfig.ComplianceCheckPolicyId != 0, "Default policy ID should not be zero for this test.");
            Assert.That(ComplianceCheck.Policy != null, "Policy should not be null for this test.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Count == 4, "There should be four violations for this test.");
            Assert.That(Logger.Logmessages.Values.Any(m => m.Contains(BasicSetup)), "Unexpected violations.");
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.Details == ExpectedViolationDetailsAutoCalcFalse));
        }

        private static bool HasCriterionId(object variables, int expectedCriterionId)
        {
            object? criterionId = variables.GetType().GetProperty("criterionId")?.GetValue(variables);
            return criterionId is int matrixId && matrixId == expectedCriterionId;
        }

        #endregion

        #region Tests - PostProcessRulesAsync

        [Test]
        public async Task PostProcessRulesAsync_DiffsAddAndRemoveCorrectly()
        {
            // Arrange

            const int policyId = 99;
            const int criterionId = 7;
            const string mgmtUid = "mgmt-1";

            Rule dbRuleKeep = CreateSimpleRule(1);
            dbRuleKeep.Uid = "rule-1";
            ComplianceViolation keepViolationDb = new()
            {
                Id = 10,
                RuleId = (int)dbRuleKeep.Id,
                RuleUid = dbRuleKeep.Uid ?? "",
                MgmtUid = mgmtUid,
                PolicyId = policyId,
                CriterionId = criterionId,
                Details = "keep"
            };
            dbRuleKeep.Violations.Add(keepViolationDb);

            Rule dbRuleObsolete = CreateSimpleRule(2);
            dbRuleObsolete.Uid = "rule-2";
            ComplianceViolation obsoleteViolationDb = new()
            {
                Id = 11,
                RuleId = (int)dbRuleObsolete.Id,
                RuleUid = dbRuleObsolete.Uid ?? "",
                MgmtUid = mgmtUid,
                PolicyId = policyId,
                CriterionId = criterionId,
                Details = "obsolete"
            };
            dbRuleObsolete.Violations.Add(obsoleteViolationDb);

            ComplianceCheck.CurrentViolationsInCheck.Clear();
            ComplianceCheck.CurrentViolationsInCheck.Add(new ComplianceViolation
            {
                RuleId = keepViolationDb.RuleId,
                RuleUid = keepViolationDb.RuleUid,
                MgmtUid = keepViolationDb.MgmtUid,
                PolicyId = keepViolationDb.PolicyId,
                CriterionId = keepViolationDb.CriterionId,
                Details = keepViolationDb.Details
            });
            ComplianceCheck.CurrentViolationsInCheck.Add(new ComplianceViolation
            {
                RuleId = 3,
                RuleUid = "rule-3",
                MgmtUid = mgmtUid,
                PolicyId = policyId,
                CriterionId = criterionId,
                Details = "new"
            });

            // Act

            await ComplianceCheck.PostProcessRulesAsync([dbRuleKeep, dbRuleObsolete]);
            await ComplianceCheck.PersistDataAsync();

            // Assert

            Assert.That(ApiConnection.SentQueries.Count, Is.EqualTo(2), "Should send add and remove mutations.");

            (string Query, object Variables) addQuery = ApiConnection.SentQueries[0];
            Assert.That(addQuery.Query, Is.EqualTo(ComplianceQueries.addViolations));
            IEnumerable<ComplianceViolationBase>? addedViolations = addQuery.Variables
                .GetType()
                .GetProperty("violations")?
                .GetValue(addQuery.Variables) as IEnumerable<ComplianceViolationBase>;
            Assert.That(addedViolations, Is.Not.Null);
            Assert.That(addedViolations!.Count(), Is.EqualTo(1));
            Assert.That(addedViolations!.First().Details, Is.EqualTo("new"));

            (string Query, object Variables) removeQuery = ApiConnection.SentQueries[1];
            Assert.That(removeQuery.Query, Is.EqualTo(ComplianceQueries.removeViolations));
            IEnumerable<int>? removedIds = removeQuery.Variables
                .GetType()
                .GetProperty("ids")?
                .GetValue(removeQuery.Variables) as IEnumerable<int>;
            Assert.That(removedIds, Is.Not.Null);
            Assert.That(removedIds!.Single(), Is.EqualTo(obsoleteViolationDb.Id));
        }

        [Test]
        public async Task PostProcessRulesAsync_ParallelPathProducesSameDiff()
        {
            // Arrange

            const int policyId = 77;
            const int criterionId = 5;
            const string mgmtUid = "mgmt-parallel";

            List<Rule> rulesFromDb = new();
            List<ComplianceViolation> currentViolations = new();

            for (int i = 0; i < 200; i++)
            {
                Rule rule = CreateSimpleRule(i + 1);
                rule.Uid = $"rule-{i + 1}";

                if (i % 2 == 0)
                {
                    ComplianceViolation violation = new()
                    {
                        Id = 1000 + i,
                        RuleId = (int)rule.Id,
                        RuleUid = rule.Uid ?? "",
                        MgmtUid = mgmtUid,
                        PolicyId = policyId,
                        CriterionId = criterionId,
                        Details = $"db-only-{i}"
                    };
                    rule.Violations.Add(violation);
                }

                rulesFromDb.Add(rule);
            }

            for (int i = 0; i < 200; i++)
            {
                if (i % 3 == 0)
                {
                    currentViolations.Add(new ComplianceViolation
                    {
                        RuleId = i + 1,
                        RuleUid = $"rule-{i + 1}",
                        MgmtUid = mgmtUid,
                        PolicyId = policyId,
                        CriterionId = criterionId,
                        Details = $"current-only-{i}"
                    });
                }
            }

            ComplianceCheck.CurrentViolationsInCheck.Clear();
            ComplianceCheck.CurrentViolationsInCheck.AddRange(currentViolations);

            // Act

            await ComplianceCheck.PostProcessRulesAsync(rulesFromDb);

            // Assert

            int expectedRemovals = rulesFromDb.Sum(r => r.Violations.Count); // every db violation is absent from current
            int expectedAdds = currentViolations.Count; // every current violation is absent from db

            ConcurrentBag<ComplianceViolationBase> adds = (ConcurrentBag<ComplianceViolationBase>)typeof(ComplianceCheck)
                .GetField("_violationsToAdd", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(ComplianceCheck)!;
            ConcurrentBag<ComplianceViolation> removes = (ConcurrentBag<ComplianceViolation>)typeof(ComplianceCheck)
                .GetField("_violationsToRemove", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(ComplianceCheck)!;

            Assert.That(adds.Count, Is.EqualTo(expectedAdds));
            Assert.That(removes.Count, Is.EqualTo(expectedRemovals));
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
            await SetUpBasic(createPolicy: true);

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

        private List<ComplianceViolation> GetCurrentViolations()
        {
            return
            [
                .. (ConcurrentBag<ComplianceViolation>)typeof(ComplianceCheck)
                    .GetField("_currentViolations", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(ComplianceCheck)!
            ];
        }
    }
}
