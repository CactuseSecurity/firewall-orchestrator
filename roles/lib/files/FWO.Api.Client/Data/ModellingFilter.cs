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


        public ModellingFilter()
        {}

        public ModellingFilter(ModellingFilter modellingFilter)
        {
            SelectedOwners = modellingFilter.SelectedOwners;
        }
    }
}
