namespace FWO.Data.Enums
{
    /// <summary>
    /// How much per-rule and per-object detail the rule_owner mapping writes to the middleware log.
    /// Installations with many legacy rules that can never be mapped would otherwise produce a message
    /// per rule on every run.
    /// <para>
    /// Always logged and never affected by this setting: the summary of every run, failed imports,
    /// and every alert. The setting applies to the rule_owner mapping only and does not change the
    /// log level of any other component.
    /// </para>
    /// <para>
    /// When adding a message, place it by these criteria rather than by how the message feels:
    /// how many of them can occur in one run, and whether someone has to act on it.
    /// </para>
    /// </summary>
    public enum RuleOwnerMappingLogLevel
    {
        /// <summary>
        /// No message about single rules or objects at all.
        /// </summary>
        None = 0,

        /// <summary>
        /// The configuration itself is unusable, so mapping cannot work as configured. Few messages per
        /// run and always worth acting on, for instance an owner network with an invalid IP range or a
        /// missing modelled marker.
        /// </summary>
        Error = 1,

        /// <summary>
        /// Additionally rules that stay unmapped although they were meant to be mapped, which is a real
        /// defect someone has to fix - for instance a marker pointing to a connection without an active
        /// owner. Bounded by the number of rules that actually have a problem. This is the default.
        /// </summary>
        Warning = 2,

        /// <summary>
        /// Additionally cases that are plausible in normal operation but useful while investigating a
        /// single rule, for instance a rule without any network object.
        /// </summary>
        Info = 3,

        /// <summary>
        /// Everything, including the cases that are the norm on a grown installation, such as every rule
        /// that was never modelled. Highest volume by far.
        /// </summary>
        Debug = 4
    }
}
