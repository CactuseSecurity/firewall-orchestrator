using FWO.Data.Workflow;
using FWO.Services.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class WorkflowVisibilityHelperTest
    {
        [Test]
        public void CanAccessState_ReturnsTrue_ForAllowedVisibilityGroup()
        {
            StateMatrix matrix = new()
            {
                StateVisibilityGroupIds =
                {
                    [10] = [3]
                }
            };

            bool visible = WorkflowVisibilityHelper.CanAccessState(10, matrix, [3]);

            Assert.That(visible, Is.True);
        }

        [Test]
        public void CanAccessState_ReturnsFalse_WhenExclusiveGroupBlocksUntaggedState()
        {
            StateMatrix matrix = new();

            bool visible = WorkflowVisibilityHelper.CanAccessState(10, matrix, [7], [7]);

            Assert.That(visible, Is.False);
        }

        [Test]
        public void CanAccessStatefulObject_UsesExplicitExclusiveGroupOverride()
        {
            StateMatrix matrix = new()
            {
                ExclusiveVisibilityGroupIds = [7]
            };
            WfStatefulObject statefulObject = new()
            {
                StateId = 10
            };

            bool visible = WorkflowVisibilityHelper.CanAccessStatefulObject(statefulObject, matrix, [3], [8]);

            Assert.That(visible, Is.True);
        }
    }
}
