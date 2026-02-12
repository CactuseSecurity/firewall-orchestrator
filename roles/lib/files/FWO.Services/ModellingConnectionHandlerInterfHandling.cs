using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using FWO.Middleware.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;


namespace FWO.Services
{
    public partial class ModellingConnectionHandler
    {
        public List<ModellingConnection> PreselectedInterfaces { get; set; } = [];
        public int RequesterId { get; set; } = 0;
        private ModellingConnection actInterface = new();


        public async Task RefreshPreselectedInterfaces()
        {
            try
            {
                PreselectedInterfaces = ModellingConnectionWrapper.Resolve(
                    await apiConnection.SendQueryAsync<List<ModellingConnectionWrapper>>(ModellingQueries.getSelectedConnections, new { appId = Application.Id })).ToList();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        private async Task RefreshInterfaceData()
        {
            try
            {
                if (ActConn.IsInterface)
                {
                    ActConn.PermittedOwners = await apiConnection.SendQueryAsync<List<FwoOwner>>(ModellingQueries.getPermittedOwnersForConnection, new { connectionId = ActConn.Id });
                    PermittedOwnersToAdd.Clear();
                    PermittedOwnersToDelete.Clear();
                    await InitUsingConnections(ActConn.Id);
                    if (!AddMode && !ReadOnly)
                    {
                        SrcFix = ActConn.SourceFilled();
                        DstFix = ActConn.DestinationFilled();
                    }
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("fetch_data"), "", true);
            }
        }

        private bool CheckInterface()
        {
            if (string.IsNullOrWhiteSpace(ActConn.InterfacePermission))
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9021"), true);
                return false;
            }
            int srcDummyARCount = ActConn.SourceAppRoles.Count(x => x.Content.Id == DummyAppRole.Id);
            int dstDummyARCount = ActConn.DestinationAppRoles.Count(x => x.Content.Id == DummyAppRole.Id);
            if (!(SrcFilledInWork(srcDummyARCount) || DstFilledInWork(dstDummyARCount)) || !SvcFilledInWork())
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9004"), true);
                return false;
            }
            if (!AddMode && (SrcFilledInWork(srcDummyARCount) != ActConnOrig.SourceFilled() || DstFilledInWork(dstDummyARCount) != ActConnOrig.DestinationFilled()))
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9005"), true);
                return false;
            }
            if (ActConn.InterfacePermission == InterfacePermissions.Private.ToString() &&
                ActConnOrig.InterfacePermission != InterfacePermissions.Private.ToString() &&
                UsingConnections.Any(c => c.AppId != ActConn.AppId))
            {
                DisplayMessageInUi(null, userConfig.GetText(EditConnection), userConfig.GetText("E9020"), true);
                return false;
            }
            return true;
        }

        public async Task DecommissionInterface(string reason, bool proposeAlternative, ModellingConnection? proposedInterface,
            MiddlewareClient middlewareClient)
        {
            ActConn.Removed = true;
            ActConn.Reason += $"<br>{DateTime.Now:dd.MM.yyyy} {userConfig.User.Name}: {userConfig.GetText("decomm_interface")}: {reason}";
            await Save(true, true);
            await RemoveFromAllSelections();

            List<FwoOwner> appsToNotify = [];
            if (userConfig.ModDecommEmailReceiver != EmailRecipientOption.None)
            {
                appsToNotify = UsingConnections.Where(c => c.AppId != null && c.AppId != ActConn.AppId).Select(c => c.App).Distinct().ToList();
                await NotifyUsers(appsToNotify, reason, proposedInterface, middlewareClient);
            }

            await AddPermittedOwnersIfMissing(proposedInterface, appsToNotify);
            await AddToSelections(proposeAlternative, proposedInterface, appsToNotify);
        }

        protected virtual async Task NotifyUsers(List<FwoOwner> appsToNotify, string reason, ModellingConnection? proposedInterface, MiddlewareClient middlewareClient)
        {
            try
            {
                EmailHelper emailHelper = CreateEmailHelper(middlewareClient);
                await emailHelper.Init();

                string subject = userConfig.ModDecommEmailSubject.Replace(Placeholder.INTERFACE_NAME, ActConn.Name);

                int successCount = 0;
                int failCount = 0;
                foreach (var app in appsToNotify)
                {
                    if (await emailHelper.SendEmailToOwnerResponsibles(app, subject, ConstructBody(app, reason, proposedInterface), userConfig.ModDecommEmailReceiver))
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                if (successCount > 0)
                {
                    string msgText = userConfig.GetText("U9033").Replace(Placeholder.OK_NUMBER, successCount.ToString());
                    DisplayMessageInUi(null, userConfig.GetText("send_email"), msgText, false);
                }
                if (failCount > 0)
                {
                    string msgText = userConfig.GetText("E9019").Replace(Placeholder.FAIL_NUMBER, failCount.ToString());
                    DisplayMessageInUi(null, userConfig.GetText("send_email"), msgText, true);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("notification"), "", true);
            }
        }

        protected virtual EmailHelper CreateEmailHelper(MiddlewareClient middlewareClient)
        {
            return new EmailHelper(apiConnection, middlewareClient, userConfig, DisplayMessageInUi);
        }

        private string ConstructBody(FwoOwner app, string reason, ModellingConnection? proposedInterface)
        {
            string interfaceUrl = $"{userConfig.UiHostName}/{PageName.Modelling}/{proposedInterface?.App.ExtAppId}/{proposedInterface?.Id}";
            string interfacelink = $"<a target=\"_blank\" href=\"{interfaceUrl}\">{userConfig.GetText("interface")}: {proposedInterface?.Name}</a><br>";
            string body = userConfig.ModDecommEmailBody
                .Replace(Placeholder.INTERFACE_NAME, $"<b>{ActConn.Name}</b>")
                .Replace(Placeholder.NEW_INTERFACE_NAME, $"<b>{proposedInterface?.Name}</b>")
                .Replace(Placeholder.NEW_INTERFACE_LINK, $"<b>{interfacelink}</b>")
                .Replace(Placeholder.REASON, $"<b>{reason}</b>")
                .Replace(Placeholder.USER_NAME, $"<b>{userConfig.User.Name}</b>");
            string connList = string.Join("<br>", UsingConnections.Where(c => c.AppId != null && c.AppId == app.Id).Select(a => a.Name));
            return $"{body}<br><b>{connList}</b>";
        }

        private async Task AddToSelections(bool proposeAlternative, ModellingConnection? proposedInterface, List<FwoOwner> appsToNotify)
        {
            if (!proposeAlternative || proposedInterface == null)
            {
                return;
            }

            foreach (var app in appsToNotify)
            {
                try
                {
                    var variables = new
                    {
                        appId = app.Id,
                        connectionId = proposedInterface.Id
                    };
                    await apiConnection.SendQueryAsync<ReturnIdWrapper>(ModellingQueries.addSelectedConnection, variables);
                }
                catch (Exception)
                {
                    Log.WriteDebug(userConfig.GetText("add_interface"), "Interface was already selected");
                }
            }
        }

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
            if (UsingConnections.Count > 0)
            {
                var Variables = new
                {
                    usedInterfaceIdOld = ActConn.Id,
                    usedInterfaceIdNew = IntConnHandler?.ActConn.Id
                };
                int replacedConns = (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.replaceUsedInterface, Variables)).AffectedRows;
                await LogChange(ModellingTypes.ChangeType.Replace, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Replaced Used Interface: {ActConn.Name} by: {IntConnHandler?.ActConn.Name} for {replacedConns} Connections", Application.Id);
            }
        }

        public async Task RemoveFromAllSelections()
        {
            await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedConnection, new { connectionId = ActConn.Id });
        }

        private async Task<bool> DeleteRequestedInterface()
        {
            if (UsingConnections.Count > 0)
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
            ActConn.InterfaceNoPermission = interf.PermittedOwnerWrappers.Any(w => w.Owner != null && w.Owner.Id == ActConn.AppId);
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
            ActConn.InterfaceNoPermission = false;
            ActConn.TicketId = null;
            SrcReadOnly = false;
            DstReadOnly = false;
            SvcReadOnly = false;
            ActConn.ExtraConfigsFromInterface = [];
        }

        public bool PreparePublishInterface()
        {
            bool publishRequested = !AddMode && ActConn.IsInterface && ActConn.IsRequested && !ActConn.IsPublished &&
                ActConn.InterfacePermission != InterfacePermissions.Private.ToString();
            if(publishRequested)
            {
                ActConn.Creator = userConfig.User.Name;
                ActConn.IsRequested = false;
                ActConn.IsPublished = true;
                if(ActConn.AppId == null)
                {
                    ActConn.AppId = ActConn.ProposedAppId;
                    ActConn.ProposedAppId = null;
                }
            }
            return publishRequested;
        }

        private async Task DecommInterface()
        {
            await DecommInterfaceInDb();
            await UpdateStatusInterfaceUsersDecomm();
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

        private async Task UpdateStatusInterfaceUsersDecomm()
        {
            try
            {
                foreach (var conn in UsingConnections.Where(c => !c.GetBoolProperty(ConState.InterfaceDecommissioned.ToString())))
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

        private async Task UpdateStatusInterfaceUsersPublished()
        {
            try
            {
                foreach (var conn in UsingConnections.Where(c => c.GetBoolProperty(ConState.InterfaceRequested.ToString())))
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
