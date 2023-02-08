namespace FWO.Api.Data
{
    public class RecertFilter
    {
        public List<int> RecertOwnerList {get; set;} = new List<int>();
        public bool RecertOverdueOnly {get; set;} = false;
        public bool RecertWithoutOwner {get; set;} = false;
        public bool RecertShowAnyMatch {get; set;} = false;
        public bool RecertSingleLinePerRule {get; set;} = false;
        public int RecertificationDisplayPeriod = 0;

    }
}
