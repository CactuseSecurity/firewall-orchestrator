using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Compliance;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FWO.Report;
using FWO.Data.Workflow;
using FWO.Services.Workflow;

namespace FWO.Middleware.Server.Controllers
{
    /// <summary>
    /// Controller class for compliance api
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ComplianceController(ApiConnection apiConnection) : ControllerBase
    {
        /// <summary>
        /// Import Compliance Matrix
        /// </summary>
        /// <param name="parameters">ComplianceImportMatrixParameters</param>
        /// <returns>Failed import filenames</returns>
        [HttpPost("ImportMatrix")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<string> Post([FromBody] ComplianceImportMatrixParameters parameters)
        {
            try
            {
                GlobalConfig GlobalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                ZoneMatrixDataImport matrixDataImport = new(apiConnection, GlobalConfig);
                return await matrixDataImport.Run(parameters.FileName, parameters.Data, parameters.UserName, parameters.UserDn);
            }
            catch (Exception exception)
            {
                Log.WriteError("Import Compliance Matrix", "Error while importing matrix.", exception);
                return exception.Message;
            }
        }

        /// <summary>
        /// Get Compliance Report
        /// </summary>
        /// <param name="parameters">ComplianceReportParameters</param>
        /// <returns>Report as json string</returns>
        [HttpPost("Report")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.Auditor}, {Roles.Reporter}, {Roles.ReporterViewAll}, {Roles.FwAdmin}, {Roles.Recertifier}")]
        public async Task<string> Get([FromBody] ComplianceReportParameters parameters)
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });

                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                await complianceCheck.RunComplianceCheck(ComplianceCheckType.Standard);

                ReportCompliance reportCompliance = new(new(""), userConfig, ReportType.ComplianceReport);
                await reportCompliance.GetManagementAndDevices(apiConnection);
                List<Management> relevantManagements = ComplianceCheck.GetRelevantManagements(globalConfig, reportCompliance.Managements!);
                reportCompliance.Managements = relevantManagements;
                reportCompliance.GetViewDataFromRules(complianceCheck.RulesInCheck!);
                string reportString = reportCompliance.ExportToCsv();
                return reportString;
            }
            catch (Exception exception)
            {
                Log.WriteError("Get Compliance Report", "Error while getting report.", exception);
            }
            return "";
        }

        /// <summary>
        /// Compliance Check
        /// </summary>
        /// <returns></returns>
        [HttpGet("ComplianceCheck")]
        [Authorize(Roles = $"{Roles.Admin}")]
        public async Task<bool> InitialComplianceCheck()
        {
            try
            {
                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                await complianceCheck.RunComplianceCheck(ComplianceCheckType.Variable);
                await complianceCheck.PersistDataAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks the provided rules against the selected compliance policies.
        /// </summary>
        /// <param name="parameters">Selected policy ids and request task ids to evaluate.</param>
        /// <returns>True if all selected policies pass for all provided rules, otherwise false.</returns>
        [HttpPost("CheckRequestedRulesAgainstPolicies")]
        [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Auditor}")]
        public async Task<bool> CheckRequestedRulesAgainstPolicies([FromBody] ComplianceRuleCheckParameters parameters)
        {
            try
            {
                if (parameters.PolicyIds.Count == 0 || parameters.RequestTaskIds.Count == 0)
                {
                    return false;
                }

                GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
                UserConfig userConfig = new(globalConfig, apiConnection, new() { Language = GlobalConst.kEnglish });
                ComplianceCheck complianceCheck = new(userConfig, apiConnection);
                WfHandler wfHandler = new(userConfig, apiConnection, WorkflowPhases.request, null);
                List<WfReqTask> requestTasks = await wfHandler.LoadRequestTasks(parameters.RequestTaskIds);
                List<Rule> rules = await BuildRulesFromRequestTasks(requestTasks);
                return await complianceCheck.AreRulesCompliant(parameters.PolicyIds, rules);
            }
            catch (Exception exception)
            {
                Log.WriteError("Check Requested Rules Against Policies", "Error while evaluating selected policies.", exception);
                return false;
            }
        }

        private static string ConvertOutput(List<(Rule, (ComplianceNetworkZone, ComplianceNetworkZone))> forbiddenCommunicationsOutput)
        {
            return JsonSerializer.Serialize(forbiddenCommunicationsOutput);
        }

        private async Task<List<Rule>> BuildRulesFromRequestTasks(IEnumerable<WfReqTask> requestTasks)
        {
            List<WfReqTask> eligibleTasks = requestTasks
                .Where(task => task.ManagementId != null)
                .Where(task => !string.Equals(task.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase))
                .Where(task => task.GetNwObjectElements(ElemFieldType.source).Count > 0)
                .Where(task => task.GetNwObjectElements(ElemFieldType.destination).Count > 0)
                .Where(task => task.GetServiceElements().Count > 0)
                .ToList();
            List<Rule> rules = [];

            foreach (WfReqTask task in eligibleTasks)
            {
                Rule? rule = BuildRuleFromRequestTask(task);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }

            return await Task.FromResult(rules);
        }

        private static Rule? BuildRuleFromRequestTask(WfReqTask requestTask)
        {
            if (requestTask.ManagementId == null)
            {
                return null;
            }

            List<NetworkLocation> froms = requestTask.GetNwObjectElements(ElemFieldType.source)
                .Where(IsRequestedElementActive)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<NetworkLocation> tos = requestTask.GetNwObjectElements(ElemFieldType.destination)
                .Where(IsRequestedElementActive)
                .Select(BuildNetworkLocation)
                .Where(location => location != null)
                .Cast<NetworkLocation>()
                .ToList();

            List<ServiceWrapper> services = requestTask.GetServiceElements()
                .Where(IsRequestedElementActive)
                .Select(BuildService)
                .Where(service => service != null)
                .Cast<ServiceWrapper>()
                .ToList();

            if (froms.Count == 0 || tos.Count == 0 || services.Count == 0)
            {
                return null;
            }

            return new Rule()
            {
                MgmtId = requestTask.ManagementId.Value,
                Uid = requestTask.GetRuleElements()
                    .Select(rule => rule.RuleUid)
                    .FirstOrDefault(uid => !string.IsNullOrWhiteSpace(uid)) ?? "",
                Name = requestTask.Title,
                Action = GetRuleAction(requestTask),
                Froms = [.. froms],
                Tos = [.. tos],
                Services = [.. services]
            };
        }

        private static NetworkLocation? BuildNetworkLocation(NwObjectElement element)
        {
            if (string.IsNullOrWhiteSpace(element.IpString))
            {
                return null;
            }

            NetworkObject inlineObject = new()
            {
                Name = element.Name ?? "",
                IP = element.IpString,
                IpEnd = element.IpEndString
            };
            inlineObject.Type = new NetworkObjectType()
            {
                Name = !string.IsNullOrWhiteSpace(element.IpEndString) && !string.Equals(element.IpString, element.IpEndString, StringComparison.Ordinal)
                    ? ObjectType.IPRange
                    : ObjectType.Network
            };
            return new NetworkLocation(new(), inlineObject);
        }

        private static ServiceWrapper? BuildService(NwServiceElement element)
        {
            if (element.Port == 0 && element.ProtoId == 0)
            {
                return null;
            }

            return new ServiceWrapper()
            {
                Content = new NetworkService()
                {
                    Name = element.Name ?? "",
                    DestinationPort = element.Port,
                    DestinationPortEnd = element.PortEnd,
                    ProtoId = element.ProtoId
                }
            };
        }

        private static bool IsRequestedElementActive(NwObjectElement element)
        {
            return !string.Equals(element.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRequestedElementActive(NwServiceElement element)
        {
            return !string.Equals(element.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRuleAction(WfReqTask requestTask)
        {
            if (string.Equals(requestTask.TaskType, WfTaskType.rule_delete.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestTask.RequestAction, nameof(RequestAction.delete), StringComparison.OrdinalIgnoreCase))
            {
                return RuleActions.Drop;
            }

            return RuleActions.Accept;
        }
    }
}
