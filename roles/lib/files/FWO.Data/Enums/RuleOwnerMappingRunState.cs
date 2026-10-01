namespace FWO.Data.Enums
{
    /// <summary>
    /// How the result of one full rule_owner reinitialize has to be read. Only <see cref="Drift"/> points at
    /// a problem of the incremental mapping; every other state explains the difference by something else.
    /// <para>
    /// Lives here rather than beside the monitoring page because the middleware decides the same question
    /// when it raises the drift alert. Both read it off the run, so the alert and the page can no longer
    /// disagree about the same run - which they did while the rule was written out twice.
    /// </para>
    /// </summary>
    public enum RuleOwnerMappingRunState
    {
        /// <summary>Rebuilt state matches the stored one, nothing to do.</summary>
        InSync,

        /// <summary>Imports were still waiting to be mapped, so the difference is just the backlog.</summary>
        ImportsPending,

        /// <summary>The run followed a deliberate change, so a different result is expected.</summary>
        ChangeApplied,

        /// <summary>
        /// The configured mapping source matched no rule at all, so every mapping was removed. Almost always
        /// a misconfigured source rather than a failure of the incremental mapping, and it carries its own
        /// more precise alert.
        /// </summary>
        EmptyResult,

        /// <summary>The incremental mapping missed the listed changes.</summary>
        Drift
    }
}
