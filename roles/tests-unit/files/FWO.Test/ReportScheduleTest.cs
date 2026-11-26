using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class ReportScheduleTest
    {
        [Test]
        public void ReportSchedule_DefaultsArchiveToFalse()
        {
            ReportSchedule reportSchedule = new();

            ClassicAssert.IsFalse(reportSchedule.Archive);
        }

        [TestCase(SchedulerInterval.Never, 0, ExpectedResult = true)]
        [TestCase(SchedulerInterval.Never, -1, ExpectedResult = true)]
        [TestCase(SchedulerInterval.Days, 1, ExpectedResult = true)]
        [TestCase(SchedulerInterval.Weeks, 3, ExpectedResult = true)]
        [TestCase(SchedulerInterval.Days, 0, ExpectedResult = false)]
        [TestCase(SchedulerInterval.Weeks, 0, ExpectedResult = false)]
        [TestCase(SchedulerInterval.Months, -1, ExpectedResult = false)]
        [TestCase(SchedulerInterval.Years, int.MinValue, ExpectedResult = false)]
        public bool HasValidRepeatOffset_RequiresPositiveOffsetForRepeatingSchedules(SchedulerInterval interval, int offset)
        {
            ReportSchedule reportSchedule = new() { RepeatInterval = interval, RepeatOffset = offset };

            return reportSchedule.HasValidRepeatOffset();
        }
    }
}
