using FWO.Api.Client.Queries;
using FWO.Basics.Enums;
using FWO.Compliance;
using FWO.Data;
using FWO.Test.Fixtures;
using NSubstitute;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceCheckAssessabilityTest : ComplianceCheckTestFixture
    {
        private static readonly List<int> SelectedPolicyIds = [1];

        [SetUp]
        public override void SetUpTest()
        {
            base.SetUpTest();
        }

        [Test]
        public void TryGetAssessabilityIssue_UnspecifiedIpv6Fallback_ReturnsIPNull()
        {
            NetworkObject networkObject = new()
            {
                IP = "::/128",
                IpEnd = "::/128"
            };
            MethodInfo method = typeof(ComplianceCheck).GetMethod("TryGetAssessabilityIssue", BindingFlags.Static | BindingFlags.NonPublic)!;
            object?[] parameters = new object?[1];
            parameters[0] = networkObject;

            AssessabilityIssue? issue = (AssessabilityIssue?)method.Invoke(null, parameters);

            Assert.That(issue, Is.EqualTo(AssessabilityIssue.IPNull));
        }

        [TestCase(null, "0.0.0.0/32")]
        [TestCase("0.0.0.0/32", null)]
        public void TryGetAssessabilityIssue_PartiallyMissingIpRange_ReturnsIPNull(string? ip, string? ipEnd)
        {
            NetworkObject networkObject = new()
            {
                IP = ip,
                IpEnd = ipEnd
            };
            MethodInfo method = typeof(ComplianceCheck).GetMethod("TryGetAssessabilityIssue", BindingFlags.Static | BindingFlags.NonPublic)!;
            object?[] parameters = new object?[1];
            parameters[0] = networkObject;

            AssessabilityIssue? issue = (AssessabilityIssue?)method.Invoke(null, parameters);

            Assert.That(issue, Is.EqualTo(AssessabilityIssue.IPNull));
        }

        [Test]
        public async Task AreRulesCompliant_MinimumCIDRLengthWithIpv6Object_ReportsNotAssessable()
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

            Rule ipv6Rule = CreateSimpleRule(52);
            ipv6Rule.Uid = "rule-52";
            ipv6Rule.Froms[0].Object.IP = "2001:db8::1/128";
            ipv6Rule.Froms[0].Object.IpEnd = "2001:db8::1/128";
            ipv6Rule.Tos[0].Object.IP = "2001:db8:1::1/128";
            ipv6Rule.Tos[0].Object.IpEnd = "2001:db8:1::1/128";
            List<Rule> rulesToCheck = [ipv6Rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Type, Is.EqualTo(ComplianceViolationType.NotAssessable));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("MinimumCIDRLength"));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("IPv6 is currently not supported"));
            });
        }

        [Test]
        public async Task AreRulesCompliant_MinimumCIDRLengthWithIpv4AndIpv6Objects_ReportsBothViolations()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpSingleCriterionPolicy(nameof(CriterionType.MinimumCIDRLength), "24");

            Rule mixedRule = CreateSimpleRule(53);
            mixedRule.Uid = "rule-53";
            mixedRule.Froms[0].Object.IP = "10.0.0.0/8";
            mixedRule.Froms[0].Object.IpEnd = "10.255.255.255/8";
            mixedRule.Tos[0].Object.IP = "2001:db8::1/128";
            mixedRule.Tos[0].Object.IpEnd = "2001:db8::1/128";
            List<Rule> rulesToCheck = [mixedRule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                // The prefix violation keeps the type of its criterion, the not-assessable one states it.
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation =>
                    violation.Type == ComplianceViolationType.None && violation.Details.Contains("24")));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation =>
                    violation.Type == ComplianceViolationType.NotAssessable && violation.Details.Contains("IPv6 is currently not supported")));
            });
        }

        [Test]
        public async Task AreRulesCompliant_MinimumCIDRLengthWithObjectWithoutAddress_ReportsMissingAddress()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpSingleCriterionPolicy(nameof(CriterionType.MinimumCIDRLength), "24");

            Rule addresslessRule = CreateSimpleRule(54);
            addresslessRule.Uid = "rule-54";
            addresslessRule.Froms[0].Object.IP = "";
            addresslessRule.Froms[0].Object.IpEnd = "";
            addresslessRule.Tos[0].Object.IP = "10.0.0.1/32";
            addresslessRule.Tos[0].Object.IpEnd = "10.0.0.1/32";
            List<Rule> rulesToCheck = [addresslessRule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Type, Is.EqualTo(ComplianceViolationType.NotAssessable));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Contain("without IP"));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck[0].Details, Does.Not.Contain("IPv6 is currently not supported"));
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AreRulesCompliant_MatrixWithIpv6Objects_ReportsNotAssessable(bool autoCalculateInternetZone)
        {
            GlobalConfig.AutoCalculateInternetZone = autoCalculateInternetZone;
            GlobalConfig.AutoCalculateUndefinedInternalZone = autoCalculateInternetZone;
            GlobalConfig.TreatDynamicAndDomainObjectsAsInternet = autoCalculateInternetZone;
            ComplianceCheck.NetworkZones = CreateNetworkZones(autoCalculateInternetZone, autoCalculateInternetZone);

            await SetUpBasic(setupRelevantManagements: true);
            SetUpSingleCriterionPolicy(nameof(CriterionType.Matrix), "", criterionId: 1);

            Rule ipv6Rule = CreateSimpleRule(61);
            ipv6Rule.Uid = "rule-61";
            ipv6Rule.Froms[0].Object.IP = "2001:db8::";
            ipv6Rule.Froms[0].Object.IpEnd = "2001:db8::ffff";
            ipv6Rule.Tos[0].Object.IP = "2001:db8:1::";
            ipv6Rule.Tos[0].Object.IpEnd = "2001:db8:1::ffff";
            List<Rule> rulesToCheck = [ipv6Rule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.All(violation =>
                    violation.Type == ComplianceViolationType.NotAssessable));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.All(violation =>
                    violation.Details.Contains("without an assignable network zone")));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation =>
                    violation.Details.Contains("source-uid-rule61")));
                Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation =>
                    violation.Details.Contains("destination-uid-rule61")));
            });
        }

        [Test]
        public async Task AreRulesCompliant_MatrixWithIpv4Objects_ReportsNoAssessabilityViolation()
        {
            await SetUpBasic(setupRelevantManagements: true);
            SetUpSingleCriterionPolicy(nameof(CriterionType.Matrix), "", criterionId: 1);

            Rule matrixViolatingRule = CreateSimpleRule(62, destinationHigh: true);
            matrixViolatingRule.Uid = "rule-62";
            List<Rule> rulesToCheck = [matrixViolatingRule];

            bool compliant = await ComplianceCheck.AreRulesCompliant(SelectedPolicyIds, rulesToCheck);

            Assert.That(compliant, Is.False);
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.Any(violation =>
                violation.Details.Contains("Matrix violation")));
            Assert.That(ComplianceCheck.CurrentViolationsInCheck.All(violation =>
                violation.Type != ComplianceViolationType.NotAssessable));
        }

        /// <summary>
        /// Mocks a policy that holds exactly one criterion, so that violations can be attributed unambiguously.
        /// </summary>
        /// <param name="criterionType">Type of the single criterion.</param>
        /// <param name="content">Content of the single criterion.</param>
        /// <param name="criterionId">Identifier of the criterion, which has to match the network zones for a matrix.</param>
        private void SetUpSingleCriterionPolicy(string criterionType, string content, int criterionId = 11)
        {
            CompliancePolicy policy = new()
            {
                Id = 1,
                Criteria =
                [
                    new ComplianceCriterionWrapper
                    {
                        Content = new ComplianceCriterion
                        {
                            Id = criterionId,
                            Name = criterionType,
                            CriterionType = criterionType,
                            Content = content
                        }
                    }
                ]
            };

            ApiConnection.AsSub()
                .SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, Arg.Any<object>())
                .Returns(policy);

            ApiConnection.AsSub()
                .SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, Arg.Any<object>())
                .Returns(ComplianceCheck.NetworkZones);
        }
    }
}
