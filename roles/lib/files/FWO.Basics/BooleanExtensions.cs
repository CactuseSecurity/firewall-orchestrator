using Microsoft.AspNetCore.Components; 

namespace FWO.Basics
{
    public static class BooleanExtensions
    {
        public static MarkupString ShowAsHtml(this bool boolVal)
        {
            // shows check (true) or x (false) in UI
            var htmlString = boolVal
            ? $"<span class=\"{@Icons.Check}\"></span>" 
            : $"<span class=\"{@Icons.Close}\"></span>";
            return new MarkupString(htmlString);
        }
    }

}

