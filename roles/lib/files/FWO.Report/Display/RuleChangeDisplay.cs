using FWO.Api.Data;
using FWO.Logging;
using System.Linq;
using System.Text.Json.Serialization;

namespace FWO.Ui.Display
{
    public static class RuleChangeDisplay
    {
        // private static StringBuilder result;

        public static string DisplayChangeTime(this RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }
        public static string DisplayChangeAction(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return "rule created";
                case 'D': return "rule deleted";
                case 'C': return "rule modified";
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public static string DisplayName(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayName();
                case 'I': return ruleChange.NewRule.DisplayName();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayName(), ruleChange.NewRule.DisplayName());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplaySourceZone(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplaySourceZone();
                case 'I': return ruleChange.NewRule.DisplaySourceZone();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplaySourceZone(), ruleChange.NewRule.DisplaySourceZone());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public static string DisplaySource(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplaySource();
                case 'I': return ruleChange.NewRule.DisplaySource();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplaySource(), ruleChange.NewRule.DisplaySource());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public static string DisplayDestinationZone(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDestinationZone();
                case 'I': return ruleChange.NewRule.DisplayDestinationZone();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDestinationZone(), ruleChange.NewRule.DisplayDestinationZone());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public static string DisplayDestination(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDestination();
                case 'I': return ruleChange.NewRule.DisplayDestination();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDestination(), ruleChange.NewRule.DisplayDestination());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayService(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayService();
                case 'I': return ruleChange.NewRule.DisplayService();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayService(), ruleChange.NewRule.DisplayService());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayAction(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayAction();
                case 'I': return ruleChange.NewRule.DisplayAction();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayAction(), ruleChange.NewRule.DisplayAction());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayTrack(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayTrack();
                case 'I': return ruleChange.NewRule.DisplayTrack();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayTrack(), ruleChange.NewRule.DisplayTrack());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayDisabled(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayDisabled();
                case 'I': return ruleChange.NewRule.DisplayDisabled();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDisabled(), ruleChange.NewRule.DisplayDisabled());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayUid(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayUid();
                case 'I': return ruleChange.NewRule.DisplayUid();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayUid(), ruleChange.NewRule.DisplayUid());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayComment(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayComment();
                case 'I': return ruleChange.NewRule.DisplayComment();
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayComment(), ruleChange.NewRule.DisplayComment());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        /// <summary>
        /// displays differences between two string objects
        /// </summary>
        /// <param name="oldElement">the original value of the object</param>
        /// <param name="newElement">the new (changed) value of the object</param>
        /// <returns><paramref name=""/>string diff result</returns>
        private static string DisplayDiff(string oldElement, string newElement)
        {
            if (oldElement == newElement)
                return oldElement;
            else
            {
                string[] separatingStrings = { "<br>" };
                string[] oldAr = oldElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                string[] newAr = newElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                string[] longerAr;
                string[] shorterAr;
                string result;
                if (oldAr.Length > newAr.Length)
                {
                    longerAr = oldAr;
                    shorterAr = newAr;
                }
                else
                {
                    longerAr = newAr;
                    shorterAr = oldAr;
                }
                string difference = string.Join("<br>", longerAr.Except(shorterAr));
                if (oldAr.Length < newAr.Length)
                    result = string.Join("<br>", shorterAr) + $" + <p style=\"text-decoration: bold;\">{difference}</p>";
                else if (oldAr.Length > newAr.Length)
                    result = string.Join("<br>", shorterAr) + $" - <p style=\"text-decoration: line-through;\">{difference.ToString()}</p>";
                else // same number of elements - one of them was replaced - todo!
                    result = string.Join("<br>", shorterAr) + $" diff: {difference}";

                return result;
            }
        }
        
        /// <summary>
        /// displays differences between two json objects
        /// </summary>
        /// <param name="oldJsonObject">the original value of the object</param>
        /// <param name="newJsonObject">the new (changed) value of the object</param>
        /// <returns><paramref name=""/> wrapped in <c>Dictionary</c> serialized to Json.</returns>
        private static string DisplayJsonDiff(string oldJsonObject, string newJsonObject)
        {
            // todo: implement diff
            if (oldJsonObject == newJsonObject)
                return oldJsonObject;
            else
                return $"{oldJsonObject} --> {newJsonObject}";
        }
        private static void ThrowErrorUnknowChangeAction(char action)
        {
            Log.WriteError("Unknown Change Action", $"found an unexpected change action [{action}]");
        }
    }
}
