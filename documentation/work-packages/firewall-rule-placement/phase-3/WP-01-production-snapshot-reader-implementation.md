# Phase 3 WP-01 implementation - Production snapshot reader

## Status

Implemented and locally validated on 2026-10-18.

The production reader, query contracts, dependency-injection registration, and tests described here are present at commit `b7300acef`.

## Goal

Add the production reader that selects the latest successfully completed normalized rule import for one management, loads the structural graph for one gateway at that fixed import, and returns one validated immutable `RulebaseGraphSnapshot`.

## Fixed requirements

- Use the latest successfully completed rule import regardless of age.
- Scope every loaded link, rulebase, and structural rule to the same import ID, management, and gateway.
- Construct the existing `RulebaseGraphSnapshot`; do not introduce a parallel policy model.
- Preserve its eager validation and defensive-copy boundary.
- Fail explicitly when no successful import exists or returned data is missing, contradictory, or malformed.
- Propagate caller cancellation.
- Keep caller-visible diagnostics free of credentials, exception text, names, UIDs, and rule contents.
- Do not add a freshness setting, stale-import rejection, automatic re-import, snapshot fingerprint, importer call, vendor API call, recipe selection, strategy registration, workflow activation, persistence, or UI.

## Public contracts and ownership

Add `IRulebaseGraphSnapshotReader.cs` under `roles/lib/files/FWO.Services/RulePositioning/` with these public contracts in namespace `FWO.Services.RulePositioning`:

```csharp
public interface IRulebaseGraphSnapshotReader
{
    Task<RulebaseGraphReadResult> ReadAsync(
        int managementId,
        int gatewayId,
        CancellationToken cancellationToken);
}
```

Add the following result types in the same file:

- abstract `RulebaseGraphReadResult`;
- sealed `RulebaseGraphReadSuccess`, exposing a non-null `RulebaseGraphSnapshot Snapshot`;
- sealed `RulebaseGraphReadFailure`, exposing `RulebaseGraphReadFailureCategory Category` and a fixed category-derived `string Diagnostic`; and
- `RulebaseGraphReadFailureCategory` with exactly:
  - `InvalidInput` for a non-positive management or gateway ID;
  - `NoSuccessfulImport` when the management has no qualifying import;
  - `ScopeMismatch` when the gateway is absent or does not belong to the requested management;
  - `InvalidGraphData` for empty, incomplete, contradictory, historically inconsistent, or snapshot-invalid returned data; and
  - `QueryFailed` for API, GraphQL, or deserialization failures.

Use these safe diagnostics:

| Category | Diagnostic |
| --- | --- |
| `InvalidInput` | `The rulebase graph scope is invalid.` |
| `NoSuccessfulImport` | `No successfully completed policy import is available.` |
| `ScopeMismatch` | `The requested management and gateway scope is unavailable or inconsistent.` |
| `InvalidGraphData` | `The imported rulebase graph is incomplete or invalid.` |
| `QueryFailed` | `The imported rulebase graph could not be loaded.` |

Unknown enum values use the `QueryFailed` diagnostic. Constructors must reject null success snapshots and must not accept caller-supplied diagnostic text.

WP-03 will consume this typed result through the interface: success supplies only the immutable snapshot, while failure supplies only the stable category and safe diagnostic. WP-01 must not log or return caught exception text, and it must not translate reader failures into Phase 1 positioning categories; that translation remains an explicit WP-03 orchestration decision.

Implement `RulebaseGraphSnapshotReader.cs` in the same project and namespace. The reader owns query orchestration, wire-response validation, mapping to existing normalized entities, and snapshot construction. Its only constructor dependency is `ApiConnection`.

The reader returns the constructed snapshot, not mutable `Rulebase`, `RulebaseLink`, or wire DTO collections. Keep wire DTOs private to the implementation; annotate them for Newtonsoft deserialization and include only the fields listed below.

Register `IRulebaseGraphSnapshotReader` to `RulebaseGraphSnapshotReader` as a singleton in `roles/middleware/files/FWO.Middleware.Server/Program.cs`. The existing `ApiConnection` is also a singleton, and the reader holds no per-call mutable state. Do not register a positioning strategy or invoke the reader at startup.

## Exact GraphQL operations

Add `RulePositioningQueries.cs` under `roles/lib/files/FWO.Api.Client/Queries/`. Follow the existing static query-loader pattern and expose these three `public static readonly string` fields:

- `getLatestSuccessfulRuleImport`;
- `getGatewayRulebaseLinksAtImport`; and
- `getRulebasesAtImport`.

Load the following files from a new `roles/common/files/fwo-api-calls/rulePositioning/` directory.

### 1. Select the import once

`getLatestSuccessfulRuleImport.graphql`:

```graphql
query getLatestSuccessfulRuleImport($managementId: Int!, $importTypeId: Int!) {
  import_control(
    where: {
      mgm_id: { _eq: $managementId }
      import_type_id: { _eq: $importTypeId }
      successful_import: { _eq: true }
      stop_time: { _is_null: false }
    }
    order_by: [{ stop_time: desc }, { control_id: desc }]
    limit: 1
  ) {
    control_id
    mgm_id
    import_type_id
    successful_import
    stop_time
  }
}
```

Pass `ImportType.RULE` as `importTypeId`. In this reader, “successful and completed” means a management-scoped type-1 rule import with `successful_import = true` and non-null `stop_time`. Order by completion time, then the unique `control_id` for deterministic ties. Do not use the existing broader `getLastSuccessImport` query.

Validate that the result contains at most one row and that a returned row has a positive control ID and exactly matches the requested management, rule import type, success flag, and completion requirement. An empty result is `NoSuccessfulImport`; a contradictory row is `InvalidGraphData`.

### 2. Load gateway ownership and links at that import

`getGatewayRulebaseLinksAtImport.graphql`:

```graphql
query getGatewayRulebaseLinksAtImport($gatewayId: Int!, $importId: bigint!) {
  device(where: { dev_id: { _eq: $gatewayId } }, limit: 2) {
    dev_id
    mgm_id
    rulebase_links(
      where: {
        created: { _lte: $importId }
        _or: [
          { removed: { _is_null: true } }
          { removed: { _gt: $importId } }
        ]
      }
      order_by: { id: asc }
    ) {
      id
      gw_id
      from_rule_id
      from_rulebase_id
      to_rulebase_id
      link_type
      is_initial
      is_global
      is_section
      created
      removed
      rule {
        rule_id
        mgm_id
        rulebase_id
        rule_create
        removed
      }
    }
  }
}
```

Require exactly one returned gateway with matching `dev_id` and `mgm_id`. Zero, multiple, or foreign-management gateways are `ScopeMismatch`. Require at least one link; an empty link set is `InvalidGraphData` because no positionable graph can be constructed.

For every link, require a positive unique link ID, the requested gateway ID, a positive target rulebase ID, non-null positive creation ID, and validity at the selected import. If `from_rule_id` exists, require the nested rule summary and validate its identity, management, owning rulebase, and historical validity. Derive the complete rulebase ID set from every target ID, every explicit source-rulebase ID, and every source rule's owning rulebase ID.

### 3. Load only the endpoint rulebases and structural rules

`getRulebasesAtImport.graphql`:

```graphql
query getRulebasesAtImport(
  $managementId: Int!
  $rulebaseIds: [Int!]!
  $importId: bigint!
) {
  firewall_rulebase(
    where: {
      id: { _in: $rulebaseIds }
      mgm_id: { _eq: $managementId }
      created: { _lte: $importId }
      _or: [
        { removed: { _is_null: true } }
        { removed: { _gt: $importId } }
      ]
    }
    order_by: { id: asc }
  ) {
    id
    name
    uid
    mgm_id
    created
    removed
    rules(
      where: {
        mgm_id: { _eq: $managementId }
        rule_create: { _lte: $importId }
        _or: [
          { removed: { _is_null: true } }
          { removed: { _gt: $importId } }
        ]
      }
      order_by: [{ rule_num_numeric: asc }, { rule_id: asc }]
    ) {
      rule_id
      rule_uid
      mgm_id
      rulebase_id
      rule_num_numeric
      rule_create
      removed
    }
  }
}
```

Pass the distinct rulebase IDs in ascending order. Require the returned ID set to equal the requested set exactly. Validate every rulebase and nested rule against the selected import, management, and owning rulebase. Require every source rule referenced by a link to occur in the returned rulebase data. Missing, unexpected, duplicate, or contradictory data is `InvalidGraphData`.

Do not request existing-rule source, destination, service, action, zone, NAT, install-on, comment, or other semantic content.

## Historical consistency proof

Capture the import control ID once and pass that same immutable value to both graph queries. A row belongs to the completed post-import snapshot exactly when:

```text
created <= selectedImportId
and (removed is null or removed > selectedImportId)
```

The rule equivalent uses `rule_create`.

Consequences:

- a row removed by the selected import is excluded because `removed == selectedImportId`;
- a row removed before the selected import is excluded;
- a row created by any later failed or in-progress import is excluded because `created > selectedImportId`; and
- a row temporarily marked removed by a later import remains part of the selected snapshot because `removed > selectedImportId`.

Thus later activity cannot make one calculation mix the selected completed import with another import. If cleanup has physically removed required historical rows, endpoint-set and snapshot validation return `InvalidGraphData` rather than silently constructing a partial graph.

## Mapping, construction, and exceptions

After wire validation, create fresh existing normalized entities containing only:

- `Rulebase`: `Id`, `Name`, `Uid`, `MgmtId`, and mapped `Rules`;
- `Rule`: `Id`, `Uid`, `MgmtId`, `RulebaseId`, and `RuleNumNumeric`; and
- `RulebaseLink`: gateway/source/target identities, link type, and initial/global/section flags.

Pass those collections to `new RulebaseGraphSnapshot(managementId, gatewayId, rulebases, links)`. Let the existing constructor remain the owner of generic graph validation such as UID requirements, duplicate identities, deterministic native order, ownership, and endpoint resolution. Translate `ArgumentException` and related construction validation exceptions to `InvalidGraphData`; do not expose their messages.

Use `ApiConnection.SendQueryAsync` with the operation name matching each GraphQL operation. Check cancellation before every query and use `WaitAsync(cancellationToken)` because the current `ApiConnection` contract has no cancellation-token parameter. Rethrow caller cancellation. Translate every other API, GraphQL, or deserialization exception to `QueryFailed`.

Do not invoke the Check Point graph interpreter here. Vendor-specific initial-layer, ordered-successor, section, and inline ambiguity remains the interpreter's responsibility in later work packages.

## Exact change set

Production files:

- `roles/common/files/fwo-api-calls/rulePositioning/getLatestSuccessfulRuleImport.graphql`
- `roles/common/files/fwo-api-calls/rulePositioning/getGatewayRulebaseLinksAtImport.graphql`
- `roles/common/files/fwo-api-calls/rulePositioning/getRulebasesAtImport.graphql`
- `roles/lib/files/FWO.Api.Client/Queries/RulePositioningQueries.cs`
- `roles/lib/files/FWO.Services/RulePositioning/IRulebaseGraphSnapshotReader.cs`
- `roles/lib/files/FWO.Services/RulePositioning/RulebaseGraphSnapshotReader.cs`
- `roles/middleware/files/FWO.Middleware.Server/Program.cs`

Tests:

- `roles/tests-unit/files/FWO.Test/RulePositioningQueriesTest.cs`
- `roles/tests-unit/files/FWO.Test/RulebaseGraphSnapshotReaderTest.cs`

Do not change database SQL, Hasura metadata, importer code, product versioning, revision history, UI help, or installer roles.

## Required tests and fixtures

### Query contract tests

`RulePositioningQueriesTest.cs` must assert that:

- import selection contains the management, `ImportType.RULE`, success, completed, deterministic ordering, and limit clauses;
- link selection contains the fixed-import creation/removal interval and source-rule ownership fields;
- rulebase and nested-rule selection both contain the same fixed-import interval and management scope;
- rulebase and rule ordering is deterministic; and
- none of the graph queries request rule source, destination, service, action, zone, NAT, comments, credentials, or other semantic content.

### Reader tests

`RulebaseGraphSnapshotReaderTest.cs` must use a recording `SimulatedApiConnection`. Store query-specific JSON response fixtures in the test file and deserialize them to the requested generic response type with Newtonsoft, so tests cover the private wire DTO bindings as well as orchestration.

Cover:

1. A successful representative graph containing a policy/initial link, ordered or domain link, section link, inline link, empty structural scope, and out-of-order rules that appear in native order in the snapshot.
2. Exactly three calls in import, link, rulebase order; operation names and variables are correct; both graph calls receive the one selected import ID; derived rulebase IDs are distinct and sorted.
3. Query contracts exclude failed, unfinished, older, later-created, and already-removed rows without applying any age threshold.
4. Non-positive management/gateway IDs return `InvalidInput` without an API call.
5. An empty import response returns `NoSuccessfulImport` and makes no graph call.
6. Missing, duplicate, or foreign-management gateway rows return `ScopeMismatch` and do not load rulebases.
7. Empty links, missing endpoints, missing source summaries, missing source rules, mismatched ownership, temporal contradictions, duplicate returned IDs, and incomplete endpoint sets return `InvalidGraphData`.
8. Blank/duplicate UIDs, duplicate native order, and other `RulebaseGraphSnapshot` construction failures return `InvalidGraphData`.
9. A failure at each query stage returns `QueryFailed`; the diagnostic contains none of the thrown exception's sentinel text or fixture contents; later stages are not called.
10. Pre-cancellation and cancellation while each query is awaiting propagate `OperationCanceledException` and prevent later calls.
11. The fake observes only the three approved GraphQL operations, proving there is no clock/freshness lookup, importer call, or vendor call.

The unit boundary ends at `ApiConnection`. Do not add a database/Hasura integration test in this package: the schema and metadata are unchanged, the middleware-server role already has read permission for all selected fields, and the repository has no narrower seeded integration fixture for this service. The captured-query tests are the required contract seam.

## Validation

Run the focused tests:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~RulebaseGraphSnapshotReaderTest|FullyQualifiedName~RulePositioningQueriesTest|FullyQualifiedName~CheckPointPositioningContractsTest"
```

Then run:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~RulebaseGraphSnapshotReaderTest" --collect:"XPlat Code Coverage" --results-directory TestResults/WP01
```

Confirm more than 80% line coverage for the new production reader and query-loader code. No disposable-VM installer validation is required because this package changes no installation, schema, metadata, configuration, or versioned upgrade behavior.

Validation evidence recorded on 2026-10-18:

- 76 focused WP-01 and positioning-contract tests passed;
- the Debug solution build completed with 0 warnings and 0 errors;
- the complete unit suite passed with 7,777 passed, 29 skipped, and 0 failed;
- focused line coverage was 98.4% for `RulebaseGraphSnapshotReader` and 87.5% for `RulePositioningQueries`; and
- all WP-01 changed C# files passed targeted formatting verification. Repository-wide formatting verification still reports unrelated pre-existing differences in `UiDisplayImplementationTaskTest.cs` and `UiDisplayRequestTaskTest.cs`.

## Acceptance criteria

- One call returns one validated immutable graph for the requested management and gateway.
- The selected import is a completed successful type-1 rule import and is chosen deterministically.
- Every loaded graph row is evaluated against the same selected import ID.
- Later failed or in-progress imports cannot leak data into the graph.
- Missing, contradictory, or malformed data returns an explicit safe failure.
- Caller cancellation propagates.
- No customer-specific behavior or production strategy is activated.

## Out of scope

- Customer-document analysis.
- Workflow-element or zone resolution.
- Recipe selection or execution.
- Check Point graph interpretation beyond generic snapshot validation.
- Check Point request rendering or communication.
- Position persistence or workflow calls.
- Import freshness policy, fingerprints, or automatic imports.
- Customer-specific scope rules.
