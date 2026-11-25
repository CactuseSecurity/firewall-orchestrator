using FWO.Data.DeviceSpecificSettings;
using FWO.Data.Labelling;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Rulesets the Guardicore writer puts the rules of a modelled connection in, by positioning algorithm: by default the
    /// ruleset is the external app id of the owner of the connection (e.g. "APP-1234"); with
    /// <see cref="ProvisioningPositioningAlgorithm.GuardicoreEndOfAppRulesetDistinguishCommonServices"/> it is the value of
    /// the App label of the owner by the configured App label value format (e.g. "Exchange (COM-1611)" for "{name} ({id})"),
    /// for common services with the configured prefix (e.g. "ComSvc Exchange (COM-1611)"). A ruleset that does not exist
    /// yet is created with its first rule, and a new rule is appended at its end. The importer shows each ruleset as rulebase
    /// "04 Allow &lt;ruleset&gt;", common service rulesets first.
    /// </summary>
    public static class GuardicoreRulesets
    {
        /// <summary>Default prefix of the rulesets of common services.</summary>
        public const string kDefaultCommonServicePrefix = "ComSvc";

        /// <summary>Maximum length of the prefix of the rulesets of common services.</summary>
        public const int kMaxCommonServicePrefixLength = 50;

        /// <summary>The ruleset of the rules of a modelled connection.</summary>
        /// <param name="algorithm">Positioning algorithm in effect for the management.</param>
        /// <param name="app">The app (owner) of the connection and whether the connection is a common service.</param>
        /// <param name="format">Configured App label value format and prefix of the rulesets of common services; null for the defaults.</param>
        /// <exception cref="ArgumentException">Thrown for an app without external app id.</exception>
        public static string Name(ProvisioningPositioningAlgorithm algorithm, GuardicoreRulesetApp app, GuardicoreRulesetFormat? format)
        {
            ArgumentNullException.ThrowIfNull(app);
            string appId = app.ExtAppId?.Trim() ?? "";
            if (appId.Length == 0)
            {
                throw new ArgumentException("The app of the connection has no external app id, which names its ruleset.", nameof(app));
            }
            if (algorithm != ProvisioningPositioningAlgorithm.GuardicoreEndOfAppRulesetDistinguishCommonServices)
            {
                return appId;
            }
            string name = new LabelValueFormat(format?.AppValueFormat).Value(app.Name, appId);
            return app.IsCommonService ? $"{EffectivePrefix(format?.CommonServicePrefix)} {name}" : name;
        }

        /// <summary>The configured prefix of the rulesets of common services, trimmed, or the default if it is empty.</summary>
        public static string EffectivePrefix(string? commonServicePrefix)
        {
            string prefix = commonServicePrefix?.Trim() ?? "";
            return prefix.Length == 0 ? kDefaultCommonServicePrefix : prefix;
        }

        /// <summary>
        /// The ruleset of an imported rule of the Allow section from the name of its rulebase ("04 Allow &lt;ruleset&gt;"),
        /// empty for a rule without ruleset; null for a rulebase of another section or without name.
        /// </summary>
        public static string? RulesetOfRulebase(string? rulebaseName)
        {
            string name = rulebaseName ?? "";
            if (!GuardicoreWriteDefaults.IsAllowRulebase(name))
            {
                return null;
            }
            return name.Length > GuardicoreWriteDefaults.kAllowRulebaseName.Length ? name[(GuardicoreWriteDefaults.kAllowRulebaseName.Length + 1)..] : "";
        }
    }

    /// <summary>The app (owner) of a modelled connection, as far as it names the ruleset of the rules of the connection.</summary>
    /// <param name="Name">Name of the app.</param>
    /// <param name="ExtAppId">External app id of the app.</param>
    /// <param name="IsCommonService">Whether the connection is a common service of the app.</param>
    public sealed record GuardicoreRulesetApp(string? Name, string? ExtAppId, bool IsCommonService);

    /// <summary>The settings that name the app rulesets, see <see cref="GuardicoreRulesets.Name"/>.</summary>
    /// <param name="AppValueFormat">Configured format of the App label values (appLabelValueFormat), e.g. "{name} ({id})"; empty or invalid for the default "{id}".</param>
    /// <param name="CommonServicePrefix">Configured prefix of the rulesets of common services (modComSvcSectionPrefix); empty for the default.</param>
    public sealed record GuardicoreRulesetFormat(string? AppValueFormat, string? CommonServicePrefix);
}
