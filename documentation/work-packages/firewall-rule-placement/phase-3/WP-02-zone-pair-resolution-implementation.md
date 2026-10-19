# Phase 3 WP-02 implementation - Creation-rule zone resolution

## Status

Implemented and locally validated on 2026-10-18.

Roadmap amendment D-035 classifies this implemented service as inactive Phase 4 preparation. It is not a dependency of generic Phase 3 recipes and must not be registered or invoked by the Phase 3 WP-05 positioning path.

This brief is self-contained. Implement only this work package and do not reopen the paired planning brief unless repository evidence makes this design impossible or unsafe.

## Goal

Add a reusable, cancellation-aware service that turns a new access rule's active source and destination workflow elements into immutable ordered zone sets. Existing Flow network groups are expanded before address normalization, and the established `ComplianceZoneResolver` supplies all overlapping persisted zones plus its existing Internet/local behavior.

The result is positioning input only. It does not select a customer section, construct a recipe, create Cartesian source/destination pairs, or decide whether a particular zone cardinality is acceptable.

## Fixed requirements

- Support only creation of new access rules.
- Use the workflow elements already present in the Phase 1 calculation context.
- Resolve existing request-visible Flow network groups when the request contains only a group reference.
- Reuse `WfReqTask.GetNwObjectElements`, `ComplianceCheck.ParseIpRange`, `ComplianceZoneResolver`, and `IFlowGroupResolver` rather than duplicating their established behavior.
- Preserve all overlapping zone matches and the existing synthetic Internet/local fallback.
- Keep the output independent of customer recipes and Check Point section names.
- Return explicit safe failures for invalid, unsupported, unresolved, or ambiguous inputs.
- Propagate caller cancellation.
- Do not query a firewall, compare the new rule with existing rules, inspect the customer document, map enums to recipes, persist a position, register a positioning strategy, or activate a workflow.

## Routing and ownership

Impacted Layers: Shared libraries (`FWO.Compliance`, `FWO.Services`), middleware dependency injection, unit tests, planning documentation

Recommended Skills:
- `fwo-route-request`

Minimal Change Set:
- Add the WP-02 contracts and resolver under `roles/lib/files/FWO.Compliance/RulePositioning/`.
- Add cancellation support to the existing compliance-zone and Flow-group resolution seams.
- Register only the new resolver in middleware dependency injection.
- Add focused unit tests and update this planning record with validation evidence.

Ripple Checklist:
- [x] Preserve existing compliance Internet/local behavior.
- [x] Keep existing `IFlowGroupResolver` callers source-compatible through an optional cancellation token.
- [x] Update `FlowCatalogService` and its test implementation for cancellation.
- [x] Keep schema, auth, installer, importer, UI, positioning enums, and customer mappings unchanged.
- [x] Leave the production positioning strategy and workflow caller unregistered.

Risk Notes:
- Flow group expansion is the only additional read performed by WP-02.
- A group created in the same ticket but not yet present in the Flow catalog returns `GroupUnresolved` under the current explicit assumption. Confirm the desired cross-task behavior with stakeholders before Phase 4 customer activation.

## Public contracts

Add `ICreationRuleZoneResolver.cs` under `roles/lib/files/FWO.Compliance/RulePositioning/` in namespace `FWO.Compliance.RulePositioning`:

```csharp
public interface ICreationRuleZoneResolver
{
    Task<CreationRuleZoneResolutionResult> ResolveAsync(
        WfReqTask requestTask,
        IReadOnlyCollection<ComplianceNetworkZone> networkZones,
        CancellationToken cancellationToken);
}
```

The same file contains the following public result contracts:

- abstract `CreationRuleZoneResolutionResult`;
- sealed `CreationRuleZoneResolutionSuccess`, exposing a non-null `CreationRuleZoneInput Input`;
- sealed `CreationRuleZoneResolutionFailure`, exposing `CreationRuleZoneResolutionFailureCategory Category` and a fixed category-derived `string Diagnostic`;
- sealed immutable `CreationRuleZoneInput`, exposing:
  - positive `CriterionId`, taken from `GlobalConfig.DesignatedZoneMatrixId` after validation;
  - `IReadOnlyList<PositioningZoneReference> SourceZones`;
  - `IReadOnlyList<PositioningZoneReference> DestinationZones`;
  - `ZoneCoverage SourceCoverage`; and
  - `ZoneCoverage DestinationCoverage`;
- sealed immutable `PositioningZoneReference`, exposing:
  - nullable `int Id`;
  - `string IdString` and `string Name`;
  - `bool IsAutoCalculatedInternetZone`;
  - `bool IsAutoCalculatedUndefinedInternalZone`; and
  - `PositioningZoneKind Kind`;
- `PositioningZoneKind` with exactly `Persisted` and `SyntheticInternetFallback`; and
- `ZoneCoverage` with exactly `Complete`, `Partial`, and `None`.

All constructors defensively copy incoming collections. Exposed collections must be read-only and must not retain caller-owned mutable collection instances. A success constructor rejects null input. Failure constructors do not accept caller-supplied diagnostic text.

Persisted zones retain their positive database ID. The synthetic zone returned internally by `ComplianceZoneResolver` is represented as `Id = null` and `Kind = SyntheticInternetFallback`. Downstream work must identify that result through `Kind`, never through its localized `Name`.

Within each side, order persisted zones by positive database ID and place the synthetic fallback last. Multiple matching zones are valid and remain in the result. WP-02 does not form Cartesian zone pairs and does not reject a result merely because either side contains multiple zones.

## Service dependencies

Add `CreationRuleZoneResolver.cs` in the same directory and namespace. It implements `ICreationRuleZoneResolver` and has only these constructor dependencies:

- `IFlowGroupResolver`, for existing request-visible Flow group expansion; and
- `GlobalConfig`, for `DesignatedZoneMatrixId`, `AutoCalculateInternetZone`, and `GetText("internet_local_zone")`.

The caller supplies the designated matrix's `ComplianceNetworkZone` rows. WP-02 performs no matrix or object GraphQL query of its own. Register `ICreationRuleZoneResolver` to `CreationRuleZoneResolver` as a singleton in `roles/middleware/files/FWO.Middleware.Server/Program.cs`. Its dependencies are already singleton services, and the resolver holds no per-call mutable state.

## Participating workflow data

Require exact ordinal values:

- `requestTask.TaskType == WfTaskType.access.ToString()`; and
- `requestTask.RequestAction == RequestAction.create.ToString()`.

Only elements whose `Field` is exactly `source` or `destination` participate. Ignore `service`, `rule`, `modelled_source`, `modelled_destination`, and unknown non-participating fields.

For participating elements:

- `create`, `unchanged`, `modify`, and `addAfterCreation` are active;
- `delete` is ignored; and
- an unknown action returns `InvalidInput`.

After deleted elements are removed, both source and destination must contain at least one active element and at least one resolved IP range. Missing either side is `InvalidInput`.

Use `WfReqTask.GetNwObjectElements` so a valid `Cidr`/`CidrEnd` remains authoritative over raw `IpString`/`IpEnd`. Do not change that existing precedence.

## Flow group expansion

An active element is a group reference when it has a positive `FlowNetworkGroupId` or a non-blank `GroupName`. A positive ID is authoritative. Use the name only when no positive ID is present.

Resolve through `IFlowGroupResolver.ResolveFlowGroupMembersAsync` as follows:

1. Deduplicate requested IDs and names without losing their association with source/destination elements.
2. Split selectors into calls of at most `FlowGroupResolutionParameters.MaxSelectors` (100 total ID and name selectors per call).
3. Resolve IDs first. Resolve remaining name-only references using the resolver's existing unique, ordinal-ignore-case name matching.
4. Require exactly one matching group for every reference. Missing, ambiguous, non-positive-ID, or duplicate result identities return `GroupUnresolved`.
5. Require every resolved group to contain at least one technical member. Expand every member; an empty group is `GroupUnresolved`.
6. Convert each member's `IpStart`/`IpEnd` into the same normalized range path used for direct elements. A member with no usable numeric address is `UnsupportedObject`; malformed endpoints are `InvalidAddress`.

Do not silently omit unresolved groups or members. Do not recursively query nested groups: the current Flow resolution contract exposes technical network-object members, not nested group nodes.

A group created by another request task in the same ticket is not resolved from sibling task data in this package. If it is not yet queryable through Flow, return `GroupUnresolved`. This is a deliberate non-blocking assumption pending stakeholder confirmation before Phase 4 customer activation.

Extend `IFlowGroupResolver` without breaking existing callers:

```csharp
Task<FlowGroupResolutionResult> ResolveFlowGroupMembersAsync(
    FlowGroupResolutionParameters parameters,
    CancellationToken cancellationToken = default);
```

Update `FlowCatalogService` to check cancellation before query construction and use `WaitAsync(cancellationToken)` while awaiting each `ApiConnection.SendQueryAsync` task. Existing callers may omit the optional token. Update the existing test fake in `ComplianceRequestedRulePolicyCheckerTest.cs` to accept the new parameter.

## Address normalization

Normalize direct elements and expanded group members through one helper:

1. Build a fresh `NetworkObject` with the selected start/end strings and an appropriate host or IP-range type.
2. Call `ComplianceCheck.ParseIpRange`.
3. Require at least one returned range.
4. Require non-null endpoints, the same address family at both ends, and start less than or equal to end.
5. Support IPv4 and IPv6 hosts, CIDRs, and explicit ranges.
6. Deduplicate identical normalized ranges before zone resolution.

An element with a direct numeric address remains valid whether or not it also carries a Flow object ID. An element that is neither a resolvable group nor a usable direct address is `UnsupportedObject`. Dynamic/domain objects without a numeric address are also `UnsupportedObject`; the available workflow and Flow-group contracts do not carry enough object-type data to apply the broader compliance check's dynamic-object shortcut safely.

## Zone-data validation and resolution

Before resolving either side:

1. Require `GlobalConfig.DesignatedZoneMatrixId > 0` and a non-empty supplied zone collection.
2. Require every supplied zone to be non-null, have a positive unique `Id`, match the designated `CriterionId`, have a non-blank name, and contain only valid ranges with matching address families and ordered endpoints.
3. When `GlobalConfig.AutoCalculateInternetZone` is true, require exactly one supplied zone with `IsAutoCalculatedInternetZone == true` and at least one IPv4 range. Missing or duplicate automatic Internet zones are `InvalidZoneData`.

Resolve source and destination independently by passing each complete normalized range list directly to:

```csharp
ComplianceZoneResolver.ResolveZones(
    ranges,
    activeNetworkZones,
    globalConfig.AutoCalculateInternetZone,
    globalConfig.GetText("internet_local_zone"),
    cancellationToken,
    out unassignableRanges);
```

Preserve the existing resolver behavior:

- every persisted zone overlapping any supplied range is returned;
- when automatic Internet calculation is disabled, uncovered IPv4 space adds the synthetic Internet/local fallback;
- when automatic calculation is enabled, the supplied persisted automatic Internet zone covers external IPv4 space; and
- uncovered IPv6 ranges remain unassignable.

Derive each side's coverage after resolution:

- `Complete` when `unassignableRanges` is empty;
- `Partial` when at least one zone and at least one unassignable range exist; and
- `None` when there are unassignable ranges and no zone.

`Partial` and `None` are successful, information-preserving results rather than resolver failures. Phase 4 WP-01 may map them, or `SyntheticInternetFallback`, to a customer recipe only after the customer document is analyzed.

Add a cancellation-aware overload to `ComplianceZoneResolver` with the parameter order shown above. Keep both existing overloads source- and behavior-compatible by delegating with `CancellationToken.None`. Check cancellation while copying unseen ranges, scanning zones, and processing remaining ranges. Do not extract or replace its Internet/local algorithm.

## Failure semantics

`CreationRuleZoneResolutionFailureCategory` contains exactly:

| Category | Meaning | Diagnostic |
| --- | --- | --- |
| `InvalidInput` | Missing task/elements, missing active source or destination, or unknown participating action. | `The creation-rule zone input is invalid.` |
| `UnsupportedTask` | The task is not an access-rule creation. | `The request task is not a supported access-rule creation.` |
| `GroupUnresolved` | A group is missing, ambiguous, invalid, empty, or not yet catalogued. | `A referenced network group could not be resolved.` |
| `GroupQueryFailed` | Flow group loading failed unexpectedly. | `Referenced network groups could not be loaded.` |
| `UnsupportedObject` | A direct object or group member has no usable numeric address representation. | `A network object has no supported address representation.` |
| `InvalidAddress` | Address text, endpoints, order, or address families are invalid. | `A network address is invalid.` |
| `InvalidZoneData` | Designated-matrix zones are absent, inconsistent, malformed, or missing the required automatic Internet zone. | `The designated zone data is incomplete or invalid.` |
| `UnexpectedFailure` | Any other unexpected failure. | `The creation-rule zones could not be resolved.` |

Unknown enum values use the `UnexpectedFailure` diagnostic. Diagnostics never contain object/group names, IP addresses, rule contents, query text, or exception messages.

Check the caller token before group calls and during element, range, and zone loops. Rethrow caller cancellation as `OperationCanceledException`. Translate other exceptions from `IFlowGroupResolver` to `GroupQueryFailed`; translate recognized address parsing/validation exceptions to `InvalidAddress`; translate every other unexpected exception to `UnexpectedFailure`.

## Exact change set

Production files:

- `roles/lib/files/FWO.Compliance/RulePositioning/ICreationRuleZoneResolver.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/CreationRuleZoneResolver.cs`
- `roles/lib/files/FWO.Compliance/ComplianceZoneResolver.cs`
- `roles/lib/files/FWO.Services/Workflow/RequestedRulePolicyChecker.cs`
- `roles/middleware/files/FWO.Middleware.Server/Services/FlowCatalogService.cs`
- `roles/middleware/files/FWO.Middleware.Server/Program.cs`

Tests:

- `roles/tests-unit/files/FWO.Test/CreationRuleZoneResolverTest.cs`
- `roles/tests-unit/files/FWO.Test/ComplianceZoneResolverTest.cs`
- `roles/tests-unit/files/FWO.Test/FlowCatalogServiceTest.cs`
- `roles/tests-unit/files/FWO.Test/ComplianceRequestedRulePolicyCheckerTest.cs`

Documentation updated after implementation:

- this file, with validation evidence;
- `documentation/work-packages/firewall-rule-placement/phase-3/README.md`; and
- `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`.

Do not change database SQL, Hasura metadata, GraphQL query files, importer code, product versioning, revision history, UI help, installer roles, positioning enums, customer recipes, or workflow controllers.

## Required tests

### Resolver contract and task filtering

`CreationRuleZoneResolverTest.cs` must cover:

1. One source and one destination each resolving to one persisted zone.
2. Multiple elements, multiple matching zones, and overlapping zones remain present and ordered by ID.
3. `create`, `unchanged`, `modify`, and `addAfterCreation` participate; `delete` and irrelevant fields do not.
4. Missing elements, no active source/destination, and unknown participating actions return `InvalidInput`.
5. Non-access or non-create tasks return `UnsupportedTask` without group resolution.
6. Source and destination are resolved independently and no Cartesian pair is constructed.

### Address and group behavior

Cover:

1. IPv4 and IPv6 hosts, CIDRs, and explicit ranges.
2. Valid `Cidr`/`CidrEnd` taking precedence over conflicting raw strings.
3. Duplicate normalized ranges do not duplicate zones.
4. Malformed addresses, reversed endpoints, and mixed address families return `InvalidAddress`.
5. Direct objects and dynamic/domain-like references without numeric addresses return `UnsupportedObject`.
6. Groups resolve by positive ID or unique case-insensitive name, and an ID takes precedence over a supplied name.
7. Repeated aliases are deduplicated, more than 100 selectors are chunked, and every technical member participates.
8. Missing, ambiguous, invalid, empty, or not-yet-catalogued groups return `GroupUnresolved`.
9. Invalid/empty group members return the correct object/address failure without silently dropping the member.
10. A thrown group query returns `GroupQueryFailed` without leaking the sentinel exception text.

### Internet, coverage, and immutability

Cover:

1. A persisted automatic Internet zone is matched when automatic calculation is enabled.
2. Missing, duplicate, or malformed required automatic Internet-zone data returns `InvalidZoneData`.
3. Uncovered IPv4 produces `SyntheticInternetFallback` when automatic calculation is disabled.
4. The synthetic result has `Id = null`, sorts last, and remains identifiable when its localized name changes.
5. Uncovered IPv6 produces `Partial` alongside matched zones and `None` when no zone matches.
6. Fully covered inputs produce `Complete`.
7. Empty, foreign-criterion, duplicate-ID, and malformed zone collections return `InvalidZoneData`.
8. Mutating request elements, supplied zones, or source collections after success cannot change the returned input.
9. Failure diagnostics contain no names, IPs, task content, query content, or exception text.

### Cancellation and compatibility

Cover pre-cancellation, cancellation while group queries await, and cancellation during large address/zone loops. All propagate `OperationCanceledException` and prevent later work.

Extend `ComplianceZoneResolverTest.cs` to prove its existing overloads retain current persisted-zone, overlap, IPv4 fallback, and IPv6-unassignable behavior, while the new overload observes cancellation.

Extend `FlowCatalogServiceTest.cs` to prove cancellation before and during group loading propagates and that the existing ID/name/member behavior is unchanged. Update the `ComplianceRequestedRulePolicyCheckerTest.cs` fake and retain its existing assertions to prove optional-token source compatibility.

## Validation

Run focused tests:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CreationRuleZoneResolverTest|FullyQualifiedName~ComplianceZoneResolverTest|FullyQualifiedName~FlowCatalogServiceTest|FullyQualifiedName~ComplianceRequestedRulePolicyCheckerTest"
```

Then run:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CreationRuleZoneResolverTest" --collect:"XPlat Code Coverage" --results-directory TestResults/WP02
```

Confirm more than 80% line coverage for the new production resolver and result-contract code. No disposable-VM installer or browser validation is required because this package changes no schema, metadata, installation behavior, configuration surface, or UI.

### Validation evidence

Validated locally on 2026-10-18:

- focused suite: 99 passed, 0 failed;
- `dotnet format roles/FWO.sln --no-restore`: passed; unrelated pre-existing formatting differences were restored and left untouched;
- Debug solution build: passed with 0 warnings and 0 errors;
- complete unit suite: 7,809 passed, 29 skipped, 0 failed;
- resolver coverage suite: 29 passed, 0 failed;
- `CreationRuleZoneResolver`: 94.75% line coverage; and
- public result-contract classes: each above 90% line coverage.

## Acceptance criteria

- Supported new-rule tasks deterministically produce immutable ordered source and destination zone sets.
- Existing request-visible Flow groups are fully expanded; unresolved groups or members never disappear silently.
- The existing `ComplianceZoneResolver` remains the owner of overlap and Internet/local resolution behavior.
- The synthetic fallback has an explicit non-localized identity in the positioning result.
- Multiple matches and incomplete IPv6 coverage are preserved for later policy decisions.
- Invalid input and data failures are explicit and safe; caller cancellation propagates.
- No customer placement behavior or production workflow is activated.

## Out of scope

- Loading the designated zone matrix.
- Resolving a group from sibling request tasks before it exists in Flow.
- Import/snapshot loading.
- Customer zone-to-scope mapping.
- Recipe construction or execution.
- Check Point request rendering.
- Strategy registration, workflow activation, or position persistence.
