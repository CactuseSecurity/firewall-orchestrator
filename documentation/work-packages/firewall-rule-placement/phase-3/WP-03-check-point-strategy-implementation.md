# Phase 3 WP-03 implementation - Check Point strategy orchestration

## Status

Implemented and locally validated on 2026-10-18.

Roadmap amendment D-035 keeps the implemented zone-dependent orchestration inactive for Phase 4 customer algorithms. Phase 3 WP-05 must not register this shape as a generic capability; it must provide or extract a graph-only path while preserving exact-one-match strategy selection.

## Implementation result

- Added the cancellation-aware designated-zone-matrix reader and reused it from `ComplianceZoneService` without changing public routes or response models.
- Added the inactive Check Point recipe-catalog contract and orchestration strategy with the specified sequential, fail-fast failure and cancellation behavior.
- Registered only `IDesignatedZoneMatrixReader`; the strategy, catalog, executor, manager, and workflow path remain unregistered.
- Added focused reader, compatibility, orchestration, failure-mapping, cancellation, concurrency, and registration-boundary tests.
- Added no customer recipe, enum mapping, persistence, rendering, vendor communication, schema, UI, installer, or versioning change.

This brief is self-contained. Implement only this work package and do not reopen the paired planning brief unless repository evidence makes this design impossible or unsafe.

## Goal

Implement a production-quality but inactive `CheckPointRulePositioningStrategy` that loads one imported rulebase graph, loads the designated zone matrix, resolves the new rule's source and destination zone input, selects one complete injected recipe, and executes it through the Phase 2 executor.

WP-03 adds the reusable orchestration and catalog seam only. It adds no customer recipe, enum-to-recipe mapping, strategy registration, manager registration, workflow caller, persistence call, renderer, or vendor API call.

## Routing and ownership

Impacted Layers: Shared libraries (`FWO.Compliance`, `FWO.Services` contracts consumed unchanged), middleware dependency injection for the designated-zone reader only, middleware compliance-zone service, unit tests, planning documentation

Recommended Skills:
- `fwo-route-request`

Minimal Change Set:
- Add one shared designated-zone-matrix reader and make the existing middleware zone service reuse it.
- Add the Check Point recipe-catalog contract and inactive strategy under `FWO.Compliance`.
- Register only the shared designated-zone reader as a singleton.
- Add focused reader, service-compatibility, strategy-orchestration, failure, cancellation, and registration-boundary tests.

Ripple Checklist:
- [x] Keep `RulePositioningManager` vendor-neutral and unchanged.
- [x] Keep `IRulebaseGraphSnapshotReader`, `ICreationRuleZoneResolver`, Phase 2 recipes, and positioning enums unchanged.
- [x] Preserve existing `ComplianceZoneService` endpoint behavior while removing its duplicate matrix-loading implementation.
- [x] Register no `IRulePositioningStrategy`, recipe catalog, or `RulePositioningManager` in production.
- [x] Add no customer recipe or zone-to-scope mapping.
- [x] Keep schema, Hasura, auth, installer, importer, UI, versioning, and workflow activation unchanged.

Risk Notes:
- The Phase 1 strategy contract supplies no zone collection, while WP-02 deliberately requires caller-loaded designated-matrix zones. The new reader closes that orchestration gap without moving GraphQL access into the strategy or resolver.
- Check Point R8x device type ID `9` is used by target gateways (and ordinary/domain managements); MDS type ID `13` is not a target gateway capability.
- The catalog is the single capability source. Phase 3 WP-05 must populate it with at least one confirmed generic capability before registering the strategy, preventing a strategy from advertising an algorithm whose complete recipe set is absent. Phase 4 may later extend it with customer mappings.

## Fixed requirements

- Implement `IRulePositioningStrategy` without changing `RulePositioningManager` or adding a vendor switch.
- Consume `IRulebaseGraphSnapshotReader` and `ICreationRuleZoneResolver` through their existing interfaces.
- Load designated-matrix rows through the new shared reader; do not query GraphQL from the strategy.
- Select a complete recipe through the explicit injected catalog contract defined below.
- Execute exactly one selected recipe through `PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue>`.
- Load at most one graph, load the designated matrix at most once, resolve zone input at most once, select at most once, and execute at most once per calculation.
- Preserve the exact configured `ProvisioningPositioningAlgorithm` in every successful `CheckPointRulePosition`.
- Propagate caller cancellation and convert all non-cancellation failures through the mapping in this brief.
- Add no customer recipe, enum mapping, default recipe, fallback recipe, or production catalog.
- Do not register the strategy or connect any workflow caller.
- Do not inspect `FWO_Regel_Platzierung_Regelwerk.md`.

## Designated-zone-matrix reader

Add `IDesignatedZoneMatrixReader.cs` under `roles/lib/files/FWO.Compliance/RulePositioning/` in namespace `FWO.Compliance.RulePositioning`:

```csharp
public interface IDesignatedZoneMatrixReader
{
    Task<DesignatedZoneMatrixReadResult> ReadAsync(CancellationToken cancellationToken);
}
```

Define the result contracts in the same file:

- abstract `DesignatedZoneMatrixReadResult`;
- sealed `DesignatedZoneMatrixReadSuccess`, exposing `IReadOnlyList<ComplianceNetworkZone> NetworkZones` and defensively copying the supplied collection into a read-only collection;
- sealed `DesignatedZoneMatrixReadFailure`, exposing `DesignatedZoneMatrixReadFailureCategory Category` and a fixed category-derived `string Diagnostic`; and
- `DesignatedZoneMatrixReadFailureCategory` with exactly:
  - `ConfigurationInvalid` when `GlobalConfig.DesignatedZoneMatrixId` is non-positive or the matrix lookup returns contradictory/multiple data;
  - `MatrixUnavailable` when the configured active matrix does not exist; and
  - `QueryFailed` when either API/GraphQL/deserialization operation fails.

Use these safe diagnostics:

| Category | Diagnostic |
| --- | --- |
| `ConfigurationInvalid` | `The designated zone-matrix configuration is invalid.` |
| `MatrixUnavailable` | `The designated zone matrix is unavailable.` |
| `QueryFailed` | `The designated zone matrix could not be loaded.` |

Unknown enum values use the `QueryFailed` diagnostic. Constructors reject a null success collection and do not accept caller-supplied diagnostic text. The collection is read-only, but its existing `ComplianceNetworkZone` elements remain the established mutable data model; WP-02 remains responsible for validating them and producing immutable positioning input.

Add `DesignatedZoneMatrixReader.cs` in the same directory and namespace. Its only constructor dependencies are:

- `ApiConnection`; and
- `GlobalConfig`.

The reader is stateless and performs this exact sequence:

1. Check caller cancellation.
2. Capture `GlobalConfig.DesignatedZoneMatrixId` once into a local value.
3. Return `ConfigurationInvalid` without an API call when the captured ID is non-positive.
4. Query `ComplianceQueries.getMatrixById` with the captured ID and await it through `WaitAsync(cancellationToken)`.
5. Return `MatrixUnavailable` for zero rows. Require exactly one row whose ID equals the captured ID; otherwise return `ConfigurationInvalid`.
6. Check cancellation again.
7. Query `NetworkZoneQueries.getNetworkZonesForMatrix` with the same captured ID and await it through `WaitAsync(cancellationToken)`.
8. Return success with the returned zones, treating a null GraphQL payload as an empty collection. An empty zone collection is not a reader failure; the existing WP-02 resolver owns zone-data validation and will return `InvalidZoneData`.

Rethrow `OperationCanceledException` when the caller token is cancelled. Translate every other exception from either query to `QueryFailed` without returning or logging exception text, query text, matrix names, zone names, addresses, or credentials.

Register `IDesignatedZoneMatrixReader` to `DesignatedZoneMatrixReader` as a singleton in `roles/middleware/files/FWO.Middleware.Server/Program.cs`. Its dependencies are already singletons and it retains no per-call state.

Refactor `ComplianceZoneService` to receive `IDesignatedZoneMatrixReader` and `GlobalConfig` rather than `ApiConnection` and `GlobalConfig`. Its private loader calls the reader with `CancellationToken.None` and preserves the existing public methods and behavior:

- reader success returns a fresh `List<ComplianceNetworkZone>` for the existing service logic;
- `ConfigurationInvalid` and `MatrixUnavailable` continue to behave as no loaded zones and return an empty list through the existing endpoints; and
- `QueryFailed` throws a new generic `InvalidOperationException` containing only the reader's safe diagnostic, preserving the endpoint's error path without leaking the caught exception.

Do not change the REST routes, response models, object-resolution behavior, or public method signatures of `ComplianceZoneService`.

## Recipe-catalog contract

Add `ICheckPointPositioningRecipeCatalog.cs` under `roles/lib/files/FWO.Compliance/RulePositioning/CheckPoint/` in namespace `FWO.Compliance.RulePositioning.CheckPoint`:

```csharp
public interface ICheckPointPositioningRecipeCatalog
{
    bool SupportsAlgorithm(ProvisioningPositioningAlgorithm positioningAlgorithm);

    CheckPointRecipeSelectionResult SelectRecipe(
        ProvisioningPositioningAlgorithm positioningAlgorithm,
        CreationRuleZoneInput zoneInput);
}
```

The contract is synchronous and side-effect-free. Catalog construction may build and validate immutable recipes, but capability checks and selection perform no I/O, mutation, logging, persistence, or vendor communication.

Define these result contracts in the same file:

- abstract `CheckPointRecipeSelectionResult`;
- sealed `CheckPointRecipeSelectionSuccess`, exposing one non-null `PositioningRecipe<CheckPointPositioningContext, CheckPointBlockValue> Recipe`;
- sealed `CheckPointRecipeSelectionFailure`, exposing `CheckPointRecipeSelectionFailureCategory Category` and a fixed safe diagnostic; and
- `CheckPointRecipeSelectionFailureCategory` with exactly:
  - `NoMatchingRecipe`, when a supported algorithm has no recipe for the supplied immutable zone input; and
  - `CatalogInvalid`, when the catalog detects incomplete, duplicate, contradictory, or otherwise invalid definitions.

Use these diagnostics:

| Category | Diagnostic |
| --- | --- |
| `NoMatchingRecipe` | `No Check Point positioning recipe matches the supplied input.` |
| `CatalogInvalid` | `The Check Point positioning recipe catalog is invalid.` |

Unknown enum values use the `CatalogInvalid` diagnostic. Constructors reject null success recipes and do not accept caller-supplied diagnostics.

WP-03 adds no implementation of `ICheckPointPositioningRecipeCatalog`. Tests use an explicit fake catalog. Phase 3 WP-05 will add the immutable production catalog, confirmed generic recipes and mappings, and registration. Customer-derived recipes and mappings are deferred to Phase 4.

## Capability behavior

`CheckPointRulePositioningStrategy.CanHandle` returns true only when both conditions hold:

1. `deviceType` is non-null and its exact ID is `9`, the Check Point R8x target-gateway type used by normalized devices; and
2. the injected catalog's `SupportsAlgorithm` returns true for the exact supplied enum value.

Use a named private constant for device type ID `9`; do not compare names, versions, manufacturers, localized text, or MDS type `13`. Reject `Undefined` and unknown enum values before consulting the catalog. Do not maintain a second supported-algorithm set in the strategy.

The catalog is the sole algorithm-capability source. A catalog capability exception is allowed to escape `CanHandle`; the existing `RulePositioningManager` converts it to safe `CalculationFailed`, as already tested. There is no fallback to another Check Point algorithm or vendor.

## Strategy implementation

Add `CheckPointRulePositioningStrategy.cs` beside the catalog contract in namespace `FWO.Compliance.RulePositioning.CheckPoint`. It is `sealed`, implements `IRulePositioningStrategy`, and has exactly these constructor dependencies:

- `IRulebaseGraphSnapshotReader`;
- `IDesignatedZoneMatrixReader`;
- `ICreationRuleZoneResolver`; and
- `ICheckPointPositioningRecipeCatalog`.

Reject null constructor dependencies. Hold one private `PositioningRecipeExecutor<CheckPointPositioningContext, CheckPointBlockValue>` instance created by the strategy; the executor is stateless and does not need a DI registration or test wrapper.

The strategy and all four dependencies are singleton-safe because they retain no calculation-specific mutable state. WP-03 registers only the designated-zone reader. It does not register the strategy, catalog, executor, or manager.

The existing DI convention for strategy selection is ordinary collection injection: `RulePositioningManager` accepts `IEnumerable<IRulePositioningStrategy>`. No keyed-service convention exists or is needed here. When WP-05 activates the path, it must use the normal multi-registration collection and preserve the manager's exact-one-match check; WP-03 adds no such registration.

### Exact operation order

`CalculateAsync` performs the following sequence and stops immediately on the first failure:

1. Check caller cancellation.
2. Validate the minimum direct-call context required to avoid null dereferences: context, target gateway/device type, target management, parent request task, effective settings, positive management/gateway IDs, and a defined non-`Undefined` algorithm. Invalid values return `InvalidInput` except an invalid algorithm, which returns `ConfigurationInvalid`.
3. Require exact Check Point device type ID `9` and catalog support for the configured algorithm. A mismatch returns `PositioningMethodNotImplemented` without loading data. This defensive direct-call check does not replace manager validation.
4. Call `IRulebaseGraphSnapshotReader.ReadAsync(targetManagement.Id, targetGateway.Id, cancellationToken)` exactly once.
5. On graph success, call `IDesignatedZoneMatrixReader.ReadAsync(cancellationToken)` exactly once.
6. On zone-reader success, call `ICreationRuleZoneResolver.ResolveAsync(parentRequestTask, networkZones, cancellationToken)` exactly once.
7. On zone-resolution success, call `ICheckPointPositioningRecipeCatalog.SelectRecipe(configuredAlgorithm, zoneInput)` exactly once.
8. Check cancellation after the synchronous selection boundary.
9. Construct one `CheckPointPositioningContext` from the original `RulePositioningContext` instance and the single loaded snapshot.
10. Call the Phase 2 executor exactly once with the selected recipe, constructed Check Point context, and original cancellation token.
11. Require executor success to contain a `CheckPointPositionValue`. Require its `CheckPointRulePosition.PositioningAlgorithm` to equal the exact configured algorithm. Any other successful block value or algorithm mismatch returns `CalculationFailed`.
12. Return `RulePositioningSuccess` containing the exact validated `CheckPointRulePosition` instance.

Do not parallelize graph and zone loading: graph failure must prevent all zone work. Do not retry, loop over recipes, select a second recipe, or fall back after any failure.

### Failure translation

Return a new Phase 1 `RulePositioningFailure` using this exact mapping. Never forward child diagnostics into a new string.

| Source | Child category | Phase 1 category |
| --- | --- | --- |
| Direct strategy validation | missing/invalid reference or ID | `InvalidInput` |
| Direct strategy validation | undefined/unknown algorithm | `ConfigurationInvalid` |
| Direct capability | wrong device or catalog does not support algorithm | `PositioningMethodNotImplemented` |
| Graph reader | `InvalidInput`, `ScopeMismatch` | `InvalidInput` |
| Graph reader | `NoSuccessfulImport`, `InvalidGraphData`, `QueryFailed`, unknown | `CalculationFailed` |
| Designated-zone reader | `ConfigurationInvalid`, `MatrixUnavailable` | `ConfigurationInvalid` |
| Designated-zone reader | `QueryFailed`, unknown | `CalculationFailed` |
| Zone resolver | `InvalidInput`, `UnsupportedObject`, `InvalidAddress` | `InvalidInput` |
| Zone resolver | `UnsupportedTask` | `UnsupportedTask` |
| Zone resolver | `InvalidZoneData` | `ConfigurationInvalid` |
| Zone resolver | `GroupUnresolved`, `GroupQueryFailed`, `UnexpectedFailure`, unknown | `CalculationFailed` |
| Recipe catalog | `NoMatchingRecipe` | `PositioningMethodNotImplemented` |
| Recipe catalog | `CatalogInvalid`, unknown | `ConfigurationInvalid` |
| Recipe executor | `InvalidRecipe` | `ConfigurationInvalid` |
| Recipe executor | `InvalidSnapshot`, `ScopeNotFound`, `ScopeAmbiguous`, `NoInsertionPoint`, `UnexpectedExecutionFailure`, unknown | `CalculationFailed` |
| Final output validation | wrong value type, null-equivalent invariant failure, or algorithm mismatch | `CalculationFailed` |
| Any unexpected non-cancellation exception | any stage | `CalculationFailed` |

Check cancellation before each asynchronous dependency call and before execution. Rethrow `OperationCanceledException` under the same association rule as `RulePositioningManager`: when the caller token is cancelled, or when the exception carries that same cancellable token. An unrelated `OperationCanceledException` is an unexpected failure and maps to `CalculationFailed`.

Diagnostics exposed by the strategy are only the fixed Phase 1 diagnostics. They must not contain exception messages, credentials, management/gateway names, zone names or addresses, rule UIDs, block kinds, recipe details, or complete rule contents.

## Exact change set

Production files:

- `roles/lib/files/FWO.Compliance/RulePositioning/IDesignatedZoneMatrixReader.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/DesignatedZoneMatrixReader.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/CheckPoint/ICheckPointPositioningRecipeCatalog.cs`
- `roles/lib/files/FWO.Compliance/RulePositioning/CheckPoint/CheckPointRulePositioningStrategy.cs`
- `roles/middleware/files/FWO.Middleware.Server/Services/ComplianceZoneService.cs`
- `roles/middleware/files/FWO.Middleware.Server/Program.cs`

Tests:

- `roles/tests-unit/files/FWO.Test/DesignatedZoneMatrixReaderTest.cs`
- `roles/tests-unit/files/FWO.Test/CheckPointRulePositioningStrategyTest.cs`
- `roles/tests-unit/files/FWO.Test/ComplianceZoneServiceTest.cs`
- `roles/tests-unit/files/FWO.Test/ComplianceControllerTest.cs`

Documentation updated after implementation:

- this file, with implementation status and validation evidence;
- `documentation/work-packages/firewall-rule-placement/phase-3/README.md`; and
- `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`.

Do not change `RulePositioningManager`, Phase 1 result categories, Phase 2 block/executor contracts, WP-01/WP-02 contracts, GraphQL files, database SQL, Hasura metadata, importer code, product versioning, revision history, UI help, installer roles, provisioning enums, workflow handlers/controllers, persistence, renderer code, or the customer document.

## Required tests

### Designated-zone reader

`DesignatedZoneMatrixReaderTest.cs` must cover:

1. A valid configured matrix produces one success containing the zone rows, performs the matrix query before the zone query, and passes the same captured criterion ID to both.
2. A non-positive configured ID returns `ConfigurationInvalid` without an API call.
3. Zero matrix rows returns `MatrixUnavailable` without a zone query.
4. Multiple or mismatched matrix rows return `ConfigurationInvalid` without a zone query.
5. A null or empty zone payload produces a successful empty read, leaving validation to WP-02.
6. Failure at either query returns `QueryFailed`; its diagnostic contains none of a sentinel exception, query, matrix name, zone name, or address; and the later query is not called.
7. Pre-cancellation and cancellation while either query awaits propagate `OperationCanceledException` and prevent later work.
8. The returned collection cannot be changed through its public interface, and later mutation of the API-owned list does not add or remove entries from the copied collection.

### Existing compliance-zone service compatibility

Update `ComplianceZoneServiceTest.cs` and the constructor helper in `ComplianceControllerTest.cs` for the new reader dependency. Preserve all existing assertions. Add focused cases proving:

- reader success retains the current designated-zone list and object-resolution behavior;
- `ConfigurationInvalid` and `MatrixUnavailable` retain the current empty-list behavior; and
- `QueryFailed` takes the existing endpoint failure path through a generic exception containing only the safe reader diagnostic.

### Capability and full orchestration

`CheckPointRulePositioningStrategyTest.cs` must use recording fakes and test-only recipes. It must not add a production catalog or imply that any enum maps to a Phase 2 example.

Cover:

1. `CanHandle` is true for exact device type ID `9` plus a catalog-supported defined algorithm.
2. It is false for MDS type `13`, other device types, null device type, `Undefined`, unknown enum values, and a catalog-unsupported algorithm.
3. A catalog capability exception remains observable from direct `CanHandle`; an integration case with `RulePositioningManager` proves it becomes safe `CalculationFailed` and performs no strategy calculation.
4. One test-only complete recipe succeeds through graph read, designated-zone read, zone resolution, catalog selection, and executor evaluation in that exact order.
5. The same original context, snapshot, zone input, configured algorithm, and cancellation token reach their intended boundaries.
6. Exactly one graph read, one designated-zone read, one zone resolution, one recipe selection, and one root execution occur. No stage is retried or called after a later result has been obtained.
7. The returned type is `RulePositioningSuccess` containing the executor's `CheckPointRulePosition`, and its algorithm exactly equals the configured algorithm.
8. A successful block value that is not `CheckPointPositionValue`, or a position containing a different algorithm, returns `CalculationFailed`.

The test-only recipe may use the existing structural blocks solely to exercise orchestration. Its chosen enum is test fixture data and must not be represented as a production mapping.

### Fail-fast failure translation

Use parameterized cases for every child category in the mapping table and assert the exact Phase 1 category and fixed diagnostic. Also assert downstream call counts are zero:

- graph failure prevents designated-zone loading, zone resolution, selection, and execution;
- designated-zone read failure prevents zone resolution, selection, and execution;
- zone-resolution failure prevents selection and execution;
- recipe-selection failure prevents execution; and
- a block failure is returned after exactly one execution without fallback.

Unknown child enum values must exercise the documented conservative mapping. Unexpected exceptions from each dependency, selection, and final processing return safe `CalculationFailed` without leaking sentinel content.

### Cancellation and singleton safety

Cover:

- pre-cancellation before dependency work;
- in-flight cancellation during graph loading, designated-zone loading, zone resolution, and block execution;
- cancellation triggered during synchronous selection and observed before execution;
- unrelated `OperationCanceledException` becoming `CalculationFailed`; and
- concurrent calculations through one strategy instance retaining separate context, snapshot, zone input, and output without shared per-call state.

All caller-associated cancellation propagates and prevents subsequent work.

### Inactive registration boundary

Add a source-level registration assertion following the existing `Program.cs` registration-test pattern. It must prove:

- `IDesignatedZoneMatrixReader` is registered to `DesignatedZoneMatrixReader` as a singleton;
- `IRulebaseGraphSnapshotReader` and `ICreationRuleZoneResolver` remain registered as before; and
- `Program.cs` contains no registration of `CheckPointRulePositioningStrategy`, `ICheckPointPositioningRecipeCatalog`, `IRulePositioningStrategy`, or `RulePositioningManager`.

This test is an intentional activation gate. WP-05 must update it when production recipes and registration are approved.

## Validation

Run focused tests:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~DesignatedZoneMatrixReaderTest|FullyQualifiedName~CheckPointRulePositioningStrategyTest|FullyQualifiedName~ComplianceZoneServiceTest|FullyQualifiedName~ComplianceControllerTest|FullyQualifiedName~RulePositioningManagerTest|FullyQualifiedName~CreationRuleZoneResolverTest|FullyQualifiedName~RulebaseGraphSnapshotReaderTest|FullyQualifiedName~CheckPointStructuralBlocksTest"
```

Then run:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~DesignatedZoneMatrixReaderTest|FullyQualifiedName~CheckPointRulePositioningStrategyTest" --collect:"XPlat Code Coverage" --results-directory TestResults/WP03
```

Confirm more than 80% line coverage for every new production class. If repository-wide formatting changes unrelated files, restore only those formatter-created unrelated differences while preserving pre-existing user changes.

No disposable-VM installer or browser validation is required because WP-03 changes no schema, Hasura metadata, installation behavior, configuration surface, UI, or versioned upgrade path.

### Validation evidence (2026-10-18)

- Required focused regression filter: 281 passed, 0 failed.
- `dotnet format roles/FWO.sln`: completed; no unrelated formatter changes were introduced.
- `dotnet build --configuration Debug roles/FWO.sln`: succeeded with 0 warnings and 0 errors.
- Complete unit suite: 7,896 passed, 29 skipped, 0 failed (7,925 total).
- Focused coverage run: 84 passed, 0 failed. Cobertura reports 100% line coverage for `DesignatedZoneMatrixReader`, `CheckPointRulePositioningStrategy`, and the new result-contract classes; executable branch coverage is at least 92.42%.
- Installer/VM and browser validation were not applicable under this work package's unchanged installer, schema, configuration, and UI boundaries.

## Acceptance criteria

- A test-only recipe proves the complete orchestration path and exact operation order.
- The manager remains vendor-neutral and unchanged.
- The catalog is the sole algorithm capability source and contains no production implementation or mapping in WP-03.
- The strategy handles only exact target device type ID `9`; MDS type `13` is not treated as a target gateway.
- One calculation loads at most one graph and designated matrix and executes at most one recipe, with no fallback.
- Every child failure and cancellation has deterministic behavior.
- Successful positions preserve the exact configured algorithm identity.
- Only the reusable designated-zone reader is registered; no incomplete strategy can be selected in production.
- Calculation performs no persistence, rendering, workflow action, vendor API call, or other external mutation.
- The customer document remains unopened.

## Out of scope

- Customer-document analysis, customer blocks, recipes, zone mappings, and enum mappings.
- Production recipe-catalog implementation or registration.
- Strategy or manager registration.
- Workflow activation, caller integration, persistence, or stale-position clearing.
- Check Point request rendering or API communication.
- Changes to imported graph semantics or existing-rule semantic fields.
- Same-ticket pre-catalog Flow group resolution.
- Schema, Hasura, auth, importer, installer, UI, and version changes.
