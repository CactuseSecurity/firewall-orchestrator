﻿using NetTools;
using FWO.Config.Api;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Ui.Services
{
    public class ModellingAppServerHandler : ModellingHandlerBase
    {
        public ModellingAppServer ActAppServer { get; set; } = new();
        public List<ModellingAppServer> AvailableAppServers { get; set; } = new();
        private ModellingAppServer ActAppServerOrig { get; set; } = new();


        public ModellingAppServerHandler(ApiConnection apiConnection, UserConfig userConfig, FwoOwner application, 
            ModellingAppServer appServer, List<ModellingAppServer> availableAppServers, bool addMode, Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, application, addMode, displayMessageInUi)
        {
            ActAppServer = appServer;
            AvailableAppServers = availableAppServers;
            ActAppServerOrig = new ModellingAppServer(ActAppServer);
        }
        
        public async Task<bool> Save()
        {
            try
            {
                if (ActAppServer.Sanitize())
                {
                    DisplayMessageInUi(null, userConfig.GetText("save_app_server"), userConfig.GetText("U0001"), true);
                }
                if(CheckAppServer())
                {
                    if(AddMode)
                    {
                        await AddAppServerToDb();
                    }
                    else
                    {
                        await UpdateAppServerInDb();
                    }
                    return true;
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
            if(!AddMode)
            {
                AvailableAppServers[AvailableAppServers.FindIndex(x => x.Id == ActAppServer.Id)] = ActAppServerOrig;
            }
        }

        private bool CheckAppServer()
        {
            if(ActAppServer.Ip == null || ActAppServer.Ip == "")
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("E5102"), true);
                return false;
            }
            if(!CheckIpAdress(ActAppServer.Ip))
            {
                DisplayMessageInUi(null, userConfig.GetText("edit_app_server"), userConfig.GetText("wrong_ip_address"), true);
                return false;
            }
            return true;
        }

        private static bool CheckIpAdress(string ip)
        {
            IPAddressRange dummyOut;
            return IPAddressRange.TryParse(ip, out dummyOut);
        }

        private async Task AddAppServerToDb()
        {
            try
            {
                var Variables = new
                {
                    name = ActAppServer.Name,
                    appId = Application.Id,
                    ip = IPAddressRange.Parse(ActAppServer.Ip).ToCidrString(),   // todo ?
                    importSource = GlobalConst.kManual  // todo
                };
                ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<NewReturning>(ModellingQueries.newAppServer, Variables)).ReturnIds;
                if (returnIds != null)
                {
                    ActAppServer.Id = returnIds[0].NewId;
                    await LogChange(ModellingTypes.ChangeType.Insert, ModellingTypes.ObjectType.AppServer, ActAppServer.Id,
                        $"New App Server: {ActAppServer.Display()}", Application.Id);
                    AvailableAppServers.Add(ActAppServer);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_app_server"), "", true);
            }
        }

        private async Task UpdateAppServerInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActAppServer.Id,
                    name = ActAppServer.Name,
                    appId = Application.Id,
                    ip = IPAddressRange.Parse(ActAppServer.Ip).ToCidrString(),   // todo ?
                    importSource = GlobalConst.kManual  // todo
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateAppServer, Variables);
                await LogChange(ModellingTypes.ChangeType.Update, ModellingTypes.ObjectType.AppServer, ActAppServer.Id,
                    $"Updated App Server: {ActAppServer.Display()}", Application.Id);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
        }
    }
}