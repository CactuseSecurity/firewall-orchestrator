using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    internal class TimeObjectElementHelperTest
    {
        [Test]
        public void GetOrCreate_CreatesTimeElementOnce()
        {
            WfReqTask task = new() { Id = 17 };

            WfReqElement first = TimeObjectElementHelper.GetOrCreate(task);
            WfReqElement second = TimeObjectElementHelper.GetOrCreate(task);

            Assert.That(second, Is.SameAs(first));
            Assert.That(task.Elements.Count(element => element.Field == ElemFieldType.time.ToString()), Is.EqualTo(1));
            Assert.That(first.TaskId, Is.EqualTo(17));
        }

        [Test]
        public void SetReference_StoresFlowAndTicketReferencesSeparately()
        {
            WfReqElement element = new();

            TimeObjectElementHelper.SetReference(element, 42);
            Assert.Multiple(() =>
            {
                Assert.That(element.FlowTimeObjectId, Is.EqualTo(42));
                Assert.That(element.TimeObjId, Is.Null);
                Assert.That(TimeObjectElementHelper.GetSelectorId(element), Is.EqualTo(42));
            });

            TimeObjectElementHelper.SetReference(element, -7);
            Assert.Multiple(() =>
            {
                Assert.That(element.FlowTimeObjectId, Is.Null);
                Assert.That(element.TimeObjId, Is.Null);
                Assert.That(TimeObjectElementHelper.GetSelectorId(element), Is.Null);
            });
        }

        [Test]
        public void ClearReference_RemovesBothReferenceKinds()
        {
            WfReqElement element = new() { FlowTimeObjectId = 42, TimeObjId = -7 };

            TimeObjectElementHelper.ClearReference(element);

            Assert.That(TimeObjectElementHelper.GetSelectorId(element), Is.Null);
        }

        [Test]
        public void SetTicketTaskReference_StoresRequestTaskReferenceWithoutForeignKeyValue()
        {
            WfReqElement element = new() { FlowTimeObjectId = 42, TimeObjId = 7 };

            TimeObjectElementHelper.SetTicketTaskReference(element, 19);

            Assert.Multiple(() =>
            {
                Assert.That(element.ObjectTaskId, Is.EqualTo(19));
                Assert.That(element.FlowTimeObjectId, Is.Null);
                Assert.That(element.TimeObjId, Is.Null);
            });
        }

        [Test]
        public void WfImplElement_CopiesTicketTimeObjectReference()
        {
            WfReqElement requestElement = new()
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = 19
            };

            WfImplElement implementationElement = new(requestElement);

            Assert.That(implementationElement.ObjectTaskId, Is.EqualTo(19));
        }
    }
}
