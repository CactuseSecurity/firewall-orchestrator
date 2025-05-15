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
                rule.Froms.Where(f => f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString()), 
                rule.DisregardedFroms.ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString()), 
                rule.Froms.Where(f => !f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString())
                );
        }

        public string DisplayDestinationDiff(Rule rule, OutputLocation location, ReportType reportType)
        {
            return DisplayDiff(
                rule.Tos.Where(f => f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString()), 
                rule.DisregardedTos.ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString()), 
                rule.Tos.Where(f => !f.Object.IsSurplus).ToList().ConvertAll(nwLoc => DisplayNetworkLocation(nwLoc, reportType).ToString())
                );
        }

        public string DisplayServiceDiff(Rule rule, OutputLocation location, ReportType reportType)
        {
            return DisplayDiff(
                rule.Services.Where(f => f.Content.IsSurplus).ToList().ConvertAll(svc => DisplayService(svc.Content, reportType).ToString()), 
                rule.DisregardedServices.ToList().ConvertAll(svc => DisplayService(svc, reportType).ToString()), 
                rule.Services.Where(f => !f.Content.IsSurplus).ToList().ConvertAll(svc => DisplayService(svc.Content, reportType).ToString())
                );
        }


        private string DisplayDiff(List<string> addedElems, List<string> deletedElems, List<string> unchangedElems)
        {
            return (unchangedElems.Count > 0 ? $"<p>{string.Join("<br>", unchangedElems)}<br></p>" : "")
                    + (deletedElems.Count > 0 ? $"{userConfig.GetText("missing")}: <p style=\"{GlobalConst.kStyleDeleted}\">{string.Join("<br>", deletedElems)}<br></p>" : "")
                    + (addedElems.Count > 0 ? $"{userConfig.GetText("surplus")}: <p style=\"{GlobalConst.kStyleAdded}\">{string.Join("<br>", addedElems)}</p>" : "");
        }
    }
}
