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
using FWO.Data.Middleware;
using System.Text.Json;
using FWO.Logging;

namespace FWO.Compliance
{
    public class ComplianceCheck
    {
        List<ComplianceNetworkZone> NetworkZones = [];
         public List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> Results { get; set; } = [];
        public List<ComplianceViolation> RestrictedServiceViolations { get; set; } = [];
        private ReportBase? currentReport;
        Action<Exception?, string, string, bool> DisplayMessageInUi { get; set; } = DefaultInit.DoNothing;
        ReportFilters reportFilters = new();

        private readonly UserConfig _userConfig;
        private readonly ApiConnection _apiConnection;
        CompliancePolicy? Policy = null;
        private readonly DebugConfig _debugConfig;


        /// <summary>
        /// Constructor for compliance check
        /// </summary>
        /// <param name="userConfig">User configuration</param>
        /// <param name="apiConnection">Api connection</param>
        public ComplianceCheck(UserConfig userConfig, ApiConnection apiConnection)
        {
            _userConfig = userConfig;
            _apiConnection = apiConnection;

            if (userConfig.GlobalConfig is GlobalConfig globalConfig && !string.IsNullOrEmpty(globalConfig.DebugConfig))
            {
                _debugConfig = JsonSerializer.Deserialize<DebugConfig>(globalConfig.DebugConfig) ?? new();
            }
            else
            {
                _debugConfig = new();
            }
        }

        /// <summary>
        /// Full compliance check to be called by scheduler
        /// </summary>
        /// <returns></returns>
        public async Task CheckAll()
        {
            if (_debugConfig.ExtendedLogComplianceCheck)
            {
                Log.WriteInfo("Compliance Check", "Starting compliance check");
            }
            Policy = await _apiConnection.SendQueryAsync<CompliancePolicy>(ComplianceQueries.getPolicyById, new { id = _userConfig.ComplianceCheckPolicyId });
            await LoadNetworkZones();
            await SetUpReportFilters();
            ReportTemplate template = new("", reportFilters.ToReportParams());
            currentReport = await ReportGenerator.Generate(template, _apiConnection, _userConfig, DisplayMessageInUi);

            Results.Clear();
            RestrictedServiceViolations.Clear();

            if (_userConfig.GlobalConfig is GlobalConfig globalConfig && _apiConnection != null && currentReport is ReportCompliance complianceReport)
            {
                if (_debugConfig.ExtendedLogComplianceCheck)
                {
                    Log.WriteInfo("Compliance Check", "Using restricted services: " + Policy.Criteria.FirstOrDefault(x => x.Content.CriterionType == CriterionType.ForbiddenService.ToString())?.Content.Content);
                }

                foreach (var management in complianceReport.ReportData.ManagementData)
                {
                    await CheckRuleCompliancePerManagement(management);
                }

                await GatherCheckResults();

                if (globalConfig.ComplianceCheckPersistData)
                {
                    await PersistData(complianceReport);
                }
            }
        }

        private async Task LoadNetworkZones()
        {
            if (Policy != null)
            {
                // ToDo later: work with several matrices?
                int? matrixId = Policy.Criteria.FirstOrDefault(c => c.Content.CriterionType == CriterionType.Matrix.ToString())?.Content.Id;
                if (matrixId != null)
                {
                    NetworkZones = await _apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
                }
            }
        }

        private async Task PersistData(ReportCompliance complianceReport)
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
            await _apiConnection.SendQueryAsync<dynamic>(ComplianceQueries.addViolations, variables);
        }

        private async Task CheckRuleCompliancePerManagement(ManagementReport management)
        {
            foreach (var rulebase in management.Rulebases)
            {
                foreach (var rule in rulebase.Rules)
                {
                    rule.IsCompliant = await CheckRuleCompliance(rule);
                }
            }
        }

        private async Task GatherCheckResults()
        {
            if (Results.Count > 0 && currentReport is ReportCompliance complianceReport)
            {
                complianceReport.Violations.Clear();

                foreach (var item in Results)
                {
                    ComplianceViolation violation = new()
                    {
                        RuleId = (int)item.Item1.Id,
                        Details = $"Matrix violation: {item.Item2.Item1.Name} -> {item.Item2.Item2.Name}"
                    };
                    complianceReport.Violations.Add(violation);
                }

                complianceReport.Violations.AddRange(RestrictedServiceViolations);

                await complianceReport.SetComplianceData();
            }
        }

        public async Task<bool> CheckRuleCompliance(Rule rule)
        {
            bool ruleIsCompliant = true;
            foreach (var criterion in (Policy?.Criteria ?? []).Select(c => c.Content))
            {
                switch (criterion.CriterionType)
                {
                    case nameof(CriterionType.Matrix):
                        ruleIsCompliant &= await CheckAgainstMatrix(rule);
                        break;
                    case nameof(CriterionType.ForbiddenService):
                        ruleIsCompliant &= CheckForForbiddenService(rule, criterion);
                        break;
                    default:
                        break;
                }
            }
            return ruleIsCompliant;
        }

        private async Task<bool> CheckAgainstMatrix(Rule rule)
        {
            Task<List<IPAddressRange>> fromsTask = GetIpRangesFromNetworkObjects([.. rule.Froms.Select(nl => nl.Object)]);
            Task<List<IPAddressRange>> tosTask = GetIpRangesFromNetworkObjects([.. rule.Tos.Select(nl => nl.Object)]);

            await Task.WhenAll(fromsTask, tosTask);

            List<IPAddressRange> froms = fromsTask.Result;
            List<IPAddressRange> tos = tosTask.Result;

            bool ruleIsCompliant = CheckMatrixCompliance(froms, tos, out List<(ComplianceNetworkZone, ComplianceNetworkZone)> forbiddenCommunication);

            foreach ((ComplianceNetworkZone, ComplianceNetworkZone) item in forbiddenCommunication)
            {
                Results.Add((rule, item));
            }
            return ruleIsCompliant;
        }

        private bool CheckForForbiddenService(Rule rule, ComplianceCriterion criterion)
        {
            List<ComplianceViolation> serviceViolations = TryGetRestrictedServiceViolation(rule, criterion);

            if (serviceViolations.Count > 0)
            {
                RestrictedServiceViolations.AddRange(serviceViolations);
            }
            return serviceViolations.Count == 0;
        }

        private static Task<List<IPAddressRange>> GetIpRangesFromNetworkObjects(List<NetworkObject> networkObjects)
        {
            List<IPAddressRange> ranges = [];
            foreach (NetworkObject networkObject in networkObjects)
            {
                ranges.AddRange(ParseIpRange(networkObject));
            }
            return Task.FromResult(ranges);
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
            reportFilters = new()
            {
                ReportType = ReportType.Compliance
            };
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

        public static List<ComplianceViolation> TryGetRestrictedServiceViolation(Rule rule, ComplianceCriterion criterion)
        {
            List<ComplianceViolation> violations = [];
            List<string> restrictedServices = [.. criterion.Content.Split(',').Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))];

            if (restrictedServices.Count > 0)
            {
                foreach (var service in rule.Services.Where(s => restrictedServices.Contains(s.Content.Uid)))
                {
                    ComplianceViolation violation = new()
                    {
                        RuleId = (int)rule.Id,
                        Details = $"Restricted service used: {service.Content.Name}"
                    };
                    violations.Add(violation);
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
        public List<(ComplianceNetworkZone, ComplianceNetworkZone)> CheckIpRangeInputCompliance(IPAddressRange? sourceIpRange, IPAddressRange? destinationIpRange, List<ComplianceNetworkZone> networkZones)
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
                foreach (ComplianceNetworkZone destinationZone in destinationZones.Where(d => !sourceZone.CommunicationAllowedTo(d)))
                {
                    forbiddenCommunication.Add((sourceZone, destinationZone));
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
