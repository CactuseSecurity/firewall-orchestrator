using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Test.Fixtures;
using NetTools;
using NSubstitute;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Verifies that the compliance check fails closed when a criterion cannot be evaluated.
    /// </summary>
    [TestFixture]
    internal class ComplianceCheckFailClosedTest : ComplianceCheckTestFixture
    {
        private const int kPolicyId = 1;
        private const int kMatrixCriterionId = 1;
        private const int kForbiddenServiceCriterionId = 2;
        private const int kMinimumCidrCriterionId = 3;
        private const string kUnparseableIp = "fwo-sweep-not-an-ip";
        private const string kForbiddenServiceUid = "telnet-uid";
        private const string kInvalidIpText = "IP address that cannot be parsed";
        private const string kCriterionFailedText = "Criterion could not be evaluated";
        private const string kCriterionInvalidText = "Criterion has invalid or empty content";
        private static readonly List<int> SelectedPolicyIds = [kPolicyId];

        [SetUp]
        public override void SetUpTest()
        {
            base.SetUpTest();
        }

        [Test]
        public async Task AreRulesCompliant_UnparseableHostIpBeforeForbiddenService_ReportsBothViolations()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpPolicy(CreateMatrixCriterion(), CreateForbiddenServiceCriterion(kForbiddenServiceUid));
            Rule rule = CreateTelnetRule(70);
            rule.Froms[0].Object.Type.Name = ObjectType.Host;
            rule.Froms[0].Object.IP = kUnparseableIp;
            rule.Froms[0].Object.IpEnd = kUnparseableIp;
            List<Rule> rulesToCheck = [rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.CriterionId == kMatrixCriterionId
                    && violation.Type == ComplianceViolationType.NotAssessable
                    && violation.Details.Contains(kInvalidIpText)), Is.True);
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.CriterionId == kForbiddenServiceCriterionId), Is.True);
            });
        }

        [Test]
        public async Task AreRulesCompliant_UnparseableRangeIp_ReportsNotAssessable()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpPolicy(CreateMatrixCriterion());
            Rule rule = CreateSimpleRule(71);
            rule.Uid = "rule-71";
            rule.Froms[0].Object.IP = kUnparseableIp;
            rule.Froms[0].Object.IpEnd = "10.0.0.1/32";
            List<Rule> rulesToCheck = [rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.Type == ComplianceViolationType.NotAssessable
                && violation.Details.Contains(kInvalidIpText)), Is.True);
        }

        [Test]
        public async Task AreRulesCompliant_ThrowingCriterion_EvaluatesRemainingCriteria()
        {
            await SetUpBasic(setupRelevantManagements: true);
            ComplianceCriterion throwingCriterion = CreateForbiddenServiceCriterion(null!);
            SetUpPolicy(throwingCriterion, CreateMinimumCidrCriterion("24"));
            Rule rule = CreateWideSourceRule(72);
            List<Rule> rulesToCheck = [rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.CriterionId == kForbiddenServiceCriterionId
                    && violation.Type == ComplianceViolationType.NotAssessable
                    && violation.Details.Contains(kCriterionFailedText)), Is.True);
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation => violation.CriterionId == kMinimumCidrCriterionId
                    && violation.Type != ComplianceViolationType.NotAssessable), Is.True);
            });
        }

        [TestCase(nameof(CriterionType.MinimumCIDRLength), "not-a-number")]
        [TestCase(nameof(CriterionType.MinimumCIDRLength), "33")]
        [TestCase(nameof(CriterionType.ForbidZonesAsSource), " ")]
        [TestCase(nameof(CriterionType.ForbidZonesAsDestination), "")]
        public async Task AreRulesCompliant_InvalidCriterionContent_IsNotCompliant(string criterionType, string content)
        {
            await SetUpBasic(setupRelevantManagements: true);
            ComplianceCriterion criterion = new()
            {
                Id = kMinimumCidrCriterionId,
                Name = criterionType,
                CriterionType = criterionType,
                Content = content
            };
            SetUpPolicy(criterion);
            Rule rule = CreateSimpleRule(73);
            rule.Uid = "rule-73";
            List<Rule> rulesToCheck = [rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Single().Details, Does.Contain(kCriterionInvalidText));
        }

        [Test]
        public async Task CheckRuleCompliance_MissingDuplicateIndex_IsNotCompliant()
        {
            ComplianceCriterion criterion = new()
            {
                Id = kMinimumCidrCriterionId,
                Name = "duplicates",
                CriterionType = nameof(CriterionType.ForbidBidirectionalDuplicate)
            };
            List<ComplianceCriterion> criteria = [criterion];

            bool compliant = await ComplianceCheck.CheckRuleCompliance(CreateSimpleRule(74), criteria);

            Assert.That(compliant, Is.False);
        }

        [Test]
        public async Task PostProcessRulesAsync_CriterionThatThrew_KeepsStoredViolations()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpPolicy(CreateForbiddenServiceCriterion(null!), CreateMinimumCidrCriterion("24"));
            Rule rule = CreateSimpleRule(75);
            rule.Uid = "rule-75";
            ComplianceViolation storedViolationOfFailedCriterion = CreateStoredViolation(rule, 100, kForbiddenServiceCriterionId);
            ComplianceViolation storedViolationOfEvaluatedCriterion = CreateStoredViolation(rule, 101, kMinimumCidrCriterionId);
            rule.Violations.Add(storedViolationOfFailedCriterion);
            rule.Violations.Add(storedViolationOfEvaluatedCriterion);
            List<Rule> rulesToCheck = [rule];

            await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);
            await ComplianceCheck.PostProcessRulesAsync(rulesToCheck);
            await ComplianceCheck.PersistDataAsync();

            List<int> removedIds = GetRemovedViolationIds();
            Assert.That(removedIds, Does.Not.Contain(storedViolationOfFailedCriterion.Id));
            Assert.That(removedIds, Does.Contain(storedViolationOfEvaluatedCriterion.Id));
        }

        [Test]
        public void TryParseIpRange_UnparseableValues_ReturnsFalseWithoutThrowing()
        {
            NetworkObject host = new() { IP = kUnparseableIp, IpEnd = kUnparseableIp };
            host.Type.Name = ObjectType.Host;
            NetworkObject range = new() { IP = kUnparseableIp, IpEnd = "10.0.0.1/32" };
            range.Type.Name = ObjectType.IPRange;

            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.TryParseIpRange(host, out List<IPAddressRange> hostRanges), Is.False);
                Assert.That(hostRanges, Is.Empty);
                Assert.That(ComplianceCheck.TryParseIpRange(range, out List<IPAddressRange> rangeRanges), Is.False);
                Assert.That(rangeRanges, Is.Empty);
                Assert.That(ComplianceCheck.ParseIpRange(host), Is.Empty);
            });
        }

        [Test]
        public void TryParseIpRange_GroupWithOneUnparseableMember_ReturnsFalseAndKeepsParsedRanges()
        {
            NetworkObject validMember = new() { IP = "10.0.0.1/32", IpEnd = "10.0.0.1/32" };
            validMember.Type.Name = ObjectType.Host;
            NetworkObject invalidMember = new() { IP = kUnparseableIp, IpEnd = kUnparseableIp };
            invalidMember.Type.Name = ObjectType.Host;
            NetworkObject group = new()
            {
                ObjectGroupFlats =
                [
                    new GroupFlat<NetworkObject> { Object = validMember },
                    new GroupFlat<NetworkObject> { Object = invalidMember }
                ]
            };
            group.Type.Name = ObjectType.Host;

            bool parsed = ComplianceCheck.TryParseIpRange(group, out List<IPAddressRange> ranges);

            Assert.That(parsed, Is.False);
            Assert.That(ranges, Has.Count.EqualTo(1));
        }

        private void SetUpPolicy(params ComplianceCriterion[] criteria)
        {
            CompliancePolicy policy = new()
            {
                Id = kPolicyId,
                Criteria = [.. criteria.Select(criterion => new ComplianceCriterionWrapper { Content = criterion })]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            ApiConnection.AsSub()
                .SendQueryAsync<List<NetworkZone>>(NetworkZoneQueries.getNetworkZonesForMatrix, Arg.Any<object>())
                .Returns(ComplianceCheck.NetworkZones);
        }

        private static ComplianceCriterion CreateMatrixCriterion()
        {
            return new ComplianceCriterion
            {
                Id = kMatrixCriterionId,
                Name = "Matrix",
                CriterionType = nameof(CriterionType.Matrix)
            };
        }

        private static ComplianceCriterion CreateForbiddenServiceCriterion(string content)
        {
            return new ComplianceCriterion
            {
                Id = kForbiddenServiceCriterionId,
                Name = "ForbiddenService",
                CriterionType = nameof(CriterionType.ForbiddenService),
                Content = content
            };
        }

        private static ComplianceCriterion CreateMinimumCidrCriterion(string content)
        {
            return new ComplianceCriterion
            {
                Id = kMinimumCidrCriterionId,
                Name = "MinimumCIDRLength",
                CriterionType = nameof(CriterionType.MinimumCIDRLength),
                Content = content
            };
        }

        private Rule CreateTelnetRule(int ruleId)
        {
            Rule rule = CreateRuleWithService(kForbiddenServiceUid, "telnet", 6, "tcp", 23);
            rule.Id = ruleId;
            rule.Uid = $"rule-{ruleId}";
            return rule;
        }

        private Rule CreateWideSourceRule(int ruleId)
        {
            Rule rule = CreateSimpleRule(ruleId);
            rule.Uid = $"rule-{ruleId}";
            rule.Froms[0].Object.Type.Name = ObjectType.Network;
            rule.Froms[0].Object.IP = "10.0.0.0/32";
            rule.Froms[0].Object.IpEnd = "10.255.255.255/32";
            return rule;
        }

        private static ComplianceViolation CreateStoredViolation(Rule rule, int violationId, int criterionId)
        {
            return new ComplianceViolation
            {
                Id = violationId,
                RuleId = (int)rule.Id,
                RuleUid = rule.Uid ?? "",
                MgmtUid = "",
                PolicyId = kPolicyId,
                CriterionId = criterionId,
                Details = $"stored violation {violationId}"
            };
        }

        private List<int> GetRemovedViolationIds()
        {
            return ApiConnection.SentQueries
                .Where(sentQuery => sentQuery.Query == ComplianceQueries.removeViolations)
                .SelectMany(sentQuery => sentQuery.Variables.GetType().GetProperty("ids")?.GetValue(sentQuery.Variables) as IEnumerable<int> ?? Enumerable.Empty<int>())
                .ToList();
        }
    }
}
