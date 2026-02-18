using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using System.Text.Json;


namespace FWO.Services.Modelling
{
    public partial class ModellingConnectionHandler
    {
        public List<ModellingAppRole> AvailableAppRoles { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableSelectedObjects { get; set; } = [];
        public List<ModellingNetworkAreaWrapper> AvailableCommonAreas { get; set; } = [];
        public List<ModellingNwGroupWrapper> AvailableNwGroups { get; set; } = [];
        public List<CommonAreaConfig> CommonAreaConfigItems { get; set; } = [];
        public ModellingAppServerHandler? AppServerHandler { get; set; }
        private readonly string InsertForbidden = "insert_forbidden";
        private ModellingAppRole actAppRole = new();
        private ModellingNwGroup actNwGrpObj = new();
        private List<ModellingConnection> FoundConnectionsForAppRole = [];

        public async Task InitAvailableNWObjects()
        {
            try
            {
                AvailableAppServers = await apiConnection.SendQueryAsync<List<ModellingAppServer>>(ModellingQueries.getAppServersForOwner, new { appId = Application.Id });
                AvailableAppRoles = await apiConnection.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getAppRoles, new { appId = Application.Id });

                List<ModellingNetworkArea> allAreas = await apiConnection.SendQueryAsync<List<ModellingNetworkArea>>(ModellingQueries.getNwGroupObjects, new { grpType = (int)ModellingTypes.ModObjectType.NetworkArea });
                CommonAreaConfigItems = [];
                if (userConfig.ModCommonAreas != "")
                {
                    CommonAreaConfigItems = JsonSerializer.Deserialize<List<CommonAreaConfig>>(userConfig.ModCommonAreas) ?? new();
                }
                AvailableCommonAreas = [];
                foreach (var comAreaConfig in CommonAreaConfigItems)
                {
                    ModellingNetworkArea? area = allAreas.FirstOrDefault(a => a.Id == comAreaConfig.AreaId);
                    if (area != null)
                    {
                        AvailableCommonAreas.Add(new() { Content = area });
                    }
                }
                AvailableNwGroups = [];

                AvailableSelectedObjects = await apiConnection.SendQueryAsync<List<ModellingNwGroupWrapper>>(ModellingQueries.getSelectedNwGroupObjects, new { appId = Application.Id });
                AvailableSelectedObjects = AvailableSelectedObjects.Where(x => AvailableCommonAreas.FirstOrDefault(a => a.Content.Id == x.Content.Id) == null).ToList();
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(InitEnvironment), "", true);
            }
        }

        public bool RefreshSelectableNwObjects()
        {
            AvailableNwElems = [];
            foreach (var obj in AvailableCommonAreas.Select(o => o.Content))
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.GroupType, obj.Id));
            }
            foreach (var obj in AvailableSelectedObjects.Select(o => o.Content))
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>(obj.GroupType, obj.Id));
            }
            foreach (var appRole in AvailableAppRoles)
            {
                AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppRole, appRole.Id));
            }
            if (userConfig.AllowServerInConn)
            {
                foreach (var appServer in AvailableAppServers.Where(x => !x.IsDeleted))
                {
                    AvailableNwElems.Add(new KeyValuePair<int, long>((int)ModellingTypes.ModObjectType.AppServer, appServer.Id));
                }
            }
            return true;
        }

        private void SyncSrcChanges()
        {
            if (ActConn.IsInterface && SrcFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    SrcAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
            foreach (var appServer in SrcAppServerToDelete)
            {
                ActConn.SourceAppServers.Remove(ActConn.SourceAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new KeyNotFoundException("Did not find app server."));
            }
            foreach (var appServer in SrcAppServerToAdd)
            {
                ActConn.SourceAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }
            foreach (var appRole in SrcAppRolesToDelete)
            {
                ActConn.SourceAppRoles.Remove(ActConn.SourceAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new KeyNotFoundException("Did not find app role."));
            }
            foreach (var appRole in SrcAppRolesToAdd)
            {
                ActConn.SourceAppRoles.Add(new ModellingAppRoleWrapper() { Content = appRole });
            }
            foreach (var area in SrcAreasToDelete)
            {
                ActConn.SourceAreas.Remove(ActConn.SourceAreas.FirstOrDefault(x => x.Content.Id == area.Id) ?? throw new KeyNotFoundException("Did not find area."));
            }
            foreach (var area in SrcAreasToAdd)
            {
                ActConn.SourceAreas.Add(new ModellingNetworkAreaWrapper() { Content = area });
            }
            foreach (var nwGroup in SrcNwGroupsToDelete)
            {
                ActConn.SourceOtherGroups.Remove(ActConn.SourceOtherGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new KeyNotFoundException("Did not find nwgroup."));
            }
            foreach (var nwGroup in SrcNwGroupsToAdd)
            {
                ActConn.SourceOtherGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup });
            }
        }

        private void SyncDstChanges()
        {
            if (ActConn.IsInterface && DstFilledInWork(1))
            {
                ModellingAppRoleWrapper? linkedDummyAR = ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == DummyAppRole.Id);
                if (linkedDummyAR != null)
                {
                    DstAppRolesToDelete.Add(linkedDummyAR.Content);
                }
            }
            foreach (var appServer in DstAppServerToDelete)
            {
                ActConn.DestinationAppServers.Remove(ActConn.DestinationAppServers.FirstOrDefault(x => x.Content.Id == appServer.Id) ?? throw new KeyNotFoundException("Did not find app server."));
            }
            foreach (var appServer in DstAppServerToAdd)
            {
                ActConn.DestinationAppServers.Add(new ModellingAppServerWrapper() { Content = appServer });
            }
            foreach (var appRole in DstAppRolesToDelete)
            {
                ActConn.DestinationAppRoles.Remove(ActConn.DestinationAppRoles.FirstOrDefault(x => x.Content.Id == appRole.Id) ?? throw new KeyNotFoundException("Did not find app role."));
            }
            foreach (var appRole in DstAppRolesToAdd)
            {
                ActConn.DestinationAppRoles.Add(new ModellingAppRoleWrapper() { Content = appRole });
            }
            foreach (var area in DstAreasToDelete)
            {
                ActConn.DestinationAreas.Remove(ActConn.DestinationAreas.FirstOrDefault(x => x.Content.Id == area.Id) ?? throw new KeyNotFoundException("Did not find area."));
            }
            foreach (var area in DstAreasToAdd)
            {
                ActConn.DestinationAreas.Add(new ModellingNetworkAreaWrapper() { Content = area });
            }
            foreach (var nwGroup in DstNwGroupsToDelete)
            {
                ActConn.DestinationOtherGroups.Remove(ActConn.DestinationOtherGroups.FirstOrDefault(x => x.Content.Id == nwGroup.Id) ?? throw new KeyNotFoundException("Did not find nwgroup."));
            }
            foreach (var nwGroup in DstNwGroupsToAdd)
            {
                ActConn.DestinationOtherGroups.Add(new ModellingNwGroupWrapper() { Content = nwGroup });
            }
        }

        /// <summary>
        /// Checks if the given network areas are allowed in the current connection/interface/service.
        /// </summary>
        /// <param name="networkAreas">The list of network areas to check.</param>
        /// <param name="reason">Out parameter to give context as a reason why it's not allowed, otherwise is's empty.</param>
        public bool NetworkAreaUseAllowed(List<ModellingNetworkArea> networkAreas, Direction direction, out (string Title, string Text) reason)
        {
            if (ActConn.IsCommonService)
            {
                reason.Title = userConfig.GetText("edit_common_service");
            }
            else if (ActConn.IsInterface)
            {
                reason.Title = userConfig.GetText("edit_interface");
            }
            else
            {
                reason.Title = userConfig.GetText(EditConnection);
            }

            reason.Text = "";
            if (IsAreaForbiddenInDirection(direction))
            {
                reason.Text = userConfig.GetText("U9022");
                return false;
            }

            if (ActConn.IsInterface)
            {
                reason.Text = userConfig.GetText("U9021");
                return false;
            }

            bool hasCommonNetworkAreas = HasCommonNetworkAreas(networkAreas);
            if (!hasCommonNetworkAreas && ActConn.IsCommonService)
            {
                return true;
            }
            else if (hasCommonNetworkAreas && (ActConn.IsCommonService || (!ActConn.IsInterface && !ActConn.IsCommonService)))
            {
                return true;
            }

            reason.Text = userConfig.GetText("U9023");
            return false;
        }

        /// <summary>
        /// Checks if a common service contains only common network areas.
        /// </summary>
        /// <returns></returns>
        private bool ComSvcContainsOnlyCommonNetworkArea()
        {
            List<ModellingNetworkArea> srcAreas = [.. ModellingNetworkAreaWrapper.Resolve(ActConn.SourceAreas)];
            List<ModellingNetworkArea> destAreas = [.. ModellingNetworkAreaWrapper.Resolve(ActConn.DestinationAreas)];

            HashSet<long> srcAreasToDeleteIds = [.. SrcAreasToDelete.Select(d => d.Id)];
            srcAreas.RemoveAll(a => srcAreasToDeleteIds.Contains(a.Id));

            HashSet<long> dstAreasToDeleteIds = [.. DstAreasToDelete.Select(d => d.Id)];
            destAreas.RemoveAll(a => dstAreasToDeleteIds.Contains(a.Id));

            if (HasOnlyCommonNetworkAreas(srcAreas) &&
                HasOnlyCommonNetworkAreas(SrcAreasToAdd) &&
                HasOnlyCommonNetworkAreas(destAreas) &&
                HasOnlyCommonNetworkAreas(DstAreasToAdd))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a common service contains only network areas in the specified direction.
        /// </summary>
        /// <param name="direction">The direction to check (Source or Destination).</param>
        /// <param name="selectedAppRoles">The app roles to be added to the direction.</param>
        /// <returns>True if only network areas exist.</returns>
        public bool ComSvcContainsOnlyNetworkAreasInDirection(Direction direction, List<ModellingAppRole> selectedAppRoles)
        {
            return ComSvcContainsOnlyNetworkAreasInDirectionInternal(direction, [], selectedAppRoles);
        }

        /// <summary>
        /// Checks if a common service contains only network areas in the specified direction.
        /// </summary>
        /// <param name="direction">The direction to check (Source or Destination).</param>
        /// <param name="selectedNetworkAreas">The network areas to be added to the direction.</param>
        /// <returns>True if only network areas exist.</returns>
        public bool ComSvcContainsOnlyNetworkAreasInDirection(Direction direction, List<ModellingNetworkArea> selectedNetworkAreas)
        {
            return ComSvcContainsOnlyNetworkAreasInDirectionInternal(direction, selectedNetworkAreas, []);
        }

        /// <summary>
        /// Internal method that validates whether only network areas (without app roles) exist in the specified direction of a common service.
        /// Combines existing areas/appRoles from the connection with items to add, removes items marked for deletion, and checks if the result contains both types.
        /// </summary>
        /// <param name="direction">The direction to check (Source or Destination).</param>
        /// <param name="initialAreas">Network areas to be considered in addition to existing ones.</param>
        /// <param name="initialRoles">App roles to be considered in addition to existing ones.</param>
        /// <returns>True if only network areas exist.</returns>
        private bool ComSvcContainsOnlyNetworkAreasInDirectionInternal(Direction direction, IEnumerable<ModellingNetworkArea> initialAreas, IEnumerable<ModellingAppRole> initialRoles)
        {
            List<ModellingNetworkArea> areas = [.. initialAreas];
            List<ModellingAppRole> appRoles = [.. initialRoles];

            if (direction == Direction.Source)
            {
                areas.AddRange([.. ModellingNetworkAreaWrapper.Resolve(ActConn.SourceAreas)]);
                areas.AddRange(SrcAreasToAdd);

                HashSet<long> srcAreasToDeleteIds = [.. SrcAreasToDelete.Select(d => d.Id)];
                areas.RemoveAll(a => srcAreasToDeleteIds.Contains(a.Id));

                appRoles.AddRange([.. ModellingAppRoleWrapper.Resolve(ActConn.SourceAppRoles)]);
                appRoles.AddRange(SrcAppRolesToAdd);

                HashSet<long> srcAppRolesToDeleteIds = [.. SrcAppRolesToDelete.Select(d => d.Id)];
                appRoles.RemoveAll(r => srcAppRolesToDeleteIds.Contains(r.Id));
            }
            else if (direction == Direction.Destination)
            {
                areas.AddRange([.. ModellingNetworkAreaWrapper.Resolve(ActConn.DestinationAreas)]);
                areas.AddRange(DstAreasToAdd);

                HashSet<long> dstAreasToDeleteIds = [.. DstAreasToDelete.Select(d => d.Id)];
                areas.RemoveAll(a => dstAreasToDeleteIds.Contains(a.Id));

                appRoles.AddRange([.. ModellingAppRoleWrapper.Resolve(ActConn.DestinationAppRoles)]);
                appRoles.AddRange(DstAppRolesToAdd);

                HashSet<long> dstAppRolesToDeleteIds = [.. DstAppRolesToDelete.Select(d => d.Id)];
                appRoles.RemoveAll(r => dstAppRolesToDeleteIds.Contains(r.Id));
            }
            else
            {
                throw new ArgumentException($"{nameof(direction)} not implemented");
            }

            return !(areas.Count > 0 && appRoles.Count > 0);
        }

        /// <summary>
        /// Checks the opposite direction if it already contains a network area.
        /// </summary>
        private bool IsAreaForbiddenInDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Source => ActConn.DestinationAreas.Count > 0 || DstAreasToAdd.Count > 0,
                Direction.Destination => ActConn.SourceAreas.Count > 0 || SrcAreasToAdd.Count > 0,
                _ => false,
            };
        }

        /// <summary>
        /// Checks the given list of network areas against common network area settings.
        /// </summary>
        /// <param name="networkAreas"></param>
        /// <returns></returns>
        private bool HasCommonNetworkAreas(List<ModellingNetworkArea> networkAreas)
        {
            return networkAreas.Any(a => CommonAreaConfigItems.Any(_ => _.AreaId == a.Id));
        }

        /// <summary>
        /// Determines whether all specified network areas are included in the set of common network areas.
        /// </summary>
        /// <param name="networkAreas">A list of network areas to check for inclusion in the common network areas.</param>
        /// <returns>true if every network area in the list is present in the common network areas configuration; otherwise, false.</returns>
        private bool HasOnlyCommonNetworkAreas(List<ModellingNetworkArea> networkAreas)
        {
            return networkAreas.All(a => CommonAreaConfigItems.Any(_ => _.AreaId == a.Id));
        }


        public void DisplayAppServer(ModellingAppServer? appServer)
        {
            if (appServer != null)
            {
                try
                {
                    AppServerHandler = new(apiConnection, userConfig, Application, appServer, [], false, DisplayMessageInUi) { ReadOnly = true };
                    DisplayAppServerMode = true;
                }
                catch (Exception exception)
                {
                    DisplayMessageInUi(exception, userConfig.GetText("display_app_server"), "", true);
                }
            }
        }

        public void AppServerToSource(List<ModellingAppServer> srcAppServers)
        {
            if (!SrcDropForbidden())
            {
                foreach (var srcAppServer in srcAppServers)
                {
                    if (!srcAppServer.IsDeleted && ActConn.SourceAppServers.FirstOrDefault(w => w.Content.Id == srcAppServer.Id) == null && !SrcAppServerToAdd.Contains(srcAppServer))
                    {
                        SrcAppServerToAdd.Add(srcAppServer);
                    }
                }
                CalcVisibility();
            }
        }

        public void AppServerToDestination(List<ModellingAppServer> dstAppServers)
        {
            if (!DstDropForbidden())
            {
                foreach (var dstAppServer in dstAppServers)
                {
                    if (!dstAppServer.IsDeleted && ActConn.DestinationAppServers.FirstOrDefault(w => w.Content.Id == dstAppServer.Id) == null && !DstAppServerToAdd.Contains(dstAppServer))
                    {
                        DstAppServerToAdd.Add(dstAppServer);
                    }
                }
                CalcVisibility();
            }
        }

        public void SearchNWObject()
        {
            SearchNWObjectMode = true;
        }

        public void RequestRemoveNwGrpObject(ModellingNwGroup? nwGrpObj)
        {
            if (nwGrpObj != null)
            {
                actNwGrpObj = nwGrpObj;
                Message = userConfig.GetText("U9006") + nwGrpObj.Name + "?";
                RemoveNwObjectMode = true;
            }
        }

        public async Task RemoveNwGrpObject()
        {
            try
            {
                if ((await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeSelectedNwGroupObject, new { appId = Application.Id, nwGroupId = actNwGrpObj.Id })).AffectedRows > 0)
                {
                    AvailableSelectedObjects.Remove(AvailableSelectedObjects.FirstOrDefault(x => x.Content.Id == actNwGrpObj.Id) ?? throw new KeyNotFoundException("Did not find object."));
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == actNwGrpObj.GroupType && x.Value == actNwGrpObj.Id));
                    RemoveNwObjectMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("remove_nw_object"), "", true);
            }
        }

        public void AreasToSource(List<ModellingNetworkArea> areas)
        {
            if (!SrcDropForbidden())
            {
                foreach (var area in areas)
                {
                    if (ActConn.SourceAreas.FirstOrDefault(w => w.Content.Id == area.Id) == null && !SrcAreasToAdd.Contains(area) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == area.Id)?.UseInSrc ?? true))
                    {
                        SrcAreasToAdd.Add(area);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void AreasToDestination(List<ModellingNetworkArea> areas)
        {
            if (!DstDropForbidden())
            {
                foreach (var area in areas)
                {
                    if (ActConn.DestinationAreas.FirstOrDefault(w => w.Content.Id == area.Id) == null && !DstAreasToAdd.Contains(area) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == area.Id)?.UseInSrc ?? true))
                    {
                        DstAreasToAdd.Add(area);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToSource(List<ModellingNwGroup> nwGroups)
        {
            if (!SrcDropForbidden())
            {
                foreach (var nwGroup in nwGroups)
                {
                    if (ActConn.SourceOtherGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !SrcNwGroupsToAdd.Contains(nwGroup) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInSrc ?? true))
                    {
                        SrcNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void NwGroupToDestination(List<ModellingNwGroup> nwGroups)
        {
            if (!DstDropForbidden())
            {
                foreach (var nwGroup in nwGroups)
                {
                    if (ActConn.DestinationOtherGroups.FirstOrDefault(w => w.Content.Id == nwGroup.Id) == null && !DstNwGroupsToAdd.Contains(nwGroup) &&
                        (CommonAreaConfigItems.FirstOrDefault(x => x.AreaId == nwGroup.Id)?.UseInDst ?? true))
                    {
                        DstNwGroupsToAdd.Add(nwGroup);
                    }
                    else
                    {
                        DisplayMessageInUi(null, userConfig.GetText(InsertForbidden), userConfig.GetText("E9015"), true);
                    }
                }
                CalcVisibility();
            }
        }

        public void CreateAppRole()
        {
            DisplayAppRoleMode = false;
            AddAppRoleMode = true;
            HandleAppRole(new ModellingAppRole() { });
        }

        public void EditAppRole(ModellingAppRole? appRole)
        {
            if (appRole != null)
            {
                DisplayAppRoleMode = false;
                AddAppRoleMode = false;
                HandleAppRole(appRole);
            }
        }

        public void DisplayAppRole(ModellingAppRole? appRole)
        {
            if (appRole != null)
            {
                DisplayAppRoleMode = true;
                AddAppRoleMode = false;
                HandleAppRole(appRole);
            }
        }

        private void HandleAppRole(ModellingAppRole appRole)
        {
            try
            {
                AppRoleHandler = new ModellingAppRoleHandler(apiConnection, userConfig, Application, AvailableAppRoles,
                    appRole, AvailableAppServers, AvailableNwElems, AddAppRoleMode, DisplayMessageInUi, IsOwner, DisplayAppRoleMode);
                EditAppRoleMode = true;
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("edit_app_role"), "", true);
            }
        }

        public async Task RequestDeleteAppRole(ModellingAppRole? appRole)
        {
            if (appRole != null)
            {
                actAppRole = appRole;
                DeleteAllowed = !await CheckAppRoleIsInUse();
                Message = DeleteAllowed ? userConfig.GetText("U9002") + actAppRole.Name + "?" : userConfig.GetText("E9009") + actAppRole.Name;
                DeleteAppRoleMode = true;
            }
        }

        private async Task<bool> CheckAppRoleIsInUse()
        {
            try
            {
                if (SrcAppRolesToAdd.Any(s => s.Id == actAppRole.Id) || DstAppRolesToAdd.Any(s => s.Id == actAppRole.Id))
                {
                    return true;
                }
                FoundConnectionsForAppRole = [.. ModellingConnectionWrapper.Resolve(await apiConnection.SendQueryAsync<List<ModellingConnectionWrapper>>(ModellingQueries.getConnectionIdsForNwGroup, new { id = actAppRole.Id }))];
                return FoundConnectionsForAppRole.Any(c => !c.Removed);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("is_in_use"), "", true);
                return true;
            }
        }

        public async Task DeleteAppRole()
        {
            try
            {
                bool deleted;
                if (FoundConnectionsForAppRole.Count == 0)
                {
                    deleted = (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.deleteNwGroup, new { id = actAppRole.Id })).AffectedRows > 0;
                }
                else
                {
                    deleted = (await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.setNwGroupDeletedState, new { id = actAppRole.Id, deleted = true })).UpdatedIdLong > 0;
                }
                if (deleted)
                {
                    await LogChange(ModellingTypes.ChangeType.Delete, ModellingTypes.ModObjectType.AppRole, actAppRole.Id,
                        $"Deleted App Role: {actAppRole.Display()}", Application.Id);
                    AvailableAppRoles.Remove(actAppRole);
                    AvailableNwElems.Remove(AvailableNwElems.FirstOrDefault(x => x.Key == (int)ModellingTypes.ModObjectType.AppRole && x.Value == actAppRole.Id));
                    DeleteAppRoleMode = false;
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText("delete_app_role"), "", true);
            }
        }

        public void AppRolesToSource(List<ModellingAppRole> appRoles)
        {
            if (!SrcDropForbidden())
            {
                foreach (var appRole in appRoles.Where(a => ActConn.SourceAppRoles.FirstOrDefault(w => w.Content.Id == a.Id) == null && !SrcAppRolesToAdd.Contains(a)))
                {
                    SrcAppRolesToAdd.Add(appRole);
                }
                CalcVisibility();
            }
        }

        public void AppRolesToDestination(List<ModellingAppRole> appRoles)
        {
            if (!DstDropForbidden())
            {
                foreach (var appRole in appRoles.Where(a => ActConn.DestinationAppRoles.FirstOrDefault(w => w.Content.Id == a.Id) == null && !DstAppRolesToAdd.Contains(a)))
                {
                    DstAppRolesToAdd.Add(appRole);
                }
                CalcVisibility();
            }
        }

        private async Task AddNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles,
            List<ModellingNetworkArea> areas, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                await AddAppServersToConnection(appServers, field);
                await AddAppRolesToConnection(appRoles, field);
                await AddAreasToConnection(areas, field);
                await AddNwGroupsToConnection(nwGroups, field);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task AddAppServersToConnection(List<ModellingAppServer> appServers, ModellingTypes.ConnectionField field)
        {
            foreach (var appServer in appServers)
            {
                var variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addAppServerToConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Added App Server {appServer.Display()} to {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private async Task AddAppRolesToConnection(List<ModellingAppRole> appRoles, ModellingTypes.ConnectionField field)
        {
            foreach (var appRole in appRoles)
            {
                var variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, variables);
                string text = appRole.Id == DummyAppRole.Id
                    ? $"Marked requested Interface: {ActConn.Name} as {field}"
                    : $"Added App Role {appRole.Display()} to {GetTypeAndName()}: {field}";
                await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id, text, Application.Id);
            }
        }

        private async Task AddAreasToConnection(List<ModellingNetworkArea> areas, ModellingTypes.ConnectionField field)
        {
            foreach (var area in areas)
            {
                var variables = new { nwGroupId = area.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Added Area {area.Display()} to {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private async Task AddNwGroupsToConnection(List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            foreach (var nwGroup in nwGroups)
            {
                var variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addNwGroupToConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Added Object {nwGroup.Display()} to {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private async Task RemoveNwObjects(List<ModellingAppServer> appServers, List<ModellingAppRole> appRoles,
            List<ModellingNetworkArea> areas, List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            try
            {
                await RemoveAppServersFromConnection(appServers, field);
                await RemoveAppRolesFromConnection(appRoles, field);
                await RemoveAreasFromConnection(areas, field);
                await RemoveNwGroupsFromConnection(nwGroups, field);
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemoveAppServersFromConnection(List<ModellingAppServer> appServers, ModellingTypes.ConnectionField field)
        {
            foreach (var appServer in appServers)
            {
                var variables = new { nwObjectId = appServer.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeAppServerFromConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Removed App Server {appServer.Display()} from {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private async Task RemoveAppRolesFromConnection(List<ModellingAppRole> appRoles, ModellingTypes.ConnectionField field)
        {
            foreach (var appRole in appRoles)
            {
                var variables = new { nwGroupId = appRole.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, variables);
                string text = appRole.Id == DummyAppRole.Id
                    ? $"Removed {field} marker from requested Interface: {ActConn.Name}"
                    : $"Removed App Role {appRole.Display()} from {GetTypeAndName()}: {field}";
                await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id, text, Application.Id);
            }
        }

        private async Task RemoveAreasFromConnection(List<ModellingNetworkArea> areas, ModellingTypes.ConnectionField field)
        {
            foreach (var area in areas)
            {
                var variables = new { nwGroupId = area.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Removed Area {area.Display()} from {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private async Task RemoveNwGroupsFromConnection(List<ModellingNwGroup> nwGroups, ModellingTypes.ConnectionField field)
        {
            foreach (var nwGroup in nwGroups)
            {
                var variables = new { nwGroupId = nwGroup.Id, connectionId = ActConn.Id, connectionField = (int)field };
                await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.removeNwGroupFromConnection, variables);
                await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                    $"Removed Object {nwGroup.Display()} from {GetTypeAndName()}: {field}", Application.Id);
            }
        }

        private string GetTypeAndName()
        {
            return $"{(ActConn.IsInterface ? kInterface : kConnection)}: {ActConn.Name}";
        }
    }
}
