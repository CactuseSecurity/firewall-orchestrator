using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayHtml: RuleDisplayBase
    {
        public RuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplaySource(Rule rule, OutputLocation location, ReportType reportType, string style = "")
        {
            return DisplaySourceOrDestination(rule, location, reportType, style, true);
        }

        public string DisplayDestination(Rule rule, OutputLocation location, ReportType reportType, string style = "")
        {
            return DisplaySourceOrDestination(rule, location, reportType, style, false);
        }

        public string DisplayServices(Rule rule, OutputLocation location, ReportType reportType, string style = "")
        {
            StringBuilder result = new StringBuilder();
            if (rule.ServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            if(reportType.IsResolvedReport())
            {
                NetworkService[] services = GetNetworkServices(rule.Services).ToArray();
                result.AppendJoin("<br>", Array.ConvertAll(services, service => ServiceToHtml(service, rule.MgmtId, location, style, reportType)));
            }
            else
            {
                result.AppendJoin("<br>", Array.ConvertAll(rule.Services, service => ServiceToHtml(service.Content, rule.MgmtId, location, style, reportType)));
            }
            return result.ToString();
        }

        public string DisplayEnabled(Rule rule, OutputLocation location)
        {
            if (location == OutputLocation.export)
            {
                return $"<b>{(rule.Disabled ? "N" : "Y")}</b>";
            }
            else
            {
                return $"<div class=\"oi {(rule.Disabled ? "oi-x" : "oi-check")}\"></div>";
            }
        }

        public string DisplayNextRecert(Rule rule)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(rule.Metadata.RuleRecertification.ToArray(), recert => getNextRecertDateString(countString(rule.Metadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public string DisplayOwner(Rule rule)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(rule.Metadata.RuleRecertification.ToArray(), recert => getOwnerDisplayString(countString(rule.Metadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public string DisplayRecertIpMatches(Rule rule)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(rule.Metadata.RuleRecertification.ToArray(), recert => getIpMatchDisplayString(countString(rule.Metadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public string DisplayLastHit(Rule rule)
        {
            if (rule.Metadata.LastHit == null)
                return "";
            else
                return DateOnly.FromDateTime((DateTime)rule.Metadata.LastHit).ToString("yyyy-MM-dd");  //rule.Metadata.LastHit.ToString("yyyy-MM-dd");
        }

        public string DisplayLastRecertifier(Rule rule)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(rule.Metadata.RuleRecertification.ToArray(), recert => getLastRecertifierDisplayString(countString(rule.Metadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        protected string NetworkLocationToHtml(NetworkLocation networkLocation, int mgmtId, OutputLocation location, string style, ReportType reportType)
        {
            return DisplayNetworkLocation(networkLocation, reportType, 
                reportType.IsResolvedReport() || networkLocation.User == null ? null :
                ReportDevicesBase.ConstructLink(ObjCatString.User, ReportBase.GetIconClass(ObjCategory.user, networkLocation.User?.Type.Name), networkLocation.User!.Id, networkLocation.User.Name, location, mgmtId, style),
                reportType.IsResolvedReport() ? null :
                ReportDevicesBase.ConstructLink(ObjCatString.NwObj, ReportBase.GetIconClass(ObjCategory.nobj, networkLocation.Object.Type.Name), networkLocation.Object.Id, networkLocation.Object.Name, location, mgmtId, style)
                ).ToString();
        }

        protected string ServiceToHtml(NetworkService service, int mgmtId, OutputLocation location, string style, ReportType reportType)
        {
            return DisplayService(service, reportType, reportType.IsResolvedReport() ? null : 
                ReportDevicesBase.ConstructLink(ObjCatString.Svc, ReportBase.GetIconClass(ObjCategory.nsrv, service.Type.Name), service.Id, service.Name, location, mgmtId, style)).ToString();
        }

        private string DisplaySourceOrDestination(Rule rule, OutputLocation location, ReportType reportType, string style, bool isSource)
        {
            StringBuilder result = new StringBuilder();
            if ((isSource && rule.SourceNegated) ||(!isSource && rule.DestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            if(reportType.IsResolvedReport())
            {
                NetworkLocation[] userNwObjects = getNetworkLocations(isSource ? rule.Froms : rule.Tos).ToArray();
                result.AppendJoin("<br>", Array.ConvertAll(userNwObjects, networkLocation => NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style, reportType)));
            }
            else
            {
                result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.Froms : rule.Tos, networkLocation => NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style, reportType)));
            }

            return result.ToString();
        }

        private string getNextRecertDateString (string countString, Recertification recert)
        {
            string color = "";
            string dateOnly = "-";
            if (recert.NextRecertDate != null)
            {
                dateOnly = DateOnly.FromDateTime((DateTime)recert.NextRecertDate).ToString("yyyy-MM-dd");
                if(recert.NextRecertDate < DateTime.Now)
                {
                    color = " style=\"color: red;\"";
                }
            }
            return "<p" + color + ">" + countString + dateOnly + "</p>";
        }

        private string getOwnerDisplayString (string countString, Recertification recert)
        {
            return "<p>" + countString + (recert.FwoOwner != null && recert.FwoOwner?.Name != null ? recert.FwoOwner.Name : "") + "</p>";
        }

        private string getIpMatchDisplayString (string countString, Recertification recert)
        {
            return "<p>" + countString + (recert.IpMatch != null && recert.IpMatch != "" ? recert.IpMatch : "&#8208;") + "</p>";
        }

        private string getLastRecertifierDisplayString (string countString, Recertification recert)
        {
            return "<p>" + countString + "</p>"; // TODO: fetch last recertifier
        }

        private string countString(bool multipleOwners, int ownerCounter)
        {
            return multipleOwners ? ownerCounter.ToString() + ".&nbsp;" : "";
        }
    }
}
