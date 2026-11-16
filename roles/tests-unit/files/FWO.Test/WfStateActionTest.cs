using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the rule which action types may use the event "While set".
    /// </summary>
    [TestFixture]
    internal class WfStateActionTest
    {
        [Test]
        public void AllowsWhileSet_OnlyForAutoPromote([Values] StateActionTypes actionType)
        {
            bool allowed = WfStateAction.AllowsWhileSet(actionType.ToString());

            Assert.That(allowed, Is.EqualTo(actionType == StateActionTypes.AutoPromote));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("autopromote")]
        [TestCase("Unknown")]
        public void AllowsWhileSet_UnknownActionType_IsFalse(string? actionType)
        {
            Assert.That(WfStateAction.AllowsWhileSet(actionType), Is.False);
        }
    }
}
