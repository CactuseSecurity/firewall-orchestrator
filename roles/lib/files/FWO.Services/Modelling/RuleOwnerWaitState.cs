namespace FWO.Services.Modelling
{
    /// <summary>
    /// What a variance analysis has already done about an unusable rule_owner prefilter: waited for
    /// the mapping job, shown the fallback warning and logged fallback reasons. Each analysis has its
    /// own state by default. A caller that runs several owners for one request shares one instance
    /// across their analyses, so the wait, the warning and each log reason happen once per request
    /// instead of once per owner.
    /// </summary>
    public class RuleOwnerWaitState
    {
        /// <summary>
        /// True once the mapping job has been waited for.
        /// </summary>
        public bool WaitDone { get; set; }

        /// <summary>
        /// True once the fallback to the marker query has been shown to the user.
        /// </summary>
        public bool FallbackReported { get; set; }

        /// <summary>
        /// Fallback reasons already written to the database log.
        /// </summary>
        public HashSet<string> LoggedFallbackReasons { get; } = [];
    }
}
