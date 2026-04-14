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

        public bool ShowAllOwners { get; set; } = false;
        public bool ShowInactiveRecertOwners { get; set; } = false;
        public long? OwnerRecertId { get; set; }
        public long? ReportId { get; set; }


        public ModellingFilter()
        { }

        public ModellingFilter(ModellingFilter modellingFilter)
        {
            SelectedOwners = modellingFilter.SelectedOwners;
            ShowSourceMatch = modellingFilter.ShowSourceMatch;
            ShowDestinationMatch = modellingFilter.ShowDestinationMatch;
            ShowAnyMatch = modellingFilter.ShowAnyMatch;
            ShowFullRules = modellingFilter.ShowFullRules;
            ShowDropRules = modellingFilter.ShowDropRules;
            AnalyseRemainingRules = modellingFilter.AnalyseRemainingRules;
            RulesForDeletedConns = modellingFilter.RulesForDeletedConns;
            ShowAllOwners = modellingFilter.ShowAllOwners;
            ShowInactiveRecertOwners = modellingFilter.ShowInactiveRecertOwners;
            OwnerRecertId = modellingFilter.OwnerRecertId;
            ReportId = modellingFilter.ReportId;
        }
    }
}
