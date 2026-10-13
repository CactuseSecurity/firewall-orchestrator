using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using RestSharp;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Creates object tasks (groups and single objects) per target management of the provisioning settings and
    /// hosts the action creating the missing implementation tasks of a ticket.
    /// </summary>
    public partial class WfHandler
    {
        private const string kImplTaskTargetsLogTitle = "Implementation Tasks";

        /// <summary>
        /// Target managements resolved in the current creation run, by request management and kind of ordered
        /// objects (true for services).
        /// </summary>
        private readonly Dictionary<(int ManagementId, bool IsServiceObject), List<Management>> objectTargetCache = [];

        /// <summary>
        /// Creates the implementation tasks still missing for every open request task of a ticket. Existing
        /// implementation tasks are kept; a gateway or management that already has one is skipped.
        /// </summary>
        /// <param name="ticket">Ticket whose request tasks get their implementation tasks.</param>
        /// <returns>The number of created implementation tasks.</returns>
        public async Task<int> CreateMissingImplTasksForTicket(WfTicket ticket)
        {
            SetTicketEnv(ticket);
            int previousCount = CountImplTasks(ActTicket);
            List<WfReqTask> openTasks = [.. ActTicket.Tasks.Where(RequestTaskIsOpen)];
            foreach (WfReqTask task in await RequestTasksForInitialImplCreation(openTasks))
            {
                await AutoCreateImplTasks(task);
                SyncActTicketFromReqTask(task);
            }
            int createdCount = Math.Max(0, CountImplTasks(ActTicket) - previousCount);
            Log.WriteInfo(kImplTaskTargetsLogTitle, $"Created {createdCount} implementation tasks for ticket {ActTicket.Id}, " +
                $"considering {CountBundles(ActTicket)} task bundles.");
            return createdCount;
        }

        private bool RequestTaskIsOpen(WfReqTask reqTask)
        {
            return stateMatrixDict.Matrices.TryGetValue(reqTask.TaskType, out StateMatrix? matrix)
                && reqTask.StateId < matrix.MinTicketCompleted;
        }

        private static int CountImplTasks(WfTicket ticket)
        {
            return ticket.Tasks.Sum(task => task.ImplementationTasks.Count);
        }

        private int CountBundles(WfTicket ticket)
        {
            if (!userConfig.ReqConsiderBundling)
            {
                return 0;
            }
            return ticket.Tasks.Select(task => task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId))
                .Where(bundleId => !string.IsNullOrWhiteSpace(bundleId)).Distinct().Count();
        }

        /// <summary>
        /// Creates one implementation task per target management of an object task, i.e. a group task
        /// (group_create, group_modify, group_delete) or a single object task (object_create, object_modify).
        /// Without a request management the single task without management of the previous behaviour is created.
        /// </summary>
        private async Task AutoCreateObjectImplTasks(WfReqTask reqTask)
        {
            if (reqTask.ManagementId is not int managementId || managementId <= 0)
            {
                if (reqTask.ImplementationTasks.Count == 0)
                {
                    await CreateGenericImplTask(reqTask);
                }
                return;
            }

            List<Management> targets = await GetObjectTargetManagements(reqTask, managementId);
            List<Management> missingTargets = [.. targets.Where(target =>
                reqTask.ImplementationTasks.All(implTask => (implTask.ManagementId ?? reqTask.ManagementId) != target.Id))];
            bool adaptTitle = targets.Count > 1 || targets.Any(target => target.Id != managementId);
            WfTicket? storedTicket = await LoadTicketForImplTaskCreation(reqTask, missingTargets.Count);
            foreach (Management target in missingTargets)
            {
                await CreateObjectImplTask(reqTask, target, adaptTitle, storedTicket);
            }
        }

        /// <summary>
        /// Creates the implementation task of an object task on one target management.
        /// </summary>
        private async Task CreateObjectImplTask(WfReqTask reqTask, Management target, bool adaptTitle, WfTicket? previousTicket)
        {
            WfImplTask newImplTask = new(reqTask)
            {
                TaskNumber = reqTask.HighestImplTaskNumber() + 1,
                StateId = reqTask.StateId,
                ManagementId = target.Id
            };
            if (adaptTitle && !string.IsNullOrWhiteSpace(target.Name))
            {
                newImplTask.Title += ": " + target.Name;
            }
            if (dbAcc != null)
            {
                newImplTask.Id = await dbAcc.AddImplTaskToDb(newImplTask, previousTicket);
            }
            reqTask.ImplementationTasks.Add(newImplTask);
        }

        /// <summary>
        /// Starts a new implementation task creation run: target managements resolved in an earlier run are
        /// dropped, so changed provisioning settings or managements take effect.
        /// </summary>
        /// <remarks>
        /// Cleared at the start of a run rather than at its end, so a run aborted by an exception leaves no stale
        /// entries behind. A nested run (e.g. a create implementation tasks action triggered while a ticket state
        /// is synchronized) only causes cache misses for the outer run, never wrong targets.
        /// </remarks>
        private void ResetObjectTargetCache()
        {
            objectTargetCache.Clear();
        }

        /// <summary>
        /// Resolves the target managements of an object task once per run and kind of ordered objects. If that
        /// fails, the request management itself is the only target, as before.
        /// </summary>
        private async Task<List<Management>> GetObjectTargetManagements(WfReqTask reqTask, int managementId)
        {
            bool isServiceObject = WfObjectTaskHelper.OrdersServiceObject(reqTask);
            if (objectTargetCache.TryGetValue((managementId, isServiceObject), out List<Management>? cachedTargets))
            {
                return cachedTargets;
            }
            try
            {
                List<Management>? targets = await ResolveObjectTargetManagements(managementId, isServiceObject);
                if (targets != null)
                {
                    objectTargetCache[(managementId, isServiceObject)] = targets;
                    return targets;
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(kImplTaskTargetsLogTitle, $"Could not resolve the target managements of request task {reqTask.Id}, " +
                    $"using management {managementId}.", exception);
            }
            return [new() { Id = managementId }];
        }

        /// <summary>
        /// Resolves the target managements with the rights of the middleware server: in the ui through the
        /// middleware, which sees every sub-management and the provisioning settings, in the middleware directly.
        /// </summary>
        /// <returns>The target managements, or null when they cannot be resolved here.</returns>
        private async Task<List<Management>?> ResolveObjectTargetManagements(int managementId, bool isServiceObject)
        {
            if (MiddlewareClient != null)
            {
                RestResponse<ImplTaskTargetResult> response = await MiddlewareClient.GetImplTaskTargets(
                    new ImplTaskTargetParameters { ManagementId = managementId, IsServiceObject = isServiceObject });
                if (!response.IsSuccessful || response.Data == null)
                {
                    throw new InvalidOperationException($"Middleware answered {(int)response.StatusCode} {response.StatusDescription}. " +
                        (response.ErrorMessage ?? response.Content ?? ""));
                }
                return response.Data.Targets;
            }
            if (apiConnection != null)
            {
                return await new ImplTaskManagementResolver(apiConnection).ResolveObjectTargets(managementId, isServiceObject);
            }
            return null;
        }

        /// <summary>
        /// Returns the name of the management an implementation task is implemented on, as shown in the
        /// implementation task views. The management is taken from the task itself, else from its gateway, else from
        /// the request task.
        /// </summary>
        /// <param name="implTask">The implementation task.</param>
        /// <param name="reqTaskManagementId">Management of the request task, used when the implementation task has
        /// neither its own management nor a gateway.</param>
        /// <returns>The management name; a placeholder when the management or gateway is not visible to the user;
        /// empty when no management is known.</returns>
        public string GetImplTaskManagementName(WfImplTask implTask, int? reqTaskManagementId)
        {
            int? managementId = implTask.ManagementId;
            if (managementId == null && implTask.DeviceId is int deviceId && deviceId > 0)
            {
                Device? device = Devices.FirstOrDefault(candidate => candidate.Id == deviceId);
                if (device == null)
                {
                    return userConfig.GetText("not_visible");
                }
                managementId = device.Management.Id;
            }
            managementId ??= reqTaskManagementId;
            if (managementId is not int visibleManagementId || visibleManagementId <= 0)
            {
                return "";
            }
            return Managements.FirstOrDefault(management => management.Id == visibleManagementId)?.Name
                ?? userConfig.GetText("not_visible");
        }

        /// <summary>
        /// Loads the gateways if they are not loaded yet, which is the case when actions run in the middleware.
        /// </summary>
        private async Task EnsureDevicesLoaded()
        {
            if (Devices.Count > 0 || apiConnection == null)
            {
                return;
            }
            try
            {
                Devices = await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails);
            }
            catch (Exception exception)
            {
                Log.WriteError(kImplTaskTargetsLogTitle, "Could not load the gateways for the implementation tasks.", exception);
            }
        }
    }
}
