using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Logging;
using RestSharp;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Creates object tasks (groups and single objects) per target management of the device specific settings and
    /// hosts the action creating the missing implementation tasks of a ticket.
    /// </summary>
    public partial class WfHandler
    {
        private const string kImplTaskTargetsLogTitle = "Implementation Tasks";

        /// <summary>
        /// Upper bound of the pages requested from the middleware for one target lookup, so a middleware that keeps
        /// announcing further pages cannot keep the ui in an endless loop.
        /// </summary>
        private const int kMaxImplTaskTargetPages = 100;

        /// <summary>
        /// Stands for the request management of an object task without management: its targets are taken from every
        /// hierarchy of managements.
        /// </summary>
        private const int kAllManagements = 0;

        /// <summary>
        /// Target managements resolved in the current creation run, by request management and kind of ordered
        /// objects (true for services).
        /// </summary>
        private readonly Dictionary<(int ManagementId, ObjectTaskKind Kind), List<Management>> objectTargetCache = [];

        /// <summary>
        /// Creates the implementation tasks still missing for every open request task of a ticket, on the gateways and
        /// managements whose auto plan device specific settings allow it. This state action is the only place where
        /// implementation tasks are created automatically. Existing implementation tasks are kept; a gateway or
        /// management that already has one is skipped.
        /// </summary>
        /// <param name="ticket">Ticket whose request tasks get their implementation tasks.</param>
        /// <param name="scopeTask">Request task the action runs for in the request task scope: only this task gets its
        /// implementation tasks, a bundled task together with its bundle once every member of the bundle has reached
        /// the state of this task. Null in the ticket scope, where every open request task gets them.</param>
        /// <returns>The number of created implementation tasks.</returns>
        public async Task<int> CreateMissingImplTasksForTicket(WfTicket ticket, WfReqTask? scopeTask = null)
        {
            SetTicketEnv(ticket);
            if (scopeTask != null)
            {
                SyncActTicketFromReqTask(scopeTask);
            }
            int previousCount = CountImplTasks(ActTicket);
            List<WfReqTask> openTasks = [.. ActTicket.Tasks.Where(RequestTaskIsOpen)];
            if (scopeTask != null)
            {
                openTasks = TasksOfRequestTaskScope(openTasks, scopeTask);
            }
            foreach (WfReqTask task in await RequestTasksForInitialImplCreation(openTasks, includeExistingImplementationTasks: true))
            {
                await AutoCreateImplTasks(task);
                SyncActTicketFromReqTask(task);
            }
            int createdCount = Math.Max(0, CountImplTasks(ActTicket) - previousCount);
            Log.WriteInfo(kImplTaskTargetsLogTitle, $"Created {createdCount} implementation tasks for ticket {ActTicket.Id}, " +
                $"considering {CountBundles(ActTicket)} task bundles.");
            return createdCount;
        }

        /// <summary>
        /// Returns the open request tasks the action handles in the request task scope: the task itself, or with
        /// bundling its whole bundle, but only once no member of the bundle is in a lower state than the task. The
        /// member reaching the state last then creates the implementation tasks of the bundle.
        /// </summary>
        private List<WfReqTask> TasksOfRequestTaskScope(List<WfReqTask> openTasks, WfReqTask scopeTask)
        {
            string bundleId = userConfig.ReqConsiderBundling ? scopeTask.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) : "";
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                return [.. openTasks.Where(task => task.Id == scopeTask.Id)];
            }
            List<WfReqTask> bundleMembers = [.. ActTicket.Tasks.Where(task => task.GetAddInfoValue(AdditionalInfoKeys.FlowBundleId) == bundleId)];
            if (bundleMembers.Any(member => member.StateId < scopeTask.StateId))
            {
                Log.WriteInfo(kImplTaskTargetsLogTitle, $"Implementation tasks of request task {scopeTask.Id} wait for bundle {bundleId}: " +
                    "not every bundled task has reached its state yet.");
                return [];
            }
            return [.. openTasks.Where(task => bundleMembers.Any(member => member.Id == task.Id))];
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
        /// The targets come from the hierarchy of the request management, without request management (e.g. ordered
        /// through the REST interface) from every hierarchy of managements. In the ui, which resolves the targets
        /// through the middleware endpoint for one management, a task without request management keeps the single
        /// task without management of the previous behaviour.
        /// </summary>
        /// <param name="reqTask">Object task to create the implementation tasks for; only target managements whose
        /// auto plan device specific settings allow it and whose implementation mode is Automatic get one.</param>
        private async Task AutoCreateObjectImplTasks(WfReqTask reqTask)
        {
            int managementId = reqTask.ManagementId is int requestManagementId && requestManagementId > 0 ? requestManagementId : kAllManagements;
            List<Management>? targets = await GetObjectTargetManagements(reqTask, managementId);
            if (targets == null)
            {
                await AutoCreateSingleImplTask(reqTask);
                return;
            }
            if (targets.Count == 0)
            {
                // ignore is an explicit setting, so no fallback task is created
                Log.WriteWarning(kImplTaskTargetsLogTitle, $"No implementation task created for request task {reqTask.Id}: " +
                    $"every target management of {DescribeRequestManagement(managementId)} is set to ignore objects of this kind.");
                return;
            }
            targets = await FilterAutoPlannedManagements(targets);
            targets = await FilterAutomaticObjectTargets(reqTask, targets);
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
        /// dropped, so changed device specific settings or managements take effect.
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
        /// <param name="reqTask">The object task.</param>
        /// <param name="managementId">Management of the request task, or <see cref="kAllManagements"/>.</param>
        /// <returns>The target managements; null for a task without request management whose targets cannot be
        /// resolved here, so that the single task without management of the previous behaviour is created.</returns>
        private async Task<List<Management>?> GetObjectTargetManagements(WfReqTask reqTask, int managementId)
        {
            ObjectTaskKind kind = WfObjectTaskHelper.GetCreationKind(reqTask);
            if (objectTargetCache.TryGetValue((managementId, kind), out List<Management>? cachedTargets))
            {
                return cachedTargets;
            }
            try
            {
                List<Management>? targets = await ResolveObjectTargetManagements(managementId, kind);
                if (targets != null)
                {
                    objectTargetCache[(managementId, kind)] = targets;
                    return targets;
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(kImplTaskTargetsLogTitle, $"Could not resolve the target managements of request task {reqTask.Id}, " +
                    $"using {DescribeRequestManagement(managementId)}.", exception);
            }
            return managementId == kAllManagements ? null : [new() { Id = managementId }];
        }

        private static string DescribeRequestManagement(int managementId)
        {
            return managementId == kAllManagements ? "all managements" : $"management {managementId}";
        }

        /// <summary>
        /// Checks before saving whether an object task with a management gets at least one implementation task, i.e.
        /// whether the device specific settings allow creating its kind of objects there. Only time objects can be set
        /// to ignore, so other tasks and tasks without management are not checked.
        /// </summary>
        /// <returns>True or false; null when the targets cannot be resolved, so that the caller does not block the
        /// request on an unavailable middleware.</returns>
        public async Task<bool?> HasObjectTargets(WfReqTask reqTask)
        {
            if (reqTask.ManagementId is not int managementId || managementId <= 0
                || WfObjectTaskHelper.GetCreationKind(reqTask) != ObjectTaskKind.Time)
            {
                return true;
            }
            try
            {
                List<Management>? targets = await ResolveObjectTargetManagements(managementId, ObjectTaskKind.Time);
                return targets == null ? null : targets.Count > 0;
            }
            catch (Exception exception)
            {
                Log.WriteError(kImplTaskTargetsLogTitle, $"Could not check the target managements of management {managementId}.", exception);
                return null;
            }
        }

        /// <summary>
        /// Resolves the target managements with the rights of the middleware server: in the ui through the
        /// middleware, which sees every sub-management and the device specific settings, in the middleware directly.
        /// </summary>
        /// <param name="managementId">Management of the request task, or <see cref="kAllManagements"/>, which only
        /// the middleware itself resolves: the middleware endpoint answers for one management.</param>
        /// <param name="kind">Kind of the created objects.</param>
        /// <returns>The target managements, or null when they cannot be resolved here.</returns>
        private async Task<List<Management>?> ResolveObjectTargetManagements(int managementId, ObjectTaskKind kind)
        {
            if (MiddlewareClient != null)
            {
                return managementId == kAllManagements ? null : await GetObjectTargetsFromMiddleware(managementId, kind);
            }
            if (apiConnection == null)
            {
                return null;
            }
            ImplTaskManagementResolver resolver = new(apiConnection);
            return managementId == kAllManagements
                ? await resolver.ResolveObjectTargetsOfAllManagements(kind)
                : await resolver.ResolveObjectTargets(managementId, kind);
        }

        /// <summary>
        /// Reads every page of the target managements from the middleware.
        /// </summary>
        /// <exception cref="InvalidOperationException">The middleware fails or announces more than
        /// <see cref="kMaxImplTaskTargetPages"/> pages.</exception>
        private async Task<List<Management>> GetObjectTargetsFromMiddleware(int managementId, ObjectTaskKind kind)
        {
            List<Management> targets = [];
            for (int page = 0; page < kMaxImplTaskTargetPages; page++)
            {
                RestResponse<ImplTaskTargetResult> response = await MiddlewareClient!.GetImplTaskTargets(new ImplTaskTargetParameters
                {
                    ManagementId = managementId,
                    IsServiceObject = kind == ObjectTaskKind.Service,
                    IsTimeObject = kind == ObjectTaskKind.Time,
                    Options = new ImplTaskTargetPaging { Offset = targets.Count }
                });
                if (!response.IsSuccessful || response.Data == null)
                {
                    throw new InvalidOperationException($"Middleware answered {(int)response.StatusCode} {response.StatusDescription}. " +
                        (response.ErrorMessage ?? response.Content ?? ""));
                }
                targets.AddRange(response.Data.Targets);
                if (response.Data.Targets.Count == 0 || !HasMoreTargets(response))
                {
                    return targets;
                }
            }
            throw new InvalidOperationException($"Middleware announced more than {kMaxImplTaskTargetPages} pages of target managements.");
        }

        private static bool HasMoreTargets(RestResponse response)
        {
            string? hasMore = response.Headers?
                .FirstOrDefault(header => string.Equals(header.Name, ImplTaskTargetPaging.kHasMoreHeader, StringComparison.OrdinalIgnoreCase))?
                .Value?.ToString();
            return string.Equals(hasMore, "true", StringComparison.OrdinalIgnoreCase);
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
