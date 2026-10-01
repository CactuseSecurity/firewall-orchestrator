# Future vendor planning contract — not current workload

This note reserves the calculation seams that future Fortinet, Azure Firewall, or other vendor teams may implement. It is not a scheduled work package. The current Check Point delivery must not add vendor-specific schema, clients, configuration choices, or algorithms for those platforms.

## Required extension components

A future vendor planning integration supplies:

1. **A positioning method identifier and complete capability registration.**
2. **A functional-target resolver** that maps the request and configuration to one permitted policy container.
3. **A policy snapshot reader** that translates current vendor policy order into the WP-04 contract or an approved extension of it.
4. **A pure placement planner** that returns a WP-01 plan or typed failure.

WP-08 discovers these components through one exhaustive registry. Missing or partial registration returns `PositioningMethodNotImplemented` before vendor policy data is read.

Execution, verification, workflow integration, and presentation are separate future projects. This contract neither defines nor schedules them.

## Fortinet team boundary

The future Fortinet planning implementation owns:

- the authoritative source for explicit `global-label` data;
- ADOM, package, and policy functional-target resolution;
- a read-only Fortinet/FortiManager policy snapshot adapter;
- `SECTION_LABEL` calculation based on the first contiguous explicit-label block;
- a separately approved policy for a missing label group, if the product later requires one.

The Check Point packages must not add placeholder columns, importer logic, or label behavior for Fortinet. Any shared snapshot change requires an explicit cross-vendor design decision.

## Azure team boundary

The future Azure planning implementation owns:

- mapping zone pairs to Rule Collection Groups and Rule Collections;
- collection-type and priority rules;
- Azure resource identities and concurrency tokens needed to describe a current snapshot;
- a read-only Azure policy snapshot adapter;
- an Azure-specific pure placement planner and configuration validation.

Azure has no Check Point Section concept. Its planner should implement the shared result contract directly rather than emulate a Check Point rulebase.

## Compatibility tests for a future vendor

Before a vendor planning method is registered, its team must prove:

- no component falls back to another vendor or positioning method;
- incomplete registration produces `PositioningMethodNotImplemented`;
- the planner is deterministic over a fixed snapshot;
- one invocation reads at most one logical snapshot;
- the planning path has no state-changing dependency;
- failures use the shared typed result contract.

