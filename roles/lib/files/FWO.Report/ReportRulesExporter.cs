using FWO.Api.Data;
using FWO.Ui.Display;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharpCore;
using PdfSharpCore.Pdf;
using VetCV.HtmlRendererCore.Core;
using VetCV.HtmlRendererCore.PdfSharpCore;

namespace FWO.Report
{
    public class ReportRulesExporter : ReportExporter
    {
        public Management[] Managements { get; set; }

        public override void ToCsv()
        {
            throw new NotImplementedException();
        }

        public override string ToHtml()
        {
            StringBuilder report = new StringBuilder();

            foreach (Management management in Managements)
            {
                report.AppendLine($"<h3>{management.Name}</h3>");
                report.AppendLine("<hr>");

                foreach (Device device in management.Devices)
                {
                    report.AppendLine($"<h4>{device.Name}</h4>");
                    report.AppendLine("<hr>");

                    report.AppendLine("<table>");
                    report.AppendLine("<tr>");
                    report.AppendLine("<th>Number</th>");
                    report.AppendLine("<th>Name</th>");
                    report.AppendLine("<th>Source</th>");
                    report.AppendLine("<th>Destination</th>");
                    report.AppendLine("<th>Services</th>");
                    report.AppendLine("<th>Action</th>");
                    report.AppendLine("<th>Track</th>");
                    report.AppendLine("<th>Disabled</th>");
                    report.AppendLine("<th>UID</th>");
                    report.AppendLine("<th>Comment</th>");
                    report.AppendLine("</tr>");

                    foreach(Rule rule in device.Rules)
                    {
                        report.AppendLine("<tr>");
                        report.AppendLine($"<td>{rule.DisplayNumber(device.Rules)}</td>");
                        report.AppendLine($"<td>{rule.DisplayName()}</td>");
                        report.AppendLine($"<td>{rule.DisplaySource()}</td>");
                        report.AppendLine($"<td>{rule.DisplayDestination()}</td>");
                        report.AppendLine($"<td>{rule.DisplayService()}</td>");
                        report.AppendLine($"<td>{rule.DisplayAction()}</td>");
                        report.AppendLine($"<td>{rule.DisplayTrack()}</td>");
                        report.AppendLine($"<td>{rule.DisplayDisabled()}</td>");
                        report.AppendLine($"<td>{rule.DisplayUid()}</td>");
                        report.AppendLine($"<td>{rule.DisplayComment()}</td>");
                        report.AppendLine("</tr>");
                    }

                    report.AppendLine("</table>");
                }
            }

            return Template.Replace("##Body##", report.ToString());
        }

        public override string ToPdf()
        {
            // HTML
            string html = ToHtml();

            // CONFIG
            PdfGenerateConfig config = new PdfGenerateConfig();
            config.PageOrientation = PageOrientation.Landscape;
            config.SetMargins(20);
            config.PageSize = PageSize.A4;

            PdfDocument document = PdfGenerator.GeneratePdf(html, config);
            document.Save("wwwroot/downloads/test.pdf");

            return "";
        }
    }
}
