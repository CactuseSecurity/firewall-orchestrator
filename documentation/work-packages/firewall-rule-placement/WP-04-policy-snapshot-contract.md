# WP-04 — Define the ordered policy snapshot contract

## Agent mission

Define the minimal ordered policy representation that a Check Point live reader feeds into the pure planner. Keep the contract extensible without modeling unfinished vendors.

## Scope

- Add an `IPolicySnapshotReader` implementation contract keyed by a resolved functional target.
- Define an immutable ordered snapshot containing policy containers, native Sections, ordinary rules, and a revision/fingerprint.
- Represent empty native Sections and Section boundaries explicitly.
- Preserve layer/package identity so the planner can prove Section membership in the functional target.
- Preserve only rule UID, name, order, Section membership, and identifiers needed to calculate and evidence the target boundary.
- Add stable ordering and fingerprint helpers.
- Allow future readers to implement the same ordered-policy contract where it fits; vendor-specific planning data belongs behind their own snapshot subtype or adapter and must not pollute the Check Point model pre-emptively.

## Tests

Build in-memory fixtures for a populated Section, empty Section, two same-named Sections in different layers, adjacent Sections, a rule outside all Sections, and stable ordering when numeric values collide.

## Acceptance criteria

- WP-07 can plan using only this contract and WP-01 types.
- Empty Sections cannot disappear during translation.
- A fingerprint changes when order, Section membership, target identity, or anchor evidence changes.
- GraphQL and persistence DTOs do not leak through the interface.
- No `global-label`, Rule Collection, or other future-vendor field is added to current persistence.

## Out of scope

Do not query FWO or Check Point, resolve a functional target, calculate a position, or consume a plan.
