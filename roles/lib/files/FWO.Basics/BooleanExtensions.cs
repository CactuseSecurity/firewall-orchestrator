using Microsoft.AspNetCore.Components;

namespace FWO.Basics
{
    public static class BooleanExtensions
    {
        public static MarkupString ShowAsHtml(this bool boolVal)
        {
            return ShowAsHtml(boolVal, withColors: false);
        }

        public static MarkupString ShowAsHtml(this bool boolVal, bool withColors)
        {
            // shows check (true) or x (false) in UI
            string colorClass = "";
            if (withColors)
            {
                colorClass = boolVal ? "text-success" : "text-danger";
            }
            var htmlString = boolVal
            ? $"<span class=\"{colorClass} {@Icons.Check}\"></span>"
            : $"<span class=\"{colorClass} {@Icons.Close}\"></span>";
            return new MarkupString(htmlString);
        }
    }

}

