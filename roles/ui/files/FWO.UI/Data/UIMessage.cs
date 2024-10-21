namespace FWO.Ui.Data
{
    public class UIMessage
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public System.Timers.Timer Timer = new();
    }
}
