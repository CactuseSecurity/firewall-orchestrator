namespace FWO.Api.Data
{
    public class ModellingFilter
    {
        public FwoOwner SelectedOwner {get; set;} = new ();


        public ModellingFilter()
        {}

        public ModellingFilter(ModellingFilter modellingFilter)
        {
            SelectedOwner = modellingFilter.SelectedOwner;
        }
    }
}
