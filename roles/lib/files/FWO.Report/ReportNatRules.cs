using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report.Filter;
using FWO.Ui.Display;
using System.Text;

namespace FWO.Report
{
    public class ReportNatRules : ReportRules
    {
        public ReportNatRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        private const int ColumnCount = 12;

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
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        AppendNatRuleHeadlineHtml(ref report, device.Name);
                        foreach (var rule in device.Rules)
                        {
                            AppendNatRuleForDeviceHtml(ref report, chapterNumber, rule, ruleDisplay);
                        }
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

        private void AppendNatRuleHeadlineHtml(ref StringBuilder report, string? deviceName)
        {
            report.AppendLine($"<h4 id=\"{Guid.NewGuid()}\">{deviceName}</h4>");
            report.AppendLine("<table>");
            report.AppendLine("<tr>");
            report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source_zone")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination_zone")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("destination")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("trans_source")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("trans_destination")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("trans_services")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("enabled")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
            report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
            report.AppendLine("</tr>");

        }

        private void AppendNatRuleForDeviceHtml(ref StringBuilder report, int chapterNumber, Rule rule, NatRuleDisplayHtml ruleDisplay)
        {
            if (string.IsNullOrEmpty(rule.SectionHeader))
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td>{ruleDisplay.DisplayNumber(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayName(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplaySourceZone(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplaySource(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayDestinationZone(rule)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayDestination(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayServices(rule, OutputLocation.export, ReportType, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedSource(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedDestination(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedService(rule, OutputLocation.export, chapterNumber)}</td>");
                report.AppendLine($"<td>{RuleDisplayHtml.DisplayEnabled(rule, OutputLocation.export)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayUid(rule)}</td>");
                report.AppendLine($"<td>{RuleDisplayBase.DisplayComment(rule)}</td>");
                report.AppendLine("</tr>");
            }
            else
            {
                report.AppendLine("<tr>");
                report.AppendLine($"<td style=\"background-color: #f0f0f0;\" colspan=\"{ColumnCount}\">{rule.SectionHeader}</td>");
                report.AppendLine("</tr>");
            }
        }
    }
}
