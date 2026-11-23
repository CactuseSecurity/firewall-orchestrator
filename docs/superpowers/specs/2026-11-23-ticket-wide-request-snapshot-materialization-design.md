# Ticket-wide request snapshot materialization

## Intent

Ticket-local object and service tasks are authoritative while a ticket is being edited. Other request elements may keep copied values together with an exact `ObjectTaskId` reference. Those copies can become stale when the referenced task is edited.

Executing the **Create Implementation Task** workflow action freezes the edited request state. Before it creates any implementation task, FWO shall refresh every stale ticket-local copy in the ticket, persist the refreshed values, and use the same values in memory for implementation-task creation. This keeps persisted request data aligned with the state that becomes operational.

## Goals

- Refresh all stale request-element snapshots in the ticket, rather than first determining which ones feed the action's current task or bundle.
- Resolve only exact ticket-local references; do not infer relationships from names or equal values.
- Persist the complete refresh before creating the first implementation task.
- Apply the persisted values to the in-memory ticket so implementation tasks are created from the same state without another database read.
- Make repeated execution idempotent: a current ticket produces no snapshot writes.
- Fail the action before implementation-task creation when references or persistence are invalid.
- Record workflow history for each request task whose persisted request content changes.

## Non-goals

- Materializing at external-ticket handoff or at every possible operational boundary.
- Compensating for a workflow whose Create Implementation Task action is placed before editing is complete. Reordering a workflow requires placing the action at the correct freeze point.
- Keeping copies synchronized eagerly on every editor, API, or middleware write path.
- Replacing copied values with a reference-only data model.
- Changing the database schema, authentication model, installer, or unrelated workflow behavior.

## Constraints

- A normal ticket contains few tasks and elements, so a ticket-wide scan is preferred over dependency-selection machinery.
- The existing `ObjectTaskId` is the relationship authority.
- Element identity and semantics must remain unchanged: IDs, owning task, field, reference, request action, catalog/Flow identities, and group delta markers are preserved.
- Only value fields represented by the referenced task's requested state are refreshed, such as name, address/range, protocol/ports, group name, and time values.
- Catalog-backed elements without a ticket-local reference remain untouched.
- Request-content writes go through `WfDbAccess` and receive normal workflow change history with `contentChange = true`.
- The batch persistence operation must prove that every intended row was updated exactly once. A missing or extra match is a failure.

## Approaches considered

### Chosen: ticket-wide materialization at Create Implementation Task

Scan the complete ticket when the workflow action executes, construct a validated replacement plan for every stale exact reference, persist it as one batch, apply it in memory, write history, and continue with existing implementation-task creation.

This establishes one clear freeze boundary, covers the real ticket shape without special selection logic, and avoids spreading synchronization responsibilities across editors and APIs.

### Rejected: eager synchronization during editing

Update dependent copies whenever an object or group is edited. Some UI paths already attempt this, but correctness would depend on every present and future write path remembering the synchronization step. It also provides no single point at which the request becomes final.

### Rejected for this change: reference-only request elements

Stop storing copied values and resolve references whenever data is consumed. This removes stale snapshots conceptually, but changes the data contract and all consumers. It belongs to the larger workflow redesign rather than this focused correction.

## Design

### 1. Build a ticket-wide plan

At the start of Create Implementation Task, inspect every persisted request element in the complete ticket that has a positive `ObjectTaskId`.

For a reference to an object or service task, locate that exact task in the same ticket and obtain its current requested element. Compare only the snapshot value fields and add a replacement when they differ.

For a reference to a group task, leave the group reference itself unchanged and inspect the referenced group's member rows. Refresh group members that themselves reference ticket-local object or service tasks. Process each referenced group once.

Before persistence, reject malformed data needed for this operation: a referenced task missing from the current ticket, a wrong reference type, an invalid object-task element structure, incompatible fields, or a non-persisted target element. Existing catalog members are valid without an `ObjectTaskId` and are not rewritten. Unrelated workflow-order and graph validation remains where it already belongs.

The planning step does not mutate the caller's ticket.

### 2. Persist as one checked batch

If the plan is empty, continue directly to implementation-task creation.

Otherwise, send one narrow GraphQL batch mutation containing the changed element IDs and only their refreshed value fields. Require one affected row for every submitted update. Any validation, transport, authorization, or affected-row failure aborts the action before implementation tasks are created.

Hasura executes the `update_many` entries in a transaction and rolls them all back when an entry errors. An entry matching no row is not an error, however: the exact affected-row proof then fails after any preceding successful entries have committed. This local and recoverable residual is accepted deliberately. Those committed rows already contain the correct snapshots, the action creates no implementation tasks, and the logged per-element results force a reload or retry from current database state. Do not add a database function or partial-write recovery for this case.

No strict full-ticket readback is required: the submitted plan and exact affected-row result are the success proof.

### 3. Update memory and history

After successful persistence, apply copies of the planned replacements to the corresponding elements in the active in-memory ticket. Use this ticket for the existing implementation-task creation loop.

Write one workflow history entry for every changed request task, using its before and after snapshots and marking it as a request-content change. History uses the existing `WfDbAccess` workflow-history path; no new audit subsystem is introduced.

If history is part of the same established write operation, its existing failure behavior applies. The implementation plan must confirm the exact ordering expected by current `WfDbAccess` callers without inventing recovery behavior unique to materialization.

### 4. Preserve the action boundary

Automatic and user-triggered uses of the existing Create Implementation Task action share the same preparation step. Ticket scope, request-task scope, bundling, and multi-device creation affect which implementation tasks are subsequently created, but not which stale ticket copies are refreshed.

External-ticket dispatch and unrelated manual UI construction do not receive separate materialization paths. Workflow configuration is responsible for placing Create Implementation Task after editing and before the request state is consumed operationally.

## Failure behavior

- Invalid references or structures: fail before persistence.
- Batch transport/authorization failure: fail before changing memory or creating implementation tasks. An unexpected affected-row count also fails before those steps and logs each planned element's result; preceding successful snapshot rows may already contain their correct values as described above.
- Successful persistence: update memory and history, then create implementation tasks.
- No stale copies: perform no snapshot mutation or materialization history write.

The user receives the existing workflow-action error presentation. Diagnostics identify the failure category and ticket/task/element identifiers where safe, without logging request payloads unnecessarily.

## Tests

Focused unit tests shall cover:

- stale network, service, and time snapshots;
- current snapshots producing no mutation;
- all stale copies across unrelated tasks in one ticket being refreshed;
- group members that reference ticket-local objects;
- preservation of group-modify create/delete actions and stable identities;
- catalog-backed members remaining unchanged;
- missing, wrong-type, structurally invalid, and field-incompatible references failing before writes;
- exact batch payload and affected-row validation;
- persistence failure creating no implementation tasks and leaving memory unchanged;
- successful persistence updating memory before one or multiple implementation tasks are created;
- repeated execution being idempotent;
- workflow history being written once per changed request task.

Existing implementation-task tests should demonstrate that ticket scope, task scope, bundles, and multi-device creation still behave as before after the single ticket-wide preparation step.

## Builder decisions remaining

- Reuse the narrowest existing request-element mutation if it supports atomic batch updates and the required roles; otherwise add one GraphQL operation without changing schema or permissions more broadly than necessary.
- Place the resolver in the shared workflow service layer so both planning and persistence remain independent of Razor/UI code.
- Reuse existing workflow exceptions and UI action error handling unless a small categorized exception materially improves diagnostics.
