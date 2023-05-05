using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleChangeDisplayCsv : RuleDisplayCsv
    {
        public RuleChangeDisplayCsv(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayChangeTime(RuleChange ruleChange)
        {
            return OutputCsv(ruleChange.ChangeImport.Time.ToString());
        }

        public string DisplayChangeAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'I': return OutputCsv(userConfig.GetText("rule_added"));
                case 'D': return OutputCsv(userConfig.GetText("rule_deleted"));
                case 'C': return OutputCsv(userConfig.GetText("rule_modified"));
                default: return ",";
            }
        }

        public string DisplayName(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayName(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayName(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayName(ruleChange.OldRule), DisplayName(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplaySourceZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplaySourceZone(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplaySourceZone(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplaySourceZone(ruleChange.OldRule), DisplaySourceZone(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplaySource(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplaySource(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplaySource(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplaySource(ruleChange.OldRule, reportType), DisplaySource(ruleChange.NewRule, reportType), ruleChange.OldRule.SourceNegated, ruleChange.NewRule.SourceNegated));
                default: return ",";
            }
        }

        public string DisplayDestinationZone(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayDestinationZone(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayDestinationZone(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayDestinationZone(ruleChange.OldRule), DisplayDestinationZone(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayDestination(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayDestination(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplayDestination(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplayDestination(ruleChange.OldRule, reportType), DisplayDestination(ruleChange.NewRule, reportType), ruleChange.OldRule.DestinationNegated, ruleChange.NewRule.DestinationNegated));
                default: return ",";
            }
        }

        public string DisplayService(RuleChange ruleChange, ReportType reportType)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayService(ruleChange.OldRule, reportType));
                case 'I': return OutputCsv(DisplayService(ruleChange.NewRule, reportType));
                case 'C': return OutputCsv(DisplayArrayDiff(DisplayService(ruleChange.OldRule, reportType), DisplayService(ruleChange.NewRule, reportType), ruleChange.OldRule.ServiceNegated, ruleChange.NewRule.ServiceNegated));
                default: return ",";
            }
        }

        public string DisplayAction(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayAction(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayAction(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayAction(ruleChange.OldRule), DisplayAction(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayTrack(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayTrack(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayTrack(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayTrack(ruleChange.OldRule), DisplayTrack(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayEnabled(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayEnabled(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayEnabled(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayEnabled(ruleChange.OldRule), DisplayEnabled(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayUid(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayUid(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayUid(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayUid(ruleChange.OldRule), DisplayUid(ruleChange.NewRule)));
                default: return ",";
            }
        }

        public string DisplayComment(RuleChange ruleChange)
        {
            switch (ruleChange.ChangeAction)
            {
                case 'D': return OutputCsv(DisplayComment(ruleChange.OldRule));
                case 'I': return OutputCsv(DisplayComment(ruleChange.NewRule));
                case 'C': return OutputCsv(DisplayDiff(DisplayComment(ruleChange.OldRule), DisplayComment(ruleChange.NewRule)));
                default: return "";
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
                    + (newElement.Length > 0 ? $" {userConfig.GetText("added")}: {newElement}" : "");
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
                List<string> unchanged = new List<string>();
                List<string> added = new List<string>();
                List<string> deleted = new List<string>();

                oldElement = oldElement.Replace("\"", "");
                newElement = newElement.Replace("\"", "");
                if(oldNegated != newNegated)
                {
                    deleted.Add(oldElement);
                    added.Add(newElement);
                }
                else
                {
                    string[] separatingStrings = { "," };
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
                            deleted.Add(deletedItem);
                        }
                    }
                    foreach (var item in newAr)
                    {
                        if (!oldAr.Contains(item))
                        {
                            string newItem = item; 
                            added.Add(newItem);
                        }
                    }
                }

                return string.Join(" ", unchanged) 
                    + (deleted.Count > 0 ? $" {userConfig.GetText("deleted")}: {string.Join(",", deleted)}" : "")
                    + (added.Count > 0 ? $" {userConfig.GetText("added")}: {string.Join(",", added)}" : "");
            }
        }
    }
}
