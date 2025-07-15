using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Report;
using FWO.Encryption;
using FWO.Mail;
using FWO.Services;
using NetTools;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using FWO.Data.Report;
using FWO.Report.Filter.FilterTypes;

namespace FWO.Compliance
{
    public class ComplianceCheck(UserConfig userConfig, ApiConnection? apiConnection = null)
    {
        ComplianceNetworkZone[] NetworkZones = [];
        ReportCompliance? ComplianceReport = null;
        public List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> Results { get; set; } = [];
        private ReportBase? currentReport;
        Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;
        ReportFilters reportFilters = new();

        /// <summary>
        /// Full compliance check to be called by scheduler
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
            await SetUpReportFilters();
            ReportTemplate template = new ReportTemplate("", reportFilters.ToReportParams());
            currentReport = await ReportGenerator.Generate(template, apiConnection, userConfig, DisplayMessageInUi);

            Results.Clear();

            if (apiConnection != null)
            {
                foreach (var management in currentReport.ReportData.ManagementData)
                {
                    foreach (var rulebase in management.Rulebases)
                    {
                        foreach (var rule in rulebase.Rules)
                        {

                            rule.IsCompliant = CheckRuleCompliance(rule, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> result);
                            if (!rule.IsCompliant)
                            {
                                foreach (var item in result)
                                {
                                    Results.Add((rule, item));

                                }

                            }

                        }
                    }
                }

                if (Results.Any())  
                {
                    if (currentReport is ReportCompliance complianceReport)
                    {
                        complianceReport.Violations.Clear();

                        foreach (var item in Results)
                        {
                            ComplianceViolation violation = new();
                            violation.RuleId = (int)item.Item1.Id;
                            violation.Details = $"Matrix violation: {item.Item2.Item1.Name} -> {item.Item2.Item2.Name}";
                            complianceReport.Violations.Add(violation);
                        }

                        await complianceReport.SetComplianceData();
                        await SendComplianceCheckEmail();
                    }
                    
                }
            }
        }

        public bool CheckRuleCompliance(Rule rule, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            List<IPAddressRange> froms = [];
            List<IPAddressRange> tos = [];

            foreach (NetworkLocation networkLocation in rule.Froms)
            {
                // Determine all source ip ranges
                froms.AddRange(ParseIpRange(networkLocation.Object));
            }
            foreach (NetworkLocation networkLocation in rule.Tos)
            {
                // Determine all destination ip ranges
                tos.AddRange(ParseIpRange(networkLocation.Object));
            }

            return CheckCompliance(froms, tos, out forbiddenCommunication);
        }


        private static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            try
            {
                if (networkObject.Type == new NetworkObjectType() { Name = ObjectType.IPRange })
                {
                    ranges.Add(IPAddressRange.Parse($"{networkObject.IP}-{networkObject.IpEnd}"));
                }
                else if (networkObject.Type != new NetworkObjectType() { Name = ObjectType.Group } && networkObject.ObjectGroupFlats.Length > 0)
                {
                    for (int j = 0; j < networkObject.ObjectGroupFlats.Length; j++)
                    {
                        if (networkObject.ObjectGroupFlats[j].Object != null)
                        {
                            ranges.AddRange(ParseIpRange(networkObject.ObjectGroupFlats[j].Object!));
                        }
                    }
                }
                else
                {
                    if (networkObject.IP != null)
                    {
                        // CIDR notation or single (host) IP can be parsed directly
                        ranges.Add(IPAddressRange.Parse(networkObject.IP));                        
                    }
                }                
            }
            catch (System.Exception e)
            {
                
                throw;
            }



            return ranges;
        }

        private async Task SetUpReportFilters()
        {
            reportFilters = new();
            reportFilters.ReportType = ReportType.Compliance;
            reportFilters.DeviceFilter.Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
            foreach (var management in reportFilters.DeviceFilter.Managements)
            {
                management.Selected = true;
                foreach (var device in management.Devices)
                {
                    device.Selected = true;
                }
            }
        }








        /// <summary>
        /// Create compliance report for given Managements
        /// </summary>
        /// <param name="mgmIds"></param>
        /// <returns></returns>
        public async Task<ReportCompliance> CreateComplianceReport(List<int> mgmIds)
        {
            await SetUpReportFilters();
            
            ReportTemplate template = new ReportTemplate("", reportFilters.ToReportParams());
            currentReport = await ReportGenerator.Generate(template, apiConnection, userConfig, DisplayMessageInUi);
            ComplianceReport = currentReport as ReportCompliance;

            return ComplianceReport;
        }

        /// <summary>
        /// Send Email with compliance report to all recipients defined in compliance settings
        /// </summary>
        /// <returns></returns>
        public async Task SendComplianceCheckEmail()
        {
            string decryptedSecret = AesEnc.TryDecrypt(userConfig.EmailPassword, false, "Compliance Check", "Could not decrypt mailserver password.");
            EmailConnection emailConnection = new(userConfig.EmailServerAddress, userConfig.EmailPort,
                userConfig.EmailTls, userConfig.EmailUser, decryptedSecret, userConfig.EmailSenderAddress);

            MailData? mail = PrepareEmail();

            bool success = await MailKitMailer.SendAsync(mail, emailConnection, false, new CancellationToken());
        }

        private MailData PrepareEmail()
        {
            string subject = userConfig.ComplianceCheckMailSubject;
            string body = userConfig.ComplianceCheckMailBody;
               MailData mailData = new(EmailHelper.CollectRecipientsFromConfig(userConfig, userConfig.ComplianceCheckMailRecipients), subject){ Body = body };
            if (currentReport is ReportCompliance complianceReport)
            {
                FormFile? attachment = EmailHelper.CreateAttachment(complianceReport.ExportToCsv(), GlobalConst.kCsv, subject);
                if (attachment != null)
                {
                    mailData.Attachments = new FormFileCollection() { attachment };
                }
            }
            return mailData;
        }

        /// <summary>
        /// Compliance check used in current UI implementation
        /// </summary>
        /// <param name="sourceIpRange"></param>
        /// <param name="destinationIpRange"></param>
        /// <param name="networkZones"></param>
        /// <returns></returns>
        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, ComplianceNetworkZone[] networkZones)
        {
            NetworkZones = networkZones;
            List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunicationsOutput = [];
            if (sourceIpRange != null && destinationIpRange != null)
            {
                CheckCompliance
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }
            return forbiddenCommunicationsOutput;
        }

        private bool CheckCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
        {
            // Determine all matching source zones
            List<ComplianceNetworkZone> sourceZones = DetermineZones(source);

            // Determine all macthing destination zones
            List<ComplianceNetworkZone> destinationZones = DetermineZones(destination);

            forbiddenCommunication = [];

            foreach (ComplianceNetworkZone sourceZone in sourceZones)
            {
                foreach (ComplianceNetworkZone destinationZone in destinationZones)
                {
                    if (!sourceZone.CommunicationAllowedTo(destinationZone))
                    {
                        forbiddenCommunication.Add((sourceZone, destinationZone));
                    }
                }
            }

            return forbiddenCommunication.Count == 0;
        }


        private List<ComplianceNetworkZone> DetermineZones(List<IPAddressRange> ranges)
        {
            List<ComplianceNetworkZone> result = [];
            List<List<IPAddressRange>> unseenIpAddressRanges = [];

            for (int i = 0; i < ranges.Count; i++)
            {
                unseenIpAddressRanges.Add(
                [
                    new(ranges[i].Begin, ranges[i].End)
                ]);
            }

            foreach (ComplianceNetworkZone zone in NetworkZones.Where(z => z.OverlapExists(ranges, unseenIpAddressRanges)))
            {
                result.Add(zone);
            }

            // Get ip ranges that are not in any zone
            List<IPAddressRange> undefinedIpRanges = [.. unseenIpAddressRanges.SelectMany(x => x)];
            if (undefinedIpRanges.Count > 0)
            {
                result.Add
                (
                    new ComplianceNetworkZone()
                    {
                        Name = userConfig.GetText("internet_local_zone"),
                    }
                );
            }

            return result;
        }

    }

}
