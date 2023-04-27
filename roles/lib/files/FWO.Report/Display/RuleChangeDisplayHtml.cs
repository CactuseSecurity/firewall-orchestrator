using FWO.Api.Data;
using FWO.Logging;
using FWO.Config.Api;
using FWO.Report.Filter;

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

        public string DisplaySource(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySource(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange));
                case 'I': return DisplaySource(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplaySource(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange)),
                                                  DisplaySource(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange)),
                                                  ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated);
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

        public string DisplayDestination(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestination(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange));
                case 'I': return DisplayDestination(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplayDestination(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange)),
                                                  DisplayDestination(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange)),
                                                  ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayService(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayService(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange));
                case 'I': return DisplayService(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange));
                case 'C': return DisplayArrayDiff(DisplayService(ruleChange.OldRule, location, reportType, DisplayStyle(ruleChange)),
                                                  DisplayService(ruleChange.NewRule, location, reportType, DisplayStyle(ruleChange)),
                                                  ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated);
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

        public string DisplayEnabled(RuleChange ruleChange, OutputLocation location)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayEnabled(ruleChange.OldRule, location);
                case 'I': return DisplayEnabled(ruleChange.NewRule, location);
                case 'C': return DisplayDiff(DisplayEnabled(ruleChange.OldRule, location), DisplayEnabled(ruleChange.NewRule, location));
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
                return (oldElement.Length > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"color: red; text-decoration: line-through red;\">{oldElement}<br></p>" : "")
                    + (newElement.Length > 0 ? $"{userConfig.GetText("added")}: <p style=\"color: green; text-decoration: bold;\">{newElement}</p>" : "");
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
                oldElement = oldElement.Replace("<p>", "");
                oldElement = oldElement.Replace("</p>", "");
                oldElement = oldElement.Replace("\r\n", "");
                newElement = newElement.Replace("<p>", "");
                newElement = newElement.Replace("</p>", "");
                newElement = newElement.Replace("\r\n", "");
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
                            deleted.Add(item.Replace("style=\"\"", "style=\"color: red\""));
                        }
                    }
                    foreach (var item in newAr)
                    {
                        if (!oldAr.Contains(item))
                        {
                            added.Add(item.Replace("style=\"\"", "style=\"color: green\""));
                        }
                    }
                }

                return (unchanged.Count > 0 ? $"<p>{string.Join("<br>", unchanged)}<br></p>" : "")
                       + (deleted.Count > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"color: red; text-decoration: line-through red;\">{string.Join("<br>", deleted)}<br></p>" : "")
                       + (added.Count > 0 ? $"{userConfig.GetText("added")}: <p style=\"color: green; text-decoration: bold;\">{string.Join("<br>", added)}</p>" : "");
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
