-- SEC-06: the workflow action endpoint executes the side effects of a state change (mail, external
-- request, flow creation) for a transition the caller describes. The object's state is persisted by
-- the caller before the actions are requested, so the only check available was that the object
-- already stands in the requested new state - which stays true once the transition happened, and
-- therefore let the same request be submitted again to fire the side effects a second time.
-- This table records which state the actions of an object were last executed for. The middleware
-- claims it in one statement before it runs anything, so the execution can be claimed exactly once.
-- The guard compares to_state_id only. from_state_id is kept for the audit trail but is taken from
-- the request body and never established against the state the object actually held, so it must not
-- decide whether a claim is granted - otherwise a replay could re-arm the guard by naming a
-- different origin state.
-- One row per object is enough: a replay repeats the state the object was last moved into, while
-- legitimately entering a state again requires leaving it first, which records the state it was
-- left for here in between. That holds because every execution of state-change actions inside the
-- middleware writes this row, not only the ones requested through the action endpoint - a request
-- task promoted by the external request chain writes it too. A row that lagged behind the object
-- would turn the next legitimate move back into a refusal.
CREATE TABLE IF NOT EXISTS request.state_change_execution
(
    object_scope VARCHAR NOT NULL,
    object_id BIGINT NOT NULL,
    from_state_id INT NOT NULL,
    to_state_id INT NOT NULL,
    executed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    executed_by VARCHAR,
    CONSTRAINT state_change_execution_pkey PRIMARY KEY (object_scope, object_id)
);

-- Existing objects have no recorded execution, so the first transition after this upgrade is
-- claimable for each of them. That is the safe direction: it can let one already executed
-- transition be re-requested once, exactly as before this upgrade, rather than blocking a
-- legitimate promote of every ticket that is currently in flight.
