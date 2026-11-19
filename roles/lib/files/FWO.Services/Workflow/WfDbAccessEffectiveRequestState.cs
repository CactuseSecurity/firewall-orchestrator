using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;

namespace FWO.Services.Workflow;

public partial class WfDbAccess
{
    private const string EffectiveStateHistoryText = "Materialized effective request state";
    private const string PartialEffectiveStateHistoryText =
        "Partially materialized effective request state before failure";

    /// <summary>
    /// Persists and verifies the latest effective values of ticket-local object references.
    /// </summary>
    /// <param name="ticket">The complete persisted ticket used to build the update plan.</param>
    /// <param name="targetTaskIds">The request tasks about to become operational.</param>
    /// <returns>The verified database readback, or the supplied ticket when no writes are needed.</returns>
    /// <exception cref="EffectiveRequestStateMaterializationException">
    /// Thrown when planning, persistence, or verification cannot complete safely.
    /// </exception>
    public async Task<WfTicket> MaterializeEffectiveRequestState(WfTicket ticket,
        IReadOnlyCollection<long> targetTaskIds)
    {
        EffectiveRequestStateMaterializationPlan plan =
            EffectiveRequestStateMaterializer.CreatePlan(ticket, targetTaskIds);
        if (!plan.HasChanges)
        {
            return ticket;
        }
        ValidatePlanForPersistence(plan);

        bool persistenceBegan = false;
        WfTicket? readback = null;
        try
        {
            persistenceBegan = true;
            foreach (WfReqElement element in plan.ChangedElements)
            {
                await PersistEffectiveRequestElement(element);
            }

            readback = await ReadTicketStrict(ticket.Id);
            VerifyEffectiveRequestState(ticket, readback, targetTaskIds,
                plan.ChangedTaskIds, plan.ChangedElements);
            await LogEffectiveStateChanges(ticket, readback, plan.ChangedTaskIds,
                EffectiveStateHistoryText);
            return readback;
        }
        catch
        {
            if (persistenceBegan)
            {
                await RecoverEffectiveStateHistory(ticket, plan, readback);
            }
            throw;
        }
    }

    /// <summary>
    /// Updates one existing request element and treats every API or id mismatch as fatal.
    /// </summary>
    private async Task PersistEffectiveRequestElement(WfReqElement element)
    {
        if (element.Id <= 0)
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
        }

        try
        {
            Dictionary<string, object?> variables = BuildEffectiveRequestElementVariables(element);
            variables["id"] = element.Id;
            ReturnId result = await ApiConnection.SendQueryAsync<ReturnId>(
                RequestQueries.updateEffectiveRequestElementSnapshot, variables);
            if (result.UpdatedIdLong != element.Id)
            {
                throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
            }
        }
        catch (EffectiveRequestStateMaterializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure, exception);
        }
    }

    /// <summary>
    /// Builds the least-privilege payload for one effective-state snapshot update.
    /// </summary>
    private static Dictionary<string, object?> BuildEffectiveRequestElementVariables(WfReqElement element)
    {
        bool hasServiceReference = HasId(element.ServiceId)
            || HasId(element.FlowServiceObjectId)
            || HasId(element.FlowServiceGroupId);
        return new Dictionary<string, object?>
        {
            ["ip"] = element.Cidr != null && element.Cidr.Valid ? element.Cidr.CidrString : null,
            ["ipEnd"] = element.CidrEnd != null && element.CidrEnd.Valid ? element.CidrEnd.CidrString : null,
            ["port"] = hasServiceReference && !HasValidPort(element.Port) ? null : element.Port,
            ["portEnd"] = hasServiceReference && !HasValidPort(element.PortEnd) ? null : element.PortEnd,
            ["proto"] = hasServiceReference && !HasId(element.ProtoId) ? null : element.ProtoId,
            ["groupName"] = element.GroupName,
            ["name"] = element.Name,
            ["startTime"] = element.StartTime?.ToUniversalTime(),
            ["endTime"] = element.EndTime?.ToUniversalTime()
        };
    }

    /// <summary>
    /// Reads a ticket without the normal UI error swallowing behavior.
    /// </summary>
    private async Task<WfTicket> ReadTicketStrict(long ticketId)
    {
        try
        {
            WfTicket ticket = await ApiConnection.SendQueryAsync<WfTicket>(
                RequestQueries.getTicketById, new { id = ticketId });
            if (ticket.Id != ticketId)
            {
                throw Failure(EffectiveRequestStateFailureCategory.ReadbackFailure);
            }
            ticket.UpdateCidrsInTaskElements();
            ticket.ResetStateChangeTracking();
            return ticket;
        }
        catch (EffectiveRequestStateMaterializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(EffectiveRequestStateFailureCategory.ReadbackFailure, exception);
        }
    }

    /// <summary>
    /// Requires the strict readback to produce an empty materialization plan.
    /// </summary>
    private static void VerifyEffectiveRequestState(WfTicket original, WfTicket readback,
        IReadOnlyCollection<long> targetTaskIds, IReadOnlyCollection<long> changedTaskIds,
        IReadOnlyCollection<WfReqElement> expectedElements)
    {
        try
        {
            VerifyChangedTasks(original, readback, changedTaskIds);
            VerifyChangedElements(readback, expectedElements);
            if (EffectiveRequestStateMaterializer.CreatePlan(readback, targetTaskIds).HasChanges)
            {
                throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch);
            }
        }
        catch (EffectiveRequestStateMaterializationException exception)
            when (exception.Category != EffectiveRequestStateFailureCategory.ReadbackMismatch)
        {
            throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch, exception);
        }
        catch (Exception exception)
        {
            throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch, exception);
        }
    }

    /// <summary>
    /// Verifies that every changed task retained its exact ticket identity and task type.
    /// </summary>
    private static void VerifyChangedTasks(WfTicket original, WfTicket readback,
        IReadOnlyCollection<long> changedTaskIds)
    {
        foreach (long taskId in changedTaskIds)
        {
            WfReqTask expected = original.Tasks.Single(task => task.Id == taskId);
            WfReqTask? actual = readback.Tasks.SingleOrDefault(task => task.Id == taskId);
            if (actual == null || actual.TicketId != expected.TicketId || actual.TaskType != expected.TaskType)
            {
                throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch);
            }
        }
    }

    /// <summary>
    /// Rejects an invalid update set before the first non-transactional element write.
    /// </summary>
    private static void ValidatePlanForPersistence(EffectiveRequestStateMaterializationPlan plan)
    {
        if (plan.ChangedElements.Any(element => element.Id <= 0)
            || plan.ChangedElements.Select(element => element.Id).Distinct().Count() != plan.ChangedElements.Count)
        {
            throw Failure(EffectiveRequestStateFailureCategory.PersistenceFailure);
        }
    }

    /// <summary>
    /// Verifies that every intended row and every persisted payload value survived the strict readback.
    /// </summary>
    private static void VerifyChangedElements(WfTicket ticket,
        IReadOnlyCollection<WfReqElement> expectedElements)
    {
        Dictionary<long, WfReqElement> actualById = ticket.Tasks.SelectMany(task => task.Elements)
            .Where(element => element.Id > 0)
            .GroupBy(element => element.Id)
            .ToDictionary(group => group.Key, group => group.Single());
        foreach (WfReqElement expected in expectedElements)
        {
            if (!actualById.TryGetValue(expected.Id, out WfReqElement? actual)
                || !ElementPayloadMatches(expected, actual))
            {
                throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch);
            }
        }
    }

    /// <summary>
    /// Compares the exact request-element payload written by the materialization operation.
    /// </summary>
    private static bool ElementPayloadMatches(WfReqElement expected, WfReqElement actual)
    {
        if (expected.TaskId != actual.TaskId)
        {
            return false;
        }
        Dictionary<string, object?> expectedPayload = BuildReqElementVariables(expected);
        Dictionary<string, object?> actualPayload = BuildReqElementVariables(actual);
        return expectedPayload.Count == actualPayload.Count
            && expectedPayload.All(pair => actualPayload.TryGetValue(pair.Key, out object? value)
                && Equals(pair.Value, value));
    }

    /// <summary>
    /// Selects tasks with at least one planned element whose exact payload is present in the readback.
    /// Malformed ownership or duplicate element ids produce no audit selection.
    /// </summary>
    private static IReadOnlyCollection<long> FindAppliedTaskIds(
        EffectiveRequestStateMaterializationPlan plan, WfTicket readback)
    {
        try
        {
            Dictionary<long, (long OwnerTaskId, WfReqElement Element)> actualById = [];
            foreach (WfReqTask task in readback.Tasks)
            {
                if (task.Id <= 0)
                {
                    continue;
                }
                foreach (WfReqElement element in task.Elements.Where(element => element.Id > 0))
                {
                    if (!actualById.TryAdd(element.Id, (task.Id, element)))
                    {
                        return [];
                    }
                }
            }

            HashSet<long> appliedTaskIds = [];
            foreach (WfReqElement expected in plan.ChangedElements)
            {
                if (expected.Id <= 0)
                {
                    return [];
                }
                if (actualById.TryGetValue(expected.Id, out var actual)
                    && expected.TaskId > 0
                    && actual.OwnerTaskId == expected.TaskId
                    && ElementPayloadMatches(expected, actual.Element))
                {
                    appliedTaskIds.Add(expected.TaskId);
                }
            }
            return [.. appliedTaskIds.OrderBy(taskId => taskId)];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Records one request-task history entry for every selected task.
    /// </summary>
    private async Task<bool> LogEffectiveStateChanges(WfTicket original, WfTicket readback,
        IReadOnlyCollection<long> taskIds, string changeText, bool rethrowFailure = false)
    {
        bool succeeded = true;
        foreach (long taskId in taskIds)
        {
            try
            {
                WfReqTask oldTask = TaskForHistory(original, taskId);
                WfReqTask newTask = TaskForHistory(readback, taskId);
                WorkflowChangeTarget target = new(original.Id, ModellingTypes.ChangeType.Update,
                    ChangeHistoryObjectType.RequestTask, taskId);
                await LogWorkflowChange(target, changeText,
                    RequestTaskHistorySnapshot(oldTask), RequestTaskHistorySnapshot(newTask),
                    original.Requester, false, rethrowFailure);
            }
            catch when (rethrowFailure)
            {
                succeeded = false;
            }
        }
        return succeeded;
    }

    /// <summary>
    /// Best-effort audits planned element payloads that strict readback proves were persisted.
    /// Recovery never replaces the materialization failure that triggered it.
    /// </summary>
    private async Task RecoverEffectiveStateHistory(WfTicket original,
        EffectiveRequestStateMaterializationPlan plan, WfTicket? readback)
    {
        try
        {
            readback ??= await ReadTicketStrict(original.Id);
            IReadOnlyCollection<long> appliedTaskIds = FindAppliedTaskIds(plan, readback);
            if (appliedTaskIds.Count > 0)
            {
                bool historySucceeded = await LogEffectiveStateChanges(original, readback,
                    appliedTaskIds, PartialEffectiveStateHistoryText, true);
                if (!historySucceeded)
                {
                    await ReportRecoveryFailure(original.Id, "history-write");
                }
            }
        }
        catch (Exception exception)
        {
            string category = exception is EffectiveRequestStateMaterializationException materializationException
                ? $"readback-{materializationException.Category}"
                : "recovery-processing";
            await ReportRecoveryFailure(original.Id, category);
        }
    }

    /// <summary>
    /// Reports a recovery problem without formatting the triggering exception or exposing request payloads.
    /// </summary>
    private async Task ReportRecoveryFailure(long ticketId, string category)
    {
        TryWriteRecoveryLog(
            $"Could not audit partially materialized effective request state for ticket {ticketId}. Category: {category}.");
        try
        {
            await AlertHelper.SetAlert(ApiConnection, "Effective request state materialization",
                $"Could not audit partially materialized effective request state for ticket {ticketId}.",
                GlobalConst.kWorkflow, AlertCode.WorkflowAlert,
                new AlertHelper.AdditionalAlertData
                {
                    JsonData = new { ticketId },
                    CompareDesc = true
                });
        }
        catch
        {
            TryWriteRecoveryLog($"Could not create a recovery audit alert for ticket {ticketId}.");
        }
    }

    /// <summary>
    /// Writes fixed recovery diagnostics and suppresses failures from the logging infrastructure itself.
    /// </summary>
    private static void TryWriteRecoveryLog(string message)
    {
        try
        {
            Log.WriteError("Effective request state materialization", message);
        }
        catch
        {
            // Recovery reporting must never replace the original materialization exception.
        }
    }

    /// <summary>
    /// Finds a task required for verified history snapshots.
    /// </summary>
    private static WfReqTask TaskForHistory(WfTicket ticket, long taskId)
    {
        return ticket.Tasks.SingleOrDefault(task => task.Id == taskId)
            ?? throw Failure(EffectiveRequestStateFailureCategory.ReadbackMismatch);
    }

    /// <summary>
    /// Creates a materialization exception without exposing ticket payload data.
    /// </summary>
    private static EffectiveRequestStateMaterializationException Failure(
        EffectiveRequestStateFailureCategory category)
    {
        return new EffectiveRequestStateMaterializationException(category);
    }

    /// <summary>
    /// Creates a materialization exception with its internal cause.
    /// </summary>
    private static EffectiveRequestStateMaterializationException Failure(
        EffectiveRequestStateFailureCategory category, Exception innerException)
    {
        return new EffectiveRequestStateMaterializationException(category, innerException);
    }
}
