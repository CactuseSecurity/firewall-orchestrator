namespace FWO.Report.Data
{
    public class ToCHeader(string title)
    {
        public string Title { get; set; } = title;
        public List<ToCItem> Items = [];
    }
}
