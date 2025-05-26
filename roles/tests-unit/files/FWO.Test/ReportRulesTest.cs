using NUnit.Framework;
using FWO.Report;
using FWO.Data;

namespace FWO.Test
{
    [TestFixture]
    internal class ReportRulesTest
    {
        [Test]
        [Ignore("This unit test is deprecated.")]
        public void CreateOrderNumbersSimpleStructureCorrectNumberingTest()
        {
            // ARRANGE

            List<Rule> rulesUnderTest = new List<Rule>
            {
                new Rule
                {
                    Id = 1,
                    Name = "1.1",
                    RulebaseId = 1,
                    RuleOrderNumber = 0
                },
                new Rule
                {
                    Id = 2,
                    Name = "1.2",
                    RulebaseId = 1,
                    RuleOrderNumber = 1
                },
                // rulebase link: ordered from rule 2 to rulebase 2
                new Rule
                {
                    Id = 3,
                    Name = "2.1",
                    RulebaseId = 2,
                    RuleOrderNumber = 0
                },
                // rulebase link: inline from rule 3 to rulebase 3
                new Rule
                {
                    Id = 4,
                    Name = "2.1.1",
                    RulebaseId = 3,
                    RuleOrderNumber = 0
                },
                new Rule
                {
                    Id = 5,
                    Name = "2.1.2",
                    RulebaseId = 3,
                    RuleOrderNumber = 1
                },
                // no more rules for rulebase => jump backwards to last rulebase with remaining rules
                new Rule
                {
                    Id = 6,
                    Name = "2.2",
                    RulebaseId = 2,
                    RuleOrderNumber = 1
                },
                // rulebase link: ordered from rule 5 to rulebase 4
                new Rule
                {
                    Id = 7,
                    Name = "3.1",
                    RulebaseId = 4,
                    RuleOrderNumber = 0
                }
            };

            // DeviceReport device = new ();
            // device.RulebaseLinks = new RulebaseLink[]
            // {
            //     new RulebaseLink
            //     {
            //         GatewayId = 1,
            //         FromRuleId = null,
            //         LinkType = 2,
            //         IsInitial = true,
            //         IsGlobal = false,
            //         NextRulebaseId = 1
            //     },
            //     new RulebaseLink
            //     {
            //         GatewayId = 1,
            //         FromRuleId = 2,
            //         LinkType = 2,
            //         IsInitial = false,
            //         IsGlobal = false,
            //         NextRulebaseId = 2
            //     },
            //     new RulebaseLink
            //     {
            //         GatewayId = 1,
            //         FromRuleId = 3,
            //         LinkType = 3,
            //         IsInitial = false,
            //         IsGlobal = false,
            //         NextRulebaseId = 3
            //     },
            //     new RulebaseLink
            //     {
            //         GatewayId = 1,
            //         FromRuleId = 6,
            //         LinkType = 2,
            //         IsInitial = false,
            //         IsGlobal = false,
            //         NextRulebaseId = 4
            //     }
            // };

            // ACT
            // ReportRules.CreateOrderNumbers(rulesUnderTest, device);

            // ASSERT
            foreach (Rule rule in rulesUnderTest)
            {
                Assert.That(rule.Id, Is.EqualTo(rule.OrderNumber), "OrderNumber should be equal to id (in this test scenario).");
                Assert.That(rule.Name, Is.EqualTo(rule.DisplayOrderNumberString), "ODisplayOrderNumberString should be equal to name (in this test scenario).");
            }

        }
    }
}
