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
