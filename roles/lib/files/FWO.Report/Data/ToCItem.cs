namespace FWO.Report.Data
{
    public class ToCItem(string title, string? id = default)
    {
        public string Title { get; set; } = title;
        public string? Id { get; set; } = id;
    }
}
