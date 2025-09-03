using FWO.Basics;
using FWO.Basics.Interfaces;
using FWO.Data;
using FWO.Logging;
using FWO.Ui.Display;
using System.Text.Json;

namespace FWO.Report.Data.ViewData
{
    public class RuleViewData : IRuleViewData
    {
        public string MgmtId { get; set; } = "";
        public string MgmtName { get; set; } = "";
        public string Uid { get; set; } = "";
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Services { get; set; } = "";
        public string Action { get; set; } = "";
        public string InstallOn { get; set; } = "";
        public string Compliance { get; set; } = "";
        public string ViolationDetails { get; set; } = "";
        public string ChangeID { get; set; } = "";
        public string AdoITID { get; set; } = "";
        public string Comment { get; set; } = "";
        public string RulebaseId { get; set; } = "";
        public string RulebaseName { get; set; } = "";

        public Rule? DataObject { get; set; }
        public bool Show { get; set; } = true;

        public RuleViewData(Rule rule, NatRuleDisplayHtml natRuleDisplayHtml, OutputLocation outputLocation, bool show, List<Device>? devices = null, List<Management>? managements = null)
        {
            DataObject = rule;
            Show = show;

            MgmtId              = SafeCall(rule, "MgmtId", () => rule.MgmtId.ToString());
            MgmtName            = SafeCall(rule, "MgmtName",() => managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Name ?? "");
            Uid                 = SafeCall(rule, "Uid",() => rule.Uid ?? "");
            Name                = SafeCall(rule, "Name",() => rule.Name ?? "");
            Source              = SafeCall(rule, "Source",() => natRuleDisplayHtml.DisplaySource(rule, outputLocation, ReportType.Compliance));
            Destination         = SafeCall(rule, "Destination",() => natRuleDisplayHtml.DisplayDestination(rule, outputLocation, ReportType.Compliance));
            Services            = SafeCall(rule, "Services",() => natRuleDisplayHtml.DisplayServices(rule, outputLocation, ReportType.Compliance));
            Action              = SafeCall(rule, "Action",() => rule.Action);
            InstallOn           = SafeCall(rule, "InstallOn",() => ResolveInstallOn(rule, devices ?? []));
            Compliance          = SafeCall(rule, "Compliance",() => ResolveCompliance(rule.Compliance));
            ViolationDetails    = SafeCall(rule, "ViolationDetails",() => rule.ViolationDetails);
            ChangeID            = SafeCall(rule, "ChangeID",() => GetFromCustomField(rule, "field-2"));
            AdoITID             = SafeCall(rule, "AdoITID",() => GetFromCustomField(rule, "field-3"));
            Comment             = SafeCall(rule, "Comment",() => rule.Comment ?? "");
            RulebaseId          = SafeCall(rule, "RulebaseId",() => rule.RulebaseId.ToString());
            RulebaseName        = SafeCall(rule, "RulebaseName",() => rule.Rulebase?.Name ?? "");
        }

        private string ResolveServices(Rule rule)
        {
            var services = rule.Services.Select(s => s.Content.Name).ToList();
            return string.Join(" | ", services);
        }

        private string ResolveCompliance(ComplianceViolationType complianceViolationType)
        {
            return complianceViolationType switch
            {
                ComplianceViolationType.NotEvaluable => "NOT EVALUABLE",
                ComplianceViolationType.None => "TRUE",
                _ => "FALSE"
            };
        }

        private string GetFromCustomField(Rule rule, string field)
        {
            try
            {
                string customFieldsString = rule.CustomFields.Replace("'", "\"");
                Dictionary<string, string>? customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsString);
                return customFields != null && customFields.TryGetValue(field, out string? value) ? value : "";
            }
            catch (JsonException)
            {
                // If custom fields are not valid JSON, just return empty string.

                return "";
            }
        }

        private string ResolveInstallOn(Rule rule, List<Device> devices)
        {
            string installOn = "";

            if (!string.IsNullOrWhiteSpace(rule.InstallOn))
            {
                if (rule.InstallOn.Contains("|"))
                {
                    List<string> uids = rule.InstallOn.Split("|").Select(s => s.Trim()).ToList();

                    foreach (string uid in uids)
                    {
                        if (installOn.Length > 0)
                        {
                            installOn += " | ";
                        }

                        string deviceName = devices.FirstOrDefault(device => device.Uid == uid)?.Name ?? uid;
                        installOn += deviceName;
                    }
                }
                else
                {
                    installOn = devices.FirstOrDefault(device => device.Uid == rule.InstallOn)?.Name ?? rule.InstallOn;
                }
            }

            return installOn;
        }
        
        private string SafeCall(Rule rule, string column, Func<string> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Log.WriteError($"Creating rule view data - Displayerror in rule {rule.Id} column {column}: {ex.Message}");
                return "Displayerror";
            }
        }

    }
}