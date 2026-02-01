using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;


namespace FWO.Services
{
    public partial class ModellingConnectionHandler
    {
        /// <summary>
        /// Checks the given interface object if it can be used with network areas that are added to the connection.
        /// </summary>
        /// <param name="interf"></param>
        /// <returns></returns>
        public bool InterfaceAllowedWithNetworkArea(ModellingConnection interf)
        {
            return ActConn.IsInterface || ActConn.IsCommonService || interf.AppId == ActConn.AppId ||
                   (ActConn.DestinationAreas.Count == 0 && DstAreasToAdd.Count == 0 && ActConn.SourceAreas.Count == 0 && SrcAreasToAdd.Count == 0);
        }

        /// <summary>
        /// Checks the selected interface if it is foreign to the modelled connection
        /// </summary>
        /// <returns></returns>
        public bool IsNotInterfaceForeignToApp()
        {
            if (!ActConn.IsInterface && !ActConn.IsCommonService && ActConn.UsedInterfaceId != null &&
                ActConn.UsedInterfaceId > 0 && PreselectedInterfaces.FirstOrDefault(_ => _.Id == ActConn.UsedInterfaceId)?.AppId != ActConn.AppId)
            {
                return false;
            }
            return true;
        }

        public async Task<long> CreateNewRequestedInterface(long ticketId, bool asSource, string name, string reason)
        {
            ActConn.TicketId = ticketId;
            ActConn.Name = name + " (Ticket: " + ActConn.TicketId.ToString() + ")";
            ActConn.Reason = $"{userConfig.GetText("from_ticket")} {ticketId} ({userConfig.GetText("U9012")}): \r\n{reason}";
            ActConn.IsInterface = true;
            ActConn.IsRequested = true;
            if (asSource)
            {
                ActConn.SourceAppRoles.Add(new() { Content = DummyAppRole });
            }
            else
            {
                ActConn.DestinationAppRoles.Add(new() { Content = DummyAppRole });
            }
            await AddConnectionToDb(true);

            ActConn.AppId = RequesterId;
            await AddToPreselectedList(ActConn);
            return ActConn.Id;
        }

        public string DisplayInterface(ModellingConnection? inter)
        {
            if (inter != null)
            {
                FwoOwner? app = AllApps.FirstOrDefault(x => x.Id == inter.AppId);
                if (app != null)
                {
                    return inter.DisplayNameWithOwner(app);
                }
                return inter.Name ?? "";
            }
            return "";
        }

        public async Task RequestReplaceInterface(ModellingConnection interf)
        {
            if (interf.SourceFilled() != ActConn.SourceFilled() || interf.DestinationFilled() != ActConn.DestinationFilled())
            {
                DisplayMessageInUi(null, userConfig.GetText("replace"), userConfig.GetText("E9017"), true);
                return;
            }
            ReplaceMode = true;
            await DisplaySelectedInterface(interf);
        }

        public async Task ReplaceInterface(Task<AuthenticationState> authenticationStateTask, MiddlewareClient middlewareClient)
        {
            try
            {
                if (IntConnHandler != null)
                {
                    await ReplaceLinks();
                    await RemoveFromAllSelections();
                    if (await DeleteRequestedInterface())
                    {
                        await UpdateTicket(authenticationStateTask, middlewareClient);
                    }
                    await RefreshParent();
                    Close();
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("replace"), "", true);
            }
        }

        private async Task ReplaceLinks()
        {
            if (await CheckInterfaceInUse(ActConn))
            {
                var Variables = new
                {
                    usedInterfaceIdOld = ActConn.Id,
                    usedInterfaceIdNew = IntConnHandler?.ActConn.Id
                };
                int usingConns = (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.replaceUsedInterface, Variables)).AffectedRows;
                await LogChange(ModellingTypes.ChangeType.Replace, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Replaced Used Interface: {ActConn.Name} by: {IntConnHandler?.ActConn.Name} for {usingConns} Connections", Application.Id);
            }
        }

        public async Task RemoveFromAllSelections()
        {
            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnection, new { connectionId = ActConn.Id });
        }

        private async Task<bool> DeleteRequestedInterface()
        {
            if (await CheckInterfaceInUse(ActConn))
            {
                DisplayMessageInUi(null, userConfig.GetText("delete_interface"), userConfig.GetText("E9016"), true);
                return false;
            }
            if (await DeleteConnection(ActConn))
            {
                await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Deleted Interface: {ActConn.Name}", Application.Id);
                Connections.Remove(ActConn);
            }
            return true;
        }

        private async Task UpdateTicket(Task<AuthenticationState> authenticationStateTask, MiddlewareClient middlewareClient)
        {
            if (ActConn.TicketId != null)
            {
                try
                {
                    // change referred connId ?
                    string comment = $"{userConfig.GetText("U9016")}: {IntConnHandler?.ActConn.Name}";
                    TicketCreator ticketCreator = new(apiConnection, userConfig, authenticationStateTask!.Result.User, middlewareClient, WorkflowPhases.implementation);
                    if (await ticketCreator.PromoteNewInterfaceImplTask((long)ActConn.TicketId, ExtStates.Done, comment))
                    {
                        DisplayMessageInUi(null, comment, userConfig.GetText("U9013"), false);
                    }
                }
                catch (Exception exception)
                {
                    DisplayMessageInUi(exception, userConfig.GetText("update_ticket"), "", true);
                }
            }
        }

        public void InterfaceToConn(ModellingConnection interf)
        {
            InterfaceName = interf.Name ?? "";
            SrcReadOnly = interf.SourceFilled();
            DstReadOnly = interf.DestinationFilled();
            SvcReadOnly = true;
            ActConn.IsInterface = false;
            ActConn.UsedInterfaceId = interf.Id;
            ActConn.InterfaceIsRequested = interf.IsRequested;
            ActConn.InterfaceIsRejected = interf.GetBoolProperty(ConState.Rejected.ToString());
            ActConn.InterfaceIsDecommissioned = interf.GetBoolProperty(ConState.Decommissioned.ToString());
            ActConn.TicketId = interf.TicketId;
            if (SrcReadOnly)
            {
                SrcAppServerToDelete.AddRange([.. ModellingAppServerWrapper.Resolve(ActConn.SourceAppServers)]);
                ActConn.SourceAppServers = [.. interf.SourceAppServers];
                SrcAppRolesToDelete.AddRange([.. ModellingAppRoleWrapper.Resolve(ActConn.SourceAppRoles)]);
                ActConn.SourceAppRoles = [.. interf.SourceAppRoles];
                SrcAreasToDelete.AddRange([.. ModellingNetworkAreaWrapper.Resolve(ActConn.SourceAreas)]);
                ActConn.SourceAreas = [.. interf.SourceAreas];
                SrcNwGroupsToDelete.AddRange([.. ModellingNwGroupWrapper.Resolve(ActConn.SourceOtherGroups)]);
                ActConn.SourceOtherGroups = [.. interf.SourceOtherGroups];
                ActConn.SrcFromInterface = true;
            }
            else
            {
                DstAppServerToDelete.AddRange([.. ModellingAppServerWrapper.Resolve(ActConn.DestinationAppServers)]);
                ActConn.DestinationAppServers = [.. interf.DestinationAppServers];
                DstAppRolesToDelete.AddRange([.. ModellingAppRoleWrapper.Resolve(ActConn.DestinationAppRoles)]);
                ActConn.DestinationAppRoles = [.. interf.DestinationAppRoles];
                DstAreasToDelete.AddRange([.. ModellingNetworkAreaWrapper.Resolve(ActConn.DestinationAreas)]);
                ActConn.DestinationAreas = [.. interf.DestinationAreas];
                DstNwGroupsToDelete.AddRange([.. ModellingNwGroupWrapper.Resolve(ActConn.DestinationOtherGroups)]);
                ActConn.DestinationOtherGroups = [.. interf.DestinationOtherGroups];
                ActConn.DstFromInterface = true;
            }
            ActConn.Services = [.. interf.Services];
            ActConn.ServiceGroups = [.. interf.ServiceGroups];
            ActConn.ExtraConfigsFromInterface = interf.ExtraConfigs;
        }

        public void RemoveInterf()
        {
            InterfaceName = "";
            if (SrcReadOnly)
            {
                ActConn.SourceAppServers = [];
                ActConn.SourceAppRoles = [];
                ActConn.SourceAreas = [];
                ActConn.SourceOtherGroups = [];
                ActConn.SrcFromInterface = false;
            }
            if (DstReadOnly)
            {
                ActConn.DestinationAppServers = [];
                ActConn.DestinationAppRoles = [];
                ActConn.DestinationAreas = [];
                ActConn.DestinationOtherGroups = [];
                ActConn.DstFromInterface = false;
            }
            ActConn.Services = [];
            ActConn.ServiceGroups = [];
            ActConn.UsedInterfaceId = null;
            ActConn.InterfaceIsRequested = false;
            ActConn.InterfaceIsRejected = false;
            ActConn.InterfaceIsDecommissioned = false;
            ActConn.TicketId = null;
            SrcReadOnly = false;
            DstReadOnly = false;
            SvcReadOnly = false;
            ActConn.ExtraConfigsFromInterface = [];
        }

        private async Task DecommInterface()
        {
            await DecommInterfaceInDb();
            await UpdateStatusInterfaceUsersDecomm(ActConn.Id);
        }

        private async Task DecommInterfaceInDb()
        {
            try
            {
                var Variables = new
                {
                    id = ActConn.Id,
                    reason = ActConn.Reason,
                    connProp = ActConn.Properties,
                    removalDate = DateTime.Now
                };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionDecommission, Variables);
                await LogChange(ModellingTypes.ChangeType.Decommission, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Decommissioned {kInterface}: {ActConn.Name}", Application.Id);

                Connections[Connections.FindIndex(x => x.Id == ActConn.Id)] = ActConn;
                foreach (var conn in Connections.Where(x => x.UsedInterfaceId == ActConn.Id))
                {
                    await ExtractUsedInterface(conn);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task UpdateStatusInterfaceUsersDecomm(int interfaceId)
        {
            try
            {
                List<ModellingConnection> usingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = interfaceId });
                foreach (var conn in usingConnections.Where(c => !c.GetBoolProperty(ConState.InterfaceDecommissioned.ToString())))
                {
                    conn.AddProperty(ConState.InterfaceDecommissioned.ToString());
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, new { id = conn.Id, connProp = conn.Properties });
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("update_interf_user"), "", true);
            }
        }

        private async Task UpdateStatusInterfaceUsersPublished(int interfaceId)
        {
            try
            {
                List<ModellingConnection> usingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = interfaceId });
                foreach (var conn in usingConnections.Where(c => c.GetBoolProperty(ConState.InterfaceRequested.ToString())))
                {
                    conn.RemoveProperty(ConState.InterfaceRequested.ToString());
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.updateConnectionProperties, new { id = conn.Id, connProp = conn.Properties });
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("update_interf_user"), "", true);
            }
        }

        // Preselected Interfaces
        public void RequestRemovePreselectedInterface(ModellingConnection interf)
        {
            actInterface = interf;
            Message = userConfig.GetText("U9006") + interf.Name + "?";
            RemovePreselectedInterfaceMode = true;
        }

        public async Task RemovePreselectedInterface()
        {
            try
            {
                if ((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnectionFromApp, new { appId = Application.Id, connectionId = actInterface.Id })).AffectedRows > 0)
                {
                    PreselectedInterfaces.Remove(PreselectedInterfaces.FirstOrDefault(x => x.Id == actInterface.Id) ?? throw new KeyNotFoundException("Did not find object."));
                    RemovePreselectedInterfaceMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_interface"), "", true);
            }
        }

        public async Task DisplaySelectedInterface(ModellingConnection interf)
        {
            FwoOwner? app = AllApps.FirstOrDefault(x => x.Id == interf.AppId);
            IntConnHandler = new ModellingConnectionHandler(apiConnection, userConfig, app ?? new(), Connections, interf, false, true, DisplayMessageInUi, DefaultInit.DoNothing, false);
            await IntConnHandler.Init();
            DisplaySelectedInterfaceMode = true;
        }

        private async Task AddToPreselectedList(ModellingConnection? requestedInterface)
        {
            try
            {
                if (requestedInterface != null)
                {
                    var Variables = new
                    {
                        appId = requestedInterface.AppId,
                        connectionId = requestedInterface.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addSelectedConnection, Variables);
                    PreselectedInterfaces.Add(requestedInterface);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("add_interface"), "", true);
            }
        }
    }
}
