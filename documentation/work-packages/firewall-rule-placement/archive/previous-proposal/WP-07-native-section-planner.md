# WP-07 — Plan `position.bottom` in a native Section

## Agent mission

Implement the pure Check Point native-Section strategy over a WP-04 snapshot. Return a plan or typed failure with no side effects.

## Algorithm

1. Receive a validated functional target and exact zone pair.
2. Build the canonical Section name with WP-01.
3. Search native Sections inside the functional target using the shared comparison normalizer.
4. If none exists, return `SectionMissing` with the canonical name.
5. If a normalized match exists only outside the target, return `SectionOutsideFunctionalTarget`.
6. If multiple normalized matches exist inside the target, return `SectionAmbiguous`.
7. Return a `BottomOfSection` plan referencing the matched Section and functional target.
8. Record the current last rule UID for a populated Section, or the current empty state, as evidence of the calculated boundary.

## Rules

- The eventual API representation is Check Point `position.bottom <section-name>`.
- Do not convert the target into a numeric offset.
- Preserve the canonical requested name separately from the matched vendor spelling.
- A missing Section never triggers creation or default placement.

## Tests

Cover specification cases 1, 2, 4, and 5 plus an empty Section, trim/case normalization, duplicate normalized names, a same-named Section in another layer, and a rulebase with no Sections.

## Acceptance criteria

- The same request and snapshot always produce the same result.
- A successful plan references only a Section inside the functional target.
- Missing or ambiguous Sections produce no executable plan.
- Empty Sections are valid and contain no fake neighbor.
- Tests use in-memory snapshots and make no API calls.

## Out of scope

Do not read Check Point, consume the plan, or add generic `SECTION_LABEL` behavior.
