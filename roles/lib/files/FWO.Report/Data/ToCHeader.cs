namespace FWO.Report.Data
{
    public class ToCHeader(string title, string? id = default)
    {
        public string Title { get; set; } = title;
        public string? Id { get; set; } = id;
        public List<ToCItem> Items = [];
    }
}
