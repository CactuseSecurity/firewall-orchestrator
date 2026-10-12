# WP-03 — Implementation-task position persistence

## Goal

Persist a calculated position on an implementation task without coupling the write to calculation, modifying other task fields, or triggering workflow actions.

## Schema and upgrade

Add the nullable column:

```text
request.impltask.rule_position jsonb
```

Apply it to:

- the fresh-install schema; and
- the idempotent upgrade script for the open product version `9.7.1`.

The original `9.5.11` target was obsolete when implementation was rebased. The migration is appended to the open `9.7.1.sql`, `product_version` remains `9.7.1`, and revision history is recorded under `9.7.1`. The upgrade must be safe to re-run and must preserve existing implementation tasks with `NULL` positions.

This work is a schema/GraphQL/installer change and must follow the repository's schema-change and installer skill instructions during implementation.

## Contract propagation

Propagate `rule_position` through the complete implementation-task data path:

- Hasura table metadata and every existing role permission that should retain its current implementation-task visibility/editability;
- implementation-task GraphQL query fragments;
- GraphQL insert/update/read shapes used by `WfImplTask`;
- a dedicated rule-position mutation; and
- `WfImplTask` using the two JSON attribute families currently required by FWO.

Do not broaden a role's access. Add the field only where the role's existing implementation-task permissions and the dedicated persistence use case require it.

## `WfImplTask` behavior

- A loaded position uses the WP-01 polymorphic contract.
- Known and unknown positions survive the database/GraphQL/model round trip.
- All copy/clone/conversion paths retain the position.
- A copy must not share mutable vendor payload or raw unknown JSON state with the original.
- A missing or database-`NULL` value maps to `null` without breaking older rows.

## Dedicated persistence operation

Add a narrow operation that identifies the implementation task and writes exactly one field: `rule_position`.

It must support:

- storing a position;
- replacing an existing position; and
- clearing a position by writing `null`.

The mutation/request must not send or update task title, rule data, state, timestamps controlled by workflow actions, ownership, assignment, or any other unrelated task field. It must not invoke workflow state actions, planning activation, automatic calculation, vendor communication, or executable changes.

Calculation failures are not stored. A caller may react to a failure by separately invoking the clear operation with `null`.

## Auditing boundary

Do not add position-change auditing in Phase 1. The requirement remains explicitly unresolved until consultation with the audit team establishes the appropriate event, actor, payload, and retention semantics. Record the later outcome by appending to `DECISIONS.md`.

## Tests

Add tests at the narrowest useful layers for:

- storing a known position and loading the same derived type and metadata;
- storing/loading an unknown position without losing its raw fields;
- replacing one position with another;
- clearing an existing position;
- loading an existing row whose position is `NULL`;
- all `WfImplTask` copy paths retaining an independent deep copy;
- the dedicated mutation containing only its identifier/key and `rule_position` input;
- unrelated implementation-task fields remaining unchanged after store, replace, and clear;
- no workflow action/client being invoked by the dedicated persistence path;
- serializer/database/GraphQL round-trip compatibility; and
- idempotent upgrade behavior.

Where existing test infrastructure permits, assert the generated GraphQL operation or captured variables directly so future refactors cannot silently turn the narrow update into a broad task update.

## Installation and upgrade validation

After unit/build/coverage checks:

1. Validate a fresh installation through the disposable installer-test workflow.
2. Validate upgrade from `9.7.0` to `9.7.1` through the same workflow.
3. Confirm the column is nullable and existing tasks load.
4. Exercise store, load, replace, and clear against the installed backend.

Do not run installation manually from the command line.

## Acceptance criteria

- Fresh installs and upgrades contain nullable `request.impltask.rule_position`.
- Hasura, GraphQL, and `WfImplTask` agree on the field and JSON representation.
- Known and unknown positions survive persistence.
- Copies retain independent position state.
- Store, replace, and clear affect only `rule_position`.
- Persistence has no calculation or workflow side effects.
- Failures are never written into the task.
- Version metadata and revision history are consistent.

## Dependencies and impact

Depends on WP-01. Expected affected layers are PostgreSQL schema/upgrade scripts, Hasura metadata/permissions, shared GraphQL fragments and models, middleware or shared persistence client, unit/integration tests, version metadata, and revision history. Exact files must be confirmed in the read-only planning pass before coding.

## Out of scope

- automatic persistence after calculation;
- storing failure details;
- audit events;
- UI display/editing;
- workflow state changes; and
- vendor execution.
