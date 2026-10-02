namespace FWO.Data.Report
{
    /// <summary>
    /// One appearance of a rulebase in the chain of a selected start rulebase (Rules report, management rulebases view).
    /// A rulebase reached from several selected start rulebases is shown once; its later appearances only link to it.
    /// </summary>
    public class RulebaseOccurrence
    {
        /// <summary>
        /// The rulebase (shared by all of its occurrences).
        /// </summary>
        public RulebaseReport Rulebase { get; set; } = new();

        /// <summary>
        /// Id of the selected start rulebase whose chain contains this occurrence.
        /// </summary>
        public int StartRulebaseId { get; set; }

        /// <summary>
        /// True if the rulebase is already shown in the chain of an earlier start rulebase, so only a link to it is shown.
        /// </summary>
        public bool IsRepeated { get; set; }

        /// <summary>
        /// Returns the html id of the shown (first) occurrence of a rulebase, used as link target by repeated occurrences.
        /// </summary>
        public static string GetAnchorId(int managementId, int rulebaseId)
        {
            return $"rulebase-{managementId}-{rulebaseId}";
        }
    }
}
