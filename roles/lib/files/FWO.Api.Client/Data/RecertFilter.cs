namespace FWO.Api.Data
{
    public class RecertFilter
    {
        public List<int> RecertOwnerList {get; set;} = new List<int>();
        public bool RecertShowAnyMatch {get; set;} = false;
        public int RecertificationDisplayPeriod = 0;

    }
}
