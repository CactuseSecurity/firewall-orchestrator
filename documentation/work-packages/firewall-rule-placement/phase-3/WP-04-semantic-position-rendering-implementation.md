# Phase 3 WP-04 implementation - Semantic Check Point position rendering

## Status

Implemented and locally validated on 2026-10-19.

Superseded by D-036 for retained scope: Phase 3 WP-05 removes this renderer, its dedicated tests, and renderer-only compatibility assertions. Phase 4 must not recreate it.

This brief is self-contained. Implement only this work package and do not reopen the paired planning brief unless repository evidence makes this design impossible or unsafe.

## Goal

Render a valid `CheckPointRulePosition` into the existing Check Point access-rule request representation without changing calculation, persistence, or external execution behavior.

## Routing and ownership

Impacted Layers: Shared libraries (`FWO.ExternalSystems` consuming existing `FWO.Data` contracts unchanged), unit tests, planning documentation

Recommended Skills:
- `fwo-route-request`

Minimal Change Set:
- Add one pure Check Point request-body renderer beside the existing ticket/task/client code.
- Add focused renderer and compatibility tests around existing template-created task bodies.
- Keep request selection, workflow activation, strategy registration, and client execution unchanged.

Ripple Checklist:
- [x] Keep `CheckPointRulePosition`, `RulePosition`, persistence, and serializers unchanged.
- [x] Keep `CheckPointTicketTask.FillTaskText`, `CheckPointTicket`, `CheckPointClient`, and current execution-plan behavior unchanged.
- [x] Do not select an implementation task or infer a position from `WfReqTask.ImplementationTasks`.
- [x] Do not add a supported-algorithm list outside the recipe catalog.
- [x] Do not register a strategy, renderer, manager, catalog, or workflow caller.
- [x] Keep schema, Hasura, auth, installer, importer, middleware, UI, versioning, and customer mappings unchanged.

Risk Notes:
- One request task can contain multiple gateway-specific implementation tasks. This renderer accepts one explicitly selected position, but the Phase 3 product ends at semantic persistence and does not invoke it. A future placer integration must own renderer-specific selection.
- Existing Check Point rule tasks are rendered from configurable JSON templates, while rule execution is still rejected before `CheckPointClient.RestCall`. The renderer must preserve arbitrary non-placement template fields and must not weaken that execution gate.
- The WP-03 catalog remains the sole source of supported algorithms. The renderer checks exact equality with a caller-supplied expected algorithm and must not duplicate a Check Point algorithm allowlist.

## Fixed requirements

- Support `Top`, `Bottom`, `Above`, and `Below` semantic anchors.
- Require the existing rule UID only for `Above` and `Below`.
- Carry the target layer UID using the authoritative Check Point request shape settled by the planner.
- Validate wrong-vendor, unknown, null, malformed, and incompatible positions according to the settled design.
- Preserve existing request behavior when no calculated position is supplied, as explicitly settled by the planner.
- Keep rendering deterministic, side-effect-free, and independent of the Check Point client login/session.
- Do not register a positioning strategy or activate a workflow caller.

## Repository request path and activation boundary

`ExternalRequestHandler.ConstructContent` passes complete `WfReqTask` objects to `ExternalTicket.CreateRequestString`. The request GraphQL fragments already load `implementation_tasks`, and `implTaskDetails.graphql` includes `rule_position`. `WfImplTask.RulePosition` therefore contains either:

- a known `CheckPointRulePosition` after polymorphic deserialization;
- an `UnknownRulePosition` for a future discriminator; or
- `null` when no position was persisted.

`CheckPointTicket.BuildRequestTasks` currently obtains a template, calls `CheckPointTicketTask.FillTaskText`, and adds the parsed `TaskBody` to its internal execution plan. That parsed `JsonNode` is the existing access-rule request representation and is the renderer's input.

WP-04 did not change `BuildRequestTasks`, inspect `WfReqTask.ImplementationTasks`, or automatically invoke the renderer. D-033 later established that Phase 3 ends at persisted semantic output, and D-036 now requires WP-05 to remove this seam. Any future placer must define its own conversion contract.

This inactive boundary is deliberate: WP-04 supplies and tests the complete conversion primitive without making existing workflow traffic start consuming persisted positions.

## Authoritative Check Point request shape

The request contract is the Check Point Management API `add-access-rule` body. The authoritative command reference is the [Check Point Management API Reference - add-access-rule](https://sc1.checkpoint.com/documents/latest/APIs/index.html#web/add-access-rule~v1.9%20), verified during planning on 2026-10-19.

Every non-null semantic position writes both top-level fields below:

| Semantic anchor | `layer` | `position` |
| --- | --- | --- |
| `Top` | exact `LayerUid` string | JSON string `"top"` |
| `Bottom` | exact `LayerUid` string | JSON string `"bottom"` |
| `Above` | exact `LayerUid` string | JSON object `{ "above": "<AnchorRuleUid>" }` |
| `Below` | exact `LayerUid` string | JSON object `{ "below": "<AnchorRuleUid>" }` |

Use the UID values exactly as persisted; do not resolve or substitute names or numeric rule positions.

The same `layer` field selects an ordered access layer or an inline access layer by UID. Do not emit `inline-layer`: in `add-access-rule`, that separate field assigns an inline layer to a newly created parent rule and does not select the layer into which the new rule is inserted.

Top and bottom are explicit. Do not omit `position` and rely on a Check Point default because omission would erase the semantic distinction and could vary with vendor behavior or template content.

## Renderer contract

Add `CheckPointRulePositionRenderer.cs` under `roles/lib/files/FWO.ExternalSystems/CheckPoint/` in namespace `FWO.ExternalSystems.CheckPoint`.

Define one `internal static` class with this entry point:

```csharp
internal static JsonNode Render(
    JsonNode requestBody,
    RulePosition? rulePosition,
    ProvisioningPositioningAlgorithm expectedAlgorithm)
```

The method is synchronous and has no constructor or dependencies. It performs no I/O, login, client creation, logging, persistence, mutation of workflow data, or mutation of the supplied position.

### Exact rendering sequence

1. Reject a null `requestBody` with `ArgumentNullException`.
2. If `rulePosition` is null, return `requestBody.DeepClone()` immediately. Do not validate `expectedAlgorithm`, require an object body, add fields, remove fields, or normalize existing template content.
3. Require `expectedAlgorithm` to be a defined non-`Undefined` enum value; otherwise throw the fixed `ConfigException` below.
4. Require the position to be exactly a `CheckPointRulePosition`; `UnknownRulePosition` and every other vendor subtype fail with the fixed unsupported-position exception.
5. Defensively validate the Check Point position again even though its constructor and deserializers already validate it. Require:
   - `PositionType == CheckPointRulePosition.kDiscriminator`;
   - `SchemaVersion == 1`;
   - non-blank `LayerUid`;
   - a defined anchor kind;
   - `Above` and `Below` to have one non-blank `AnchorRuleUid`; and
   - `Top` and `Bottom` to have no rule UID.
6. Require the position's algorithm to be a defined non-`Undefined` value and exactly equal to `expectedAlgorithm`. Do not infer capability from enum names and do not maintain an algorithm allowlist in the renderer.
7. Require `requestBody` to be a `JsonObject` when a non-null position is supplied.
8. Deep-clone that object, replace its top-level `layer` and `position` properties with the mapping above, and return the clone.

Assigning the two properties replaces any template-provided values. It never adds duplicate JSON properties. Rendering the returned object again with the same position must produce structurally equal JSON. All unrelated template properties, arrays, and nested objects remain unchanged.

Neither the input body nor `CheckPointRulePosition` may be mutated. In particular, do not trim, rewrite, deep-copy back into, or otherwise normalize persisted UID or algorithm values.

### Failure conventions

Use the existing `FWO.Basics.Exceptions.ConfigException` convention used by `CheckPointTicketTask` for request/template rendering failures. Use exactly these safe messages and do not include UIDs, algorithm values, raw JSON, credentials, or exception details:

| Condition | Exception message |
| --- | --- |
| Undefined or unknown expected algorithm | `The configured rule-positioning algorithm is invalid.` |
| `UnknownRulePosition` or another vendor subtype | `The rule position is not supported for Check Point rendering.` |
| Invalid discriminator, schema version, layer UID, anchor kind, rule-UID combination, or position algorithm | `The Check Point rule position is invalid.` |
| Position algorithm differs from `expectedAlgorithm` | `The rule position does not match the configured positioning algorithm.` |
| Non-object body with a non-null position | `The Check Point access-rule request body must be a JSON object.` |

Validation stops at the first failure in the sequence above. Do not wrap `ArgumentNullException` or `ConfigException` in another exception.

## Null and compatibility behavior

A null position is a supported backward-compatibility state, not an error. It returns a deep clone whose JSON is structurally identical to the input, including any existing template-supplied `layer` or `position` field. This preserves all Check Point request templates and task bodies that predate rule positioning.

The implemented renderer does not read `WfReqTask`, choose among `ImplementationTasks`, or derive effective configuration. This historical behavior remains documented for removal review; D-036 prohibits retaining it as the future placer contract.

## External-call boundary

Rendering ends at the cloned `JsonNode`. It must not reference `CheckPointClient`, `RestRequest`, `CheckPointTicket.CreateExternalTicket`, an endpoint name, or a session identifier.

The existing `CheckPointTicket.ExecuteAllSteps` rule-change gate remains unchanged and continues to return `BadRequest` before any `CheckPointClient.RestCall`. WP-04 does not add the `add-access-rule` endpoint mapping or execute, publish, discard, log in, or log out.

## Exact change set

Production:

- add `roles/lib/files/FWO.ExternalSystems/CheckPoint/CheckPointRulePositionRenderer.cs`.

Tests:

- add `roles/tests-unit/files/FWO.Test/CheckPointRulePositionRendererTest.cs`;
- update `roles/tests-unit/files/FWO.Test/CheckPointTicketTest.cs` only to add the explicit request-construction/no-client-call regression described below.

Documentation updated after implementation:

- this file, with implementation status and validation evidence;
- `documentation/work-packages/firewall-rule-placement/phase-3/README.md`; and
- `documentation/work-packages/firewall-rule-placement/SESSION-HANDOFF.md`.

Do not change `CheckPointRulePosition`, `RulePosition`, JSON converters, `WfImplTask`, GraphQL fragments, `CheckPointTicketTask`, `CheckPointTicket`, `CheckPointTaskTypes`, `CheckPointClient`, `SimulatedCheckPointClient`, external-request middleware, strategy/catalog code, dependency injection, schema, Hasura metadata, importer, UI, installer, provisioning enums, customer documentation, or product versioning.

## Required tests

Add `CheckPointRulePositionRendererTest.cs` with these focused cases:

1. Parameterized mapping for all four anchors asserts the exact top-level JSON shape shown above.
2. Ordered-layer and inline-layer UIDs both render only through `layer`; neither output contains `inline-layer`.
3. Above/below preserve the exact anchor rule UID inside the one-property position object; top/bottom are scalar strings.
4. A template-created `CheckPointTicketTask.TaskBody` retains every unrelated property and nested value while pre-existing `layer` and `position` values are replaced.
5. Null position returns structurally identical JSON for both object and non-object bodies, does not validate an `Undefined` expected algorithm, and returns a distinct node rather than the caller's reference.
6. `UnknownRulePosition` and a test-only non-Check-Point `RulePosition` subtype throw the exact unsupported-position `ConfigException`.
7. `Undefined` and unknown expected enum values throw the exact invalid-configuration `ConfigException`.
8. A valid Check Point position whose algorithm differs from the expected algorithm throws the exact mismatch `ConfigException`.
9. Defensively malformed Check Point instances cover wrong schema version, blank layer UID, unknown anchor kind, missing required rule UID, prohibited rule UID, `Undefined`/unknown position algorithm, and wrong discriminator where reflection is needed to bypass the already-settled constructor/deserialization invariants. Every case throws only the exact invalid-position `ConfigException`.
10. A non-object request body with a non-null valid position throws the exact body-shape `ConfigException`.
11. Rendering twice with the same position produces structurally equal JSON with exactly one `layer` and one `position` property.
12. Mutating the returned body's unrelated nested data does not mutate the original request body. Rendering does not alter any common or Check Point position property.
13. Safe diagnostics contain none of sentinel layer/rule UIDs, algorithm values, raw body content, or credentials.

Add or retain a `CheckPointTicketTest` request-construction regression using `SimulatedCheckPointClient`: after `CreateRequestString`, assert `CalledEndpoints` and `RequestBodies` are empty. Do not call `CreateExternalTicket`. The renderer fixture captures and asserts the rendered `JsonNode` directly; it must not construct a client. Together these tests establish that request creation/rendering needs no session or outbound call while preserving the existing execution boundary.

Retain the existing `CreateExternalTicketReturnsBadRequestForRuleChangeTask` assertion unchanged: it proves rule execution still stops before the simulated client's `RestCall`.

## Validation

Run focused tests:

```powershell
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CheckPointRulePositionRendererTest|FullyQualifiedName~CheckPointTicketTaskTest|FullyQualifiedName~CheckPointTicketTest|FullyQualifiedName~CheckPointPositioningContractsTest|FullyQualifiedName~RulePositionPersistenceTest"
```

Then run:

```powershell
dotnet format roles/FWO.sln
dotnet build --configuration Debug roles/FWO.sln
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj
dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj --filter "FullyQualifiedName~CheckPointRulePositionRendererTest" --collect:"XPlat Code Coverage" --results-directory TestResults/WP04
```

Confirm more than 80% line coverage for `CheckPointRulePositionRenderer`. If repository-wide formatting changes unrelated files, restore only formatter-created unrelated differences while preserving pre-existing user changes.

No disposable-VM installer or browser validation is required because WP-04 changes no schema, Hasura metadata, installation behavior, configuration surface, middleware route, UI, or versioned upgrade path.

## Implementation evidence

Implemented on 2026-10-19 with the renderer kept as an unregistered, dependency-free internal seam. The production change adds only `CheckPointRulePositionRenderer`; it deep-clones legacy bodies, validates non-null positions in the prescribed order, and replaces only top-level `layer` and `position` fields.

Validation completed successfully:

- focused compatibility group: 86 passed, 0 failed, 0 skipped;
- `dotnet format roles/FWO.sln`: completed successfully; two unrelated formatter-only changes were removed afterward;
- Debug solution build: succeeded with 0 warnings and 0 errors;
- full unit suite: 7,917 passed, 29 skipped, 0 failed;
- isolated renderer suite with coverage: 20 passed, 0 failed;
- `CheckPointRulePositionRenderer`: 97.95% line coverage and 96.77% branch coverage.

No installer or browser validation was run, as allowed by this work package's unchanged deployment and UI boundary.

## Acceptance criteria

- Every valid semantic position renders to the settled Check Point representation.
- Invalid positions fail safely before an external call.
- Existing unrelated Check Point request behavior remains unchanged.
- No workflow or strategy becomes active.
- At WP-04 completion the renderer was callable only through an inactive in-process seam; D-036 supersedes this retained state and requires WP-05 removal.

## Out of scope

- Customer-document analysis or recipes.
- Snapshot or zone loading.
- Strategy registration and workflow activation.
- Executing, publishing, or installing a Check Point policy.
- Selecting a gateway implementation task, calculating/persisting a position, or connecting the renderer to `CheckPointTicket.BuildRequestTasks`.
- Adding `add-access-rule` endpoint execution or removing the existing rule-change execution gate.
