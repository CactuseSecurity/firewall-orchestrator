using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Data.Workflow;

namespace FWO.Services.Workflow;

public partial class WfDbAccess
{
    private const string EffectiveStateHistoryText = "Materialized effective request state";

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

        foreach (WfReqElement element in plan.ChangedElements)
        {
            await PersistEffectiveRequestElement(element);
        }

        WfTicket readback = await ReadTicketStrict(ticket.Id);
        VerifyEffectiveRequestState(ticket, readback, targetTaskIds,
            plan.ChangedTaskIds, plan.ChangedElements);
        await LogEffectiveStateChanges(ticket, readback, plan.ChangedTaskIds);
        return readback;
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
            Dictionary<string, object?> variables = BuildReqElementVariables(element);
            variables["id"] = element.Id;
            ReturnId result = await ApiConnection.SendQueryAsync<ReturnId>(
                RequestQueries.updateRequestElement, variables);
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
    /// Records one request-task history entry for every task changed by the verified plan.
    /// </summary>
    private async Task LogEffectiveStateChanges(WfTicket original, WfTicket readback,
        IReadOnlyCollection<long> changedTaskIds)
    {
        foreach (long taskId in changedTaskIds)
        {
            WfReqTask oldTask = TaskForHistory(original, taskId);
            WfReqTask newTask = TaskForHistory(readback, taskId);
            WorkflowChangeTarget target = new(original.Id, ModellingTypes.ChangeType.Update,
                ChangeHistoryObjectType.RequestTask, taskId);
            await LogWorkflowChange(target, EffectiveStateHistoryText,
                RequestTaskHistorySnapshot(oldTask), RequestTaskHistorySnapshot(newTask),
                original.Requester, false);
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
