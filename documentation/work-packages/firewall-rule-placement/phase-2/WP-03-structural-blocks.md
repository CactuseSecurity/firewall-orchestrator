# WP-03 — Structural blocks and composed acceptance scenarios

## Outcome

Provide side-effect-free blocks for selecting structural Check Point scopes and resolving their beginning or end to a semantic position.

## Blocks

- Layer scope: initial layer, exact ordinal name, or UID.
- Section scope: exact ordinal name or UID within its selected parent layer.
- `BeginningOf(scope)`.
- `EndOf(scope)`.

A selector uses exactly one mode. Missing matches return `ScopeNotFound`; duplicate name matches return `ScopeAmbiguous`. A section never falls back to a parent or another layer. Snapshot/calculation identity mismatch returns `InvalidSnapshot`.

## Boundary rules

- Beginning of a populated scope is above its first rule.
- End of a populated scope is below its last rule.
- An empty layer resolves to `Top` for beginning and `Bottom` for end.
- An empty section has one canonical gap: above the next layer rule, otherwise below the previous layer rule, otherwise top.
- Direct rules and section rules retain their true layer interleaving.
- A scope not belonging to the supplied snapshot returns `NoInsertionPoint`.

## Acceptance scenarios

- `EndOf(InitialLayer())`
- `BeginningOf(Section(InitialLayer(), name-or-uid))`

No production strategy, dependency-injection registration, policy loader, workflow call, or Check Point API renderer is part of this package.
