using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Report;
using Microsoft.AspNetCore.Components;
using System.Data;
using System.Data.Common;

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
                case 'D': return OutputHtmlDeleted(DisplayName(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayName(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayName(ruleChange.OldRule), DisplayName(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplaySourceZones(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplaySourceZones(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplaySourceZones(ruleChange.OldRule), DisplaySourceZones(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplaySource(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplaySource(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplaySource(ruleChange.OldRule, location, reportType),
                                                  DisplaySource(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayDestinationZones(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayDestinationZones(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayDestinationZones(ruleChange.OldRule), DisplayDestinationZones(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayDestination(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplayDestination(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplayDestination(ruleChange.OldRule, location, reportType),
                                                  DisplayDestination(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayServices(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayServices(ruleChange.OldRule, location, reportType, 0, GlobalConst.kStyleDeleted));
                case 'I': return OutputHtmlAdded(DisplayServices(ruleChange.NewRule, location, reportType, 0, GlobalConst.kStyleAdded));
                case 'C': return DisplayArrayDiff(DisplayServices(ruleChange.OldRule, location, reportType),
                                                  DisplayServices(ruleChange.NewRule, location, reportType),
                                                  ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated);
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayAction(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayAction(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayAction(ruleChange.OldRule), DisplayAction(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayTrack(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayTrack(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayTrack(ruleChange.OldRule), DisplayTrack(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange, OutputLocation location)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayEnabled(ruleChange.OldRule, location));
                case 'I': return OutputHtmlAdded(DisplayEnabled(ruleChange.NewRule, location));
                case 'C': return DisplayDiff(DisplayEnabled(ruleChange.OldRule, location), DisplayEnabled(ruleChange.NewRule, location));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayUid(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayUid(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayUid(ruleChange.OldRule), DisplayUid(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayEnforcingGateways(RuleChange ruleChange, OutputLocation location, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayEnforcingGateways(ruleChange.OldRule, location, reportType));
                case 'I': return OutputHtmlAdded(DisplayEnforcingGateways(ruleChange.NewRule, location, reportType));
                case 'C': return DisplayDiff(DisplayEnforcingGateways(ruleChange.OldRule, location, reportType), DisplayEnforcingGateways(ruleChange.NewRule, location, reportType));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputHtmlDeleted(DisplayComment(ruleChange.OldRule));
                case 'I': return OutputHtmlAdded(DisplayComment(ruleChange.NewRule));
                case 'C': return DisplayDiff(DisplayComment(ruleChange.OldRule), DisplayComment(ruleChange.NewRule));
                default: ThrowErrorUnknowChangeAction(ruleChange.ChangeAction); return "";
            }
        }

        public string DisplayStyle(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return GlobalConst.kStyleDeleted;
                case 'I': return GlobalConst.kStyleAdded;
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
                return (oldElement.Length > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"{GlobalConst.kStyleDeleted}\">{oldElement}<br></p>" : "")
                    + (newElement.Length > 0 ? $"{userConfig.GetText("added")}: <p style=\"{GlobalConst.kStyleAdded}\">{newElement}</p>" : "");
            }
        }

        private string DisplayArrayDiff(string oldElement, string newElement, bool oldNegated, bool newNegated)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                oldElement = oldElement.Replace("<p>", "");
                oldElement = oldElement.Replace("</p>", "");
                oldElement = oldElement.Replace("\r\n", "");
                newElement = newElement.Replace("<p>", "");
                newElement = newElement.Replace("</p>", "");
                newElement = newElement.Replace("\r\n", "");
                List<string> unchanged = [];
                List<string> added = [];
                List<string> deleted = [];

                if(oldNegated != newNegated)
                {
                    deleted.Add(SetStyle(oldElement, GlobalConst.kStyleDeleted));
                    added.Add(SetStyle(newElement, GlobalConst.kStyleAdded));
                }
                else
                {
                    string[] separatingStrings = ["<br>"];
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
                            deleted.Add(SetStyle(item, GlobalConst.kStyleDeleted));
                        }
                    }
                    foreach (var item in newAr)
                    {
                        if (!oldAr.Contains(item))
                        {
                            added.Add(SetStyle(item, GlobalConst.kStyleAdded));
                        }
                    }
                }

                return (unchanged.Count > 0 ? $"<p>{string.Join("<br>", unchanged)}<br></p>" : "")
                       + (deleted.Count > 0 ? $"{userConfig.GetText("deleted")}: <p style=\"{GlobalConst.kStyleDeleted}\">{string.Join("<br>", deleted)}<br></p>" : "")
                       + (added.Count > 0 ? $"{userConfig.GetText("added")}: <p style=\"{GlobalConst.kStyleAdded}\">{string.Join("<br>", added)}</p>" : "");
            }
        }
        
        private static string OutputHtmlDeleted(string? input)
        {
            return  input != null && input != "" ? $"<p style=\"{GlobalConst.kStyleDeleted}\">{input}</p>" : "";
        }

        private static string OutputHtmlAdded(string? input)
        {
            return  input != null && input != "" ? $"<p style=\"{GlobalConst.kStyleAdded}\">{input}</p>" : "";
        }

        private static string SetStyle(string input, string style)
        {
            return input.Replace("style=\"\"", $"style=\"{style}\"");
        }

        private static void ThrowErrorUnknowChangeAction(char action)
        {
            Log.WriteError("Unknown Change Action", $"found an unexpected change action [{action}]");
        }
    }
}
