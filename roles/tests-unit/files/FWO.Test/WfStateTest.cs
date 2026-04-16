using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WfStateTest
    {
        [Test]
        public void ActionList_ReturnsCommaSeparatedNames()
        {
            WfState state = new()
            {
                Actions =
                [
                    new WfStateActionDataHelper { Action = new WfStateAction { Name = "A" } },
                    new WfStateActionDataHelper { Action = new WfStateAction { Name = "B" } }
                ]
            };

            string list = state.ActionList();

            Assert.That(list, Is.EqualTo("A, B"));
        }

        [Test]
        public void CopyConstructor_CopiesFields()
        {
            WfState original = new()
            {
                Id = 3,
                Name = "state",
                AutomaticOnly = true,
                Actions =
                [
                    new WfStateActionDataHelper { Action = new WfStateAction { Name = "A" } }
                ]
            };

            WfState copy = new(original);

            Assert.That(copy.Id, Is.EqualTo(3));
            Assert.That(copy.Name, Is.EqualTo("state"));
            Assert.That(copy.AutomaticOnly, Is.True);
            Assert.That(copy.Actions, Is.EqualTo(original.Actions));
        }

        [Test]
        public void Automatic_DefaultsToFalse()
        {
            WfState state = new();

            Assert.That(state.AutomaticOnly, Is.False);
        }

        [Test]
        public void IsReadonlyType_ReturnsTrueForReadonlyActions()
        {
            Assert.That(WfStateAction.IsReadonlyType(StateActionTypes.TrafficPathAnalysis.ToString()), Is.True);
            Assert.That(WfStateAction.IsReadonlyType(StateActionTypes.DisplayConnection.ToString()), Is.True);
        }

        [Test]
        public void IsReadonlyType_ReturnsFalseForOtherActions()
        {
            Assert.That(WfStateAction.IsReadonlyType(StateActionTypes.SendEmail.ToString()), Is.False);
            Assert.That(WfStateAction.IsReadonlyType("NotAType"), Is.False);
        }

        [Test]
        public void TryParseAutoPromoteParams_ParsesLegacyStateId()
        {
            bool parsed = WfStateAction.TryParseAutoPromoteParams("7", out int? toStateId, out ConditionalAutoPromoteParams? conditionalParams);

            Assert.That(parsed, Is.True);
            Assert.That(toStateId, Is.EqualTo(7));
            Assert.That(conditionalParams, Is.Null);
        }

        [Test]
        public void TryParseAutoPromoteParams_ParsesConditionalPayload()
        {
            string payload = "{\"to_be_called\":\"PolicyCheck\",\"policy_ids\":[1,2],\"check_result_label\":\"policy_check\",\"if_compliant_state\":3,\"if_not_compliant_state\":4}";

            bool parsed = WfStateAction.TryParseAutoPromoteParams(payload, out int? toStateId, out ConditionalAutoPromoteParams? conditionalParams);

            Assert.That(parsed, Is.True);
            Assert.That(toStateId, Is.Null);
            Assert.That(conditionalParams, Is.Not.Null);
            Assert.That(conditionalParams?.ToBeCalled, Is.EqualTo(ToBeCalled.PolicyCheck));
            Assert.That(conditionalParams?.PolicyIds, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(conditionalParams?.CheckResultLabel, Is.EqualTo("policy_check"));
            Assert.That(conditionalParams?.IfCompliantState, Is.EqualTo(3));
            Assert.That(conditionalParams?.IfNotCompliantState, Is.EqualTo(4));
        }
    }
}
