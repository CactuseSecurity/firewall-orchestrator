using FWO.Basics;
using FWO.Data;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

/// <summary>
/// Builds validated, idempotent request-element replacements without mutating or persisting a ticket.
/// </summary>
public static class EffectiveRequestStateMaterializer
{
    /// <summary>
    /// Creates the effective-state replacement plan for the selected request tasks.
    /// </summary>
    /// <param name="ticket">The persisted ticket containing all exact references.</param>
    /// <param name="targetTaskIds">The request tasks about to become operational.</param>
    /// <returns>A validated plan containing only changed element replacements.</returns>
    public static EffectiveRequestStateMaterializationPlan CreatePlan(WfTicket ticket,
        IReadOnlyCollection<long> targetTaskIds)
    {
        return new Planner(ticket).CreatePlan(targetTaskIds);
    }

    private sealed class Planner
    {
        private readonly WfTicket ticket;
        private readonly Dictionary<long, WfReqTask> tasksById;
        private readonly List<WfReqElement> changedElements = [];
        private readonly List<long> changedTaskIds = [];
        private readonly HashSet<long> changedTaskIdSet = [];
        private readonly HashSet<long> processedGroupTaskIds = [];

        /// <summary>
        /// Creates a planner and validates the persisted task-ID index.
        /// </summary>
        internal Planner(WfTicket ticket)
        {
            this.ticket = ticket;
            tasksById = BuildTaskIndex(ticket.Tasks);
        }

        /// <summary>
        /// Builds the materialization plan for the requested task IDs.
        /// </summary>
        internal EffectiveRequestStateMaterializationPlan CreatePlan(IReadOnlyCollection<long> targetTaskIds)
        {
            if (targetTaskIds.Count != targetTaskIds.Distinct().Count())
            {
                throw Failure(EffectiveRequestStateFailureCategory.DuplicateTask);
            }

            List<WfReqTask> targetTasks = targetTaskIds.Select(GetTargetTask)
                .OrderBy(task => task.TaskNumber)
                .ThenBy(task => task.Id)
                .ToList();
            foreach (WfReqTask targetTask in targetTasks)
            {
                ValidateTicketMembership(targetTask);
                ProcessTargetTask(targetTask);
            }
            return new EffectiveRequestStateMaterializationPlan(changedElements, changedTaskIds);
        }

        /// <summary>
        /// Indexes positive persisted task IDs and rejects duplicates.
        /// </summary>
        private static Dictionary<long, WfReqTask> BuildTaskIndex(IEnumerable<WfReqTask> tasks)
        {
            Dictionary<long, WfReqTask> result = [];
            foreach (WfReqTask task in tasks.Where(task => task.Id > 0))
            {
                if (!result.TryAdd(task.Id, task))
                {
                    throw Failure(EffectiveRequestStateFailureCategory.DuplicateTask);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a selected persisted task or fails for a missing/non-positive ID.
        /// </summary>
        private WfReqTask GetTargetTask(long targetTaskId)
        {
            if (targetTaskId <= 0 || !tasksById.TryGetValue(targetTaskId, out WfReqTask? task))
            {
                throw Failure(EffectiveRequestStateFailureCategory.InvalidTargetTask);
            }
            return task;
        }

        /// <summary>
        /// Dispatches supported reference-bearing target tasks.
        /// </summary>
        private void ProcessTargetTask(WfReqTask task)
        {
            if (IsGroupTask(task))
            {
                ProcessGroupTask(task);
            }
            else if (task.TaskType == WfTaskType.access.ToString())
            {
                ProcessAccessTask(task);
            }
        }

        /// <summary>
        /// Materializes exact object references and reachable whole-group references in an access task.
        /// </summary>
        private void ProcessAccessTask(WfReqTask accessTask)
        {
            foreach (WfReqElement element in accessTask.Elements.Where(element => element.ObjectTaskId is > 0))
            {
                WfReqTask referencedTask = GetReferencedTask(element.ObjectTaskId.GetValueOrDefault());
                ValidateReferenceBoundary(accessTask, referencedTask);
                if (IsGroupTask(referencedTask))
                {
                    ProcessGroupTask(referencedTask);
                }
                else
                {
                    MaterializeObjectReference(accessTask, element, referencedTask);
                }
            }
        }

        /// <summary>
        /// Materializes group delta/member rows once, leaving stable catalog rows unchanged.
        /// </summary>
        private void ProcessGroupTask(WfReqTask groupTask)
        {
            if (!processedGroupTaskIds.Add(groupTask.Id))
            {
                return;
            }

            foreach (WfReqElement member in groupTask.Elements)
            {
                if (member.ObjectTaskId is > 0)
                {
                    WfReqTask referencedTask = GetReferencedTask(member.ObjectTaskId.GetValueOrDefault());
                    if (IsGroupTask(referencedTask))
                    {
                        throw Failure(EffectiveRequestStateFailureCategory.NestedGroupReference);
                    }
                    ValidateReferenceBoundary(groupTask, referencedTask);
                    MaterializeObjectReference(groupTask, member, referencedTask);
                }
                else if (!HasStableCatalogIdentity(member))
                {
                    throw Failure(EffectiveRequestStateFailureCategory.UnidentifiableReference);
                }
            }
        }

        /// <summary>
        /// Returns an exact referenced task from the ticket index.
        /// </summary>
        private WfReqTask GetReferencedTask(long referenceTaskId)
        {
            if (!tasksById.TryGetValue(referenceTaskId, out WfReqTask? referencedTask))
            {
                throw Failure(EffectiveRequestStateFailureCategory.MissingReference);
            }
            return referencedTask;
        }

        /// <summary>
        /// Validates ticket membership and that a referenced task is ordered before its consumer.
        /// </summary>
        private void ValidateReferenceBoundary(WfReqTask consumer, WfReqTask referencedTask)
        {
            ValidateTicketMembership(consumer);
            ValidateTicketMembership(referencedTask);
            if (referencedTask.TaskNumber > consumer.TaskNumber
                || (referencedTask.TaskNumber == consumer.TaskNumber && referencedTask.Id >= consumer.Id))
            {
                throw Failure(EffectiveRequestStateFailureCategory.LaterReference);
            }
        }

        /// <summary>
        /// Rejects a task whose persisted parent differs from the supplied ticket.
        /// </summary>
        private void ValidateTicketMembership(WfReqTask task)
        {
            if (task.TicketId != ticket.Id)
            {
                throw Failure(EffectiveRequestStateFailureCategory.CrossTicketReference);
            }
        }

        /// <summary>
        /// Clones and updates one object-reference snapshot when its effective values changed.
        /// </summary>
        private void MaterializeObjectReference(WfReqTask consumer, WfReqElement target, WfReqTask referencedTask)
        {
            if (!WfObjectTaskHelper.IsObjectTask(referencedTask.TaskType))
            {
                throw Failure(EffectiveRequestStateFailureCategory.WrongReferenceType);
            }
            if (!WfObjectTaskHelper.HasValidElementStructure(referencedTask))
            {
                throw Failure(EffectiveRequestStateFailureCategory.InvalidRequestedElement);
            }

            WfReqElement source = WfObjectTaskHelper.GetRequestedElement(referencedTask)
                ?? throw Failure(EffectiveRequestStateFailureCategory.InvalidRequestedElement);
            if (!FieldsMatch(target.Field, source.Field))
            {
                throw Failure(EffectiveRequestStateFailureCategory.WrongReferenceType);
            }

            WfReqElement replacement = new(target);
            WfObjectTaskHelper.CopyRequestedStateSnapshot(replacement, source);
            if (!SnapshotValuesEqual(target, replacement))
            {
                changedElements.Add(replacement);
                if (changedTaskIdSet.Add(consumer.Id))
                {
                    changedTaskIds.Add(consumer.Id);
                }
            }
        }

        /// <summary>
        /// Determines whether network source/destination aliases or exact service/time fields match.
        /// </summary>
        private static bool FieldsMatch(string targetField, string sourceField)
        {
            if (targetField == sourceField)
            {
                return true;
            }
            return targetField is nameof(ElemFieldType.source) or nameof(ElemFieldType.destination)
                && sourceField is nameof(ElemFieldType.source) or nameof(ElemFieldType.destination);
        }

        /// <summary>
        /// Determines whether a group member has a persisted catalog or Flow identity.
        /// </summary>
        private static bool HasStableCatalogIdentity(WfReqElement element)
        {
            return element.NetworkId is > 0
                || element.ServiceId is > 0
                || element.TimeObjId is > 0
                || element.FlowNetworkObjectId is > 0
                || element.FlowNetworkGroupId is > 0
                || element.FlowServiceObjectId is > 0
                || element.FlowServiceGroupId is > 0
                || element.FlowTimeObjectId is > 0;
        }

        /// <summary>
        /// Compares only the current value fields copied into a durable snapshot.
        /// </summary>
        private static bool SnapshotValuesEqual(WfReqElement left, WfReqElement right)
        {
            return string.Equals(EffectiveIp(left.IpString, left.Cidr), EffectiveIp(right.IpString, right.Cidr), StringComparison.Ordinal)
                && string.Equals(EffectiveIp(left.IpEnd, left.CidrEnd), EffectiveIp(right.IpEnd, right.CidrEnd), StringComparison.Ordinal)
                && left.Port == right.Port
                && left.PortEnd == right.PortEnd
                && left.ProtoId == right.ProtoId
                && string.Equals(left.GroupName, right.GroupName, StringComparison.Ordinal)
                && string.Equals(left.Name, right.Name, StringComparison.Ordinal)
                && SamePointInTime(left.StartTime, right.StartTime)
                && SamePointInTime(left.EndTime, right.EndTime);
        }

        /// <summary>
        /// Returns the persisted address representation, falling back to a parsed CIDR.
        /// </summary>
        private static string? EffectiveIp(string? ip, Cidr? cidr)
        {
            string? value = ip ?? cidr?.CidrString;
            if (value == null)
            {
                return null;
            }

            Cidr normalized = new(value);
            return normalized.Valid ? normalized.CidrString : value;
        }

        /// <summary>
        /// Compares optional points in time independently of their time zone.
        /// </summary>
        private static bool SamePointInTime(DateTime? left, DateTime? right)
        {
            if (!left.HasValue || !right.HasValue)
            {
                return left.HasValue == right.HasValue;
            }
            return left.Value.ToUniversalTime() == right.Value.ToUniversalTime();
        }

        /// <summary>
        /// Determines whether a task represents a supported group snapshot/delta.
        /// </summary>
        private static bool IsGroupTask(WfReqTask task)
        {
            return task.TaskType == WfTaskType.group_create.ToString()
                || task.TaskType == WfTaskType.group_modify.ToString();
        }

        /// <summary>
        /// Creates a fixed-category materialization failure.
        /// </summary>
        private static EffectiveRequestStateMaterializationException Failure(
            EffectiveRequestStateFailureCategory category)
        {
            return new EffectiveRequestStateMaterializationException(category);
        }
    }
}
