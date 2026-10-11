using FWO.Data.Enums;
using FWO.Logging;

namespace FWO.Services
{
    /// <summary>
    /// Writes the per-rule and per-object messages of the rule_owner mapping, filtered by the configured
    /// level. An installation with many legacy rules that can never be mapped would otherwise get one
    /// message per rule on every run. Import failures, alerts and the per-run summary do not go through
    /// this filter - they are always logged.
    /// </summary>
    public class RuleOwnerMappingLogger
    {
        private const string kLogMessageTitle = "Update rule_owner Notifier";

        /// <summary>
        /// Logger used where no configuration is available, keeping the behaviour of the default setting.
        /// </summary>
        public static RuleOwnerMappingLogger Default { get; } = new(RuleOwnerMappingLogLevel.Warning);

        private readonly RuleOwnerMappingLogLevel configuredLevel;

        /// <summary>
        /// Creates a logger for the configured level.
        /// </summary>
        /// <param name="configuredLevel">Level from the rule owner mapping settings.</param>
        public RuleOwnerMappingLogger(RuleOwnerMappingLogLevel configuredLevel)
        {
            this.configuredLevel = configuredLevel;
        }

        /// <summary>Logs unusable input, for instance an owner network with an invalid IP range.</summary>
        /// <param name="message">Message to log.</param>
        public void Error(string message)
        {
            if (IsEnabled(RuleOwnerMappingLogLevel.Error))
            {
                Log.WriteError(kLogMessageTitle, message);
            }
        }

        /// <summary>Logs a rule or owner network that could not be mapped.</summary>
        /// <param name="message">Message to log.</param>
        public void Warning(string message)
        {
            if (IsEnabled(RuleOwnerMappingLogLevel.Warning))
            {
                Log.WriteWarning(kLogMessageTitle, message);
            }
        }

        /// <summary>Logs informational detail about a single rule.</summary>
        /// <param name="message">Message to log.</param>
        public void Info(string message)
        {
            if (IsEnabled(RuleOwnerMappingLogLevel.Info))
            {
                Log.WriteInfo(kLogMessageTitle, message);
            }
        }

        /// <summary>Logs why a single rule was skipped.</summary>
        /// <param name="message">Message to log.</param>
        public void Debug(string message)
        {
            if (IsEnabled(RuleOwnerMappingLogLevel.Debug))
            {
                Log.WriteDebug(kLogMessageTitle, message);
            }
        }

        /// <summary>
        /// Checks whether messages of the given level are written.
        /// </summary>
        /// <param name="messageLevel">Level of the message.</param>
        /// <returns>True if the configured level covers it.</returns>
        public bool IsEnabled(RuleOwnerMappingLogLevel messageLevel)
        {
            return configuredLevel >= messageLevel;
        }
    }
}
