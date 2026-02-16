using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;

namespace FWO.Services.Modelling
{
    public partial class ModellingConnectionHandler
    {
        public List<FwoOwner> PermittedOwnersToAdd { get; set; } = [];
        public List<FwoOwner> PermittedOwnersToDelete { get; set; } = [];

        private async Task EnsureUsingAppsPermittedIfRestricted()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString() ||
                ActConnOrig.InterfacePermission == InterfacePermissions.Restricted.ToString())
            {
                return;
            }

            HashSet<int> existingIds = ActConn.PermittedOwners.Select(o => o.Id).ToHashSet();
            HashSet<int> pendingIds = PermittedOwnersToAdd.Select(o => o.Id).ToHashSet();

            List<ModellingConnection> usingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getInterfaceUsers, new { id = ActConn.Id });
            IEnumerable<int> usingAppIds = usingConnections.Where(c => c.AppId != null).Select(c => c.AppId!.Value).Distinct();

            foreach (int appId in usingAppIds)
            {
                if (existingIds.Contains(appId) || pendingIds.Contains(appId))
                {
                    continue;
                }

                FwoOwner? owner = AllApps.FirstOrDefault(o => o.Id == appId);
                if (owner != null)
                {
                    PermittedOwnersToAdd.Add(owner);
                    ActConn.PermittedOwners.Add(owner);
                }
            }
        }

        private async Task ApplyPermittedOwnersOnInsert()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString())
            {
                PermittedOwnersToAdd.Clear();
                PermittedOwnersToDelete.Clear();
                return;
            }
            await AddPermittedOwners(ActConn, PermittedOwnersToAdd);
        }

        private async Task ApplyPermittedOwnersOnUpdate()
        {
            if (!ActConn.IsInterface || ActConn.InterfacePermission != InterfacePermissions.Restricted.ToString())
            {
                await RemoveAllPermittedOwners();
                ActConn.PermittedOwners.Clear();
                PermittedOwnersToAdd.Clear();
                PermittedOwnersToDelete.Clear();
                return;
            }
            await RemovePermittedOwners(PermittedOwnersToDelete);
            await AddPermittedOwners(ActConn, PermittedOwnersToAdd);
        }

        private void SyncPermittedOwnersChanges()
        {
            foreach (var owner in PermittedOwnersToDelete)
            {
                ActConn.PermittedOwners.RemoveAll(o => o.Id == owner.Id);
            }
            foreach (var owner in PermittedOwnersToAdd)
            {
                if (!ActConn.PermittedOwners.Any(o => o.Id == owner.Id))
                {
                    ActConn.PermittedOwners.Add(owner);
                }
            }
        }

        public async Task AddPermittedOwnersIfMissing(ModellingConnection? proposedInterface, List<FwoOwner> owners)
        {
            if (proposedInterface == null)
            {
                return;
            }

            await AddPermittedOwners(proposedInterface, [.. owners.Where(own => !proposedInterface.PermittedOwners.Any(o => o.Id == own.Id))]);
        }

        private async Task AddPermittedOwners(ModellingConnection conn, List<FwoOwner> owners)
        {
            try
            {
                foreach (int ownerId in owners.Select(o => o.Id).Distinct().Where(id => id > 0))
                {
                    var variables = new { connectionId = conn.Id, appId = ownerId };
                    await apiConnection.SendQueryAsync<ReturnId>(ModellingQueries.addPermittedOwner, variables);
                    FwoOwner? owner = owners.FirstOrDefault(o => o.Id == ownerId);
                    string ownerLabel = owner != null ? owner.Display(userConfig.GetText("common_service")) : ownerId.ToString();
                    await LogChange(ModellingTypes.ChangeType.Assign, ModellingTypes.ModObjectType.Connection, conn.Id,
                        $"Added permitted owner {ownerLabel} to {kInterface}: {conn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemovePermittedOwners(List<FwoOwner> owners)
        {
            try
            {
                foreach (int ownerId in owners.Select(o => o.Id).Distinct().Where(id => id > 0))
                {
                    var variables = new { connectionId = ActConn.Id, appId = ownerId };
                    await apiConnection.SendQueryAsync<FwoOwner>(ModellingQueries.deletePermittedOwner, variables);
                    FwoOwner? owner = owners.FirstOrDefault(o => o.Id == ownerId);
                    string ownerLabel = owner != null ? owner.Display(userConfig.GetText("common_service")) : ownerId.ToString();
                    await LogChange(ModellingTypes.ChangeType.Unassign, ModellingTypes.ModObjectType.Connection, ActConn.Id,
                        $"Removed permitted owner {ownerLabel} from {kInterface}: {ActConn.Name}", Application.Id);
                }
            }
            catch (Exception exception)
            {
                DisplayMessageInUi(exception, userConfig.GetText(EditConnection), "", true);
            }
        }

        private async Task RemoveAllPermittedOwners()
        {
            List<FwoOwner> existing = await apiConnection.SendQueryAsync<List<FwoOwner>>(
                ModellingQueries.getPermittedOwnersForConnection,
                new { connectionId = ActConn.Id });
            await RemovePermittedOwners(existing);
        }
    }
}
