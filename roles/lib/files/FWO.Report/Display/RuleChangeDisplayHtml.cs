using FWO.Api.Data;
using FWO.Logging;
using FWO.Config.Api;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayHtml : RuleDisplayHtml
    {
        public RuleChangeDisplayHtml(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return ruleChange.ChangeImport.Time.ToString();
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return userConfig.GetText("rule_added");
                case 'D': return userConfig.GetText("rule_deleted");
                case 'C': return userConfig.GetText("rule_modified");
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayName(ruleChange.OldRule);
                case 'I': return DisplayName(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayName(ruleChange.OldRule), DisplayName(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySourceZone(ruleChange.OldRule);
                case 'I': return DisplaySourceZone(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplaySourceZone(ruleChange.OldRule), DisplaySourceZone(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySource(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return DisplaySource(ruleChange.NewRule, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplaySource(ruleChange.OldRule), DisplaySource(ruleChange.NewRule), ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestinationZone(ruleChange.OldRule);
                case 'I': return DisplayDestinationZone(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayDestinationZone(ruleChange.OldRule), DisplayDestinationZone(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestination(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return DisplayDestination(ruleChange.NewRule, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplayDestination(ruleChange.OldRule), DisplayDestination(ruleChange.NewRule), ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayService(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayService(ruleChange.OldRule, DisplayStyle(ruleChange));
                case 'I': return DisplayService(ruleChange.NewRule, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplayService(ruleChange.OldRule), DisplayService(ruleChange.NewRule), ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayAction(ruleChange.OldRule);
                case 'I': return DisplayAction(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayAction(ruleChange.OldRule), DisplayAction(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayTrack(ruleChange.OldRule);
                case 'I': return DisplayTrack(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayTrack(ruleChange.OldRule), DisplayTrack(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange, bool export = false)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayEnabled(ruleChange.OldRule, export);
                case 'I': return DisplayEnabled(ruleChange.NewRule, export);
                case 'C': return DisplayDiff(DisplayEnabled(ruleChange.OldRule, export), DisplayEnabled(ruleChange.NewRule, export));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayUid(ruleChange.OldRule);
                case 'I': return DisplayUid(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayUid(ruleChange.OldRule), DisplayUid(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayComment(ruleChange.OldRule);
                case 'I': return DisplayComment(ruleChange.NewRule);
                case 'C': return DisplayDiff(DisplayComment(ruleChange.OldRule), DisplayComment(ruleChange.NewRule));
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

        private string DisplayDiff(string oldElement, string newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                return (oldElement.Length > 0 ? $" {userConfig.GetText("deleted")}: {oldElement}" : "")
                    + (newElement.Length > 0 ? $" {userConfig.GetText("added")}: {newElement}" : "") + ",";
            }
        }

        /// <summary>
        /// displays differences between two string objects
        /// </summary>
        /// <param name="oldElement">the original value of the object</param>
        /// <param name="newElement">the new (changed) value of the object</param>
        /// <returns><paramref name=""/>string diff result</returns>
        private string DisplayArrayDiff(string oldElement, string newElement, bool oldNegated, bool newNegated)
        {
            if (oldElement == newElement)
                return oldElement;
            else
            {
                List<string> unchanged = new List<string>();
                List<string> added = new List<string>();
                List<string> deleted = new List<string>();

                if(oldNegated != newNegated)
                {
                    deleted.Add(oldElement.Replace("style=\"\"", "style=\"color: red\""));
                    added.Add(newElement.Replace("style=\"\"", "style=\"color: green\""));
                }
                else
                {
                    string[] separatingStrings = { "<br>" };
                    string[] oldAr = oldElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
                    string[] newAr = newElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);

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
        // private string DisplayJsonDiff(string oldJsonObject, string newJsonObject)
        // {
        //     // todo: implement diff
        //     if (oldJsonObject == newJsonObject)
        //         return oldJsonObject;
        //     else
        //         return $"{oldJsonObject} --> {newJsonObject}";
        // }

        private void ThrowErrorUnknowChangeAction(char action)
        {
            Log.WriteError("Unknown Change Action", $"found an unexpected change action [{action}]");
        }
    }
}
