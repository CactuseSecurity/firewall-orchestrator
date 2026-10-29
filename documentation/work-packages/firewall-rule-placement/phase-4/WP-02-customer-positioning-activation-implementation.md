# Phase 4 WP-02 implementation: customer positioning activation

## Implemented behavior

Selecting “Check Point: end of layer-guard zone-pair section” now uses the existing implementation-task creation trigger to calculate and persist a semantic Check Point position. The calculation resolves one canonical effective source/destination range input from Rule A plus its management-specific ticket task slice, loads topology and guards at the same successful import, resolves one designated zone per side, selects the customer recipe, and persists its end-of-Section result.

The workflow retains the transient or merged parent Rule A supplied by the creation event. Persisted ticket tasks are separate context used for same-ticket object and group state. Cross-management tasks are excluded before calculation.

## Capability boundary

- `CheckPointGraphRulePositioningStrategy`: Check Point type `9` plus `DefaultEndOfRulebase` only.
- `CheckPointRulePositioningStrategy`: Check Point type `9` plus `CheckPointLayerGuardEndOfZonePairSection` only.
- Unsupported device types or algorithms receive no fallback.
- Generic positioning does not load tickets, groups, guards, zone matrices, or zones.

## Deployment boundary

Localized labels and help are in the rerunnable `fworch-texts.sql`. No schema or Hasura change is needed, so the existing `9.8.1.sql` and `product_version` are unchanged.

## Validation record

- Scoped `dotnet format --verify-no-changes` passed for every changed C# file.
- Debug solution build passed with zero warnings and zero errors.
- The combined positioning/settings regression selection passed 545 tests with two environment-dependent skips.
- The final complete C# suite passed 8,822 tests with 30 expected skips.
- Focused activation coverage passed 177 tests; line coverage was 100% for the customer strategy, catalog, and workflow caller and 87.4% for the effective-range resolver.
- `git diff --check` passed.
- Disposable-VM installation validation was not executable in this session because the FWO VM MCP tools were unavailable. No VM was created.
