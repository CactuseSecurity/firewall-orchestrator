# Phase 4 — Customer-specific positioning extension

## Status

Deferred until Phase 3 WP-05 has produced and validated a usable generic semantic-positioning product.

Phase 4 extends the finished product with customer-specific behavior. It owns the first active use of the already-implemented designated-zone reader, creation-rule zone resolver, and zone-dependent strategy orchestration. It does not own generic activation, workflow integration, persistence, request rendering, or firewall execution.

## Work packages

| Work package | Status | Planning brief | Implementation brief | Outcome |
| --- | --- | --- | --- | --- |
| WP-01 | Blocked by Phase 3 WP-05 | [Plan customer-specific recipes](WP-01-customer-specific-recipes-planning.md) | [Implement customer-specific recipes](WP-01-customer-specific-recipes-implementation.md) | Analyze the customer document and extend the production catalog with only the required blocks, recipes, and enum mappings. |

## Pre-implemented Phase 4 foundation

- The historical Phase 3 WP-02 service already produces immutable creation-rule zone input by reusing Flow and compliance infrastructure.
- The zone-capable portion of historical Phase 3 WP-03 already demonstrates designated-zone loading, zone resolution, catalog selection, and recipe execution.
- These components remain inactive for positioning until WP-01 defines exact customer capabilities and activates them without affecting the graph-only generic path.

## Customer-document boundary

`FWO_Regel_Platzierung_Regelwerk.md` is opened for the first time in the Phase 4 WP-01 planning session. The planner converts every relevant customer statement into a decision, recipe, test, explicit failure, or exclusion. The implementation brief must be self-contained so the implementer does not reopen the source document.

## Fixed boundaries

- Start from the validated Phase 3 product; do not redesign its workflow caller or persistence path without a proven requirement.
- Use zone input only for customer algorithms that require it. The generic algorithm remains graph-only.
- Activate the existing designated-zone and creation-rule zone services only through exact customer capabilities; do not make them generic prerequisites.
- Add only blocks and graph data proven necessary by customer behavior.
- Extend the catalog with explicit customer mappings; never use vendor, algorithm, scope, or recipe fallback.
- Keep the semantic-position-only terminal boundary.
- The Phase 3 WP-04 renderer is removed. Do not recreate vendor request rendering or execute Check Point API, publish, or installation operations.
- Route any newly proven schema, installer, RBAC, or UI impact through the applicable specialized skill before implementation.

## Phase acceptance

- Every customer statement is represented by a decision, recipe, test, failure, or exclusion.
- Each supported customer algorithm maps to exactly one complete immutable recipe.
- Customer behavior extends rather than replaces the generic product.
- Zone loading occurs only for customer capabilities that explicitly require it.
- No rendering or firewall execution is activated.
