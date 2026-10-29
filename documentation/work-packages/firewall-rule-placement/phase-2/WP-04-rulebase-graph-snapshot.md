# WP-04 — Generalize the snapshot to a rulebase graph

## Status and outcome

Implemented and locally validated on 2026-10-14. This completes the required Phase 2 follow-up before Phase 3 starts.

Replace the Check Point-specific structural snapshot with one immutable, gateway-scoped `RulebaseGraphSnapshot` constructed from FWO's normalized `Rulebase`, `RulebaseLink`, and structural `Rule` identity/order data. Check Point blocks continue producing `CheckPointRulePosition`, but interpret Check Point layers, sections, and inline layers as a vendor-specific view of the shared graph.

## Why this follow-up exists

The current `CheckPointPolicySnapshot` is a valid immutable calculation boundary, but it independently models layers and sections that FWO already represents through rulebases and links. That naming and shape suggest that every vendor needs a separate snapshot model.

The follow-up keeps the useful properties of the snapshot—one consistent input, defensive copying, eager validation, deterministic evaluation, and no I/O inside blocks—while making the captured data match the normalized source of truth.

## Public contract changes

- Add `RulebaseGraphSnapshot` under the vendor-neutral rule-positioning data namespace.
- The snapshot contains management ID, gateway ID, immutable rulebase nodes, immutable links, and the structural rules belonging to each rulebase.
- A rulebase node contains its existing ID, UID, name, and rules in native `RuleNumNumeric` order.
- A structural rule contains only ID, UID, and native order. Do not copy source, destination, service, action, zone, NAT, or other rule semantics.
- A link preserves its existing source rulebase or source rule identity, target rulebase identity, `RulebaseLinkTypes` value, and the `IsInitial`, `IsGlobal`, and `IsSection` flags needed to interpret the graph.
- Provide a pure construction boundary accepting already-loaded `Rulebase` and `RulebaseLink` collections. It performs no GraphQL, database, importer, or vendor API access.
- Change `CheckPointPositioningContext.Snapshot` to the generic graph snapshot. Keep the context, block values, structural block names, and `CheckPointRulePosition` vendor-specific.
- Remove the public `CheckPointPolicySnapshot`, `CheckPointLayerSnapshot`, `CheckPointSectionSnapshot`, and `CheckPointRuleSnapshot` types after their consumers and tests have migrated. No compatibility shim is required because Phase 2 has no registered production strategy or external consumer.

## Graph and Check Point interpretation

- Treat `Rulebase` and `RulebaseLink` as the source data; the snapshot is an immutable projection, not a competing policy model.
- Copy primitive values and collection contents so later mutation of the supplied FWO entities cannot change a calculation.
- At generic snapshot construction, require positive management/gateway identities, matching management/gateway ownership, unique rulebase and structural-rule identities, nonblank rulebase/rule UIDs, deterministic native rule order, and link endpoints that resolve within the supplied graph.
- In the Check Point interpretation, require exactly one initial link for a positionable gateway. Reject ambiguous ordered/domain successors, section successors, or multiple inline links for one rule instead of choosing one.
- If the initial link is a `Policy` link, treat its target as the policy container and resolve the unique first ordered/domain successor as the initial selectable access layer. Otherwise treat the initial target itself as the initial layer.
- Interpret ordered/domain links as layer succession, `IsSection` links as the ordered section chain, and inline links as the layer attached to their source rule. Reuse the shared `RulebaseLinkTypes` constants.
- Preserve the existing exact name/UID selection and beginning/end behavior, including empty-layer and empty-section anchors.
- Do not feed the report-oriented `RuleTreeBuilder` output into positioning. It adds mutable caching, synthetic presentation nodes, and report numbering. Keep it unchanged in this follow-up; use its tested link semantics as the behavioral reference and cover parity with focused graph tests.

## Data boundary for the intended customer behavior

Current and anticipated creation-only placement needs:

1. the new rule's workflow elements;
2. a resolved source/destination zone pair;
3. the rulebase graph; and
4. existing rule UIDs/order for semantic anchors.

Existing rules' source, destination, service, action, zone, or other semantics are deliberately excluded. Add them only if a concrete later block must compare the new rule with existing rule contents.

Resolving the new rule's zones and mapping a zone pair to a section/recipe remain Phase 3 work. The existing workflow context already carries the rule elements needed by the zone-resolution service.

## Import policy and non-goals

The Phase 3 production reader will use the latest successfully completed FWO import, regardless of age. Do not add a maximum-age setting, stale-import rejection, automatic re-import, snapshot fingerprint, or new schema/API field. Missing successful import data or an internally inconsistent graph must fail explicitly.

This work package does not add production I/O, dependency-injection registration, a positioning strategy, enum-to-recipe mapping, customer-document analysis, workflow activation, UI, or vendor API rendering.

## Tests and acceptance

- Construct a graph snapshot from representative `Rulebase` and `RulebaseLink` entities containing initial, ordered/domain, section, and inline relationships.
- Prove defensive copying by mutating all supplied collections/entities after construction and observing unchanged evaluation.
- Reject duplicate identities, missing link endpoints, and invalid rule order during generic snapshot construction; reject missing/multiple initial links, ambiguous successors, and multiple inline links from one rule during Check Point interpretation.
- Migrate all existing structural-block scenarios to the generic snapshot and preserve their results and failure categories.
- Cover both composed examples: `EndOf(InitialLayer())` and `BeginningOf(Section(InitialLayer(), name-or-uid))`.
- Keep existing `RuleTreeBuilder` tests passing and add parity cases for the link relationships used by positioning.
- Run targeted tests, the complete unit suite, changed-file formatting, Debug solution build, and production-file coverage above 80%.

## Handoff prompt

> Read `.agents/AGENTS.md`, `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`, the roadmap and decision log, then all Phase 2 work packages in order. Implement only Phase 2 WP-04. Preserve the existing block behavior and semantic Check Point position, do not inspect the customer document, and do not begin Phase 3 production wiring.
