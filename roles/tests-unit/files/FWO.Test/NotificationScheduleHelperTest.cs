using FWO.Data;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class NotificationScheduleHelperTest
    {
        [Test]
        public void IsNotificationDue_ReturnsTrueForNoneDeadline()
        {
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.None
            };

            Assert.That(NotificationScheduleHelper.IsNotificationDue(new FwoOwner(), null, notification), Is.True);
        }

        [Test]
        public void IsNotificationDue_ReturnsFalseForInactiveNotification()
        {
            FwoNotification notification = new()
            {
                Active = false,
                Deadline = NotificationDeadline.None
            };

            Assert.That(NotificationScheduleHelper.IsNotificationDue(new FwoOwner(), null, notification), Is.False);
        }

        [Test]
        public void IsNotificationDue_ReturnsTrueForImmediateRequestDeadline()
        {
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RequestDate,
                IntervalBeforeDeadline = SchedulerInterval.Days,
                OffsetBeforeDeadline = 0
            };

            Assert.That(NotificationScheduleHelper.IsNotificationDue(new FwoOwner(), DateTime.Now, notification), Is.True);
        }

        [Test]
        public void IsNotificationDue_ReturnsFalseForFutureRequestDeadline()
        {
            FwoNotification notification = new()
            {
                Deadline = NotificationDeadline.RequestDate,
                IntervalBeforeDeadline = SchedulerInterval.Days,
                OffsetBeforeDeadline = 0
            };

            Assert.That(NotificationScheduleHelper.IsNotificationDue(new FwoOwner(), DateTime.Now.AddDays(1), notification), Is.False);
        }
    }
}
