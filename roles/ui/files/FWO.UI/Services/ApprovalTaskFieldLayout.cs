namespace FWO.Ui.Services
{
    /// <summary>
    /// Column layout of the access task view. Fields use a grid of 3 columns (of 12); extendable fields
    /// take up to 3 additional columns left free by fields hidden in the same row.
    /// </summary>
    public static class ApprovalTaskFieldLayout
    {
        private const int kGridColumns = 12;
        private const int kFieldColumns = 3;
        private const int kMaxExtension = 3;

        /// <summary>Column classes of a field with the standard width.</summary>
        public static readonly string StandardColumnClass = ColumnClass(kFieldColumns);

        /// <summary>Column classes of a field with double width.</summary>
        public static readonly string DoubleColumnClass = ColumnClass(kFieldColumns + kMaxExtension);

        /// <summary>
        /// Returns the column classes for the given number of grid columns on extra large screens.
        /// </summary>
        /// <param name="columns">Number of grid columns.</param>
        public static string ColumnClass(int columns)
        {
            return $"col-xl-{columns} col-lg-6 mb-2";
        }

        /// <summary>
        /// Returns the widths of the shown extendable fields of a row in their order. Each starts with the standard
        /// width and takes the remaining free columns of the row up to double width; earlier fields are served first.
        /// </summary>
        /// <param name="standardFields">Number of shown fields of the row with fixed standard width.</param>
        /// <param name="extendableFields">Number of shown extendable fields of the row.</param>
        public static List<int> ExtendableWidths(int standardFields, int extendableFields)
        {
            int freeColumns = kGridColumns - kFieldColumns * (standardFields + extendableFields);
            List<int> widths = [];
            for (int field = 0; field < extendableFields; field++)
            {
                int extension = Math.Clamp(freeColumns, 0, kMaxExtension);
                freeColumns -= extension;
                widths.Add(kFieldColumns + extension);
            }
            return widths;
        }
    }
}
