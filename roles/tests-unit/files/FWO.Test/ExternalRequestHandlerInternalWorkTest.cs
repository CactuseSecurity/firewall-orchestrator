using FWO.Data;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Services;
using FWO.Services.Workflow;
using NUnit.Framework;
using System.Reflection;
using System.Text.Json;

namespace FWO.Test
{
    /// <summary>
    /// Tests of the internal work part of the external request handling: which request tasks form one
    /// internal work batch, when a task counts as configured for internal work, and how the individual
    /// fallback reports and books its deliveries after a bundled flush failed.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class ExternalRequestHandlerInternalWorkTest
    {
        private const long kTicketId = 123;

        private static readonly SimulatedUserConfig kUserConfig = new();

        /// <summary>
        /// Builds a request task, optionally marked as internal work the way the promotion does it.
        /// </summary>
        private static WfReqTask Task(long id, int taskNumber, bool internalWork,
            string taskType = nameof(WfTaskType.access))
        {
            WfReqTask task = new()
            {
                Id = id,
                TaskNumber = taskNumber,
                TicketId = kTicketId,
                TaskType = taskType,
                ManagementId = 1
            };
            if (internalWork)
            {
                task.SetAddInfo(AdditionalInfoKeys.FwConfigChangeTarget, ManagementFwConfigChangeTargets.InternalWork);
            }
            return task;
        }

        private static WfTicket TicketWith(List<WfReqTask> tasks)
        {
            return new WfTicket { Id = kTicketId, Tasks = tasks };
        }

        private static List<WfReqTask> InvokeGetInternalWorkBatch(WfTicket ticket, WfReqTask task)
        {
            MethodInfo method = typeof(ExternalRequestHandler).GetMethod("GetInternalWorkBatch",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(typeof(ExternalRequestHandler).FullName, "GetInternalWorkBatch");
            List<object?> arguments = [ticket, task];
            return (List<WfReqTask>)(method.Invoke(null, arguments.ToArray())
                ?? throw new InvalidOperationException("GetInternalWorkBatch returned null."));
        }

        private static bool InvokeIsInternalWorkConfiguredForTask(ExternalRequestHandler handler, WfReqTask task)
        {
            MethodInfo method = typeof(ExternalRequestHandler).GetMethod("IsInternalWorkConfiguredForTask",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ExternalRequestHandler).FullName, "IsInternalWorkConfiguredForTask");
            List<object?> arguments = [task];
            return (bool)(method.Invoke(handler, arguments.ToArray())
                ?? throw new InvalidOperationException("IsInternalWorkConfiguredForTask returned null."));
        }

        private static async Task<bool> InvokeTrySendPendingIndividually(ExternalRequestHandler handler,
            WorkflowEmailBundleCollector collector)
        {
            MethodInfo method = typeof(ExternalRequestHandler).GetMethod("TrySendPendingInternalWorkEmailsIndividually",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException(typeof(ExternalRequestHandler).FullName, "TrySendPendingInternalWorkEmailsIndividually");
            List<object?> arguments = [kTicketId, collector];
            return await (Task<bool>)(method.Invoke(handler, arguments.ToArray())
                ?? throw new InvalidOperationException("TrySendPendingInternalWorkEmailsIndividually returned null."));
        }

        [Test]
        public void GetInternalWorkBatch_IsEmptyWhenTheTaskIsNotPartOfTheTicket()
        {
            List<WfReqTask> tasks = [Task(1, 1, true)];
            WfTicket ticket = TicketWith(tasks);

            List<WfReqTask> batch = InvokeGetInternalWorkBatch(ticket, Task(99, 9, true));

            Assert.That(batch, Is.Empty);
        }

        [Test]
        public void GetInternalWorkBatch_IsEmptyWhenTheTaskIsNotInternalWork()
        {
            WfReqTask externalTask = Task(1, 1, false);
            List<WfReqTask> tasks = [externalTask];
            WfTicket ticket = TicketWith(tasks);

            List<WfReqTask> batch = InvokeGetInternalWorkBatch(ticket, externalTask);

            Assert.That(batch, Is.Empty);
        }

        [Test]
        public void GetInternalWorkBatch_SpansConsecutiveInternalWorkTasksInBothDirections()
        {
            // The changed task sits in the middle, so the batch has to grow backwards as well as forwards
            // and must stop at the external task on either side.
            WfReqTask changedTask = Task(3, 3, true);
            List<WfReqTask> tasks =
            [
                Task(1, 1, false),
                Task(2, 2, true),
                changedTask,
                Task(4, 4, true),
                Task(5, 5, false)
            ];
            WfTicket ticket = TicketWith(tasks);

            List<WfReqTask> batch = InvokeGetInternalWorkBatch(ticket, changedTask);

            Assert.That(batch.ConvertAll(task => task.TaskNumber), Is.EqualTo(new List<int> { 2, 3, 4 }));
        }

        [Test]
        public void GetInternalWorkBatch_OrdersTasksByTaskNumberRegardlessOfTicketOrder()
        {
            WfReqTask changedTask = Task(2, 2, true);
            List<WfReqTask> tasks = [Task(3, 3, true), Task(1, 1, true), changedTask];
            WfTicket ticket = TicketWith(tasks);

            List<WfReqTask> batch = InvokeGetInternalWorkBatch(ticket, changedTask);

            Assert.That(batch.ConvertAll(task => task.TaskNumber), Is.EqualTo(new List<int> { 1, 2, 3 }));
        }

        [Test]
        public void IsInternalWorkConfiguredForTask_IsFalseForAnUnsupportedTaskType()
        {
            // GetChangeCategory throws for a task type that maps to no change category. The caller has to
            // answer "not internal work" instead of letting that escape into the state change handling.
            using ExternalRequestHandler handler = new(kUserConfig, new ExtTicketHandlerTestApiConn(), null);

            bool configured = InvokeIsInternalWorkConfiguredForTask(handler, Task(1, 1, false, "unsupported_type"));

            Assert.That(configured, Is.False);
        }

        [Test]
        public void IsInternalWorkConfiguredForTask_IsFalseWhenTheManagementSettingsAreNotValidJson()
        {
            SimulatedUserConfig brokenConfig = new() { FwConfigChangeMgmSettings = "{not json" };
            using ExternalRequestHandler handler = new(brokenConfig, new ExtTicketHandlerTestApiConn(), null);

            bool configured = InvokeIsInternalWorkConfiguredForTask(handler, Task(1, 1, false));

            Assert.That(configured, Is.False);
        }

        [Test]
        public void IsInternalWorkConfiguredForTask_IsFalseWhenNoSettingMatchesTheManagement()
        {
            SimulatedUserConfig otherManagementConfig = new()
            {
                FwConfigChangeMgmSettings = JsonSerializer.Serialize(new List<ManagementFwConfigChangeState>
                {
                    new()
                    {
                        Id = 42,
                        Name = "Other",
                        Enabled = true,
                        SelectedChanges = new()
                        {
                            [ManagementFwConfigChangeCategories.RuleChanges] = ManagementFwConfigChangeTargets.InternalWork
                        }
                    }
                })
            };
            using ExternalRequestHandler handler = new(otherManagementConfig, new ExtTicketHandlerTestApiConn(), null);

            bool configured = InvokeIsInternalWorkConfiguredForTask(handler, Task(1, 1, false));

            Assert.That(configured, Is.False);
        }

        [Test]
        public async Task TrySendPendingInternalWorkEmailsIndividually_ReportsSuccessForAnEmptyCollector()
        {
            using ExternalRequestHandler handler = new(kUserConfig, new ExtTicketHandlerTestApiConn(), null);
            WorkflowEmailBundleCollector collector = new();

            bool delivered = await InvokeTrySendPendingIndividually(handler, collector);

            Assert.Multiple(() =>
            {
                Assert.That(delivered, Is.True);
                Assert.That(collector.PendingItems, Is.Empty);
            });
        }

        [Test]
        public async Task TrySendPendingInternalWorkEmailsIndividually_KeepsOnlyTheUndeliveredItemsAndReportsFailure()
        {
            // One bundle group is deliverable, the other carries unparsable action parameters and fails.
            // The alert of the caller counts what is left here, so a delivered group must not stay behind.
            WfStateAction deliverableAction = new()
            {
                Id = 7,
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams { Subject = "Internal work", Body = "done" })
            };
            WfStateAction brokenAction = new() { Id = 8, ExternalParams = "not json" };

            WorkflowEmailBundleCollector collector = new();
            collector.TryAdd(deliverableAction, Task(1, 1, true), null, null);
            collector.TryAdd(brokenAction, Task(2, 2, true), null, null);

            using ExternalRequestHandler handler = new(kUserConfig, new ExtTicketHandlerTestApiConn(), null);

            bool delivered = await InvokeTrySendPendingIndividually(handler, collector);

            Assert.Multiple(() =>
            {
                Assert.That(delivered, Is.False, "a failed group has to be reported to the caller");
                Assert.That(collector.PendingItems, Has.Count.EqualTo(1), "the delivered group has to leave the collector");
                Assert.That(collector.PendingItems[0].RequestTask.TaskNumber, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task TrySendPendingInternalWorkEmailsIndividually_EmptiesTheCollectorWhenEveryGroupIsDelivered()
        {
            WfStateAction deliverableAction = new()
            {
                Id = 7,
                ExternalParams = JsonSerializer.Serialize(new EmailActionParams { Subject = "Internal work", Body = "done" })
            };

            WorkflowEmailBundleCollector collector = new();
            collector.TryAdd(deliverableAction, Task(1, 1, true), null, null);
            collector.TryAdd(deliverableAction, Task(2, 2, true), null, null);

            using ExternalRequestHandler handler = new(kUserConfig, new ExtTicketHandlerTestApiConn(), null);

            bool delivered = await InvokeTrySendPendingIndividually(handler, collector);

            Assert.Multiple(() =>
            {
                Assert.That(delivered, Is.True);
                Assert.That(collector.PendingItems, Is.Empty);
            });
        }
    }
}
