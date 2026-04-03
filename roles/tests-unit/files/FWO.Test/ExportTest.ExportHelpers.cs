namespace FWO.Test
{
    internal partial class ExportTest
    {
        private static string RemoveGenDate(string exportString, bool html = false, bool json = false)
        {
            string Quote = json ? "\"" : "";
            string dateText = html ? "<p>Generated on: " : "report generation date" + Quote + ": " + Quote;
            int startGenTime = exportString.IndexOf(dateText);
            if (startGenTime > 0)
            {
                return exportString.Remove(startGenTime + dateText.Length, 19);
            }
            return exportString;
        }

        private static string RemoveLinebreaks(string exportString)
        {
            while (exportString.Contains("\n "))
            {
                exportString = exportString.Replace("\n ", "\n");
            }
            while (exportString.Contains(" \n"))
            {
                exportString = exportString.Replace(" \n", "\n");
            }
            while (exportString.Contains(" \r"))
            {
                exportString = exportString.Replace(" \r", "\r");
            }
            exportString = exportString.Replace("\r", "");
            return exportString.Replace("\n", "");
        }
    }
}
