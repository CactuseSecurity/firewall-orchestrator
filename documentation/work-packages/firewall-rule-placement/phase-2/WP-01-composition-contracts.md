# WP-01 — Generic composition contracts and executor

## Outcome

Provide `IPositioningBlock<TContext,TOutput>`, an immutable validated root recipe, typed success/failure results, and a stateless asynchronous executor in `FWO.Services`.

## Contract

- Every block exposes a stable kind, an output role, its children, and the role required from each child.
- Recipe construction snapshots the tree and rejects missing children, incompatible roles, inconsistent declarations, blank kinds, and cycles.
- The executor evaluates children in declaration order, stops at the first failure, and returns a child failure unchanged.
- Caller cancellation propagates as cancellation. Unexpected block exceptions become `UnexpectedExecutionFailure` without exposing exception or evaluated data.
- Failure categories are `InvalidRecipe`, `InvalidSnapshot`, `ScopeNotFound`, `ScopeAmbiguous`, `NoInsertionPoint`, and `UnexpectedExecutionFailure`.
- Every failure retains the stable kind of the block where it originated.
- The executor retains no per-run state, allowing independent concurrent evaluations.

## Validation

Tests cover nested and multi-child recipes, success propagation, child short-circuiting, null/role/cycle rejection, immutable plan snapshots, cancellation, safe unexpected failures, null recipes, and concurrent evaluations.
