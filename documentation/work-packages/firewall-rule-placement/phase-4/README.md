# Phase 4 — Customer-specific positioning extension

## Status

WP-01 is implemented and locally validated. WP-02 activates that customer calculation through the existing workflow and persistence seam while keeping generic positioning graph-only.

Phase 4 first adds isolated KfW Check Point calculation behavior. Per D-047, enum mapping, production registration, zone-service activation for positioning, and workflow selection of the customer algorithm move to a separate later plan. Per D-058, Fortinet `SECTION_LABEL` is deferred beyond the current sprint and quarter. Phase 4 does not own generic activation, persistence, request rendering, or firewall execution.

## Work packages

| Work package | Status | Planning brief | Implementation brief | Outcome |
| --- | --- | --- | --- | --- |
| WP-01 | Implemented and locally validated | [Plan customer-specific calculation](WP-01-customer-specific-recipes-planning.md) | [Implemented customer-specific calculation](WP-01-customer-specific-recipes-implementation.md) | Adds isolated KfW Check Point guard-derived functional-layer selection, normalized Section matching, same-ticket effective-group input, and activation-neutral recipe construction without production registration. |
| WP-02 | Implemented and locally validated; VM validation unavailable | [Plan customer activation](WP-02-customer-positioning-activation-planning.md) | [Implemented customer activation](WP-02-customer-positioning-activation-implementation.md) | Adds the behavior-based setting, algorithm-gated ticket context, customer catalog/strategy registration, and automatic semantic-position persistence. |

## Pre-implemented Phase 4 foundation

- The historical Phase 3 WP-02 service already produces immutable creation-rule zone input by reusing Flow and compliance infrastructure.
- The zone-capable portion of historical Phase 3 WP-03 already demonstrates designated-zone loading, zone resolution, catalog selection, and recipe execution.
- These components remain inactive for positioning throughout WP-01. A later selection/activation plan may connect them without affecting the graph-only generic path.

## Customer-document boundary

`FWO_Regel_Platzierung_Regelwerk.md` is opened for the first time in the Phase 4 WP-01 planning session. The planner converts every relevant customer statement into a decision, recipe, test, explicit failure, or exclusion. The implementation brief must be self-contained so the implementer does not reopen the source document.

Settled customer behavior and the completed technical-design reasoning are maintained in [EXPERT-QUESTIONS.md](EXPERT-QUESTIONS.md).

## Fixed boundaries

- Start from the validated Phase 3 product; do not redesign its workflow caller or persistence path without a proven requirement.
- Use zone input only for customer algorithms that require it. The generic algorithm remains graph-only.
- Do not activate the existing designated-zone and creation-rule zone services in WP-01. A later plan may activate them only through exact customer capabilities; they never become generic prerequisites.
- Add only blocks and graph data proven necessary by customer behavior.
- Implement Check Point `SECTION_NATIVE` only. Defer Fortinet `SECTION_LABEL` and its distinct semantic position and label-block behavior to a future work package.
- Select the Check Point functional layer by complete source/destination IP containment in exactly one layer guard. Never infer it from layer names or Section location.
- Resolve same-ticket created or modified groups from an explicit immutable workflow effective/mock projection; Flow-catalog absence alone is not a customer-valid failure.
- Build activation-neutral customer calculation without enum or production-catalog mapping; never use vendor, algorithm, scope, or recipe fallback.
- Keep the semantic-position-only terminal boundary.
- The Phase 3 WP-04 renderer is removed. Do not recreate vendor request rendering or execute Check Point API, publish, or installation operations.
- Route any newly proven schema, installer, RBAC, or UI impact through the applicable specialized skill before implementation.

## Phase acceptance

- Every customer statement is represented by a decision, recipe, test, failure, or exclusion.
- Each settled customer calculation branch has one complete immutable recipe construction path.
- Customer behavior extends rather than replaces the generic product.
- No customer zone-loading path is newly activated in production by WP-01.
- No rendering or firewall execution is activated.
