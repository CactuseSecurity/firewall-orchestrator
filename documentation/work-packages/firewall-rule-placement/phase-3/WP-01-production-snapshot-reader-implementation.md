# Phase 3 WP-01 implementation - Production snapshot reader

## Status

Draft - planning required.

Do not implement this work package until the WP-01 planning session replaces every open item below and marks this brief `Ready for implementation`.

## Goal

Add the production reader that obtains the normalized structural policy data for one target management and gateway from the latest successfully completed FWO import and creates one immutable `RulebaseGraphSnapshot`.

## Fixed requirements

- Use the latest successfully completed import regardless of age.
- Scope all loaded rulebases, links, and structural rules to one consistent import, management, and gateway.
- Construct the existing `RulebaseGraphSnapshot`; do not introduce a parallel policy model.
- Preserve its eager validation and defensive-copy boundary.
- Fail explicitly when no successful import exists or the returned graph is incomplete or inconsistent.
- Propagate caller cancellation.
- Keep diagnostics free of credentials and complete rule contents.
- Do not add a freshness setting, stale-import rejection, automatic re-import, snapshot fingerprint, importer call, vendor API call, recipe selection, strategy registration, workflow activation, or UI.

## Settled implementation design

The WP-01 planner must replace this paragraph with the exact reader contract, result model, query behavior, construction ownership, dependency-injection ownership, and failure mapping.

## Exact change set

To be supplied by the WP-01 planner after repository discovery.

## Required tests

At minimum, the final plan must require tests for:

- selecting the latest successfully completed import;
- excluding failed, incomplete, in-progress, older, removed, and unrelated management/gateway data;
- building the expected graph from representative initial, ordered/domain, section, and inline links;
- no successful import;
- inconsistent import ownership or missing graph endpoints;
- safe query/validation failures;
- cancellation; and
- proof that no freshness check or vendor/importer call occurs.

The planner must name the exact test files and fixtures.

## Validation

The planner must provide the exact focused test command. Implementation also requires:

- `dotnet format roles/FWO.sln` for changed C# files;
- `dotnet build --configuration Debug roles/FWO.sln`;
- `dotnet test roles/tests-unit/files/FWO.Test/FWO.Test.csproj`; and
- more than 80% line coverage for new production code.

## Acceptance criteria

- One call returns one validated immutable graph from one latest successful import.
- The graph belongs to the requested management and gateway.
- Missing or inconsistent data produces a safe explicit failure.
- Cancellation propagates.
- No customer-specific behavior or production strategy is activated.

## Out of scope

- Customer-document analysis.
- Workflow-element or zone resolution.
- Recipe selection or execution.
- Check Point request rendering or communication.
- Position persistence or workflow calls.
