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
using FWO.Data.Report;
using FWO.Report.Filter.FilterTypes;

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        ComplianceNetworkZone[] NetworkZones = [];
        ReportCompliance? ComplianceReport = null;
        public List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> Results { get; set; } = [];
        public List<ComplianceViolation> RestrictedServiceViolations { get; set; } = [];
        private ReportBase? currentReport;
        Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;
        ReportFilters reportFilters = new();
        private readonly UserConfig _userConfig;
        private readonly ApiConnection _apiConnection;
        private readonly List<string> _restrictedServices = ["97aeb369-9aea-11d5-bd16-0090272ccb30"];
        private bool _checkRestrictedServices = true;


        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection)
        {
            _userConfig = userConfig;
            _apiConnection = apiConnection;
        }

        /// <summary>
        /// Full compliance check to be called by scheduler
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
            NetworkZones = await _apiConnection.SendQueryAsync<ComplianceNetworkZone[]>(ComplianceQueries.getNetworkZones);
            await SetUpReportFilters();
            ReportTemplate template = new ReportTemplate("", reportFilters.ToReportParams());
            currentReport = await ReportGenerator.Generate(template, _apiConnection, _userConfig, DisplayMessageInUi);

            Results.Clear();
            RestrictedServiceViolations.Clear();

            if (_apiConnection != null && currentReport is ReportCompliance complianceReport)
            {
                foreach (var management in complianceReport.ReportData.ManagementData)
                {
                    foreach (var rulebase in management.Rulebases)
                    {
                        foreach (var rule in rulebase.Rules)
                        {
                            rule.IsCompliant = CheckRuleCompliance(rule);
                        }
                    }
                }

                await GatherCheckResults();

                if (_userConfig.GlobalConfig is GlobalConfig globalConfig && globalConfig.ComplianceCheckPersistData)
                {
                    List<ComplianceViolationBase> violationsForInsert = complianceReport.Violations
                    .Select(v => new ComplianceViolationBase
                    {
                        RuleId = v.RuleId,
                        Details = v.Details,
                        FoundDate = v.FoundDate,
                        RemovedDate = v.RemovedDate,
                        RiskScore = v.RiskScore,
                        PolicyId = v.PolicyId,
                        CriterionId = v.CriterionId
                    })
                    .ToList();
                    var variables = new
                    {
                        violations = violationsForInsert
                    };
                    await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variables );
                }

            }
        }

        private async Task GatherCheckResults()
        {
            if (Results.Any() && currentReport is ReportCompliance complianceReport)
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
                ComplianceReport = complianceReport;
            }
        }

        public async Task<bool> CheckRuleCompliance(Rule rule)
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

            bool ruleIsCompliant = CheckMatrixCompliance(froms, tos, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication);

            foreach (var item in forbiddenCommunication)
            {
                Results.Add((rule, item));
            }

            List<ComplianceViolation> serviceViolations = await TryGetRestrictedServiceViolation(rule);

            if (serviceViolations.Count > 0)
            {
                ruleIsCompliant = false;
                RestrictedServiceViolations.AddRange(serviceViolations);
            }

            return ruleIsCompliant;
        }

        private static List<IPAddressRange> ParseIpRange(NetworkObject networkObject)
        {
            List<IPAddressRange> ranges = [];

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

            return ranges;
        }

        private async Task SetUpReportFilters()
        {
            reportFilters = new();
            reportFilters.ReportType = ReportType.Compliance;
            reportFilters.DeviceFilter.Managements = await _apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
            foreach (var management in reportFilters.DeviceFilter.Managements)
            {
                management.Selected = true;
                foreach (var device in management.Devices)
                {
                    device.Selected = true;
                }
            }
        }

        public async Task<List<ComplianceViolation>> TryGetRestrictedServiceViolation(Rule rule)
        {
            List<ComplianceViolation> violations = [];

            if (_checkRestrictedServices)
            {
                foreach (var service in rule.Services)
                {
                    if (_restrictedServices.Contains(service.Content.Uid))
                    {
                        ComplianceViolation violation = new()
                        {
                            RuleId = (int)rule.Id,
                            Details = $"Restricted service used: {service.Content.Name}"
                        };
                        RestrictedServiceViolations.Add(violation);
                        violations.Add(violation);
                    }
                }
            }
            return violations;
        }

        /// <summary>
        /// Send Email with compliance report to all recipients defined in compliance settings
        /// </summary>
        /// <returns></returns>
        public async Task SendComplianceCheckEmail()
        {
            if (_userConfig.GlobalConfig is GlobalConfig globalConfig)
            {
                string decryptedSecret = AesEnc.TryDecrypt(globalConfig.EmailPassword, false, "Compliance Check", "Could not decrypt mailserver password.");

                EmailConnection emailConnection = new(
                    globalConfig.EmailServerAddress,
                    globalConfig.EmailPort,
                    globalConfig.EmailTls,
                    globalConfig.EmailUser,
                    decryptedSecret,
                    globalConfig.EmailSenderAddress
                );

                MailData? mail = PrepareEmail();

                if (mail != null)
                {
                    await MailKitMailer.SendAsync(mail, emailConnection, false, new CancellationToken());
                }
                
            }

        }

        private MailData? PrepareEmail()
        {
            if (_userConfig.GlobalConfig is GlobalConfig globalConfig)
            {
                string subject = globalConfig.ComplianceCheckMailSubject;
                string body = globalConfig.ComplianceCheckMailBody;
                MailData mailData = new(EmailHelper.CollectRecipientsFromConfig(_userConfig, globalConfig.ComplianceCheckMailRecipients), subject) { Body = body };

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
            else
            {
                return null;
            }
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
                CheckMatrixCompliance
                (
                    [sourceIpRange],
                    [destinationIpRange],
                    out forbiddenCommunicationsOutput
                );
            }
            return forbiddenCommunicationsOutput;
        }

        private bool CheckMatrixCompliance(List<IPAddressRange> source, List<IPAddressRange> destination, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication)
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
                        Name =  _userConfig.GetText("internet_local_zone"),
                    }
                );
            }

            return result;
        }

    }

}
