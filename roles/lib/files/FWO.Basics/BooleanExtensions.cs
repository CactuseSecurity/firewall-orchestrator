namespace FWO.Basics
{
    public static class BooleanExtensions
    {
        public static string ShowAsHtml(this bool boolVal)
        {
            // shows hook (true) or x (false) in UI
            return boolVal ? "\u2714" : "\u2716";
        }
    }

}

