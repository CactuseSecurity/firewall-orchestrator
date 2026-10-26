# Phase 4 expert questions

## Status

Open questions collected during Phase 4 WP-01 planning. These questions block production activation of the customer algorithm, but they do not block isolated, side-effect-free customer calculation primitives where the unresolved input remains explicit.

Answers must be recorded in `DECISIONS.md` and propagated into the implementation brief before affected behavior is activated. Do not infer answers from enum names.

## EQ-01 - Zone cardinality and coverage

### Status

Partially answered on 2026-10-25. The strict baseline is settled; only the precise meaning of a permitted "multiple source" case remains to be confirmed.

### Why this needs an answer

The customer rulebook states that the separate zone-resolution function supplies exactly one source Security Zone and exactly one destination Security Zone. The implemented `CreationRuleZoneResolver` deliberately preserves more information:

- zero, one, or multiple matching zones on each side;
- `Complete`, `Partial`, or `None` range coverage;
- overlapping persisted zones;
- a persisted automatically calculated Internet zone; and
- a synthetic Internet/local fallback when automatic Internet calculation is disabled.

The customer recipe must not silently choose the first zone or discard uncovered ranges.

### Settled customer behavior

1. A customer calculation requires one source side and one destination side with usable normalized address input. A missing or empty side fails.
2. Both sides require `Complete` coverage.
3. Each side must resolve to exactly one distinct persisted zone from the configured designated zone matrix.
4. `Partial`, `None`, zero-zone, and multiple-distinct-zone results fail without choosing a zone or falling back.
5. A persisted designated-matrix zone is eligible, including the persisted automatic Internet zone when that configured matrix supplies it.
6. `SyntheticInternetFallback` is not an eligible customer zone because it is not a persisted designated-matrix zone.
7. Identical source and destination zones remain valid.

### Remaining question for the customer/domain expert

When the customer says that a rule may have "two sources and one destination," does that mean:

- two source objects/ranges that both resolve completely to the same one source zone, which still satisfies the strict one-zone invariant; or
- two distinct source zones, which would require an explicit exception to the strict baseline?

Until that is answered, multiple input objects are accepted only when their combined ranges have `Complete` coverage and resolve to the same one persisted source zone. Multiple distinct source zones fail.

## EQ-02 - Functional Check Point target

### Why this needs an answer

The customer rulebook separates:

1. the functional target, which determines the only layer or inline layer in which the rule can work; and
2. the customer placement, which finds the zone-pair Section inside that target.

The imported graph knows which main and inline layers exist, but neither the zone pair nor the current request context identifies which one is functionally correct. The same normalized Section name may exist in multiple layers.

This is independent of deciding which enum or workflow configuration triggers the customer algorithm. Even a directly invoked calculation needs a permitted parent layer in which to search.

### Questions for the customer/domain expert

1. Does the customer zone-pair behavior always search the initial access layer?
2. If it can search an inline layer, what authoritative request or configuration value selects that layer: UID, unique name, parent-rule identity, application identity, or another mapping?
3. Does a zone pair itself select an inline layer? If so, what is the exact mapping or naming convention?
4. If the requested Section exists only outside the selected functional target, should the result be the rulebook's explicit functional-conflict failure? The current reading says yes.
5. How should missing or ambiguous functional-target input be presented for manual handling?

### Implementation boundary while unanswered

Preserve existing exact structural blocks. Add customer behavior as a separate block or mode that accepts an explicit parent-layer scope. Do not search all layers, select the first inline layer, or fall back to the initial layer.

## EQ-03 - Same-ticket Flow groups

### Status

The technical behavior is understood. The remaining product question is whether pre-catalog sibling groups must succeed in the customer's workflow.

### Why this needs an answer

The implemented resolver expands existing request-visible Flow groups through the Flow catalog and applies the same zone rules to all expanded member ranges. A group created by another task in the same ticket may not yet be present there when rule positioning runs.

In that pre-catalog case, the access-rule element supplies only a group ID or name. The positioning resolver receives the access request task, not an authoritative sibling-task membership snapshot, so it has no member addresses from which to calculate zones.

### Questions for the workflow/Flow expert

1. Must a new rule be positionable before a referenced sibling-created group becomes available in the Flow catalog?
2. If yes, should the workflow materialize groups first, or should positioning receive an explicit sibling-group membership snapshot?
3. What ordering guarantee exists between group creation/materialization and access-rule implementation-task creation?
4. If the group is not yet resolvable, should positioning fail once for manual handling or be retried by a separately designed workflow?

Existing catalogued groups already work: all technical members are expanded and then classified using EQ-01. The current safe behavior for a pre-catalog sibling group is an explicit `GroupUnresolved` failure without automatic retry.

## Existing stakeholder follow-ups outside the customer calculation

These were already present in the root TODO and should not be confused with Phase 4 calculation decisions:

- Ask audit stakeholders whether storing, replacing, or clearing a rule position needs an event, and define actor, payload, retention, and clearing semantics.
- In a later modify/retry work package, decide whether gateway, management, or relevant rule-content changes clear or recalculate a stored position, and whether an authorized manual retry is required.

