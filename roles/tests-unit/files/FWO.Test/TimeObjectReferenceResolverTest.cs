using FWO.Data.Workflow;
using NUnit.Framework;

namespace FWO.Test
{
    internal class TimeObjectReferenceResolverTest
    {
        [Test]
        public void FindParallelTimeElement_UsesElementIdBeforeDateFallback()
        {
            WfReqTask accessTask = BuildAccessTask();
            WfReqTask objectTask = BuildObjectTask(
                new WfReqElement { Id = 21, Field = ElemFieldType.time.ToString(), StartTime = accessTask.TargetBeginDate, EndTime = accessTask.TargetEndDate },
                new WfReqElement { Id = 22, Field = ElemFieldType.time.ToString(), StartTime = accessTask.TargetBeginDate, EndTime = accessTask.TargetEndDate });

            WfReqElement? result = TimeObjectReferenceResolver.FindParallelTimeElement(
                BuildTasks(accessTask, objectTask), accessTask, null, 22);

            Assert.That(result?.Id, Is.EqualTo(22));
        }

        [Test]
        public void FindParallelTimeElement_DoesNotGuessWhenDatesAreAmbiguous()
        {
            WfReqTask accessTask = BuildAccessTask();
            WfReqTask objectTask = BuildObjectTask(
                new WfReqElement { Id = 31, Field = ElemFieldType.time.ToString(), StartTime = accessTask.TargetBeginDate, EndTime = accessTask.TargetEndDate },
                new WfReqElement { Id = 32, Field = ElemFieldType.time.ToString(), StartTime = accessTask.TargetBeginDate, EndTime = accessTask.TargetEndDate });

            WfReqElement? result = TimeObjectReferenceResolver.FindParallelTimeElement(
                BuildTasks(accessTask, objectTask), accessTask, null, null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindParallelTimeElement_UsesReferencedObjectTask()
        {
            WfReqTask accessTask = BuildAccessTask();
            accessTask.Id = 100;
            accessTask.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = 200
            });
            WfReqTask objectTask = BuildObjectTask(
                new WfReqElement
                {
                    Id = 61,
                    Field = ElemFieldType.time.ToString(),
                    Name = "Maintenance",
                    StartTime = accessTask.TargetBeginDate?.AddDays(1),
                    EndTime = accessTask.TargetEndDate?.AddDays(1)
                });
            objectTask.Id = 200;

            WfReqElement? result = TimeObjectReferenceResolver.FindParallelTimeElement(
                BuildTasks(accessTask, objectTask), accessTask, null, null);

            Assert.That(result?.Id, Is.EqualTo(61));
        }

        [Test]
        public void FindParallelTimeTask_ReturnsTaskFromStoredReference()
        {
            WfReqTask accessTask = BuildAccessTask();
            accessTask.Id = 100;
            accessTask.Elements.Add(new WfReqElement
            {
                Field = ElemFieldType.time.ToString(),
                ObjectTaskId = 200
            });
            WfReqTask objectTask = BuildObjectTask(new WfReqElement
            {
                Id = 61,
                Field = ElemFieldType.time.ToString(),
                Name = "Maintenance"
            });
            objectTask.Id = 200;

            WfReqTask? result = TimeObjectReferenceResolver.FindParallelTimeTask(
                BuildTasks(accessTask, objectTask), accessTask, null, null);

            Assert.That(result, Is.SameAs(objectTask));
        }

        [Test]
        public void GetSelectorId_UsesRequestObjectIdWhenAvailable()
        {
            WfReqElement element = new() { Id = 41, TimeObjId = -9 };

            Assert.That(TimeObjectReferenceResolver.GetSelectorId(element), Is.EqualTo(-9));
        }

        [Test]
        public void GetSelectorId_UsesStableNegativeElementIdForUnsavedObject()
        {
            WfReqElement element = new() { Id = 42 };

            Assert.That(TimeObjectReferenceResolver.GetSelectorId(element), Is.EqualTo(-42));
        }

        [Test]
        public void GetTicketTimeElements_ExcludesFlowBoundAndNonTimeElements()
        {
            WfReqTask objectTask = BuildObjectTask(
                new WfReqElement { Id = 51, Field = ElemFieldType.time.ToString(), TimeObjId = -1 },
                new WfReqElement { Id = 52, Field = ElemFieldType.time.ToString(), FlowTimeObjectId = 7 },
                new WfReqElement { Id = 53, Field = ElemFieldType.source.ToString(), TimeObjId = -2 });

            List<WfReqTask> tasks = [];
            tasks.Add(objectTask);
            List<WfReqElement> result = [.. TimeObjectReferenceResolver.GetTicketTimeElements(tasks)];

            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(1));
                Assert.That(result.Single().Id, Is.EqualTo(51));
            });
        }

        private static WfReqTask BuildAccessTask()
        {
            return new WfReqTask
            {
                TaskType = WfTaskType.access.ToString(),
                TargetBeginDate = new DateTime(2026, 1, 1),
                TargetEndDate = new DateTime(2026, 2, 1)
            };
        }

        private static WfReqTask BuildObjectTask(params WfReqElement[] elements)
        {
            foreach (WfReqElement element in elements)
            {
                if (string.IsNullOrWhiteSpace(element.Field))
                {
                    element.Field = ElemFieldType.time.ToString();
                }
            }

            return new WfReqTask
            {
                TaskType = WfTaskType.object_create.ToString(),
                Elements = [.. elements]
            };
        }

        private static List<WfReqTask> BuildTasks(WfReqTask accessTask, WfReqTask objectTask)
        {
            List<WfReqTask> tasks = [];
            tasks.Add(accessTask);
            tasks.Add(objectTask);
            return tasks;
        }
    }
}
