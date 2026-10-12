namespace FWO.Data.Report
{
    public class ModellingFilter
    {
        public List<FwoOwner> SelectedOwners { get; set; } = [];
        public FwoOwner SelectedOwner
        {
            get { return SelectedOwners.FirstOrDefault() ?? new(); }
            set { SelectedOwners = [value]; }
        }

        public FwoOwner SelectedTemplateOwner { get; set; } = new();
        public bool ShowSourceMatch { get; set; } = true;
        public bool ShowDestinationMatch { get; set; } = true;
        public bool ShowAnyMatch { get; set; } = false;
        public bool ShowFullRules { get; set; } = false;
        public bool ShowDropRules { get; set; } = false;

        public bool AnalyseRemainingRules { get; set; } = false;
        public bool RulesForDeletedConns { get; set; } = false;
        public bool VerifyRuleOwnerPreFilterCompleteness { get; set; } = false;

        /// <summary>
        /// Allows the analysis to wait for a pending rule_owner mapping run instead of falling back to
        /// the marker query right away. Only set where somebody is actually waiting for the result -
        /// the background job leaves it false so its owner loop cannot accumulate wait times.
        /// </summary>
        public bool AllowWaitForRuleOwnerMapping { get; set; } = false;

        public bool ShowAllOwners { get; set; } = false;
        public bool ShowInactiveRecertOwners { get; set; } = false;
        public bool MergeOwnerRecertTables { get; set; } = false;
        public string OwnerAdditionalInfoKey { get; set; } = "";
        public AddInfoFilter OwnerAddInfoFilter { get; set; } = new();
        public long? OwnerRecertId { get; set; }
        public long? ReportId { get; set; }


        public ModellingFilter()
        { }

        public ModellingFilter(ModellingFilter? modellingFilter)
        {
            if (modellingFilter == null)
            {
                return;
            }

            SelectedOwners = modellingFilter.SelectedOwners;
            SelectedTemplateOwner = modellingFilter.SelectedTemplateOwner;
            ShowSourceMatch = modellingFilter.ShowSourceMatch;
            ShowDestinationMatch = modellingFilter.ShowDestinationMatch;
            ShowAnyMatch = modellingFilter.ShowAnyMatch;
            ShowFullRules = modellingFilter.ShowFullRules;
            ShowDropRules = modellingFilter.ShowDropRules;
            AnalyseRemainingRules = modellingFilter.AnalyseRemainingRules;
            RulesForDeletedConns = modellingFilter.RulesForDeletedConns;
            VerifyRuleOwnerPreFilterCompleteness = modellingFilter.VerifyRuleOwnerPreFilterCompleteness;
            AllowWaitForRuleOwnerMapping = modellingFilter.AllowWaitForRuleOwnerMapping;
            ShowAllOwners = modellingFilter.ShowAllOwners;
            ShowInactiveRecertOwners = modellingFilter.ShowInactiveRecertOwners;
            MergeOwnerRecertTables = modellingFilter.MergeOwnerRecertTables;
            OwnerAdditionalInfoKey = modellingFilter.OwnerAdditionalInfoKey;
            OwnerAddInfoFilter = new(modellingFilter.OwnerAddInfoFilter);
            OwnerRecertId = modellingFilter.OwnerRecertId;
            ReportId = modellingFilter.ReportId;
        }
    }
}
