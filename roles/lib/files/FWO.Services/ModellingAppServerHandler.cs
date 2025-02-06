using NetTools;
using FWO.Config.Api;
using FWO.Basics;
using FWO.Api.Data;
using FWO.Api.Client;


namespace FWO.Services
{
    public class ModellingAppServerHandler : ModellingHandlerBase
    {
        public ModellingAppServer ActAppServer { get; set; } = new();
        private ModellingAppServer ActAppServerOrig { get; set; } = new();


        public ModellingAppServerHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application,
            ModellingAppServer appServer, List<ModellingAppServer> availableAppServers, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
            : base(apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            ActAppServer = appServer;
            AvailableAppServers = availableAppServers;
            ActAppServerOrig = new ModellingAppServer(ActAppServer);
        }

        public async Task<bool> Save()
        {
            try
            {
                ActAppServer.AppId = Application.Id;
                ActAppServer.ImportSource = GlobalConst.kManual;
                if (ActAppServer.Sanitize())
                {
                   DisplayMessageInUi(null, userConfig.GetText("save_app_server"), userConfig.GetText("U0001"), true);
                }
                if (CheckAppServer())
                {
                    (long? appServerId, string? ExistingAppServerName) = await AppServerHelper.UpsertAppServer(apiConnection, ActAppServer, !userConfig.DnsLookup, true, AddMode);
                    if (appServerId != null)
                    {
                        if (AddMode)
                        {
                            ActAppServer.Id = (long)appServerId;
                            AvailableAppServers.Add(ActAppServer);
                            await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ModObjectType.AppServer, ActAppServer.Id,
                                $"New App Server: {ActAppServer.Display()}", Application.Id);
                        }
                        else
                        {
                            await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ModObjectType.AppServer, ActAppServer.Id,
                                $"Updated App Server: {ActAppServer.Display()}", Application.Id);
                        }
                        return true;
                    }
                    else if (!string.IsNullOrEmpty(ExistingAppServerName))
                    {
                        if(ExistingAppServerName == ActAppServer.Name)
                        {
                            DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("E9018"), true);
                        }
                        else
                        {
                            DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), $"{userConfig.GetText("E9010")} {DisplayBase.DisplayIp(ActAppServer.Ip, ActAppServer.IpEnd)} in {ExistingAppServerName}", true);
                        }
                        return false;
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("E0034"), true);
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
            return false;
        }

        public void Reset()
        {
            ActAppServer = ActAppServerOrig;
            if (!AddMode && !ReadOnly)
            {
                AvailableAppServers[AvailableAppServers.FindIndex(x => x.Id == ActAppServer.Id)] = ActAppServerOrig;
            }
        }

        private bool CheckAppServer()
        {
            if (ActAppServer.Ip == null || ActAppServer.Ip == "" || ActAppServer.CustomType == null || ActAppServer.CustomType == 0)
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("E5102"), true);
                return false;
            }
            if (!CheckIpAdress(ActAppServer.Ip))
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("wrong_ip_address"), true);
                return false;
            }
            return true;
        }

        private static bool CheckIpAdress(string ip)
        {
            return IPAddressRange.TryParse(ip, out _);
        }
    }
}
