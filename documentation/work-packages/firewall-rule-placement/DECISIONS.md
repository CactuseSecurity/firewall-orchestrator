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

## 2026-10-18 - D-030 - Reuse compliance zone and Flow group resolution for creation placement

Phase 3 WP-02 resolves active source and destination elements into immutable ordered zone sets rather than requiring one exact zone pair. It preserves multiple and overlapping matches for the later customer-policy decision. Existing request-visible Flow network groups are expanded through `IFlowGroupResolver`, with positive IDs preferred over names and selector calls chunked to the existing limit.

Address normalization reuses workflow CIDR precedence and `ComplianceCheck.ParseIpRange`. Zone matching reuses `ComplianceZoneResolver` directly, including the persisted automatic-Internet zone when enabled, the synthetic Internet/local fallback for uncovered IPv4 when disabled, and unassignable uncovered IPv6 ranges. The positioning result gives the synthetic fallback an explicit non-localized kind and reports complete, partial, or absent coverage without choosing a section or recipe.

Groups created in the same ticket but not yet available in the Flow catalog are treated as unresolved in WP-02. Stakeholders must confirm whether cross-task pre-catalog group resolution is required before WP-05 activates the production workflow.

Rationale: the established compliance and Flow seams already implement the required general behavior. Reusing them prevents a competing zone algorithm while preserving unresolved and multi-zone information for the final customer-specific mapping.

## 2026-10-18 - D-031 - Keep Check Point strategy capability catalog-backed and inactive

Phase 3 WP-03 adds a shared cancellation-aware designated-zone-matrix reader because the Phase 1 strategy context contains no zone rows and the WP-02 resolver deliberately accepts caller-loaded data. The reader becomes the single owner of the existing designated-matrix queries and is reused by `ComplianceZoneService`. It is registered as a singleton, while the Check Point strategy, recipe catalog, positioning manager, and workflow caller remain unregistered.

`CheckPointRulePositioningStrategy` handles only target device type ID `9` and only algorithms reported complete by its injected recipe catalog. MDS type ID `13` is not a target-gateway capability. The catalog is the sole supported-algorithm source; WP-03 defines its typed selection seam but supplies no implementation, customer recipe, zone mapping, or enum mapping. WP-05 must populate the catalog before it may register the strategy.

Each calculation runs sequentially and fail-fast: one graph read, one designated-zone read, one creation-rule zone resolution, one recipe selection, and one recipe execution at most. Caller cancellation propagates. Invalid request/address input maps to `InvalidInput`, unsupported task types to `UnsupportedTask`, invalid matrix/catalog/recipe configuration to `ConfigurationInvalid`, absent recipe mappings to `PositioningMethodNotImplemented`, and operational, graph, scope, unresolved-group, or unexpected failures to `CalculationFailed`. A success is accepted only when the returned `CheckPointRulePosition` retains the exact configured algorithm.

Rationale: one data-loading boundary avoids duplicated matrix-query behavior, catalog-backed capability prevents incomplete strategies from advertising support, and deterministic coarse Phase 1 translations keep child details safe without adding public failure categories.

## 2026-10-19 - D-032 - Render Check Point positions through a pure inactive request-body seam

Phase 3 WP-04 renders `CheckPointRulePosition` into the Check Point Management API `add-access-rule` body by writing the exact layer UID to `layer` and mapping anchors to `position`: `"top"`, `"bottom"`, `{ "above": "<rule-uid>" }`, or `{ "below": "<rule-uid>" }`. Inline-layer insertion also uses the target inline layer UID in `layer`; the separate `inline-layer` field describes a parent rule's action and is not an insertion target. Top and bottom are always explicit rather than relying on vendor defaults.

The renderer is a synchronous, dependency-free `FWO.ExternalSystems.CheckPoint` helper. It accepts an already-rendered request body, one explicitly selected nullable `RulePosition`, and the caller's expected algorithm. Null returns a deep-cloned legacy body unchanged. Non-null unknown/wrong-vendor, malformed, or algorithm-mismatched positions fail with fixed safe `ConfigException` messages. Valid rendering clones the body, replaces only `layer` and `position`, is idempotent, and never mutates the persisted position.

WP-04 does not inspect `WfReqTask.ImplementationTasks`, choose among gateway-specific positions, invoke `CheckPointClient`, register a strategy or renderer, add an endpoint, or connect the workflow. WP-05 must select the applicable implementation task and effective algorithm before invoking this seam. The recipe catalog remains the sole source of supported algorithms; the renderer checks identity only and carries no algorithm allowlist.

Rationale: the vendor request shape can be implemented and exhaustively tested independently of the unresolved activation context, while an explicit expected algorithm and no silent fallback prevent stale or foreign persisted positions from reaching a future API call.

## 2026-10-19 - D-033 - End the generic product at persisted semantic output

Phase 3 production activation ends after calculating and independently persisting a semantic `RulePosition` on the applicable gateway-specific implementation task. The WP-04 Check Point renderer remains an inactive adapter and is not connected to the workflow. Phase 3 must not call `CheckPointClient`, execute `add-access-rule`, publish, install policy, or weaken the existing rule-change execution gate.

D-033 narrows the future-activation direction in D-032: a later placer integration may choose to consume the semantic position and use or replace the dormant renderer, but that downstream contract is not part of the current generic product.

Rationale: rule positioning produces placement intent; applying that intent to a firewall is a separate product responsibility and must not be activated implicitly.

## 2026-10-19 - D-034 - Separate generic completion from the customer extension

D-034 supersedes the Phase 3 packaging and customer-document timing in D-016 and D-028 while preserving their reusable-first intent. Phase 3 WP-05 now completes a generic semantic-positioning product: it confirms at least one genuinely generic existing enum mapping, implements the corresponding production catalog recipe, registers the calculation path, connects the approved workflow caller, and persists semantic output.

The customer document remains unopened throughout Phase 3. A new Phase 4 WP-01 analyzes it only after the generic product is implemented and validated, then adds only the missing customer-specific blocks, recipes, and enum mappings. Phase 4 reuses the Phase 3 activation and persistence path and does not activate request rendering or firewall execution.

Rationale: the infrastructure should become a usable, testable product independently of one customer's special algorithm. Customer behavior is then an extension rather than the event that first makes the entire pipeline operational.

## 2026-10-19 - D-035 - Keep generic recipes independent of zone input

Phase 3 generic recipes consume the imported rulebase graph and no `CreationRuleZoneInput`. The generic runtime path must not load the designated zone matrix, expand Flow groups, invoke compliance-zone resolution, or require zone configuration. The zone reader, creation-rule zone resolver, and zone-dependent orchestration implemented earlier remain inactive preparation for Phase 4 customer algorithms.

Phase 3 WP-05 must isolate the graph-only generic capability from the currently zone-dependent Check Point strategy shape. It may refactor the orchestration seam or introduce a distinct exact-capability strategy, but it must preserve `RulePositioningManager`'s exact-one-match behavior and must not register zone dependencies for the generic path.

Rationale: zone input was built for known future customer behavior, not because generic structural placement requires it. Making it a generic prerequisite would add avoidable configuration, queries, and failure modes.

## 2026-10-19 - D-036 - Remove the speculative Check Point renderer

D-036 supersedes the retention option in D-033 and the future-caller direction in D-032. Phase 3 WP-05 removes `CheckPointRulePositionRenderer`, its dedicated unit tests, and renderer-only compatibility assertions that no longer protect retained behavior. It does not replace them with another Check Point payload adapter.

A future placer must define and own any conversion from persisted semantic position to vendor request shape. Phase 4 remains semantic-output-only and must not recreate rendering as part of the customer algorithm.

Rationale: no current product path owns Check Point rule execution, so retaining a predesigned API adapter creates maintenance cost and prematurely constrains the later placer contract.
