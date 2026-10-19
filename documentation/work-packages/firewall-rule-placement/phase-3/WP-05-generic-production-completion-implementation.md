# Phase 3 WP-05 implementation — Generic production completion

## Status

Draft — planning required.

Do not implement this package until the paired planner has resolved every placeholder and marked this brief `Ready for implementation`.

## Goal

Activate generic rule-position calculation and semantic-position persistence for supported new access-rule tasks. The persisted `RulePosition` is the terminal product output for a later placer.

## Generic capability and recipe

To be supplied by the WP-05 planner. This section must name each supported existing enum value, define its complete immutable recipe, and list all unsupported values explicitly. No mapping may be inferred from an enum name.

## Workflow and persistence design

To be supplied by the WP-05 planner. It must define the exact caller and trigger, implementation-task selection, context loading, DI registration, calculation sequence, transaction/error boundary, and store/replace/preserve/clear behavior.

## Phase separation and cleanup

The generic recipe and its strategy path must use no zone input. They must not load a designated zone matrix, invoke `ICreationRuleZoneResolver`, expand Flow groups, or require compliance-zone configuration. The planner must define the minimal strategy/catalog adjustment that isolates the already-implemented zone-dependent path for Phase 4 without registering it in Phase 3.

Remove `CheckPointRulePositionRenderer`, `CheckPointRulePositionRendererTest`, and renderer-only compatibility assertions or documentation that no longer protect retained behavior. Do not replace the renderer with another vendor-payload adapter. A future placer owns any conversion contract.

## Exact change set

To be supplied by the WP-05 planner after the duplication and activation audits.

## Required tests

The final brief must enumerate tests for:

- every supported and unsupported generic enum mapping;
- exact manager selection and duplicate-registration protection;
- populated and empty initial layers;
- missing, ambiguous, or malformed graph structure;
- the generic positioning strategy operating without zone dependencies and making no zone queries or group-expansion calls, while existing non-positioning zone consumers remain compatible;
- one calculation per applicable gateway-specific implementation task;
- successful persistence and replacement;
- the approved stale-position preserve/clear behavior for every failure boundary;
- cancellation without unintended persistence;
- backward compatibility for unsupported vendors and configurations;
- absence of the renderer type and its dedicated tests;
- absence of Check Point client calls; and
- safe diagnostics and required integration/installer scenarios.

## Validation

The finalized brief must include focused tests, `dotnet format roles/FWO.sln`, a Debug solution build, the complete unit suite, relevant integration tests, and more than 80% line coverage for new production code. The outstanding Phase 1 disposable-VM fresh-install and `9.7.2` to `9.7.3` upgrade gates remain separately required.

## Acceptance criteria

- At least one confirmed generic capability calculates and persists a semantic position end to end.
- Unsupported capabilities fail safely without fallback.
- Empty layers produce the approved semantic anchor; missing layers fail explicitly.
- Generic calculation has no designated-zone, Flow-group, or compliance-zone prerequisite.
- The persisted position is the final output.
- The WP-04 renderer is removed; no request body can be rendered and no firewall API, publish, or installation action is reachable.

## Out of scope

- Customer-document analysis and customer-specific recipes.
- Zone-input loading or mapping, which belongs to Phase 4.
- Any replacement for the removed WP-04 renderer.
- Check Point API execution, publish, or policy installation.
- New settings, enums, schema fields, UI, or audit events unless separately approved and routed.
