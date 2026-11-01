# Rule Positioning Acceptance Runbook Design

## Intent

Create an agent-first acceptance-testing runbook for the completed Firewall Orchestrator rule-positioning feature. The runbook exists to reduce the chance of an obvious failure during the Monday demonstration by driving repeatable weekend testing on an installed VM connected read-only to company Check Point test systems.

## Goals

- Exercise the production workflow trigger, calculation, and semantic-position persistence path.
- Cover both `DefaultEndOfRulebase` and `CheckPointLayerGuardEndOfZonePairSection`.
- Prefer real imported Check Point data when it contains the required structures.
- Provide deterministic fallback data whose normalized database state is produced by the real importer contract rather than tailored to the positioning readers.
- Give agents exact setup, action, assertion, evidence, cleanup, and triage instructions.
- End with low-risk demonstrations for one success, one missing zone-pair Section, and one unmatched layer guard.

## Non-goals

- Installation or upgrade testing.
- Proving importer correctness.
- Modifying the external Check Point test system.
- Rendering or writing rules to Check Point, publishing, or policy installation.
- Exhaustively covering every unit-level edge case already represented by automated tests.

## Selected Approach

Use one agent-first Markdown runbook. It combines browser workflows, GraphQL and PostgreSQL observations, middleware logs, deterministic fixtures, evidence recording, and a short human demonstration chapter.

Alternatives considered were a human-first checklist and separate specification/execution-log documents. The combined agent-first runbook was selected because it gives weekend agents one authoritative sequence while remaining teachable to a human presenter.

## Test Architecture

The runbook uses four layers:

1. Baseline source/build/unit verification.
2. Environment and live imported-data discovery.
3. End-to-end standard and customer-algorithm acceptance tests.
4. Importer-faithful fixture scenarios for structures absent from live data.

The observable terminal output is `request.impltask.rule_position`. A valid test must exercise production readers, resolvers, strategies, workflow activation, and persistence. Firewall request rendering and execution are outside the implemented feature boundary.

## Importer-faithful Fixture Boundary

Fixture generation occurs before the positioning test boundary. Preferred fixtures are produced by running sanitized Check Point API-shaped source data through the real importer once, then exporting the normalized rows and historical relationships it actually created. Replaying that exported normalized state into the isolated VM does not make importer behavior an acceptance assertion.

The fixture must preserve actual importer structures and quirks, including import-control history, rulebase and rulebase-link topology, rule ordering, section links, inline guard source rules, source/destination associations, negation, normalized objects, flattened group membership, and created/removed intervals.

Acceptance tests must not mock `RulebaseGraphSnapshot`, `CheckPointLayerGuardSnapshot`, resolved zone input, recipe input/result, or the final `rule_position`. They must not directly invoke a recipe factory as a substitute for workflow execution.

Each fixture records provenance, importer revision, source-data shape, normalized row counts, identifiers, validation-query output, and a checksum. Hand-authored idealized high-level snapshots are prohibited.

## Coverage Shape

Cover representative success, failure, isolation, and lifecycle behavior without reproducing every unit-test permutation. Include settings inheritance, task creation, persistence, clearing of stale values after completed failures, capability separation, replay/non-recalculation behavior, multiple gateways, same-ticket groups, fixed-import consistency, timeout/log safety, and cleanup.

Every test has a stable ID, priority, prerequisites, data source, exact action, expected semantic JSON, evidence list, cleanup, and failure-triage route.

## Demonstration Gate

No scenario becomes a Monday demonstration candidate until it passes twice from a known starting state and has a documented reset procedure. The minimum demonstration set is:

1. successful customer placement at the end of the expected zone-pair Section;
2. safe failure when that zone-pair Section is absent; and
3. safe failure when no inline-layer guard contains the requested traffic.

The failure demonstrations must show that workflow creation remains successful, no fallback position is stored, no firewall write occurs, and logs remain safe.

