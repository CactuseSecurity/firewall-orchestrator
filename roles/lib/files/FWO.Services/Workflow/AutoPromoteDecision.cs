using FWO.Data.Workflow;

namespace FWO.Services.Workflow
{
    /// <summary>
    /// Result of a check called by a conditional auto-forward.
    /// </summary>
    /// <param name="Passed">True when the condition is met, e.g. compliant or all managements imported.</param>
    /// <param name="TicketComment">Comment documenting the result, added to the ticket when it is moved; null for none.</param>
    public sealed record ConditionalCheckResult(bool Passed, string? TicketComment = null);

    /// <summary>
    /// Decision of an auto-forward action.
    /// </summary>
    /// <param name="ToStateId">Target state: null for the automatically determined state,
    /// <see cref="WfStateAction.RemainStateId"/> to keep the current state.</param>
    /// <param name="TicketComment">Comment added to the ticket after the move; null for none.</param>
    public sealed record AutoPromoteDecision(int? ToStateId, string? TicketComment = null)
    {
        /// <summary>
        /// Decision that keeps the object in its current state.
        /// </summary>
        public static AutoPromoteDecision Remain { get; } = new(WfStateAction.RemainStateId);

        /// <summary>
        /// Decision for a conditional target <see cref="WfStateAction.AutomaticStateId"/>, which conditional
        /// auto-forwards do not support. Configurations saved with it keep the object in its current state.
        /// </summary>
        public static AutoPromoteDecision RemainForAutomaticTarget { get; } = new(WfStateAction.RemainStateId) { HasAutomaticTarget = true };

        /// <summary>
        /// True when the object stays in its current state.
        /// </summary>
        public bool RemainsInState => ToStateId == WfStateAction.RemainStateId;

        /// <summary>
        /// True when the decision results from the unsupported conditional target "automatic".
        /// </summary>
        public bool HasAutomaticTarget { get; private init; }
    }
}
