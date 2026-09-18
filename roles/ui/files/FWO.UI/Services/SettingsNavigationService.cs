using FWO.Basics;
using FWO.Config.Api;
using System.Globalization;
using System.Text;

namespace FWO.Ui.Services
{
    /// <summary>
    /// One selectable page inside a section of the settings sidebar.
    /// </summary>
    /// <param name="TextKey">Localization key of the label shown to the user.</param>
    /// <param name="Href">Route the navigation link points to.</param>
    /// <param name="Icon">Icon css classes rendered ahead of the label.</param>
    /// <param name="Roles">Comma separated roles allowed to see the entry, empty when every user may see it.</param>
    /// <param name="MatchAll">True when the link is only marked active on an exact route match.</param>
    /// <param name="InternalUsersOnly">True when only users of the internal ldap may see the entry.</param>
    public record SettingsNavEntry(
        string TextKey,
        string Href,
        string Icon,
        string Roles = "",
        bool MatchAll = false,
        bool InternalUsersOnly = false);

    /// <summary>
    /// One headed group of settings pages in the sidebar navigation.
    /// </summary>
    /// <param name="TextKey">Localization key of the section heading.</param>
    /// <param name="Entries">Pages listed below the heading, in display order.</param>
    /// <param name="Roles">Comma separated roles allowed to see the heading, empty when every user may see it.</param>
    /// <param name="TooltipKey">Localization key of the heading tooltip, empty when the heading carries none.</param>
    public record SettingsNavSection(
        string TextKey,
        IReadOnlyList<SettingsNavEntry> Entries,
        string Roles = "",
        string TooltipKey = "");

    /// <summary>
    /// Holds the settings sidebar navigation and narrows it down to the entries matching a search term.
    /// Role visibility is deliberately not decided here, it stays with ExecutionModeAuthorizeView in the markup.
    /// </summary>
    public static class SettingsNavigationService
    {
        private const string kDeviceRoles = $"{Roles.Admin}, {Roles.Importer}, {Roles.Auditor}, {Roles.FwAdmin}";
        private const string kAdminAuditorFwAdmin = $"{Roles.Admin}, {Roles.Auditor}, {Roles.FwAdmin}";
        private const string kAdminAuditor = $"{Roles.Admin}, {Roles.Auditor}";
        private const string kAdminOnly = Roles.Admin;

        private static readonly List<SettingsNavEntry> kDeviceEntries = new()
        {
            new("readonly_credential", "settings/credentials", Icons.Credential, kAdminAuditorFwAdmin),
            new("managements", "settings/managements", Icons.Management, kAdminAuditorFwAdmin),
            new("gateways", "settings/gateways", Icons.Gateway, kAdminAuditorFwAdmin)
        };

        private static readonly List<SettingsNavEntry> kTopologyEntries = new()
        {
            new("matrix", "/settings/matrix", Icons.Matrix, kAdminAuditorFwAdmin),
            new("internet", "/settings/internet", Icons.Network, kAdminAuditorFwAdmin)
        };

        private static readonly List<SettingsNavEntry> kAuthorizationEntries = new()
        {
            new("ldap_conns", "settings/ldap", Icons.Ldap, kAdminAuditor),
            new("tenants", "settings/tenants", Icons.Tenant, kAdminAuditorFwAdmin),
            new("users", "settings/users", Icons.User, kAdminAuditor),
            new("groups", "settings/groups", Icons.UserGroup, kAdminAuditor),
            new("roles", "settings/roles", Icons.Role, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kOwnerEntries = new()
        {
            new("owners", "settings/owners", Icons.Owner, kAdminAuditor, MatchAll: true),
            new("responsibles", "settings/owners/responsibles", Icons.Owner, kAdminAuditor),
            new("lifecycle_states", "settings/owners/lifecycles", Icons.Owner, kAdminAuditor),
            new("import_app_data", "settings/owners/appdataimport", Icons.Import, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kModuleEntries = new()
        {
            new("reporting", "settings/reportgeneral", Icons.Reporting, kAdminAuditor),
            new("recertification", "settings/recertificationgeneral", Icons.Recertification, kAdminAuditor),
            new("compliance", "settings/compliance", Icons.Compliance, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kModellingEntries = new()
        {
            new("modelling_general_settings", "settings/modelling", Icons.Modelling, kAdminAuditor),
            new("notifications", "settings/modellingnotifications", Icons.Email, kAdminAuditor),
            new("logging", "settings/logging", Icons.Import, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kWorkflowEntries = new()
        {
            new("state_actions", "settings/stateactions", Icons.Action, kAdminAuditor),
            new("state_definitions", "settings/statedefinitions", Icons.State, kAdminAuditor),
            new("state_matrix", "settings/statematrix", Icons.Matrix, kAdminAuditor),
            new("customizing", "settings/workflowcustomizing", Icons.Settings, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kFlowEntries = new()
        {
            new("flow_general_settings", "settings/flows/general", Icons.Settings, kAdminOnly),
            new("network_objects", "settings/flows/networkobjects", Icons.Settings, kAdminOnly),
            new("network_groups", "settings/flows/networkgroups", Icons.Settings, kAdminOnly),
            new("service_objects", "settings/flows/serviceobjects", Icons.Settings, kAdminOnly),
            new("service_groups", "settings/flows/servicegroups", Icons.Settings, kAdminOnly),
            new("time_objects", "settings/flows/timeobjects", Icons.Settings, kAdminOnly)
        };

        private static readonly List<SettingsNavEntry> kDefaultEntries = new()
        {
            new("standards", "settings/defaults", Icons.Settings, kAdminAuditor),
            new("email_settings", "settings/email", Icons.Email, kAdminAuditor),
            new("importer_settings", "settings/importer", Icons.Import, kAdminAuditor),
            new("change_trigger", "settings/changetrigger", Icons.Check, kAdminAuditor),
            new("notifications", "settings/notifications", Icons.Email, kAdminAuditor),
            new("password_policy", "settings/passwordpolicy", Icons.Policy, kAdminAuditor),
            new("customize_texts", "settings/customtexts", Icons.Text, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kFwConfigChangeEntries = new()
        {
            new("fwconfigchangegeneral", "settings/fwconfigchangegeneral", Icons.Settings, kAdminAuditor),
            new("ext_ticket_templates", "settings/exttickettemplates", Icons.Settings, kAdminAuditor)
        };

        private static readonly List<SettingsNavEntry> kPersonalEntries = new()
        {
            new("password", "settings/password", Icons.Login, InternalUsersOnly: true),
            new("personal_settings", "settings/personal", Icons.Settings)
        };

        private static readonly List<SettingsNavSection> kSections = new()
        {
            new("devices", kDeviceEntries, kDeviceRoles, "U5011"),
            new("network_topology", kTopologyEntries, kAdminAuditorFwAdmin, "U5329"),
            new("authorization", kAuthorizationEntries, kAdminAuditorFwAdmin, "U5012"),
            new("owners", kOwnerEntries, kAdminAuditor),
            new("modules", kModuleEntries, kAdminAuditor, "U5017"),
            new("modelling", kModellingEntries, kAdminAuditor),
            new("workflow", kWorkflowEntries, kAdminAuditor, "U5015"),
            new("flow", kFlowEntries, kAdminOnly, "U5011"),
            new("defaults", kDefaultEntries, kAdminAuditor, "U5013"),
            new("fwconfigchange", kFwConfigChangeEntries, kAdminAuditor, "U5013"),
            new("personal", kPersonalEntries, TooltipKey: "U5014")
        };

        /// <summary>
        /// Returns the sidebar sections whose localized heading or entry labels match the given search term.
        /// A matching heading keeps all of its entries, so that a section can be found by its own name.
        /// An empty or whitespace only term returns the complete navigation.
        /// </summary>
        /// <param name="userConfig">Config used to resolve the labels into the language of the current user.</param>
        /// <param name="searchTerm">Term typed by the user, matched case and diacritic insensitively.</param>
        /// <returns>The matching sections in display order, each holding only its matching entries.</returns>
        public static IReadOnlyList<SettingsNavSection> GetSections(UserConfig userConfig, string? searchTerm)
        {
            string normalizedTerm = NormalizeForSearch(searchTerm ?? string.Empty);
            if (normalizedTerm.Length == 0)
            {
                return kSections;
            }

            List<SettingsNavSection> matching = new();
            foreach (SettingsNavSection section in kSections)
            {
                bool headingMatches = Matches(userConfig, section.TextKey, normalizedTerm);
                IReadOnlyList<SettingsNavEntry> entries = headingMatches
                    ? section.Entries
                    : section.Entries.Where(entry => Matches(userConfig, entry.TextKey, normalizedTerm)).ToList();
                if (headingMatches || entries.Count > 0)
                {
                    matching.Add(section with { Entries = entries });
                }
            }
            return matching;
        }

        /// <summary>
        /// Lower cases the given text and strips diacritics, so that a term typed without umlauts
        /// still finds a localized label carrying them, for example "Uberwachung" finding "Überwachung".
        /// </summary>
        /// <param name="text">Text to fold into its comparable form.</param>
        /// <returns>The trimmed, lower cased text without diacritical marks.</returns>
        public static string NormalizeForSearch(string text)
        {
            StringBuilder stripped = new();
            foreach (char character in text.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    stripped.Append(character);
                }
            }
            return stripped.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// <summary>
        /// Checks whether the localized text behind the given key contains the already normalized search term.
        /// </summary>
        private static bool Matches(UserConfig userConfig, string textKey, string normalizedTerm)
        {
            return NormalizeForSearch(userConfig.GetText(textKey)).Contains(normalizedTerm);
        }
    }
}
