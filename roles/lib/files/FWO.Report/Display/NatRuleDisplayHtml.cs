using FWO.Basics;
using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class NatRuleDisplayHtml : RuleDisplayHtml
    {
        public NatRuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplayTranslatedSource(Rule rule, OutputLocation location, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, location, style, true);
        }

        public string DisplayTranslatedDestination(Rule rule, OutputLocation location, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, location, style, false);
        }

        public string DisplayTranslatedService(Rule rule, OutputLocation location, string style = "")
        {
            StringBuilder result = new StringBuilder();
            if (rule.NatData.TranslatedServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }
            result.AppendJoin("<br>", Array.ConvertAll(rule.NatData.TranslatedServices, service => ServiceToHtml(service.Content, rule.MgmtId, location, style, ReportType.NatRules)));
            return result.ToString();
        }

        private string DisplayTranslatedSourceOrDestination(Rule rule, OutputLocation location, string style, bool isSource)
        {
            StringBuilder result = new StringBuilder();
            if ((isSource && rule.NatData.TranslatedSourceNegated) ||(!isSource && rule.NatData.TranslatedDestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }
            result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.NatData.TranslatedFroms : rule.NatData.TranslatedTos, networkLocation => NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style, ReportType.NatRules)));
            return result.ToString();
        }
    }
}
