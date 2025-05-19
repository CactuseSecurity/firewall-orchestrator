namespace FWO.Report.Data
{
    public class MyData
    {
        public string Name { get; set; } = "";
        public HeaderType HeaderType { get; set; } = HeaderType.None;
    }

    public enum HeaderType
    {
        None,
        Section,
        Rulebase
    }
}
