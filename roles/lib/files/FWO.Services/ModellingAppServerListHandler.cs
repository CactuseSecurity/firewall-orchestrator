﻿using FWO.Config.Api;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Api.Client;
using FWO.Api.Client.Queries;


namespace FWO.Services
{
    public class ModellingAppServerListHandler : ModellingHandlerBase
    {
        public List<ModellingAppServer> ManualAppServers { get; set; } = [];
        public ModellingAppServerHandler? AppServerHandler;
        private ModellingAppServer actAppServer = new();
        public bool AddAppServerMode = false;
        public bool EditAppServerMode = false;
        public bool DeleteAppServerMode = false;
        public bool ReactivateAppServerMode = false;


        public ModellingAppServerListHandler(ApiConnection apiConnection, UserConfig userConfig,
            Action<Exception?, string, string, bool> displayMessageInUi)
            : base (apiConnection, userConfig, new FwoOwner(), false, displayMessageInUi)
        {}

        public async Task Init(FwoOwner application)
        {
            try
            {
                Application = application;
                ManualAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, new { importSource = GlobalConst.kManual, appId = Application.Id });
                ManualAppServers.AddRange(await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersBySource, new { importSource = GlobalConst.kCSV_ + "%", appId = Application.Id }));
                foreach(var appServer in ManualAppServers)
                {
                    appServer.InUse = await CheckAppServerInUse(appServer);
                    appServer.HighestPrio = await AppServerHelper.NoHigherPrioActive(apiConnection, appServer);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        public void CreateAppServer()
        {
            AddAppServerMode = true;
            HandleAppServer(new ModellingAppServer(){ ImportSource = GlobalConst.kManual, InUse = false });
        }

        public void EditAppServer(ModellingAppServer appServer)
        {
            AddAppServerMode = false;
            HandleAppServer(appServer);
        }

        public void HandleAppServer(ModellingAppServer appServer)
        {
            try
            {
                AppServerHandler = new ModellingAppServerHandler(apiConnection, userConfig, Application, appServer, ManualAppServers, AddAppServerMode, DisplayMessageInUi);
                EditAppServerMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_server"), "", true);
            }
        }

        public void RequestDeleteAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            Message = userConfig.GetText(appServer.InUse ? "U9007" : "U9008") + appServer.Name + "?";
            DeleteAppServerMode = true;
        }

        public async Task DeleteAppServer()
        {
            try
            {
                apiConnection.SetRole(Roles.Admin);
                if(await CheckAppServerInUse(actAppServer))
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerDeletedState, new { id = actAppServer.Id, deleted = true });
                    await LogChange(ModellingTypes.ChangeType.MarkDeleted, ModellingTypes.ModObjectType.AppServer, actAppServer.Id,
                        $"Mark App Server as deleted: {actAppServer.Display()}", Application.Id);
                }
                else if((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteAppServer, new { id = actAppServer.Id })).AffectedRows > 0)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.AppServer, actAppServer.Id,
                        $"Deleted App Server: {actAppServer.Display()}", Application.Id);
                }
                await AppServerHelper.ReactivateOtherSource(apiConnection, userConfig, actAppServer);
                await Init(Application);
                apiConnection.SwitchBack();
                DeleteAppServerMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_server"), "", true);
            }
        }

        public void RequestReactivateAppServer(ModellingAppServer appServer)
        {
            actAppServer = appServer;
            Message = userConfig.GetText("U9005") + appServer.Name + "?";
            ReactivateAppServerMode = true;
        }

        public async Task ReactivateAppServer()
        {
            try
            {
                if(actAppServer.IsDeleted)
                {
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setAppServerDeletedState, new { id = actAppServer.Id, deleted = false });
                    await LogChange(ModellingTypes.ChangeType.Reactivate, ModellingTypes.ModObjectType.AppServer, actAppServer.Id,
                        $"Reactivate App Server: {actAppServer.Display()}", Application.Id);
                    await AppServerHelper.DeactivateOtherSources(apiConnection, userConfig, actAppServer);
                    await Init(Application);
                }
                ReactivateAppServerMode = false;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("reactivate"), "", true);
            }
        }
    }
}
