using FWO.Api.Data;
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
            StringBuilder report = new StringBuilder();
            NatRuleDisplayHtml ruleDisplay = new NatRuleDisplayHtml(userConfig);

            foreach (Management management in Managements.Where(mgt => !mgt.Ignore))
            {
                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (Device device in management.Devices)
                {
                    if (device.Rules != null && device.Rules.Length > 0)
                    {
                        report.AppendLine($"<h4>{device.Name}</h4>");
                        report.AppendLine("<hr>");

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

                        foreach (Rule rule in device.Rules)
                        {
                            if (string.IsNullOrEmpty(rule.SectionHeader))
                            {
                                report.AppendLine("<tr>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayNumber(rule, device.Rules)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayName(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplaySourceZone(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplaySource(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestinationZone(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayDestination(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayService(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedSource(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedDestination(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayTranslatedService(rule, location: "")}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayEnabled(rule, export: true)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayUid(rule)}</td>");
                                report.AppendLine($"<td>{ruleDisplay.DisplayComment(rule)}</td>");
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
                    }
                }

                // show all objects used in this management's rules

                int objNumber = 1;
                if (management.ReportObjects != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("network_objects")}</h4>");
                    report.AppendLine("<hr>");
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
                    foreach (NetworkObject nwobj in management.ReportObjects)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td><a name=nwobj{nwobj.Id}>{nwobj.Name}</a></td>");
                        report.AppendLine($"<td>{nwobj.Type.Name}</td>");
                        report.AppendLine($"<td>{nwobj.IP}{(nwobj.IpEnd != null && nwobj.IpEnd != "" && nwobj.IpEnd != nwobj.IP ? $"-{nwobj.IpEnd}" : "")}</td>");
                        if (nwobj.MemberNames != null && nwobj.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", nwobj.MemberNames.Split('|'))}</td>");
                        else
                            report.AppendLine($"<td>{nwobj.MemberNames}</td>");
                        report.AppendLine($"<td>{nwobj.Uid}</td>");
                        report.AppendLine($"<td>{nwobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportServices != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("network_services")}</h4>");
                    report.AppendLine("<hr>");
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
                    foreach (NetworkService svcobj in management.ReportServices)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{svcobj.Name}</td>");
                        report.AppendLine($"<td><a name=svc{svcobj.Id}>{svcobj.Name}</a></td>");
                        report.AppendLine($"<td>{((svcobj.Protocol!=null)?svcobj.Protocol.Name:"")}</td>");
                        if (svcobj.DestinationPortEnd != null && svcobj.DestinationPortEnd != svcobj.DestinationPort)
                            report.AppendLine($"<td>{svcobj.DestinationPort}-{svcobj.DestinationPortEnd}</td>");
                        else
                            report.AppendLine($"<td>{svcobj.DestinationPort}</td>");
                        if (svcobj.MemberNames != null && svcobj.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", svcobj.MemberNames.Split('|'))}</td>");
                        else 
                            report.AppendLine($"<td>{svcobj.MemberNames}</td>");
                        report.AppendLine($"<td>{svcobj.Uid}</td>");
                        report.AppendLine($"<td>{svcobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                if (management.ReportUsers != null)
                {
                    report.AppendLine($"<h4>{userConfig.GetText("users")}</h4>");
                    report.AppendLine("<hr>");
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
                    foreach (NetworkUser userobj in management.ReportUsers)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{objNumber++}</td>");
                        report.AppendLine($"<td>{userobj.Name}</td>");
                        report.AppendLine($"<td><a name=user{userobj.Id}>{userobj.Name}</a></td>");
                        if (userobj.MemberNames != null && userobj.MemberNames.Contains("|"))
                            report.AppendLine($"<td>{string.Join("<br>", userobj.MemberNames.Split('|'))}</td>");
                        else
                            report.AppendLine($"<td>{userobj.MemberNames}</td>");
                        report.AppendLine($"<td>{userobj.Uid}</td>");
                        report.AppendLine($"<td>{userobj.Comment}</td>");
                        report.AppendLine("</tr>");
                    }
                    report.AppendLine("</table>");
                }

                report.AppendLine("</table>");
            }

            return GenerateHtmlFrame(userConfig.GetText(ReportType.ToString()), Query.RawFilter, DateTime.Now, report);
        }
    }
}
