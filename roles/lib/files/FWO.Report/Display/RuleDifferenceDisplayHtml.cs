using FWO.Data;
using FWO.Logging;
using FWO.Config.Api;
using FWO.Report;
using FWO.Basics;

namespace FWO.Ui.Display
{
    public class RuleDifferenceDisplayHtml : RuleDisplayBase
    {
        public RuleDifferenceDisplayHtml(UserConfig userConfig) : base(userConfig)
        { }

        public string DisplaySourceDiff(Rule rule, OutputLocation location, ReportType reportType)
        {
            return DisplayDiff(
                rule.Froms.Where(f => f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType)),
                rule.DisregardedFroms.ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType)),
                rule.Froms.Where(f => !f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType))
                );
        }

        public string DisplayDestinationDiff(Rule rule, OutputLocation location, ReportType reportType)
        {
            return DisplayDiff(
                rule.Tos.Where(f => f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType)),
                rule.DisregardedTos.ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType)),
                rule.Tos.Where(f => !f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocationHtml(nwLoc, reportType))
                );
        }

        public string DisplayServiceDiff(Rule rule, OutputLocation location, ReportType reportType)
        {
            return DisplayDiff(
                rule.Services.Where(f => f.Content.IsSurplus).ToList().ConvertAll(svc => DisplayServiceHtml(svc.Content, reportType)),
                rule.DisregardedServices.ToList().ConvertAll(svc => DisplayServiceHtml(svc, reportType)),
                rule.Services.Where(f => !f.Content.IsSurplus).ToList().ConvertAll(svc => DisplayServiceHtml(svc.Content, reportType))
                );
        }


        /// <summary>
        /// Renders the unchanged elements followed by the elements missing in and surplus in the firewall rule.
        /// </summary>
        private string DisplayDiff(List<string> surplusElems, List<string> missingElems, List<string> unchangedElems)
        {
            return (unchangedElems.Count > 0 ? $"<p>{string.Join("<br>", unchangedElems)}<br></p>" : "")
                    + (missingElems.Count > 0 ? $"{userConfig.GetText("missing_in_fw_rule")}: <p style=\"{GlobalConst.kStyleMissing}\">{string.Join("<br>", missingElems)}<br></p>" : "")
                    + (surplusElems.Count > 0 ? $"{userConfig.GetText("surplus_in_fw_rule")}: <p style=\"{GlobalConst.kStyleSurplus}\">{string.Join("<br>", surplusElems)}</p>" : "");
        }
    }
}
