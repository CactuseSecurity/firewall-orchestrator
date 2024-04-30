using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayJson : RuleDisplayJson
    {
        public RuleChangeDisplayJson(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return DisplayJsonString("change time", ruleChange.ChangeImport.Time.ToString());
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return DisplayJsonString("change action", userConfig.GetText("rule_added"));
                case 'D': return DisplayJsonString("change action", userConfig.GetText("rule_deleted"));
                case 'C': return DisplayJsonString("change action", userConfig.GetText("rule_modified"));
                default: return "";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayName(ruleChange.OldRule.Name);
                case 'I': return DisplayName(ruleChange.NewRule.Name);
                case 'C': return DisplayName(DisplayDiff(ruleChange.OldRule.Name, ruleChange.NewRule.Name));
                default: return "";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySourceZone(ruleChange.OldRule.SourceZone?.Name);
                case 'I': return DisplaySourceZone(ruleChange.NewRule.SourceZone?.Name);
                case 'C': return DisplaySourceZone(DisplayDiff(ruleChange.OldRule.SourceZone?.Name, ruleChange.NewRule.SourceZone?.Name));
                default: return "";
            }
        }

        public string DisplaySourceNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySourceNegated(ruleChange.OldRule.SourceNegated);
                case 'I': return DisplaySourceNegated(ruleChange.NewRule.SourceNegated);
                case 'C': return ruleChange.OldRule.SourceNegated == ruleChange.NewRule.SourceNegated ?
                    DisplaySourceNegated(ruleChange.NewRule.SourceNegated) :
                    DisplayJsonString("source negated", DisplayDiff(ruleChange.OldRule.SourceNegated.ToString().ToLower(), ruleChange.NewRule.SourceNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplaySource(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplaySource(ruleChange.OldRule, reportType);
                case 'I': return DisplaySource(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("source", DisplayArrayDiff(ListNetworkLocations(ruleChange.OldRule, reportType, true), 
                    ListNetworkLocations(ruleChange.NewRule, reportType, true)));
                default: return "";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestinationZone(ruleChange.OldRule.DestinationZone?.Name);
                case 'I': return DisplayDestinationZone(ruleChange.NewRule.DestinationZone?.Name);
                case 'C': return DisplayDestinationZone(DisplayDiff(ruleChange.OldRule.DestinationZone?.Name, ruleChange.NewRule.DestinationZone?.Name));
                default: return "";
            }
        }

        public string DisplayDestinationNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestinationNegated(ruleChange.OldRule.DestinationNegated);
                case 'I': return DisplayDestinationNegated(ruleChange.NewRule.DestinationNegated);
                case 'C': return ruleChange.OldRule.DestinationNegated == ruleChange.NewRule.DestinationNegated ?
                    DisplayDestinationNegated(ruleChange.NewRule.DestinationNegated) :
                    DisplayJsonString("destination negated", DisplayDiff(ruleChange.OldRule.DestinationNegated.ToString().ToLower(), ruleChange.NewRule.DestinationNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayDestination(ruleChange.OldRule, reportType);
                case 'I': return DisplayDestination(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("destination", DisplayArrayDiff(ListNetworkLocations(ruleChange.OldRule, reportType, false),
                    ListNetworkLocations(ruleChange.NewRule, reportType, false)));
                default: return "";
            }
        }

        public string DisplayServiceNegated(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayServiceNegated(ruleChange.OldRule.ServiceNegated);
                case 'I': return DisplayServiceNegated(ruleChange.NewRule.ServiceNegated);
                case 'C': return ruleChange.OldRule.ServiceNegated == ruleChange.NewRule.ServiceNegated ?
                    DisplayServiceNegated(ruleChange.NewRule.ServiceNegated) :
                    DisplayJsonString("service negated", DisplayDiff(ruleChange.OldRule.ServiceNegated.ToString().ToLower(), ruleChange.NewRule.ServiceNegated.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplayServices(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayServices(ruleChange.OldRule, reportType);
                case 'I': return DisplayServices(ruleChange.NewRule, reportType);
                case 'C': return DisplayJsonArray("service", DisplayArrayDiff(ListServices(ruleChange.OldRule, reportType), 
                    ListServices(ruleChange.NewRule, reportType)));
                default: return "";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayAction(ruleChange.OldRule.Action);
                case 'I': return DisplayAction(ruleChange.NewRule.Action);
                case 'C': return DisplayAction(DisplayDiff(ruleChange.OldRule.Action, ruleChange.NewRule.Action));
                default: return "";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayTrack(ruleChange.OldRule.Track);
                case 'I': return DisplayTrack(ruleChange.NewRule.Track);
                case 'C': return DisplayTrack(DisplayDiff(ruleChange.OldRule.Track, ruleChange.NewRule.Track));
                default: return "";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayEnabled(ruleChange.OldRule.Disabled);
                case 'I': return DisplayEnabled(ruleChange.NewRule.Disabled);
                case 'C': return ruleChange.OldRule.Disabled == ruleChange.NewRule.Disabled ?
                    DisplayEnabled(ruleChange.NewRule.Disabled) :
                    DisplayJsonString("disabled", DisplayDiff(ruleChange.OldRule.Disabled.ToString().ToLower(), ruleChange.NewRule.Disabled.ToString().ToLower()));
                default: return "";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayUid(ruleChange.OldRule.Uid);
                case 'I': return DisplayUid(ruleChange.NewRule.Uid);
                case 'C': return DisplayUid(DisplayDiff(ruleChange.OldRule.Uid, ruleChange.NewRule.Uid));
                default: return "";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return DisplayComment(ruleChange.OldRule.Comment);
                case 'I': return DisplayComment(ruleChange.NewRule.Comment);
                case 'C': return DisplayComment(DisplayDiff(ruleChange.OldRule.Comment, ruleChange.NewRule.Comment));
                default: return "";
            }
        }

        private string? DisplayDiff(string? oldElement, string? newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                return (oldElement != null && oldElement.Length > 0 ? $"{userConfig.GetText("deleted")}: {oldElement}{(newElement != null && newElement.Length > 0 ? ", " : "")}" : "")
                    + (newElement != null && newElement.Length > 0 ?$"{userConfig.GetText("added")}: {newElement}" : "");
            }
        }

        private string DisplayArrayDiff(string oldElement, string newElement)
        {
            if (oldElement == newElement)
            {
                return oldElement;
            }
            else
            {
                List<string> unchanged = new List<string>();
                List<string> added = new List<string>();
                List<string> deleted = new List<string>();

                oldElement = oldElement.Replace("\"", "");
                newElement = newElement.Replace("\"", "");
                AnalyzeElements(oldElement, newElement, ref unchanged, ref deleted, ref added);

                return string.Join(",", Array.ConvertAll(unchanged.ToArray(), elem => Quote(elem))) + (unchanged.Count > 0 && (deleted.Count > 0 || added.Count > 0 ) ? "," : "")
                    + (deleted.Count > 0 ? string.Join(",", Array.ConvertAll(deleted.ToArray(), elem => Quote($"{userConfig.GetText("deleted")}: {elem}"))) : "") + (deleted.Count > 0 && added.Count > 0 ? "," : "")
                    + (added.Count > 0 ? string.Join(",", Array.ConvertAll(added.ToArray(), elem => Quote($"{userConfig.GetText("added")}: {elem}"))) : "");
            }
        }
    }
}
