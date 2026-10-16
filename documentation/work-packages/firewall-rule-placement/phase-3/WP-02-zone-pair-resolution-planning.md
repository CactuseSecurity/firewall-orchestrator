# Phase 3 WP-02 planning - Creation-rule zone resolution

## Status

Blocked until WP-01 implementation and validation are complete.

## Planning objective

Produce a self-contained implementation brief for turning a new access rule's source and destination workflow elements into the immutable zone input consumed by Check Point positioning.

This is a planning-only session. Do not change production code. Do not inspect `FWO_Regel_Platzierung_Regelwerk.md` and do not map any zone result to a customer section or recipe.

## Required reading

Read the authoritative roadmap material, Phase 1 positioning context, Phase 2 WP-04, the completed WP-01 implementation brief, `phase-3/README.md`, and this file.

Use `fwo-route-request` to confirm whether the reusable logic belongs in shared services or middleware. A schema, auth, installer, or UI skill is not expected unless discovery identifies a real cross-layer change.

## Repository discovery

Inspect and record:

- the canonical `WfReqTask` and `WfReqElement` fields for source and destination members of a new access rule;
- existing range/group expansion and address normalization behavior;
- `ComplianceZoneResolver`, `ComplianceZoneService`, network-zone models, and their data-loading conventions;
- how Internet, dynamic/domain objects, groups, mixed address families, unresolved elements, and overlapping zones are represented today;
- the effective management/gateway settings needed by resolution;
- whether resolution can be kept in a pure shared service after the caller loads zones; and
- existing tests and fixtures that can be reused without coupling positioning to an HTTP controller.

## Decisions the planner must settle

1. The immutable output contract: exact pair, ordered zone sets, or another representation that does not encode customer mapping.
2. Which request actions and element kinds participate in creation placement.
3. Group expansion and address normalization ownership.
4. Cardinality rules for zero, one, or multiple source/destination zone matches.
5. Ambiguity, unresolved-input, unsupported-object, and cancellation behavior.
6. Whether existing compliance-zone behavior can be reused directly or needs a narrowly shared extraction.
7. Which data is caller-loaded and which work is performed by the resolver.
8. Safe diagnostic boundaries.

If exact zone cardinality is customer-policy-dependent, preserve that information in the generic result and defer only the mapping decision to WP-05. Do not guess it here.

## Required planning output

Make `WP-02-zone-pair-resolution-implementation.md` self-contained with exact contracts, files, algorithms, failure semantics, tests, and validation. It must contain no unresolved placeholders before it is marked ready.

Record decisions in `DECISIONS.md` and update the phase status and `SESSION-HANDOFF.md`.

## Planning acceptance

- The planned resolver consumes only new-rule workflow data and caller-loaded zone data.
- It does not compare existing rule semantics.
- It does not select a layer, section, recipe, or enum mapping.
- Existing reusable zone behavior is not duplicated without a documented reason.
- The customer document remains unopened.
