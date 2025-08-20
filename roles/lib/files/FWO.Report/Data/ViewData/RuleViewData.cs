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

        public Rule? DataObject { get; set; }

        public RuleViewData(Rule rule, NatRuleDisplayHtml natRuleDisplayHtml, OutputLocation outputLocation)
        {
            DataObject = rule;

            MgmtId = rule.MgmtId.ToString();
            Uid = rule.Uid ?? "";
            Name = rule.Name ?? "";
            Source = natRuleDisplayHtml.DisplaySource(rule, outputLocation, ReportType.ComplianceNew);
            Destination = natRuleDisplayHtml.DisplayDestination(rule, outputLocation, ReportType.ComplianceNew);
            Services = natRuleDisplayHtml.DisplayServices(rule, outputLocation, ReportType.ComplianceNew);
            Action = rule.Action;
            InstallOn = rule.InstallOn; // TODO: resolve InstallOn properly
            Compliance = ResolveCompliance(rule.Compliance);
            ViolationDetails = rule.ViolationDetails;
            ChangeID = GetFromCustomField(rule, "field-2");
            AdoITID = GetFromCustomField(rule, "field-3");
            Comment = rule.Comment ?? "";
            RulebaseId = rule.RulebaseId.ToString();
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
            string customFieldsString = rule.CustomFields.Replace("'", "\"");
            Dictionary<string, string>? customFields = JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsString);
            return customFields != null && customFields.TryGetValue(field, out string? value) ? value : "";
        }
    }
}