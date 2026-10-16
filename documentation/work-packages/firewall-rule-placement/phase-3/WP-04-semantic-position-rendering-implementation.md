# Phase 3 WP-04 implementation - Semantic Check Point position rendering

## Status

Draft - planning required after WP-03 is complete.

## Goal

Render a valid `CheckPointRulePosition` into the existing Check Point access-rule request representation without changing calculation, persistence, or external execution behavior.

## Fixed requirements

- Support `Top`, `Bottom`, `Above`, and `Below` semantic anchors.
- Require the existing rule UID only for `Above` and `Below`.
- Carry the target layer UID using the authoritative Check Point request shape settled by the planner.
- Validate wrong-vendor, unknown, null, malformed, and incompatible positions according to the settled design.
- Preserve existing request behavior when no calculated position is supplied, as explicitly settled by the planner.
- Keep rendering deterministic, side-effect-free, and independent of the Check Point client login/session.
- Do not register a positioning strategy or activate a workflow caller.

## Settled request mapping and design

The WP-04 planner must replace this paragraph with an exact anchor-to-request mapping and the chosen rendering entry point.

## Exact change set

To be supplied by the WP-04 planner.

## Required tests

The final brief must name tests for:

- each of the four semantic anchors;
- layer and inline-layer representation;
- required/prohibited rule UID combinations;
- null, unknown, wrong-vendor, wrong-algorithm, and malformed positions;
- requests without a position;
- repeated rendering without mutation or duplicate fields;
- compatibility with existing ticket templates/task bodies; and
- proof that rendering performs no external call.

## Validation

The planner must add focused commands. Implementation also requires C# formatting, the Debug solution build, the complete unit suite, and more than 80% line coverage for new production code.

## Acceptance criteria

- Every valid semantic position renders to the settled Check Point representation.
- Invalid positions fail safely before an external call.
- Existing unrelated Check Point request behavior remains unchanged.
- No workflow or strategy becomes active.

## Out of scope

- Customer-document analysis or recipes.
- Snapshot or zone loading.
- Strategy registration and workflow activation.
- Executing, publishing, or installing a Check Point policy.
