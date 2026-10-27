# Phase 4 expert questions

## Status

The KfW Check Point calculation behavior, vendor scope, and WP-01 technical design are settled. The former technical questions below are retained as the reasoning record; their answers are normative in D-059 through D-061 and the ready implementation brief.

No open expert or technical-design question blocks WP-01 implementation. Do not infer later activation answers from enum names.

The completed plain-language reasoning is preserved in [OPEN-QUESTIONS-WORKSHEET.md](OPEN-QUESTIONS-WORKSHEET.md).

## EQ-01 - Zone cardinality and coverage

### Status

Answered on 2026-10-26. There is no multiple-distinct-source-zone exception for this customer algorithm.

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
8. Multiple distinct source zones are never valid. Multiple distinct destination zones are likewise invalid under the exact-one-zone invariant.
9. Multiple input objects or ranges remain valid only when their combined ranges have `Complete` coverage and resolve to the same one persisted zone on that side.

## EQ-02 - Functional Check Point target

### Status

Answered. Customer behavior was settled on 2026-10-26; the fixed-import projection, containment precedence, and failures were settled on 2026-10-27 in D-060 and D-061.

### Why this needs an answer

The customer rulebook separates:

1. the functional target, which determines the only layer or inline layer in which the rule can work; and
2. the customer placement, which finds the zone-pair Section inside that target.

The imported graph knows which main and inline layers exist, but its current structural snapshot does not retain the source and destination semantics of the rules that guard those layers. The same normalized Section name may exist in multiple layers.

This is independent of deciding which enum or workflow configuration triggers the customer algorithm. Even a directly invoked calculation needs a permitted parent layer in which to search.

### Settled customer behavior

1. There is no layer naming formula for KfW placement.
2. A candidate layer matches only when all normalized source IPs of the new rule lie within its source layer guard and all normalized destination IPs lie within its destination layer guard.
3. Exactly one matching layer is required. Zero or multiple matching layer guards fail safely; both are customer-invariant violations.
4. The normalized zone-pair Section is searched only inside that functional layer. A matching Section elsewhere must not be used.
5. The layer-guard decision is independent of the later enum/workflow selection decision.
6. Only guarded inline layers are candidates. The main or initial layer is never a KfW target and is not a fallback.
7. KfW does not use IPv6 for this algorithm, but shared FWO normalization and containment behavior remains address-family aware rather than IPv4-only.
8. Layer guards use normal Check Point source/destination fields. `Any` is supported and commonly appears on one side; it contains every requested range on that side for the corresponding address family.
9. KfW guarantees complete containment of each new rule by exactly one guard. This guarantee does not apply to arbitrary Check Point inline rules, which may be partially contained or incapable of receiving hits.
10. Positioning accepts the normalized object representation from the fixed-import FWO database as authoritative. Importer mistakes are importer defects; positioning does not maintain a second KfW type allow-list or reconstruct discarded vendor semantics.
11. Guard containment is three-state: `Contained`, `NotContained`, or `Indeterminate`. Unique layer selection requires one definite match and definite non-matches for every other candidate.

### Technical resolution

Use the separate immutable fixed-import guard projection from D-060. Use `ScopeNotFound`, `ScopeAmbiguous`, `ScopeIndeterminate`, and `InvalidSnapshot` with the precedence fixed in D-061.

### Settled IP-less-object behavior

An imported positive dynamic object without IP ranges must not be treated as empty, non-matching, or `Any`. The proposed behavior is:

1. If `Any` or the guard's known positive ranges already cover every requested range, that side is definitely contained; an additional unresolved positive object cannot undo that proof.
2. If known ranges do not cover every requested range and an unresolved positive object could cover the remainder, the result is indeterminate rather than non-matching.
3. A side is definitely not contained only when its complete imported address-set semantics are known and at least one requested range remains uncovered.
4. Negation, exclusion, or other subtractive semantics become indeterminate whenever an unresolved term could change the result.
5. Layer selection succeeds only with one definite match and no other candidate whose indeterminate result could also match. Otherwise it fails safely.

KfW is expected not to use IP-less dynamic objects in these guards, so this is a safe exceptional path rather than a customer success scenario. A future resolver may turn an indeterminate object into a concrete address set without changing the containment contract.

### Repository evidence

- The Check Point importer creates an inline `RulebaseLink` from the parent guard rule to the inline layer, and the normalized graph retains that `SourceRuleId`. The guard-to-inline-layer topology therefore already exists.
- `RulebaseGraphSnapshotReader` and `RulebaseGraphSnapshot` currently retain only each rule's identity and native order. They do not load or copy the guard rule's source/destination objects, so guard coverage cannot be calculated from the present snapshot.
- The required extension should stay fixed-import consistent with the existing graph read. It must not query a live guard state that may differ from the imported topology.
- FWO's object-type catalog includes ordinary networks, groups, hosts, ranges, dynamic objects, gateways, domains, exclusion groups, access roles, and several vendor-specific kinds. The calculator consumes their imported normalized representation rather than applying a customer allow-list.
- The Check Point importer deliberately leaves `dynamic_net_obj`, `domain`, and `access-role` without static IPs. It currently maps `group-with-exclusion` to a group while noting that exclusion semantics are not handled correctly. Positioning does not repair either condition: missing address meaning is indeterminate, while incorrectly normalized semantics remain an importer defect.

Preserve existing exact structural blocks. Add guard-derived customer target selection and normalized Section matching as separate blocks or explicit modes. Do not search for the Section across all layers, select the first inline layer, infer a layer from its name, or fall back to the initial layer.

## EQ-03 - Same-ticket Flow groups

### Status

Answered. Customer behavior was settled on 2026-10-26; the canonical ticket-effective range projection, ordering, identity precedence, and action folding were settled on 2026-10-27 in D-059.

### Why this needs an answer

The implemented resolver expands existing request-visible Flow groups through the Flow catalog and applies the same zone rules to all expanded member ranges. A group created by another task in the same ticket may not yet be present there when rule positioning runs.

In that pre-catalog case, the access-rule element supplies only a group ID or name. The current positioning resolver receives the access request task, not an authoritative sibling-task membership snapshot, so it has no member addresses from which to calculate zones.

### Settled customer behavior

1. Tickets order object tasks before rule tasks for provisioning.
2. Provisioning creates or changes those objects before it writes the rules that reference them.
3. Before provisioning, workflow logic must represent new or changed objects as mock/effective objects.
4. Positioning must resolve a same-ticket group through that effective state; catalog absence alone is not a valid `GroupUnresolved` outcome.
5. The final intended ticket state overrides the older Flow-catalog version of the same group.
6. Groups, including nested groups, are recursively expanded to leaf IP ranges. Missing or ambiguous members and cycles fail safely.
7. Ticket creators must place the relevant object and group changes before rules that consume them. Positioning does not compensate for a ticket that violates this protocol.

### Technical resolution

Build one immutable ticket-aware `CreationRuleNetworkInput` from the broader ticket task set, ordered by task number and ID. Ticket-local state precedes the consuming access task and overrides Flow; identity preference and action folding follow D-059. The projection is independent of Flow persistence and is shared by zone and guard calculation.

### Repository evidence

- `FlowDbCreator.BuildFlowCreationPayloads` already creates ticket-level object/group/access snapshots, and its persistence path deliberately processes objects first, groups second, and access payloads last.
- `FlowPayloadMerger` already combines bundled Flow payloads. It remains useful evidence, but D-059 does not use it as the authoritative projection because it does not model recursively validated ticket-final group state; positioning must not invoke the writer side of `FlowDbCreator`.
- `ResolveRulePositioningRequestTask` currently supplies only the access request task or an access-bundle merge. It does not supply sibling object/group tasks, so `CreationRuleZoneResolver` cannot construct the required same-ticket effective membership from its current input alone.

Existing catalogued groups still use the Flow catalog and are classified using EQ-01. A same-ticket group uses the authoritative final ticket projection with precedence over catalog state. Missing, ambiguous, cyclic, or unusable effective membership fails safely; retry behavior remains outside this calculation package.

## EQ-04 - Phase 4 vendor scope

### Status

Answered on 2026-10-26.

### Why this needs an answer

The customer rulebook specifies both Check Point `SECTION_NATIVE` behavior and Fortinet `SECTION_LABEL` behavior. The implemented positioning product currently has a Check Point semantic position, Check Point graph interpreter, and Check Point structural blocks only. All Phase 4 decisions made so far describe the KfW Check Point branch.

Adding Fortinet to this same work package would require a Fortinet semantic position contract, normalized policy/label snapshot, first-contiguous-label-block selection, and vendor-specific tests. That is materially larger than adding the isolated Check Point customer calculation.

### Settled scope

Phase 4 WP-01 implements Check Point `SECTION_NATIVE` only. Fortinet `SECTION_LABEL` is explicitly deferred to a separate future work package and is outside the current sprint and quarter.

## Existing stakeholder follow-ups outside the customer calculation

These were already present in the root TODO and should not be confused with Phase 4 calculation decisions:

- Ask audit stakeholders whether storing, replacing, or clearing a rule position needs an event, and define actor, payload, retention, and clearing semantics.
- In a later modify/retry work package, decide whether gateway, management, or relevant rule-content changes clear or recalculate a stored position, and whether an authorized manual retry is required.

