using NUnit.Framework;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class WorkflowEmailBundleStoreTest
    {
        private const string kBundleId = "0123456789abcdef0123456789abcdef";
        private const string kOtherBundleId = "fedcba9876543210fedcba9876543210";
        private const string kCallerDn = "uid=requester,ou=operator,ou=user";
        private const string kOtherCallerDn = "uid=approver,ou=operator,ou=user";
        private const long kTicketId = 42;

        private static WfStateAction TestAction => new() { Id = 7, ExternalParams = "params" };

        private static WfReqTask TestRequestTask => new()
        {
            Id = 11,
            TicketId = kTicketId,
            TaskNumber = 1,
            StateId = 60,
            TaskType = WfTaskType.access.ToString()
        };

        [Test]
        public void GetOrCreate_ReturnsSameCollectorForSameTicketAndBundle()
        {
            WorkflowEmailBundleStore store = new();

            WorkflowEmailBundleCollector? first = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);
            WorkflowEmailBundleCollector? second = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.SameAs(first));
            });
        }

        [Test]
        public void GetOrCreate_SeparatesBundlesByTicketId()
        {
            WorkflowEmailBundleStore store = new();

            WorkflowEmailBundleCollector? ticketOne = store.GetOrCreate(1, kBundleId, kCallerDn);
            WorkflowEmailBundleCollector? ticketTwo = store.GetOrCreate(2, kBundleId, kCallerDn);

            Assert.That(ticketTwo, Is.Not.SameAs(ticketOne));
        }

        [Test]
        public void GetOrCreate_SeparatesBundlesByBundleId()
        {
            WorkflowEmailBundleStore store = new();

            WorkflowEmailBundleCollector? first = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);
            WorkflowEmailBundleCollector? second = store.GetOrCreate(kTicketId, kOtherBundleId, kCallerDn);

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void GetOrCreate_RefusesBundleOfAnotherCaller()
        {
            WorkflowEmailBundleStore store = new();
            store.GetOrCreate(kTicketId, kBundleId, kCallerDn);

            WorkflowEmailBundleCollector? foreignCollector = store.GetOrCreate(kTicketId, kBundleId, kOtherCallerDn);

            Assert.That(foreignCollector, Is.Null);
        }

        [Test]
        public void Get_RefusesBundleOfAnotherCaller()
        {
            WorkflowEmailBundleStore store = new();
            store.GetOrCreate(kTicketId, kBundleId, kCallerDn);

            Assert.Multiple(() =>
            {
                Assert.That(store.Get(kTicketId, kBundleId, kOtherCallerDn), Is.Null);
                Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Not.Null);
            });
        }

        [Test]
        public void Get_DoesNotCreateMissingBundle()
        {
            WorkflowEmailBundleStore store = new();

            Assert.Multiple(() =>
            {
                Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Null);
                Assert.That(store.Sweep().DiscardedItems, Is.Zero);
            });
        }

        [Test]
        public void Remove_DropsBundleSoItIsNotFoundAgain()
        {
            WorkflowEmailBundleStore store = new();
            store.GetOrCreate(kTicketId, kBundleId, kCallerDn);

            store.Remove(kTicketId, kBundleId);

            Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Null);
        }

        [Test]
        public void Sweep_KeepsFreshBundlesAndReportsNothing()
        {
            WorkflowEmailBundleStore store = new();
            WorkflowEmailBundleCollector? collector = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);
            collector!.TryAdd(TestAction, TestRequestTask, null, null);

            WorkflowEmailBundleSweepResult sweepResult = store.Sweep();

            Assert.Multiple(() =>
            {
                Assert.That(sweepResult.LostEmails, Is.False);
                Assert.That(sweepResult.DiscardedBundles, Is.Zero);
                Assert.That(sweepResult.DiscardedItems, Is.Zero);
                Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Not.Null);
            });
        }

        [Test]
        public void Sweep_ReportsCapturedEmailsOfExpiredBundle()
        {
            WorkflowEmailBundleStore store = new();
            WorkflowEmailBundleCollector? collector = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);
            collector!.TryAdd(TestAction, TestRequestTask, null, null);
            collector.TryAdd(TestAction, TestRequestTask, null, null);
            ExpireCollector(collector);

            WorkflowEmailBundleSweepResult sweepResult = store.Sweep();

            Assert.Multiple(() =>
            {
                Assert.That(sweepResult.LostEmails, Is.True);
                Assert.That(sweepResult.DiscardedBundles, Is.EqualTo(1));
                Assert.That(sweepResult.DiscardedItems, Is.EqualTo(2));
                Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Null);
            });
        }

        [Test]
        public void Sweep_EvictsExpiredEmptyBundleWithoutReportingLostEmails()
        {
            WorkflowEmailBundleStore store = new();
            WorkflowEmailBundleCollector? collector = store.GetOrCreate(kTicketId, kBundleId, kCallerDn);
            ExpireCollector(collector!);

            WorkflowEmailBundleSweepResult sweepResult = store.Sweep();

            Assert.Multiple(() =>
            {
                Assert.That(sweepResult.LostEmails, Is.False);
                Assert.That(sweepResult.DiscardedBundles, Is.Zero);
                Assert.That(store.Get(kTicketId, kBundleId, kCallerDn), Is.Null);
            });
        }

        [Test]
        public void TryAdd_RefusesFurtherEmailsBeyondBundleCapacity()
        {
            WorkflowEmailBundleCollector collector = new(kCallerDn);
            for (int itemCount = 0; itemCount < WorkflowEmailBundleStore.kMaxPendingItemsPerBundle; ++itemCount)
            {
                Assert.That(collector.TryAdd(TestAction, TestRequestTask, null, null), Is.True);
            }

            Assert.Multiple(() =>
            {
                Assert.That(collector.TryAdd(TestAction, TestRequestTask, null, null), Is.False);
                Assert.That(collector.PendingItems, Has.Count.EqualTo(WorkflowEmailBundleStore.kMaxPendingItemsPerBundle));
            });
        }

        [Test]
        public void BelongsTo_TreatsUnboundCollectorAsUsableByAnyCaller()
        {
            WorkflowEmailBundleCollector unboundCollector = new();

            Assert.Multiple(() =>
            {
                Assert.That(unboundCollector.CallerDn, Is.Empty);
                Assert.That(unboundCollector.BelongsTo(kCallerDn), Is.True);
                Assert.That(unboundCollector.BelongsTo(kOtherCallerDn), Is.True);
            });
        }

        private static void ExpireCollector(WorkflowEmailBundleCollector collector)
        {
            typeof(WorkflowEmailBundleCollector)
                .GetProperty(nameof(WorkflowEmailBundleCollector.LastTouchedAt))!
                .SetValue(collector, DateTime.UtcNow.AddHours(-2));
        }
    }
}
