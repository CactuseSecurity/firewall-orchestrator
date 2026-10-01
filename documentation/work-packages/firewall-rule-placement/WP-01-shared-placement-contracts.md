# WP-01 — Add vendor-neutral placement-plan contracts

## Agent mission

Create the immutable input, plan, and failure types used by the Check Point planning pipeline. Define only the extension interfaces needed to calculate a position; execution and presentation contracts are outside this workload.

## Scope

- Define positioning-method/capability identifiers without coupling the coordinator to vendor names.
- Add immutable placement request, functional target, snapshot identity, plan, anchor evidence, and failure models.
- Add the stable failure reasons listed in the overview.
- Add interfaces for functional-target resolution, policy snapshot reading, and pure placement planning.
- Implement one canonical Section-name formatter and comparison normalizer for Check Point.
- Add an exhaustive component registry that can report an unavailable future method without instantiating it.

The canonical Check Point Section name is:

```text
[DESTINATION-ZONE] < [SOURCE-ZONE]
```

Trim zone names, uppercase invariantly, retain brackets, and use one space around `<`. Source and destination may be equal. Reject null, blank, or multiple-zone inputs before formatting.

## Plan contents

The result must describe:

- management and gateway identity;
- positioning method;
- package/layer/inline-layer identity;
- canonical requested and actual matched Section names;
- relative operation `BottomOfSection`;
- current last-rule UID or explicit empty-Section evidence;
- snapshot timestamp and fingerprint;
- safe correlation/diagnostic metadata.

The plan must contain no Check Point session, credentials, REST request, execution state, or UI formatting.

## Suggested location

Use `FWO.Services.RulePlacement` for behavior. Put DTOs in `FWO.Data` only when they cross an existing application boundary.

## Tests

Cover canonical formatting, whitespace, mixed case, invariant casing, intra-zone flows, blank zones, failure serialization where needed, exhaustive registry lookup, and an unregistered method returning `PositioningMethodNotImplemented`.

## Acceptance criteria

- WP-03 through WP-08 can depend on these contracts without importing Check Point response DTOs.
- A failure cannot be mistaken for a plan with null fields.
- The plan contains enough evidence for a future consumer to detect staleness, without claiming execution or verification.
- No executor, workflow, UI, Fortinet/Azure client, schema field, or algorithm is introduced.
- A future vendor can implement the planning interfaces in `FUTURE-VENDOR-EXTENSIONS.md` without changing the common result shape unnecessarily.

## Out of scope

Do not resolve zones, read configuration or policies, calculate a Section, execute a plan, or define presentation models.

