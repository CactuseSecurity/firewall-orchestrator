# WP-02 — Cross-vendor positioning manager

## Goal

Provide a calculation-only manager that validates caller-supplied context and invokes exactly one registered strategy for the exact device type and configured positioning algorithm.

## Public API

Expose:

```csharp
Task<RulePositioningResult> CalculateAsync(
    RulePositioningContext context,
    CancellationToken cancellationToken);
```

Define `IRulePositioningStrategy` with:

- a side-effect-free capability check for device type and `ProvisioningPositioningAlgorithm`; and
- an asynchronous calculation method accepting the shared context and cancellation token and returning `RulePositioningResult`.

Construct `RulePositioningManager` from registered strategies. Do not hard-code Check Point, Fortigate, or any other vendor switch in the manager.

## Validation before strategy selection

Validate all of the following before invoking a strategy:

1. Context and required references are present.
2. Both the implementation task and parent request task describe a new access rule.
3. The implementation task belongs to/corresponds to the supplied parent request task using the repository's canonical task relationship fields.
4. Exactly one target gateway is identified.
5. The supplied gateway agrees with implementation-task and request-task references.
6. The supplied management and gateway agree with the effective implementation-task management (`implTask.ManagementId ?? requestTask.ManagementId`); any loaded request-management object agrees with the request task's own management ID.
7. Effective `GatewayProvisioningSettings` are present.
8. The effective positioning algorithm is a defined, usable enum value.

Return failures using this mapping:

| Condition | Failure category |
| --- | --- |
| Missing/malformed context, ID mismatch, zero/multiple gateways, or contradictory gateway/management/task references | `InvalidInput` |
| Either task is a modification, deletion, object, or unknown task/action rather than a new access rule | `UnsupportedTask` |
| Effective settings missing or positioning algorithm undefined/invalid | `ConfigurationInvalid` |

No strategy capability check or calculation may run when validation fails. Validation order must be deterministic enough that tests can assert the intended category without depending on incidental null-reference behavior.

## Exact strategy selection

Evaluate the registered strategies against the validated device type and exact configured algorithm:

- no match returns `PositioningMethodNotImplemented`;
- exactly one match invokes that strategy once; and
- more than one match returns `StrategyRegistrationAmbiguous` without invoking any calculation.

Never retry with a different algorithm, fall back to a different vendor, pick the first duplicate, or infer an algorithm from the device type.

Phase 1 registers no production strategy. The empty production registration set is a valid state and must lead to a safe not-implemented result.

## Cancellation and exception behavior

- Honor a pre-cancelled token before strategy calculation.
- Pass the original token to the selected strategy.
- Propagate cancellation associated with the supplied token.
- Convert other unexpected strategy exceptions to a safe `CalculationFailed` result.
- Do not expose exception details that contain credentials or complete rule contents.

If a strategy deliberately returns a typed failure, preserve that typed result; do not replace it or try another strategy.

## Tests

Use test-only strategies and derived positions to prove:

- a Check Point-like device/algorithm selects only the matching Check Point-like strategy;
- a Fortigate-like device/algorithm selects only the matching Fortigate-like strategy;
- both test strategies coexist without manager changes;
- every context mismatch listed above returns before capability or calculation is invoked;
- modification, deletion, object, and unknown task/action cases return `UnsupportedTask` without strategy invocation;
- missing/undefined effective configuration returns `ConfigurationInvalid`;
- no registration returns `PositioningMethodNotImplemented`;
- duplicate matches return `StrategyRegistrationAmbiguous` and invoke neither calculation;
- a nonmatching vendor or algorithm never receives fallback;
- a returned strategy failure is preserved without fallback;
- unexpected exceptions become safe `CalculationFailed` results;
- pre-cancellation and in-strategy cancellation propagate; and
- the strategy receives the same validated context and cancellation token.

The two vendor-like test strategies must return different test-only `RulePosition` derived types. They do not become production registrations.

## Acceptance criteria

- The manager has no vendor-specific branch or production strategy registration.
- Every invalid/unsupported context is rejected before strategy work.
- Exactly one exact match is required.
- Cancellation is observable as cancellation, not `CalculationFailed`.
- Unexpected failures are typed and safe.
- Production behavior with no real strategies is a safe `PositioningMethodNotImplemented` result.

## Dependencies and impact

Depends on WP-01. Expected code belongs in the shared/workflow service layer selected during repository validation. Dependency-injection registration may expose an empty strategy collection, but must not register a real strategy.

## Out of scope

- loading the context;
- persistence or clearing;
- customer algorithm logic;
- vendor communication;
- workflow activation; and
- auditing.
