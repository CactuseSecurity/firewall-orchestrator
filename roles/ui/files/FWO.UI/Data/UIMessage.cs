using FWO.Data;

namespace FWO.Ui.Data
{
    public class UIMessage : IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string? Title { get; set; }
        public string? Message { get; set; }
        public MessageType Type { get; set; } = MessageType.Info;
        public System.Timers.Timer? ShowTimer;
        public string CSSClass
        {
            get
            {
                return GetCSSClass(Type);
            }
        }

        private static string GetCSSClass(MessageType messageType)
        {
            string cssClass = "mb-1 fly-in-animation";

            switch (messageType)
            {
                case MessageType.Info:
                    cssClass += " alert alert-info";
                    break;
                case MessageType.Success:
                    cssClass += " alert alert-success";
                    break;
                case MessageType.Warning:
                    cssClass += " alert alert-warning-override";
                    break;
                case MessageType.Error:
                    cssClass += " alert alert-danger";
                    break;
                default:
                    break;
            }

            return cssClass;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(ShowTimer != null)
            {
                ShowTimer.Stop();
                ShowTimer.Dispose();
                ShowTimer = null;
            }
        }
    }
}
