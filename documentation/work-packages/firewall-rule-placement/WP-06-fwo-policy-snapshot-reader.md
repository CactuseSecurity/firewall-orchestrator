# WP-06 — Read imported FWO Sections and rule order

## Agent mission

Implement the FWO-data adapter for WP-04. It reads one complete, acceptable imported policy snapshot for the WP-05 functional target and never connects to Check Point.

## Existing seam

The normalized-config and rulebase GraphQL queries already expose active rulebases, ordered rules, gateways, and `RulebaseLink` topology. `import_control` records the ID and completion time of successful imports. The current latest-normalized-config result does not expose snapshot freshness or a fingerprint and must not be used as an unversioned floating read.

## Scope

- Select the latest completed successful import for the target management and enforce the freshness limit decided in WP-00.
- Anchor all policy data to that import control ID so one invocation cannot combine states from different imports.
- Read only the resolved package/layer from WP-05, including its native Sections and ordinary rules in stable order.
- Preserve original Section names, empty Sections, rule UIDs, and Section membership.
- Produce a WP-04 snapshot with the import control ID, import completion timestamp, and deterministic fingerprint.
- Treat a missing, stale, in-progress, incomplete, inconsistent, or ambiguous imported snapshot as `PolicyReadFailed`.
- Use existing `ApiConnection`/GraphQL read seams and safe logging conventions.

## Tests

Use an injected FWO query adapter to cover a populated Section, an empty Section, stable rule ordering, mixed-case/whitespace names, same-named Sections in another layer, absent Sections, missing topology links, stale imports, an import changing during a floating read attempt, and query failures. Verify that every query is anchored to one completed import ID and that no Check Point client is constructed or called.

## Acceptance criteria

- The planner can locate an exact native Section without vendor JSON or Check Point access.
- Empty Sections remain visible.
- Reads cannot drift to a same-named layer in another package or combine multiple imports.
- The returned timestamp lets callers determine snapshot age, and the fingerprint identifies the exact ordered content used.
- A missing, stale, or incomplete import cannot become a successful snapshot.
- The package has no dependency on `FWO.ExternalSystems.CheckPoint`.

## Out of scope

Do not change the importer, trigger an import, select the functional target, calculate the position, change external or workflow state, or connect to Check Point.
