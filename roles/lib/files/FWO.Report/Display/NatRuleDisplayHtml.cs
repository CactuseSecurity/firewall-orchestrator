using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

namespace FWO.Ui.Display
{
    public class NatRuleDisplayHtml : RuleDisplayHtml
    {
        public NatRuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplayTranslatedSource(Rule rule, string location, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, style, location, true);
        }

        public string DisplayTranslatedDestination(Rule rule, string location, string style = "")
        {
            return DisplayTranslatedSourceOrDestination(rule, style, location, false);
        }

        public string DisplayTranslatedService(Rule rule, string location, string style = "")
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedServiceNegated)
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            foreach (ServiceWrapper service in rule.NatData.TranslatedServices)
            {
                result.Append(constructLink("svc", service.Content.Type.Name == "group" ? "oi oi-list-rich" : "oi oi-wrench", service.Content.Id, service.Content.Name, location, rule.MgmtId, style));

                if (service.Content.DestinationPort != null)
                    result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                        : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

            return result.ToString();
        }

        private string DisplayTranslatedSourceOrDestination(Rule rule, string style, string location, bool isSource)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if ((isSource && rule.NatData.TranslatedSourceNegated) ||(!isSource && rule.NatData.TranslatedDestinationNegated))
            {
                result.AppendLine(userConfig.GetText("negated") + "<br>");
            }

            foreach (NetworkLocation nwLocation in isSource ? rule.NatData.TranslatedFroms : rule.NatData.TranslatedTos )
            {
                if (nwLocation.User != null)
                {
                    result.Append(constructLink("user", "oi oi-people", nwLocation.User.Id, nwLocation.User.Name, location, rule.MgmtId, style) + "@");
                }
                result.Append(constructLink("nwobj", getObjSymbol(nwLocation.Object.Type.Name), nwLocation.Object.Id, nwLocation.Object.Name, location, rule.MgmtId, style));

                result.Append(" (");
                result.Append(DisplayIpRange(nwLocation.Object.IP, nwLocation.Object.IpEnd));
                result.Append(")");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

            return result.ToString();
        }
    }
}
