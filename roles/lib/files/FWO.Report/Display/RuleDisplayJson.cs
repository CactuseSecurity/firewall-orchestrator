using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayJson : RuleDisplayBase
    {
        public RuleDisplayJson(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplayJsonPlain(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": {value}," : "");
        }

        public string DisplayJsonString(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": \"{value}\"," : "");
        }

        public string DisplayJsonArray(string tag, string? value)
        {
            return (value != null ? $"\"{tag}\": [{value}]," : "");
        }


        public new string DisplayNumber(Rule rule)
        {
            return DisplayJsonPlain("number", rule.DisplayOrderNumber.ToString());
        }

        public string DisplayName(string? name)
        {
            return DisplayJsonString("name", name);
        }

        public string DisplaySourceZone(string? sourceZone)
        {
            return DisplayJsonString("source zone", sourceZone);
        }

        public string DisplaySourceNegated(bool sourceNegated)
        {
            return DisplayJsonPlain("source negated", sourceNegated.ToString().ToLower());
        }

        public string DisplaySource(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("source", ListNetworkLocations(rule, reportType, true));
        }

        public string DisplayDestinationZone(string? destinationZone)
        {
            return DisplayJsonString("destination zone", destinationZone);
        }

        public string DisplayDestinationNegated(bool destinationNegated)
        {
            return DisplayJsonPlain("destination negated", destinationNegated.ToString().ToLower());
        }

        public string DisplayDestination(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("destination", ListNetworkLocations(rule, reportType, false));
        }

        public string DisplayServiceNegated(bool serviceNegated)
        {
            return DisplayJsonPlain("service negated", serviceNegated.ToString().ToLower());
        }

        public string DisplayServices(Rule rule, ReportType reportType)
        {
            return DisplayJsonArray("service", ListServices(rule, reportType));
        }

        public string DisplayAction(string? action)
        {
            return DisplayJsonString("action", action);
        }

        public string DisplayTrack(string? track)
        {
            return DisplayJsonString("tracking", track);
        }

        public string DisplayUid(string? uid)
        {
            return DisplayJsonString("rule uid", uid);
        }

        public string DisplayEnabled(bool disabled)
        {
            return DisplayJsonPlain("disabled", disabled.ToString().ToLower());
        }

        public string DisplayComment(string? comment)
        {
            return DisplayJsonString("comment", comment);
        }

        protected string ListNetworkLocations(Rule rule, ReportType reportType, bool isSource)
        {
            if (reportType.IsResolvedReport())
            {
                List<string> displayedLocations = new List<string>();
                foreach (NetworkLocation networkLocation in getNetworkLocations(isSource ? rule.Froms : rule.Tos))
                {
                    displayedLocations.Add(Quote(DisplayNetworkLocation(networkLocation, reportType).ToString()));
                }
                return string.Join(",", displayedLocations);
            }
            return "";
        }

        protected string ListServices(Rule rule, ReportType reportType)
        {
            if (reportType.IsResolvedReport())
            {
                List<string> displayedServices = new List<string>();
                foreach (NetworkService service in getNetworkServices(rule.Services))
                {
                    displayedServices.Add(Quote(DisplayService(service, reportType).ToString()));
                }
                return(string.Join(",", displayedServices));
            }
            return "";
        }
    }
}
