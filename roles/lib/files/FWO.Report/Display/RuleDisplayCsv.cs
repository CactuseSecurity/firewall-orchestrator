using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Encoders;
using FWO.Report;
using FWO.Report.Filter;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using FWO.Data.Labelling;

namespace FWO.Ui.Display
{
    public partial class RuleDisplayCsv : RuleDisplayBase
    {
        public RuleDisplayCsv(UserConfig userConfig) : base(userConfig)
        { }

        public string OutputCsv(string? input)
        {
            return $"{CsvOutputEncoder.EncodeCell(input)},";
        }

        /// <summary>
        /// Csv header field of the source labels, empty if csv contains no side labels.
        /// </summary>
        public string DisplaySourceLabelsCsvHeader()
        {
            return ShowSourceLabelField ? "\"source-labels\"," : "";
        }

        /// <summary>
        /// Csv header field of the destination labels, empty if csv contains no side labels.
        /// </summary>
        public string DisplayDestinationLabelsCsvHeader()
        {
            return ShowDestinationLabelField ? "\"destination-labels\"," : "";
        }

        /// <summary>
        /// Csv header field of the rule labels, empty if they are not shown.
        /// </summary>
        public string DisplayRuleLabelsCsvHeader()
        {
            return ShowRuleLevelLabels ? "\"rule-labels\"," : "";
        }

        /// <summary>
        /// Csv field of the source labels with AND / OR, empty if csv contains no side labels.
        /// </summary>
        public string DisplaySourceLabelsCsv(Rule rule)
        {
            return ShowSourceLabelField ? OutputCsv(FormatSideLabelsCsv(rule.SourceLabels)) : "";
        }

        /// <summary>
        /// Csv field of the destination labels with AND / OR, empty if csv contains no side labels.
        /// </summary>
        public string DisplayDestinationLabelsCsv(Rule rule)
        {
            return ShowDestinationLabelField ? OutputCsv(FormatSideLabelsCsv(rule.DestinationLabels)) : "";
        }

        /// <summary>
        /// Csv field of the rule labels, empty if they are not shown.
        /// </summary>
        public string DisplayRuleLabelsCsv(Rule rule)
        {
            return ShowRuleLevelLabels ? OutputCsv(RuleLabelFormatter.FormatRuleLabels(rule.RuleLabels)) : "";
        }

        /// <summary>
        /// Formats the labels of a rule side with AND / OR.
        /// </summary>
        public static string FormatSideLabelsCsv(List<Dictionary<string, List<string>>>? labels)
        {
            return RuleLabelFormatter.FormatSideLabels(labels, RuleLabelFormatter.kCsvAnd, RuleLabelFormatter.kCsvOr);
        }

        public string DisplayNumberCsv(Rule rule)
        {
            return OutputCsv(rule.OrderNumber.ToString(CultureInfo.InvariantCulture));
        }

        public string DisplayNameCsv(Rule rule)
        {
            return OutputCsv(DisplayName(rule));
        }

        public string DisplaySourceZoneCsv(Rule rule)
        {
            return OutputCsv(ListFirewallZones(rule.RuleFromZones.Select(z => z.Content).ToArray()));
        }

        public string DisplaySourceCsv(Rule rule, ReportType reportType)
        {
            return OutputCsv(DisplaySource(rule, reportType));
        }

        public string DisplayDestinationZoneCsv(Rule rule)
        {
            return OutputCsv(ListFirewallZones(rule.RuleToZones.Select(z => z.Content).ToArray()));
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

        public string DisplayLastModifiedCsv(Rule rule)
        {
            return OutputCsv(DisplayLastModified(rule));
        }


        public new string DisplayName(Rule rule)
        {
            return rule.Name != null ? SanitizeComment(rule.Name) : "";
        }

        public new string DisplayComment(Rule rule)
        {
            return rule.Comment != null ? SanitizeComment(rule.Comment) : "";
        }

        public string DisplayComment(NetworkObject nwo)
        {
            return nwo.Comment != null ? SanitizeComment(nwo.Comment) : "";
        }
        public string DisplayComment(NetworkService nws)
        {
            return nws.Comment != null ? SanitizeComment(nws.Comment) : "";
        }
        public string DisplayComment(NetworkUser nwu)
        {
            return nwu.Comment != null ? SanitizeComment(nwu.Comment) : "";
        }

        public string DisplayEnabled(Rule rule)
        {
            return rule.Disabled ? "disabled" : "enabled";
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
                List<string> displayedServices = [];
                foreach (NetworkService service in GetNetworkServices(rule.Services))
                {
                    displayedServices.Add(DisplayService(service, reportType).ToString());
                }

                if (rule.ServiceNegated)
                {
                    result.Append($"{userConfig.GetText("negated")}(");
                }
                result.Append(string.Join(",", displayedServices));
                if (rule.ServiceNegated)
                {
                    result.Append(")");
                }
            }
            return result.ToString();
        }

        private static string SanitizeComment(string inputString)
        {
            string output = QuotesRegex().Replace(inputString, "").Trim();
            output = output.Replace("\n", ", ").Trim();
            return output;
        }

        private string DisplaySourceOrDestination(Rule rule, ReportType reportType, bool isSource)
        {
            if (!reportType.IsResolvedReport())
            {
                return "";
            }

            NetworkLocation[] networkLocations = isSource ? rule.Froms : rule.Tos;
            string displayedLocations = string.Join(",", GetResolvedNetworkLocations(networkLocations)
                .Select(networkLocation => DisplayNetworkLocation(networkLocation, reportType).ToString()));
            bool negated = isSource ? rule.SourceNegated : rule.DestinationNegated;
            return negated ? $"{userConfig.GetText("negated")}({displayedLocations})" : displayedLocations;
        }

        public static string DisplayEnforcingGateways(Rule rule)
        {
            return string.Join(",",
                rule.EnforcingGateways?
                    .Select(g => g.Content?.Name ?? "")
                ?? Enumerable.Empty<string>());
        }

        protected static string ListFirewallZones(FirewallZone[] firewallZones)
        {
            List<string> displayedZones = [];
            foreach (FirewallZone networkZone in firewallZones)
            {
                displayedZones.Add(Quote(networkZone.Name));
            }
            return string.Join(",", displayedZones);
        }

        [GeneratedRegex(@"[""'']")]
        private static partial Regex QuotesRegex();
    }
}
