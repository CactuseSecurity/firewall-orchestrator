# WP-08 — Compose the side-effect-free placement pipeline

## Agent mission

Create one application service that returns a complete Check Point placement plan or typed failure for one implementation task and gateway. Keep component selection extensible so future vendor teams can register their own planning pipeline.

## Pipeline

1. Validate that the request describes one new access rule.
2. Load the effective positioning configuration through WP-02.
3. Resolve exactly one source/destination zone pair through WP-03.
4. Resolve the Check Point functional target through WP-05.
5. Select the complete registered reader/planner capability from the WP-01 contracts.
6. Read one acceptable completed FWO import snapshot through WP-06.
7. Calculate the native-Section position through WP-07.
8. Recheck that the matched Section belongs to the functional target.
9. Return the descriptive plan with its calculation evidence, or return one typed failure.

## Scope

- Define one public coordinator entry point for placement calculation.
- Centralize positioning-method registration and selection; do not scatter manufacturer switches.
- Carry the ticket, request-task, implementation-task, management, and gateway correlation identifiers needed for diagnostics.
- Support cancellation and avoid duplicate policy reads within one invocation.
- Log the completed stage and safe failure reason without credentials or full rule contents.
- Return `PositioningMethodNotImplemented` when a configured method has no complete planning registration.

## Behavior

Stop at the first failed prerequisite. Never convert an error into policy-end placement or another fallback. Repeated calculation over the same request, configuration, and snapshot must return the same result. The returned snapshot fingerprint and Section-boundary evidence describe the state used for the calculation.

## Tests

Use fakes for each dependency. Cover every failure through `SectionAmbiguous`, successful composition, functional precedence, cancellation, an unregistered method, partial registration, a single snapshot read, and proof that later dependencies are not called after an earlier failure.

## Acceptance criteria

- One call returns either a complete descriptive placement plan or one typed failure.
- The coordinator has no state-changing external dependency.
- Component lookup is exhaustive and centrally tested.
- The Check Point path uses WP-02 through WP-07 without duplicating their logic.
- A future vendor can add a complete planning capability through registration without changing Check Point branches.

## Out of scope

Do not persist or consume plans, mutate workflow state, change firewall policy, implement UI, or implement future vendor components.
