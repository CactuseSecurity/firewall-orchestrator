# Phase 3 WP-05 implementation - Customer recipes and production activation

## Status

Draft - customer analysis and planning required after WP-01 through WP-04 are complete.

Do not implement this package until the WP-05 planner has replaced all draft sections, resolved every customer ambiguity, and marked it `Ready for implementation`. The implementer should not need to reopen the customer document or read the WP-05 planning brief.

## Goal

Complete and activate Check Point rule positioning by implementing the approved customer blocks and recipes, mapping supported existing positioning enum values, registering the production strategy, and connecting the approved workflow caller and semantic-position renderer.

## Fixed requirements

- Reuse the Phase 1 manager/persistence contracts, Phase 2 recipe framework/graph/blocks, and Phase 3 WP-01 through WP-04 services.
- Add only blocks proven necessary by the customer behavior.
- Map supported existing `ProvisioningPositioningAlgorithm` values explicitly to complete immutable recipes.
- Never fall back to another algorithm, vendor, layer, section, or recipe unless the final customer-derived specification explicitly requires that behavior.
- Register exactly one Check Point strategy for each supported exact capability.
- Resolve one positioning input, load one graph, and execute one recipe per calculation.
- Persist successful positions and clear stale positions only according to the planner's approved caller policy.
- Render semantic anchors through WP-04; do not duplicate vendor request mapping.
- Preserve caller cancellation and safe diagnostics.
- Do not add new enum values, settings, schema fields, existing-rule semantics, UI, auditing, automatic imports, or freshness checks unless the finalized plan explicitly proves and routes that need.

## Normative customer behavior

To be supplied by the WP-05 planner as a complete behavior table. This section must identify inputs, selected recipe/scope, resulting anchor behavior, and failure behavior for every case.

## Recipe and enum mapping

To be supplied by the WP-05 planner as complete recipe trees and an exhaustive supported/unsupported enum mapping.

## Additional block contracts

To be supplied by the WP-05 planner. If no new blocks are necessary, state that explicitly.

## Workflow and activation design

To be supplied by the WP-05 planner, including exact caller, trigger, context/data loading, persistence/clearing behavior, renderer handoff, dependency-injection registration, and external-execution boundary.

## Exact change set

To be supplied by the WP-05 planner.

## Required tests

The final brief must enumerate:

- one or more acceptance scenarios for every customer behavior and recipe branch;
- every supported and unsupported enum mapping;
- all additional blocks, including missing and ambiguous scopes;
- zone mapping and precedence;
- full strategy orchestration using production recipes;
- exact manager selection after registration and duplicate-registration protection;
- successful persistence, replacement, and the approved clearing behavior;
- semantic rendering in the selected workflow path;
- cancellation and each safe failure boundary;
- absence of vendor calls during calculation;
- backward compatibility for workflows/configurations where positioning is unsupported; and
- any required integration, UI, or disposable-environment scenario.

## Validation

The finalized plan must provide focused commands and any required integration/disposable-environment workflow. At minimum:

- format all changed C# files with `dotnet format roles/FWO.sln`;
- build with `dotnet build --configuration Debug roles/FWO.sln`;
- run `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj`;
- run the complete relevant integration tests;
- demonstrate more than 80% line coverage for all new production code; and
- update UI help if a UI feature changes.

## Acceptance criteria

- All finalized customer scenarios produce the specified semantic position or typed safe failure.
- Exact enum-to-recipe mapping is complete and tested.
- Exactly one production Check Point strategy is selected for supported configurations.
- The approved workflow caller calculates, persists/clears, and renders according to the finalized sequence.
- Unsupported configurations retain safe behavior without fallback.
- All required validation passes and evidence is recorded.

## Out of scope

The WP-05 planner must replace this section with the final explicit exclusions, including the audit and external-execution boundaries.
