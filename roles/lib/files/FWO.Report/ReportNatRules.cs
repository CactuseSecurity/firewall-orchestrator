using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report.Filter;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Display;
using System.Text;

namespace FWO.Report
{
    public class ReportNatRules : ReportRules
    {
        public ReportNatRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType, IRuleTreeBuilder? ruleTreeBuilder = null) : base(query, userConfig, reportType, ruleTreeBuilder) { }

        private static readonly string[] HeaderKeys =
        [
            "number", "name", "source_zone", "source", "destination_zone", "destination",
            "services", "trans_source", "trans_destination", "trans_services", "enabled", "uid", "comment"
        ];

        public override string ExportToHtml()
        {
            StringBuilder report = new();
            NatRuleDisplayHtml ruleDisplay = new(userConfig);
            int chapterNumber = 0;

            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                chapterNumber++;
                report.AppendLine($"<h3 id=\"{Guid.NewGuid()}\">{managementReport.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    Rule[] deviceNatRules = GetCachedRulesForExport(device.Id, managementReport.Id);
                    if (deviceNatRules.Length > 0)
                    {
                        AppendNatRuleHeadlineHtml(ref report, device.Name);

                        report.AppendLine(ExportSingleRulebaseToHtml(deviceNatRules, ruleDisplay, chapterNumber));

                        report.AppendLine("</table>");
                        report.AppendLine("<hr>");
                    }
                }
                // show all objects used in this management's rules
                AppendNetworkObjectsForManagementHtml(ref report, chapterNumber, managementReport);
                AppendNetworkServicesForManagementHtml(ref report, chapterNumber, managementReport);
                AppendUsersForManagementHtml(ref report, chapterNumber, managementReport);
                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }

        public string ExportSingleRulebaseToHtml(Rule[] rulebase, NatRuleDisplayHtml ruleDisplay, int chapterNumber)
        {
            StringBuilder report = new();
            foreach (var rule in rulebase)
            {
                AppendNatRuleForDeviceHtml(ref report, chapterNumber, rule, ruleDisplay);
            }
            return report.ToString();
        }

        private void AppendNatRuleHeadlineHtml(ref StringBuilder report, string? deviceName)
        {
            report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{deviceName}</h4>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            foreach (string headerKey in HeaderKeys)
            {
                report.AppendLine($"<th>{userConfig.GetText(headerKey)}</th>");
            }
            report.AppendLine("</tr>");

        }

        private void AppendNatRuleForDeviceHtml(ref StringBuilder report, int chapterNumber, Rule rule, NatRuleDisplayHtml ruleDisplay)
        {
            if (string.IsNullOrEmpty(rule.SectionHeader))
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayHierarchicalNumber(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayName(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplaySourceZones(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplaySource(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayDestinationZones(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayDestination(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayServices(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedSource(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedDestination(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedService(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{NatRuleDisplayHtml.DisplayEnabled(rule, OutputLocation.export)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayUid(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayComment(rule)}</td>");
                report.AppendLine("</tr>");
            }
            else
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td style=\"background-color: #f0f0f0;\" colspan=\"{HeaderKeys.Length}\">{rule.SectionHeader}</td>");
                report.AppendLine("</tr>");
            }
        }
    }
}
