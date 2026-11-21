namespace FWO.Data.Labelling
{
    /// <summary>
    /// Writers of the labels of rules as a whole (labelling.rule_label.label_source). Each writer reads and closes only
    /// its own assignments, so labels written by others are kept. The importer writes with the source "import", which
    /// the API presets for it.
    /// </summary>
    public static class RuleLabelSource
    {
        /// <summary>The App labels kept in line with the rule owner mapping by RuleAppLabelSync.</summary>
        public const string kAppSync = "app_sync";
    }
}
