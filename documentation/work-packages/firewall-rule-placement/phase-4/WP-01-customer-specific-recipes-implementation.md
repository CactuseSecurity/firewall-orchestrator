# Phase 4 WP-01 implementation — Customer-specific recipes

## Status

Draft — customer analysis and planning required after Phase 3 WP-05 is complete.

Do not implement this package until the paired planner has replaced every draft section and marked it `Ready for implementation`.

## Goal

Extend the validated graph-only generic product with the approved customer-specific blocks, zone-input usage, recipes, and enum mappings without changing its workflow activation, persistence, semantic-output boundary, or external-execution boundary.

## Normative customer behavior

To be supplied by the Phase 4 planner as a complete behavior table.

## Recipe and enum mapping

To be supplied by the Phase 4 planner as complete recipe trees, explicit zone-input requirements, and an exhaustive supported/unsupported mapping. Generic capabilities must remain zone-independent.

## Additional block contracts

To be supplied by the Phase 4 planner. If existing blocks cover every customer behavior, state explicitly that no new block is required.

## Exact change set

To be supplied by the Phase 4 planner. Generic activation and persistence files must remain unchanged unless the plan proves a necessary defect or missing extension seam.

## Required tests

The final brief must enumerate one or more acceptance scenarios for every customer behavior and recipe branch, every supported and unsupported customer enum mapping, all additional blocks, zone-set mapping and precedence, customer-only zone loading, empty/missing/ambiguous scopes, cancellation, safe failures, and regression coverage proving the generic Phase 3 capability remains zone-independent.

## Validation

The finalized brief must provide focused commands, repository formatting/build/unit-test commands, relevant integration checks, and more than 80% line coverage for new production code.

## Acceptance criteria

- Every approved customer scenario produces the specified semantic position or typed safe failure.
- Customer recipes extend the existing catalog without changing the generic workflow path.
- Zone readers and resolvers run only for customer capabilities that require them.
- No renderer, Check Point client, publish, or installation action is activated.

## Out of scope

- Redesigning generic workflow activation or persistence.
- Reintroducing the removed request renderer or another vendor-payload adapter.
- Executing, publishing, or installing Check Point policy changes.
- Unproven schema, UI, auth, installer, or existing-rule semantic changes.
