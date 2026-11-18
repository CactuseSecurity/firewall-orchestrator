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
    /// in the report period are contained too.
    /// </summary>
    public sealed class ChangeReportObjectFetcher
    {
        private readonly ApiConnection apiConnection;
        private readonly Dictionary<string, object> baseQueryVariables;
        private readonly ObjCategory objects;
        private readonly int elementsPerFetch;
        private readonly ManagementReport fetchedObjects = new();
        private int remainingFetches;

        /// <summary>
        /// Creates a fetcher for one management.
        /// </summary>
        /// <param name="apiConnection">Api connection.</param>
        /// <param name="queryVariables">Query variables, must contain management id, limit and offset.</param>
        /// <param name="objects">Object category to fetch.</param>
        /// <param name="maxFetchCycles">Maximum number of fetches for referenced objects and group members together.</param>
        public ChangeReportObjectFetcher(ApiConnection apiConnection, Dictionary<string, object> queryVariables, ObjCategory objects, int maxFetchCycles)
        {
            this.apiConnection = apiConnection;
            baseQueryVariables = queryVariables;
            this.objects = objects;
            elementsPerFetch = (int)queryVariables[QueryVar.Limit];
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
            Dictionary<string, object> queryVariables = new(baseQueryVariables)
            {
                [QueryVar.NwObjIds] = ids.GetNetworkObjectIds(objects),
                [QueryVar.SvcIds] = ids.GetServiceIds(objects),
                [QueryVar.UserIds] = ids.GetUserIds(objects)
            };
            bool keepFetching = HasIdsToFetch(queryVariables);
            while (keepFetching)
            {
                if (remainingFetches-- <= 0)
                {
                    return false;
                }
                ManagementReport? page = (await apiConnection.SendQueryAsync<List<ManagementReport>>(ObjectQueries.getObjectDetailsByIds, queryVariables)).FirstOrDefault();
                if (page == null)
                {
                    break;
                }
                keepFetching = AddPage(page);
                SetReportObjects(managementReport);
                queryVariables[QueryVar.Offset] = (int)queryVariables[QueryVar.Offset] + elementsPerFetch;
                await onPageFetched();
            }
            return true;
        }

        private static bool HasIdsToFetch(Dictionary<string, object> queryVariables)
        {
            return ((List<long>)queryVariables[QueryVar.NwObjIds]).Count > 0
                || ((List<long>)queryVariables[QueryVar.SvcIds]).Count > 0
                || ((List<long>)queryVariables[QueryVar.UserIds]).Count > 0;
        }

        /// <summary>
        /// Adds a fetched page to the objects fetched so far.
        /// </summary>
        /// <returns>True if another page has to be fetched.</returns>
        private bool AddPage(ManagementReport page)
        {
            fetchedObjects.ReportObjects = [.. fetchedObjects.ReportObjects, .. page.ReportObjects];
            fetchedObjects.ReportServices = [.. fetchedObjects.ReportServices, .. page.ReportServices];
            fetchedObjects.ReportUsers = [.. fetchedObjects.ReportUsers, .. page.ReportUsers];
            return page.ReportObjects.Length >= elementsPerFetch
                || page.ReportServices.Length >= elementsPerFetch
                || page.ReportUsers.Length >= elementsPerFetch;
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
