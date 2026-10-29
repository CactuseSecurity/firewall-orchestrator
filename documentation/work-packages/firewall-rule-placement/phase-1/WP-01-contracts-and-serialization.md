# WP-01 — Contracts and serialization

## Goal

Define the shared, vendor-neutral calculation contracts and make persisted rule positions forward-compatible across both JSON serializers used by FWO.

## Deliverables

### `RulePosition`

Add an abstract persisted model containing:

- a stable position-type discriminator;
- a schema version; and
- the `ProvisioningPositioningAlgorithm` identifier used to calculate it.

The discriminator is a durable storage contract and must not depend on a CLR type name, assembly name, or serializer-specific metadata. Schema versioning belongs to the position payload, not the implementation-task schema.

Support derived vendor-specific types without defining future vendor payload fields now. The production model may establish empty subtype shells if required by the chosen serializer design, but Phase 1 must not invent Check Point or Fortigate positioning data.

### `UnknownRulePosition`

Unknown discriminator values must deserialize as `UnknownRulePosition`, not fail the entire `WfImplTask`. It must retain enough raw JSON data to serialize the unknown position again without dropping unrecognized vendor data. Known common metadata remains accessible where safely possible.

Forward compatibility applies to both serializers currently used by FWO. Tests must cover each serializer independently rather than assuming shared converter behavior.

### `RulePositioningContext`

Define a context containing exactly the resolved inputs required by Phase 1:

- implementation task;
- parent request task;
- target gateway;
- target management; and
- effective `GatewayDeviceSpecificSettings`.

The context is caller-supplied and calculation-only. It does not load data or persist results.

### `RulePositioningResult`

Use explicit success and failure variants.

The success variant contains a non-null `RulePosition`. The failure variant contains one of these stable categories plus a safe diagnostic suitable for callers and logs:

- `InvalidInput`
- `UnsupportedTask`
- `ConfigurationInvalid`
- `PositioningMethodNotImplemented`
- `StrategyRegistrationAmbiguous`
- `CalculationFailed`

Do not put credentials, tokens, full serialized tasks, or complete rule contents into diagnostics. Exceptions may be retained internally according to existing logging conventions, but the returned failure must remain safe.

## Copy semantics

Update every relevant `WfImplTask` copy/clone path in WP-03 to retain a position while preventing shared mutable payload state. The contract chosen here must offer a reliable deep-copy mechanism for known and unknown subtypes. A JSON round trip is acceptable only if it preserves the required type and unknown fields through both supported serializers and fits existing project conventions.

## Tests

Add unit tests for:

- known derived-position round trips through serializer A;
- known derived-position round trips through serializer B;
- unknown discriminator input loading as `UnknownRulePosition` through each serializer;
- unknown content surviving a deserialize/serialize cycle;
- common discriminator, schema-version, and algorithm metadata;
- explicit success and every failure category;
- safe diagnostics that do not reproduce supplied credentials or complete rule content; and
- deep-copy behavior proving later mutation of the original position or raw unknown payload does not alter the copy.

Use minimal test-only derived positions. Their payloads should exist only to prove polymorphism and mutable-state isolation, not to define a production vendor contract.

## Acceptance criteria

- Both FWO serializers round-trip a known derived position.
- Both serializers preserve an unknown position as `UnknownRulePosition` without making the parent task unloadable.
- Unknown vendor JSON is not silently discarded on reserialization.
- Result callers can branch on a typed success/failure and stable failure category.
- No diagnostic test exposes secrets or full rule contents.
- The contract supports independent calculation and persistence.

## Dependencies and impact

This is the prerequisite for WP-02 and WP-03. The expected owning layer is the shared C# model/library used by workflow clients; exact files must be confirmed during the read-only repository validation recorded in `SESSION-HANDOFF.md`.

## Out of scope

- production strategy behavior;
- customer algorithm fields;
- database and GraphQL changes;
- context loading;
- persistence; and
- workflow triggering.
