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
    }
}
