using FWO.Basics;
using FWO.Data;

namespace FWO.Services
{
    /// <summary>
    /// Evaluates notification schedules against the current date.
    /// </summary>
    public static class NotificationScheduleHelper
    {
        /// <summary>
        /// Returns whether the notification is due for sending at the current time.
        /// </summary>
        /// <param name="owner">Owner context used for owner-based deadlines.</param>
        /// <param name="extDeadline">External deadline date, for example a request creation time.</param>
        /// <param name="notification">Notification to evaluate.</param>
        /// <returns>True when the notification should be sent now.</returns>
        public static bool IsNotificationDue(FwoOwner? owner, DateTime? extDeadline, FwoNotification notification)
        {
            if (!notification.Active)
            {
                return false;
            }

            if (notification.Deadline == NotificationDeadline.None)
            {
                return true;
            }

            if (notification.Deadline == NotificationDeadline.RequestDate
                && extDeadline?.Date == DateTime.Now.Date
                && notification.OffsetBeforeDeadline == 0)
            {
                return notification.LastSent == null || notification.LastSent.Value.Date < DateTime.Now.Date;
            }

            DateTime deadline = GetDeadlineDate(notification.Deadline, owner, extDeadline);
            return deadline.Date >= DateTime.Now.Date
                ? IsNotificationDueBeforeDeadline(deadline, notification)
                : IsNotificationDueAfterDeadline(deadline, notification);
        }

        private static bool IsTimeToSend(DateTime? lastSent, DateTime notifDate)
        {
            return (lastSent == null || ((DateTime)lastSent).Date < notifDate.Date) && notifDate.Date <= DateTime.Now.Date;
        }

        private static bool TryGetConfiguredInterval(SchedulerInterval? interval, out SchedulerInterval configuredInterval)
        {
            if (interval != null)
            {
                configuredInterval = (SchedulerInterval)interval;
                return true;
            }

            // Before- and after-deadline schedules are optional. An unset interval
            // disables only the corresponding phase; it is not an error.
            configuredInterval = default;
            return false;
        }

        private static bool IsNotificationDueBeforeDeadline(DateTime deadline, FwoNotification notification)
        {
            if (!TryGetConfiguredInterval(notification.IntervalBeforeDeadline, out SchedulerInterval intervalBeforeDeadline))
            {
                return false;
            }

            DateTime notifDate = ApplyIntervalOffset(deadline, intervalBeforeDeadline, -(int)(notification.OffsetBeforeDeadline ?? 0));
            return IsTimeToSend(notification.LastSent, notifDate);
        }

        private static bool IsNotificationDueAfterDeadline(DateTime deadline, FwoNotification notification)
        {
            if (!TryGetConfiguredInterval(notification.RepeatIntervalAfterDeadline, out SchedulerInterval repeatIntervalAfterDeadline))
            {
                return false;
            }

            DateTime nextNotifDate = ApplyIntervalOffset(deadline.Date, repeatIntervalAfterDeadline, (int)(notification.InitialOffsetAfterDeadline ?? 0));
            DateTime currentNotifDate = nextNotifDate;
            int counter = -1;
            while (nextNotifDate <= DateTime.Now.Date && counter++ <= notification.RepetitionsAfterDeadline)
            {
                currentNotifDate = nextNotifDate;
                nextNotifDate = ApplyIntervalOffset(nextNotifDate, repeatIntervalAfterDeadline, (int)(notification.RepeatOffsetAfterDeadline ?? 0));
            }

            return counter <= notification.RepetitionsAfterDeadline && IsTimeToSend(notification.LastSent, currentNotifDate);
        }

        private static DateTime GetDeadlineDate(NotificationDeadline deadline, FwoOwner? owner, DateTime? extDeadline)
        {
            if (deadline == NotificationDeadline.RecertDate && owner?.NextRecertDate != null)
            {
                return (DateTime)owner.NextRecertDate;
            }
            else if (deadline == NotificationDeadline.RequestDate && extDeadline != null)
            {
                return (DateTime)extDeadline;
            }
            else if (deadline == NotificationDeadline.RuleExpiry && extDeadline != null)
            {
                return (DateTime)extDeadline;
            }
            else if (deadline == NotificationDeadline.DecommissionDate && owner?.DecommDate != null)
            {
                return (DateTime)owner.DecommDate;
            }
            return DateTime.Now;
        }

        private static DateTime ApplyIntervalOffset(DateTime dateTime, SchedulerInterval interval, long value)
        {
            return interval switch
            {
                SchedulerInterval.Minutes => dateTime.AddMinutes(value),
                SchedulerInterval.Hours => dateTime.AddHours(value),
                SchedulerInterval.Days => dateTime.AddDays(value),
                SchedulerInterval.Weeks => dateTime.AddDays(value * GlobalConst.kDaysPerWeek),
                SchedulerInterval.Months => dateTime.AddMonths((int)value),
                _ => throw new NotSupportedException("Time interval is not supported.")
            };
        }
    }
}
