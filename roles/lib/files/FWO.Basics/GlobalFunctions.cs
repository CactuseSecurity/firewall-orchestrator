
namespace FWO.Basics
{

    public class GlobalFunc
    {
        public static string ShowBool(bool boolVal)
        {
            // shows hook (true) or x (false) in UI
            return boolVal ? "\u2714" : "\u2716";
        }
    }
}

