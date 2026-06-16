using FWO.Data;

namespace FWO.Ui.Pages.Monitoring
{
    public static class ImportStatusMonitor
    {
        public const int kSortPrioDisabled = -1;
        public const int kSortPrioOk = 0;
        public const int kSortPrioRunning = 1;
        public const int kSortPrioIssue = 2;

        /// <summary>
        /// Calculates the UI sort priority for a management import status row.
        /// </summary>
        /// <param name="importStatus">The import status row to update.</param>
        /// <param name="maxImportDuration">Maximum expected import duration in hours.</param>
        /// <param name="maxImportInterval">Maximum expected import interval in hours.</param>
        /// <param name="now">The current timestamp used for age checks.</param>
        public static void SetSortPriority(ImportStatus importStatus, int maxImportDuration, int maxImportInterval, DateTime now)
        {
            importStatus.SortPrio = kSortPrioOk;

            if (importStatus.ImportDisabled)
            {
                importStatus.SortPrio = kSortPrioDisabled;
                return;
            }

            if (importStatus.LastIncompleteImport != null && importStatus.LastIncompleteImport.Length > 0)
            {
                importStatus.SortPrio = kSortPrioRunning;
                if (importStatus.LastIncompleteImport[0].StartTime < now.AddHours(-maxImportDuration))
                {
                    importStatus.SortPrio = kSortPrioIssue;
                }
            }
            else if (
                    (importStatus.LastImport != null && importStatus.LastImport.Length > 0 && !importStatus.LastImport[0].SuccessfulImport)
                    || importStatus.LastImport == null
                    || importStatus.LastImport.Length == 0
                    || importStatus.LastImportAttempt != null && importStatus.LastImportAttempt < now.AddHours(-maxImportInterval)
                    )
            {
                importStatus.SortPrio = kSortPrioIssue;
            }
        }

        /// <summary>
        /// Gets the CSS class for an import status row.
        /// </summary>
        /// <param name="importStatus">The import status row to display.</param>
        /// <returns>The row CSS class, or an empty string for normal rows.</returns>
        public static string GetTableRowClass(ImportStatus importStatus)
        {
            return importStatus.SortPrio switch
            {
                kSortPrioDisabled => "background-disabled",
                kSortPrioRunning => "background-upcoming",
                kSortPrioIssue => "background-overdue",
                _ => ""
            };
        }
    }
}
