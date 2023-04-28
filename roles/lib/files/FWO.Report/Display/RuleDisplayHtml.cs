using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public enum OutputLocation
    {
        export,
        report,
        certification
    }

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

        public string DisplayService(Rule rule, OutputLocation location, ReportType reportType, string style = "")
        {
            result = new StringBuilder();
            if (rule.ServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            if(reportType.IsResolvedReport())
            {
                NetworkService[] services = getNetworkServices(rule.Services).ToArray();
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

        public string DisplayNextRecert(Rule rule, bool multipleOwners)
        {
            string result = "";
            int count = 0;
            foreach (Recertification recert in rule.Metadata.RuleRecertification) 
            {
                count += 1;
                result += getNextRecertDateString(count, recert, multipleOwners);
            }
            return result;
        }

        public string DisplayOwner(Rule rule, bool multipleOwners)
        {
            string result = "";
            int count = 0;
            foreach (Recertification recert in rule.Metadata.RuleRecertification) 
            {
                count += 1;
                result += getOwnerDisplayString(count, recert, multipleOwners);
            }
            return result;
        }

        public string DisplayRecertIpMatches(Rule rule, bool multipleOwners)
        {
            string result = "";
            int count = 0;
            foreach (Recertification recert in rule.Metadata.RuleRecertification) 
            {
                count += 1;
                result += getIpMatchDisplayString(count, recert, multipleOwners);
            }
            return result;
        }

        public string DisplayLastHit(Rule rule, bool multipleOwners)
        {
            if (rule.Metadata.LastHit == null)
                return "";
            else
                return DateOnly.FromDateTime((DateTime)rule.Metadata.LastHit).ToString("yyyy-MM-dd");  //rule.Metadata.LastHit.ToString("yyyy-MM-dd");
        }

        public string DisplayLastRecertifier(Rule rule, bool multipleOwners)
        {
            string result = "";
            int count = 1;
            foreach (Recertification recert in rule.Metadata.RuleRecertification) 
            {
                // result += count.ToString() + ".&nbsp;" + "" + "<br />";
                // TODO: fetch last recertifier
                count += 1;
            }
            return result;
        }

        protected string constructLink(string type, string symbol, long id, string name, OutputLocation location, int mgmtId, string style)
        {
            string link = location == OutputLocation.export ? $"#" : $"{location.ToString()}#goto-report-m{mgmtId}-";
            return $"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{link}{type}{id}\" target=\"_top\" style=\"{style}\">{name}</a>";
        }

        protected string getObjSymbol(string objType)
        {
            switch(objType)
            {
                case "group": return "oi oi-list-rich";
                case "network": return "oi oi-rss";
                case "ip_range": return "oi oi-resize-width";
                default: return "oi oi-monitor";
            }
        }

        private string DisplaySourceOrDestination(Rule rule, OutputLocation location, ReportType reportType, string style, bool isSource)
        {
            result = new StringBuilder();
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

        protected string NetworkLocationToHtml(NetworkLocation userNetworkObject, int mgmtId, OutputLocation location, string style, ReportType reportType)
        {
            StringBuilder result = new StringBuilder();
            
            if (userNetworkObject.User?.Id != null && userNetworkObject.User?.Id > 0)
            {
                if (reportType.IsResolvedReport())
                {
                    result.Append($"{userNetworkObject.User.Name}@");
                }
                else
                {
                    result.Append(constructLink("user", "oi oi-people", userNetworkObject.User.Id, userNetworkObject.User.Name, location, mgmtId, style) + "@");
                }
            }

            if(!reportType.IsTechReport())
            {
                if (reportType.IsResolvedReport())
                {
                    result.Append($"{userNetworkObject.Object.Name}");
                }
                else
                {
                    result.Append(constructLink("nwobj", getObjSymbol(userNetworkObject.Object.Type.Name), userNetworkObject.Object.Id, userNetworkObject.Object.Name, location, mgmtId, style));
                }
                if (userNetworkObject.Object.Type.Name != "group")
                {
                    result.Append(" (");
                }
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (!reportType.IsTechReport() && userNetworkObject.Object.Type.Name != "group")
            {
                result.Append(")");
            }
            return result.ToString();
        }

        protected string ServiceToHtml(NetworkService service, int mgmtId, OutputLocation location, string style, ReportType reportType)
        {
            StringBuilder result = new StringBuilder();
            if(!reportType.IsTechReport())
            {
                if (reportType.IsResolvedReport())
                {
                    result.Append($"{service.Name}");
                }
                else
                {
                    result.Append(constructLink("svc", service.Type.Name == "group" ? "oi oi-list-rich" : "oi oi-wrench", service.Id, service.Name, location, mgmtId, style));
                }
            }
            if (service.DestinationPort != null)
            {
                if (!reportType.IsTechReport())
                    result.Append(" (");
                result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                    : $"{service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
                if (!reportType.IsTechReport())
                    result.Append(")");
            }
            else if (reportType.IsTechReport())
            {
                // if no port can be displayed, use the service name as fall-back
                result.Append($"{service.Name}");
            }
            return result.ToString();
        }

        private string getNextRecertDateString (int ownerCounter, Recertification recert, bool multipleOwners)
        {
            string result = "";
            string color = "";
            string countString = multipleOwners ? ownerCounter.ToString() + ".&nbsp;" : "";
            string dateOnly = "-";
            if (recert.NextRecertDate != null)
            {
                dateOnly = DateOnly.FromDateTime((DateTime)recert.NextRecertDate).ToString("yyyy-MM-dd");
                if(recert.NextRecertDate < DateTime.Now)
                {
                    color = " style=\"color:rgb(255, 0, 0);\"";
                }
            }
            result = "<p" + color + ">" + countString + dateOnly + "</p>";
            return result;
        }

        private string getOwnerDisplayString (int ownerCounter, Recertification recert, bool multipleOwners)
        {
            string result = "";
            string countString = multipleOwners ? ownerCounter.ToString() + ".&nbsp;" : "";
            if (recert.FwoOwner != null && recert.FwoOwner.Name != null)
            {
                result += countString + recert.FwoOwner.Name + "<br />";
            }
            return result;
        }

        private string getIpMatchDisplayString (int ownerCounter, Recertification recert, bool multipleOwners)
        {
            string result = "";
            string matchString = "&#8208;";
            string countString = multipleOwners ? ownerCounter.ToString() + ".&nbsp;" : "";
            if (recert.IpMatch != null && recert.IpMatch != "")
            {
                matchString = recert.IpMatch;
            }
            result += countString + matchString + "<br />";
            return result;
        }
    }
}
