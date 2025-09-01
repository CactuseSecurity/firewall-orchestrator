using FWO.Basics;
using FWO.Basics.Interfaces;
using FWO.Data;
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

            MgmtId              = SafeCall(() => rule.MgmtId.ToString());
            MgmtName            = SafeCall(() => managements?.FirstOrDefault(m => m.Id == rule.MgmtId)?.Name ?? "");
            Uid                 = SafeCall(() => rule.Uid ?? "");
            Name                = SafeCall(() => rule.Name ?? "");
            Source              = SafeCall(() => natRuleDisplayHtml.DisplaySource(rule, outputLocation, ReportType.Compliance));
            Destination         = SafeCall(() => natRuleDisplayHtml.DisplayDestination(rule, outputLocation, ReportType.Compliance));
            Services            = SafeCall(() => ResolveServices(rule));
            Action              = SafeCall(() => rule.Action);
            InstallOn           = SafeCall(() => ResolveInstallOn(rule, devices ?? []));
            Compliance          = SafeCall(() => ResolveCompliance(rule.Compliance));
            ViolationDetails    = SafeCall(() => rule.ViolationDetails);
            ChangeID            = SafeCall(() => GetFromCustomField(rule, "field-2"));
            AdoITID             = SafeCall(() => GetFromCustomField(rule, "field-3"));
            Comment             = SafeCall(() => rule.Comment ?? "");
            RulebaseId          = SafeCall(() => rule.RulebaseId.ToString());
            RulebaseName        = SafeCall(() => rule.Rulebase?.Name ?? "");
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
        
        private string SafeCall(Func<string> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

    }
}