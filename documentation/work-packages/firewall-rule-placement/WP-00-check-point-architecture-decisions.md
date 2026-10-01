# WP-00 — Record the Check Point planning decisions

## Agent mission

Produce one short ADR that fixes the inputs, read source, compatibility behavior, and output boundary of the Check Point positioning algorithm. Do not implement production behavior in this package.

## Decisions to record

1. **Policy source of truth.** Decide whether planning reads the live Check Point access rulebase or the latest imported FWO snapshot. The recommended choice is the live read-only API because the output claims a current Section boundary. If cached data is permitted, define an explicit freshness limit and expose its timestamp/fingerprint.

Answer: Use the latest completed successful FWO import as the policy source. WP-00 through WP-08 must not connect to Check Point. The imported data already preserves rulebase, Section, rule-order, and topology data, while `import_control.stop_time` provides the import timestamp. Extend the snapshot query/result to expose the import control ID and completion timestamp and calculate a deterministic snapshot fingerprint. Reject missing, incomplete, in-progress, or stale snapshots; the maximum acceptable age remains open in [`WP-00-remaining-decisions-pitch.md`](WP-00-remaining-decisions-pitch.md).

2. **Historical configuration values.** Decide whether an existing `ProvisioningPositioningAlgorithm` value maps exactly to native Section placement. Values with different semantics remain unsupported rather than being silently reinterpreted.

Answer: No existing value maps to this behavior. Add a new positioning value and preserve every historical value without reinterpretation.

3. **Functional target inputs.** State which configured/imported fields uniquely identify the policy package, access layer, and optional inline layer. Missing identifiers fail; names are never guessed.

Answer: Open. This directly blocks WP-05 and affects the contracts or behavior in WP-01, WP-02, WP-04, WP-06, WP-07, and WP-08. WP-03 is independent. See [`WP-00-remaining-decisions-pitch.md`](WP-00-remaining-decisions-pitch.md).

4. **Input ownership.** State which upstream component supplies the target gateway and whether WP-03 receives task object trees or an already resolved exact zone pair.

Answer: Open. A short problem statement and the questions for coworkers are in [`WP-00-remaining-decisions-pitch.md`](WP-00-remaining-decisions-pitch.md).

5. **Plan output.** Confirm the minimum descriptive result: target IDs, canonical/matched Section names, `BottomOfSection`, current last-rule UID or empty-Section evidence, snapshot fingerprint, and safe diagnostic metadata.

Answer: Accepted as the minimum contract. Define a reasonable extensible result in WP-01; minor consumer-driven additions may follow without weakening the required evidence.

6. **Read-only guarantee.** Define the endpoints/actions forbidden inside WP-00 through WP-08 and the test seam used to prove no write occurs.

Answer: WP-00 through WP-08 have no Check Point dependency and must make zero Check Point calls. They may perform read-only FWO queries only and must not update workflow state or persist an execution claim. Prove this by keeping Check Point clients out of the dependency graph, injecting the FWO snapshot reader, and testing that planning uses only read operations. Check Point write sessions and endpoints belong to a future execution delivery.

## Evidence to inspect

- `roles/lib/files/FWO.Data/Provisioning/`
- `roles/lib/files/FWO.Data/RulebaseLink.cs`
- `roles/lib/files/FWO.Services/PathAnalysis.cs`
- `roles/lib/files/FWO.Data/ImportStatus.cs`
- current normalized-config and rulebase GraphQL queries

## Deliverable

Add an ADR under the established architecture-document location. If none exists, use `documentation/developer-docs/architecture/ADR-check-point-rule-placement-planning.md` and link it from the overview.

The ADR must include context, decisions, rejected alternatives, compatibility impact, data freshness, and the boundary between configuration, zone resolver, functional-target resolver, policy reader, planner, and coordinator.

## Acceptance criteria

- Every decision above has one unambiguous answer.
- WP-01 through WP-08 need not invent product or data-source choices.
- Reviewers can determine how current the returned position is.
- The ADR explicitly states that execution, verification, workflow changes, UI, Fortinet, and Azure are outside this delivery.

## Out of scope

Do not decide write-session behavior, publish/install policy, UI columns, workflow outcomes, multi-firewall rollback, Fortinet labels, Azure collections, default placement, Section creation, or shadow analysis.
