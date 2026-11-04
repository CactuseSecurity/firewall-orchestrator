using FWO.Basics;
using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class WorkflowStateIconsTest
    {
        private const int kDoneStateId = 600;
        private const int kRejectedStateId = 610;
        private const int kInImplementationStateId = 210;
        private const int kCustomStateId = 777;

        [Test]
        public void Get_DoneState_ReturnsGreenCheck()
        {
            WorkflowStateIcon? icon = WorkflowStateIcons.Get(kDoneStateId);

            Assert.That(icon, Is.Not.Null);
            Assert.That(icon!.CssClass, Is.EqualTo($"{Icons.Check} text-success"));
        }

        [Test]
        public void Get_RejectedState_ReturnsDangerIcon()
        {
            WorkflowStateIcon? icon = WorkflowStateIcons.Get(kRejectedStateId);

            Assert.That(icon?.ColorClass, Is.EqualTo("text-danger"));
        }

        [Test]
        public void Get_InProgressState_ReturnsPrimaryIcon()
        {
            WorkflowStateIcon? icon = WorkflowStateIcons.Get(kInImplementationStateId);

            Assert.That(icon?.ColorClass, Is.EqualTo("text-primary"));
        }

        [Test]
        public void Get_UnknownState_ReturnsNull()
        {
            Assert.That(WorkflowStateIcons.Get(kCustomStateId), Is.Null);
        }
    }
}
