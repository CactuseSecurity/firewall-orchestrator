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

        public string DisplayTranslatedSource(Rule rule, OutputLocation location, int chapterNumber = 0, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, chapterNumber, location, style, true);
        }

        public string DisplayTranslatedDestination(Rule rule, OutputLocation location, int chapterNumber = 0, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, chapterNumber, location, style, false);
        }

        public string DisplayTranslatedService(Rule rule, OutputLocation location, int chapterNumber = 0, string style = "")
        {
            StringBuilder result = new();
            if (rule.NatData.TranslatedServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }
            result.AppendJoin("<br>", Array.ConvertAll(rule.NatData.TranslatedServices, service => ServiceToHtml(service.Content, rule.MgmtId, chapterNumber, location, style, ReportType.NatRules)));
            return result.ToString();
        }

        private string DisplayTranslatedSourceOrDestination(Rule rule, int chapterNumber, OutputLocation location, string style, bool isSource)
        {
            StringBuilder result = new();
            if ((isSource && rule.NatData.TranslatedSourceNegated) ||(!isSource && rule.NatData.TranslatedDestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }
            result.AppendJoin("<br>", Array.ConvertAll(isSource ? rule.NatData.TranslatedFroms : rule.NatData.TranslatedTos, networkLocation => NetworkLocationToHtml(networkLocation, rule.MgmtId, chapterNumber, location, style, ReportType.NatRules)));
            return result.ToString();
        }
    }
}
