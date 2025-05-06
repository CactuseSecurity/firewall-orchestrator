namespace FWO.Data.Report
{    public class RuleGroup
    {
        public string GroupName { get; set; } = string.Empty;
        public List<Rule> Rules { get; set; } = new();
    }

}
