using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

namespace FWO.Ui.Display
{
    public class NatRuleDisplay : RuleDisplay
    {
        public NatRuleDisplay(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplayTranslatedSource(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedSourceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (NetworkLocation source in rule.NatData.TranslatedFroms)
            {
                if (source.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (source.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else
                    symbol = "oi oi-monitor";

                string page = recert ? "certification" : "report";

                if (source.User != null)
                    result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a href=\"report#report-m{rule.MgmtId}-user{source.User.Id}\" target=\"_top\" style=\"{style}\">{source.User.Name}</a>@");
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report#report-m{rule.MgmtId}-nwobj{source.Object.Id}\" target=\"_top\" style=\"{style}\">{source.Object.Name}</a>");
                result.Append((source.Object.IP != null ? $" ({source.Object.IP})" : ""));
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

//            string translSrc = result.ToString();
//            if(translSrc == DisplaySource(rule, style))
//            {
//                return "origin";
//            }
//            return translSrc;
            return result.ToString();
        }

        public string DisplayTranslatedDestination(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedDestinationNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (NetworkLocation destination in rule.NatData.TranslatedTos)
            {
                if (destination.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (destination.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else
                    symbol = "oi oi-monitor";

                string page = recert ? "certification" : "report";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report#report-m{rule.MgmtId}-nwobj{destination.Object.Id}\" target=\"_top\" style=\"{style}\">{destination.Object.Name}</a>");
                result.Append(destination.Object.IP != null ? $" ({destination.Object.IP})" : "");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

//            string translDst = result.ToString();
//            if(translDst == DisplayDestination(rule, style))
//            {
//                return "origin";
//            }
//            return translDst;
            return result.ToString();
        }

        public string DisplayTranslatedService(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedServiceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (ServiceWrapper service in rule.NatData.TranslatedServices)
            {
                if (service.Content.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else
                    symbol = "oi oi-wrench";

                string page = recert ? "certification" : "report";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report#report-m{rule.MgmtId}-svc{service.Content.Id}\" target=\"_top\" style=\"{style}\">{service.Content.Name}</a>");

                if (service.Content.DestinationPort != null)
                    result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                        : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

//            string translSvc = result.ToString();
//            if(translSvc == DisplayService(rule, style))
//            {
//                return "origin";
//            }
//            return translSvc;
            return result.ToString();
        }
    }
}
