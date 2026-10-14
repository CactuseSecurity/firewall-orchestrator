# WP-02 — Immutable Check Point snapshot and semantic position

> Implementation baseline: the semantic position remains current, but the Check Point-specific snapshot shape is superseded by the required refactor in [WP-04](WP-04-rulebase-graph-snapshot.md).

## Outcome

Represent the caller-loaded policy data required by structural blocks and persist a semantic Check Point insertion anchor without introducing a vendor API request shape.

## Snapshot contract

The snapshot belongs to exactly one management and gateway and identifies one initial layer. It contains layers by UID and name; each layer preserves the ordered interleaving of direct rules and sections. Sections contain ordered rules. Rules contain UID, native order, and an optional inline-layer UID.

Construction defensively copies collections and rejects invalid identities, duplicate layer/section/rule UIDs, malformed rules or sections, non-increasing native order, missing initial layers, and inline-layer references outside the snapshot. Exact names may be duplicated because name-based scope resolution must report ambiguity explicitly.

Loading the snapshot from GraphQL or importer data is deferred.

## Position contract

`CheckPointRulePosition` extends the Phase 1 polymorphic `RulePosition` contract with:

- stable discriminator `check-point.rule-position.v1`;
- layer UID;
- `Top`, `Bottom`, `Above`, or `Below` anchor kind;
- a rule UID required only by `Above` and `Below`; and
- the existing positioning algorithm from the calculation context.

Both supported JSON serializers round-trip the subtype through `RulePosition`; inherited deep-copy and existing persistence remain compatible.
