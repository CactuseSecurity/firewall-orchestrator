# Session handoff

Last updated: 2026-10-05

## Current status

The documentation source of truth has been established. The previous WP-00 through WP-08 proposal is archived, the two-phase roadmap is active, and the three Phase 1 work packages are documented. This session changed documentation only; no production code, schema, metadata, or tests were changed.

Phase 1 is approved as a plan and remains unimplemented. Phase 2 is deferred. `FWO_Regel_Platzierung_Regelwerk.md` has not been analyzed.

## Latest decisions

- Repository documents, not conversation history, are authoritative (D-001).
- Calculation and persistence remain separate (D-003).
- Exactly one registered vendor/algorithm strategy may run, and Phase 1 registers no production strategy (D-005).
- Unknown position types remain loadable and round-trippable (D-006).
- Position auditing is deferred pending audit-team consultation (D-009).
- Version `9.5.11` must be reconfirmed as unclaimed at implementation start (D-011).
- Each planning session ends with a focused committed documentation recovery point (D-014).

See `DECISIONS.md` for the complete rationale and decision history.

## Open questions

- What event, actor, payload, and retention semantics will the audit team require for future position-change auditing? This does not block Phase 1 because auditing is out of scope.
- Is `9.5.11` still the next unclaimed product version when implementation begins?

## Next action

Perform a read-only repository validation of the Phase 1 plan before coding. Locate and confirm the current serializer conventions, `WfImplTask` copy paths, task relationship fields, effective provisioning settings, Hasura implementation-task permissions, GraphQL fragments/mutations, schema/version files, and the narrowest persistence seam. Update the active work packages, append changed decisions, and replace this handoff with the evidence-backed recovery point. Do not implement code and do not analyze the customer document during that planning pass.

## Files to read

Read in this order:

1. `.agents/AGENTS.md`
2. `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`
3. `documentation/work-packages/firewall-rule-placement/README.md`
4. `documentation/work-packages/firewall-rule-placement/DECISIONS.md`
5. `documentation/work-packages/firewall-rule-placement/phase-1/README.md`
6. `documentation/work-packages/firewall-rule-placement/phase-1/WP-01-contracts-and-serialization.md`
7. `documentation/work-packages/firewall-rule-placement/phase-1/WP-02-positioning-manager.md`
8. `documentation/work-packages/firewall-rule-placement/phase-1/WP-03-position-persistence.md`

The archived proposal is historical only. Leave unrelated working-tree changes untouched.
