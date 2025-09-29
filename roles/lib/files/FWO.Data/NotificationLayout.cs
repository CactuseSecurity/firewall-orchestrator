namespace FWO.Data
{
    public enum NotificationLayout
    {
        SimpleText = 0,
        HtmlInBody = 1,
        PdfAsAttachment = 10,
        HtmlAsAttachment = 11,
        CsvAsAttachment = 12,
        JsonAsAttachment = 13
    }
    
    public static class NotificationLayoutGroups
    {
        public static List<NotificationLayout> ListWithoutCsv()
        {
            return [ NotificationLayout.SimpleText,
                NotificationLayout.HtmlInBody,
                NotificationLayout.PdfAsAttachment,
                NotificationLayout.HtmlAsAttachment,
                NotificationLayout.JsonAsAttachment ];
        }
    }
}
