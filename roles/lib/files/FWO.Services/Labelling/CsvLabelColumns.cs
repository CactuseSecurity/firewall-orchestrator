using FWO.Data.Labelling;

namespace FWO.Services.Labelling
{
    /// <summary>
    /// Optional label columns of the app server csv upload: after the fixed columns, a column named "label:&lt;key&gt;"
    /// (e.g. "label:Stage") holds the value of label &lt;key&gt; for the app server of the line.
    /// </summary>
    /// <remarks>
    /// Without label columns the labels of the uploaded app servers stay untouched. With label columns every uploaded
    /// app server gets the labels of its line, an empty cell meaning no label of that key.
    /// </remarks>
    public static class CsvLabelColumns
    {
        /// <summary>
        /// Prefix of the label columns.
        /// </summary>
        public const string kLabelColumnPrefix = "label:";

        /// <summary>
        /// Returns the label key of every label column of a header by its column number.
        /// </summary>
        /// <param name="header">The columns of the header line.</param>
        /// <param name="fixedColumnCount">Number of fixed columns before the optional ones.</param>
        /// <param name="unknownColumns">Optional columns that are no label column.</param>
        /// <returns>The label keys by column number.</returns>
        public static Dictionary<int, string> Find(string[] header, int fixedColumnCount, out List<string> unknownColumns)
        {
            Dictionary<int, string> labelColumns = [];
            unknownColumns = [];
            for (int columnNo = fixedColumnCount; columnNo < header.Length; columnNo++)
            {
                string column = header[columnNo].Trim().Trim('"').Trim();
                string labelKey = column.StartsWith(kLabelColumnPrefix, StringComparison.OrdinalIgnoreCase)
                    ? column[kLabelColumnPrefix.Length..].Trim()
                    : "";
                if (labelKey.Length > 0)
                {
                    labelColumns[columnNo] = labelKey;
                }
                else if (column.Length > 0)
                {
                    unknownColumns.Add(column);
                }
            }
            return labelColumns;
        }

        /// <summary>
        /// Returns the labels of a csv line, or null if the file has no label columns.
        /// </summary>
        /// <param name="entries">The columns of the line.</param>
        /// <param name="labelColumns">The label keys by column number.</param>
        /// <returns>The labels with a value, null without label columns.</returns>
        public static List<ModellingImportLabel>? GetLabels(string[] entries, Dictionary<int, string> labelColumns)
        {
            if (labelColumns.Count == 0)
            {
                return null;
            }
            List<ModellingImportLabel> labels = [];
            foreach ((int columnNo, string labelKey) in labelColumns)
            {
                string value = columnNo < entries.Length ? entries[columnNo].Trim().Trim('"').Trim() : "";
                if (value.Length > 0)
                {
                    labels.Add(new ModellingImportLabel { Key = labelKey, Value = value });
                }
            }
            return labels;
        }
    }
}
