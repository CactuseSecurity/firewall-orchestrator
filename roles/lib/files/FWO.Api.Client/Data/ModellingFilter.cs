namespace FWO.Api.Data
{
    public class ModellingFilter
    {
        public List<FwoOwner> SelectedOwners {get; set;} = [];
        public FwoOwner SelectedOwner 
        {
            get { return SelectedOwners.FirstOrDefault() ?? new(); }
            set { SelectedOwners = [value]; }
        }

        public FwoOwner SelectedTemplateOwner { get; set; } = new();
        public bool ShowSourceMatch {get; set;} = true;
        public bool ShowDestinationMatch {get; set;} = true;
        public bool ShowAnyMatch {get; set;} = false;
        public bool ShowFullRules {get; set;} = false;
        public bool ShowDropRules {get; set;} = false;


        public ModellingFilter()
        {}

        public ModellingFilter(ModellingFilter modellingFilter)
        {
            SelectedOwners = modellingFilter.SelectedOwners;
            ShowSourceMatch = modellingFilter.ShowSourceMatch;
            ShowDestinationMatch = modellingFilter.ShowDestinationMatch;
            ShowAnyMatch = modellingFilter.ShowAnyMatch;
            ShowFullRules = modellingFilter.ShowFullRules;
            ShowDropRules = modellingFilter.ShowDropRules;
        }
    }
}
