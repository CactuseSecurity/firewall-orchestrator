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

namespace FWO.Compliance
{
    public class ComplianceCheck(UserConfig userConfig, ApiConnection? apiConnection = null)
    {
        ComplianceNetworkZone[] NetworkZones = [];
        ReportCompliance? ComplianceReport = null;

        /// <summary>
        /// Full compliance check to be called by scheduler
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
            if (apiConnection != null)
            {
                // something like this
                List<int> managementIds = [.. (await apiConnection.SendQueryAsync<List<Management>>(DeviceQueries.getManagementNames)).Select(m => m.Id)];
                await CreateComplianceReport(managementIds);

                // write result to database
            }
        }

        /// <summary>
        /// Create compliance report for given Managements
        /// </summary>
        /// <param name="mgmIds"></param>
        /// <returns></returns>
        public async Task<ReportCompliance> CreateComplianceReport(List<int> mgmIds)
        {
            ComplianceReport = new();

            // ToDo: create real report with different parameters
            if (apiConnection != null)
            {
                foreach (var mgtId in mgmIds)
                {
                    Management mgt = new(); // await apiConnection.SendQueryAsync<Management>(DeviceQueries.getSingleManagementDetails);
                    foreach (var rulebase in mgt.Rulebases)
                    {
                        foreach (var rule in rulebase.Rules)
                        {
                            CheckRuleCompliance(rule, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> result);
                            ComplianceReport.Results.AddRange(result);
                        }
                    }
                }
            }
            // Filter out duplicates ?
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

            await MailKitMailer.SendAsync(mail, emailConnection, false, new CancellationToken());
        }

        private MailData PrepareEmail()
        {
            string subject = userConfig.ComplianceCheckMailSubject;
            string body = userConfig.ComplianceCheckMailBody;
            MailData mailData = new(EmailHelper.CollectRecipientsFromConfig(userConfig, userConfig.ComplianceCheckMailRecipients), subject) { Body = body };
            if (ComplianceReport != null)
            {
                FormFile? attachment = EmailHelper.CreateAttachment(ComplianceReport?.ExportToCsv(), GlobalConst.kCsv, subject);
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

        private static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

            if (networkObject.Type == new NetworkObjectType() { Name = ObjectType.IPRange })
            {
                ranges.Add(IPAddressRange.Parse($"{networkObject.IP}-{networkObject.IpEnd}"));
            }
            else if (networkObject.Type != new NetworkObjectType() { Name = ObjectType.Group })
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
                // CIDR notation or single (host) IP can be parsed directly
                ranges.Add(IPAddressRange.Parse(networkObject.IP));
            }

            return ranges;
        }


    }
    
    public class ReportCompliance //: ReportRules
    {
		// Todo: move deeper into ReportData
		public List<(ComplianceNetworkZone, ComplianceNetworkZone)> Results { get; set; } = [];

        //public ReportCompliance(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }

		//public override string ExportToCsv()
		public string ExportToCsv()
		{
			return "";
		}
    }
}
