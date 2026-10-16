# Phase 3 WP-03 implementation - Check Point strategy orchestration

## Status

Draft - planning required after WP-01 and WP-02 are complete.

## Goal

Implement the Check Point strategy orchestration and its recipe-selection seam, while leaving production registration and all customer mappings for WP-05.

## Fixed requirements

- Implement `IRulePositioningStrategy` without changing the vendor-neutral manager into a vendor switch.
- Consume the production snapshot reader and creation-rule zone resolver through their settled contracts.
- Select a complete recipe through an explicit catalog/selector seam.
- Execute that recipe through the existing immutable Phase 2 executor and `CheckPointPositioningContext`.
- Load at most one snapshot and execute at most one recipe per calculation.
- Preserve exact configured algorithm identity in successful positions.
- Propagate caller cancellation and convert other failures according to the settled safe mapping.
- Add no customer recipe or enum mapping.
- Do not register the strategy in production and do not connect a workflow caller.

## Settled implementation design

The WP-03 planner must replace this paragraph with exact constructor dependencies, capability behavior, orchestration order, recipe-selector contract, failure translations, and service lifetime.

## Exact change set

To be supplied by the WP-03 planner.

## Required tests

The completed brief must specify tests proving:

- exact Check Point device/algorithm capability behavior;
- a test-only recipe can complete through the entire strategy;
- no matching recipe fails safely without fallback;
- loader failure prevents zone resolution and recipe execution;
- zone-resolution failure prevents recipe selection/execution;
- recipe-selection and block failures map exactly as planned;
- one snapshot load and one execution occur;
- unexpected exceptions yield safe diagnostics;
- pre-cancellation and in-flight cancellation propagate; and
- the strategy remains absent from production dependency injection.

## Validation

The planner must add focused commands. Implementation also requires C# formatting, the Debug solution build, the complete unit suite, and more than 80% line coverage for new production code.

## Acceptance criteria

- Test-only recipes prove the complete orchestration path.
- No production customer mapping or registration exists.
- Calculation remains side-effect-free with respect to persistence and external systems.
- Manager behavior for zero production matches remains safe until WP-05.

## Out of scope

- Customer-document analysis and customer recipes.
- Production dependency-injection registration.
- Workflow activation or position persistence.
- Check Point request rendering or API calls.
