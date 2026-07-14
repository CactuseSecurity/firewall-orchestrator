namespace FWO.Data.Report
{
    public class OwnerFilter
    {
        public int? SelectedOwnerLifeCycleStateId { get; set; }
        public string? SelectedCriticality { get; set; }

        public OwnerFilter()
        { }

        public OwnerFilter(OwnerFilter ownerFilter)
        {
            SelectedOwnerLifeCycleStateId = ownerFilter.SelectedOwnerLifeCycleStateId;
            SelectedCriticality = ownerFilter.SelectedCriticality;
        }
    }
}
