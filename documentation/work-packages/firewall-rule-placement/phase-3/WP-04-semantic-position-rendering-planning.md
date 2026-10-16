# Phase 3 WP-04 planning - Semantic Check Point position rendering

## Status

Blocked until WP-03 implementation is complete.

## Planning objective

Produce a self-contained implementation brief for translating a persisted semantic `CheckPointRulePosition` into the existing Check Point rule-request representation without performing an external API call or activating a workflow.

This is a planning-only session. Do not change production code and do not inspect `FWO_Regel_Platzierung_Regelwerk.md`.

## Required reading

Read the authoritative roadmap material, Phase 2 position contract, the completed WP-03 implementation brief, the existing `FWO.ExternalSystems.CheckPoint` ticket/task/client code and tests, `phase-3/README.md`, and this file.

## Repository discovery

Identify and record:

- where access-rule request JSON is currently created and where a placement field belongs;
- the Check Point request representation already used by templates or task bodies;
- the exact supported representation for top, bottom, above-rule, below-rule, and layer/inline-layer selection;
- whether rendering belongs in `CheckPointTicketTask`, a dedicated pure renderer, or another existing abstraction;
- how known and unknown `RulePosition` values reach request construction;
- current validation and exception conventions; and
- captured-request tests that can prove no outbound call is required.

Use primary Check Point documentation during the planning session if repository evidence does not settle the request shape.

## Decisions the planner must settle

1. The exact JSON/request fields for every semantic anchor.
2. How the layer UID is represented, including inline layers.
3. The rendering entry point and owning project.
4. Behavior for null, unknown, malformed, wrong-algorithm, and wrong-vendor positions.
5. Whether top/bottom need explicit fields or omission/default behavior.
6. How rendering remains idempotent and avoids mutating the persisted position.
7. How templates and existing rule-task rendering remain backward-compatible when no position is supplied.
8. The exact unit tests and simulated-client boundary.

## Required planning output

Finalize `WP-04-semantic-position-rendering-implementation.md` with the authoritative request shape, exact files, behavior, tests, and validation. The brief must not rely on the implementer reading this plan or researching the vendor API again.

Record decisions and update the phase status and handoff.

## Planning acceptance

- Every semantic anchor has one unambiguous request representation.
- Existing requests without a position retain settled backward-compatible behavior.
- Rendering performs no login or API call.
- No workflow caller or strategy registration is activated.
- The customer document remains unopened.
