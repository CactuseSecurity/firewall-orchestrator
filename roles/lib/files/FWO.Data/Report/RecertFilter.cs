namespace FWO.Data.Report
{
    public class RecertFilter
    {
        public List<int> RecertOwnerList {get; set;} = [];
        public bool RecertShowAnyMatch {get; set;} = false;
        public int RecertificationDisplayPeriod = 0;

        public RecertFilter()
        {}

        public RecertFilter(RecertFilter recertFilter)
        {
            RecertOwnerList = new(recertFilter.RecertOwnerList);
            RecertShowAnyMatch = recertFilter.RecertShowAnyMatch;
            RecertificationDisplayPeriod = recertFilter.RecertificationDisplayPeriod;
        }
    }
}
