# Firewall rule positioning decisions

This is an append-only decision log. Add new entries for new decisions, amendments, or reversals; do not rewrite older entries. A later entry may explicitly supersede an earlier one.

## 2026-10-05 — D-001 — Repository documents are authoritative

The active files in this directory are the source of truth across sessions. Conversation history is not authoritative, and the archived proposal is historical only.

Rationale: implementation planning must survive context loss and agent/session changes without reviving superseded scope.

## 2026-10-05 — D-002 — Use a two-phase roadmap

Phase 1 builds the generic calculation and persistence foundation. Phase 2 is limited initially to the future analysis of the customer document, implementation of small Check Point helpers, and composition of those helpers into `CheckPointRulePositioningStrategy`.

Rationale: the reusable platform seam can be agreed and tested independently of the customer-specific algorithm.

## 2026-10-05 — D-003 — Keep calculation and persistence independent

The caller loads `RulePositioningContext`, invokes calculation, and decides whether and when to persist. A successful position may be stored; a failure may cause the caller to clear a stale position by storing `null`. Calculation itself never writes the task.

Rationale: a side-effect-free calculation API is easier to test, reuse, retry, and integrate safely.

## 2026-10-05 — D-004 — Context is explicit and management is not duplicated

`RulePositioningContext` contains the implementation task, parent request task, target gateway, target management, and effective `GatewayProvisioningSettings`. Management is derived from the request-task/gateway relationship; no `management_id` is added to the implementation task.

Rationale: callers provide the resolved context while the manager verifies consistency without creating a second source of truth.

## 2026-10-05 — D-005 — Strategy registration is exact

`RulePositioningManager` receives registered `IRulePositioningStrategy` implementations and contains no vendor switch. Exactly one strategy must match the device type and configured positioning algorithm. Zero matches returns `PositioningMethodNotImplemented`; multiple matches returns `StrategyRegistrationAmbiguous`. There is no fallback to another vendor or algorithm.

Phase 1 has no production strategy. Check Point-like and Fortigate-like strategies exist only in tests.

Rationale: explicit capability matching prevents silent cross-vendor or cross-algorithm behavior changes.

## 2026-10-05 — D-006 — Persist a forward-compatible polymorphic position

`RulePosition` is an abstract persisted model with a stable position-type discriminator, schema version, and positioning-algorithm identifier. Vendor-specific subtypes are supported without defining their future payloads now. Unknown types load as `UnknownRulePosition` and remain round-trippable through both JSON serializers used by FWO.

Rationale: older clients must continue loading implementation tasks created by newer position producers.

## 2026-10-05 — D-007 — Phase 1 supports only new access rules

Both tasks must describe the same new access-rule request. Modification, deletion, object, and unknown task/action types return `UnsupportedTask` before any strategy is invoked.

Rationale: declining unsupported work explicitly is safer than guessing how it should be positioned.

## 2026-10-05 — D-008 — Failures are typed and diagnostics are safe

The shared categories are `InvalidInput`, `UnsupportedTask`, `ConfigurationInvalid`, `PositioningMethodNotImplemented`, `StrategyRegistrationAmbiguous`, and `CalculationFailed`. Diagnostics must not expose credentials or complete rule contents. Calculation failures are returned, not persisted in the task.

Rationale: callers need stable branching behavior without turning operational diagnostics into a data-leak path.

## 2026-10-05 — D-009 — Store position in a dedicated nullable column

Add nullable `request.impltask.rule_position` (`jsonb`) and propagate it through Hasura permissions, implementation-task GraphQL operations, and `WfImplTask` with both JSON attribute families. A dedicated operation writes or clears only `rule_position`, without updating unrelated task fields or invoking workflow actions.

Position-change auditing is explicitly excluded from Phase 1. This must be reconsidered after consultation with the audit team; the eventual audit event, payload, retention, and actor semantics remain unresolved.

Rationale: minimal persistence avoids accidental workflow transitions, while the audit requirement needs domain-owner input before a durable contract is chosen.

## 2026-10-05 — D-010 — Reuse existing provisioning configuration

The hierarchical provisioning configuration and `ProvisioningPositioningAlgorithm` enum are reused unchanged.

Rationale: Phase 1 introduces a consumer of the existing effective configuration rather than a competing configuration model.

## 2026-10-05 — D-011 — Plan version 9.5.11 and validate it before coding

The schema upgrade, product metadata, and revision history target the next currently unclaimed product version, `9.5.11`. Its availability must be checked again at implementation start.

Rationale: version ownership can change while this work remains in planning.

## 2026-10-05 — D-012 — Defer the customer document completely

Do not analyze `FWO_Regel_Platzierung_Regelwerk.md` during Phase 1 planning or implementation. Bring it back only when Phase 2 is explicitly started.

Rationale: customer-algorithm details must not distort the generic Phase 1 contracts.

## 2026-10-05 — D-013 — Preserve but demote the previous proposal

The previous WP-00 through WP-08 proposal and its vendor-extension note are preserved verbatim under `archive/previous-proposal/`. They are not approved work and do not override active documents.

Rationale: historical reasoning remains recoverable without creating two competing plans.

## 2026-10-05 — D-014 — End planning sessions with a committed recovery point

At the end of each firewall-rule-positioning planning session, append any changed decisions to this file, replace `SESSION-HANDOFF.md` with the current recovery point, and commit only the resulting planning-document changes. Unrelated working-tree changes must remain outside that commit.

Rationale: a committed repository checkpoint makes the documented plan recoverable without depending on an agent's conversation state or the user's local staging workflow.

## 2026-10-12 — D-015 — Retarget persistence migration to 9.7.1

D-015 supersedes D-011. After rebasing onto `Cactus/develop` at `ed03a8529`, `9.5.11` was already sealed and the current open product version was `9.7.1`. The nullable, idempotent `request.impltask.rule_position` migration is therefore appended to `9.7.1.sql`; `product_version` remains `9.7.1`, and the feature notes belong under the existing `9.7.1` revision-history section.

Local validation passed on 2026-10-12: 78 focused tests and the complete unit suite (7,366 passed, 29 skipped) succeeded, the Debug solution build completed with no warnings or errors, changed C# files passed formatting, and new production files reached 91.77% line coverage. Disposable-VM fresh-install and `9.7.0` to `9.7.1` upgrade validation was not executed because the FWO VM tools were unavailable, so Phase 1 remains open.

Rationale: migrations must follow the active upstream release line, and completion may not be claimed without the installer gates required by the approved Phase 1 plan.

## 2026-10-12 — D-016 — Rephase customer behavior to Phase 3

D-016 supersedes the Phase 2 scope in D-002 and D-012. Phase 2 now builds the generic block-composition framework, immutable Check Point snapshot, semantic position, and minimal structural blocks. Customer-document analysis, customer-specific recipes, enum-to-recipe mapping, and production strategy wiring move to deferred Phase 3. `FWO_Regel_Platzierung_Regelwerk.md` remains unopened during Phase 2.

Rationale: structural composition can be designed and verified independently from customer-specific policy while preserving a deliberate boundary before production activation.

## 2026-10-12 — D-017 — Snapshot a typed recipe tree at construction

An in-memory recipe is rooted at `IPositioningBlock<TContext,TOutput>`. Every block declares a stable kind, output role, ordered children, and required child roles. Recipe construction validates null children, declaration counts, role compatibility, and cycles, then snapshots the execution tree so later mutation of a block's child collection cannot change the recipe.

Rationale: a validated immutable plan keeps executor behavior deterministic and supports future zero-, one-, and multi-child blocks without executor changes.

## 2026-10-12 — D-018 — Use one immutable gateway-specific Check Point snapshot

All Phase 2 Check Point blocks inspect one caller-supplied snapshot identified by management, gateway, and initial-layer identity. The snapshot preserves ordered direct-rule/section interleaving, copies all collections, and validates identifiers, rule order, and inline-layer relationships. Loading it from GraphQL or imported data remains deferred.

Rationale: blocks remain side-effect-free and testable while the eventual policy-loading boundary stays explicit.

## 2026-10-12 — D-019 — Fail fast with originating block identity

Block results use the stable categories `InvalidRecipe`, `InvalidSnapshot`, `ScopeNotFound`, `ScopeAmbiguous`, `NoInsertionPoint`, and `UnexpectedExecutionFailure`. Child failures propagate unchanged and stop sibling and parent evaluation. Caller cancellation propagates, while unexpected exceptions become safe diagnostics attributed only to the originating block kind.

Rationale: recipes must not hide data-quality or scope errors by falling back to another branch, parent scope, or placement method.

## 2026-10-12 — D-020 — Persist semantic Check Point anchors

`CheckPointRulePosition` uses discriminator `check-point.rule-position.v1` and stores layer UID, the existing positioning algorithm, and one semantic anchor: `Top`, `Bottom`, `Above`, or `Below`. Above/below require a rule UID; top/bottom prohibit one. Conversion to a Check Point API request is deferred.

Rationale: semantic anchors are stable across loading and persistence and avoid prematurely coupling Phase 2 to a vendor request payload.

## 2026-10-12 — D-021 — Start with structural scope and boundary blocks

The initial production block library selects an initial/name/UID layer, selects a name/UID section within its exact parent, and resolves the beginning or end of that scope. Names use ordinal exact matching and must resolve once. Empty sections use their single canonical gap: above the next rule, otherwise below the previous rule, otherwise top.

Rationale: these blocks establish useful composition semantics and acceptance examples without guessing the customer's algorithm or mapping enum values to recipes.

## 2026-10-12 — D-022 — Do not create automatic recovery commits

D-022 supersedes the automatic-commit direction in D-014. Planning and implementation changes remain unstaged and uncommitted unless the user explicitly requests a commit.

Rationale: the repository contribution workflow reserves staging and commits for the user unless they grant explicit authorization.

## 2026-10-13 — D-023 — Keep existing-rule semantics out of creation placement

The current and anticipated customer behavior calculates the new rule's source/destination zones, maps that zone pair to a structural scope, and places the rule at a boundary. It requires workflow elements, resolved zones, rulebase topology, and existing rule UID/order anchors, but not existing rules' source, destination, service, action, zone, NAT, or other semantic content. Those fields remain outside the positioning snapshot until a concrete block requires comparison with existing rule contents.

Rationale: FWO can load rich existing-rule data, but exposing it now would widen the calculation contract without serving the agreed creation-only behavior.

## 2026-10-13 — D-024 — Replace the Check Point snapshot with an immutable rulebase graph

D-024 supersedes the Check Point-specific snapshot shape in D-018 while retaining its immutable, single-calculation boundary. Before Phase 3, replace `CheckPointPolicySnapshot` and its layer/section/rule snapshot types with a vendor-neutral `RulebaseGraphSnapshot` constructed from already-loaded normalized `Rulebase`, `RulebaseLink`, and structural rule identity/order data. Check Point-specific blocks interpret layers, sections, and inline layers from this graph and continue producing `CheckPointRulePosition`.

The Phase 2 follow-up remains I/O-free. It does not use the report-oriented `RuleTreeBuilder` output as the positioning model, but follows the same normalized link semantics and retains strict ambiguity failures.

Rationale: the calculation still needs defensive copying and deterministic validation, but rulebases and links are the existing normalized source of structural truth and should not be replaced by a parallel per-vendor policy model.

## 2026-10-13 — D-025 — Accept the latest successful import regardless of age

When Phase 3 adds production loading, positioning uses the latest successfully completed FWO import regardless of its age. Do not add a maximum-age setting, stale-import rejection, automatic re-import, or a required snapshot fingerprint. Missing successful import data or an internally inconsistent graph fails explicitly.

Rationale: imported policy data is sufficient for the agreed positioning use case, and freshness policy would add complexity without a current product requirement.

## 2026-10-15 — D-026 — Retarget persistence migration to 9.7.3

D-026 supersedes D-015. After rebasing onto current `Cactus/develop`, `9.7.1` and `9.7.2` were sealed and `9.7.3` was the open product version. The nullable, idempotent `request.impltask.rule_position` migration therefore belongs in `9.7.3.sql`, with feature notes under `9.7.3` and upgrade acceptance from `9.7.2` to `9.7.3`.

Rationale: feature branches must not modify sealed upgrade scripts, and the schema change must follow the active release's fresh-install and upgrade paths.

## 2026-10-15 — D-027 — Use the implementation task's effective target management

The upstream `request.impltask.mgm_id` field is preserved alongside `rule_position`. Positioning derives the target as `implTask.ManagementId ?? requestTask.ManagementId`. That effective ID must agree with the supplied management and the gateway's management; any loaded request-management object must independently agree with the request task's own management ID.

Rationale: implementation tasks may legitimately target a management different from their parent request, while contradictory caller-supplied context must still fail before strategy selection.

## 2026-10-16 - D-028 - Use paired planning and implementation briefs for Phase 3

Phase 3 is divided into five ordered work packages: production snapshot reading, creation-rule zone resolution, inactive Check Point strategy orchestration, semantic Check Point position rendering, and final customer recipes plus activation. Each package has a planning brief and a separate implementation brief.

The planner performs repository discovery, settles decisions, and makes the paired implementation brief self-contained without changing production code. The implementer reads the settled implementation brief and does not need the planning brief. An implementation brief may not be executed while it is marked draft or contains blocking placeholders.

`FWO_Regel_Platzierung_Regelwerk.md` remains unopened through WP-04. It is analyzed only in the WP-05 planning session, which converts the customer behavior into normative recipes, enum mappings, failure cases, and acceptance tests for the final implementation session.

Rationale: separating discovery from execution gives each work package a recoverable planning gate, keeps implementers focused on settled requirements, and delays customer-specific policy until all reusable production infrastructure is ready.

## 2026-10-16 - D-029 - Anchor production graph reads to one completed rule import

The production snapshot reader selects one management-scoped type-1 rule import with `successful_import = true` and non-null `stop_time`, ordered by completion time and then control ID. It captures that control ID once and loads gateway links, endpoint rulebases, and structural rules using `created <= import_id` and `removed IS NULL OR removed > import_id` (with `rule_create` for rules).

The reader validates gateway-management ownership, historical intervals, exact endpoint completeness, and source-rule ownership before constructing the existing `RulebaseGraphSnapshot`. It returns a typed success or safe typed failure, while caller cancellation remains exceptional. The existing GraphQL schema and middleware-server permissions are sufficient; no schema or Hasura metadata change is required.

Rationale: a fixed import ID reconstructs the completed post-import state even if a newer failed or in-progress import is changing current rows, and explicit validation prevents partial historical data from becoming a calculation input.
