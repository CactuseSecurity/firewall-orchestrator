using FWO.Api.Data;
using FWO.Logging;
using System.Linq;
using System.Collections.Generic;
using System;

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
                case 'D': return ruleChange.OldRule.DisplaySource(style: DisplayStyle(ruleChange));
                case 'I': return ruleChange.NewRule.DisplaySource(style: DisplayStyle(ruleChange));
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
                case 'D': return ruleChange.OldRule.DisplayDestination(style: DisplayStyle(ruleChange));
                case 'I': return ruleChange.NewRule.DisplayDestination(style: DisplayStyle(ruleChange));
                case 'C': return DisplayDiff(ruleChange.OldRule.DisplayDestination(), ruleChange.NewRule.DisplayDestination());
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public static string DisplayService(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleChange.OldRule.DisplayService(style: DisplayStyle(ruleChange));
                case 'I': return ruleChange.NewRule.DisplayService(style: DisplayStyle(ruleChange));
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
        public static string DisplayEnabled(this RuleChange ruleChange, bool export = false)
        {
            if (export)
            {
                switch (ruleChange.ChangeAction)
                {
                    case 'D': return ruleChange.OldRule.DisplayEnabled(export: true);
                    case 'I': return ruleChange.NewRule.DisplayEnabled(export: true);
                    case 'C': return DisplayDiff(ruleChange.OldRule.DisplayEnabled(export: true), ruleChange.NewRule.DisplayEnabled(export: true));
                    default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
                }
            }

            else
            {
                switch (ruleChange.ChangeAction)
                {
                    case 'D': return ruleChange.OldRule.DisplayEnabled();
                    case 'I': return ruleChange.NewRule.DisplayEnabled();
                    case 'C': return DisplayDiff(ruleChange.OldRule.DisplayEnabled(), ruleChange.NewRule.DisplayEnabled());
                    default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
                }
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

        public static string DisplayStyle(this RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return "color: red";
                case 'I': return "color: green";
                case 'C': return "";
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
                List<string> unchanged = new List<string>();
                List<string> added = new List<string>();
                List<string> deleted = new List<string>();

                foreach (var item in oldAr)
                {
                    if (newAr.Contains(item))
                    {
                        unchanged.Add(item);
                    }
                    else
                    {
                        string deletedItem = item;
                        deletedItem = deletedItem.Replace("<p>", "");
                        deleted.Add(deletedItem.Replace("style=\"\"", "style=\"color: red\""));
                    }
                }
                foreach (var item in newAr)
                {
                    if (!oldAr.Contains(item))
                    {
                        string newItem = item; 
                        newItem = newItem.Replace("<p>", "");
                        added.Add(newItem.Replace("style=\"\"", "style=\"color: green\""));
                    }
                }

                return string.Join("<br>", unchanged) 
                       + (deleted.Count > 0 ? $" deleted: <p style=\"color: red; text-decoration: line-through red;\">{string.Join("<br>", deleted)}</p>" : "")
                       + (added.Count > 0 ? $" added: <p style=\"color: green; text-decoration: bold;\">{string.Join("<br>", added)}</p>" : "");
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
