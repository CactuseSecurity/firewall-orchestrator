# Phase 1 — Generic rule-positioning foundation

## Objective

Build a calculation-only, cross-vendor positioning foundation for one gateway-specific implementation task at a time. The caller supplies all required context. The manager validates it, selects exactly one registered vendor/algorithm strategy, and returns either a typed `RulePosition` success or a typed failure.

Calculation and persistence are deliberately separate.

## Work packages and order

1. [WP-01 — Contracts and serialization](WP-01-contracts-and-serialization.md)
2. [WP-02 — Positioning manager](WP-02-positioning-manager.md)
3. [WP-03 — Position persistence](WP-03-position-persistence.md)

WP-01 establishes the contracts consumed by both later packages. WP-02 and WP-03 can be implemented in parallel after those contracts stabilize, then verified together.

## Phase deliverable

At completion:

- calculation can be exercised with test-only Check Point-like and Fortigate-like strategies;
- persistence can independently store, load, replace, and clear a position;
- unknown future position types remain loadable by older clients;
- invalid or unsupported contexts cannot reach a strategy;
- no real vendor strategy is registered in production; and
- production calculation therefore returns a safe `PositioningMethodNotImplemented` failure.

## Shared implementation rules

- Reuse the current hierarchical provisioning settings and `ProvisioningPositioningAlgorithm` unchanged.
- Do not add `management_id` to the implementation task.
- Do not include credentials or complete rule contents in diagnostics, logs, exception messages surfaced to callers, or test fixtures intended to model safe failures.
- Do not write failures to `rule_position`.
- Do not let calculation invoke persistence or workflow actions.
- Do not make persistence invoke calculation or workflow actions.
- Do not add a fallback vendor or algorithm.
- Do not add production Check Point or Fortigate strategies.
- Preserve a copied position without sharing mutable payload state.

## Shared failure categories

| Category | Intended use |
| --- | --- |
| `InvalidInput` | Missing, malformed, or contradictory context and task/reference relationships. |
| `UnsupportedTask` | Task/action is not a new access rule. |
| `ConfigurationInvalid` | Effective settings are missing or the positioning algorithm is undefined. |
| `PositioningMethodNotImplemented` | No registered strategy matches the exact device type and algorithm. |
| `StrategyRegistrationAmbiguous` | More than one registered strategy matches. |
| `CalculationFailed` | A selected strategy fails unexpectedly. |

## Validation gate

Implementation is complete only after the repository-required checks and these feature checks pass:

- formatter/linter checks for every changed language;
- `dotnet build --configuration Debug roles/FWO.sln`;
- relevant unit tests, including all new test cases in the work packages;
- coverage measurement showing more than 80% coverage for new code;
- fresh-install validation; and
- upgrade validation from the immediately previous version through the disposable installer-test workflow.

Because WP-03 changes schema, GraphQL/Hasura, upgrade SQL, and product versioning, implementation must use the repository's schema-change and installer skills. Significant installer-affecting changes must use the disposable installation-test skill rather than a manually run installation.

## Out of scope

- customer-document analysis;
- customer-specific placement behavior;
- vendor communication or API conversion;
- automatic workflow activation;
- plan presentation or UI;
- auditing; and
- firewall execution.
