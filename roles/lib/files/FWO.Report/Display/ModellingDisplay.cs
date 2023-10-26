using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;
using System.Text.RegularExpressions;

namespace FWO.Ui.Display
{
    public class ModellingDisplay : RuleDisplayBase
    {
        public ModellingDisplay(UserConfig userConfig) : base(userConfig)
        { }

        public static string DisplayService(ModellingService service)
        {
            return RuleDisplayBase.DisplayService(ModellingService.ToNetworkService(service), FWO.Report.Filter.ReportType.Rules, service.Name).ToString();
        }

        public static string DisplayServiceGroup(ServiceGroup grp)
        {
            if(grp.Name != null && grp.Name != "")
            {
                return grp.Name;
            }
            if(grp.Services.Count > 0)
            {
                return DisplayService(grp.Services[0].Content);
            }
            return "anything else";
        }
    }
}
