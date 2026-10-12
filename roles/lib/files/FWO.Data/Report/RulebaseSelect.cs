using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FWO.Data.Report
{
    /// <summary>
    /// Start rulebase selected for a Rules report in management rulebases view (persisted in report templates).
    /// </summary>
    public class SelectedRulebase
    {
        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int ManagementId { get; set; }

        [JsonProperty("mgm_name"), JsonPropertyName("mgm_name")]
        public string ManagementName { get; set; } = "";

        [JsonProperty("rulebase_id"), JsonPropertyName("rulebase_id")]
        public int RulebaseId { get; set; }

        [JsonProperty("rulebase_name"), JsonPropertyName("rulebase_name")]
        public string RulebaseName { get; set; } = "";

        /// <summary>
        /// Returns the display text "management: rulebase".
        /// </summary>
        public override string ToString()
        {
            return $"{ManagementName}: {RulebaseName}";
        }
    }

    /// <summary>
    /// Management with its start rulebases offered for selection in management rulebases view.
    /// </summary>
    public class RulebaseManagementSelect
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("rulebases"), JsonPropertyName("rulebases")]
        public List<RulebaseSelect> Rulebases { get; set; } = [];

        public bool Visible { get; set; } = true;

        /// <summary>
        /// Reduces each management to its start rulebases and drops managements without any start rulebase.
        /// </summary>
        /// <param name="managements">managements with all active rulebases and their incoming links</param>
        /// <returns>managements containing start rulebases only</returns>
        public static List<RulebaseManagementSelect> KeepStartRulebases(List<RulebaseManagementSelect> managements)
        {
            foreach (RulebaseManagementSelect management in managements)
            {
                management.Rulebases = [.. management.Rulebases.Where(rulebase => rulebase.IsStartRulebase(management.Id))];
            }
            return [.. managements.Where(management => management.Rulebases.Count > 0)];
        }
    }

    /// <summary>
    /// Rulebase offered for selection in management rulebases view.
    /// </summary>
    public class RulebaseSelect
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Active links pointing to this rulebase.
        /// </summary>
        [JsonProperty("rulebase_links"), JsonPropertyName("rulebase_links")]
        public List<RulebaseLink> IncomingLinks { get; set; } = [];

        /// <summary>
        /// A start rulebase is not reached from another rulebase or rule of the same management.
        /// Links from other managements (e.g. global policies) and initial gateway links do not count.
        /// </summary>
        /// <param name="managementId">id of the management owning this rulebase</param>
        /// <returns>true if the rulebase is the beginning of a rulebase chain</returns>
        public bool IsStartRulebase(int managementId)
        {
            return !IncomingLinks.Any(link => IsLinkFromManagement(link, managementId));
        }

        /// <summary>
        /// Checks whether a link starts at a rulebase or rule of the given management.
        /// A link whose source is not visible (null) is treated as coming from the same management.
        /// </summary>
        private static bool IsLinkFromManagement(RulebaseLink link, int managementId)
        {
            if (link.FromRulebaseId != null)
            {
                return link.FromRulebase == null || link.FromRulebase.MgmtId == managementId;
            }
            if (link.FromRuleId != null)
            {
                return link.FromRule == null || link.FromRule.MgmtId == managementId;
            }
            return false;
        }
    }

    /// <summary>
    /// Helpers to maintain a selection of start rulebases.
    /// </summary>
    public static class RulebaseSelectionHelper
    {
        /// <summary>
        /// Checks whether the rulebase is part of the selection.
        /// </summary>
        public static bool IsSelected(List<SelectedRulebase> selection, int rulebaseId)
        {
            return selection.Any(selected => selected.RulebaseId == rulebaseId);
        }

        /// <summary>
        /// Adds the rulebase to the selection or removes it if already selected.
        /// </summary>
        public static void Toggle(List<SelectedRulebase> selection, RulebaseManagementSelect management, RulebaseSelect rulebase)
        {
            if (selection.RemoveAll(selected => selected.RulebaseId == rulebase.Id) == 0)
            {
                selection.Add(CreateSelection(management, rulebase));
            }
        }

        /// <summary>
        /// Checks whether all rulebases of the visible managements are selected.
        /// </summary>
        public static bool AreAllSelected(List<SelectedRulebase> selection, List<RulebaseManagementSelect> managements)
        {
            List<RulebaseSelect> visibleRulebases = [.. managements.Where(management => management.Visible).SelectMany(management => management.Rulebases)];
            return visibleRulebases.Count > 0 && visibleRulebases.All(rulebase => IsSelected(selection, rulebase.Id));
        }

        /// <summary>
        /// Selects all rulebases of the visible managements.
        /// </summary>
        public static List<SelectedRulebase> SelectAll(List<RulebaseManagementSelect> managements)
        {
            return [.. managements.Where(management => management.Visible)
                .SelectMany(management => management.Rulebases.Select(rulebase => CreateSelection(management, rulebase)))];
        }

        /// <summary>
        /// Keeps only selected rulebases still offered by a visible management and refreshes their names.
        /// </summary>
        public static List<SelectedRulebase> KeepAvailable(List<SelectedRulebase> selection, List<RulebaseManagementSelect> managements)
        {
            return [.. SelectAll(managements).Where(available => IsSelected(selection, available.RulebaseId))];
        }

        /// <summary>
        /// Creates the persisted selection entry for a rulebase of a management.
        /// </summary>
        private static SelectedRulebase CreateSelection(RulebaseManagementSelect management, RulebaseSelect rulebase)
        {
            return new()
            {
                ManagementId = management.Id,
                ManagementName = management.Name,
                RulebaseId = rulebase.Id,
                RulebaseName = rulebase.Name
            };
        }
    }
}
