# Phase 4 WP-01 implementation — KfW Check Point calculation

## Status

Implemented and locally validated on 2026-10-27. Production activation remains deliberately deferred.

The customer behavior and technical input contracts are decision-complete through D-061. Implement the ordered slices below without reopening the customer rulebook. Stop and amend this brief only if the repository disproves a stated schema or workflow assumption.

## Goal

Add an isolated, side-effect-free KfW Check Point `SECTION_NATIVE` calculation path that:

1. derives one immutable source/destination range input from the access task and authoritative same-ticket network state;
2. validates exactly one persisted designated-matrix zone on each side;
3. selects exactly one guarded inline layer through fixed-import source/destination containment;
4. finds the canonical zone-pair Section through normalized matching; and
5. produces the existing semantic Check Point end-of-Section position.

The calculation is directly invocable in unit tests but remains inactive in production. WP-01 does not map a `ProvisioningPositioningAlgorithm`, register a customer strategy or catalog, alter the workflow caller, or persist a customer result.

## Impact routing

Impacted Layers: shared C# data/services/compliance libraries, GraphQL query assets, C# unit tests, planning documentation

Recommended Skills:
- `fwo-route-request`

Minimal Change Set:
- immutable customer input and guard contracts under `roles/lib/files/FWO.Data/RulePositioning/`
- ticket-aware address resolution and KfW calculation under `roles/lib/files/FWO.Compliance/RulePositioning/`
- fixed-import guard read under `roles/lib/files/FWO.Services/RulePositioning/`
- one new query under `roles/common/files/fwo-api-calls/rulePositioning/`
- focused tests under `roles/tests-unit/files/FWO.Test/`

Ripple Checklist:
- [ ] Preserve the generic graph-only strategy, recipe, registration, workflow caller, and persistence path.
- [ ] Preserve existing exact layer/Section selector behavior.
- [ ] Verify the new GraphQL query uses existing schema only; add no migration or Hasura metadata.
- [ ] Keep customer services unregistered and prove that boundary in tests.
- [ ] Keep all result diagnostics fixed and free of names, addresses, UIDs, and exception text.

Risk Notes:
- Historical mismatch between topology and guard objects could select the wrong functional layer; the graph import ID is therefore mandatory input to the guard read.
- Ticket object/group actions are richer than the current Flow persistence implementation; positioning uses an independent immutable projection and must not call Flow persistence.
- IP-less imported terms make containment unknowable, not negative. The three-state contract is security-significant.

No schema, installer, RBAC, importer, middleware endpoint, or UI change is planned. Do not invoke the corresponding specialized skills unless implementation proves such a change necessary.

## Fixed boundaries

- Check Point `SECTION_NATIVE` only. Fortinet `SECTION_LABEL` remains deferred under D-058.
- New access-rule calculations only.
- Guarded inline layers only; the initial/main layer is never a KfW target or fallback.
- Fixed-import normalized database state is authoritative for layer guards.
- Ticket final state is authoritative for same-ticket objects and groups.
- No customer object allow-list and no importer repair.
- No application/common-service branch: the customer document does not define one.
- No Section creation.
- No UI display, request rendering, Check Point API calls, publish, installation, verification readback, audit, retry, or recalculation trigger.
- No enum mapping, DI registration, production recipe-catalog entry, or workflow selection.
- No changes to `RulePositioningWorkflowCaller`, `RulePositioningManager`, `CheckPointGraphRulePositioningStrategy`, its generic catalog, or rule-position persistence.

## Normative end-to-end behavior

### Requested rule input

| Input | Result |
| --- | --- |
| One or more direct source and destination ranges | Normalize, deduplicate, and continue. |
| Several objects/ranges on one side | Valid when every leaf resolves and the later zone/guard invariants hold. |
| Missing or empty source/destination | `InvalidInput`. |
| Malformed nonblank address or mixed-family endpoints in one range | `InvalidAddress`. |
| IP-less leaf in the requested rule | `ObjectUnresolved`; it cannot meet complete zone coverage. |
| Same-ticket group reference | Use the ticket's final effective definition. |
| Group absent from ticket state | Resolve the unique Flow-catalog group by positive ID, otherwise by unique trimmed case-insensitive name. |
| Group present in both ticket and Flow | Ticket state overrides Flow membership. |
| Nested ticket groups | Recursively expand to leaf ranges. |
| Missing/ambiguous group, empty final group, cycle, invalid action, or conflicting identity | Typed safe failure; do not drop a member. |
| Relevant object/group change ordered after the access task | `ProtocolOrderInvalid`; do not look ahead silently. |

### Zone pair

| Zone result | Result |
| --- | --- |
| `Complete` coverage and one distinct persisted designated-matrix zone on each side | Continue. |
| Multiple requested ranges all covered by the same one zone | Continue. |
| Same source and destination zone | Continue. |
| `Partial` or `None` on either side | `ZoneCoverageInvalid`. |
| Zero or multiple distinct zones on either side | `ZoneCardinalityInvalid`. |
| Persisted automatic Internet zone | Continue. |
| `SyntheticInternetFallback` | `ZoneNotPersisted`. |
| Blank zone name or non-positive/missing persisted ID | `ZoneDataInvalid`. |

### Functional layer

| Guard evaluation | Result |
| --- | --- |
| Exactly one guard is `Contained` on source and destination, and every other guard is definitely `NotContained` | Select its inline layer. |
| Zero definite matches and no indeterminate candidate | `ScopeNotFound`. |
| More than one definite match | `ScopeAmbiguous`. |
| One definite match plus any indeterminate candidate, or no definite match plus any indeterminate candidate | `ScopeIndeterminate`. |
| Main/initial layer only | `ScopeNotFound`; never fall back. |
| Matching Section exists only in another layer | `ScopeNotFound` at the selected layer's Section block. |
| Guard snapshot malformed, incomplete, from another scope/import, or not one-to-one with inline links | `InvalidSnapshot`. |

### Section and boundary

The canonical Section name is built in exactly one function:

```text
[DESTINATION-ZONE] < [SOURCE-ZONE]
```

The builder trims each zone name, rejects blank names and `[` / `]` / `<` inside a zone name, applies invariant uppercase, and inserts exactly one space around `<`. Section comparison trims the complete imported Section name and applies invariant uppercase. It does not rewrite interior whitespace or brackets; an imported `"[A]<[B]"` therefore does not match `"[A] < [B]"`.

| Section result inside selected layer | Result |
| --- | --- |
| Exactly one normalized match | Place at its end through the existing `EndOfBlock`. |
| No normalized match | `ScopeNotFound`; never create a Section. |
| Multiple normalized matches | `ScopeAmbiguous`; never choose the first. |
| Populated Section | Existing semantic result is `Below` its final rule UID. |
| Empty Section with a following rule | Existing semantic result is `Above` that rule UID. |
| Empty trailing Section with a previous rule | Existing semantic result is `Below` that rule UID. |
| Only empty Section/layer content | Existing semantic result is `Top`. |

## Slice 1 — Canonical effective rule-address input

### Contracts

Add `CreationRuleNetworkInput.cs` under `FWO.Data/RulePositioning/`:

- `CreationRuleNetworkInput`, with defensively copied, non-empty `IReadOnlyList<IPAddressRange>` source and destination ranges;
- range copies must not retain mutable caller lists;
- constructor validation requires non-null endpoints, one address family per range, and begin <= end;
- the input may contain both IPv4 and IPv6 ranges across separate entries;
- exact duplicate ranges are removed by the resolver before construction.

Add `ICreationRuleNetworkInputResolver.cs` under `FWO.Compliance/RulePositioning/`:

```csharp
Task<CreationRuleNetworkResolutionResult> ResolveAsync(
    WfReqTask accessTask,
    IReadOnlyCollection<WfReqTask> ticketTasks,
    CancellationToken cancellationToken);
```

Result categories are exactly:

- `InvalidInput` — null collections, duplicate/non-positive task identities where identity is required, inconsistent ticket/management scope, unsupported field/action shape, missing side, or ambiguous deterministic order;
- `UnsupportedTask` — not an access/create task;
- `ProtocolOrderInvalid` — a network object/group task relevant to this rule is not strictly before the consuming access task;
- `ObjectUnresolved` — a requested/ticket leaf cannot yield a concrete range;
- `GroupUnresolved` — a group is missing, deleted, empty, cyclic, or cannot be expanded completely;
- `GroupAmbiguous` — identity/name correlation or catalog lookup produces more than one candidate;
- `GroupQueryFailed` — the Flow resolver throws or returns an invalid/null result;
- `InvalidAddress` — nonblank address data cannot be normalized safely;
- `UnexpectedFailure` — conservative catch-all.

Diagnostics are fixed by category and never include ticket content.

### Ordered ticket projection

Implement `CreationRuleNetworkInputResolver` as a stateless service over `IFlowGroupResolver`.

1. Validate access/create shape, ticket ID, management ID, element collection, and membership of the access task in `ticketTasks` by request-task ID.
2. Sort tasks by `TaskNumber`, then by positive `Id`. Duplicate `(TaskNumber, Id)` ordering identities fail. Do not use collection order as a tie-breaker.
3. Consider network `object_create`, `object_modify`, `group_create`, `group_modify`, and `group_delete` tasks in the same ticket and effective management. Service-only group tasks are ignored.
4. Require every relevant task to precede the access task. A task is relevant when its positive identity or normalized name matches a directly/recursively referenced object or group. Resolve dependency reachability before accepting a later task as irrelevant.
5. Group definitions store address members in `ElemFieldType.source`, matching existing workflow and UI behavior.
6. Fold actions in order:
   - `create` and `addAfterCreation` add the member/final object;
   - `unchanged` retains baseline membership/value;
   - `modify` replaces the same uniquely correlated object/member;
   - `delete` removes the same uniquely correlated object/member;
   - any unknown action fails `InvalidInput`.
7. `group_create` starts without catalog members. `group_modify` starts from its uniquely identified Flow-catalog group. `group_delete` marks the group unavailable. More than one task may affect a group only when ordered folding yields one unambiguous final state.
8. Ticket definitions take precedence over Flow data even when Flow IDs exist. Flow is consulted only for an unchanged group or the baseline of `group_modify`.
9. Correlate objects/groups in this priority order: positive Flow ID, positive normalized-object/group ID, then trimmed case-insensitive name. A lower-priority key is used only when the higher-priority key is absent. Name lookup must yield exactly one candidate.
10. A member with a concrete IP range is a leaf even when it also carries a display/group name. A member without a concrete range and with a group identity/name is a nested-group reference. Any other IP-less member is unresolved.
11. Recursively expand nested ticket groups with an explicit visited path and a maximum depth of 100. A repeated group or depth overflow is `GroupUnresolved`.
12. Catalog groups are already flattened by `IFlowGroupResolver`; validate every returned leaf and do not reinterpret catalog membership as nested ticket state.
13. Normalize ranges with the established `ComplianceCheck.ParseIpRange` behavior and shared IP helpers. Preserve address family, reject malformed endpoints, and deduplicate exact `(family, begin, end)` values.

Keep orchestration, ordered state folding, and address parsing in separate focused classes. `CreationRuleNetworkInputResolver` coordinates the operation; an internal `TicketNetworkStateProjector` owns task ordering/action/identity folding; and an internal `PositioningAddressRangeNormalizer` owns leaf parsing and deduplication. This split is required to retain the repository limits on method length, complexity, and source-file size.

Do not call `FlowDbCreator.CreateFlowInFlowDb`, any mutation, or any workflow state action. `FlowDbCreator.BuildFlowCreationPayloads` and `FlowPayloadMerger` were evaluated but are not the authoritative implementation seam: their payloads support Flow persistence/bundling, omit recursive final-state validation, and current Flow group persistence rejects nested group references. Small pure identity/action helpers may be extracted only if their semantics exactly match this contract; do not couple positioning to Flow writes.

### Zone resolver reuse

Extend `ICreationRuleZoneResolver` with an overload that accepts `CreationRuleNetworkInput` plus designated zones. Move only zone validation/cloning, `ComplianceZoneResolver.ResolveZones`, coverage classification, and `PositioningZoneReference` construction behind that overload.

Keep the existing `WfReqTask` overload behavior-compatible. It may retain its current single-task group-loading path and delegate after address normalization. Do not make the generic/inactive path require a whole ticket in WP-01. Customer tests call the new network-input overload so layer and zone calculation consume the exact same ranges.

## Slice 2 — Fixed-import layer-guard snapshot

### Import consistency token

Extend `RulebaseGraphReadSuccess` with a positive `ImportId`. The production `RulebaseGraphSnapshotReader` passes the already selected `control_id`. Update every production/test constructor call. Do not add another latest-import query and do not expose mutable import entities.

### Guard contracts

Add `CheckPointLayerGuardSnapshot.cs` under `FWO.Data/RulePositioning/CheckPoint/`:

- management ID, gateway ID, import ID;
- immutable guards keyed by target inline-layer rulebase ID;
- each guard contains source rule ID, source rulebase ID, target layer ID, and immutable source/destination side terms;
- each side contains `IsNegated` and immutable top-level address terms;
- each term contains known included ranges, known excluded ranges, `HasUnresolvedIncludedTerms`, and `HasUnresolvedExcludedTerms`;
- known ranges are defensively copied and exact duplicates removed within their term;
- constructor rejects duplicate target layer IDs and invalid identities/ranges.

Add `ICheckPointLayerGuardSnapshotReader.cs` and implementation under `FWO.Services/RulePositioning/CheckPoint/`:

```csharp
Task<CheckPointLayerGuardReadResult> ReadAsync(
    RulebaseGraphSnapshot graph,
    long importId,
    CancellationToken cancellationToken);
```

Reader failures are exactly `InvalidInput`, `ScopeMismatch`, `InvalidGuardData`, and `QueryFailed`, with fixed diagnostics.

### Query

Add `getCheckPointLayerGuardsAtImport.graphql` and load it through `RulePositioningQueries`.

The query receives management ID, distinct source rule IDs from inline links, and import ID. It returns only those rules and includes:

- rule ID, management ID, rulebase ID, create/remove interval;
- `rule_src_neg` and `rule_dst_neg`;
- active historical `rule_froms` and `rule_tos` association rows at the fixed import, including their stable association ID and duplicated `negated` flag;
- top-level object ID, UID, name, type name, IP, end IP, create/remove interval; and
- each object's active historical `objgrp_flats` members with the membership `negated` flag, the same address/type/identity fields, and import interval.

Use the repository's half-open interval consistently: `created <= importId && (removed is null || removed > importId)`. Apply it to rule rows, rule-object associations, top-level objects, and flattened group members using their actual schema field names. Order every returned collection by stable numeric identity.

### Reader validation and mapping

1. Require positive graph scope and import ID.
2. Derive candidates only from `RulebaseLinkTypes.Inline` links with both `SourceRuleId` and a target layer.
3. Require one inline link per source rule and one source rule per target layer. Initial, ordered, domain, Section, and NAT links are not guards.
4. Require the query to return exactly the requested source-rule set, at the supplied management/import, in the source rulebase represented by the graph.
5. Require each candidate target to be a graph layer reachable through its inline link. No guard may target the initial layer.
6. Require every source/destination association's `negated` value to agree with its owning rule-side flag. A mismatch is `InvalidGuardData`; do not guess which duplicate representation is newer.
7. Preserve one address term per top-level object association:
   - parse a valid nonblank normalized top-level IP/end pair into the term's included ranges;
   - treat a normalized full-family range as ordinary known coverage (`Any` needs no special type branch);
   - when the top-level object has no range, inspect its flattened members;
   - add a valid non-negated member range to that term's included ranges and a valid negated member range to that term's excluded ranges;
   - set the matching unresolved-included or unresolved-excluded flag when a flat member has no address;
   - an IP-less top-level object with no usable flat semantics is an unresolved included term;
   - reject malformed nonblank address data as `InvalidGuardData`.
8. Trust the imported flattened representation. Do not recursively rebuild imported groups, infer exclusion semantics from names/types, or compensate for importer defects.
9. An empty guard side is unresolved, not `Any` and not a known empty set.
10. Preserve the rule side's negation flag separately; do not materialize the IPv4/IPv6 universe in the reader.
11. Propagate caller cancellation; convert all non-cancellation query exceptions to `QueryFailed`.

The existing generic graph query and snapshot contents remain otherwise unchanged, so generic positioning pays no guard-query cost.

## Slice 3 — Three-state containment and layer selection

Add an internal/pure `CheckPointGuardContainmentEvaluator` beside the Check Point blocks. Its result enum is exactly `Contained`, `NotContained`, and `Indeterminate`. Evaluate each top-level term independently, then union term results; never subtract one term's exclusions from another term's positive ranges.

### Positive side

For a non-negated side:

1. Partition term ranges and requested ranges by address family.
2. For each term, subtract its known excluded ranges from its known included ranges. A term with unresolved exclusions contributes potential but no definite coverage from the affected family; another independent term may still prove coverage.
3. Union every term's definite coverage. A requested range is definitely covered when subtracting that union leaves no address.
4. If all requested ranges are definitely covered, return `Contained` even when unresolved included terms also exist.
5. If any requested address remains and an unresolved included term or coverage made uncertain by an unresolved exclusion could cover it, return `Indeterminate`.
6. Otherwise return `NotContained`.

Coverage is by the union, not necessarily one guard range. Adjacent/overlapping ranges may be merged through shared `IpOperations` helpers. Never compare IPv4 numerically with IPv6.

### Negated side

For a negated rule side, the union of its top-level terms is excluded:

1. Reuse each term's definite and potential positive-set calculation, including its own flat-member exclusions.
2. If any requested range overlaps the definitely included union, return `NotContained`; complete containment by the rule-side complement is already disproved.
3. If no definite union overlap exists but an unresolved inclusion/exclusion could change whether the union intersects the request, return `Indeterminate`.
4. Otherwise return `Contained`.

Consequently, negated `Any` is `NotContained` for non-empty requested input. Invalid/empty requested input is rejected before evaluation.

### Layer block

Add `CheckPointLayerGuardScopeBlock` with kind `check-point.layer-guard-scope`, no children, and output role `check-point.scope`. Extend `CheckPointPositioningContext` with an optional immutable `KfwCheckPointPositioningInput`. The guard block consumes that input's `CreationRuleNetworkInput` and `CheckPointLayerGuardSnapshot`.

Existing two-argument construction remains source-compatible and generic blocks ignore the optional value. The guard block requires it and validates graph/guard management, gateway, import, inline-link, source-rule, and target-layer identity before evaluating.

A guard matches only when both sides are `Contained`. Select with this precedence:

1. invalid cross-snapshot data => `InvalidSnapshot`;
2. more than one definite match => `ScopeAmbiguous`;
3. any indeterminate candidate after excluding definite ambiguity => `ScopeIndeterminate`;
4. zero definite matches => `ScopeNotFound`;
5. exactly one definite match and every other candidate definite non-match => existing `CheckPointScopeValue` for that inline layer.

Add `ScopeIndeterminate` to `PositioningBlockFailureCategory` with a fixed diagnostic. Existing strategy failure mapping remains conservative `CalculationFailed`; no public Phase 1 category is added.

## Slice 4 — Canonical Section and activation-neutral recipe

### Section naming and matching

Add `CheckPointSectionName` as a pure shared helper with `TryBuild(destinationZoneName, sourceZoneName, out string)` or an equivalent typed result. It owns the one canonical format and validation described above.

Extend `CheckPointSectionSelectionMode` and `CheckPointSectionSelector` with `NormalizedName`. Preserve `ExactName` and `Uid` construction and ordinal behavior byte-for-byte. The Section block applies trim plus `ToUpperInvariant()` only in `NormalizedName` mode. It still searches only the single parent layer returned by its child and still fails on zero/multiple matches.

### Customer input and factory

Add immutable `KfwCheckPointPositioningInput` containing:

- `CreationRuleNetworkInput`;
- `CheckPointLayerGuardSnapshot`.

Keep this type in `FWO.Data`; it must not depend on the higher-level `FWO.Compliance` assembly. Add `KfwCheckPointPositioningRecipeFactory` under `FWO.Compliance/RulePositioning/CheckPoint/`. Its synchronous `Create` method accepts the non-null `KfwCheckPointPositioningInput` and a separate non-null `CreationRuleZoneInput`, then:

1. validates both zone coverages are `Complete`;
2. requires exactly one distinct source and destination zone after ID-based deduplication;
3. requires both references are `Persisted`, have positive IDs, and have valid names;
4. builds the destination-first canonical Section name;
5. returns this exact complete tree:

```text
EndOfBlock
└── CheckPointSectionScopeBlock(NormalizedName)
    └── CheckPointLayerGuardScopeBlock
```

Factory result failures are exactly:

- `InvalidInput` — missing network/guard input;
- `ZoneCoverageInvalid`;
- `ZoneCardinalityInvalid`;
- `ZoneNotPersisted`;
- `ZoneDataInvalid`; and
- `InvalidRecipe` — constructed recipe fails structural validation.

The factory does not accept, inspect, or map a `ProvisioningPositioningAlgorithm`. The existing `EndOfBlock` obtains and preserves the caller-supplied valid algorithm from `RulePositioningContext`; tests treat that value as opaque fixture data.

### Direct calculation harness

Do not add a second production strategy. Unit tests invoke the factory and existing executor directly with a `CheckPointPositioningContext` containing the graph and customer input. This proves the calculation while honoring D-047. The future activation package will own:

- full-ticket loading at the workflow seam;
- enum/capability mapping;
- orchestration of graph, guards, designated zones, and effective ranges;
- DI registration; and
- persistence through the already validated caller.

## Exact change set

Production files to add:

- `roles/lib/files/FWO.Data/RulePositioning/CreationRuleNetworkInput.cs`
- `roles/lib/files/FWO.Data/RulePositioning/CheckPoint/CheckPointLayerGuardSnapshot.cs`
- `roles/lib/files/FWO.Data/RulePositioning/CheckPoint/KfwCheckPointPositioningInput.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/ICreationRuleNetworkInputResolver.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleNetworkInputResolver.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/TicketNetworkStateProjector.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/PositioningAddressRangeNormalizer.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/CheckPoint/KfwCheckPointPositioningRecipeFactory.cs`
- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/ICheckPointLayerGuardSnapshotReader.cs`
- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/CheckPointLayerGuardSnapshotReader.cs`
- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/CheckPointGuardContainmentEvaluator.cs`
- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/CheckPointSectionName.cs`
- `roles/common/files/fwo-api-calls/rulePositioning/getCheckPointLayerGuardsAtImport.graphql`

Production files to modify:

- `roles/lib/files/FWO.Services/RulePositioning/IRulebaseGraphSnapshotReader.cs` — expose fixed import ID on success;
- `roles/lib/files/FWO.Services/RulePositioning/RulebaseGraphSnapshotReader.cs` — populate that ID only;
- `roles/lib/files/FWO.Api.Client/Queries/RulePositioningQueries.cs` — load the guard query;
- `roles/lib/files/FWO.Compliance/RulePositioning/ICreationRuleZoneResolver.cs` — add normalized-input overload;
- `roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleZoneResolver.cs` — extract/reuse pure zone mapping;
- `roles/lib/files/FWO.Data/RulePositioning/CheckPoint/CheckPointPositioningContext.cs` — optional customer input;
- `roles/lib/files/FWO.Services/RulePositioning/Blocks/PositioningBlockContracts.cs` — `ScopeIndeterminate`;
- `roles/lib/files/FWO.Services/RulePositioning/CheckPoint/CheckPointStructuralBlocks.cs` — additive guard block and normalized Section selector mode.

Tests to add:

- `roles/tests-unit/files/FWO.Test/CreationRuleNetworkInputResolverTest.cs`
- `roles/tests-unit/files/FWO.Test/CheckPointLayerGuardSnapshotReaderTest.cs`
- `roles/tests-unit/files/FWO.Test/CheckPointGuardContainmentTest.cs`
- `roles/tests-unit/files/FWO.Test/KfwCheckPointPositioningRecipeTest.cs`

Tests to modify:

- `RulebaseGraphSnapshotReaderTest.cs`
- `RulePositioningQueriesTest.cs`
- `CreationRuleZoneResolverTest.cs`
- `CheckPointPositioningContractsTest.cs`
- `CheckPointStructuralBlocksTest.cs`
- `CheckPointGraphPositioningTest.cs`
- `CheckPointRulePositioningStrategyTest.cs`
- any test fake constructing `RulebaseGraphReadSuccess` or `CheckPointPositioningContext`.

Documentation updated after implementation:

- this brief with validation evidence;
- `phase-4/README.md`;
- root `README.md`, `TODO.md`, and `SESSION-HANDOFF.md`;
- `DECISIONS.md` only for genuinely new decisions, never to rewrite D-046 through D-061.

Do not change production registration files, middleware/UI files, SQL, Hasura metadata, importer files, provisioning enums, device-specific setting serialization, persistence mutations, renderer/vendor clients, version metadata, or revision history.

## Required tests

### Effective address input

Cover at minimum:

1. direct IPv4 host/network/range normalization and exact deduplication;
2. IPv6 normalization and no cross-family comparison/coercion;
3. missing side, invalid field/action, malformed address, and IP-less leaf;
4. unique catalog group by ID and by name; missing, duplicate, null, and throwing catalog result;
5. ticket `group_create` before access overriding catalog absence;
6. ticket `group_modify` folding unchanged/add/modify/delete over catalog baseline;
7. ticket `group_delete` making a later reference fail;
8. ticket state overriding stale catalog membership;
9. nested group expansion, duplicate leaves, direct plus nested members, cycle, depth limit, and empty final group;
10. Flow ID, normalized ID, and unique-name identity precedence;
11. conflicting IDs/names and ambiguous name matches;
12. relevant group/object task after access producing `ProtocolOrderInvalid`;
13. unrelated later task not invalidating the rule;
14. deterministic `TaskNumber`/ID ordering independent of input collection order;
15. cancellation before work, during catalog load, and during recursive expansion;
16. immutable output and concurrent resolver calls retaining no shared state.

### Zone reuse

Prove the normalized-input overload preserves all existing zone behavior and receives the exact same ranges by value from the effective resolver. Cover complete/partial/none, overlapping zones, persisted Internet, synthetic fallback, same-zone flow, and multi-zone output. The KfW factory—not the generic resolver—enforces the strict one-persisted-zone customer invariant.

### Guard reader

Cover:

1. exact import ID and inline source-rule IDs in the query variables;
2. one valid guard per inline target with positive ranges;
3. `Any` full-family range;
4. flattened group and nested-flat leaf data;
5. known plus IP-less included/excluded terms retaining known ranges and the correct uncertainty flag;
6. negated flat members remaining scoped to their own top-level group term;
7. rule-side negation flags retained separately;
8. malformed nonblank range, mixed endpoints, empty side, duplicate target, missing/extra rule, wrong management/rulebase/import history, and non-inline source;
9. zero inline links returning a valid empty snapshot, later yielding `ScopeNotFound`;
10. query exception sanitization and cancellation propagation;
11. immutable returned collections;
12. generic graph reads performing no guard query.

### Containment

Use table-driven IPv4 and IPv6 cases for:

- exact, subset, superset, disjoint, partial-overlap, adjacent-union, and multi-range coverage;
- positive known coverage with unresolved extra term => `Contained`;
- positive uncovered space with unresolved term => `Indeterminate`;
- positive uncovered space without unresolved term => `NotContained`;
- negated known overlap => `NotContained` even with unresolved terms;
- negated no known overlap plus unresolved term => `Indeterminate`;
- negated no overlap and complete known semantics => `Contained`;
- positive `Any`, negated `Any`, and family-specific `Any`;
- cancellation during large input iteration.

### Guard block

Cover unique definite match, zero match, multiple definite matches, one match plus competing indeterminate, only indeterminate, initial-layer exclusion, cross-snapshot mismatch, missing guard/link, and exact return of the existing layer scope value. Prove precedence: definite ambiguity beats indeterminate; invalid snapshot beats all selection results.

### Section and full recipe

Cover:

1. canonical destination-first string, same-zone string, trim, invariant uppercase, and invalid zone characters;
2. normalized matching of imported outer whitespace/case;
3. rejection of incorrect interior whitespace/brackets;
4. zero and duplicate normalized Section matches;
5. exact-name and UID selector regression preserving ordinal behavior;
6. strict zone coverage/cardinality/persistence validation for every factory category;
7. populated, empty-middle, empty-trailing, and only-empty Section boundary output;
8. matching Section in wrong layer never selected;
9. end-to-end unique guard + canonical Section success;
10. opaque caller algorithm preserved in the semantic position;
11. cancellation and unexpected executor failure behavior;
12. generic Phase 3 recipe output unchanged and zone/customer-input independent.

### Inactive boundary

Source-contract tests must prove no production registration or mapping for:

- `KfwCheckPointPositioningRecipeFactory`;
- `CreationRuleNetworkInputResolver` as a customer workflow dependency;
- `CheckPointLayerGuardSnapshotReader`;
- the zone-capable `CheckPointRulePositioningStrategy`; or
- `CheckPointInlineLayerPerZonePair` to the new factory/recipe.

Do not assert that the enum name implies customer behavior. The later activation plan owns that decision.

## Validation

Implementation evidence (2026-10-27):

- the combined WP-01/regression filter passed with 250 tests and zero failures;
- the four new focused suites passed with 43 tests and zero failures;
- focused Coverlet line coverage exceeded 80% for every new production file (81.8% to 100%); the effective-input resolver reached 86.4%, guard reader 82.1%, containment evaluator 94.2%, and recipe factory 91.8%;
- the complete C# unit suite passed with 8,793 tests, 30 skipped, and zero failures;
- `dotnet format roles/FWO.sln --no-restore` completed successfully and incidental unrelated formatter changes were removed;
- Debug solution build and `git diff --check` completed successfully; and
- source-contract checks confirm that the KfW factory, effective-input resolver, guard reader, and zone-capable strategy remain unregistered.

Run focused tests while implementing each slice, then this combined filter:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CreationRuleNetworkInputResolverTest|FullyQualifiedName~CreationRuleZoneResolverTest|FullyQualifiedName~RulebaseGraphSnapshotReaderTest|FullyQualifiedName~RulePositioningQueriesTest|FullyQualifiedName~CheckPointLayerGuardSnapshotReaderTest|FullyQualifiedName~CheckPointGuardContainmentTest|FullyQualifiedName~CheckPointStructuralBlocksTest|FullyQualifiedName~KfwCheckPointPositioningRecipeTest|FullyQualifiedName~CheckPointGraphPositioningTest|FullyQualifiedName~CheckPointRulePositioningStrategyTest"
```

Then run:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CreationRuleNetworkInputResolverTest|FullyQualifiedName~CheckPointLayerGuardSnapshotReaderTest|FullyQualifiedName~CheckPointGuardContainmentTest|FullyQualifiedName~KfwCheckPointPositioningRecipeTest" --collect:"XPlat Code Coverage" --results-directory TestResults/Phase4WP01
git diff --check
```

Require more than 80% line coverage for every new production class. Inspect the diff for accidental registration, enum mapping, persistence, UI, schema, installer, importer, renderer, and vendor-client changes.

No disposable-VM or browser validation is required because WP-01 changes no schema, installer behavior, endpoint, UI, or production activation. A live GraphQL smoke test is optional confidence only; query-shape unit tests and the full C# suite are mandatory.

## Acceptance criteria

- One immutable effective source/destination range input drives both zone and guard decisions.
- Ticket-local final group state overrides Flow state and recursively resolves without mutation.
- Topology and guard semantics are tied to one exact successful import.
- Every guarded inline candidate produces deterministic `Contained`, `NotContained`, or `Indeterminate` side results.
- Exactly one definite functional layer is required; main-layer, first-match, and name-based fallbacks do not exist.
- Section naming is centralized, destination-first, and normalized exactly as specified.
- The complete recipe ends at the existing semantic end-of-Section boundary.
- Existing exact selectors and generic graph-only positioning remain unchanged.
- Customer calculation is directly testable but not selectable or active in production.
- All failures are typed and diagnostics disclose no customer data.
- No external mutation or firewall operation occurs.

## Out of scope

- Enum/capability selection and customer strategy/catalog registration.
- Loading the whole ticket at the production workflow seam.
- Production orchestration and persistence of a KfW result.
- Fortinet, Azure, application/common-service recipes, and default placement.
- Section creation or sorted insertion of missing Sections.
- UI visualization from customer chapter 11.
- Rendering, Check Point API execution, publish/install, and post-write verification.
- Audit, retries, manual recalculation, update-triggered recalculation, and multi-firewall partial-failure policy.
- Importer correction for exclusion groups or other lost semantics.
- Database/Hasura schema, auth, installer, version, and help-content changes.
