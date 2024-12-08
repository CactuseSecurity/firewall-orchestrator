using FWO.Basics;
using System.Text;
using FWO.Report.Filter;
using FWO.Ui.Display;
using FWO.Config.Api;

namespace FWO.Report
{
    public class ReportNatRules : ReportRules
    {
        public ReportNatRules(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

        private const int ColumnCount = 12;

        public override string ExportToHtml()
        {
            StringBuilder report = new ();
            NatRuleDisplayHtml ruleDisplay = new (userConfig);
            int chapterNumber = 0;

            foreach (var managementReport in ReportData.ManagementData.Where(mgt => !mgt.Ignore))
            {
                chapterNumber++;
                report.AppendLine($"<h3>{managementReport.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (var device in managementReport.Devices)
                {
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        report.AppendLine($"<h4>{device.Name}</h4>");
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

                        foreach (var rule in device.Rules)
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
                                report.AppendLine($"<td>{ruleDisplay.DisplayEnabled(rule, OutputLocation.export)}</td>");
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
                        report.AppendLine("</table>");
                        report.AppendLine("<hr>");
                    }
                }

                // show all objects used in this management's rules

                int objNumber = 1;
                if (managementReport.ReportObjects != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("ip_address")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");
                    foreach (var nwobj in managementReport.ReportObjects)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name={ObjCatString.NwObj}{chapterNumber}x{nwobj.Id}>{nwobj.Name}</a></td>");
                        report.AppendLine($"<td>{(nwobj.Type.Name != "" ? userConfig.GetText(nwobj.Type.Name) : "")}</td>");
                        report.AppendLine($"<td>{NwObjDisplay.DisplayIp(nwobj.IP, nwobj.IpEnd, nwobj.Type.Name)}</td>");
                        report.AppendLine(nwobj.MemberNamesAsHtml());
                        report.AppendLine($"<td>{nwobj.Uid}</td>");
                        report.AppendLine($"<td>{nwobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                    report.AppendLine("<hr>");
                }

                if (managementReport.ReportServices != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("protocol")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("port")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");
                    objNumber = 1;
                    foreach (var svcobj in managementReport.ReportServices)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name={ObjCatString.Svc}{chapterNumber}x{svcobj.Id}>{svcobj.Name}</a></td>");
                        report.AppendLine($"<td>{(svcobj.Type.Name != "" ? userConfig.GetText(svcobj.Type.Name) : "")}</td>");
                        report.AppendLine($"<td>{((svcobj.Type.Name!=ServiceType.Group && svcobj.Protocol!=null)?svcobj.Protocol.Name:"")}</td>");
                        if (svcobj.DestinationPortEnd != null && svcobj.DestinationPortEnd != svcobj.DestinationPort)
                            report.AppendLine($"<td>{svcobj.DestinationPort}-{svcobj.DestinationPortEnd}</td>");
                        else
                            report.AppendLine($"<td>{svcobj.DestinationPort}</td>");
                        report.AppendLine(svcobj.MemberNamesAsHtml());
                        report.AppendLine($"<td>{svcobj.Uid}</td>");
                        report.AppendLine($"<td>{svcobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                    report.AppendLine("<hr>");
                }

                if (managementReport.ReportUsers != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("users")}</h4>");
                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine($"<th>{userConfig.GetText("number")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("name")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("type")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("members")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("uid")}</th>");
                    report.AppendLine($"<th>{userConfig.GetText("comment")}</th>");
                    report.AppendLine("</tr>");
                    objNumber = 1;
                    foreach (var userobj in managementReport.ReportUsers)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name={ObjCatString.User}{chapterNumber}x{userobj.Id}>{userobj.Name}</a></td>");
                        report.AppendLine($"<td>{(userobj.Type.Name != "" ? userConfig.GetText(userobj.Type.Name) : "")}</td>");
                        report.AppendLine(userobj.MemberNamesAsHtml());
                        report.AppendLine($"<td>{userobj.Uid}</td>");
                        report.AppendLine($"<td>{userobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                    report.AppendLine("<hr>");
                }

                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }
    }
}
