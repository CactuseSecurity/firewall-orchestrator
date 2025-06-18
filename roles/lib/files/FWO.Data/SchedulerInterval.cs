namespace FWO.Data
{
    public enum SchedulerInterval
    {
        Never = 0,
        Days = 2,
        Weeks = 3,
        Months = 4,
        Years = 5,
        Hours = 6,
        Minutes = 7,
        Seconds = 8
    }

    public static class SchedulerIntervalGroups
    {
        public static bool OfferedForReport(this SchedulerInterval schedulerInterval)
        {
            return schedulerInterval switch
            {
                SchedulerInterval.Days or
                SchedulerInterval.Weeks or
                SchedulerInterval.Months or
                SchedulerInterval.Years => true,
                _ => false,
            };
        }

        public static bool OfferedForReportOrNever(this SchedulerInterval schedulerInterval)
        {
            return schedulerInterval switch
            {
                SchedulerInterval.Days or
                SchedulerInterval.Weeks or
                SchedulerInterval.Months or
                SchedulerInterval.Years or
                SchedulerInterval.Never => true,
                _ => false,
            };
        }

        public static bool OfferedForRecert(this SchedulerInterval schedulerInterval)
        {
            return schedulerInterval switch
            {
                SchedulerInterval.Days or
                SchedulerInterval.Weeks or
                SchedulerInterval.Months => true,
                _ => false,
            };
        }
    }
}
