using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayHtml: RuleDisplayBase
    {

        public RuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        {}

        private string DisplaySourceOrDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules, bool isSource = true)
        {
            if (location=="certification")
                reportType=ReportType.Rules;
            result = new StringBuilder();
            result.AppendLine("<p>");
            if ((isSource && rule.SourceNegated) ||(!isSource && rule.DestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + " <br>");
            }

            if(reportType.IsResolvedReport())
            {
                List<NetworkLocation> userNwObjectList = getNetworkLocations(isSource ? rule.Froms : rule.Tos);
                foreach (NetworkLocation networkLocation in userNwObjectList)
                {
                    result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style, reportType));
                }
            }
            else
            {
                foreach (NetworkLocation networkLocation in isSource ? rule.Froms : rule.Tos)
                {
                    result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style));
                }
            }

            result.AppendLine("</p>");
            return result.ToString();
        }

        public string DisplaySource(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, true);
        }

        public string DisplayDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, false);
        }

        private StringBuilder NetworkLocationToHtml(NetworkLocation userNetworkObject, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            string nwobjLink = "";
            string symbol = "oi oi-wrench";
            StringBuilder result = new StringBuilder();
            if (userNetworkObject.Object.Type.Name == "group")
                symbol = "oi oi-list-rich";
            else if (userNetworkObject.Object.Type.Name == "network")
                symbol = "oi oi-rss";
            else if (userNetworkObject.Object.Type.Name == "ip_range")
                symbol = "oi oi-resize-width";
            else
                symbol = "oi oi-monitor";
            
            if (userNetworkObject.User?.Id != null)
            {
                if (reportType.IsTechReport())
                    result.Append($"{userNetworkObject.User.Name}@");
                else
                {
                    string userLink = location == "" ? $"user{userNetworkObject.User.Id}" : $"goto-report-m{mgmtId}-user{userNetworkObject.User.Id}";
                    result.Append($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.User.Name}</a>@");
                }
            }

            nwobjLink = location == "" ? $"nwobj{userNetworkObject.Object.Id}" : $"goto-report-m{mgmtId}-nwobj{userNetworkObject.Object.Id}";

            if (!reportType.IsTechReport())
            {
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.Object.Name}</a>");
                if (userNetworkObject.Object.Type.Name != "group")
                    result.Append(" (");
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (userNetworkObject.Object.Type.Name != "group" && !reportType.IsTechReport())
                result.Append(")");
            result.AppendLine("<br>");
            return result;
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            if (location=="certification")
                reportType=ReportType.Rules;
            result = new StringBuilder();
            result.AppendLine("<p>");
            if (rule.ServiceNegated)
                result.AppendLine(userConfig.GetText("negated") + " <br>");

            if(reportType.IsResolvedReport())
            {
                List<NetworkService> serviceList = getNetworkServices(rule.Services);
                foreach (NetworkService service in serviceList)
                {
                    result.Append(ServiceToHtml(service, rule.MgmtId, location, style, reportType));
                }
            }
            else
            {
                foreach (ServiceWrapper service in rule.Services)
                {
                    result.Append(ServiceToHtml(service.Content, rule.MgmtId, location, style, reportType));
                }
            }

            result.AppendLine("</p>");
            return result.ToString();
        }

        private StringBuilder ServiceToHtml(NetworkService service, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            string link = "";
            string symbol = "oi oi-wrench";
            StringBuilder result = new StringBuilder();
            if (service.Type.Name == "group")
                symbol = "oi oi-list-rich";
            else
                symbol = "oi oi-wrench";
            link = location == "" ? $"svc{service.Id}" : $"goto-report-m{mgmtId}-svc{service.Id}";
            if (!reportType.IsTechReport())
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Name}</a>");

            if (service.DestinationPort != null)
            {
                if (!reportType.IsTechReport())
                    result.Append(" (");
                result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                    : $" {service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
                if (!reportType.IsTechReport())
                    result.Append(")");
            }
            else if (reportType.IsTechReport())
            {
                // if no port can be displayed, use the service name as fall-back
                result.Append($"{service.Name}");
            }
            result.AppendLine("<br>");
            return result;
        }

        public string DisplayEnabled(Rule rule, bool export = false)
        {
            if (export)
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
    }
}
