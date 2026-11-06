namespace FWO.Data.Labelling
{
    /// <summary>
    /// How rule-based reports show the source and destination labels of a rule.
    /// </summary>
    public enum RuleLabelDisplayMode
    {
        /// <summary>
        /// Own columns for source labels and destination labels.
        /// </summary>
        LabelColumns = 0,

        /// <summary>
        /// Labels below the objects in the source and destination cells.
        /// </summary>
        LabelsWithObjects = 2,

        /// <summary>
        /// Objects only, the labels are not shown.
        /// </summary>
        ObjectsOnly = 3
    }
}
