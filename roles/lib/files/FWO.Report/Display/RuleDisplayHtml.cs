using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report;
using FWO.Data.Labelling;

namespace FWO.Ui.Display
{
    public class RuleDisplayHtml(UserConfig userConfig) : RuleDisplayBase(userConfig)
    {
        public string DisplayRuleTime(Rule rule)
        {
            // Quick implementation to satisfy current requirements: use only the first time object with an EndTime.
            DateTimeOffset? firstEndTime = rule.RuleTimes.FirstOrDefault(ruleTime => ruleTime.TimeObj?.EndTime != null)?.TimeObj?.EndTime;
            return firstEndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayName(Rule)"/>: the name is encoded.
        /// </summary>
        public static new string DisplayName(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayName(rule));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayName(NetworkObject)"/>: the name is encoded.
        /// </summary>
        public static new string DisplayName(NetworkObject nto)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayName(nto));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayName(NetworkService)"/>: the name is encoded.
        /// </summary>
        public static new string DisplayName(NetworkService nts)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayName(nts));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayName(NetworkUser)"/>: the name is encoded.
        /// </summary>
        public static new string DisplayName(NetworkUser ntu)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayName(ntu));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayAction(Rule)"/>: the action is encoded.
        /// </summary>
        public static new string DisplayAction(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayAction(rule));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayTrack(Rule)"/>: the track is encoded.
        /// </summary>
        public static new string DisplayTrack(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayTrack(rule));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayUid(Rule)"/>: the uid is encoded.
        /// </summary>
        public static new string DisplayUid(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayUid(rule));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayUid(NetworkObject)"/>: the uid is encoded.
        /// </summary>
        public static new string DisplayUid(NetworkObject nwo)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayUid(nwo));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayUid(NetworkService)"/>: the uid is encoded.
        /// </summary>
        public static new string DisplayUid(NetworkService nws)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayUid(nws));
        }

        /// <summary>
        /// Html variant of <see cref="RuleDisplayBase.DisplayComment(Rule)"/>: the comment is encoded.
        /// </summary>
        public static new string DisplayComment(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleDisplayBase.DisplayComment(rule));
        }

        /// <summary>
        /// Encoded source labels of a rule, e.g. "(AppRole: AR1 ∩ Stage: Prod) ∪ (AppRole: AR2)".
        /// </summary>
        public static string DisplaySourceLabels(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleLabelFormatter.FormatSideLabels(rule.SourceLabels, RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr));
        }

        /// <summary>
        /// Encoded destination labels of a rule.
        /// </summary>
        public static string DisplayDestinationLabels(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleLabelFormatter.FormatSideLabels(rule.DestinationLabels, RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr));
        }

        /// <summary>
        /// Encoded labels of a rule as a whole, e.g. "App: APP-1".
        /// </summary>
        public static string DisplayRuleLabels(Rule rule)
        {
            return HtmlOutputEncoder.EncodeText(RuleLabelFormatter.FormatRuleLabels(rule.RuleLabels));
        }

        /// <summary>
        /// Header cell of the source labels column, empty if the column is not shown.
        /// </summary>
        public string DisplaySourceLabelsHeader()
        {
            return ShowSourceLabelColumn ? $"<th>{userConfig.GetText("source_labels")}</th>" : "";
        }

        /// <summary>
        /// Header cell of the destination labels column, empty if the column is not shown.
        /// </summary>
        public string DisplayDestinationLabelsHeader()
        {
            return ShowDestinationLabelColumn ? $"<th>{userConfig.GetText("destination_labels")}</th>" : "";
        }

        /// <summary>
        /// Header cell of the rule labels column, empty if the column is not shown.
        /// </summary>
        public string DisplayRuleLabelsHeader()
        {
            return ShowRuleLevelLabels ? $"<th>{userConfig.GetText("rule_labels")}</th>" : "";
        }

        /// <summary>
        /// Cell of the source labels column, empty if the column is not shown.
        /// </summary>
        public string DisplaySourceLabelsCell(Rule rule)
        {
            return ShowSourceLabelColumn ? $"<td>{DisplaySourceLabels(rule)}</td>" : "";
        }

        /// <summary>
        /// Cell of the destination labels column, empty if the column is not shown.
        /// </summary>
        public string DisplayDestinationLabelsCell(Rule rule)
        {
            return ShowDestinationLabelColumn ? $"<td>{DisplayDestinationLabels(rule)}</td>" : "";
        }

        /// <summary>
        /// Cell of the rule labels column, empty if the column is not shown.
        /// </summary>
        public string DisplayRuleLabelsCell(Rule rule)
        {
            return ShowRuleLevelLabels ? $"<td>{DisplayRuleLabels(rule)}</td>" : "";
        }

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
            result.AppendJoin("<br>", Array.ConvertAll(rule.EnforcingGateways, gw => EnforcingGatewayToHtml(gw.Content, reportType)));
            return result.ToString();
        }
        public static string DisplaySectionHeader(Rule rule, int ColumnCount)
        {
            return $"<tr><td class=\"bg-gray\" colspan=\"{ColumnCount}\"><b>{HtmlOutputEncoder.EncodeText(rule.SectionHeader)}</b></td></tr>";
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
                return DateOnly.FromDateTime(ruleMetadata.LastHit.Value).ToString("yyyy-MM-dd");
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

            string nwLocation = DisplayNetworkLocationHtml(networkLocation, reportType, userOutput, objectLink);

            return reportType.IsRuleReport() ? $"<span style=\"{style}\">{nwLocation}</span>" : nwLocation;
        }

        protected static string ServiceToHtml(NetworkService service, int mgmtId, int chapterNumber, OutputLocation location, string style, ReportType reportType)
        {
            if (reportType.IsResolvedReport() || reportType == ReportType.VarianceAnalysis)
            {
                return DisplayServiceHtml(service, reportType);
            }
            else
            {
                // Construct link for unresolved report types
                string serviceLink = ReportDevicesBase.GetReportDevicesLinkAddress(location, mgmtId, ObjCatString.Svc, chapterNumber, service.Id, reportType);
                string serviceName = ReportBase.ConstructLink(ReportBase.GetIconClass(ObjCategory.nsrv, service.Type.Name), service.Name, style, serviceLink);
                return DisplayServiceHtml(service, reportType, serviceName);
            }
        }
        protected static string EnforcingGatewayToHtml(Device gateway, ReportType reportType)
        {
            // DisplayGateway shows the plain gateway name in every report type, so it is encoded as a whole
            return HtmlOutputEncoder.EncodeText(DisplayGateway(gateway, reportType).ToString());
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
            else
            {
                result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.Froms : rule.Tos,
                    nwLoc => NetworkLocationToHtml(nwLoc, rule.MgmtId, chapterNumber, location, highlightedStyle, reportType)));
            }
            if (reportType == ReportType.AppRules)
            {
                result.Append(DisplayAppRuleSourceOrDestination(rule, chapterNumber, location, reportType, style, highlightedStyle, isSource));
            }
            AppendLabelsToObjects(result, isSource ? DisplaySourceLabels(rule) : DisplayDestinationLabels(rule));

            return result.ToString();
        }

        private void AppendLabelsToObjects(StringBuilder result, string labels)
        {
            if (ShowSideLabelsWithObjects && labels.Length > 0)
            {
                result.Append(result.Length > 0 ? "<br>" : "").Append(labels);
            }
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
            return "<p>" + countString + HtmlOutputEncoder.EncodeText(recert.FwoOwner?.Name) + "</p>";
        }

        private static string GetIpMatchDisplayString(string countString, Recertification recert)
        {
            return "<p>" + countString + (recert.IpMatch != null && recert.IpMatch != "" ? HtmlOutputEncoder.EncodeText(recert.IpMatch) : "&#8208;") + "</p>";
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
