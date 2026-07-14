using FWO.Basics;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Test.Fixtures;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    internal class ComplianceRequestedRulePolicyCheckerTest : ComplianceCheckTestFixture
    {
        private ComplianceRequestedRulePolicyChecker checker = default!;

        [SetUp]
        public override void SetUpTest()
        {
            base.SetUpTest();
            checker = new ComplianceRequestedRulePolicyChecker(UserConfig, ApiConnection);
        }

        [Test]
        public async Task AreRequestTasksCompliant_NoPolicies_ReturnsFalse()
        {
            bool result = await checker.AreRequestTasksCompliant([], [CreateEligibleRequestTask(11)]);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task AreRequestTasksCompliant_NoEligibleRules_ReturnsFalse()
        {
            WfReqTask incompleteTask = new()
            {
                Id = 12,
                ManagementId = 1,
                Title = "Incomplete request",
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = "rule-12" }
                ]
            };

            bool result = await checker.AreRequestTasksCompliant([5], [incompleteTask]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void BuildRulesFromRequestTasks_MapsEligibleTaskAndSkipsDeletedElements()
        {
            WfReqTask task = CreateEligibleRequestTask(21);
            task.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.source.ToString(),
                IpString = "10.0.0.2/32",
                Name = "deleted-src",
                RequestAction = nameof(RequestAction.delete)
            });
            task.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.service.ToString(),
                Port = 8443,
                PortEnd = 8450,
                ProtoId = 6,
                Name = "deleted-svc",
                RequestAction = nameof(RequestAction.delete)
            });

            List<Rule> rules = BuildRulesFromRequestTasks(task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(rules[0].MgmtId, Is.EqualTo(1));
                Assert.That(rules[0].Uid, Is.EqualTo("rule-21"));
                Assert.That(rules[0].Action, Is.EqualTo(RuleActions.Accept));
                Assert.That(rules[0].Froms, Has.Length.EqualTo(1));
                Assert.That(rules[0].Tos, Has.Length.EqualTo(1));
                Assert.That(rules[0].Services, Has.Length.EqualTo(1));
                Assert.That(rules[0].Froms[0].Object.IP, Is.EqualTo("10.0.0.1/32"));
                Assert.That(rules[0].Services[0].Content.DestinationPort, Is.EqualTo(443));
            });
        }

        [Test]
        public void BuildRulesFromRequestTasks_MapsDeleteTaskToDropAndRangeToIpRange()
        {
            WfReqTask task = CreateEligibleRequestTask(22, nameof(WfTaskType.rule_delete));
            task.Elements[0].IpString = "10.0.0.1";
            task.Elements[0].IpEnd = "10.0.0.9";
            task.Elements[0].CidrEnd = new Cidr("10.0.0.9");

            List<Rule> rules = BuildRulesFromRequestTasks(task);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(rules[0].Action, Is.EqualTo(RuleActions.Drop));
                Assert.That(rules[0].Froms[0].Object.Type.Name, Is.EqualTo(ObjectType.IPRange));
                Assert.That(rules[0].Froms[0].Object.IpEnd, Is.EqualTo("10.0.0.9/32"));
            });
        }

        [Test]
        public void BuildRulesFromRequestTasks_SkipsTasksWithoutRequiredActiveElements()
        {
            WfReqTask task = CreateEligibleRequestTask(23);
            task.Elements.First(element => element.Field == ElemFieldType.service.ToString()).RequestAction = nameof(RequestAction.delete);

            List<Rule> rules = BuildRulesFromRequestTasks(task);

            Assert.That(rules, Is.Empty);
        }

        private static WfReqTask CreateEligibleRequestTask(long id, string? taskType = null, string? requestAction = null)
        {
            return new WfReqTask
            {
                Id = id,
                ManagementId = 1,
                Title = $"Request {id}",
                TaskType = taskType ?? WfTaskType.access.ToString(),
                RequestAction = requestAction ?? nameof(RequestAction.create),
                Elements =
                [
                    new WfReqElement { Field = ElemFieldType.source.ToString(), IpString = "10.0.0.1/32", Name = "src" },
                    new WfReqElement { Field = ElemFieldType.destination.ToString(), IpString = "10.0.1.1/32", Name = "dst" },
                    new WfReqElement { Field = ElemFieldType.service.ToString(), Port = 443, ProtoId = 6, Name = "https" },
                    new WfReqElement { Field = ElemFieldType.rule.ToString(), RuleUid = $"rule-{id}" }
                ]
            };
        }

        private static List<Rule> BuildRulesFromRequestTasks(params WfReqTask[] requestTasks)
        {
            MethodInfo method = typeof(ComplianceRequestedRulePolicyChecker).GetMethod("BuildRulesFromRequestTasks", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new AssertionException("BuildRulesFromRequestTasks method not found.");
            object? result = method.Invoke(null, [requestTasks.AsEnumerable()]);
            return result as List<Rule> ?? throw new AssertionException("BuildRulesFromRequestTasks returned unexpected result.");
        }
    }
}
