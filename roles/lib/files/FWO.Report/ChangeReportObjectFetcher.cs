using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;

namespace FWO.Report
{
    /// <summary>
    /// Fetches the details of the objects a change report shows for one management (right sidebar report tab):
    /// first the objects referenced by the reported changes, then the members of the fetched groups, so the member
    /// links in the group details point to objects of the report tab as well.
    /// Objects are fetched by their ids, which identify one object version, so versions replaced or deleted later
    /// in the report period are contained too. The ids are not limited to the reported management, because its rules
    /// may reference objects of its super manager; the row permissions of the api limit them to visible managements.
    /// </summary>
    public sealed class ChangeReportObjectFetcher
    {
        private readonly ApiConnection apiConnection;
        private readonly Dictionary<string, object> importRangeVariables;
        private readonly ObjCategory objects;
        private readonly int elementsPerFetch;
        private readonly ManagementReport fetchedObjects = new();
        private int remainingFetches;

        /// <summary>
        /// Creates a fetcher for one management.
        /// </summary>
        /// <param name="apiConnection">Api connection.</param>
        /// <param name="importRangeVariables">Query variables limiting the group members to a range of imports, may be empty.</param>
        /// <param name="objects">Object category to fetch.</param>
        /// <param name="elementsPerFetch">Maximum number of objects of one category fetched at once.</param>
        /// <param name="maxFetchCycles">Maximum number of fetches for referenced objects and group members together.</param>
        public ChangeReportObjectFetcher(ApiConnection apiConnection, Dictionary<string, object> importRangeVariables, ObjCategory objects,
            int elementsPerFetch, int maxFetchCycles)
        {
            this.apiConnection = apiConnection;
            this.importRangeVariables = importRangeVariables;
            this.objects = objects;
            this.elementsPerFetch = elementsPerFetch;
            remainingFetches = maxFetchCycles;
        }

        /// <summary>
        /// Fetches the reported objects and the members of the fetched groups into the report objects of the management.
        /// </summary>
        /// <param name="managementReport">Management of the change report, receives the fetched objects.</param>
        /// <param name="onPageFetched">Called after every fetched page.</param>
        /// <returns>True if all objects could be fetched within the maximum number of fetches.</returns>
        public async Task<bool> Fetch(ManagementReport managementReport, Func<Task> onPageFetched)
        {
            ReportedChangeObjectIds reportedIds = ReportedChangeObjectIds.Collect(managementReport);
            if (!await FetchByIds(reportedIds, managementReport, onPageFetched))
            {
                return false;
            }
            ReportedChangeObjectIds memberIds = ReportedChangeObjectIds.CollectMissingGroupMembers(fetchedObjects, reportedIds);
            return await FetchByIds(memberIds, managementReport, onPageFetched);
        }

        private async Task<bool> FetchByIds(ReportedChangeObjectIds ids, ManagementReport managementReport, Func<Task> onPageFetched)
        {
            List<long[]> networkObjectIds = [.. ids.GetNetworkObjectIds(objects).Chunk(elementsPerFetch)];
            List<long[]> serviceIds = [.. ids.GetServiceIds(objects).Chunk(elementsPerFetch)];
            List<long[]> userIds = [.. ids.GetUserIds(objects).Chunk(elementsPerFetch)];
            int fetchesNeeded = Math.Max(networkObjectIds.Count, Math.Max(serviceIds.Count, userIds.Count));
            for (int fetch = 0; fetch < fetchesNeeded; fetch++)
            {
                if (remainingFetches-- <= 0)
                {
                    return false;
                }
                List<NetworkObject> networkObjects = await FetchChunk<NetworkObject>(ObjectQueries.getNetworkObjectDetailsByIds, QueryVar.NwObjIds, networkObjectIds, fetch);
                List<NetworkService> services = await FetchChunk<NetworkService>(ObjectQueries.getNetworkServiceDetailsByIds, QueryVar.SvcIds, serviceIds, fetch);
                List<NetworkUser> users = await FetchChunk<NetworkUser>(ObjectQueries.getUserDetailsByIds, QueryVar.UserIds, userIds, fetch);
                fetchedObjects.ReportObjects = [.. fetchedObjects.ReportObjects, .. networkObjects];
                fetchedObjects.ReportServices = [.. fetchedObjects.ReportServices, .. services];
                fetchedObjects.ReportUsers = [.. fetchedObjects.ReportUsers, .. users];
                SetReportObjects(managementReport);
                await onPageFetched();
            }
            return true;
        }

        /// <summary>
        /// Fetches the objects of one chunk of ids.
        /// </summary>
        /// <returns>The fetched objects, empty without a query if there is no such chunk.</returns>
        private async Task<List<TObject>> FetchChunk<TObject>(string query, string idsVariable, List<long[]> idChunks, int chunkIndex)
        {
            if (chunkIndex >= idChunks.Count)
            {
                return [];
            }
            Dictionary<string, object> queryVariables = new(importRangeVariables)
            {
                [idsVariable] = idChunks[chunkIndex]
            };
            return await apiConnection.SendQueryAsync<List<TObject>>(query, queryVariables);
        }

        /// <summary>
        /// Shows the objects fetched so far in the requested categories, ordered by name like the other report tabs.
        /// </summary>
        private void SetReportObjects(ManagementReport managementReport)
        {
            if (objects == ObjCategory.all || objects == ObjCategory.nobj)
            {
                managementReport.ReportObjects = [.. fetchedObjects.ReportObjects.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Id)];
            }
            if (objects == ObjCategory.all || objects == ObjCategory.nsrv)
            {
                managementReport.ReportServices = [.. fetchedObjects.ReportServices.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id)];
            }
            if (objects == ObjCategory.all || objects == ObjCategory.user)
            {
                managementReport.ReportUsers = [.. fetchedObjects.ReportUsers.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Id)];
            }
        }
    }
}
