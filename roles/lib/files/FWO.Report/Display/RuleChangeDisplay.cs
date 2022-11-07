using FWO.Api.Data;
using FWO.Logging;
using System.Linq;
using System.Collections.Generic;
using System;
using FWO.Config.Api;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplay
    {
        // private static StringBuilder result;

        private UserConfig userConfig;
        private RuleDisplayHtml ruleDisplay;

        public RuleChangeDisplay(UserConfig userConfig)
        {
            this.userConfig = userConfig;
            ruleDisplay = new RuleDisplayHtml(userConfig);
        }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }
        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return "rule created";
                case 'D': return "rule deleted";
                case 'C': return "rule modified";
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayName(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayName(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayName(ruleChange.OldRule), ruleDisplay.DisplayName(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplaySourceZone(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplaySourceZone(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplaySourceZone(ruleChange.OldRule), ruleDisplay.DisplaySourceZone(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplaySource(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return ruleDisplay.DisplaySource(ruleChange.NewRule, DisplayStyle(ruleChange));
                case 'C': return DisplayDiff(ruleDisplay.DisplaySource(ruleChange.OldRule), ruleDisplay.DisplaySource(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayDestinationZone(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayDestinationZone(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayDestinationZone(ruleChange.OldRule), ruleDisplay.DisplayDestinationZone(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayDestination(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return ruleDisplay.DisplayDestination(ruleChange.NewRule, DisplayStyle(ruleChange));
                case 'C': return DisplayDiff(ruleDisplay.DisplayDestination(ruleChange.OldRule), ruleDisplay.DisplayDestination(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayService(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayService(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return ruleDisplay.DisplayService(ruleChange.NewRule, DisplayStyle(ruleChange));
                // case 'C': return ruleDisplay.DisplayService(ruleChange.OldRule, ruleDisplay.DisplayService(ruleChange.NewRule));
                case 'C': return DisplayDiff(ruleDisplay.DisplayService(ruleChange.OldRule), ruleDisplay.DisplayService(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayAction(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayAction(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayAction(ruleChange.OldRule), ruleDisplay.DisplayAction(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayTrack(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayTrack(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayTrack(ruleChange.OldRule), ruleDisplay.DisplayTrack(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayEnabled(RuleChange ruleChange, bool export = false)
        {
            if (export)
            {
                switch (ruleChange.ChangeAction)
                {
                    case 'D': return ruleDisplay.DisplayEnabled(ruleChange.OldRule, export: true);
                    case 'I': return ruleDisplay.DisplayEnabled(ruleChange.NewRule, export: true);
                    case 'C': return DisplayDiff(ruleDisplay.DisplayEnabled(ruleChange.OldRule, export: true), ruleDisplay.DisplayEnabled(ruleChange.NewRule, export: true));
                    default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
                }
            }

            else
            {
                switch (ruleChange.ChangeAction)
                {
                    case 'D': return ruleDisplay.DisplayEnabled(ruleChange.OldRule);
                    case 'I': return ruleDisplay.DisplayEnabled(ruleChange.NewRule);
                    case 'C': return DisplayDiff(ruleDisplay.DisplayEnabled(ruleChange.OldRule), ruleDisplay.DisplayEnabled(ruleChange.NewRule));
                    default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
                }
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayUid(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayUid(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayUid(ruleChange.OldRule), ruleDisplay.DisplayUid(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }
        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return ruleDisplay.DisplayComment(ruleChange.OldRule);
                case 'I': return ruleDisplay.DisplayComment(ruleChange.NewRule);
                case 'C': return DisplayDiff(ruleDisplay.DisplayComment(ruleChange.OldRule), ruleDisplay.DisplayComment(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayStyle(RuleChange ruleChange)
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
        private string DisplayDiff(string oldElement, string newElement)
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
                       + (deleted.Count > 0 ? $" {userConfig.GetText("deleted")}: <p style=\"color: red; text-decoration: line-through red;\">{string.Join("<br>", deleted)}</p>" : "")
                       + (added.Count > 0 ? $" {userConfig.GetText("added")}: <p style=\"color: green; text-decoration: bold;\">{string.Join("<br>", added)}</p>" : "");
            }
        }
        
        /// <summary>
        /// displays differences between two json objects
        /// </summary>
        /// <param name="oldJsonObject">the original value of the object</param>
        /// <param name="newJsonObject">the new (changed) value of the object</param>
        /// <returns><paramref name=""/> wrapped in <c>Dictionary</c> serialized to Json.</returns>
        private string DisplayJsonDiff(string oldJsonObject, string newJsonObject)
        {
            // todo: implement diff
            if (oldJsonObject == newJsonObject)
                return oldJsonObject;
            else
                return $"{oldJsonObject} --> {newJsonObject}";
        }
        private void ThrowErrorUnknowChangeAction(char action)
        {
            Log.WriteError("Unknown Change Action", $"found an unexpected change action [{action}]");
        }
    }
}
