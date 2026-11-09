using FWO.Data;
using FWO.Ui.Pages.Monitoring;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    internal class ImportStatusMonitorTest
    {
        [Test]
        public void SetSortPriority_MarksDisabledImport_AsDisabled()
        {
            ImportStatus importStatus = new()
            {
                ImportDisabled = true,
                LastImport =
                [
                    new ImportControl
                    {
                        SuccessfulImport = true,
                        StopTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                    }
                ],
                LastImportAttempt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            };

            ImportStatusMonitor.SetSortPriority(importStatus, 1, 2, new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));

            Assert.That(importStatus.SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioDisabled));
            Assert.That(ImportStatusMonitor.GetTableRowClass(importStatus), Is.EqualTo("background-disabled"));
        }

        [Test]
        public void SetSortPriority_ResetsPreviousPriority_ForHealthyActiveImport()
        {
            ImportStatus importStatus = new()
            {
                ImportDisabled = false,
                SortPrio = ImportStatusMonitor.kSortPrioIssue,
                LastImport =
                [
                    new ImportControl
                    {
                        SuccessfulImport = true,
                        StopTime = new DateTime(2026, 1, 1, 12, 0, 0)
                    }
                ],
                LastImportAttempt = new DateTime(2026, 1, 1, 12, 0, 0)
            };

            ImportStatusMonitor.SetSortPriority(importStatus, 1, 2, new DateTime(2026, 1, 1, 13, 0, 0));

            Assert.That(importStatus.SortPrio, Is.EqualTo(ImportStatusMonitor.kSortPrioOk));
            Assert.That(ImportStatusMonitor.GetTableRowClass(importStatus), Is.Empty);
        }
    }
}
