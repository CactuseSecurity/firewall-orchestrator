using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

namespace FWO.Ui.Display
{
    public class ModellingDisplay : RuleDisplayBase
    {
        public ModellingDisplay(UserConfig userConfig) : base(userConfig)
        { }

        public static string DisplayService(ModellingService service)
        {
            return DisplayService(ModellingService.ToNetworkService(service), Report.Filter.ReportType.Rules, service.Name).ToString();
        }

        public static string DisplayServiceGroup(ModellingServiceGroup grp)
        {
            if(grp.Name != null && grp.Name != "")
            {
                return grp.Name;
            }
            if(grp.Services.Count > 0)
            {
                return DisplayService(grp.Services[0].Content);
            }
            return "no name";
        }

        public static string DisplayAppServer(ModellingAppServer appServer)
        {
            return (appServer.IsDeleted ? "*" : "") + NwObjDisplay.DisplayWithName(ModellingAppServer.ToNetworkObject(appServer));
        }

        public static string DisplayAppRole(ModellingAppRole appRole)
        {
            if(appRole.Name != null && appRole.Name != "")
            {
                return appRole.Name;
            }
            if(appRole.AppServers.Count > 0)
            {
                return DisplayAppServer(appRole.AppServers[0].Content);
            }
            return "anything else";
        }
    }
}
