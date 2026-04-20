using NUnit.Framework;
using NUnit.Framework.Legacy;
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
    }
}
