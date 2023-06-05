using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;
using System.Text.RegularExpressions;

namespace FWO.Ui.Display
{
    public class RuleDisplayCsv : RuleDisplayBase
    {
        public RuleDisplayCsv(UserConfig userConfig) : base(userConfig)
        { }

        public string OutputCsv(string? input)
        {
            return  $"\"{input ?? ""}\",";
        }

        public string DisplayNumberCsv(Rule rule)
        {
            return OutputCsv(DisplayNumber(rule));
        }

        public string DisplayNameCsv(Rule rule)
        {
            return OutputCsv(DisplayName(rule));
        }

        public string DisplaySourceZoneCsv(Rule rule)
        {
            return OutputCsv(DisplaySourceZone(rule));
        }

        public string DisplaySourceCsv(Rule rule, ReportType reportType)
        {
            return OutputCsv(DisplaySource(rule, reportType));
        }

        public string DisplayDestinationZoneCsv(Rule rule)
        {
            return OutputCsv(DisplayDestinationZone(rule));
        }

        public string DisplayDestinationCsv(Rule rule, ReportType reportType)
        {
            return OutputCsv(DisplayDestination(rule, reportType));
        }

        public string DisplayServicesCsv(Rule rule, ReportType reportType)
        {
            return OutputCsv(DisplayServices(rule, reportType));
        }

        public string DisplayActionCsv(Rule rule)
        {
            return OutputCsv(DisplayAction(rule));
        }

        public string DisplayTrackCsv(Rule rule)
        {
            return OutputCsv(DisplayTrack(rule));
        }

        public string DisplayEnabledCsv(Rule rule)
        {
            return OutputCsv(DisplayEnabled(rule));
        }

        public string DisplayUidCsv(Rule rule)
        {
            return OutputCsv(DisplayUid(rule));
        }

        public string DisplayCommentCsv(Rule rule)
        {
            return OutputCsv(DisplayComment(rule));
        }
       

        public new string DisplayName(Rule rule)
        {
            return (rule.Name != null ? SanitizeComment(rule.Name) : "");
        }

        public new string DisplayComment(Rule rule)
        {
            return (rule.Comment != null ? SanitizeComment(rule.Comment) : "");
        }
        
        public string DisplayEnabled(Rule rule)
        {
            return (rule.Disabled) ? "disabled" : "enabled";
        }

        public string DisplaySource(Rule rule, ReportType reportType)
        {
            return DisplaySourceOrDestination(rule, reportType, true);
        }

        public string DisplayDestination(Rule rule, ReportType reportType)
        {
            return DisplaySourceOrDestination(rule, reportType, false);
        }

        public string DisplayServices(Rule rule, ReportType reportType)
        {
            StringBuilder result = new StringBuilder();
            if (reportType.IsResolvedReport())
            {
                List<string> displayedServices = new List<string>();
                foreach (NetworkService service in GetNetworkServices(rule.Services))
                {
                    displayedServices.Add(DisplayService(service, reportType).ToString());
                }

                if(rule.ServiceNegated)
                {
                    result.Append($"{userConfig.GetText("negated")}(");
                }
                result.Append(string.Join(",", displayedServices));
                if(rule.ServiceNegated)
                {
                    result.Append(")");
                }
            }
            return result.ToString();
        }

        private string SanitizeComment(string inputString)
        {
            string output = Regex.Replace(inputString, @"[""'']", "").Trim();
            output = Regex.Replace(output, @"[\n]", ", ").Trim();
            return output;
        }

        private string DisplaySourceOrDestination(Rule rule, ReportType reportType , bool isSource)
        {
            StringBuilder result = new StringBuilder("");

            if (reportType.IsResolvedReport())
            {
                List<string> displayedLocations = new List<string>();
                foreach (NetworkLocation networkLocation in getNetworkLocations(isSource ? rule.Froms : rule.Tos))
                {
                    displayedLocations.Add(DisplayNetworkLocation(networkLocation, reportType).ToString());
                }

                if ((isSource && rule.SourceNegated) || (!isSource && rule.DestinationNegated))
                {
                    result.Append($"{userConfig.GetText("negated")}(");
                }
                result.Append(string.Join(",", displayedLocations));
                if ((isSource && rule.SourceNegated) || (!isSource && rule.DestinationNegated))
                {
                    result.Append(")");
                }
            }

            return result.ToString();
        }
    }
}
