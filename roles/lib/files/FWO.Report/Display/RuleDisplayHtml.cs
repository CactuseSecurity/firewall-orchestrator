using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report;

namespace FWO.Ui.Display
{
    public class RuleDisplayHtml(UserConfig userConfig) : RuleDisplayBase(userConfig)
    {
        public string DisplaySource(Rule rule, OutputLocation location, ReportType reportType, int chapterNumber = 0, string style = "", bool overwriteIsResolvedReport = false)
        {
            return DisplaySourceOrDestination(rule, chapterNumber, location, reportType, style, true, overwriteIsResolvedReport);
        }

        public string DisplayDestination(Rule rule, OutputLocation location, ReportType reportType, int chapterNumber = 0, string style = "", bool overwriteIsResolvedReport = false)
        {
            return DisplaySourceOrDestination(rule, chapterNumber, location, reportType, style, false, overwriteIsResolvedReport);
        }

        public string DisplayServices(Rule rule, OutputLocation location, ReportType reportType, int chapterNumber = 0, string style = "", bool overwriteIsResolvedReport = false)
        {
            StringBuilder result = new();
            if (rule.ServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            if (!overwriteIsResolvedReport && reportType.IsResolvedReport())
            {
                NetworkService[] services = GetNetworkServices(rule.Services).ToArray();
                result.AppendJoin("<br>", Array.ConvertAll(services, service => ServiceToHtml(service, rule.MgmtId, chapterNumber, location, style, reportType)));
            }
            else
            {
                result.AppendJoin("<br>", Array.ConvertAll(rule.Services, service => ServiceToHtml(service.Content, rule.MgmtId, chapterNumber, location, style, reportType)));
            }

            return result.ToString();
        }

        public static string DisplayEnforcingGateways(Rule rule, OutputLocation location, ReportType reportType, int chapterNumber = 0, string style = "")
        {
            StringBuilder result = new();
            result.AppendJoin("<br>", Array.ConvertAll(rule.EnforcingGateways, gw => EnforcingGatewayToHtml(gw.Content, rule.MgmtId, chapterNumber, location, style, reportType)));
            return result.ToString();
        }
        public static string DisplaySectionHeader(Rule rule, int ColumnCount)
        {
            return $"<tr><td class=\"bg-gray\" colspan=\"{ColumnCount}\"><b>{rule.SectionHeader}</b></td></tr>";
        }

        public static string DisplayNextRecert(RuleMetadata ruleMetadata)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(ruleMetadata.RuleRecertification.ToArray(), recert => GetNextRecertDateString(CountString(ruleMetadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public static string DisplayOwner(RuleMetadata ruleMetadata)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(ruleMetadata.RuleRecertification.ToArray(), recert => GetOwnerDisplayString(CountString(ruleMetadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public static string DisplayRecertIpMatches(RuleMetadata ruleMetadata)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(ruleMetadata.RuleRecertification.ToArray(), recert => GetIpMatchDisplayString(CountString(ruleMetadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        public static string DisplayLastHit(RuleMetadata ruleMetadata)
        {
            if (ruleMetadata.LastHit == null)
                return "";
            else
                return DateOnly.FromDateTime((DateTime)ruleMetadata.LastHit).ToString("yyyy-MM-dd");
        }

        public static string DisplayLastRecertifier(RuleMetadata ruleMetadata)
        {
            int count = 0;
            return string.Join("", Array.ConvertAll<Recertification, string>(ruleMetadata.RuleRecertification.ToArray(),
                recert => GetLastRecertifierDisplayString(CountString(ruleMetadata.RuleRecertification.Count > 1, ++count), recert).ToString()));
        }

        protected static string NetworkLocationToHtml(NetworkLocation networkLocation, int mgmtId, int chapterNumber, OutputLocation location, string style, ReportType reportType)
        {
            // Determine if links should be constructed
            bool isResolved = reportType.IsResolvedReport() || reportType == ReportType.VarianceAnalysis;

            string? userOutput = null;
            if (!isResolved && networkLocation.User != null)
            {
                string userLink = ReportDevicesBase.GetReportDevicesLinkAddress(location, mgmtId, ObjCatString.User, chapterNumber, networkLocation.User.Id, reportType);
                userOutput = ReportBase.ConstructLink(ReportBase.GetIconClass(ObjCategory.user, networkLocation.User.Type.Name), networkLocation.User.Name, style, userLink);
            }

            string? objectLink = null;
            if (!isResolved)
            {
                string objLink = ReportDevicesBase.GetReportDevicesLinkAddress(location, mgmtId, ObjCatString.NwObj, chapterNumber, networkLocation.Object.Id, reportType);
                objectLink = ReportBase.ConstructLink(ReportBase.GetIconClass(ObjCategory.nobj, networkLocation.Object.Type.Name), networkLocation.Object.Name, style, objLink);
            }

            string nwLocation = DisplayNetworkLocation(networkLocation, reportType, userOutput, objectLink).ToString();

            return reportType.IsRuleReport() ? $"<span style=\"{style}\">{nwLocation}</span>" : nwLocation;
        }

        protected static string ServiceToHtml(NetworkService service, int mgmtId, int chapterNumber, OutputLocation location, string style, ReportType reportType)
        {
            if (reportType.IsResolvedReport() || reportType == ReportType.VarianceAnalysis)
            {
                return DisplayService(service, reportType, null).ToString();
            }
            else
            {
                // Construct link for unresolved report types
                string serviceLink = ReportDevicesBase.GetReportDevicesLinkAddress(location, mgmtId, ObjCatString.Svc, chapterNumber, service.Id, reportType);
                string serviceName = ReportBase.ConstructLink(ReportBase.GetIconClass(ObjCategory.nsrv, service.Type.Name), service.Name, style, serviceLink);
                return DisplayService(service, reportType, serviceName).ToString();
            }
        }
        protected static string EnforcingGatewayToHtml(Device gateway, int mgmtId, int chapterNumber, OutputLocation location, string style, ReportType reportType)
        {
            string gwLink = ReportDevicesBase.GetReportDevicesLinkAddress(location, mgmtId, ObjCatString.NwObj, chapterNumber, gateway.Id, reportType);

            return DisplayGateway(gateway, reportType, reportType.IsResolvedReport() ? null :
                ReportBase.ConstructLink(ReportBase.GetIconClass(ObjCategory.nsrv, "Gateway"), gateway.Name ?? string.Empty, style, gwLink)).ToString();
        }

        private string DisplaySourceOrDestination(Rule rule, int chapterNumber, OutputLocation location, ReportType reportType, string style, bool isSource, bool overwriteIsResolvedReport = false)
        {
            StringBuilder result = new();
            if ((isSource && rule.SourceNegated) || (!isSource && rule.DestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }
            string highlightedStyle = style + (reportType == ReportType.AppRules ? " " + GlobalConst.kStyleHighlightedRed : "");

            if (!overwriteIsResolvedReport && reportType.IsResolvedReport())
            {
                NetworkLocation[] userNwObjects = [.. GetResolvedNetworkLocations(isSource ? rule.Froms : rule.Tos)];
                result.AppendJoin("<br>", Array.ConvertAll(userNwObjects,
                    nwLoc => NetworkLocationToHtml(nwLoc, rule.MgmtId, chapterNumber, location, highlightedStyle, reportType)));
            }
            else if (reportType == ReportType.AppRules)
            {
                result.Append(DisplayAppRuleSourceOrDestination(rule, chapterNumber, location, reportType, style, highlightedStyle, isSource));
            }
            else
            {
                result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.Froms : rule.Tos,
                    nwLoc => NetworkLocationToHtml(nwLoc, rule.MgmtId, chapterNumber, location, highlightedStyle, reportType)));
            }
            return result.ToString();
        }

        private string DisplayAppRuleSourceOrDestination(Rule rule, int chapterNumber, OutputLocation location, ReportType reportType, string style, string highlightedStyle, bool isSource)
        {
            StringBuilder result = new();
            if (!rule.ShowDisregarded &&
                ((isSource && rule.Froms.Length > 0 && rule.DisregardedFroms.Length > 0) ||
                (!isSource && rule.Tos.Length > 0 && rule.DisregardedTos.Length > 0)))
            {
                result.Append($"<br><span class=\"text-secondary\">... ({(isSource ? rule.DisregardedFroms.Length : rule.DisregardedTos.Length)} {userConfig.GetText("more")})</span>");
            }
            else
            {
                if (result.Length > 0)
                {
                    result.Append("<br>");
                }
                result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.DisregardedFroms : rule.DisregardedTos,
                    nwLoc => NetworkLocationToHtml(nwLoc, rule.MgmtId, chapterNumber, location, nwLoc.Object.IsAnyObject() ? highlightedStyle : style, reportType)));
            }
            return result.ToString();
        }

        private static string GetNextRecertDateString(string countString, Recertification recert)
        {
            string color = "";
            string dateOnly = "-";
            if (recert.NextRecertDate != null)
            {
                dateOnly = DateOnly.FromDateTime((DateTime)recert.NextRecertDate).ToString("yyyy-MM-dd");
                if (recert.NextRecertDate < DateTime.Now)
                {
                    color = $" style=\"{GlobalConst.kStyleHighlightedRed}\"";
                }
            }
            return "<p" + color + ">" + countString + dateOnly + "</p>";
        }

        private static string GetOwnerDisplayString(string countString, Recertification recert)
        {
            return "<p>" + countString + (recert.FwoOwner != null && recert.FwoOwner?.Name != null ? recert.FwoOwner.Name : "") + "</p>";
        }

        private static string GetIpMatchDisplayString(string countString, Recertification recert)
        {
            return "<p>" + countString + (recert.IpMatch != null && recert.IpMatch != "" ? recert.IpMatch : "&#8208;") + "</p>";
        }

        private static string GetLastRecertifierDisplayString(string countString, Recertification recert)
        {
            return "<p>" + countString + "</p>"; // TODO: fetch last recertifier
        }

        private static string CountString(bool multipleOwners, int ownerCounter)
        {
            return multipleOwners ? ownerCounter.ToString() + ".&nbsp;" : "";
        }
    }
}
