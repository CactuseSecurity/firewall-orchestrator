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

`RulePositioningContext` contains the implementation task, parent request task, target gateway, target management, and effective `GatewayDeviceSpecificSettings`. Management is derived from the request-task/gateway relationship; no `management_id` is added to the implementation task.

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

## 2026-10-19 - D-037 - Use the generic end-of-initial-rulebase recipe

Phase 3 WP-05 supports exactly `ProvisioningPositioningAlgorithm.DefaultEndOfRulebase` for Check Point gateway device type ID `9`. It maps to `EndOfBlock(CheckPointLayerScopeBlock(CheckPointLayerSelector.InitialLayer()))`, producing `Below` the last direct rule in a populated initial layer and `Bottom` in an existing empty initial layer. Every other named value, `Undefined`, and unknown numeric value is unsupported without fallback.

The mapping is supported by both the compiled `GatewayDeviceSpecificSettings` default and the existing localized UI statement that the default is the end of the rulebase; it is not inferred from the enum name alone.

Rationale: this is the only existing setting whose current product default and description establish genuinely generic graph-only behavior without opening the customer document or requiring zone input.

## 2026-10-19 - D-038 - Add a separate graph-only Check Point strategy

WP-05 adds a graph-only catalog and Check Point strategy that depend only on the production graph reader and immutable recipe execution. Only this strategy is registered with `RulePositioningManager`. The existing zone-capable `CheckPointRulePositioningStrategy` and its catalog remain unregistered Phase 4 preparation; existing zone services remain registered only for their non-positioning consumers.

The audit of `RuleTreeBuilder` found no safe extraction for WP-05. Its mutable, cached, presentation-oriented, synthetic-node/NAT behavior and tolerant missing-link semantics differ materially from the immutable strict positioning interpreter, so the two remain separate over the same normalized source data.

Rationale: a distinct exact-capability strategy enforces the graph-only boundary without destabilizing the already implemented customer-oriented seam or weakening topology validation.

## 2026-10-19 - D-039 - Treat the persisted implementation task as the gateway calculation unit

The concrete persisted `WfImplTask` supplies the target gateway for one positioning calculation. Its positive device ID must equal the supplied gateway, and its effective management remains `implTask.ManagementId ?? requestTask.ManagementId`. A finite explicit parent device list must contain that gateway, while empty, all-device, and workflow-expanded implicit selections do not invalidate a concrete gateway implementation task. Combined or targetless implementation tasks remain inapplicable.

Rationale: workflow expansion can create gateway-specific implementation tasks from parent selections that are not themselves a single-device list; validating the persisted unit avoids rejecting legitimate work while retaining strict identity checks.

## 2026-10-19 - D-040 - Clear stale positions after every completed failure

A successful calculation stores or replaces the semantic position. Every completed typed failure and unexpected non-cancellation failure attempts one `null` persistence to clear stale state. Cancellation before persistence begins preserves the prior value and propagates; after a mutation begins, the caller awaits its known result because the existing API mutation has no cancellation token. Persistence failure is reported safely without retry.

Rationale: a stale position is more dangerous than no position after a fresh failed attempt, while pre-write cancellation must not mutate state and automatic retry could duplicate or obscure workflow behavior.

## 2026-10-19 - D-041 - Keep positioning best-effort and defer only its workflow hook

Positioning and semantic-position persistence are best-effort relative to the surrounding workflow. Their failure must not roll back or fail otherwise successful workflow processing. The calculation/persistence caller is designed and registered independently, but remains unhooked until the workflow diagram is reviewed and one authoritative post-persistence implementation-task trigger is selected as the final WP-05 planning decision.

Rationale: the diagram may reveal the only reliable creation/expansion seam. Deferring that small hook avoids embedding an uncertain event assumption while preserving all settled generic design and failure semantics.

## 2026-10-19 - D-042 - Split callable completion from workflow activation

D-042 refines the packaging consequence of D-041. WP-05 is ready for implementation and owns the complete graph-only recipe, strategy, manager correction, independently callable context-loading/persistence service, production registration, failure policy, and renderer removal. It deliberately leaves that registered caller unreferenced by production workflow code.

New Phase 3 WP-06 owns a separate planning and implementation pair for the workflow activation hook after the workflow diagram is available. WP-06 must reuse the WP-05 caller and may add only the authoritative server-side invocation, applicability/idempotency behavior, focused tests, and documentation unless its planning proves a mandatory correction.

Rationale: the uncertain trigger does not prevent implementing or validating the self-contained calculation/persistence capability. Isolating activation allows WP-05 work to begin now without guessing the workflow event and preserves the two-session planning gate for the blocked feature.

## 2026-10-19 - D-043 - Complete the unhooked generic production capability

Phase 3 WP-05 implements D-036 through D-042. Production registration now contains exactly one graph-only positioning strategy for Check Point gateway type `9` and `DefaultEndOfRulebase`, plus an independently callable best-effort context-loading and semantic-position-persistence service. The zone-capable strategy remains unregistered, and no production workflow controller or handler invokes the caller.

The implementation removes the speculative WP-04 renderer and retains persisted semantic `RulePosition` as the terminal output. Focused coverage, a warning-free Debug build, the complete unit suite, and exclusion searches validate the package. WP-06 remains responsible for diagram-based activation planning and the sole authoritative post-persistence hook.

Rationale: completing calculation and persistence independently provides a testable generic capability without guessing workflow event semantics or introducing firewall execution behavior.

## 2026-10-20 - D-044 - Activate positioning from implementation-task creation

Phase 3 WP-06 activates positioning from the middleware implementation-task creation event emitted through `ActionHandler.DoStateChangeActions`, after `WfDbAccess.AddImplTaskToDb` has assigned a durable positive task ID and persisted its nested data. UI-originated creation delegates that event to middleware, while creation already running in middleware invokes the caller locally. The delegated request must retain `StateChangedByCreation`, and the existing state-execution claim remains the replay-suppression boundary.

The activation reuses the caller's single applicability policy and preserves the exact parent request context. Direct middleware creation carries its transient request, including merged bundle context; delegated UI creation reconstructs the same merged bundle from persisted siblings when necessary. Only initial creation activates positioning. Later task edits do not recalculate or clear a position, no manual retry surface is added, and recreating a task with a new ID is a new creation attempt.

Once task persistence succeeds, the best-effort positioning attempt is not cancelled by the originating client request. Typed outcomes and unexpected failures remain contained and safely logged so they cannot fail workflow processing, and no completed failure is retried automatically. This provides one attempt for each normally completed creation event, not crash-durable exactly-once delivery; an outbox, attempt ledger, schema change, or recovery job is outside WP-06.

Rationale: the creation event is the first shared post-persistence seam for manual and automatically expanded implementation tasks, matches the supplied planning-phase workflow, preserves bundle semantics, and activates the completed WP-05 caller without expanding into modification, UI, schema, or firewall-execution concerns.

## 2026-10-22 - D-045 - Canonicalize persisted positioning algorithms numerically

`RulePosition.PositioningAlgorithm` writes its numeric enum value through both Newtonsoft.Json and System.Text.Json, independent of any surrounding GraphQL enum naming policy. Reads remain backward compatible with integer values, numeric strings, C# enum field names, `EnumMember` values, and separator-normalized GraphQL names such as `DEFAULT_END_OF_RULEBASE` and `CHECK_POINT_END_OF_APP_SECTION`.

One shared lookup is built from the enum field names and `EnumMemberAttribute` values and is reused by known- and unknown-position readers. Unrecognized values still cause known position validation to fail, while unknown position types remain loadable with their raw payload. The converter is property-local: `ProvisioningPositioningAlgorithm` and device-specific-settings serialization are unchanged globally.

Rationale: persisted JSON must survive GraphQL serializer naming policies and later middleware loading, while already stored uppercase documents and forward-compatible unknown position types must not require a data migration.

## 2026-10-23 - D-046 - Preserve existing blocks and add customer matching behavior

Phase 4 customer matching extends the working structural block library without changing the semantics of its existing exact ordinal layer and Section selectors. The customer-required trimmed, invariant-uppercase Section-name behavior may use a separate block or an explicit selector mode, provided the existing behavior remains unchanged and covered by regression tests.

The customer block accepts an explicit parent-layer scope. It must not search all layers, choose the first inline layer, or silently fall back to the initial layer when the functional target is missing or ambiguous.

Rationale: the generic behavior is implemented and validated, while the customer convention has different matching semantics and an unresolved functional-target source. Additive behavior isolates that risk and preserves existing callers.

## 2026-10-23 - D-047 - Build customer calculation before selection and activation

Phase 4 first implements the customer-specific calculation primitives and activation-neutral recipe construction as directly testable, side-effect-free behavior. It does not yet map that behavior to an existing `ProvisioningPositioningAlgorithm`, register the zone-capable Check Point strategy, or invoke customer positioning from the production workflow.

A separate later plan will define how customer behavior is selected and triggered. The existing graph-only `DefaultEndOfRulebase` registration, workflow activation, and persistence path remain unchanged.

Rationale: calculation semantics can be built and validated independently, while the product selection model is intentionally being designed separately and must not be guessed from existing enum names.

## 2026-10-25 - D-048 - Require complete single persisted zones for customer calculation

The customer calculation proceeds only when both source and destination have `Complete` coverage and each resolves to exactly one distinct persisted zone from the configured designated zone matrix. Missing or empty sides, `Partial` or `None` coverage, zero matches, and multiple distinct matches fail without selecting the first result or falling back.

A persisted designated-matrix zone is eligible, including the persisted automatic Internet zone when present. `SyntheticInternetFallback` is ineligible because it is not a persisted designated-matrix zone. Identical source and destination zones are valid. Multiple input objects or ranges remain valid when their combined ranges resolve completely to the same one zone. Whether a customer exception permits multiple distinct source zones remains an explicit expert question; until amended, it fails under the strict baseline.

Rationale: the customer guarantees an exact source/destination zone result. Preserving and validating the resolver's richer cardinality and coverage data makes violations explicit instead of hiding ambiguity or uncovered address space.

## 2026-10-26 - D-049 - Select the functional Check Point layer by layer-guard coverage

The KfW functional Check Point target is not derived from a layer naming convention. It is the unique layer whose source layer guard contains every normalized source IP of the requested rule and whose destination layer guard contains every normalized destination IP. Section matching occurs only inside that selected layer.

Zero matching layers and multiple matching layers fail safely; neither case may choose the initial layer, the first inline layer, or a name-based fallback. A Section found in another layer does not override the functional target. This supersedes D-046 only where D-046 treated the functional parent scope as caller-supplied because its derivation was still unknown; D-046's additive normalized Section matching and preservation of existing exact selectors remain unchanged.

The current structural graph snapshot carries only rule identity and order, so this requirement proves that customer calculation needs immutable layer-guard source/destination semantics in addition to topology. The planner must define the smallest historically consistent projection and its normalization/containment rules before implementation. No database schema change is implied by this decision.

## 2026-10-26 - D-050 - Resolve same-ticket groups from workflow mock state

KfW tickets order object tasks before rule tasks, and provisioning creates or changes the objects before writing rules. Rule positioning happens earlier, so a group created or modified in the same ticket must be resolved from the workflow's effective/mock object state rather than being rejected merely because it is not yet available in the Flow catalog.

Existing catalogued groups may continue to use the Flow catalog. The Phase 4 planner must define an authoritative immutable projection for same-ticket object and group membership, including create/modify/delete effects and its precedence over catalog state, and pass it explicitly into calculation. Missing, ambiguous, cyclic, or otherwise unusable effective membership fails safely. Exact construction and ownership of that workflow projection remain a technical design task with the workflow/Flow expert; production activation remains deferred under D-047.

## 2026-10-26 - D-051 - Reject multiple distinct zones on either side

The KfW customer algorithm never accepts multiple distinct source zones or multiple distinct destination zones. Each side must have `Complete` coverage and resolve to exactly one distinct persisted designated-matrix zone. Multiple input objects or ranges are valid only when every range on that side is covered and the combined result is the same single zone.

This closes the exception left open by D-048. Multiple distinct zones fail safely without selecting, prioritizing, merging, or otherwise reducing the zone set.

## 2026-10-26 - D-052 - Exclude the main layer from the KfW algorithm

The KfW customer algorithm considers only inline layers reached through layer-guard rules. The main or initial Check Point layer is never a valid target and is not a fallback when no inline-layer guard matches. Exactly one guarded inline layer must match the requested source and destination ranges; zero or multiple matches fail safely.

This restriction applies only to the KfW algorithm. Generic FWO positioning may continue to target an initial layer through its separately configured behavior.

## 2026-10-26 - D-053 - Treat the ticket's final group state as authoritative

When a referenced group is created or changed in the same ticket, customer calculation uses the final intended group state after applying that ticket's ordered object and group tasks. This ticket-local state overrides the older Flow-catalog version. The protocol requires creators to define object and group changes before rules that consume them; a ticket that violates that protocol is not entitled to a guessed result and may fail positioning.

Groups are resolved recursively to leaf address ranges, including nested groups. Missing or ambiguous references, cycles, or any other condition that prevents one complete final expansion fail safely. After expansion, calculation uses the leaf ranges for both functional layer-guard matching and designated-zone resolution; group identity has no further placement meaning.

## 2026-10-26 - D-054 - Keep shared guard comparison address-family aware

KfW does not use IPv6 in this customer algorithm, so its customer acceptance scenarios may use IPv4 guard and request data. The reusable FWO normalization and range-containment primitives must nevertheless remain address-family aware and must not hard-code IPv4 behavior. IPv4 and IPv6 ranges are compared only within their own address families; unsupported or mixed-family input fails explicitly rather than being ignored or coerced.

## 2026-10-26 - D-055 - Apply normal rule-field semantics to KfW layer guards

KfW layer guards use normal Check Point source and destination fields. `Any` is explicitly supported and commonly appears on one side; for the corresponding address family it contains every requested range on that side. Ordinary objects and groups use their normal imported rule-field meaning and must be normalized before containment is evaluated.

KfW guarantees that each new rule handled by this algorithm is completely contained by exactly one layer guard. The calculator still validates that invariant and fails on zero, partial-only, or multiple complete matches. This guarantee is customer-specific: FWO must not generalize it to arbitrary Check Point inline rules, because rules may exist whose source or destination is only partially contained—or not contained at all—by their parent guard.

Support for every object type stored in FWO is not implied. Object kinds without a stable, correctly imported address-set meaning require an explicit resolver or fail safely. The exact KfW exclusions remain an expert follow-up, particularly for dynamic/updatable objects, domains, access roles, exclusion groups, negation, and other special objects.

## 2026-10-26 - D-056 - Trust the normalized fixed-import object model

Customer positioning accepts every object represented by the historically consistent imported FWO snapshot and interprets it according to that normalized database representation. It does not maintain a separate KfW object-type allow-list and does not attempt to detect or repair importer mistakes. If an importer loses or misrepresents vendor semantics, that is an importer defect owned outside the positioning calculation.

This supersedes D-055 only where it requested a KfW prohibition list as an implementation prerequisite. An object inventory may still define acceptance-test coverage, but the calculation boundary is capability-based: consume the normalized semantics the database actually provides, and never invent missing address meaning.

## 2026-10-26 - D-057 - Use three-state guard containment for IP-less objects

Each candidate guard side evaluates to `Contained`, `NotContained`, or `Indeterminate`. Known positive ranges or `Any` produce `Contained` when they already cover every requested range, even if the same side also contains an unresolved positive object. If known ranges leave requested space uncovered and an IP-less positive object could cover it, the result is `Indeterminate`. `NotContained` requires complete known semantics and at least one uncovered requested range.

Negation, exclusion, or other subtractive semantics are `Indeterminate` whenever an unresolved term could change the answer. A layer is a definite match only when both sides are `Contained`. Selection succeeds only when exactly one layer definitely matches and every other candidate is definitely non-matching; any competing indeterminate candidate prevents proof of uniqueness and fails safely.

An IP-less object in the new rule itself cannot satisfy the customer's complete single-zone prerequisite unless an earlier authoritative resolver supplies its concrete ranges. A future dynamic-object resolver may turn indeterminate input into ranges without changing this containment contract.

## 2026-10-26 - D-058 - Limit Phase 4 WP-01 to Check Point

Phase 4 WP-01 implements only the KfW Check Point `SECTION_NATIVE` calculation. The customer rulebook's Fortinet `SECTION_LABEL` branch is explicitly deferred to a separate future work package and is not planned for the current sprint or quarter.

WP-01 therefore does not add a Fortinet semantic-position contract, policy-label snapshot, first-contiguous-label-block selector, recipe, strategy, registration, or tests. Deferral accounts for those customer statements without silently treating them as implemented.

## 2026-10-27 - D-059 - Use one canonical effective rule-address input

KfW zone resolution and layer-guard containment consume the same immutable normalized source and destination range input. A ticket-aware resolver constructs that input once from the access task and the ticket's ordered network object/group tasks. It resolves direct ranges, ticket-local effective objects and groups, and unchanged Flow-catalog groups before either placement decision runs. The two consumers must not independently expand workflow elements or groups.

Ticket tasks are folded in ascending `TaskNumber`, then ascending positive task ID as the deterministic tie-breaker. A relevant object/group task ordered after the consuming access task is a protocol-order failure. Ticket-local create/modify/delete state overrides the catalog; group definitions use their canonical source-field members. Identity correlation prefers positive Flow IDs, then positive normalized-object IDs, and uses trimmed case-insensitive names only when exactly one candidate remains. Missing identity, conflicting final definitions, empty final groups, cycles, invalid actions, and incomplete leaf ranges are typed failures rather than guessed input.

Rationale: both the zone pair and functional layer describe the same requested traffic. A shared immutable range projection prevents the rule from resolving to one meaning for zones and another for layer guards. It also isolates workflow mock-state behavior from the existing Flow persistence path.

## 2026-10-27 - D-060 - Load guard semantics at the graph's fixed import

`RulebaseGraphReadSuccess` exposes the positive import control ID selected by the existing structural reader. A separate Check Point layer-guard reader receives that ID and the immutable graph, selects only source rules of inline links, and loads their source/destination negation flags and normalized address terms using the same half-open historical interval. It returns a separate immutable guard snapshot keyed by guarded inline-layer ID; it does not add semantic fields to every structural graph rule and does not make the generic graph-only path perform extra queries.

The guard projection represents each side as a rule-side negation flag plus immutable top-level address terms. A term retains known included ranges, known excluded ranges from normalized negated flat memberships, and separate unresolved inclusion/exclusion flags. This term boundary prevents one group's exclusion from being incorrectly subtracted from a separate positive object on the same rule side. Imported `Any` is represented by its normalized full-family range. Groups use the fixed-import flattened membership already stored by FWO. Blank IP data is unresolved; malformed nonblank ranges, scope/history mismatches, duplicate guards for one inline layer, missing inline guards, or unexpected returned rows invalidate the snapshot. No importer repair or customer object-type allow-list is added.

Rationale: the import ID is the consistency token between topology and guard semantics. A separate projection keeps the Phase 3 structural graph small and preserves the generic path's current query cost and behavior.

## 2026-10-27 - D-061 - Compose the KfW recipe from additive customer primitives

The activation-neutral KfW recipe is:

`EndOfBlock(CheckPointSectionScopeBlock(CheckPointLayerGuardScopeBlock(), NormalizedName(CheckPointSectionName.Build(destinationZone, sourceZone))))`

`CheckPointLayerGuardScopeBlock` has no child, considers only inline links, applies three-state containment to both sides, and returns the existing Check Point scope value for the uniquely selected layer. More than one definite match is `ScopeAmbiguous`; a competing indeterminate candidate is `ScopeIndeterminate`; zero definite matches with no indeterminate candidate is `ScopeNotFound`. Invalid or cross-scope snapshots remain `InvalidSnapshot`.

The existing Section block gains an explicit normalized-name selector mode. Its exact-name and UID modes remain ordinal and unchanged. The canonical builder emits `"[DESTINATION] < [SOURCE]"`; normalized comparison trims both complete names and applies invariant uppercase. Zero and multiple normalized matches use the existing `ScopeNotFound` and `ScopeAmbiguous` failures. The existing `EndOfBlock` produces the semantic end-of-Section anchor, including its already-defined empty-Section behavior.

Recipe construction validates complete coverage, exactly one persisted zone on each side, and a nonblank canonical name. It is a direct factory, not an enum catalog mapping. WP-01 adds no production strategy registration, workflow caller change, persistence change, or activation fallback.

## 2026-10-28 - D-062 - Select customer behavior through a new stable algorithm value

`CheckPointLayerGuardEndOfZonePairSection` is the product-facing behavior name for the KfW Check Point recipe. Its device-specific-settings wire value is `cpLayerGuardEndOfZonePairSection` and its persisted numeric semantic-position value is `7`. Existing members are explicitly fixed at values `0` through `6`.

The customer strategy supports exactly Check Point gateway device type `9` plus this new value. The graph-only strategy continues to support exactly `DefaultEndOfRulebase`; the two capability sets must not overlap and neither strategy provides fallback.

Rationale: the behavior needs an understandable selectable identity, while explicit ordinals prevent later enum edits from reinterpreting stored semantic positions.

## 2026-10-28 - D-063 - Load management-specific ticket state only for customer positioning

After effective settings resolution, the workflow caller loads the full ticket only for `CheckPointLayerGuardEndOfZonePairSection`. It validates ticket identity, keeps positive tasks for the effective management in deterministic task-number/ID order, and passes them as an immutable optional context collection.

The caller-supplied parent Rule A remains authoritative for the rule being positioned, including transient bundled/merged content. The persisted ticket slice supplies same-ticket object and group state. `DefaultEndOfRulebase` performs no ticket query.

Rationale: bundled rule content exists at the creation seam while same-ticket group state is durable ticket data. Keeping both inputs explicit preserves each authority and protects the generic path from customer-only work.

## 2026-10-28 - D-064 - Activate calculation without expanding the terminal boundary

The production customer orchestrator reads the graph and its exact successful import, resolves canonical effective ranges, reads layer guards at that import, loads the designated-zone matrix, resolves zones from the same ranges, selects the exact customer recipe, executes it, and verifies that the returned semantic position retains algorithm value `7`.

It reuses the existing creation trigger and dedicated `rule_position` mutation. Failures have no fallback or retry and clear stale positions through the established best-effort workflow behavior. Rendering, Check Point API calls, publish/install, auditing, manual recalculation, and schema changes remain out of scope.

Rationale: WP-01 already completed calculation semantics; WP-02 connects those semantics to the validated production seam without reopening persistence or firewall-execution design.
