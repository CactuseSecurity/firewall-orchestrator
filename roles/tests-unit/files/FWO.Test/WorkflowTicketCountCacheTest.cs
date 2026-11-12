using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class WorkflowTicketCountCacheTest
    {
        private static Dictionary<string, object> Where(int stateId)
        {
            return new Dictionary<string, object> { ["state_id"] = new Dictionary<string, object> { ["_eq"] = stateId } };
        }

        [Test]
        public void TryGet_Empty_ReturnsFalse()
        {
            WorkflowTicketCountCache cache = new();

            bool cached = cache.TryGet(Where(1), out int count);

            Assert.That((cached, count), Is.EqualTo((false, 0)));
        }

        [Test]
        public void TryGet_SameWhere_ReturnsStoredCount()
        {
            WorkflowTicketCountCache cache = new();
            cache.Store(Where(1), 42);

            bool cached = cache.TryGet(Where(1), out int count);

            Assert.That((cached, count), Is.EqualTo((true, 42)));
        }

        [Test]
        public void TryGet_OtherWhere_ReturnsFalse()
        {
            WorkflowTicketCountCache cache = new();
            cache.Store(Where(1), 42);

            bool cached = cache.TryGet(Where(2), out int count);

            Assert.That((cached, count), Is.EqualTo((false, 0)));
        }

        [Test]
        public void Store_ReplacesPreviousCount()
        {
            WorkflowTicketCountCache cache = new();
            cache.Store(Where(1), 42);
            cache.Store(Where(2), 7);

            Assert.Multiple(() =>
            {
                Assert.That(cache.TryGet(Where(1), out _), Is.False);
                Assert.That(cache.TryGet(Where(2), out int count) ? count : -1, Is.EqualTo(7));
            });
        }

        [Test]
        public void Clear_ForgetsCount()
        {
            WorkflowTicketCountCache cache = new();
            cache.Store(Where(1), 42);

            cache.Clear();

            Assert.That(cache.TryGet(Where(1), out _), Is.False);
        }
    }
}
